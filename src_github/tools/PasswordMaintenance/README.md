# Manutenção de senhas

Ferramenta local .NET 9, sem endpoints adicionais. O setup a chama após validar a base. Pare a API e outras escritas durante a conversão. A autenticação da API nunca aceita senha legada.

Na pasta `src_github`:

```powershell
# Opcional para outra instância; a conexão padrão é .\SQLEXPRESS.
$env:THS_PASSWORD_CONNECTION = 'Server=.\SQLEXPRESS;Database=TechHelpSolutions;Integrated Security=True;TrustServerCertificate=True'
dotnet run --project tools/PasswordMaintenance/PasswordMaintenance.csproj -- migrate tests/evidencias/senhas/nova_execucao.json
```

Escolha um nome novo para preservar as evidências anteriores. Não há senha, hash individual ou JWT na saída ou nos arquivos de evidência. O baseline contém somente hashes agregados, contagens e IDs convertidos. O primeiro relatório registra igualdade de todos os dados exceto Senha antes/depois; o hash integral necessariamente muda.

Quando detecta legado, cria backup COPY_ONLY com CHECKSUM no diretório padrão do servidor e executa RESTORE VERIFYONLY WITH CHECKSUM. A conta Windows precisa de permissões de backup, consulta e atualização. Falha no backup impede a conversão. Os registros são convertidos numa transação SERIALIZABLE com bloqueio da tabela de usuários; qualquer falha antes do commit desfaz a transação. Uma repetição sobre hashes válidos não regrava senhas nem cria outro backup. Formato reservado THS inválido aborta para revisão.

## Recuperação

O caminho do backup está no primeiro relatório da conversão. Ele contém credenciais legadas e deve permanecer sob as ACLs restritas do diretório de backup SQL Server, fora do repositório/ZIP. Não o copie para pasta pública. Não use SQL Server Profiler com captura de parâmetros de senha durante a operação.

Uma transação não confirmada é revertida automaticamente; reexecutar após corrigir a causa é seguro. Se a conversão já foi confirmada e surgir problema, não restaure automaticamente sobre a base em uso. Pare a API e peça ao administrador que restaure o backup em outra base isolada, valide usuários/perfis e planeje a recuperação considerando escritas posteriores. RESTORE VERIFYONLY verifica o arquivo, mas não substitui um ensaio completo de recuperação. Não foi feita restauração sobre a base original.

Reverter somente o código da API para comparação de texto puro impede o login sobre a base convertida. Código e formato do banco devem ser implantados de forma coordenada. Não introduza fallback de autenticação para contornar isso. Não regrave seeds antigos sobre a base existente.

PBKDF2-HMAC-SHA256 usa 600.000 iterações, conforme a referência da OWASP: https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html . O serviço aceita metadados v1 válidos com custos anteriores dentro dos limites definidos e informa NeedsRehash; hashes inválidos nunca são interpretados como credenciais no login.
