# TechHelpSolutions

Sistema acadêmico de suporte técnico para registrar problemas, organizar atendimentos e acompanhar sua resolução. Usuários cadastram-se, abrem e acompanham seus próprios chamados, editam chamados abertos pela interface e enviam comentários. Técnicos consultam e filtram a fila, assumem e finalizam atendimentos. O painel do gestor apresenta indicadores; contas com perfil Admin também podem excluir chamados mediante confirmação.

A API oferece CRUD de chamados, cadastro/consulta/atualização/exclusão de usuários conforme suas permissões e manutenção administrativa dos catálogos. Comentários podem ser criados e consultados; não há edição ou exclusão individual pela interface. A NP2 preserva a base da NP1 e acrescenta objetos T-SQL, transações, concorrência, índices e proteção de senhas por hash.

## Início rápido

1. Instale os pré-requisitos descritos abaixo e confirme que o serviço **SQL Server (SQLEXPRESS)** está em execução.
2. Abra o PowerShell na pasta extraída `Grupo_03_TrabalhoFinal` e entre na pasta do código:

   ```powershell
   cd .\src_github
   ```

3. Execute `.\setup_database.ps1` e aguarde a mensagem de inicialização concluída ou de banco já inicializado e validado.
4. Nesse mesmo PowerShell, inicie a API:

   ```powershell
   dotnet run --project api/UsuariosApi.csproj --launch-profile http
   ```

5. Mantenha esse terminal aberto. Confirme a mensagem `Now listening on: http://localhost:5266` e abra `http://localhost:5266/swagger` para conferir a disponibilidade da API.
6. No Explorador de Arquivos, abra `frontend/pages/login.html` com um duplo clique e entre com uma das contas de demonstração listadas adiante.

Para encerrar a API, pressione **Ctrl+C** no terminal. Para utilizá-la novamente, basta iniciá-la pelo comando acima; não é necessário repetir o setup a cada acesso. Se o projeto estiver em outra pasta, abra o PowerShell nela antes de executar `cd .\src_github`.

## Tecnologias e ambiente

- API ASP.NET Core, alvo `net9.0`, Entity Framework Core 9 e JWT Bearer.
- SQL Server Express, T-SQL e autenticação integrada do Windows.
- Frontend HTML, CSS e JavaScript, sem framework ou etapa de build.
- PowerShell 5.1 ou superior e **sqlcmd ODBC** para o setup.
- As validações anteriores utilizaram executáveis .NET e testes JavaScript em DOM simulado; seus arquivos não integram o pacote atual.

Ambiente efetivamente validado: **Windows, PowerShell, SQL Server 2025 Express e runtime .NET/ASP.NET Core 9**. A compilação anterior utilizou o SDK .NET **10.0.401**, com runtimes .NET/ASP.NET Core **9.0.14** instalados; o projeto continua direcionado a `net9.0`. Para usar a solução `.slnx`, é necessário um SDK compatível com esse formato. Não há `global.json` fixando o SDK. Se o SDK não reconhecer a solução, use os comandos pelo `.csproj` apresentados abaixo.

## Estrutura

```text
Grupo_03_TrabalhoFinal/
|-- Documentacao_Projeto.pdf
|-- Apresentacao_Projeto.pdf
`-- src_github/
    |-- README.md
    |-- setup_database.ps1
    |-- database/
    |   |-- 01_script.sql
    |   |-- 02_functions.sql
    |   |-- 03_views.sql
    |   |-- 04_procedures.sql
    |   |-- 05_triggers.sql
    |   |-- 06_indexes.sql
    |   `-- 07_validacao.sql
    |-- api/
    |   |-- UsuariosApi.slnx
    |   |-- UsuariosApi.csproj
    |   |-- Controllers/, Services/, Models/, Data/, Filters/
    |   |-- Migrations/
    |   `-- Properties/launchSettings.json
    `-- frontend/
        |-- pages/
        |-- scripts/
        `-- styles/
