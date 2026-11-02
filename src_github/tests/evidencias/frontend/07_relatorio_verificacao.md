# Verificação frontend e inventário de evidências — 02/11/2026

## Ambiente e limites

Windows, PowerShell, SDK .NET 10.0.401 com alvo/runtime ASP.NET Core 9, SQL Server 2025 Express 17.0.1000.7 em `.\SQLEXPRESS`, Windows Authentication, compatibilidade 170 e READ_COMMITTED_SNAPSHOT ON. Node.js 24.14.0 e Python 3.13.4.

README conferido. Foram iniciados, a partir de `src_github`, exatamente:

```powershell
dotnet run --project api/UsuariosApi.csproj --launch-profile http
python -m http.server 5500 --bind 127.0.0.1 --directory frontend
```

API: `http://localhost:5266`; frontend: `http://127.0.0.1:5500/pages/login.html`. Não foi executado setup, migrations ou reaplicação de seeds.

**Testes visuais: nenhum realizado. Capturas produzidas: nenhuma.** A consulta ao Computer Use encontrou Chrome em execução, mas o host de acesso ao navegador retornou `The browser access host is unavailable`; a tentativa de selecionar a janela pelo Computer Use foi recusada com `Computer Use was not approved to use Google Chrome`. Não houve tentativa de contornar a restrição nem geração de imagens simuladas. Console real, layout, execução dos scripts externos e interação no navegador permanecem não verificados.

## Resultado individual das páginas

| Página | HTTP e arquivos JS/CSS locais | Evidência funcional automatizada | Visual/Console |
|---|---|---|---|
| login.html | 200; referências locais 200 | Login HTTP real nos três perfis originais; redirecionamentos Usuario/Tecnico/Gestor/Admin em DOM simulado | Não verificados |
| cadastro.html | 200; referências locais 200 | HTML oferece somente Usuario; payload fixo/validações em DOM simulado; cadastro e bloqueios HTTP reais no conjunto acesso | Não verificados |
| home-usuario.html | 200; referências locais 200 | DOM simulado com respostas reais; listagem própria e restrições HTTP; texto literal/contadores/links automatizados | Não verificados |
| abrir-chamado.html | 200; referências locais 200 | Formulário/catálogos/envio/edição em DOM simulado; criação/edição HTTP reais nos testes backend | Não verificados |
| comentarios.html | 200; referências locais 200 | DOM simulado com respostas reais; autoria, envio e navegação simulados; GET/POST/permissões HTTP reais | Não verificados |
| home-tecnico.html | 200; referências locais 200 | DOM simulado com respostas reais; filtros/delegação de eventos; filtros/assumir/finalizar HTTP reais | Não verificados |
| home-gestor.html | 200; referências locais 200 | DOM simulado com respostas reais e casos controlados de indicadores/gráficos; listagem HTTP autorizada | Não verificados |

HTTP 200 prova disponibilidade dos arquivos, não renderização ou ausência de falhas de JavaScript. Campos/botões/IDs foram analisados no HTML e exercitados pelos testes de DOM simulado; visibilidade e alinhamento não foram observados. A conta `gestor@gmail.com` é Admin; Gestor literal também foi exercitado com fixture pelo conjunto backend de acesso.

## Execuções desta sessão

```powershell
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://localhost:5266
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://localhost:5266 --consultas
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://localhost:5266 --trigger
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://localhost:5266 --correcoes
dotnet run --project tests/Chamados.IntegrationTests/Chamados.IntegrationTests.csproj -- http://localhost:5266 --acesso
$env:THS_API_READONLY_URL = 'http://localhost:5266'
node --test --test-isolation=none tests/frontend/renderizacao.test.cjs
```

- Procedures/transações/concorrência: **18 grupos aprovados**.
- Consultas/Function/Views: **9 grupos aprovados**, incluindo 45 listagens HTTP.
- Trigger: **15 grupos aprovados**, incluindo criação, edição, atribuição, finalização e exclusão HTTP de fixtures.
- Correções de comentários/status: **10 grupos aprovados**.
- Controle de acesso: **11 grupos aprovados**, incluindo 90 listagens HTTP, contas temporárias e autorização entre solicitantes.
- Total backend: **63 grupos aprovados**; nenhum conjunto falhou.
- Frontend: **23 testes aprovados**, zero falhas/ignorados. As operações de escrita frontend foram simuladas; as escritas HTTP reais são evidenciadas pelos testes backend, não por cliques em navegador.

Os testes de renderização verificam caracteres especiais e seis classes de payload em títulos, descrições, nomes, comentários, catálogos e gráficos. Confirmam texto literal e ausência de elementos ativos no DOM simulado; não demonstram execução segura em navegador real. O cadastro contém somente Usuario e o payload permanece Usuario mesmo após manipulação do controle simulado. Não se usou payload destrutivo nem persistiram payloads nos seeds.

## Arquivos desta coleta

