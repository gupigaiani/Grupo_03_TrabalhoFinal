-- SQL gerado por ToQueryString para reproducao manual.
-- A captura usa o comando EF parametrizado, nao estes DECLAREs.
DECLARE @__p_1 int = 1;
DECLARE @__ticketId_0 int = 1;

SELECT [c0].[Id], [c0].[CategoriaId], [c0].[DataAbertura], [c0].[DataFechamento], [c0].[Descricao], [c0].[PrioridadeId], [c0].[StatusId], [c0].[TecnicoId], [c0].[Titulo], [c0].[UsuarioId], [u].[Id], [u].[DataCriacao], [u].[Email], [u].[Nome], [u].[Senha], [u].[Tipo], [u0].[Id], [u0].[DataCriacao], [u0].[Email], [u0].[Nome], [u0].[Senha], [u0].[Tipo], [c1].[Id], [c1].[Nome], [p].[Id], [p].[Nome], [s].[Id], [s].[Nome], [s0].[Id], [s0].[ChamadoId], [s0].[DataEnvio], [s0].[Mensagem], [s0].[UsuarioId], [s0].[Id0], [s0].[DataCriacao], [s0].[Email], [s0].[Nome], [s0].[Senha], [s0].[Tipo]
FROM (
    SELECT TOP(@__p_1) [c].[Id], [c].[CategoriaId], [c].[DataAbertura], [c].[DataFechamento], [c].[Descricao], [c].[PrioridadeId], [c].[StatusId], [c].[TecnicoId], [c].[Titulo], [c].[UsuarioId]
    FROM [Chamados] AS [c]
    WHERE [c].[Id] = @__ticketId_0
) AS [c0]
INNER JOIN [Usuarios] AS [u] ON [c0].[UsuarioId] = [u].[Id]
LEFT JOIN [Usuarios] AS [u0] ON [c0].[TecnicoId] = [u0].[Id]
INNER JOIN [Categorias] AS [c1] ON [c0].[CategoriaId] = [c1].[Id]
INNER JOIN [Prioridades] AS [p] ON [c0].[PrioridadeId] = [p].[Id]
INNER JOIN [Status] AS [s] ON [c0].[StatusId] = [s].[Id]
LEFT JOIN (
    SELECT [c2].[Id], [c2].[ChamadoId], [c2].[DataEnvio], [c2].[Mensagem], [c2].[UsuarioId], [u1].[Id] AS [Id0], [u1].[DataCriacao], [u1].[Email], [u1].[Nome], [u1].[Senha], [u1].[Tipo]
    FROM [Comentarios] AS [c2]
    INNER JOIN [Usuarios] AS [u1] ON [c2].[UsuarioId] = [u1].[Id]
) AS [s0] ON [c0].[Id] = [s0].[ChamadoId]
ORDER BY [c0].[Id], [u].[Id], [u0].[Id], [c1].[Id], [p].[Id], [s].[Id], [s0].[Id]