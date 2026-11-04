#requires -Version 5.1
<#
.SYNOPSIS
Inicializa TechHelpSolutions a partir do export oficial, sem migrations.
.EXAMPLE
.\setup_database.ps1 -Instance '.\SQLEXPRESS'
.NOTES
Requer sqlcmd ODBC e acesso Windows ao SQL Server 2019 ou superior.
Em base existente, valida sem repetir a carga. Falhas nunca apagam a base.
#>
[CmdletBinding()]
param([ValidateNotNullOrEmpty()][string]$Instance = '.\SQLEXPRESS')

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$temporaryFiles = [System.Collections.Generic.List[string]]::new()

function Invoke-DatabaseCommand {
    param([string]$Database = 'master', [string]$Query, [string]$InputFile)
    $arguments = @('-S', $Instance, '-E', '-C', '-d', $Database,
        '-l', '15', '-t', '120', '-b', '-V', '11', '-r', '1',
        '-x', '-f', '65001', '-h', '-1', '-y', '8000', '-w', '65535')
    if ($InputFile) { $arguments += @('-i', $InputFile) }
    else { $arguments += @('-Q', $Query) }
    $result = & $script:SqlcmdPath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd falhou (codigo $LASTEXITCODE), instancia '$Instance', banco '$Database'. Verifique a mensagem SQL/conexao acima."
    }
    return ($result -join [Environment]::NewLine)
}

function New-TemporarySql {
    param([string]$Content)
    $path = [IO.Path]::GetTempFileName()
    $temporaryFiles.Add($path)
    [IO.File]::WriteAllText($path, $Content, [Text.UTF8Encoding]::new($true))
    return $path
}

function Install-Np2Objects {
    param([string[]]$Paths)
    $definitions = ($Paths | ForEach-Object {
        Write-Host "Aplicando script $([IO.Path]::GetFileName($_)) (reexecucao segura)..."
        [IO.File]::ReadAllText($_)
    }) -join "`r`nGO`r`n"
    $installation = @"
:ON ERROR EXIT
SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO
$definitions
GO
IF OBJECT_ID(N'dbo.ufn_DuracaoMinutos', N'FN') IS NULL
    THROW 51002, 'Function ufn_DuracaoMinutos nao encontrada.', 1;
IF OBJECT_ID(N'dbo.vw_ChamadosDetalhados', N'V') IS NULL
    THROW 51002, 'View vw_ChamadosDetalhados nao encontrada.', 1;
IF OBJECT_ID(N'dbo.vw_ComentariosDetalhados', N'V') IS NULL
    THROW 51002, 'View vw_ComentariosDetalhados nao encontrada.', 1;
IF OBJECT_ID(N'dbo.usp_AssumirChamado', N'P') IS NULL
    THROW 51002, 'Procedure usp_AssumirChamado nao encontrada.', 1;
IF OBJECT_ID(N'dbo.usp_FinalizarChamado', N'P') IS NULL
    THROW 51002, 'Procedure usp_FinalizarChamado nao encontrada.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.triggers
               WHERE object_id=OBJECT_ID(N'dbo.trg_Chamados_ValidarDatas', N'TR')
                 AND parent_id=OBJECT_ID(N'dbo.Chamados') AND is_disabled=0 AND is_instead_of_trigger=0)
    THROW 51002, 'Trigger trg_Chamados_ValidarDatas ausente, desabilitada ou na tabela incorreta.', 1;
IF EXISTS (
    SELECT 1 FROM (VALUES
        (N'Chamados',N'IX_Chamados_UsuarioId_DataAbertura'),
        (N'Chamados',N'IX_Chamados_TecnicoId_DataAbertura'),
        (N'Comentarios',N'IX_Comentarios_ChamadoId_DataEnvio')
    ) AS e(Tabela,Indice)
    WHERE NOT EXISTS (SELECT 1 FROM sys.indexes i
                      WHERE i.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.Tabela))
                        AND i.name=e.Indice AND i.is_disabled=0)
)
    THROW 51002, 'Indice composto esperado ausente ou desabilitado.', 1;
