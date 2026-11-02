# Documentação técnica do backend — TechHelpSolutions

> Documento elaborado a partir da leitura integral do código-fonte existente em `backend/`, incluindo configuração, models, DTOs, contexto do Entity Framework Core, migration, services e controllers. A análise retrata o estado do código em 22/09/2026. Não foram presumidos dados já existentes no banco nem comportamentos do frontend.

## 1. Visão geral

O backend implementa uma API REST para um sistema simples de suporte técnico (help desk). O domínio central é o **chamado**: um usuário abre uma solicitação, um técnico pode assumi-la, alterar seu status, editar seus dados e finalizá-la, e usuários autenticados podem registrar comentários conforme as regras de acesso existentes. Administradores podem excluir chamados. A API também disponibiliza catálogos de categorias, prioridades e status e operações básicas de usuários e autenticação.

O sistema diferencia três perfis efetivamente usados na autorização:

- `Usuario`: cria chamados, lista seus próprios chamados, consulta os próprios detalhes, edita os próprios chamados e comenta neles;
- `Tecnico`: lista atendimentos atribuídos a si, assume, altera, atualiza status e finaliza chamados;
- `Admin`: pode excluir chamados e, por estar apenas autenticado, acessar endpoints que exigem `[Authorize]` sem papel específico.

No cadastro também é aceito o texto `Gestor`, mas ele é convertido e armazenado como `Admin`. Não existe papel `Gestor` persistido por esse fluxo nem regra de autorização com esse nome.

Não existe uma camada de aplicação completa ou padrão de repositórios. Os controllers acessam `AppDbContext` diretamente em muitas operações; apenas autenticação e parte das operações de chamados foram extraídas para services.

## 2. Tecnologias, frameworks e versões

### 2.1 Plataforma e dependências diretas

| Tecnologia/pacote | Versão declarada | Uso encontrado |
|---|---:|---|
| .NET / ASP.NET Core | `net9.0` | Plataforma da API e controllers MVC |
| Entity Framework Core SQL Server | `9.0.0` | ORM e provedor do SQL Server |
| Entity Framework Core Tools | `9.0.0` | Criação/manutenção de migrations |
| ASP.NET Core JWT Bearer | `9.0.0` | Autenticação de bearer tokens JWT |
| System.IdentityModel.Tokens.Jwt | `8.17.0` | Criação e escrita dos tokens JWT |
| Swashbuckle.AspNetCore | `9.0.0` | Geração da especificação e interface Swagger |
| Swashbuckle SwaggerGen / SwaggerUI | `9.0.0` | Componentes explicitamente referenciados do Swagger |
| Microsoft.AspNetCore.OpenApi | `9.0.14` | Serviços OpenAPI registrados |
| AutoMapper.Extensions.Microsoft.DependencyInjection | `12.0.1` | Registrado na injeção de dependência, mas sem profiles ou chamadas de mapeamento no código |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | `9.0.0` | Referenciado, mas ASP.NET Core Identity não é configurado nem utilizado |
| Microsoft.Extensions.Identity.Stores | `9.0.0` | Referenciado, mas não utilizado diretamente |

O projeto habilita `Nullable` e `ImplicitUsings`. Não há `global.json`; portanto, o SDK exato usado depende do ambiente. Na verificação desta análise, o build foi executado com SDK .NET `10.0.401`, compilando o alvo `net9.0` contra o runtime ASP.NET Core `9.0.14` instalado.

### 2.2 Recursos de infraestrutura

- Banco relacional: Microsoft SQL Server, instância configurada como `DESKTOP-GU\SQLEXPRESS`.
- Documentação interativa: Swagger somente em ambiente `Development`.
- Serialização: `System.Text.Json`, com `ReferenceHandler.IgnoreCycles`.
- Injeção de dependência nativa do ASP.NET Core, com `AppDbContext`, `ChamadoService` e `AuthService` registrados como scoped.
- CORS: política `AllowAll`, permitindo qualquer origem, método e cabeçalho.
- HTTPS redirection habilitado.

## 3. Arquitetura e organização

O backend é um único projeto Web API, sem projetos separados por camada.

```text
backend/
├── Controllers/
│   ├── AuthController.cs
│   ├── CatalogosController.cs
│   ├── ChamadosController.cs
│   └── UsuariosController.cs
├── Data/
│   ├── AppDbContext.cs
│   └── Dtos/
│       ├── AtualizarChamadoDTO.cs
│       ├── AtualizarStatusDTO.cs
│       ├── AtualizarUsuarioDTO.cs
│       ├── AuthLoginResponseDTO.cs
│       ├── ChamadoResponseDTO.cs
│       ├── ComentarioResponseDTO.cs
│       ├── CriarChamadoDTO.cs
│       ├── CriarComentarioDTO.cs
│       ├── ItemCatalogoDTO.cs
│       ├── LoginDTO.cs
│       ├── UsuarioDTO.cs
│       ├── UsuarioResponseDTO.cs
│       └── UsuarioResumoDTO.cs
├── Migrations/
│   ├── 20260912153825_InitialCreate.cs
│   ├── 20260912153825_InitialCreate.Designer.cs
│   └── AppDbContextModelSnapshot.cs
├── Models/
│   ├── Categoria.cs
│   ├── Chamado.cs
│   ├── Comentario.cs
│   ├── Prioridade.cs
│   ├── Status.cs
│   └── Usuario.cs
├── Properties/launchSettings.json
├── Services/
│   ├── AuthService.cs
│   └── ChamadoService.cs
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
├── UsuariosApi.csproj
└── UsuariosApi.slnx
```

