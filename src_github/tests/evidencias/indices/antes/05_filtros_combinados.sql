-- SQL gerado por ToQueryString para reproducao manual.
-- A captura usa o comando EF parametrizado, nao estes DECLAREs.
DECLARE @__statusId_0 int = 2;
DECLARE @__priorityId_1 int = 2;
DECLARE @__categoryId_2 int = 2;

SELECT [v].[Id], [v].[Titulo], [v].[Descricao], [v].[UsuarioId], [v].[TecnicoId], [v].[CategoriaId], [v].[PrioridadeId], [v].[StatusId], [v].[DataAbertura], [v].[DataFechamento], [v].[UsuarioNome], [v].[UsuarioEmail], [v].[UsuarioTipo], CASE
    WHEN [v].[TecnicoId] IS NULL THEN CAST(1 AS bit)
    ELSE CAST(0 AS bit)
END, [v].[TecnicoNome], [v].[TecnicoEmail], [v].[TecnicoTipo], [v].[CategoriaNome], [v].[PrioridadeNome], [v].[StatusNome]
FROM [dbo].[vw_ChamadosDetalhados] AS [v]
WHERE [v].[StatusId] = @__statusId_0 AND [v].[PrioridadeId] = @__priorityId_1 AND [v].[CategoriaId] = @__categoryId_2
ORDER BY [v].[DataAbertura] DESC