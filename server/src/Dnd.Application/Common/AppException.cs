namespace Dnd.Application.Common;

public enum AppErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
}

/// <summary>
/// Expected business error raised by a use case. The API maps it to a ProblemDetails response
/// whose status depends on <see cref="Kind"/>. Messages are user-facing (Spanish).
/// </summary>
public sealed class AppException : Exception
{
    private AppException(AppErrorKind kind, string message, IReadOnlyDictionary<string, string[]>? errors)
        : base(message)
    {
        Kind = kind;
        Errors = errors;
    }

    public AppErrorKind Kind { get; }

    /// <summary>Field errors keyed by camelCase field name, for validation-like failures.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public static AppException Validation(string field, string message) =>
        new(AppErrorKind.Validation, message, new Dictionary<string, string[]> { [field] = [message] });

    public static AppException Unauthorized(string message) => new(AppErrorKind.Unauthorized, message, null);

    public static AppException Forbidden(string message) => new(AppErrorKind.Forbidden, message, null);

    public static AppException NotFound(string message) => new(AppErrorKind.NotFound, message, null);

    public static AppException Conflict(string message) => new(AppErrorKind.Conflict, message, null);
}