Responsabilidades observadas:

- **Controllers**: expõem HTTP, aplicam autorização, fazem parte das validações e projetam respostas.
- **Services**: `AuthService` autentica e gera JWT; `ChamadoService` persiste criação/edição/status/comentário.
- **Data**: contém o `DbContext` e, apesar do nome da pasta, os DTOs usam namespace `TechHelpSolutions.DTOs`.
- **Models**: entidades persistidas pelo EF Core.
- **Migrations**: definição versionada do schema inicial.

## 4. Modelo de domínio e entidades

### 4.1 `Usuario`

Representa tanto solicitantes quanto técnicos e administradores.

| Atributo C# | Tipo | Papel |
|---|---|---|
| `Id` | `int` | Identificador |
| `Nome` | `string` | Nome do usuário |
| `Email` | `string` | Credencial de login |
| `Senha` | `string` | Senha, armazenada sem hash no estado atual |
| `Tipo` | `string` | Papel usado como role JWT |
| `DataCriacao` | `DateTime` | Inicializado com `DateTime.Now` ao criar a entidade |
| `ChamadosAbertos` | `ICollection<Chamado>` | Navegação para chamados em que é solicitante |
| `ChamadosAtendidos` | `ICollection<Chamado>` | Navegação para chamados em que é técnico |

Não existe entidade separada para técnico ou administrador. O campo textual `Tipo` diferencia os perfis.

### 4.2 `Chamado`

| Atributo C# | Tipo | Papel |
|---|---|---|
| `Id` | `int` | Identificador |
| `Titulo` | `string` | Título da solicitação |
| `Descricao` | `string` | Detalhamento |
| `UsuarioId` | `int` | FK do solicitante |
| `Usuario` | `Usuario` | Navegação do solicitante |
| `TecnicoId` | `int?` | FK opcional do técnico responsável |
| `Tecnico` | `Usuario` | Navegação do técnico; a propriedade não está marcada como nullable, embora sua FK seja opcional |
| `CategoriaId` | `int` | FK de categoria |
| `Categoria` | `Categoria` | Navegação de categoria |
| `PrioridadeId` | `int` | FK de prioridade |
| `Prioridade` | `Prioridade` | Navegação de prioridade |
| `StatusId` | `int` | FK de status |
| `Status` | `Status` | Navegação de status |
| `DataAbertura` | `DateTime` | Inicializado com `DateTime.Now` |
| `DataFechamento` | `DateTime?` | Preenchido ao finalizar ou ao definir status 3 pelo service |
| `Comentarios` | `ICollection<Comentario>` | Comentários associados |

### 4.3 `Comentario`

| Atributo C# | Tipo | Papel |
|---|---|---|
| `Id` | `int` | Identificador |
| `ChamadoId` | `int` | FK do chamado |
| `Chamado` | `Chamado` | Navegação para o chamado |
| `UsuarioId` | `int` | FK do autor |
| `Usuario` | `Usuario` | Navegação para o autor |
| `Mensagem` | `string` | Conteúdo textual |
| `DataEnvio` | `DateTime` | Inicializado com `DateTime.Now` |

### 4.4 `Categoria`, `Prioridade` e `Status`

As três entidades têm a mesma forma:

| Atributo C# | Tipo | Papel |
|---|---|---|
| `Id` | `int` | Identificador |
| `Nome` | `string` | Nome exibível do item |
| `Chamados` | `ICollection<Chamado>` | Navegação para os chamados que usam o item |

Não há enumerações. Os valores são linhas de banco. Contudo, o código assume semanticamente que `StatusId = 1` significa “Aberto”, `2` significa “Em andamento” e `3` significa “Finalizado”, sem seed ou constraint que assegure essa correspondência.

## 5. Relacionamentos

```text
Usuario (solicitante) 1 ─── N Chamado
Usuario (técnico)     1 ─── N Chamado   [Chamado.TecnicoId opcional]
Categoria             1 ─── N Chamado
Prioridade            1 ─── N Chamado
Status                1 ─── N Chamado
Chamado               1 ─── N Comentario
Usuario (autor)       1 ─── N Comentario
```

Regras de exclusão no banco:

- excluir usuário referenciado como solicitante ou técnico é bloqueado (`Restrict`);
- excluir categoria, prioridade ou status apaga em cascata os chamados ligados e, indiretamente, seus comentários;
- excluir chamado apaga comentários em cascata;
- excluir usuário autor de comentário apaga seus comentários em cascata.

Não existe endpoint para excluir usuários ou itens de catálogo, portanto várias dessas regras só seriam acionadas por acesso direto ao banco ou por código futuro.

## 6. Estrutura do banco de dados

O schema abaixo é o produzido pela migration `InitialCreate` e confirmado pelo snapshot do EF Core.

### 6.1 Tabela `Usuarios`