```

`database/` reúne a criação/população inicial, os objetos avançados e a validação. Em `api/`, Controllers definem as rotas, Services concentram as operações extraídas, Models representam as entidades, Data contém o contexto EF, DTOs e projeções, e Filters trata erros específicos. Migrations preserva o histórico, mas não substitui o setup. No frontend, `pages/` contém as sete páginas HTML, `scripts/` contém autenticação, requisições e scripts de cada página, e `styles/` reúne os estilos comuns e específicos.

## Pré-requisitos e SQL Server

Instale o SQL Server Express pelo instalador oficial da Microsoft, com o mecanismo de banco e instância nomeada `SQLEXPRESS`. Habilite o acesso da conta Windows que executará o projeto. Instale o cliente sqlcmd ODBC e seu driver compatível; o setup não instala dependências. SSMS é opcional para inspeção.

Instale o SDK .NET e o runtime ASP.NET Core 9 necessários à API. O frontend não exige Python, Node.js, npm, framework, build ou servidor estático.

Links oficiais de instalação:

- [.NET 9 — SDK e runtimes](https://dotnet.microsoft.com/en-us/download/dotnet/9.0): instale o SDK para compilar e executar.
- [SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads): selecione a edição Express; o ambiente validado utiliza SQL Server 2025.
- [sqlcmd ODBC](https://learn.microsoft.com/en-us/sql/tools/sqlcmd/sqlcmd-download-install): siga a seção da versão ODBC utilizada pelo setup.
- [Microsoft ODBC Driver para SQL Server](https://learn.microsoft.com/en-us/sql/connect/odbc/download-odbc-driver-for-sql-server): driver necessário ao cliente sqlcmd.
- [SQL Server Management Studio — opcional](https://learn.microsoft.com/en-us/ssms/install/install): ferramenta de administração; não substitui o mecanismo SQL Server.

A conexão padrão é `Server=.\SQLEXPRESS;Database=TechHelpSolutions;Trusted_Connection=True;TrustServerCertificate=True`. A conta Windows precisa de acesso ao servidor, `VIEW ANY DATABASE`, permissão de criação quando o banco estiver ausente e permissões para criar/alterar os objetos e ler os dados. A conta da API precisa executar consultas, escritas e procedures. Não use permissões administrativas como configuração de produção.

Todos os comandos seguintes partem da pasta **`src_github`**. Confira as ferramentas e a conexão:

```powershell
dotnet --info
Get-Command sqlcmd
sqlcmd -S '.\SQLEXPRESS' -E -C -b -Q "SELECT SERVERPROPERTY('ProductVersion'), SERVERPROPERTY('Edition');"
```

## Inicializar o banco

```powershell
.\setup_database.ps1
# Para outra instância:
.\setup_database.ps1 -Instance 'SERVIDOR\INSTANCIA'
```

Se o erro indicar bloqueio da execução de scripts pela política do PowerShell, revise o arquivo e abra uma nova sessão com:

```powershell
PowerShell -ExecutionPolicy Bypass
```

Na nova sessão, entre na pasta `src_github`, se necessário, e execute novamente `.\setup_database.ps1`. O parâmetro vale somente para esse processo, sem alterar permanentemente a política da máquina; políticas da organização podem prevalecer. Ele não corrige erros de conexão, permissões SQL ou dependências ausentes.

Na primeira execução, o setup verifica dependências, conexão Windows, versão, permissões e diretórios padrão. Adapta somente o provisionamento em um arquivo temporário, preservando `database/01_script.sql`; cria o banco e carrega estrutura e dados originais.

Em banco existente, valida a estrutura e os registros mínimos sem repetir a carga e verifica os metadados do formato PBKDF2 das senhas, sem regravá-las. Senha em texto puro ou hash invalido interrompe o setup, sem alterar usuarios; uma migração com backup deve ser realizada separadamente antes de continuar. Verificar o formato nao substitui testar o login.

Instala/atualiza Function, Views, Procedures e Trigger e cria índices ausentes, verificando os objetos antes do commit. A ordem é validação original, verificacao do formato das senhas, `02_functions.sql`, `03_views.sql`, `04_procedures.sql`, `05_triggers.sql` e `06_indexes.sql`. `07_validacao.sql` contém as verificações da base original. Os seeds de senha do export e do modelo EF foram atualizados para hashes; estrutura, identidades e demais dados permanecem preservados.

Erros interrompem a execução. Uma instalação parcial é preservada para diagnóstico: o script não apaga nem recria automaticamente o banco. Não faça carga manual sobre uma base parcial sem investigar a falha.

## Compilar e iniciar a API

```powershell
dotnet sln api/UsuariosApi.slnx list
dotnet restore api/UsuariosApi.slnx
dotnet build api/UsuariosApi.slnx --no-restore
dotnet run --project api/UsuariosApi.csproj --launch-profile http
```

Alternativa para compilar diretamente o projeto, sem depender do reconhecimento de `.slnx`:

```powershell
dotnet restore api/UsuariosApi.csproj
dotnet build api/UsuariosApi.csproj --no-restore
dotnet run --project api/UsuariosApi.csproj --launch-profile http
```

O restore precisa acessar o NuGet para baixar os pacotes caso eles ainda não estejam no cache local. Instalar apenas o runtime não fornece os comandos de compilação: é necessário o SDK.

A API escuta em **`http://localhost:5266`**. O perfil `http` ativa `Development`; Swagger fica em `http://localhost:5266/swagger`. O frontend não é servido pela API. Iniciar a API não cria o banco nem aplica migrations.

