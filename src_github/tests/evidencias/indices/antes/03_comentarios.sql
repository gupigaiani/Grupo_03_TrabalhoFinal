-- SQL gerado por ToQueryString para reproducao manual.
-- A captura usa o comando EF parametrizado, nao estes DECLAREs.
DECLARE @__ticketId_0 int = 1;

SELECT [v].[Id], [v].[ChamadoId], [v].[Mensagem], [v].[DataEnvio], [v].[UsuarioId] AS [Id], [v].[UsuarioNome] AS [Nome], [v].[UsuarioEmail] AS [Email], [v].[UsuarioTipo] AS [Tipo]
FROM [dbo].[vw_ComentariosDetalhados] AS [v]
WHERE [v].[ChamadoId] = @__ticketId_0
ORDER BY [v].[DataEnvio]