| Campo | SQL Server | Nulo? | Chave/constraint |
|---|---|---:|---|
| `Id` | `int IDENTITY(1,1)` | não | PK `PK_Usuarios` |
| `Nome` | `nvarchar(max)` | não | nenhuma adicional |
| `Email` | `nvarchar(max)` | não | sem índice único |
| `Senha` | `nvarchar(max)` | não | nenhuma adicional |
| `Tipo` | `nvarchar(max)` | não | sem CHECK |
| `DataCriacao` | `datetime2` | não | sem default SQL |

Os valores de data são fornecidos pela aplicação; não existe `DEFAULT GETDATE()` no banco.

### 6.2 Tabela `Chamados`

| Campo | SQL Server | Nulo? | Chave/constraint |
|---|---|---:|---|
| `Id` | `int IDENTITY(1,1)` | não | PK `PK_Chamados` |
| `Titulo` | `nvarchar(max)` | não | nenhuma adicional |
| `Descricao` | `nvarchar(max)` | não | nenhuma adicional |
| `UsuarioId` | `int` | não | FK → `Usuarios(Id)`, `Restrict`; índice não único |
| `TecnicoId` | `int` | sim | FK → `Usuarios(Id)`, `Restrict`; índice não único |
| `CategoriaId` | `int` | não | FK → `Categorias(Id)`, cascade; índice não único |
| `PrioridadeId` | `int` | não | FK → `Prioridades(Id)`, cascade; índice não único |
| `StatusId` | `int` | não | FK → `Status(Id)`, cascade; índice não único |
| `DataAbertura` | `datetime2` | não | sem default SQL |
| `DataFechamento` | `datetime2` | sim | nenhuma adicional |

### 6.3 Tabela `Comentarios`

| Campo | SQL Server | Nulo? | Chave/constraint |
|---|---|---:|---|
| `Id` | `int IDENTITY(1,1)` | não | PK `PK_Comentarios` |
| `ChamadoId` | `int` | não | FK → `Chamados(Id)`, cascade; índice não único |
| `UsuarioId` | `int` | não | FK → `Usuarios(Id)`, cascade; índice não único |
| `Mensagem` | `nvarchar(max)` | não | nenhuma adicional |
| `DataEnvio` | `datetime2` | não | sem default SQL |

### 6.4 Tabelas de catálogo

`Categorias`, `Prioridades` e `Status` têm, cada uma:

| Campo | SQL Server | Nulo? | Chave/constraint |
|---|---|---:|---|
| `Id` | `int IDENTITY(1,1)` | não | PK da respectiva tabela |
| `Nome` | `nvarchar(max)` | não | sem índice único |

### 6.5 Constraints ausentes relevantes

Não há no schema:

- unicidade de e-mail;
- limites de tamanho de strings;
- validação de formato de e-mail;
- CHECK para tipos de usuário, status ou datas;
- índices além dos índices de FKs criados pelo EF;
- valores default no SQL;
- controle de concorrência (`rowversion`);
- tabelas/colunas de auditoria além das datas de criação, abertura, envio e fechamento.

## 7. Conexão com SQL Server e persistência

`appsettings.json` define:

```text
Server=DESKTOP-GU\SQLEXPRESS;Database=TechHelpSolutions;Trusted_Connection=True;TrustServerCertificate=True
```

Significado:

- conecta à instância local nomeada `SQLEXPRESS` da máquina `DESKTOP-GU`;
- usa o banco `TechHelpSolutions`;
- usa autenticação integrada do Windows (`Trusted_Connection=True`), sem usuário/senha SQL no arquivo;
- aceita o certificado do servidor sem validar sua cadeia (`TrustServerCertificate=True`).

Em `Program.cs`, a configuração lê `ConnectionStrings:UsuarioConnection`; se não existir, a aplicação lança `InvalidOperationException` ainda na inicialização. Em seguida, `AddDbContext<AppDbContext>(options => options.UseSqlServer(connString))` configura o provider. Por ser scoped, normalmente existe uma instância de contexto por requisição HTTP.

`AppDbContext` expõe seis `DbSet`: `Usuarios`, `Chamados`, `Categorias`, `Prioridades`, `Status` e `Comentarios`. O EF usa convenções para a maior parte do modelo; Fluent API é usada apenas para desambiguar os dois relacionamentos entre `Chamado` e `Usuario` e aplicar `DeleteBehavior.Restrict`.

As escritas seguem o padrão `Add`/alteração rastreada/`Remove` e `SaveChangesAsync`. As consultas de leitura geralmente usam `AsNoTracking`, `Include` e projeção para DTO. Não existe repositório, unit of work próprio, stored procedure, SQL manual nem transação explícita.

### 7.1 Migrations

Existe uma única migration, `20260912153825_InitialCreate`, com métodos `Up` e `Down`, seu arquivo Designer e o snapshot do modelo. Ela cria e remove as seis tabelas, FKs e índices descritos acima.

Não há chamada a `Database.Migrate()` em `Program.cs`; portanto, iniciar a API **não aplica migrations automaticamente**. Também não existe script de criação/implantação ou seed. A aplicação pressupõe que o banco, schema e registros de catálogo necessários já tenham sido preparados externamente, por exemplo com `dotnet ef database update`.

## 8. DTOs

Os nomes JSON seguem por padrão camelCase, embora as propriedades C# sejam PascalCase.

