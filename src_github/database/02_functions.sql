-- Date no navegador usa milissegundos: subtrai endpoints truncados a essa precisao.
-- Nao usar DATEDIFF(MINUTE), que contaria fronteiras de minutos inteiros.
-- FLOAT corresponde ao Number do JavaScript; nao ha arredondamento de exibicao aqui.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER FUNCTION dbo.ufn_DuracaoMinutos
(
    @DataAbertura datetime2(7),
    @DataFechamento datetime2(7)
)
RETURNS float
WITH SCHEMABINDING
AS
BEGIN
    RETURN CASE
        WHEN @DataAbertura IS NULL OR @DataFechamento IS NULL THEN NULL
        WHEN @DataFechamento < @DataAbertura THEN CONVERT(float, 0)
        ELSE CONVERT(float, DATEDIFF_BIG(MILLISECOND, @DataAbertura, @DataFechamento)) / 60000.0
    END;
END;
GO
