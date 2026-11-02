# TechHelpSolutions

Sistema acadêmico de suporte técnico: usuários abrem e acompanham chamados, técnicos assumem e finalizam atendimentos e o gestor consulta indicadores. A NP2 preserva a base da NP1 e acrescenta objetos T-SQL, transações, concorrência e índices.

## Tecnologias e ambiente

- API ASP.NET Core, alvo `net9.0`, Entity Framework Core 9 e JWT Bearer.
- SQL Server Express, T-SQL e autenticação integrada do Windows.
- Frontend HTML, CSS e JavaScript, sem framework ou etapa de build.
- PowerShell 5.1 ou superior e **sqlcmd ODBC** para o setup.
- Testes backend em executável .NET e frontend com módulos nativos do Node.js.

Ambiente efetivamente validado: **Windows, PowerShell, SQL Server 2025 Express e runtime .NET/ASP.NET Core 9**. A compilação local utilizou SDK .NET `10.0.401`, com runtimes 9.0.14 instalados; o alvo permanece .NET 9. Use um SDK que reconheça soluções `.slnx`. Não há `global.json` fixando o SDK.

O setup prevê adaptações para SQL Server 2019 e 2022, mas essas versões **não foram validadas integralmente**. A previsão no script não equivale a instalação homologada nesses ambientes.

## Estrutura

```text
src_github/
├── README.md
├── setup_database.ps1
├── database/
│   ├── script.sql              # Export oficial: estrutura e dados NP1
│   ├── schema.sql              # Referência; não substitui o export
│   └── np2/                    # Scripts 02 a 07
├── api/
│   ├── UsuariosApi.slnx
│   ├── UsuariosApi.csproj
│   ├── Controllers/, Services/, Models/, Data/, Filters/
│   ├── Migrations/             # Histórico; não usar na instalação inicial
│   └── Properties/launchSettings.json
├── frontend/
│   ├── pages/
│   ├── scripts/
│   └── styles/
└── tests/
    ├── Chamados.IntegrationTests/
    ├── frontend/renderizacao.test.cjs
    └── evidencias/indices/
```

## Pré-requisitos e SQL Server

Instale o SQL Server Express pelo instalador oficial da Microsoft, com o mecanismo de banco e instância nomeada `SQLEXPRESS`. Habilite o acesso da conta Windows que executará o projeto. Instale o cliente sqlcmd ODBC e seu driver compatível; o setup não instala dependências. SSMS é opcional para inspeção.

Instale o SDK .NET e o runtime ASP.NET Core 9 necessários à API. Para os testes frontend, foi utilizado Node.js `24.14.0`. Para o servidor estático sugerido abaixo, Python 3 é opcional (validado com 3.13.4); ele não é dependência da aplicação.

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

Se a política local bloquear scripts, revise o arquivo e use uma sessão cuja política permita sua execução, conforme as regras do ambiente. Não é necessário alterar permanentemente a política da máquina.

Na primeira execução, o setup verifica dependências, conexão Windows, versão, permissões e diretórios padrão. Adapta somente o provisionamento em um arquivo temporário, preservando `database/script.sql`; cria o banco e carrega estrutura e dados originais. Não usa migrations nem `schema.sql` para substituir essa carga.

Em banco existente, valida a estrutura e os registros mínimos sem repetir a carga. Instala/atualiza Function, Views, Procedures e Trigger e cria índices ausentes, verificando os objetos antes do commit. A ordem é validação original, `02_functions.sql`, `03_views.sql`, `04_procedures.sql`, `05_triggers.sql` e `06_indexes.sql`. `07_validacao.sql` contém as verificações da base original.

Erros interrompem a execução. Uma instalação parcial é preservada para diagnóstico: o script não apaga nem recria automaticamente o banco. Não faça carga manual sobre uma base parcial sem investigar a falha.

## Compilar e iniciar a API

```powershell
dotnet sln api/UsuariosApi.slnx list
dotnet restore api/UsuariosApi.slnx
dotnet build api/UsuariosApi.slnx --no-restore
dotnet run --project api/UsuariosApi.csproj --launch-profile http
```

A API escuta em **`http://localhost:5266`**. O perfil `http` ativa `Development`; Swagger fica em `http://localhost:5266/swagger`. O frontend não é servido pela API. Iniciar a API não cria o banco nem aplica migrations.

Para outra instância, configure a conexão na sessão antes de iniciar a API:

```powershell
$env:ConnectionStrings__UsuarioConnection = 'Server=SERVIDOR\INSTANCIA;Database=TechHelpSolutions;Trusted_Connection=True;TrustServerCertificate=True'
```