| DTO | Campos | Função |
|---|---|---|
| `LoginDTO` | `Email: string`, `Senha: string` | Credenciais de login |
| `AuthLoginResponseDTO` | `Token: string`, `Usuario: UsuarioResumoDTO` | Resposta de autenticação |
| `UsuarioDTO` | `Nome`, `Email`, `Senha`, `Tipo` (todos `string`) | Cadastro de usuário |
| `AtualizarUsuarioDTO` | `Nome: string`, `Email: string`, `Senha: string?`, `Tipo: string` | Edição de usuário; senha é opcional no tipo |
| `UsuarioResponseDTO` | `Id`, `Nome`, `Email`, `Tipo`, `DataCriacao` | Resposta completa sem senha |
| `UsuarioResumoDTO` | `Id`, `Nome`, `Email`, `Tipo` | Usuário aninhado em outras respostas |
| `CriarChamadoDTO` | `Titulo`, `Descricao`, `CategoriaId`, `PrioridadeId`, `UsuarioId` | Abertura; `UsuarioId` recebido é sobrescrito pelo ID do token |
| `AtualizarChamadoDTO` | `Titulo`, `Descricao`, `CategoriaId`, `PrioridadeId` | Edição do conteúdo do chamado |
| `AtualizarStatusDTO` | `StatusId: int` | Troca de status |
| `ChamadoResponseDTO` | IDs, conteúdo, datas, usuário, técnico, categoria, prioridade, status e comentários opcionais | Resposta projetada de chamado |
| `CriarComentarioDTO` | `Mensagem: string` | Criação de comentário |
| `ComentarioResponseDTO` | `Id`, `ChamadoId`, `Mensagem`, `DataEnvio`, `Usuario` | Comentário projetado sem navegações cíclicas |
| `ItemCatalogoDTO` | `Id`, `Nome` | Resposta resumida de categoria, prioridade ou status |

Não existem atributos como `[Required]`, `[EmailAddress]`, `[StringLength]` ou validadores externos. Como os controllers usam `[ApiController]`, erros de parsing/model binding e falhas de validação reconhecidas pelo framework podem gerar `400` automaticamente. Propriedades reference-type não anuláveis podem ser tratadas como implicitamente obrigatórias pelo MVC, mas isso não substitui validações de formato, tamanho ou conteúdo.

## 9. Services

### 9.1 `AuthService`

`Login(LoginDTO)` procura sincronicamente um usuário cuja combinação `Email` + `Senha` seja exatamente igual à informada. Se não encontrar, retorna `null`. Se encontrar, cria JWT HMAC-SHA256 válido por duas horas, contendo:

- `ClaimTypes.Name`: nome;
- `ClaimTypes.Email`: e-mail;
- `ClaimTypes.Role`: tipo;
- `UserId`: ID convertido para texto.

Retorna o token e um resumo do usuário. Não há refresh token, revogação, logout no servidor, lockout, hash de senha ou Identity.

### 9.2 `ChamadoService`

- `CriarChamado`: instancia o chamado, fixa `StatusId = 1`, persiste e devolve a entidade.
- `AtualizarStatus`: localiza pelo ID, altera `StatusId`; se for `3`, registra `DataFechamento = DateTime.Now`; lança `Exception` se não encontrar.
- `AtualizarChamado`: localiza e substitui título, descrição, categoria e prioridade; lança `Exception` se não encontrar.
- `CriarComentario`: confirma existência de chamado e usuário, cria comentário com data inicializada pelo model e persiste; lança `Exception` em ausências.

O service não valida a existência prévia de categoria, prioridade ou status; FKs inválidas falham no `SaveChangesAsync`. Ele também não restringe transições de status.

## 10. Controllers e endpoints

Base local configurada: `http://localhost:5266` ou `https://localhost:7253` no profile HTTPS. Endpoints protegidos esperam `Authorization: Bearer <token>`.

### 10.1 `AuthController`

#### `POST /api/auth/login`

- **Acesso:** anônimo (não possui `[Authorize]`).
- **Body:** `LoginDTO`.
- **Finalidade:** validar e-mail/senha e emitir JWT.
- **Sucesso:** `200 OK`, `AuthLoginResponseDTO`.
- **Falha explícita:** `401 Unauthorized` com texto `Email ou senha invalidos`.