Para outra instância, configure a conexão na sessão antes de iniciar a API:

```powershell
$env:ConnectionStrings__UsuarioConnection = 'Server=SERVIDOR\INSTANCIA;Database=TechHelpSolutions;Trusted_Connection=True;TrustServerCertificate=True'
```

O parâmetro `-Instance` do setup não altera a conexão da API.

## Iniciar o frontend

1. Inicie a API pelo comando documentado acima e mantenha-a em execução.
2. Acesse a pasta `src_github/frontend`.
3. Abra a subpasta `pages` e localize `login.html`. O caminho atual é **`src_github/frontend/pages/login.html`**; não existe outro `login.html` na raiz de `frontend`.
4. Dê um duplo clique nesse arquivo para abri-lo no navegador padrão.
5. Utilize a interface a partir do login, com a API disponível.

As páginas usam somente HTML, CSS e JavaScript puro. Não é necessário iniciar um servidor específico para abri-las. `frontend/scripts/app.js` e `frontend/scripts/pages/cadastro.js` usam **`http://localhost:5266`** como endereço da API; mantenha essa porta no uso local. Em outro ambiente, ambos os endereços precisam ser ajustados de forma coerente.

O cadastro público cria somente `Usuario`. Contas existentes de técnico e gestor continuam com suas telas. O login encaminha `Usuario` ao painel do solicitante, `Tecnico` ao painel técnico e `Gestor`/`Admin` ao painel do gestor; a autorização efetiva permanece no backend. O perfil literal Gestor não recebe as permissões administrativas de Admin.

Pela interface é possível cadastrar uma conta comum, trabalhar com chamados e comentários e consultar os painéis conforme o perfil. A exclusão de chamados aparece somente para Admin. A manutenção dos catálogos, a alteração de perfil e a atualização/exclusão de usuários são operações da API: utilize Postman, Insomnia ou outra ferramenta REST, conforme os exemplos abaixo. Não existem telas específicas para essas operações administrativas.

## Contas de demonstração

Os dados iniciais incluem quatro contas:

| E-mail | Perfil |
|---|---|
| `usuario@gmail.com` | Usuario |
| `tecnico@gmail.com` | Tecnico |
| `gestor@gmail.com` | Gestor |
| `admin@gmail.com` | Admin |

A senha inicial das **quatro contas de demonstração acima** é `Senha123`, armazenada como hash PBKDF2. Novas contas usam a senha escolhida no cadastro. Se a senha de uma conta inicial tiver sido alterada, use o valor atualizado.

## Problemas comuns

| Sintoma | O que conferir |
|---|---|
| `dotnet` não reconhecido | Instale o SDK .NET e abra novamente o PowerShell. Confira com `dotnet --info`. |
| API solicita instalação do runtime 9 | Confira `dotnet --list-runtimes`: a aplicação precisa dos runtimes .NET e ASP.NET Core 9, mesmo quando a compilação usa um SDK mais recente. |
| Solução `.slnx` não reconhecida | Use a alternativa pelo `.csproj` ou um SDK compatível com `.slnx`. |
| `sqlcmd` não reconhecido | Instale/configure o cliente sqlcmd ODBC e seu driver; reabra o terminal e confira com `Get-Command sqlcmd`. |
| Instância não encontrada ou conexão recusada | Abra o aplicativo Serviços do Windows e confira **SQL Server (SQLEXPRESS)**. Se a instância tiver outro nome, informe-o no setup e na conexão da API. |
| Falha de login Windows ou permissão negada | A conexão usa a conta Windows que iniciou o processo. Solicite ao administrador do SQL Server as permissões necessárias; trocar somente o perfil da aplicação não concede acesso ao banco. |
| PowerShell bloqueia o arquivo `.ps1` | Após revisar o arquivo, abra uma sessão com `PowerShell -ExecutionPolicy Bypass` e execute o setup novamente. Isso não corrige falhas de conexão ou permissões SQL; políticas da organização podem prevalecer. |
| `Address already in use` na porta 5266 | Confira se a API já está aberta em outro terminal. Reutilize essa execução ou encerre-a com Ctrl+C antes de iniciar outra. O frontend espera a porta 5266. |
| Frontend não carrega os dados | Verifique se a API continua executando e se o Swagger abre. Confira `http://localhost:5266` e a conexão SQL da API. Em caso de erro no navegador, examine Console e Network para identificar a requisição que falhou. |
| HTTP 401 | Faça login novamente; o token pode estar ausente, inválido ou expirado. Depois de mudar um perfil, obtenha um novo token. |
| HTTP 403 | A conta está autenticada, mas não tem o perfil ou a permissão exigidos para a operação ou para aquele chamado. Use uma conta autorizada. |
| Setup informa instalação parcial ou senha legada | Interrompa e investigue a mensagem. Não apague o banco nem reaplique seeds manualmente. A migração de senhas legadas não integra o pacote atual. |

