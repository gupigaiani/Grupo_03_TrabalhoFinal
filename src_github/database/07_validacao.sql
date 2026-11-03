-- Validacao somente de leitura. Execute em TechHelpSolutions com sqlcmd -b.
-- Reexecucao exige IDs originais; alteracoes de conteudo pela API nao sao sobrescritas.
SET NOCOUNT ON;
IF DB_NAME() <> N'TechHelpSolutions' THROW 51000, 'Execute a validacao no banco TechHelpSolutions.', 1;
DECLARE @Erros TABLE (Mensagem nvarchar(1000));
DECLARE @Tabelas TABLE (Nome sysname);
INSERT @Tabelas VALUES (N'__EFMigrationsHistory'),(N'Categorias'),(N'Chamados'),(N'Comentarios'),(N'Prioridades'),(N'Status'),(N'Usuarios');
INSERT @Erros SELECT N'Tabela ausente: dbo.'+Nome FROM @Tabelas WHERE OBJECT_ID(N'dbo.'+QUOTENAME(Nome),N'U') IS NULL;
DECLARE @Colunas TABLE (Tabela sysname, Nome sysname, Tipo sysname, Tamanho int, Escala int, Nula bit, Identidade bit);
INSERT @Colunas VALUES
(N'__EFMigrationsHistory',N'MigrationId',N'nvarchar',300,0,0,0),
(N'__EFMigrationsHistory',N'ProductVersion',N'nvarchar',64,0,0,0),
(N'Categorias',N'Id',N'int',4,0,0,1),
(N'Categorias',N'Nome',N'nvarchar',-1,0,0,0),
(N'Chamados',N'Id',N'int',4,0,0,1),
(N'Chamados',N'Titulo',N'nvarchar',-1,0,0,0),
(N'Chamados',N'Descricao',N'nvarchar',-1,0,0,0),
(N'Chamados',N'UsuarioId',N'int',4,0,0,0),
(N'Chamados',N'TecnicoId',N'int',4,0,1,0),
(N'Chamados',N'CategoriaId',N'int',4,0,0,0),
(N'Chamados',N'PrioridadeId',N'int',4,0,0,0),
(N'Chamados',N'StatusId',N'int',4,0,0,0),
(N'Chamados',N'DataAbertura',N'datetime2',8,7,0,0),
(N'Chamados',N'DataFechamento',N'datetime2',8,7,1,0),
(N'Comentarios',N'Id',N'int',4,0,0,1),
(N'Comentarios',N'ChamadoId',N'int',4,0,0,0),
(N'Comentarios',N'UsuarioId',N'int',4,0,0,0),
(N'Comentarios',N'Mensagem',N'nvarchar',-1,0,0,0),
(N'Comentarios',N'DataEnvio',N'datetime2',8,7,0,0),
(N'Prioridades',N'Id',N'int',4,0,0,1),
(N'Prioridades',N'Nome',N'nvarchar',-1,0,0,0),
(N'Status',N'Id',N'int',4,0,0,1),
(N'Status',N'Nome',N'nvarchar',-1,0,0,0),
(N'Usuarios',N'Id',N'int',4,0,0,1),
(N'Usuarios',N'Nome',N'nvarchar',-1,0,0,0),
(N'Usuarios',N'Email',N'nvarchar',-1,0,0,0),
(N'Usuarios',N'Senha',N'nvarchar',-1,0,0,0),
(N'Usuarios',N'Tipo',N'nvarchar',-1,0,0,0),
(N'Usuarios',N'DataCriacao',N'datetime2',8,7,0,0);

INSERT @Erros
SELECT N'Coluna ausente ou incompativel: dbo.'+e.Tabela+N'.'+e.Nome
FROM @Colunas e
LEFT JOIN sys.columns c ON c.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.Tabela)) AND c.name=e.Nome
WHERE c.column_id IS NULL OR TYPE_NAME(c.user_type_id)<>e.Tipo
 OR c.max_length<>e.Tamanho OR (e.Tipo=N'datetime2' AND c.scale<>e.Escala)
 OR c.is_nullable<>e.Nula OR c.is_identity<>e.Identidade;
INSERT @Erros
SELECT N'IDENTITY divergente: dbo.'+e.Tabela+N'.'+e.Nome
FROM @Colunas e JOIN sys.identity_columns c
 ON c.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.Tabela)) AND c.name=e.Nome
