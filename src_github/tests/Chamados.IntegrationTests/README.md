# Testes de chamados NP2

Executável de integração sem framework de testes adicional. Usa SQL Server real
e a API local; não deve ser executado em produção ou durante alterações de dados
por outros usuários. A comparação final espera todos os registros inalterados.

Na pasta `src_github`, prepare o banco:

```powershell
.\setup_database.ps1
```

Em um terminal, inicie a API:

```powershell
dotnet run --project api/UsuariosApi.csproj --no-launch-profile --urls http://127.0.0.1:5278
```

Em outro terminal, execute:

```powershell
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://127.0.0.1:5278
```

A conta Windows precisa ler e gravar nas tabelas, executar as procedures e
consultar `sys.dm_exec_requests` para observar bloqueios entre sessões
(`VIEW SERVER PERFORMANCE STATE` no SQL Server 2025 ou privilégio equivalente).
O banco deve conter os seeds originais, inclusive o login `usuario@gmail.com`.
Outra conexão pode ser informada por `THS_TEST_CONNECTION`; nesse caso, a API
também precisa apontar para o mesmo banco.

São usados dois técnicos e um chamado com IDs negativos aleatórios e marcador
exclusivo. O `finally` remove somente esses registros. IDs explícitos negativos
não avançam os contadores IDENTITY da base inicial. Os testes de concorrência
precisam confirmar alterações entre conexões; por isso esses registros temporários
são commitados e removidos ao final. Os seeds nunca são atualizados.

As verificações abrangem:

- existência das procedures e atribuição/reatribuição;
- técnico inexistente ou inelegível e chamado ausente;
- finalização válida, sem responsável ou por outro responsável;
- preservação da data de fechamento ao reassumir;
- rollback de escrita anterior em transação externa e ausência de transação aberta;
- duas atribuições, duas finalizações e finalização versus atribuição concorrentes;
- tradução dos erros SQL e invalidação do tracker no serviço;
- respostas HTTP 204, 400, 401, 403, 404 e 409, incluindo corridas reais;
- comparação integral de todas as tabelas e remoção dos registros temporários.

## Contrato de concorrência

As procedures recebem `TecnicoId`, `StatusId` e `DataFechamento` esperados,
obtidos pela leitura sem tracking da API. Não há retry automático com estado
atualizado: isso esconderia o conflito. `UPDLOCK` na consulta por PK mantém
validação e atualização protegidas mesmo com `READ_COMMITTED_SNAPSHOT ON`.
Uma leitura com `REPEATABLEREAD` protege a elegibilidade do técnico durante a
operação. Não se muda o isolamento da conexão nem se bloqueia toda a tabela.

Erros 51101–51106 são o contrato interno SQL/serviço. O conflito 51105 vira
HTTP 409; demais erros inesperados continuam propagados. Cada procedure possui
`XACT_ABORT`, `TRY/CATCH`, transação própria ou savepoint e `THROW` após rollback.
Se uma transação externa ficar inviável, o rollback necessariamente abrange toda
essa transação. Os endpoints usam chamadas independentes, sem transação externa.

A comparação do estado não detecta ABA (mudança seguida de retorno aos mesmos
valores), nem alterações em campos fora desses três. Detectar toda versão de
escrita exigiria um token como `rowversion`, fora do escopo desta etapa.
As rotas de edição e alteração genérica de status continuam com a implementação
anterior; esta etapa integra exclusivamente assumir e finalizar.

## Functions e Views

Depois de executar o setup atualizado e iniciar a API, execute o conjunto de
testes somente de leitura:

```powershell
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://127.0.0.1:5278 --consultas
```

Esse modo verifica os três objetos, o determinismo da Function, equivalência das
Views com os Includes/projeções anteriores, tradução real do EF e ausência de
tracking. Verifica um único comando SQL por consulta às Views, 11 casos de
duração e a média calculada com a mesma seleção do painel. As verificações HTTP
comparam o JSON completo, incluindo NULLs, filtros e ordenação, em 45 listagens
para os três perfis, além de meus chamados, atendimentos, comentários,
detalhamento e respostas de autorização. Nenhum registro é gravado nesse modo.

