SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER TRIGGER dbo.trg_Chamados_ValidarDatas
ON dbo.Chamados
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Um unico registro invalido rejeita toda a operacao, inclusive em lotes.
    -- NULL e igualdade sao permitidos. Nao corrige nem sobrescreve valores.
    IF EXISTS (
        SELECT 1 FROM inserted
        WHERE DataFechamento IS NOT NULL AND DataFechamento < DataAbertura
    )
        THROW 51201, 'A data de fechamento nao pode ser anterior a data de abertura.', 1;
END;
GO
ENABLE TRIGGER dbo.trg_Chamados_ValidarDatas ON dbo.Chamados;
GO