O Swagger está disponível no ambiente Development utilizado pelo perfil `http`. Uma resposta 404 em `http://localhost:5266/` não indica, por si só, falha: a API não possui página inicial nessa URL.

## Exemplos práticos de chamadas REST

Use Postman, Insomnia ou outra ferramenta HTTP com a API em execução em `http://localhost:5266`. Nas rotas protegidas, envie o cabeçalho `Authorization: Bearer <TOKEN>`, usando o campo `token` retornado pelo login. Para corpos JSON, utilize `Content-Type: application/json`.

As credenciais abaixo são fictícias. Substitua `{id}` pelo ID retornado pela API; os IDs de categoria, prioridade e status devem vir dos catálogos. Operações de escrita devem usar somente registros de teste.

### 1. Autenticação

`POST http://localhost:5266/api/auth/login` — público.

Corpo:
```json
{
  "email": "usuario@exemplo.com",
  "senha": "senha"
}
```

Retorna 200 com o token e o resumo do usuário; credenciais inválidas retornam 401.

### 2. Usuários

`POST http://localhost:5266/api/usuarios` — cadastro público, somente perfil Usuario.

Corpo:
```json
{
  "nome": "Usuário de teste",
  "email": "usuario@exemplo.com",
  "senha": "senha-de-teste",
  "tipo": "Usuario"
}
```

`GET http://localhost:5266/api/usuarios` — listar usuários; exige JWT.

`GET http://localhost:5266/api/usuarios/me` — consultar a própria conta; exige JWT.

`PUT http://localhost:5266/api/usuarios/{id}` — atualizar a própria conta; Admin pode atualizar outras contas. Usuários comuns não podem trocar de perfil.

Corpo (senha opcional; omitida ou nula preserva a senha atual):
```json
{
  "nome": "Usuário atualizado",
  "email": "usuario@exemplo.com",
  "senha": null,
  "tipo": "Usuario"
}
```

**Como atribuir o perfil Admin:** cadastre a nova conta como Usuario e faça login com um administrador existente, como a conta de demonstração `admin@gmail.com`. Envie o PUT acima para o ID da conta que deseja promover, utilizando o JWT do administrador e o seguinte corpo:

```json
{
  "nome": "Nome do usuário",
  "email": "novo.admin@exemplo.com",
  "senha": null,
  "tipo": "Admin"
}
```

Informe o nome e o e-mail corretos da conta, pois também serão atualizados; `senha: null` mantém a senha atual. Após o retorno HTTP 200, faça login novamente com a conta promovida para obter um JWT com a role Admin. O cadastro público não permite criar Admin diretamente, e uma conta sem esse privilégio não pode promover a si mesma.

`DELETE http://localhost:5266/api/usuarios/{id}` — somente Admin; conta sem chamados ou comentários vinculados.

### 3. Chamados

`GET http://localhost:5266/api/chamados` — exige JWT; Usuario vê apenas os próprios chamados. Tecnico, Gestor e Admin têm a listagem geral.

`GET http://localhost:5266/api/chamados?status={statusId}&prioridade={prioridadeId}&categoria={categoriaId}` — mesmos perfis, com filtros opcionais.

`GET http://localhost:5266/api/chamados/meus` — somente Usuario.

`GET http://localhost:5266/api/chamados/meus-atendimentos` — somente Tecnico.

`GET http://localhost:5266/api/chamados/{id}` — detalhe; Usuario acessa apenas chamado próprio.

`POST http://localhost:5266/api/chamados` — somente Usuario; o solicitante é identificado pelo JWT.

Corpo:
```json
{
  "titulo": "Problema no computador",
  "descricao": "O computador não está ligando.",
  "categoriaId": 1,
  "prioridadeId": 1
}
```

`PUT http://localhost:5266/api/chamados/{id}` — Usuario no próprio chamado ou Tecnico.

Corpo:
```json
{
  "titulo": "Problema no computador atualizado",
  "descricao": "O problema continua após verificar a tomada.",
  "categoriaId": 1,
  "prioridadeId": 1
}
```

Os números acima são ilustrativos: consulte os catálogos antes de enviá-los.

