namespace TechHelpSolutions.Data.Queries;

public static class FuncoesSql
{
    // Somente em LINQ traduzido pelo EF; nao oferece calculo alternativo no cliente.
    public static double? DuracaoMinutos(DateTime abertura, DateTime? fechamento)
        => throw new NotSupportedException("Use esta funcao em uma consulta LINQ ao SQL Server.");
}