COMMIT TRANSACTION;
PRINT N'Function, views, procedures, trigger e indices criados/atualizados e verificados.';
GO
"@
    $installationPath = New-TemporarySql -Content $installation
    Write-Host (Invoke-DatabaseCommand -Database 'TechHelpSolutions' -InputFile $installationPath)
}

# A ferramenta de migracao foi retirada do pacote. A instalacao atual ja usa seeds com hash.
# Esta verificacao e somente leitura; nunca tenta converter ou exibir credenciais.
function Test-DatabasePasswordFormat {
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source'] = $Instance
    $builder['Initial Catalog'] = 'TechHelpSolutions'
    $builder['Integrated Security'] = $true
    $builder['TrustServerCertificate'] = $true
    $builder['Connect Timeout'] = 15
    $connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
    try {
        $connection.Open()
        $query = $connection.CreateCommand()
        $query.CommandText = 'SELECT Id, Senha FROM dbo.Usuarios'
        $reader = $query.ExecuteReader()
        try {
            while ($reader.Read()) {
                $valid = $false
                $stored = $reader.GetString(1)
                if ($stored.Length -gt 512) { throw 'Formato de hash invalido. Nenhum usuario foi alterado.' }
                $parts = $stored.Split('$')
                $cost = 0
                if ($parts.Count -eq 6 -and $parts[0] -ceq 'THS' -and $parts[1] -ceq '1' -and
                    $parts[2] -ceq 'PBKDF2-SHA256' -and $parts[3] -cmatch '^[0-9]+$' -and
                    [int]::TryParse($parts[3], [ref]$cost) -and $cost -ge 100000 -and $cost -le 2000000) {
                    try {
                        $salt = [Convert]::FromBase64String($parts[4])
                        $hash = [Convert]::FromBase64String($parts[5])
                        $valid = $salt.Length -ge 16 -and $salt.Length -le 64 -and $hash.Length -eq 32
                    }
                    catch [FormatException] { $valid = $false }
                }
                if (-not $valid) {
                    throw "Senha legada ou formato de hash invalido no usuario Id=$($reader.GetInt32(0)). O pacote atual nao inclui migracao de senhas. Realize manutencao com backup antes de continuar; nenhum usuario foi alterado."
                }
            }
        }
        finally { $reader.Dispose(); $query.Dispose() }
    }
    finally { $connection.Dispose() }
    Write-Host 'Formato PBKDF2 das senhas verificado, sem alterar usuarios.'
}

function Update-DemonstrationSeeds {
    # Reutiliza somente o seed confiavel do export, com hash PBKDF2 ja gerado.
    # Em base existente, o ID e automatico para nao sobrescrever outra conta.
    $source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'database/01_script.sql'))
    $checks = [Text.StringBuilder]::new()
    foreach ($account in @(
        @{ Email = 'gestor@gmail.com'; Role = 'Gestor'; Id = 3 },
        @{ Email = 'admin@gmail.com'; Role = 'Admin'; Id = 4 }
    )) {
        $pattern = "(?m)^INSERT \[dbo\]\.\[Usuarios\].*VALUES \(" + $account.Id + ", .*N'" + [regex]::Escape($account.Email) + "'.*N'" + $account.Role + "'.*$"
        $seed = [regex]::Matches($source, $pattern)
        if ($seed.Count -ne 1) { throw 'Seed administrativo ausente, divergente ou ambiguo.' }
        $insert = $seed[0].Value.Replace('([Id], ', '(')
        $insert = [regex]::Replace($insert, 'VALUES \(' + $account.Id + ', ', 'VALUES (')
        $email = $account.Email
        $role = $account.Role
        [void]$checks.AppendLine(@"
IF EXISTS (SELECT 1 FROM dbo.Usuarios WITH (UPDLOCK,HOLDLOCK)
           WHERE Email=N'$email' AND Tipo COLLATE Latin1_General_100_BIN2<>N'$role')
    THROW 51003, 'Email de demonstracao pertence a outro perfil; revise a conta antes de continuar.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WITH (UPDLOCK,HOLDLOCK) WHERE Email=N'$email')
BEGIN
    $insert;
END;
"@)
    }
    $sql = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
-- Compatibilidade apenas com os seeds conhecidos da versao anterior.
-- Nao altera senhas e nao promove contas arbitrarias cadastradas pela API.
UPDATE dbo.Usuarios SET Tipo=N'Gestor'
WHERE Id=3 AND Nome=N'Luiz Silveira' AND Email=N'gestor@gmail.com' AND Tipo=N'Admin';
IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WITH (UPDLOCK,HOLDLOCK) WHERE Email=N'admin@gmail.com')
    UPDATE dbo.Usuarios SET Nome=N'Fernando Campos',Email=N'admin@gmail.com',Tipo=N'Admin'
    WHERE Email=N'gestor@techhelp.local' AND Nome=N'Gestor de demonstração' AND Tipo=N'Gestor';