`PUT http://localhost:5266/api/chamados/{id}/status` — somente Tecnico.

Corpo:
```json
{
  "statusId": 2
}
```

`PUT http://localhost:5266/api/chamados/{id}/assumir` — somente Tecnico; sem corpo.

`PUT http://localhost:5266/api/chamados/{id}/finalizar` — somente o técnico responsável; sem corpo.

`DELETE http://localhost:5266/api/chamados/{id}` — somente Admin; sem corpo. Remove também os comentários associados.

### 4. Comentários

`GET http://localhost:5266/api/chamados/{id}/comentarios` — exige JWT; Usuario acessa apenas chamado próprio.

`POST http://localhost:5266/api/chamados/{id}/comentarios` — mesma restrição; o autor vem do JWT.

Corpo:
```json
{
  "mensagem": "O problema continua após reiniciar."
}
```

### 5. Catálogos

As consultas exigem JWT:

- `GET http://localhost:5266/api/catalogos/categorias`
- `GET http://localhost:5266/api/catalogos/prioridades`
- `GET http://localhost:5266/api/catalogos/status`

As escritas dos três catálogos exigem **Admin**; Gestor não possui essa permissão:

- `POST http://localhost:5266/api/catalogos/categorias`
- `PUT http://localhost:5266/api/catalogos/categorias/{id}`
- `DELETE http://localhost:5266/api/catalogos/categorias/{id}`
- `POST http://localhost:5266/api/catalogos/prioridades`
- `PUT http://localhost:5266/api/catalogos/prioridades/{id}`
- `DELETE http://localhost:5266/api/catalogos/prioridades/{id}`
- `POST http://localhost:5266/api/catalogos/status`
- `PUT http://localhost:5266/api/catalogos/status/{id}`
- `DELETE http://localhost:5266/api/catalogos/status/{id}`

Corpo para POST e PUT (DELETE não recebe corpo):
```json
{
  "nome": "Item de teste"
}
```

Nome ausente, vazio ou somente espaços retorna 400; nomes válidos são aparados nas extremidades. Use apenas itens novos de teste, sem renomear os status utilizados pelas regras atuais.

### Conferir os resultados

Criação, consulta e atualização de usuários/catálogos/chamados retornam 200, assim como o envio de comentários. Alterar status, assumir, finalizar e excluir retornam 204 sem corpo.

Para testar falhas: token ausente/inválido retorna 401; perfil ou acesso sem permissão, 403; ID inexistente nas operações por ID, 404. Campos inválidos podem retornar 400; conflitos concorrentes ao assumir/finalizar retornam 409.

A exclusão de usuário com chamados como solicitante/técnico ou comentários como autor retorna 409. A exclusão de catálogo vinculado a chamado também retorna 409. Essas operações protegem a verificação de vínculos e a exclusão na mesma transação, impedindo cascatas concorrentes; conflitos de integridade reconhecidos retornam mensagens fixas, sem detalhes SQL.

## Objetos SQL implementados

- `usp_AssumirChamado` e `usp_FinalizarChamado`: transações, validações, rollback e detecção de concorrência pelo estado esperado.
- `ufn_DuracaoMinutos`: duração em minutos sem arredondamento inteiro; sem fechamento retorna NULL.
- `vw_ChamadosDetalhados` e `vw_ComentariosDetalhados`: consultas reutilizáveis consumidas pela API, sem senhas.
- `trg_Chamados_ValidarDatas`: rejeita fechamento anterior à abertura em INSERT/UPDATE, inclusive em múltiplas linhas.
- `IX_Chamados_UsuarioId_DataAbertura`, `IX_Chamados_TecnicoId_DataAbertura` e `IX_Comentarios_ChamadoId_DataEnvio`: índices compostos adicionais, preservando os originais.

## Limitações e segurança

Senhas são armazenadas em PBKDF2-HMAC-SHA256, com 600.000 iterações, salt aleatório de 16 bytes e derivação de 32 bytes. O formato versionado registra algoritmo, custo, salt e derivação; o login compara em tempo constante e rejeita texto puro e formatos inválidos. O serviço identifica hashes que precisam de atualização futura; não há fallback de login legado.

Este é um ambiente acadêmico de desenvolvimento, não uma configuração pronta para produção. Persistem chave JWT fixa no código, CORS aberto, HTTP local e aceitação do certificado SQL por `TrustServerCertificate=True`. O frontend guarda o token no armazenamento local. 

A proteção contra XSS usa APIs seguras do DOM; cadastro privilegiado e autoelevação são bloqueados no servidor, assim como a listagem de chamados de terceiros para `Usuario`. 