using Dnd.Application.Common;
using Dnd.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Dnd.Api.Errors;

/// <summary>
/// Maps <see cref="AppException"/> and <see cref="DomainException"/> to a ProblemDetails (RFC 9457) response.
/// </summary>
internal sealed class AppExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        AppErrorKind kind;
        IReadOnlyDictionary<string, string[]>? errors = null;
        switch (exception)
        {
            case AppException app:
                kind = app.Kind;
                errors = app.Errors;
                break;
            case DomainException domain:
                kind = ToAppErrorKind(domain.Kind);
                break;
            default:
                return false;
        }

        var (status, title) = kind switch
        {
            AppErrorKind.Validation => (StatusCodes.Status400BadRequest, "La solicitud no es válida."),
            AppErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "No autorizado."),
            AppErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Acceso denegado."),
            AppErrorKind.NotFound => (StatusCodes.Status404NotFound, "No encontrado."),
            AppErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflicto."),
            _ => (StatusCodes.Status500InternalServerError, "Error inesperado."),
        };

        ProblemDetails problem = errors is not null
            ? new HttpValidationProblemDetails(errors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = title;
        problem.Detail = exception.Message;

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static AppErrorKind ToAppErrorKind(DomainErrorKind kind) => kind switch
    {
        DomainErrorKind.Forbidden => AppErrorKind.Forbidden,
        DomainErrorKind.NotFound => AppErrorKind.NotFound,
        DomainErrorKind.Conflict => AppErrorKind.Conflict,
        _ => AppErrorKind.Validation,
    };
}
