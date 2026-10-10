using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace OpenTrpg.Core.Api.Errors;

/// <summary>
/// Maps <see cref="AppException"/> and <see cref="DomainException"/> to a ProblemDetails (RFC 9457) response.
/// A <see cref="DbUpdateConcurrencyException"/> (optimistic concurrency token changed by another
/// request, e.g. two purchases of the last unit) becomes a 409, and so does a unique index violation
/// (e.g. two sessions created at the same time getting the same number): the client retries.
/// </summary>
internal sealed class AppExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public const string UniqueViolationMessage = "Otra operación simultánea ha creado el mismo dato. Vuelve a intentarlo.";

    public const string ConcurrencyMessage = "Los datos han cambiado mientras se procesaba la operación. Vuelve a intentarlo.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        AppErrorKind kind;
        IReadOnlyDictionary<string, string[]>? errors = null;
        var detail = exception.Message;
        string? code = null;
        switch (exception)
        {
            case AppException app:
                kind = app.Kind;
                errors = app.Errors;
                code = app.Code;
                break;
            case DomainException domain:
                kind = ToAppErrorKind(domain.Kind);
                code = domain.Code;
                break;
            case DbUpdateConcurrencyException:
                kind = AppErrorKind.Conflict;
                detail = ConcurrencyMessage;
                break;
            case DbUpdateException update when IsUniqueViolation(update):
                kind = AppErrorKind.Conflict;
                detail = UniqueViolationMessage;
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
            AppErrorKind.PayloadTooLarge => (StatusCodes.Status413PayloadTooLarge, "El fichero es demasiado grande."),
            _ => (StatusCodes.Status500InternalServerError, "Error inesperado."),
        };

        ProblemDetails problem = errors is not null
            ? new HttpValidationProblemDetails(errors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();
        problem.Status = status;
        problem.Title = title;
        problem.Detail = detail;
        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary>PostgreSQL error 23505, or the equivalent SQLite constraint failure (used by the tests).</summary>
    private static bool IsUniqueViolation(DbUpdateException exception) => exception.InnerException switch
    {
        PostgresException postgres => postgres.SqlState == PostgresErrorCodes.UniqueViolation,
        { } inner when inner.GetType().Name == "SqliteException" => inner.Message.Contains("UNIQUE constraint failed", StringComparison.Ordinal),
        _ => false,
    };

    private static AppErrorKind ToAppErrorKind(DomainErrorKind kind) => kind switch
    {
        DomainErrorKind.Forbidden => AppErrorKind.Forbidden,
        DomainErrorKind.NotFound => AppErrorKind.NotFound,
        DomainErrorKind.Conflict => AppErrorKind.Conflict,
        _ => AppErrorKind.Validation,
    };
}