$checks
-- Ajuste pontual do seed antigo; nao altera outros comentarios nem cria regra nova.
UPDATE c SET DataEnvio=CONVERT(datetime2(7),'2026-10-27T09:38:24.3671343')
FROM dbo.Comentarios c JOIN dbo.Chamados ch ON ch.Id=c.ChamadoId
WHERE c.Id=2 AND c.ChamadoId=1 AND c.UsuarioId=1
  AND c.DataEnvio=CONVERT(datetime2(7),'2026-10-27T09:38:54.3671343')
  AND ch.DataAbertura<=CONVERT(datetime2(7),'2026-10-27T09:38:24.3671343')
  AND ch.DataFechamento>CONVERT(datetime2(7),'2026-10-27T09:38:24.3671343');
COMMIT TRANSACTION;
PRINT N'Luiz/Gestor e Fernando/Admin verificados; ajuste pontual do comentario verificado.';
"@
    $path = New-TemporarySql -Content $sql
    Write-Host (Invoke-DatabaseCommand -Database 'TechHelpSolutions' -InputFile $path)
}

try {
    $sourcePath = Join-Path $PSScriptRoot 'database/01_script.sql'
    $validationPath = Join-Path $PSScriptRoot 'database/07_validacao.sql'
    $objectPaths = @(
        (Join-Path $PSScriptRoot 'database/02_functions.sql'),
        (Join-Path $PSScriptRoot 'database/03_views.sql'),
        (Join-Path $PSScriptRoot 'database/04_procedures.sql'),
        (Join-Path $PSScriptRoot 'database/05_triggers.sql'),
        (Join-Path $PSScriptRoot 'database/06_indexes.sql')
    )
    foreach ($path in (@($sourcePath, $validationPath) + $objectPaths)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Arquivo obrigatorio ausente: $path" }
    }
    $command = Get-Command sqlcmd -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $command) { throw 'Dependencia ausente: sqlcmd. Instale/configure o cliente SQL Server antes de executar; nenhuma dependencia sera instalada automaticamente.' }
    $script:SqlcmdPath = $command.Source
    Write-Host "sqlcmd: $script:SqlcmdPath"
    Write-Host "Verificando conexao Windows e metadados em $Instance..."
    $metadataSql = @'
