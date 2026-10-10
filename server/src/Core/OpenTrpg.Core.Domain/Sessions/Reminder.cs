namespace OpenTrpg.Core.Domain.Sessions;

/// <summary>
/// An email reminder scheduled <see cref="OffsetMinutes"/> before a session. Pending while it has
/// neither been sent nor given up on. Created only through <see cref="GameSession"/>.
/// </summary>
public sealed class Reminder
{
    /// <summary>Failed attempts after which the reminder is abandoned (<see cref="FailedAt"/> is set).</summary>
    public const int MaxAttempts = 3;

    /// <summary>Minimum time between two attempts of the same reminder.</summary>
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);

    public const int LastErrorMaxLength = 500;

    private Reminder()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SessionId { get; private set; }

    public int OffsetMinutes { get; private set; }

    /// <summary>Instant (UTC) at which the reminder becomes due.</summary>
    public DateTimeOffset SendAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Failed sending attempts so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>When the last failed attempt happened; drives the retry delay.</summary>
    public DateTimeOffset? LastAttemptAt { get; private set; }

    /// <summary>Set when the reminder was abandoned after <see cref="MaxAttempts"/> failures.</summary>
    public DateTimeOffset? FailedAt { get; private set; }

    public string? LastError { get; private set; }

    public bool IsPending => SentAt is null && FailedAt is null;

    internal static Reminder Create(Guid sessionId, int offsetMinutes, DateTimeOffset sendAt) => new()
    {
        SessionId = sessionId,
        OffsetMinutes = offsetMinutes,
        SendAt = sendAt,
    };

    /// <summary>True when the reminder is due at <paramref name="now"/>: pending, not before <see cref="SendAt"/> and, after a failure, past the retry delay.</summary>
    public bool IsDue(DateTimeOffset now) =>
        IsPending && SendAt <= now && (Attempts == 0 || LastAttemptAt is null || LastAttemptAt.Value + RetryDelay <= now);

    public void MarkSent(DateTimeOffset now, string? warning = null)
    {
        SentAt = now;
        LastError = Truncate(warning);
    }

    /// <summary>Records a failed attempt; the third one abandons the reminder.</summary>
    public void RegisterFailure(DateTimeOffset now, string error)
    {
        Attempts++;
        LastAttemptAt = now;
        LastError = Truncate(error);
        if (Attempts >= MaxAttempts)
        {
            FailedAt = now;
        }
    }

    private static string? Truncate(string? text) =>
        text is { Length: > LastErrorMaxLength } ? text[..LastErrorMaxLength] : text;
}