`ufn_DuracaoMinutos(datetime2(7), datetime2(7))` retorna `float`, equivalente ao
`Number` JavaScript. Usa `DATEDIFF_BIG(MILLISECOND)` entre os endpoints para
reproduzir a precisão em milissegundos de `new Date(...).getTime()`, dividindo
por 60000, sem arredondar minutos inteiros. Por exemplo, o chamado original 1
resulta em aproximadamente `4.34745` minutos. Fechamento ausente retorna NULL;
intervalo negativo retorna zero, como `Math.max(0, ...)`. Não consulta a hora
atual. Os parâmetros e a base continuam sem fuso (`datetime2`), como na NP1.

`vw_ChamadosDetalhados` expõe os relacionamentos e a coluna `DuracaoMinutos`;
`vw_ComentariosDetalhados` mantém todos os comentários, inclusive posteriores
ao fechamento. Os campos nome, e-mail e tipo são os mesmos já publicados pelo
`UsuarioResumoDTO`; senhas e tokens não são selecionados. Não há ORDER BY nas
Views. Ambas são mapeadas por `HasNoKey().ToView(...)`; a Function também possui
`HasDbFunction`, utilizável dentro de LINQ.

As três listagens de chamados e a listagem de comentários usam as Views sem
mudar DTOs. O detalhamento continua com o Include existente, que já recupera
os comentários, evitando acrescentar uma consulta. Não existia paginação e
nenhuma foi introduzida. Operações de escrita permanecem nas entidades normais.

Os indicadores existentes (total, abertos, tempo médio, SLA, distribuição e
taxas) são calculados pelo navegador. Não há endpoint de métricas nem campo
de duração no DTO. Por isso a Function não foi artificialmente executada nas
listagens nem acrescentada ao JSON: ela é reutilizada na View e pode ser
consultada no SQL ou pelo mapeamento EF já validado. O frontend permanece
inalterado. O cálculo de idade de chamados abertos, que usa `Date.now()` no
painel, é separado do tempo de resolução e também permanece inalterado.

O setup valida primeiro a base, depois aplica `02_functions.sql`, `03_views.sql`,
`04_procedures.sql` e `05_triggers.sql` em uma única transação e verifica os seis
objetos antes do commit. A reexecução não repete a carga nem modifica os seeds.

## Trigger de integridade das datas

Com a API iniciada, execute:

```powershell
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://127.0.0.1:5278 --trigger
```

`trg_Chamados_ValidarDatas` é AFTER INSERT/UPDATE e consulta somente `inserted`.
Usa EXISTS para verificar todas as linhas, `NOCOUNT`, `XACT_ABORT` e THROW 51201.
NULL e datas iguais são permitidos. Nenhum valor é corrigido silenciosamente.
Uma violação rejeita o lote; uma transação externa atingida pode ficar inviável
e precisar de rollback integral pelo chamador. As procedures existentes já
executam esse rollback no CATCH e não foram alteradas.

O EF usa `UseSqlOutputClause(false)` somente para Chamado, pois SQL Server não
permite OUTPUT direto em tabela com trigger habilitada. Identidades continuam
sendo recuperadas pelo EF e relacionamentos/propriedades não mudaram.

O filtro de exceção `ChamadoDatasExceptionFilter` está aplicado exclusivamente
ao ChamadosController. Ele reconhece apenas erro 51201, direto ou encapsulado
em DbUpdateException, e retorna HTTP 400 com mensagem fixa, sem repassar texto
SQL, número de erro, nome de trigger ou stack trace. Outros erros mantêm seu
tratamento anterior; não existe novo middleware global.

Os 15 grupos específicos verificam INSERT/UPDATE válidos, NULL, igualdade,
inversão de 100 ns, lotes mistos, rollback de todas as colunas, transação externa,
SaveChanges com várias entidades e falha de finalização na procedure. Os testes
HTTP cobrem criação, edição, atribuição, finalização, exclusão e resposta segura
para erro direto da procedure e erro encapsulado pelo EF no endpoint de status.
Como os DTOs não recebem datas, uma fixture temporária com abertura futura e
sem fechamento reproduz a violação na tentativa de fechar com DateTime.Now.

As fixtures SQL possuem IDs negativos e marcadores exclusivos. Um chamado é
criado e excluído pela API, exercitando a geração normal de IDENTITY; esse teste
consome um valor da sequência, sem executar reseed. O finally limpa somente os
registros do teste. A comparação integral antes/depois confirma que nenhum dado
original foi alterado. A trigger não valida retroativamente linhas antigas;
o teste também verifica que a base existente não contém datas inválidas.