O parâmetro `-Instance` do setup não altera a conexão da API.

## Iniciar o frontend

Em outro terminal, sirva os arquivos estáticos com uma ferramenta já instalada. Com Python 3:

```powershell
python -m http.server 5500 --bind 127.0.0.1 --directory frontend
```

Abra `http://127.0.0.1:5500/pages/login.html`. Não há instalação npm nem build frontend. `frontend/scripts/app.js` e `frontend/scripts/pages/cadastro.js` usam **`http://localhost:5266`** como endereço da API; mantenha essa porta no uso local. Em outro ambiente, ambos os endereços precisam ser ajustados de forma coerente.

O cadastro público cria somente `Usuario`. Contas existentes de técnico e gestor continuam com suas telas. O login encaminha `Usuario` ao painel do solicitante, `Tecnico` ao painel técnico e `Gestor`/`Admin` ao painel do gestor; a autorização efetiva permanece no backend.

## Contas de demonstração

Os seeds originais contêm `usuario@gmail.com` (Usuario), `tecnico@gmail.com` (Tecnico) e `gestor@gmail.com` (Admin, utilizado no painel do gestor). Solicite as credenciais de demonstração ao responsável pela entrega, por canal separado. Este README não publica senhas ou chaves JWT. Os testes dependem dos seeds originais; não altere suas credenciais para executar os testes.

## Exemplos REST com PowerShell

Com a API em execução, use `Invoke-RestMethod` no Windows PowerShell 5.1 ou superior. Execute os blocos na ordem, na mesma sessão. As credenciais abaixo são placeholders: substitua pelos dados de contas existentes com os perfis indicados. Não publique senhas ou os tokens retornados.

**As operações POST, PUT e DELETE abaixo gravam no banco.** Execute-as somente em ambiente de teste e use exclusivamente o chamado criado neste roteiro, nunca os chamados originais. Os IDs de categoria/prioridade vêm das consultas e o ID do chamado vem da resposta de criação.

### Login e cabeçalho Bearer

O login recebe `email` e `senha`, retorna `token` e `usuario` com HTTP 200; credenciais inválidas retornam 401. Esta função autentica uma conta por vez e evita imprimir o JWT. A senha é convertida em texto apenas para montar o JSON exigido pela API; use o HTTP local documentado somente no ambiente acadêmico.

```powershell
$ErrorActionPreference = 'Stop'
$baseUrl = 'http://localhost:5266'
function Get-ThsHeaders {
    param([string]$Email)
    $securePassword = Read-Host 'Senha da conta informada' -AsSecureString
    $credential = New-Object System.Management.Automation.PSCredential($Email, $securePassword)
    $body = @{ email = $Email; senha = $credential.GetNetworkCredential().Password } | ConvertTo-Json
    $login = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/auth/login" -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
    return @{ Authorization = "Bearer $($login.token)" }
}
$headersUsuario = Get-ThsHeaders -Email '<EMAIL_CONTA_USUARIO>'
# Authorization enviado nas consultas: Bearer <TOKEN retornado pelo login>
```

### Catálogos e listagem — conta Usuario

Os três catálogos exigem autenticação e retornam arrays `{ id, nome }` com HTTP 200. A listagem geral admite Usuario, Tecnico, Gestor e Admin; para Usuario retorna somente seus próprios chamados. Os filtros opcionais são `status`, `prioridade` e `categoria`; a ordenação é abertura decrescente.

```powershell
$categorias = @(Invoke-RestMethod -Method Get -Uri "$baseUrl/api/catalogos/categorias" -Headers $headersUsuario)
$prioridades = @(Invoke-RestMethod -Method Get -Uri "$baseUrl/api/catalogos/prioridades" -Headers $headersUsuario)
$status = @(Invoke-RestMethod -Method Get -Uri "$baseUrl/api/catalogos/status" -Headers $headersUsuario)
$categorias | Format-Table id, nome
$prioridades | Format-Table id, nome
$status | Format-Table id, nome
Invoke-RestMethod -Method Get -Uri "$baseUrl/api/chamados" -Headers $headersUsuario
if (!$categorias.Count -or !$prioridades.Count) { throw 'Catálogos necessários estão vazios.' }
$categoriaId = [int]$categorias[0].id
$prioridadeId = [int]$prioridades[0].id
Invoke-RestMethod -Method Get -Uri "$baseUrl/api/chamados?categoria=$categoriaId&prioridade=$prioridadeId" -Headers $headersUsuario
```

### Criar, consultar e atualizar o chamado de teste