WHERE e.Identidade=1 AND (CONVERT(bigint,c.seed_value)<>1 OR CONVERT(bigint,c.increment_value)<>1);
INSERT @Erros
SELECT N'PK ausente ou divergente: dbo.'+t.Nome FROM @Tabelas t
WHERE NOT EXISTS (
 SELECT 1 FROM sys.key_constraints k JOIN sys.indexes i ON i.object_id=k.parent_object_id AND i.index_id=k.unique_index_id
 JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal=1
 JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
 WHERE k.parent_object_id=OBJECT_ID(N'dbo.'+QUOTENAME(t.Nome)) AND k.type='PK'
 AND k.name=N'PK_'+t.Nome AND i.type=1 AND i.is_disabled=0
 AND c.name=CASE WHEN t.Nome=N'__EFMigrationsHistory' THEN N'MigrationId' ELSE N'Id' END
 AND (SELECT COUNT(*) FROM sys.index_columns x WHERE x.object_id=i.object_id AND x.index_id=i.index_id AND x.key_ordinal>0)=1);
DECLARE @FK TABLE (Tabela sysname, Nome sysname, Coluna sysname, Referencia sysname, ColunaReferencia sysname, Cascata int);
INSERT @FK VALUES
(N'Chamados',N'FK_Chamados_Categorias_CategoriaId',N'CategoriaId',N'Categorias',N'Id',1),
(N'Chamados',N'FK_Chamados_Prioridades_PrioridadeId',N'PrioridadeId',N'Prioridades',N'Id',1),
(N'Chamados',N'FK_Chamados_Status_StatusId',N'StatusId',N'Status',N'Id',1),
(N'Chamados',N'FK_Chamados_Usuarios_TecnicoId',N'TecnicoId',N'Usuarios',N'Id',0),
(N'Chamados',N'FK_Chamados_Usuarios_UsuarioId',N'UsuarioId',N'Usuarios',N'Id',0),
(N'Comentarios',N'FK_Comentarios_Chamados_ChamadoId',N'ChamadoId',N'Chamados',N'Id',1),
(N'Comentarios',N'FK_Comentarios_Usuarios_UsuarioId',N'UsuarioId',N'Usuarios',N'Id',1);

INSERT @Erros SELECT N'FK ausente, desabilitada, nao confiavel ou divergente: '+e.Nome
FROM @FK e WHERE NOT EXISTS (
 SELECT 1 FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id
 WHERE f.name=e.Nome AND f.parent_object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.Tabela))
 AND f.referenced_object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.Referencia))
 AND COL_NAME(fc.parent_object_id,fc.parent_column_id)=e.Coluna
 AND COL_NAME(fc.referenced_object_id,fc.referenced_column_id)=e.ColunaReferencia
 AND f.is_disabled=0 AND f.is_not_trusted=0 AND f.delete_referential_action=e.Cascata
 AND f.update_referential_action=0
 AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id=f.object_id)=1);
IF EXISTS (SELECT 1 FROM @Erros)
BEGIN
 SELECT Mensagem AS Inconsistencia FROM @Erros ORDER BY Mensagem;
 DECLARE @Mensagem nvarchar(2048)=N'Instalacao parcial/incompativel: ';
 SELECT @Mensagem=LEFT(@Mensagem+Mensagem+N'; ',2048) FROM @Erros;
 THROW 51000, @Mensagem, 1;
END;
GO
DECLARE @Erros TABLE (Mensagem nvarchar(1000));

