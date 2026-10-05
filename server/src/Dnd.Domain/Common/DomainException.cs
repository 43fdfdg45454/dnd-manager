namespace Dnd.Domain.Common;

/// <summary>Category of a domain rule violation; the API maps it to an HTTP status.</summary>
public enum DomainErrorKind
{
    /// <summary>The operation breaks a business rule (400).</summary>
    RuleViolation,

    /// <summary>The actor lacks the permission the rule requires (403).</summary>
    Forbidden,

    /// <summary>The referenced child entity does not exist (404).</summary>
    NotFound,

    /// <summary>The operation would duplicate existing state (409).</summary>
    Conflict,
}

/// <summary>
/// Raised by entities when an operation violates a domain rule. Messages are user-facing (Spanish).
/// </summary>
public sealed class DomainException : Exception
{
    private DomainException(DomainErrorKind kind, string message)
        : base(message)
    {
        Kind = kind;
    }

    public DomainErrorKind Kind { get; }

    public static DomainException RuleViolation(string message) => new(DomainErrorKind.RuleViolation, message);

    public static DomainException Forbidden(string message) => new(DomainErrorKind.Forbidden, message);

    public static DomainException NotFound(string message) => new(DomainErrorKind.NotFound, message);

    public static DomainException Conflict(string message) => new(DomainErrorKind.Conflict, message);
}