```json
{
  "email": "ana@exemplo.com",
  "senha": "senha123"
}
```

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "usuario": {
    "id": 1,
    "nome": "Ana",
    "email": "ana@exemplo.com",
    "tipo": "Usuario"
  }
}
```

### 10.2 `UsuariosController`

#### `POST /api/usuarios`

- **Acesso:** anônimo (`[AllowAnonymous]`).
- **Body:** `UsuarioDTO`.
- **Finalidade:** cadastrar usuário.
- **Regras:** os quatro campos não podem ser vazios; tipo deve ser `Usuario`, `Tecnico`, `Admin` ou `Gestor`, ignorando maiúsculas/minúsculas; e-mail é `Trim().ToLower()`; nome recebe `Trim()`; `Gestor` vira `Admin`; duplicidade de e-mail é consultada sem diferenciar caixa.
- **Sucesso:** `200 OK`, `UsuarioResponseDTO`.
- **Falhas:** `400` para campos vazios/tipo inválido; `409 Conflict` para e-mail já encontrado.

```json
{
  "nome": " Ana Silva ",
  "email": "ANA@EXEMPLO.COM",
  "senha": "senha123",
  "tipo": "Usuario"
}
```

```json
{
  "id": 1,
  "nome": "Ana Silva",
  "email": "ana@exemplo.com",
  "tipo": "Usuario",
  "dataCriacao": "2026-09-22T14:30:00.0000000-03:00"
}
```

#### `GET /api/usuarios`

- **Acesso:** qualquer autenticado.
- **Parâmetros/body:** nenhum.
- **Finalidade:** listar todos os usuários por nome.
- **Retorno:** `200 OK`, array de `UsuarioResponseDTO`; pode ser `[]`.
- **Observação:** não há restrição a administradores.

#### `GET /api/usuarios/me`

- **Acesso:** qualquer autenticado.
- **Finalidade:** retornar o usuário cujo ID está no claim `UserId`.
- **Retorno:** `200 OK`, `UsuarioResponseDTO`.
- **Falhas:** `401` se o claim não for inteiro; `404` se o usuário não existir mais.

#### `PUT /api/usuarios/{id}`

- **Acesso:** autenticado; `Admin` edita qualquer ID, outros perfis apenas o próprio.
- **Path:** `id: int`.
- **Body:** `AtualizarUsuarioDTO`.
- **Finalidade:** substituir nome e e-mail; substituir senha somente se não vazia; substituir tipo somente se o solicitante for `Admin`.
- **Retorno:** `200 OK`, `UsuarioResponseDTO`.
- **Falhas:** `401`, `403` ou `404` conforme claim, propriedade e existência.
- **Limitação:** não repete as validações do cadastro, não normaliza e-mail/nome, não verifica duplicidade e não valida o tipo fornecido pelo admin.

```json
{
  "nome": "Ana Souza",
  "email": "ana.souza@exemplo.com",
  "senha": null,
  "tipo": "Usuario"
}
```

Não existem endpoints para obter usuário por ID, excluir usuário ou criar/listar papéis.

### 10.3 `CatalogosController`

Todos exigem apenas usuário autenticado e retornam `200 OK` com array de `{ "id": int, "nome": string }`:

| Método e rota | Ordenação | Finalidade |
|---|---|---|
| `GET /api/catalogos/categorias` | por nome | Listar categorias |
| `GET /api/catalogos/prioridades` | por ID | Listar prioridades |
| `GET /api/catalogos/status` | por ID | Listar status |

Exemplo:

```json
[
  { "id": 1, "nome": "Baixa" },
  { "id": 2, "nome": "Média" },
  { "id": 3, "nome": "Alta" }
]
```

Não há CRUD HTTP dos catálogos nem seed no código.

### 10.4 `ChamadosController`

#### `POST /api/chamados`

- **Acesso:** role `Usuario`.
- **Body:** `CriarChamadoDTO`.
- **Finalidade:** abrir chamado com status fixo `1`.
- **Regra de identidade:** qualquer `usuarioId` do JSON é ignorado e sobrescrito pelo claim `UserId`.
- **Retorno:** `200 OK` com a entidade `Chamado` recém-criada, não com `ChamadoResponseDTO`. Como as navegações não são carregadas, normalmente aparecem nulas; a forma pode diferir dos endpoints de consulta.
- **Falhas:** `401` por token/claim; violações de FK ou banco não são tratadas localmente.

```json
{
  "titulo": "Computador não liga",
  "descricao": "Ao pressionar o botão, nenhum LED acende.",
  "categoriaId": 2,
  "prioridadeId": 3,
  "usuarioId": 999
}
```

Resposta típica (campos de navegação dependem da serialização da entidade):

```json
{
  "id": 10,
  "titulo": "Computador não liga",
  "descricao": "Ao pressionar o botão, nenhum LED acende.",
  "usuarioId": 1,
  "usuario": null,
  "tecnicoId": null,
  "tecnico": null,
  "categoriaId": 2,
  "categoria": null,
  "prioridadeId": 3,
  "prioridade": null,
  "statusId": 1,
  "status": null,
  "dataAbertura": "2026-09-22T14:35:00.0000000-03:00",
  "dataFechamento": null,
  "comentarios": null
}
```

#### `GET /api/chamados`

- **Acesso:** qualquer autenticado.
- **Query opcional:** `status: int?`, `prioridade: int?`, `categoria: int?`.
- **Finalidade:** listar todos os chamados, com filtros combináveis por igualdade.
- **Retorno:** `200 OK`, array de `ChamadoResponseDTO`, decrescente por abertura; `comentarios` é `null`.
- **Observação de acesso:** usuários comuns também conseguem listar todos os chamados por este endpoint; a restrição aos próprios existe apenas em `/meus` e no detalhe por ID.

Exemplo: `GET /api/chamados?status=2&prioridade=3&categoria=2`.

#### `GET /api/chamados/meus`

- **Acesso:** role `Usuario`.
- **Finalidade:** listar chamados cujo `UsuarioId` seja o ID do token.
- **Retorno:** `200 OK`, array de `ChamadoResponseDTO`, sem comentários e em ordem decrescente de abertura.

#### `GET /api/chamados/meus-atendimentos`

- **Acesso:** role `Tecnico`.
- **Finalidade:** listar chamados cujo `TecnicoId` seja o ID do token.
- **Retorno:** `200 OK`, array de `ChamadoResponseDTO`, sem comentários.

#### `GET /api/chamados/{id}`

- **Acesso:** qualquer autenticado.
- **Path:** `id: int`.
- **Finalidade:** obter chamado e comentários.
- **Autorização adicional:** se o role for `Usuario`, só permite quando ele é o solicitante. Técnico e admin não sofrem teste adicional.
- **Retorno:** `200 OK`, `ChamadoResponseDTO` com comentários ordenados por envio.
- **Falhas:** `404`, `401` ou `403`.

Exemplo abreviado de resposta:

```json
{
  "id": 10,
  "titulo": "Computador não liga",
  "descricao": "Ao pressionar o botão, nenhum LED acende.",
  "usuarioId": 1,
  "tecnicoId": 4,
  "categoriaId": 2,
  "prioridadeId": 3,
  "statusId": 2,
  "dataAbertura": "2026-09-22T14:35:00-03:00",
  "dataFechamento": null,
  "usuario": { "id": 1, "nome": "Ana", "email": "ana@exemplo.com", "tipo": "Usuario" },
  "tecnico": { "id": 4, "nome": "Bruno", "email": "bruno@exemplo.com", "tipo": "Tecnico" },
  "categoria": { "id": 2, "nome": "Hardware" },
  "prioridade": { "id": 3, "nome": "Alta" },
  "status": { "id": 2, "nome": "Em andamento" },
  "comentarios": [
    {
      "id": 7,
      "chamadoId": 10,
      "mensagem": "Vou verificar a fonte.",
      "dataEnvio": "2026-09-22T15:00:00-03:00",
      "usuario": { "id": 4, "nome": "Bruno", "email": "bruno@exemplo.com", "tipo": "Tecnico" }
    }
  ]
}
```

#### `PUT /api/chamados/{id}/status`

- **Acesso:** role `Tecnico`.
- **Path/body:** `id: int`; `{ "statusId": 3 }`.
- **Finalidade:** alterar livremente o status; se `statusId == 3`, preencher fechamento.
- **Retorno:** `204 No Content`.
- **Limitações:** não exige que o técnico seja o responsável, não valida transição, não limpa `DataFechamento` se o status sair de 3 e não trata a exceção de chamado inexistente; uma FK inválida também pode resultar em erro não tratado.

#### `PUT /api/chamados/{id}/assumir`

- **Acesso:** role `Tecnico`.
- **Body:** nenhum.
- **Finalidade:** definir o técnico como o ID do token e fixar status `2`.
- **Retorno:** `204 No Content`.
- **Falhas:** `404` ou `401`.
- **Limitação:** pode sobrescrever outro técnico já atribuído; não checa o estado atual.

#### `PUT /api/chamados/{id}/finalizar`

- **Acesso:** role `Tecnico`.
- **Body:** nenhum.
- **Finalidade:** fixar status `3` e `DataFechamento = DateTime.Now`.
- **Regras:** precisa ter sido assumido e somente o técnico atualmente atribuído pode finalizar.
- **Retorno:** `204 No Content`.
- **Falhas:** `404`; `400` se não assumido; `401`; `403` se atribuído a outro técnico.

#### `PUT /api/chamados/{id}`

- **Acesso:** roles `Usuario` ou `Tecnico`.
- **Body:** `AtualizarChamadoDTO`.
- **Finalidade:** substituir título, descrição, categoria e prioridade.
- **Regras:** título/descrição não vazios e IDs maiores que zero; usuário comum só edita chamado próprio. Um técnico pode editar qualquer chamado, mesmo não atribuído a ele.
- **Retorno:** `200 OK`, `ChamadoResponseDTO` sem comentários.
- **Falhas:** `404`, `401`, `403`, `400`; FKs inexistentes não são antecipadamente validadas.

```json
{
  "titulo": "Computador não liga após queda de energia",
  "descricao": "Testado em outra tomada, sem resposta.",
  "categoriaId": 2,
  "prioridadeId": 3
}
```

#### `POST /api/chamados/{chamadoId}/comentarios`

- **Acesso:** qualquer autenticado.
- **Path/body:** `chamadoId: int`; `{ "mensagem": "Texto do comentário" }`.
- **Finalidade:** comentar usando o autor identificado no token.
- **Autorização adicional:** um `Usuario` só comenta no próprio chamado; técnico/admin podem comentar em qualquer chamado.
- **Retorno:** `200 OK` com a entidade `Comentario`, não `ComentarioResponseDTO`; navegações normalmente estão nulas.
- **Falhas:** `404`, `401`, `403`; exceções do service são convertidas em `400` com a mensagem.
- **Limitação:** não há teste explícito de mensagem vazia/branca.

Resposta típica:

```json
{
  "id": 7,
  "chamadoId": 10,
  "chamado": null,
  "usuarioId": 4,
  "usuario": null,
  "mensagem": "Vou verificar a fonte.",
  "dataEnvio": "2026-09-22T15:00:00-03:00"
}
```

#### `GET /api/chamados/{id}/comentarios`

- **Acesso:** qualquer autenticado.
- **Finalidade:** listar comentários em ordem crescente de envio.
- **Autorização adicional:** usuário comum apenas no próprio chamado.
- **Retorno:** `200 OK`, array de `ComentarioResponseDTO`.
- **Falhas:** `404`, `401`, `403`.

#### `DELETE /api/chamados/{id}`

- **Acesso:** role `Admin`.
- **Finalidade:** excluir chamado; o controller carrega e remove explicitamente os comentários antes de remover o chamado, embora a FK também tenha cascade.
- **Retorno:** `204 No Content`.
- **Falha:** `404`.

## 11. Matriz CRUD

| Recurso | Create | Read | Update | Delete |
|---|---|---|---|---|
| Usuário | sim | lista e próprio perfil | sim, por ID | não |
| Chamado | sim | lista geral, próprios, atendimentos e detalhe | conteúdo, status, assumir e finalizar | sim, admin |
| Comentário | sim | por chamado | não | apenas como efeito da exclusão do chamado |
| Categoria | não | lista | não | não |
| Prioridade | não | lista | não | não |
| Status (catálogo) | não | lista | não | não |
| Sessão/token | login | não | não há refresh | não há logout/revogação |

## 12. Regras de negócio e validações

Regras efetivamente implementadas:

- cadastro exige nome, e-mail, senha e tipo não brancos;
- cadastro aceita quatro textos de tipo e converte `Gestor` em `Admin`;
- cadastro normaliza o e-mail e impede duplicidade por consulta da aplicação;
- somente `Usuario` abre chamado; o solicitante vem do token;
- chamado novo recebe status ID 1;
- assumir chamado define técnico e status ID 2;
- finalizar exige atribuição ao técnico logado, define status ID 3 e data de fechamento;
- atualização genérica para status 3 também define data de fechamento;
- usuário comum só consulta detalhe, edita ou comenta no próprio chamado;
- somente admin exclui chamado;
- edição de chamado exige textos não brancos e IDs de categoria/prioridade positivos.

Validações **não** implementadas:

- formato de e-mail, força/tamanho/hash da senha ou tamanhos máximos;
- unicidade de e-mail garantida pelo banco;
- validação uniforme no update de usuário;
- existência amigavelmente verificada de todas as FKs antes de salvar;
- conteúdo não vazio do comentário por regra explícita;
- consistência entre tipo de usuário e atribuição como técnico/solicitante;
- fluxo permitido de estados;
- impedimento de reatribuição de chamado;
- autorização do técnico apenas aos chamados que assumiu, exceto na finalização;
- paginação, busca textual, rate limiting, auditoria ou soft delete.

## 13. Autenticação e autorização

O middleware autentica JWT Bearer. A validação verifica:

- assinatura HMAC com a chave simétrica fixa;
- expiração (`ValidateLifetime = true`);
- chave emissora (`ValidateIssuerSigningKey = true`).

Não valida issuer nem audience. `RequireHttpsMetadata` está desabilitado. A mesma chave literal aparece em `Program.cs` e `AuthService.cs`. O token expira em duas horas.

O pipeline chama `UseAuthentication()` antes de `UseAuthorization()`, e os atributos dos controllers definem as políticas. A role é o valor textual de `Usuario.Tipo`; por isso, valores inesperados ou com capitalização não canônica podem não satisfazer os atributos de role. O cadastro valida ignorando caixa, mas preserva a grafia fornecida (exceto `Gestor`), o que pode gerar essa inconsistência.

Riscos/limitações concretas do estado atual:

- senhas são comparadas e armazenadas em texto puro;
- chave JWT está hard-coded e versionada;
- endpoint de cadastro anônimo permite solicitar inclusive `Admin` ou `Gestor` (convertido em admin);
- qualquer autenticado pode listar todos os usuários e todos os chamados;
- não há refresh/revogação de token ou alteração segura/recuperação de senha;
- CORS aceita qualquer origem.

## 14. Tratamento de erros

Há tratamento localizado via resultados MVC:

- `400 Bad Request`: validações manuais, finalização sem técnico e exceções capturadas ao comentar;
- `401 Unauthorized`: credenciais inválidas, token ausente/inválido/expirado ou claim `UserId` inválido;
- `403 Forbidden`: usuário autenticado sem role/permissão ou acesso negado pelas verificações de propriedade;
- `404 Not Found`: recurso não localizado nos endpoints que o verificam;
- `409 Conflict`: e-mail duplicado no cadastro;
- `204 No Content`: atualizações sem representação e exclusão.

Não existe middleware global de exceções, `ProblemDetails` customizado, logging de exceções ou tipo próprio de exceção de domínio. Assim, exceções não capturadas — por exemplo, `AtualizarStatus` para ID inexistente, falha de FK, banco indisponível ou valor nulo chegando ao banco — tendem a resultar em `500 Internal Server Error`. Em Development, detalhes podem ser expostos pelo comportamento padrão do host; o projeto não configura explicitamente uma página/handler de exceção.

Respostas textuais e respostas automáticas de validação não seguem um único contrato. Também há mensagens-fonte com indícios de codificação incorreta (`Ã£`, `Ã¡`) em alguns arquivos.

## 15. Fluxo completo de uma requisição

Exemplo: um usuário abre um chamado.

1. O cliente envia `POST /api/chamados` com JSON e header `Authorization: Bearer ...`.
2. Kestrel recebe a requisição. `UseHttpsRedirection` pode redirecionar HTTP para HTTPS, conforme a configuração do host.
3. A política CORS processa a origem/cabeçalhos.
4. O middleware JWT valida assinatura e expiração e cria `HttpContext.User` com os claims.
5. A autorização verifica o role `Usuario`.
6. O MVC seleciona `ChamadosController.CriarChamado`, desserializa o JSON em `CriarChamadoDTO` e realiza o model binding/validação automática aplicável.
7. O controller lê o claim `UserId`, ignora o `UsuarioId` enviado pelo cliente e coloca no DTO o ID autenticado.
8. `ChamadoService.CriarChamado` cria a entidade, fixa `StatusId = 1` e a adiciona ao `AppDbContext`.
9. `SaveChangesAsync` faz o EF Core gerar e executar `INSERT` parametrizado pelo provider SQL Server usando a conexão configurada.
10. O SQL Server valida PK/FKs/nulabilidade, gera o ID identity e confirma a operação.
11. O EF atualiza `Chamado.Id`; o service devolve a entidade ao controller.
12. O controller retorna `200 OK`; `System.Text.Json` serializa a entidade em camelCase e ignora ciclos de referência.

Nas consultas, o controller monta LINQ com `Include` e filtros. O EF traduz a expressão para SQL, materializa entidades sem tracking e o controller as converte manualmente para DTOs antes da serialização.

## 16. Arquivos mais importantes

| Arquivo | Função |
|---|---|
| `UsuariosApi.csproj` | Alvo .NET, nullable/implicit usings e dependências NuGet |
| `Program.cs` | Composition root: banco, DI, JWT, MVC/JSON, Swagger, CORS e pipeline |
| `appsettings.json` | Logging, hosts e connection string |
| `Data/AppDbContext.cs` | Mapeamento dos DbSets e dois relacionamentos Usuario–Chamado |
| `Models/*.cs` | Modelo persistente e navegações |
| `Data/Dtos/*.cs` | Contratos de entrada/saída |
| `Services/AuthService.cs` | Verificação de credenciais e emissão JWT |
| `Services/ChamadoService.cs` | Escritas principais de chamados/comentários |
| `Controllers/AuthController.cs` | Endpoint de login |
| `Controllers/UsuariosController.cs` | Cadastro, listagem, perfil e atualização |
| `Controllers/CatalogosController.cs` | Consultas de categorias, prioridades e status |
| `Controllers/ChamadosController.cs` | Fluxo operacional completo dos chamados |
| `Migrations/20260912153825_InitialCreate.cs` | Schema relacional inicial |
| `Migrations/AppDbContextModelSnapshot.cs` | Estado atual do modelo reconhecido pelo EF |
| `Properties/launchSettings.json` | URLs e ambiente para execução local |

`backend/README.md` existe, mas está vazio. `UsuariosApi.slnx` referencia a solução/projeto. Arquivos em `bin/` e `obj/` são artefatos gerados e não fazem parte da lógica autoral.

## 17. Decisões e observações adicionais

- O projeto compilou na análise com **0 erros e 54 avisos**, todos associados principalmente a propriedades reference-type não anuláveis sem inicialização. Isso inclui navegações e DTOs. O descompasso mais evidente é `Chamado.Tecnico`: a FK é nullable, mas a navegação C# não é.
- AutoMapper é registrado, mas as projeções são manuais e não há profile; a dependência não participa do funcionamento atual.
- Pacotes de ASP.NET Core Identity são referenciados, porém o sistema usa autenticação própria, sem `IdentityDbContext`, `UserManager`, `PasswordHasher` ou tabelas Identity.
- `AddOpenApi()` é chamado, mas não há `MapOpenApi()`. A documentação acessível decorre de `AddSwaggerGen`, `UseSwagger` e `UseSwaggerUI`, somente em Development.
- `ReferenceHandler.IgnoreCycles` reduz problemas ao serializar entidades com navegações bidirecionais, mas respostas baseadas diretamente em entidade ainda têm contrato menos controlado que DTOs.
- Datas usam `DateTime.Now` (hora local do servidor), enquanto a expiração do token usa `DateTime.UtcNow`. Não há padronização explícita de fuso/UTC para dados do domínio.
- Nenhuma migration é aplicada no startup e nenhum catálogo é semeado. Os IDs 1/2/3 usados como constantes de status constituem dependência implícita de dados externos.
- Não foram encontrados testes automatizados, health checks, versionamento da API, paginação, cache, telemetria, Dockerfile, CI/CD ou configuração específica de produção.

## 18. Síntese do que não foi implementado

Para evitar interpretar dependências instaladas como funcionalidades prontas, este backend **não implementa**: ASP.NET Core Identity, hash de senhas, refresh token, logout/revogação, recuperação de senha, seed, aplicação automática de migrations, CRUD de catálogos, exclusão de usuário, atualização/exclusão individual de comentário, anexos, notificações, SLA, pesquisa, paginação, soft delete, auditoria completa, concorrência otimista, testes automatizados, middleware global de erros ou deploy automatizado.

Essas ausências não significam necessariamente requisitos do trabalho; apenas delimitam com precisão o que o código analisado oferece hoje.
