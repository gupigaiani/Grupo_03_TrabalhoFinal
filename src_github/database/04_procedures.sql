-- NP2: contratos internos de erro usados pelo ChamadoService.
-- 51101: chamado ausente; 51102: tecnico inexistente/inelegivel;
-- 51103: chamado sem responsavel; 51104: outro responsavel;
-- 51105: estado concorrente; 51106: parametro invalido.
-- O estado esperado e obrigatorio (inclusive seus NULLs), obtido pela API.
-- UPDLOCK por PK le o estado atual mesmo com RCSI e protege ate o commit.
-- A comparacao com EXCEPT trata NULLs como iguais, sem sentinelas.
-- Nao detecta ABA (mudanca e retorno ao mesmo estado); nao ha rowversion na NP1.
-- Reatribuicao e repeticao sequencial de finalizacao continuam permitidas.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_AssumirChamado
    @ChamadoId int,
    @TecnicoId int,
    @TecnicoIdEsperado int,
    @StatusIdEsperado int,
    @DataFechamentoEsperada datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @TransacoesIniciais int = @@TRANCOUNT;
    BEGIN TRY
        IF @TransacoesIniciais = 0 BEGIN TRANSACTION;
        ELSE SAVE TRANSACTION usp_AssumirChamado;

        DECLARE @IdAtual int, @TecnicoAtual int, @StatusAtual int,
                @FechamentoAtual datetime2(7);
        SELECT @IdAtual = Id, @TecnicoAtual = TecnicoId,
               @StatusAtual = StatusId, @FechamentoAtual = DataFechamento
        FROM dbo.Chamados WITH (UPDLOCK)
        WHERE Id = @ChamadoId;
        IF @IdAtual IS NULL
            THROW 51101, 'Chamado nao encontrado.', 1;

        -- S lock apenas no usuario consultado, retido ate terminar a validacao/escrita.
        IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WITH (REPEATABLEREAD)
                       WHERE Id = @TecnicoId AND Tipo COLLATE Latin1_General_100_BIN2 = N'Tecnico')
            THROW 51102, 'Tecnico inexistente ou nao elegivel.', 1;

        IF EXISTS (SELECT @TecnicoAtual, @StatusAtual, @FechamentoAtual
                   EXCEPT SELECT @TecnicoIdEsperado, @StatusIdEsperado, @DataFechamentoEsperada)
            THROW 51105, 'O chamado foi alterado por outra operacao. Recarregue e tente novamente.', 1;

        UPDATE dbo.Chamados SET TecnicoId = @TecnicoId, StatusId = 2
        WHERE Id = @ChamadoId;
        -- Assumir preserva DataFechamento, como na NP1. Finalizar usa a hora local da API.
        IF @TransacoesIniciais = 0 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
        BEGIN
            -- XACT_ABORT pode inviabilizar tambem a transacao do chamador.
            IF @TransacoesIniciais = 0 OR XACT_STATE() = -1 ROLLBACK TRANSACTION;
            ELSE ROLLBACK TRANSACTION usp_AssumirChamado;
        END;
        THROW;
    END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_FinalizarChamado
    @ChamadoId int,
    @TecnicoId int,
    @TecnicoIdEsperado int,
    @StatusIdEsperado int,
    @DataFechamentoEsperada datetime2(7),
    @DataFechamento datetime2(7)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @TransacoesIniciais int = @@TRANCOUNT;
    BEGIN TRY
        IF @TransacoesIniciais = 0 BEGIN TRANSACTION;
        ELSE SAVE TRANSACTION usp_FinalizarChamado;

        DECLARE @IdAtual int, @TecnicoAtual int, @StatusAtual int,
                @FechamentoAtual datetime2(7);
        SELECT @IdAtual = Id, @TecnicoAtual = TecnicoId,
               @StatusAtual = StatusId, @FechamentoAtual = DataFechamento
        FROM dbo.Chamados WITH (UPDLOCK)
        WHERE Id = @ChamadoId;
        IF @IdAtual IS NULL
            THROW 51101, 'Chamado nao encontrado.', 1;

        -- S lock apenas no usuario consultado, retido ate terminar a validacao/escrita.
        IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WITH (REPEATABLEREAD)
                       WHERE Id = @TecnicoId AND Tipo COLLATE Latin1_General_100_BIN2 = N'Tecnico')
            THROW 51102, 'Tecnico inexistente ou nao elegivel.', 1;

        IF EXISTS (SELECT @TecnicoAtual, @StatusAtual, @FechamentoAtual
                   EXCEPT SELECT @TecnicoIdEsperado, @StatusIdEsperado, @DataFechamentoEsperada)
            THROW 51105, 'O chamado foi alterado por outra operacao. Recarregue e tente novamente.', 1;

        IF @TecnicoAtual IS NULL
            THROW 51103, 'O chamado precisa ser assumido antes de ser finalizado.', 1;
        IF @TecnicoAtual <> @TecnicoId
            THROW 51104, 'Somente o tecnico responsavel pode finalizar o chamado.', 1;
        IF @DataFechamento IS NULL
            THROW 51106, 'Data de fechamento obrigatoria.', 1;

        UPDATE dbo.Chamados SET StatusId = 3, DataFechamento = @DataFechamento
        WHERE Id = @ChamadoId;
        -- Assumir preserva DataFechamento, como na NP1. Finalizar usa a hora local da API.
        IF @TransacoesIniciais = 0 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
        BEGIN
            -- XACT_ABORT pode inviabilizar tambem a transacao do chamador.
            IF @TransacoesIniciais = 0 OR XACT_STATE() = -1 ROLLBACK TRANSACTION;
            ELSE ROLLBACK TRANSACTION usp_FinalizarChamado;
        END;
        THROW;
    END CATCH;
END;
GO
