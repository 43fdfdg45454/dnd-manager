namespace OpenTrpg.Core.Application.Common;

public enum AppErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    PayloadTooLarge,
}

/// <summary>
/// Expected business error raised by a use case. The API maps it to a ProblemDetails response
/// whose status depends on <see cref="Kind"/>. Messages are user-facing (Spanish).
/// </summary>
public sealed class AppException : Exception
{
    private AppException(AppErrorKind kind, string message, IReadOnlyDictionary<string, string[]>? errors, string? code = null)
        : base(message)
    {
        Kind = kind;
        Errors = errors;
        Code = code;
    }

    public AppErrorKind Kind { get; }

    /// <summary>Stable machine-readable code of the case, sent as <c>code</c> in the ProblemDetails; null for most errors.</summary>
    public string? Code { get; }

    /// <summary>Field errors keyed by camelCase field name, for validation-like failures.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public static AppException Validation(string field, string message, string? code = null) =>
        new(AppErrorKind.Validation, message, new Dictionary<string, string[]> { [field] = [message] }, code);

    /// <summary>Several field errors at once (keys in camelCase); the message is the first one.</summary>
    public static AppException Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(AppErrorKind.Validation, errors.Values.First()[0], errors);

    public static AppException Unauthorized(string message) => new(AppErrorKind.Unauthorized, message, null);

    public static AppException Forbidden(string message) => new(AppErrorKind.Forbidden, message, null);

    public static AppException NotFound(string message) => new(AppErrorKind.NotFound, message, null);

    public static AppException Conflict(string message, string? code = null) => new(AppErrorKind.Conflict, message, null, code);

    public static AppException PayloadTooLarge(string message) => new(AppErrorKind.PayloadTooLarge, message, null);
}