Somente Usuario pode criar. O solicitante é obtido do JWT; não é necessário enviar `usuarioId`, pois o controller sobrescreve esse campo. Criação e atualização retornam HTTP **200**, não 201. A atualização aceita Usuario (somente chamado próprio) ou Tecnico. A consulta por ID exige autenticação; Usuario só acessa o próprio chamado.

```powershell
$body = @{
    titulo = 'Teste REST NP2'
    descricao = 'Chamado temporário para verificar o CRUD.'
    categoriaId = $categoriaId
    prioridadeId = $prioridadeId
} | ConvertTo-Json
$chamado = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/chamados" -Headers $headersUsuario -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
$chamadoId = [int]$chamado.id
if ($chamadoId -le 0) { throw 'Criação não retornou um ID válido; interrompa o roteiro.' }
Invoke-RestMethod -Method Get -Uri "$baseUrl/api/chamados/$chamadoId" -Headers $headersUsuario

$body = @{
    titulo = 'Teste REST NP2 atualizado'
    descricao = 'Atualização do chamado temporário.'
    categoriaId = $categoriaId
    prioridadeId = $prioridadeId
} | ConvertTo-Json
Invoke-RestMethod -Method Put -Uri "$baseUrl/api/chamados/$chamadoId" -Headers $headersUsuario -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
```

### Enviar e consultar comentários

Ambos exigem autenticação; Usuario só comenta e consulta comentários de seus chamados. POST recebe somente `mensagem` e retorna HTTP 200 com o comentário e resumo público do autor. GET retorna HTTP 200 com os comentários em ordem de envio.

```powershell
$body = @{ mensagem = 'Comentário de teste REST.' } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "$baseUrl/api/chamados/$chamadoId/comentarios" -Headers $headersUsuario -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
Invoke-RestMethod -Method Get -Uri "$baseUrl/api/chamados/$chamadoId/comentarios" -Headers $headersUsuario
```

### Assumir e finalizar — conta Tecnico

Use uma conta Tecnico existente. Ambas as operações são PUT **sem corpo** e retornam HTTP 204, sem JSON. Finalizar exige que o chamado esteja atribuído ao técnico autenticado. Não troque `$chamadoId` por um ID original.

```powershell
$headersTecnico = Get-ThsHeaders -Email '<EMAIL_CONTA_TECNICO>'
Invoke-RestMethod -Method Put -Uri "$baseUrl/api/chamados/$chamadoId/assumir" -Headers $headersTecnico
Invoke-RestMethod -Method Put -Uri "$baseUrl/api/chamados/$chamadoId/finalizar" -Headers $headersTecnico
Invoke-RestMethod -Method Get -Uri "$baseUrl/api/chamados/$chamadoId" -Headers $headersTecnico
```

### Excluir — conta Admin

Somente o perfil literal Admin pode excluir; Gestor não recebe essa permissão pelo nome do perfil. A API remove também os comentários associados conforme sua implementação existente. DELETE retorna HTTP 204 sem corpo.

```powershell
$headersAdmin = Get-ThsHeaders -Email '<EMAIL_CONTA_ADMIN>'
if ((Read-Host "Excluir o chamado de teste #$chamadoId? Digite EXCLUIR") -ceq 'EXCLUIR') {
    Invoke-RestMethod -Method Delete -Uri "$baseUrl/api/chamados/$chamadoId" -Headers $headersAdmin
}
```

### Conferir respostas e falhas

Além dos sucessos descritos, chamadas autenticadas podem retornar 401 (token ausente/inválido/expirado) ou 403 (perfil ou propriedade sem permissão). Operações por ID retornam 404 para chamado ausente. Atualização pode retornar 400 por campos inválidos; finalização sem responsável retorna 400 e por outro técnico, 403. Assumir/finalizar podem retornar 409 por conflito concorrente e 400 por violação de integridade das datas. Outras falhas de banco não são uniformemente convertidas em 400.

No PowerShell 5.1, `Invoke-RestMethod` lança exceção para respostas HTTP de erro. Este exemplo consulta o ID obtido neste roteiro e imprime somente o código, sem expor o corpo interno de erro. **Após confirmar a exclusão**, o esperado é 404; caso a exclusão tenha sido cancelada, o GET continua retornando o chamado com 200.

```powershell
try {
    Invoke-RestMethod -Method Get -Uri "$baseUrl/api/chamados/$chamadoId" -Headers $headersAdmin
} catch {
    if ($null -ne $_.Exception.Response) {
        Write-Host ('HTTP {0}' -f [int]$_.Exception.Response.StatusCode)
    } else {
        Write-Host 'Falha de conexão com a API.'
    }
}
```