INSERT @Erros SELECT N'Registro minimo ausente: dbo.Categorias Id='+CONVERT(nvarchar(12),e.Id) FROM (VALUES (1),(2),(3),(4)) e(Id) WHERE NOT EXISTS (SELECT 1 FROM dbo.[Categorias] a WHERE a.Id=e.Id);
INSERT @Erros SELECT N'Registro minimo ausente: dbo.Chamados Id='+CONVERT(nvarchar(12),e.Id) FROM (VALUES (1),(2),(3)) e(Id) WHERE NOT EXISTS (SELECT 1 FROM dbo.[Chamados] a WHERE a.Id=e.Id);
INSERT @Erros SELECT N'Registro minimo ausente: dbo.Comentarios Id='+CONVERT(nvarchar(12),e.Id) FROM (VALUES (1),(2)) e(Id) WHERE NOT EXISTS (SELECT 1 FROM dbo.[Comentarios] a WHERE a.Id=e.Id);
INSERT @Erros SELECT N'Registro minimo ausente: dbo.Prioridades Id='+CONVERT(nvarchar(12),e.Id) FROM (VALUES (1),(2),(3)) e(Id) WHERE NOT EXISTS (SELECT 1 FROM dbo.[Prioridades] a WHERE a.Id=e.Id);
INSERT @Erros SELECT N'Registro minimo ausente: dbo.Status Id='+CONVERT(nvarchar(12),e.Id) FROM (VALUES (1),(2),(3)) e(Id) WHERE NOT EXISTS (SELECT 1 FROM dbo.[Status] a WHERE a.Id=e.Id);
INSERT @Erros SELECT N'Registro minimo ausente: dbo.Usuarios Id='+CONVERT(nvarchar(12),e.Id) FROM (VALUES (1),(2),(3)) e(Id) WHERE NOT EXISTS (SELECT 1 FROM dbo.[Usuarios] a WHERE a.Id=e.Id);
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260912153825_InitialCreate' AND ProductVersion=N'9.0.0') INSERT @Erros VALUES (N'Historico ausente/divergente: 20260912153825_InitialCreate');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20261027122452_SeedDadosIniciais' AND ProductVersion=N'9.0.0') INSERT @Erros VALUES (N'Historico ausente/divergente: 20261027122452_SeedDadosIniciais');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20261027124115_SeedUsuariosChamadosComentarios' AND ProductVersion=N'9.0.0') INSERT @Erros VALUES (N'Historico ausente/divergente: 20261027124115_SeedUsuariosChamadosComentarios');
IF EXISTS (SELECT 1 FROM dbo.[Chamados] a LEFT JOIN dbo.[Categorias] b ON b.[Id]=a.[CategoriaId] WHERE a.[CategoriaId] IS NOT NULL AND b.[Id] IS NULL) INSERT @Erros VALUES (N'Integridade referencial violada: FK_Chamados_Categorias_CategoriaId');
IF EXISTS (SELECT 1 FROM dbo.[Chamados] a LEFT JOIN dbo.[Prioridades] b ON b.[Id]=a.[PrioridadeId] WHERE a.[PrioridadeId] IS NOT NULL AND b.[Id] IS NULL) INSERT @Erros VALUES (N'Integridade referencial violada: FK_Chamados_Prioridades_PrioridadeId');
IF EXISTS (SELECT 1 FROM dbo.[Chamados] a LEFT JOIN dbo.[Status] b ON b.[Id]=a.[StatusId] WHERE a.[StatusId] IS NOT NULL AND b.[Id] IS NULL) INSERT @Erros VALUES (N'Integridade referencial violada: FK_Chamados_Status_StatusId');
IF EXISTS (SELECT 1 FROM dbo.[Chamados] a LEFT JOIN dbo.[Usuarios] b ON b.[Id]=a.[TecnicoId] WHERE a.[TecnicoId] IS NOT NULL AND b.[Id] IS NULL) INSERT @Erros VALUES (N'Integridade referencial violada: FK_Chamados_Usuarios_TecnicoId');
IF EXISTS (SELECT 1 FROM dbo.[Chamados] a LEFT JOIN dbo.[Usuarios] b ON b.[Id]=a.[UsuarioId] WHERE a.[UsuarioId] IS NOT NULL AND b.[Id] IS NULL) INSERT @Erros VALUES (N'Integridade referencial violada: FK_Chamados_Usuarios_UsuarioId');
IF EXISTS (SELECT 1 FROM dbo.[Comentarios] a LEFT JOIN dbo.[Chamados] b ON b.[Id]=a.[ChamadoId] WHERE a.[ChamadoId] IS NOT NULL AND b.[Id] IS NULL) INSERT @Erros VALUES (N'Integridade referencial violada: FK_Comentarios_Chamados_ChamadoId');
IF EXISTS (SELECT 1 FROM dbo.[Comentarios] a LEFT JOIN dbo.[Usuarios] b ON b.[Id]=a.[UsuarioId] WHERE a.[UsuarioId] IS NOT NULL AND b.[Id] IS NULL) INSERT @Erros VALUES (N'Integridade referencial violada: FK_Comentarios_Usuarios_UsuarioId');

IF EXISTS (SELECT 1 FROM @Erros)
BEGIN
 SELECT Mensagem AS Inconsistencia FROM @Erros ORDER BY Mensagem;
 DECLARE @Mensagem nvarchar(2048)=N'Inicializacao inconsistente: ';
 SELECT @Mensagem=LEFT(@Mensagem+Mensagem+N'; ',2048) FROM @Erros;
 THROW 51000, @Mensagem, 1;
END;
SELECT N'Categorias' AS Tabela, COUNT_BIG(*) AS Quantidade FROM dbo.Categorias
UNION ALL SELECT N'Prioridades',COUNT_BIG(*) FROM dbo.Prioridades
UNION ALL SELECT N'Status',COUNT_BIG(*) FROM dbo.Status
UNION ALL SELECT N'Usuarios',COUNT_BIG(*) FROM dbo.Usuarios
UNION ALL SELECT N'Chamados',COUNT_BIG(*) FROM dbo.Chamados
UNION ALL SELECT N'Comentarios',COUNT_BIG(*) FROM dbo.Comentarios
UNION ALL SELECT N'__EFMigrationsHistory',COUNT_BIG(*) FROM dbo.__EFMigrationsHistory;
PRINT N'Validacao NP1 concluida: estrutura, registros minimos, historico e integridade referencial verificados.';
GO
