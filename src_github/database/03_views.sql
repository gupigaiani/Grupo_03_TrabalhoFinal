SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
-- Campos de usuario limitados aos que o UsuarioResumoDTO ja publica na API.
-- Sem senhas/tokens. Ordenacao e filtros pertencem ao consumidor.
CREATE OR ALTER VIEW dbo.vw_ChamadosDetalhados
AS
    SELECT c.Id, c.Titulo, c.Descricao,
           c.UsuarioId, u.Nome AS UsuarioNome, u.Email AS UsuarioEmail, u.Tipo AS UsuarioTipo,
           c.TecnicoId, t.Nome AS TecnicoNome, t.Email AS TecnicoEmail, t.Tipo AS TecnicoTipo,
           c.CategoriaId, ca.Nome AS CategoriaNome,
           c.PrioridadeId, p.Nome AS PrioridadeNome,
           c.StatusId, s.Nome AS StatusNome,
           c.DataAbertura, c.DataFechamento,
           dbo.ufn_DuracaoMinutos(c.DataAbertura, c.DataFechamento) AS DuracaoMinutos
    FROM dbo.Chamados AS c
    INNER JOIN dbo.Usuarios AS u ON u.Id = c.UsuarioId
    LEFT JOIN dbo.Usuarios AS t ON t.Id = c.TecnicoId
    INNER JOIN dbo.Categorias AS ca ON ca.Id = c.CategoriaId
    INNER JOIN dbo.Prioridades AS p ON p.Id = c.PrioridadeId
    INNER JOIN dbo.Status AS s ON s.Id = c.StatusId;
GO
-- Nao filtra por status nem por data de fechamento: preserva todos os comentarios.
CREATE OR ALTER VIEW dbo.vw_ComentariosDetalhados
AS
    SELECT c.Id, c.ChamadoId, c.UsuarioId,
           u.Nome AS UsuarioNome, u.Email AS UsuarioEmail, u.Tipo AS UsuarioTipo,
           c.Mensagem, c.DataEnvio
    FROM dbo.Comentarios AS c
    INNER JOIN dbo.Usuarios AS u ON u.Id = c.UsuarioId;
GO