Os exemplos foram conferidos estaticamente contra controllers/DTOs e pelo parser PowerShell; as escritas deste roteiro não foram executadas durante sua elaboração. Os conjuntos automatizados da seção seguinte oferecem verificações reproduzíveis de códigos HTTP, autorização e concorrência.

## Testes automatizados

Use somente a base acadêmica local, sem outros usuários gravando durante os testes. O executável backend não utiliza descoberta por `dotnet test`. Com a API iniciada na porta 5266, execute os cinco modos:

```powershell
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://localhost:5266
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://localhost:5266 --consultas
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://localhost:5266 --trigger
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://localhost:5266 --correcoes
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://localhost:5266 --acesso
```

Eles cobrem procedures/concorrência, Views/Function, Trigger e regressões de segurança. Verificam a preservação dos registros originais; os modos de escrita criam e removem somente suas fixtures temporárias. Testes via API podem consumir valores IDENTITY, sem reseed. A conta Windows precisa das permissões de leitura/escrita/execução e, para observar bloqueios, `VIEW SERVER PERFORMANCE STATE` no SQL Server 2025 ou equivalente. `THS_TEST_CONNECTION` permite outra conexão, que deve apontar para a mesma base utilizada pela API.

As evidências de índices estão em `tests/evidencias/indices/`: inventários, SQL, planos reais, leituras e comparação. Não repita a captura `--indices` sobre essas pastas: o executável protege as evidências contra sobrescrita. Com três chamados e dois comentários não há base para afirmar ganho expressivo; índices eliminaram ordenações em consultas específicas, mas as leituras observadas não diminuíram.

Testes frontend sem API (22 aprovados e um teste real opcional ignorado):

```powershell
node --test --test-isolation=none tests/frontend/renderizacao.test.cjs
Get-ChildItem frontend/scripts -Recurse -Filter '*.js' | ForEach-Object { node --check $_.FullName; if ($LASTEXITCODE -ne 0) { throw "Erro de sintaxe: $($_.FullName)" } }
```

Para incluir consultas reais e obter os 23 testes, mantenha a API ligada:

```powershell
$env:THS_API_READONLY_URL = 'http://localhost:5266'
node --test --test-isolation=none tests/frontend/renderizacao.test.cjs
Remove-Item Env:THS_API_READONLY_URL
```

Os testes frontend usam DOM simulado e não substituem validação visual em navegador. O modo real autentica e consulta a API sem gravar dados. O histórico validado inclui 63 grupos backend e 23 testes frontend; consulte o resultado da execução atual, sem tratar esses números como garantia em outro ambiente.

## Objetos SQL implementados

- `usp_AssumirChamado` e `usp_FinalizarChamado`: transações, validações, rollback e detecção de concorrência pelo estado esperado.
- `ufn_DuracaoMinutos`: duração em minutos sem arredondamento inteiro; sem fechamento retorna NULL.
- `vw_ChamadosDetalhados` e `vw_ComentariosDetalhados`: consultas reutilizáveis consumidas pela API, sem senhas.
- `trg_Chamados_ValidarDatas`: rejeita fechamento anterior à abertura em INSERT/UPDATE, inclusive em múltiplas linhas.
- `IX_Chamados_UsuarioId_DataAbertura`, `IX_Chamados_TecnicoId_DataAbertura` e `IX_Comentarios_ChamadoId_DataEnvio`: índices compostos adicionais, preservando os originais.

## Limitações e segurança

Este é um ambiente acadêmico de desenvolvimento, não uma configuração pronta para produção. Persistem senhas sem hash, chave JWT fixa no código, CORS aberto, HTTP local e aceitação do certificado SQL por `TrustServerCertificate=True`. O frontend guarda o token no armazenamento local. Não exponha a aplicação nem as contas de demonstração à internet. O export e as fixtures contêm credenciais de teste e exigem revisão antes de qualquer publicação.

A proteção contra XSS usa APIs seguras do DOM; cadastro privilegiado e autoelevação são bloqueados no servidor, assim como a listagem de chamados de terceiros para `Usuario`. Essas correções não eliminam as limitações anteriores de autenticação e implantação. Os testes exigem SQL Server real e permissões específicas; não há provisionamento automático de dependências.

`api/DOCUMENTACAO_TECNICA_BACKEND.md` é um registro histórico da NP1 e não deve integrar o pacote final como descrição atual. A documentação oficial e a apresentação NP2 serão preparadas separadamente. Antes de empacotar, exclua da seleção do ZIP os diretórios `bin/`, `obj/`, caches de IDE e arquivos `*.csproj.user`; preserve fontes, scripts, testes e evidências. Não inclua configurações pessoais, tokens ou segredos locais.