SET NOCOUNT ON;
SELECT CONVERT(varchar(30), SERVERPROPERTY('ProductVersion')) AS ProductVersion,
 CONVERT(nvarchar(128), SERVERPROPERTY('Edition')) AS Edition,
 CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultDataPath')) AS DataPath,
 CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultLogPath')) AS LogPath,
 HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE') AS CanCreate,
 HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW ANY DATABASE') AS CanViewDatabases,
 (SELECT state_desc FROM sys.databases WHERE name=N'TechHelpSolutions') AS DatabaseState
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES;
'@
    $metadata = (Invoke-DatabaseCommand -Query $metadataSql) | ConvertFrom-Json
    Write-Host "Servidor: $($metadata.ProductVersion) - $($metadata.Edition)"
    Write-Host "Dados: $($metadata.DataPath)"
    Write-Host "Log: $($metadata.LogPath)"
    Write-Host "Permissao CREATE ANY DATABASE: $($metadata.CanCreate)"
    if ($metadata.CanViewDatabases -ne 1) { throw 'Sem VIEW ANY DATABASE: nao e seguro concluir que o banco nao existe.' }

    if ($null -ne $metadata.DatabaseState) {
        if ($metadata.DatabaseState -ne 'ONLINE') { throw "Banco existente em estado $($metadata.DatabaseState). Nenhuma tentativa de recriacao sera feita." }
        Write-Host 'Banco existente: verificando inicializacao, sem repetir a carga...'
        Write-Host (Invoke-DatabaseCommand -Database 'TechHelpSolutions' -InputFile $validationPath)
        Update-DemonstrationSeeds
        Test-DatabasePasswordFormat
        Install-Np2Objects -Paths $objectPaths
        Write-Host 'Banco ja inicializado e validado. Carga nao repetida; formato das senhas verificado.'
        return
    }
    if ($metadata.CanCreate -ne 1) { throw 'A conta Windows nao possui CREATE ANY DATABASE. Solicite a permissao ao administrador.' }
    $major = [int]($metadata.ProductVersion.Split('.')[0])
    if ($major -lt 15) { throw "Versao $($metadata.ProductVersion) nao suportada por esta adaptacao controlada (minimo SQL Server 2019). Nenhum SQL foi aplicado." }
    foreach ($directory in @($metadata.DataPath, $metadata.LogPath)) {
        if ([string]::IsNullOrWhiteSpace($directory) -or $directory -match "[\r\n]") {
            throw 'Diretorios padrao indisponiveis/invalidos. Nao e seguro adaptar os arquivos MDF/LDF.'
        }
    }

    # ReadAllText reconhece o BOM UTF-16 do export original.
    $source = [IO.File]::ReadAllText($sourcePath)
    $adapted = $source
    $paths = @{
        TechHelpSolutions = $metadata.DataPath.TrimEnd('\', '/') + '\TechHelpSolutions.mdf'
        TechHelpSolutions_log = $metadata.LogPath.TrimEnd('\', '/') + '\TechHelpSolutions_log.ldf'
    }
    foreach ($logicalName in $paths.Keys) {
        $pattern = "(NAME = N'" + [regex]::Escape($logicalName) + "', FILENAME = N')[^']*(')"
        $matchesFound = [regex]::Matches($adapted, $pattern)
        if ($matchesFound.Count -ne 1) { throw "Layout de provisionamento inesperado para $logicalName. Adaptacao cancelada." }
        $replacementPath = $paths[$logicalName].Replace("'", "''")
        $adapted = [regex]::Replace($adapted, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($m) $m.Groups[1].Value + $replacementPath + $m.Groups[2].Value })
        Write-Host "Adaptacao FILENAME $logicalName -> $($paths[$logicalName])"
    }
    $compatibility = if ($major -ge 17) { 170 } elseif ($major -eq 16) { 160 } else { 150 }
    $compatibilityPattern = 'ALTER DATABASE \[TechHelpSolutions\] SET COMPATIBILITY_LEVEL = 170'
    if ([regex]::Matches($adapted, $compatibilityPattern).Count -ne 1) { throw 'Comando de compatibilidade inesperado no export.' }
    $adapted = [regex]::Replace($adapted, $compatibilityPattern, "ALTER DATABASE [TechHelpSolutions] SET COMPATIBILITY_LEVEL = $compatibility")
    Write-Host "Compatibilidade selecionada pela versao do servidor: $compatibility"
    if ($major -lt 17) {
        $option = 'ALTER DATABASE [TechHelpSolutions] SET OPTIMIZED_LOCKING = OFF'
        if (-not $adapted.Contains($option)) { throw 'Comando OPTIMIZED_LOCKING inesperado.' }
        $adapted = $adapted.Replace($option, '-- OPTIMIZED_LOCKING OFF: omitido, recurso ausente nesta versao.')
        Write-Host 'Adaptacao: omitido OPTIMIZED_LOCKING OFF (recurso SQL Server 2025).'
    }
    if ($major -lt 16) {
        $option = ' WITH CATALOG_COLLATION = DATABASE_DEFAULT, LEDGER = OFF'
        if (-not $adapted.Contains($option)) { throw 'Clausula CATALOG_COLLATION/LEDGER inesperada.' }
        $adapted = $adapted.Replace($option, '-- Defaults de catalogo e ausencia de ledger preservados no SQL Server 2019.')
        Write-Host 'Adaptacao: omitida clausula CATALOG_COLLATION DATABASE_DEFAULT / LEDGER OFF; usados defaults do SQL Server 2019.'
    }

    # Somente o provisionamento fica fora da transacao; o corpo NP1 permanece intacto.
    $bodyMarker = "USE [TechHelpSolutions]"
    $bodyStart = $adapted.IndexOf($bodyMarker, [StringComparison]::Ordinal)
    $tailStart = $adapted.LastIndexOf('USE [master]', [StringComparison]::Ordinal)
    if ($bodyStart -lt 0 -or $tailStart -le $bodyStart) { throw 'Delimitadores do export inesperados. Nenhum SQL foi aplicado.' }
    $provision = $adapted.Substring(0, $bodyStart)
    $body = $adapted.Substring($bodyStart, $tailStart - $bodyStart)
    $tail = $adapted.Substring($tailStart)
    $validation = [IO.File]::ReadAllText($validationPath)

    # Compara todas as colunas de cada seed na primeira carga, sem expor senhas.
    $seedChecks = [Text.StringBuilder]::new()
    $seedPattern = '(?m)^INSERT (\[dbo\]\.\[[^\]]+\]) (\([^\r\n]+?\)) VALUES (\([^\r\n]+\))\s*$'
    $seeds = [regex]::Matches($source, $seedPattern)
    if ($seeds.Count -ne 22) { throw "Quantidade inesperada de seeds no export: $($seeds.Count). Revise a adaptacao antes de executar." }
    foreach ($seed in $seeds) {
        $table = $seed.Groups[1].Value
        $columns = $seed.Groups[2].Value.Trim('(', ')')
        $values = $seed.Groups[3].Value.Trim()
        [void]$seedChecks.AppendLine("IF EXISTS (SELECT * FROM (VALUES $values) AS esperado ($columns) EXCEPT SELECT $columns FROM $table) THROW 51001, 'Seed original divergente em $table.', 1;")
    }
    $transactionalBody = $body.Replace($bodyMarker, "$bodyMarker`r`nGO`r`nSET XACT_ABORT ON;`r`nBEGIN TRANSACTION;")
    $executionSql = ":ON ERROR EXIT`r`n$provision$transactionalBody`r`nGO`r`n$validation`r`nGO`r`n$seedChecks`r`nGO`r`nCOMMIT TRANSACTION;`r`nGO`r`n$tail"
    $temporaryPath = New-TemporarySql -Content $executionSql
    Write-Host 'Criando banco e carregando estrutura e seeds oficiais...'
    Write-Host (Invoke-DatabaseCommand -InputFile $temporaryPath)
    Write-Host (Invoke-DatabaseCommand -Database 'TechHelpSolutions' -InputFile $validationPath)
    Update-DemonstrationSeeds
    Test-DatabasePasswordFormat
    Install-Np2Objects -Paths $objectPaths
    Write-Host 'Inicializacao concluida e validada com sucesso.'
}
catch {
    Write-Error "Falha no setup: $($_.Exception.Message) Se o banco foi criado parcialmente, ele foi preservado para diagnostico; nao execute carga manual sobre ele." -ErrorAction Continue
    throw
}
finally {
    foreach ($temporaryPath in $temporaryFiles) {
        if (Test-Path -LiteralPath $temporaryPath) { Remove-Item -LiteralPath $temporaryPath -Force }
    }
}
