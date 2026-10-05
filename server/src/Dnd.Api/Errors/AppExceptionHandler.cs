using Dnd.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Dnd.Api.Errors;

/// <summary>Maps <see cref="AppException"/> to a ProblemDetails (RFC 9457) response.</summary>
internal sealed class AppExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException appException)
        {
            return false;
        }

        var (status, title) = appException.Kind switch
        {
            AppErrorKind.Validation => (StatusCodes.Status400BadRequest, "La solicitud no es válida."),
            AppErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "No autorizado."),
            AppErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Acceso denegado."),
            AppErrorKind.NotFound => (StatusCodes.Status404NotFound, "No encontrado."),
            AppErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflicto."),
            _ => (StatusCodes.Status500InternalServerError, "Error inesperado."),
        };

        ProblemDetails problem = appException.Errors is { } errors
            ? new HttpValidationProblemDetails(errors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = title;
        problem.Detail = appException.Message;

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
