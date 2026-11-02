# Checklist manual de validação visual — pendente

Data de preparação: 02/11/2026. **Nenhum item visual deste documento foi executado.** Todos os resultados abaixo são esperados, não observados. O host do navegador estava indisponível; Computer Use recusou acesso ao Chrome. Não foram produzidas imagens artificiais ou capturas substitutas.

## Preparação e proteção dos dados

1. Partir de `src_github` e iniciar dois terminais com os comandos do README:
   - `dotnet run --project api/UsuariosApi.csproj --launch-profile http`
   - `python -m http.server 5500 --bind 127.0.0.1 --directory frontend`
2. Não executar setup, migrations, seeds ou remoção de índices. Confirmar a base e registrar contagens/hash antes dos testes. A referência está em `01_banco_antes.json`.
3. Usar contas de demonstração somente para leitura/login. Obter as senhas por canal privado, sem registrar em notas, screenshots ou console. Não fotografar formulários de login preenchidos nem painéis de rede com tokens.
4. Para cadastro/escritas, preparar duas contas temporárias A/B e um chamado exclusivo com marcador `NP2_VISUAL_<identificador>`. Anotar seus IDs privadamente. Usar apenas esses registros; não editar os três chamados originais. A interface não possui exclusão de usuário: combinar antes uma limpeza controlada pelo responsável do banco, por IDs e marcador exatos, sem atingir seeds. A exclusão de chamado exige Admin na API e não deve ser presumida disponível na interface.
5. Abrir DevTools em Console/Network, sem gravar tokens; registrar apenas mensagens sanitizadas, códigos HTTP e ausência/presença de erros. Manter evidências de desktop e janela estreita separadas; anotar tamanho da janela, navegador e versão.
6. Ao terminar, remover somente fixtures criadas nesta sessão, respeitando FKs, e conferir hash/contagens novamente. Não executar reseed. Consumo de IDENTITY por INSERT não equivale a alteração de registros originais.

## Páginas e fluxos

