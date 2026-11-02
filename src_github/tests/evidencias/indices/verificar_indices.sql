-- Somente leitura. Executar no banco TechHelpSolutions com sqlcmd -b.
SET NOCOUNT ON;
DECLARE @Esperados TABLE (Tabela sysname, Indice sysname, Primeira sysname, Segunda sysname, Descendente bit);
INSERT @Esperados VALUES
    (N'Chamados',N'IX_Chamados_UsuarioId_DataAbertura',N'UsuarioId',N'DataAbertura',1),
    (N'Chamados',N'IX_Chamados_TecnicoId_DataAbertura',N'TecnicoId',N'DataAbertura',1),
    (N'Comentarios',N'IX_Comentarios_ChamadoId_DataEnvio',N'ChamadoId',N'DataEnvio',0);
IF EXISTS (
    SELECT 1 FROM @Esperados e WHERE NOT EXISTS (
        SELECT 1 FROM sys.indexes i
        JOIN sys.index_columns k1 ON k1.object_id=i.object_id AND k1.index_id=i.index_id AND k1.key_ordinal=1
        JOIN sys.index_columns k2 ON k2.object_id=i.object_id AND k2.index_id=i.index_id AND k2.key_ordinal=2
        WHERE i.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.Tabela)) AND i.name=e.Indice
          AND i.type=2 AND i.is_unique=0 AND i.is_disabled=0 AND i.has_filter=0 AND i.is_hypothetical=0
          AND COL_NAME(k1.object_id,k1.column_id)=e.Primeira AND k1.is_descending_key=0
          AND COL_NAME(k2.object_id,k2.column_id)=e.Segunda AND k2.is_descending_key=e.Descendente
          AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)=2
          AND NOT EXISTS (SELECT 1 FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.is_included_column=1)
    )
)
    THROW 51302, 'Indices compostos ausentes ou com definicao divergente.', 1;

-- Os sete indices de FK originais continuam presentes e habilitados.
IF EXISTS (
    SELECT 1 FROM (VALUES
        (N'Chamados',N'CategoriaId'),(N'Chamados',N'PrioridadeId'),(N'Chamados',N'StatusId'),
        (N'Chamados',N'TecnicoId'),(N'Chamados',N'UsuarioId'),
        (N'Comentarios',N'ChamadoId'),(N'Comentarios',N'UsuarioId')
    ) e(Tabela,Coluna)
    WHERE NOT EXISTS (
        SELECT 1 FROM sys.indexes i JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id
        WHERE i.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.Tabela)) AND i.name=N'IX_'+e.Tabela+N'_'+e.Coluna
          AND i.type=2 AND i.is_disabled=0 AND i.has_filter=0
          AND k.key_ordinal=1 AND COL_NAME(k.object_id,k.column_id)=e.Coluna
          AND (SELECT COUNT(*) FROM sys.index_columns x WHERE x.object_id=i.object_id AND x.index_id=i.index_id AND x.key_ordinal>0)=1
    )
)
    THROW 51302, 'Indice de FK original ausente ou divergente.', 1;

SELECT OBJECT_NAME(i.object_id) AS Tabela,i.name AS Indice,c.name AS Coluna,k.key_ordinal,k.is_descending_key
FROM sys.indexes i JOIN sys.index_columns k ON k.object_id=i.object_id AND k.index_id=i.index_id
JOIN sys.columns c ON c.object_id=k.object_id AND c.column_id=k.column_id
JOIN @Esperados e ON i.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.Tabela)) AND i.name=e.Indice
ORDER BY Tabela,Indice,k.key_ordinal;
PRINT N'Indices compostos corretos e indices originais preservados.';
