using Dnd.Domain.Common;

namespace Dnd.Domain.Sessions;

/// <summary>Attendance answer of one member to a session. Unique per (session, user).</summary>
public sealed class SessionRsvp
{
    public const int CommentMaxLength = 500;

    private SessionRsvp()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; private set; }

    public Guid UserId { get; private set; }

    public RsvpStatus Status { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal static SessionRsvp Create(Guid sessionId, Guid userId, RsvpStatus status, string? comment, DateTimeOffset now)
    {
        var rsvp = new SessionRsvp { SessionId = sessionId, UserId = userId };
        rsvp.Update(status, comment, now);
        return rsvp;
    }

    internal void Update(RsvpStatus status, string? comment, DateTimeOffset now)
    {
        var trimmed = comment?.Trim();
        if (trimmed is { Length: > CommentMaxLength })
        {
            throw DomainException.RuleViolation($"El comentario no puede superar los {CommentMaxLength} caracteres.");
        }

        Status = status;
        Comment = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        UpdatedAt = now;
    }
}