| Arquivo | Conteúdo e limite da evidência |
|---|---|
| `01_banco_antes.json` | Versão, opções, seis objetos SQL, 17 índices, contagens e hash anterior; sem dados brutos/credenciais. |
| `02_resultados_backend.txt` | Saídas OK e totais dos cinco modos; logs sanitizados sem respostas brutas ou JWT. |
| `03_resultados_frontend.txt` | Nomes dos 23 testes e resultados desta execução. |
| `04_http_paginas.json` | HTTP de cada página/referência local, campos do HTML e recursos externos não verificados. |
| `05_preservacao.json` | Contagens e hash final, igualdade com antes e com a evidência original dos índices. |
| `06_checklist_manual.md` | Passos e resultados esperados das verificações ainda pendentes, incluindo plano de nove capturas futuras. |
| `07_relatorio_verificacao.md` | Este relatório de coleta; não é documentação final nem apresentação. |

## Evidências SQL já disponíveis

| Tema | Fontes e evidências utilizáveis |
|---|---|
| Procedures | `database/np2/04_procedures.sql`; `tests/Chamados.IntegrationTests/Program.cs`; resultados 18/18 no log desta sessão. |
| Transações/rollback | TRY/CATCH, XACT_ABORT, transação/savepoint e THROW nas procedures; grupos 8 e testes da Trigger demonstram rollback de escrita anterior e estado transacional. |
| Concorrência | Estado esperado e bloqueio por PK; grupos 9–11 e 16–17 confirmam corridas reais e 409 sem sobrescrita; limites ABA descritos no README dos testes. |
| Function | `database/np2/02_functions.sql`, mapeamento EF e `ConsultasTests.cs`; 11 casos de duração no grupo 4, incluindo NULL, frações, datas negativas e fronteiras. |
| Views | `database/np2/03_views.sql`; equivalência com consultas originais, LEFT JOIN, projeções sem senha, ausência de tracking e contratos HTTP em `ConsultasTests.cs`. |
| Trigger | `database/np2/05_triggers.sql`, `TriggerTests.cs`; 15 grupos comprovam datas, NULL, igualdade, lote misto, rollback e integração com EF/procedures. |
| Índices/planos | `tests/evidencias/indices/antes/` e `depois/`: 14 planos reais `.sqlplan`, SQLs, inventários, ambiente, resumos e mensagens IO/TIME. `comparacao.csv` contém 70 amostras; `relatorio_tecnico.txt` explica método, operadores e limitações. |
| Integridade | `database/np2/07_validacao.sql`; hashes antes/depois das sete tabelas nos testes e nesta coleta. |

Nenhum índice foi removido, recriado ou medido novamente nesta etapa. Há 17 índices atuais (14 originais + três compostos). Nos planos históricos de próprios/atendimentos/comentários, Scan + Sort mudou para Seek + Lookup, sem Sort, mas as leituras aumentaram **32→37, 23→26 e 6→10**. Não houve ganho líquido demonstrado. Os tempos ínfimos e a base de três chamados/dois comentários impedem conclusão confiável de aceleração. As outras consultas mantiveram os comportamentos documentados. Não confundir adequação estrutural a filtros/ordenação com ganho medido.

## Problemas e prioridades

- **Alta — pendência de validação:** acesso ao navegador bloqueado/host indisponível. Impede aprovação visual e capturas reais; executar o checklist em ambiente com acesso antes de apresentar telas como validadas.
- **Alta — limitação preexistente para produção:** senhas sem hash, chave JWT fixa, CORS aberto, HTTP local e token em armazenamento local, conforme README. Nenhuma credencial foi copiada para estas evidências. Não são falhas novas confirmadas nesta sessão; exigem tratamento antes de exposição pública, fora deste escopo.
- **Média — cobertura de recursos externos:** as páginas incluem VLibras remoto e inicialização inline. Sua disponibilidade, falhas de carregamento e acessibilidade real não foram verificadas no navegador. É risco de dependência, não erro de console comprovado.
- **Média — representatividade:** amostra de dados pequena e DOM simulado não comprovam layout responsivo, comportamento completo do navegador ou desempenho em escala.
- **Defeitos funcionais novos comprovados nesta sessão:** nenhum nos testes executados. Isso não equivale a ausência de defeitos visuais.

## Preservação e materiais para a próxima etapa

Comparação integral de registros de Usuarios, Categorias, Prioridades, Status, Chamados, Comentarios e histórico de migrations: hash antes/depois idêntico e igual à referência original:

`20D69479A5A7D342AD3064DAB77D47703091869EFE90F701A2C79927EA3C7355`

Contagens finais: Usuarios 3, Categorias 4, Prioridades 3, Status 3, Chamados 3, Comentarios 2 e histórico 3. Fixtures temporárias removidas pelos testes; nenhum registro original alterado. O hash compara registros, não contador IDENTITY; testes HTTP de criação podem consumir IDs sem reseed.

As fontes protegidas foram comparadas por hash com o início da sessão: API, frontend, SQL, PowerShell, README e testes anteriores não foram modificados. Somente os sete arquivos desta pasta de evidências foram acrescentados.

Estão disponíveis código SQL/EF, planos reais, comparação IO/TIME, logs atuais de 63 grupos backend e 23 testes frontend, registros HTTP das sete páginas, checklist e provas de preservação. Capturas reais e aprovação de layout/console continuam pendentes. Não foram gerados PDFs, slides ou ZIP.
