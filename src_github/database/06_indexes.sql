-- Indices estreitos para igualdade + ordem cronologica nas consultas existentes.
-- Nao sao covering: titulo/descricao/mensagem nvarchar(max) permanecem na tabela.
-- Os indices simples de FK sobrepostos sao preservados ate haver carga/uso representativo.
-- StatusId + data foi adiado: baixa cardinalidade e nenhuma evidencia de beneficio liquido.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
BEGIN TRY
    BEGIN TRANSACTION;

    -- GET /api/chamados/meus: solicitante exato, mais recentes primeiro.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id=OBJECT_ID(N'dbo.Chamados') AND name=N'IX_Chamados_UsuarioId_DataAbertura')
        CREATE NONCLUSTERED INDEX IX_Chamados_UsuarioId_DataAbertura
            ON dbo.Chamados (UsuarioId ASC, DataAbertura DESC);

    -- GET /api/chamados/meus-atendimentos: tecnico exato, mais recentes primeiro.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id=OBJECT_ID(N'dbo.Chamados') AND name=N'IX_Chamados_TecnicoId_DataAbertura')
        CREATE NONCLUSTERED INDEX IX_Chamados_TecnicoId_DataAbertura
            ON dbo.Chamados (TecnicoId ASC, DataAbertura DESC);

    -- GET /api/chamados/{id}/comentarios: chamado exato, mensagens em ordem crescente.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE object_id=OBJECT_ID(N'dbo.Comentarios') AND name=N'IX_Comentarios_ChamadoId_DataEnvio')
        CREATE NONCLUSTERED INDEX IX_Comentarios_ChamadoId_DataEnvio
            ON dbo.Comentarios (ChamadoId ASC, DataEnvio ASC);

    -- Nao basta existir o nome: uma definicao divergente exige intervencao explicita.
    DECLARE @Esperados TABLE (Tabela sysname, Indice sysname, Primeira sysname, Segunda sysname, Descendente bit);
    INSERT @Esperados VALUES
        (N'Chamados',N'IX_Chamados_UsuarioId_DataAbertura',N'UsuarioId',N'DataAbertura',1),
        (N'Chamados',N'IX_Chamados_TecnicoId_DataAbertura',N'TecnicoId',N'DataAbertura',1),
        (N'Comentarios',N'IX_Comentarios_ChamadoId_DataEnvio',N'ChamadoId',N'DataEnvio',0);
    DECLARE @Inconsistencias nvarchar(2048);
    SELECT @Inconsistencias=STRING_AGG(CONVERT(nvarchar(max),e.Indice),N', ')
    FROM @Esperados e
    WHERE NOT EXISTS (
        SELECT 1 FROM sys.indexes i
        JOIN sys.index_columns k1 ON k1.object_id=i.object_id AND k1.index_id=i.index_id AND k1.key_ordinal=1
        JOIN sys.index_columns k2 ON k2.object_id=i.object_id AND k2.index_id=i.index_id AND k2.key_ordinal=2
        WHERE i.object_id=OBJECT_ID(N'dbo.'+QUOTENAME(e.Tabela)) AND i.name=e.Indice
          AND i.type=2 AND i.is_unique=0 AND i.is_primary_key=0
          AND i.is_disabled=0 AND i.is_hypothetical=0 AND i.has_filter=0
          AND COL_NAME(k1.object_id,k1.column_id)=e.Primeira AND k1.is_descending_key=0
          AND COL_NAME(k2.object_id,k2.column_id)=e.Segunda AND k2.is_descending_key=e.Descendente
          AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.key_ordinal>0)=2
          AND NOT EXISTS (SELECT 1 FROM sys.index_columns k WHERE k.object_id=i.object_id AND k.index_id=i.index_id AND k.is_included_column=1)
    );
    IF @Inconsistencias IS NOT NULL
    BEGIN
        SET @Inconsistencias=N'Indice ausente ou com definicao divergente (nenhum indice existente sera substituido): '+@Inconsistencias;
        THROW 51301, @Inconsistencias, 1;
    END;

    COMMIT TRANSACTION;
    PRINT N'Tres indices compostos verificados: nomes, colunas, ordem e habilitacao corretos.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