| ID | Passos manuais | Resultado esperado e comprovação |
|---|---|---|
| V01 | Abrir `/pages/login.html` sem sessão. Conferir e-mail, senha, Entrar e Cadastre-se. Submeter vazio, e-mail inválido e credenciais inválidas. | Campos legíveis, senha mascarada, validação nativa e erro legível; sem erro JS. Capturar tela vazia, sem credenciais. |
| V02 | Pelo link, abrir cadastro. Conferir nome, e-mail, tipo, senha e confirmação. Tentar confirmação diferente e campos obrigatórios vazios. | Apenas Usuario, controle de perfil desabilitado; nenhuma opção Tecnico/Gestor/Admin. Mensagem de senhas diferentes, validações preservadas. |
| V03 | Cadastrar conta temporária A com dados fictícios e nome `QA <b>Texto</b> & "Teste"`. Conferir requisição sem expor body de senha em imagem. | POST `/api/usuarios` com `tipo: Usuario`; sucesso e redirecionamento para login. Erros não devem gerar HTML interpretado. |
| V04 | Entrar com A. Conferir `home-usuario.html`, saudação, indicadores, estado vazio/lista, Sair e Abrir Chamado. | Redirecionamento ao painel do usuário. Nome com marcação deve aparecer como texto literal, sem negrito causado por `<b>`. Sem dados de outros solicitantes. |
| V05 | Abrir `abrir-chamado.html`. Conferir título, categoria, prioridade, descrição, Enviar, Cancelar e Voltar. Tentar submeter vazio; depois criar chamado temporário com título `NP2_VISUAL <b>Título</b> & "QA"` e descrição `Descrição <i>literal</i> & teste`. | Catálogos carregados pela API; obrigatórios validados; POST bem-sucedido e retorno ao painel. Texto literal no card, sem elementos criados pela marcação. |
| V06 | Editar o chamado temporário ainda Aberto; alterar texto/categoria/prioridade e salvar. Testar Cancelar/Voltar sem salvar. | Formulário preenchido, PUT bem-sucedido, lista atualizada. Cancelar não grava. Após sair de Aberto, conferir estado de edição desabilitado conforme interface existente. |
| V07 | Abrir Comentários do chamado temporário. Conferir título, metadados, textarea, botão Enviar comentário e Voltar. Enviar `Comentário <b>literal</b> & "QA"`. | GETs do detalhe/comentários e POST funcionando; mensagem literal, nome público/data e ordem cronológica preservados; campo limpo e mensagem de sucesso. Sem senha na resposta. |
| V08 | Criar/logar B. Consultar painel e tentar abrir detalhe/comentários/edição de A pela URL, usando apenas o ID temporário. | Lista de B não mostra chamado de A; API nega acesso alheio (403 no detalhe/comentários). Mensagem da interface legível, sem vazamento do conteúdo. Query string não amplia acesso. |
| V09 | Sair e entrar com técnico de demonstração. Conferir `home-tecnico.html`, filtros prioridade/status, cards e botões. Combinar filtros e limpar seleção. | Login vai ao painel técnico. Listagem/filtros refletem GET `/api/chamados`, em ordem decrescente de abertura. Sem erro JS ou deformação pelos textos especiais. |
| V10 | No técnico, assumir exclusivamente o chamado temporário; consultar também seus comentários e enviar comentário fictício. | PUT `/assumir` 204; responsável/status atualizados após recarga; ação não dispara duas vezes; nome/textos mantidos. A interface desabilita Assumir quando já há técnico; reatribuição permitida no backend não deve ser presumida oferecida na tela. |
| V11 | Finalizar o chamado temporário como técnico responsável. | PUT `/finalizar` 204; status/data refletidos; botão finalização desabilitado quando finalizado. Conferir novamente os comentários existentes, inclusive envio após fechamento se oferecido pela tela atual. |
| V12 | Sair e entrar com a conta de gestor de demonstração (tipo persistido Admin). Conferir `home-gestor.html`. | Login direciona ao painel do gestor. Totais, abertos, tempo médio, SLA, gráficos por dia/setor/status aparecem sem NaN/erros. Comparar com os dados GET e as fórmulas existentes, sem impor novos indicadores. O intervalo de dias depende da data atual. |
| V13 | Fazer logout, tentar acessar páginas protegidas por URL e testar links de volta/home em cada perfil permitido. | Sessão removida, navegação protegida volta ao login ou painel permitido; chamadas da API sem token retornam 401. Nenhum novo privilégio é concedido pela tela. |
| V14 | Em cada página, inspecionar janela desktop e estreita; navegar com Tab e observar Console. | Ausência de sobreposição, campos cortados, rolagem horizontal indevida, erros JS ou controles inacessíveis. Registrar cada problema com página, passos e dimensão. Não considerar esses critérios aprovados sem observar. |

Não é necessário usar scripts ou handlers executáveis para o teste visual de texto literal. Usar somente os textos inofensivos acima. Os testes automatizados de payloads já estão registrados separadamente.

## Plano de capturas reais — arquivos ainda não existem

Salvar em `tests/evidencias/frontend/`, somente após executar os passos e conferir ausência de credenciais/dados pessoais desnecessários. Se o perfil de demonstração exibir nome pessoal, usar uma fixture de nome fictício para a captura. Não alterar conta original para ocultar dados.

| Nome sugerido | O que deverá comprovar |
|---|---|
| `01_login.png` | Campos vazios e navegação para cadastro. |
| `02_cadastro_usuario.png` | Cadastro público com somente Usuario, sem credenciais preenchidas. |
| `03_painel_usuario.png` | Chamado temporário próprio, contadores e texto literal. |
| `04_abrir_chamado.png` | Formulário e catálogos, sem submissão de registro original. |
| `05_comentarios.png` | Autoria pública, data, mensagem literal e formulário. |
| `06_painel_tecnico.png` | Filtros, cards e ações disponíveis. |
| `07_chamado_assumido.png` | Responsável/status após assumir o temporário. |
| `08_chamado_finalizado.png` | Estado finalizado após a operação. |
| `09_painel_gestor.png` | Indicadores e gráficos renderizados. |

Cada captura futura precisa de legenda com data, navegador/dimensão, passo Vxx e resultado observado. Uma imagem comprova o estado visível, não o rollback, concorrência ou segurança do backend por si só.
