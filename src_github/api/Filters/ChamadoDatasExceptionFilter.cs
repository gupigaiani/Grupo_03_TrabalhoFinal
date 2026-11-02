using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;

namespace TechHelpSolutions.Filters;

// Restrito ao ChamadosController. Cobre SqlException direta e encapsulada pelo EF.
public sealed class ChamadoDatasExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception.GetBaseException() is not SqlException sqlException ||
            !sqlException.Errors.Cast<SqlError>().Any(error => error.Number == 51201))
            return;

        // Mensagem publica fixa; nao repassa ex.Message, nomes SQL ou stack trace.
        context.Result = new BadRequestObjectResult(
            "A data de fechamento não pode ser anterior à data de abertura.");
        context.ExceptionHandled = true;
    }
}
