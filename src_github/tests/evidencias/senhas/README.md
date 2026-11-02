# Evidências da proteção de senhas — 02/11/2026

## Migração da base original

`01_migracao.json` registra backup COPY_ONLY com CHECKSUM e RESTORE VERIFYONLY antes da conversão, contagens, IDs 1/2/3 convertidos, hashes agregados antes/depois e hash dos dados excluindo somente Usuarios.Senha. Não contém senhas, hashes individuais ou JWT. O arquivo `.bak` permanece no diretório de backup do SQL Server, fora do repositório. Deve ser tratado como confidencial, pois contém o estado legado.

Somente Senha foi alterada nos três usuários. IDs, nomes, e-mails, perfis, datas, chamados, comentários, catálogos, histórico e objetos avançados foram preservados. A própria transação compara todos os dados fora de Senha antes do commit. `04_reexecucao.json` confirma zero conversões adicionais, mesmo baseline integral e mesmas definições SQL.

Os hashes integrais históricos das evidências de índices continuam válidos para a coleta anterior à conversão. O novo baseline é AfterDataHash em `01_migracao.json`; não é correto substituí-lo nos arquivos históricos nem comparar a base convertida diretamente com o hash antigo exigindo igualdade.

## Testes

- `02_testes_backend.txt`: 10 grupos novos de senhas + 63 anteriores, todos aprovados. As fixtures SQL de técnico/Gestor agora recebem hashes; as credenciais de entrada dos testes continuam as mesmas. Não houve relaxamento das verificações de autorização ou comparação integral antes/depois.
- `03_testes_frontend.txt`: 39 testes aprovados, incluindo leitura real e exclusão real somente de chamado temporário.
- Hash malformado, legado e custo abusivo são rejeitados. Metadados antigos válidos sinalizam NeedsRehash. API não regrava hash em atualização sem senha. Alteração de senha rejeita a senha antiga, mantendo login com a nova.
- A comparação final integral coincide com o baseline pós-conversão. Contagens: Usuarios=3, Chamados=3, Comentarios=2, Categorias=4, Prioridades=3, Status=3 e histórico=3. Permanecem 17 índices e os seis objetos programáveis NP2. Fixtures temporárias foram removidas. IDs consumidos pelos testes não foram resemeados.

Comandos, na pasta `src_github`, com API local ativa:

```powershell
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://localhost:5266 --senhas
# Outros modos preservados: sem flag, --consultas, --trigger, --correcoes, --acesso.
$env:THS_API_READONLY_URL = 'http://localhost:5266'
$env:THS_API_TEMP_DELETE_URL = 'http://localhost:5266'
node --test --test-isolation=none tests/frontend/renderizacao.test.cjs
```

## Instalação e reexecução

Foi criada uma instância isolada `(localdb)\THSHashValidation`, SQL Server 2025 LocalDB 17.0.4025.3, com uma base nova TechHelpSolutions. A estrutura e os seeds foram carregados pelo setup, com os três hashes diretamente no export. A primeira tentativa expôs um erro de acesso a propriedades do SqlConnectionStringBuilder no PowerShell 5.1; foi corrigido usando as chaves SQL do builder. O setup concluiu ao ser reexecutado sobre essa base inicializada, sem repetir seeds.

A API separada na porta 5279 autenticou os três usuários dessa instalação e passou os dez grupos de senhas via HTTP real. Após a correção, a base LocalDB inicial foi desanexada e seus dois arquivos arquivados em `%LOCALAPPDATA%/TechHelpSolutionsHashQA/`, sem DROP. Foi então executado um ciclo completo de instalação limpa com o setup corrigido: criação, seeds com hashes, validação e todos os objetos NP2, concluído sem erro. A segunda execução também passou sem repetir seeds. O setup também passou na instância original `.\SQLEXPRESS`, com zero conversões adicionais. O parse PowerShell foi aprovado. Os relatórios `setup_*.json` identificam a base/estado verificados. A instância LocalDB de teste e seus arquivos foram preservados para inspeção; não foi apagado/recriado o banco original.

Limite: não foi ensaiada uma restauração completa do backup sobre a base original; apenas RESTORE VERIFYONLY WITH CHECKSUM. Não há alegação de teste visual em navegador nesta etapa.

## Fonte e compilação

O export SQL foi comparado ao original após neutralizar apenas os três valores de senha: restante idêntico. Program.cs/JWT, frontend e scripts NP2 02–06 mantiveram seu conteúdo. A compilação da API e testes passou. Foi necessário excluir `api/tests/**` da compilação/publicação Web, pois artefatos de testes restaurados nessa subpasta geravam atributos de assembly duplicados; os testes oficiais continuam em `tests/`.

Chave JWT fixa, CORS aberto, armazenamento local de tokens e HTTP de desenvolvimento permanecem limitações. A mudança de senha não revoga JWTs já emitidos; o mecanismo de tokens foi preservado. Senhas de demonstração continuam previsíveis e não são apropriadas para produção, mesmo com hash seguro.
