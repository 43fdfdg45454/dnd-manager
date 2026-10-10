using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Sessions;

/// <summary>
/// A play session of a campaign: when and where it happens, who is coming (RSVPs), the email
/// reminders still to be sent and, once played, the DM's summary for the campaign journal.
/// </summary>
public sealed class GameSession : EntityBase
{
    public const int TitleMaxLength = 200;
    public const int LocationMaxLength = 200;
    public const int NotesMaxLength = 10_000;
    public const int SummaryMaxLength = 100_000;
    public const int MaxDurationMinutes = 24 * 60;

    /// <summary>Length assumed for a session without duration when deciding whether it is still in progress.</summary>
    public const int AssumedDurationMinutes = 240;

    private readonly List<SessionRsvp> _rsvps = [];
    private readonly List<Reminder> _reminders = [];

    private GameSession()
    {
    }

    public Guid CampaignId { get; private set; }

    /// <summary>Correlative per campaign by creation order ("Sesión 12"); never changes.</summary>
    public int Number { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Start instant, always stored in UTC.</summary>
    public DateTimeOffset StartsAt { get; private set; }

    public int? DurationMinutes { get; private set; }

    public string? Location { get; private set; }

    /// <summary>Markdown shown before the session.</summary>
    public string? Notes { get; private set; }

    /// <summary>Markdown story of what happened, written by DMs. Null when there is none.</summary>
    public string? SummaryMarkdown { get; private set; }

    public DateTimeOffset? SummaryUpdatedAt { get; private set; }

    public SessionStatus Status { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<SessionRsvp> Rsvps => _rsvps;

    public IReadOnlyCollection<Reminder> Reminders => _reminders;

    /// <summary>
    /// Creates a scheduled session. Reminders are not generated here: call
    /// <see cref="SyncReminders"/> with the offsets of the campaign.
    /// </summary>
    public static GameSession Create(
        Guid campaignId,
        int number,
        string title,
        DateTimeOffset startsAt,
        int? durationMinutes,
        string? location,
        string? notes,
        Guid createdByUserId,
        DateTimeOffset now) => new()
    {
        CampaignId = campaignId,
        Number = number,
        Title = NormalizeTitle(title),
        StartsAt = startsAt.ToUniversalTime(),
        DurationMinutes = ValidateDuration(durationMinutes),
        Location = NormalizeOptional(location, LocationMaxLength, "El lugar"),
        Notes = NormalizeOptional(notes, NotesMaxLength, "Las notas"),
        Status = SessionStatus.Scheduled,
        CreatedByUserId = createdByUserId,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public SessionRsvp? FindRsvp(Guid userId) => _rsvps.FirstOrDefault(r => r.UserId == userId);

    public void Rename(string title, DateTimeOffset now)
    {
        Title = NormalizeTitle(title);
        UpdatedAt = now;
    }

    public void Reschedule(DateTimeOffset startsAt, DateTimeOffset now)
    {
        StartsAt = startsAt.ToUniversalTime();
        UpdatedAt = now;
    }

    public void SetDuration(int? minutes, DateTimeOffset now)
    {
        DurationMinutes = ValidateDuration(minutes);
        UpdatedAt = now;
    }

    public void SetLocation(string? location, DateTimeOffset now)
    {
        Location = NormalizeOptional(location, LocationMaxLength, "El lugar");
        UpdatedAt = now;
    }

    public void SetNotes(string? notes, DateTimeOffset now)
    {
        Notes = NormalizeOptional(notes, NotesMaxLength, "Las notas");
        UpdatedAt = now;
    }

    public void SetStatus(SessionStatus status, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
    }

    /// <summary>Sets the journal summary; a blank text removes it (and its timestamp).</summary>
    public void SetSummary(string? markdown, DateTimeOffset now)
    {
        if (markdown is { Length: > SummaryMaxLength })
        {
            throw DomainException.RuleViolation($"El resumen no puede superar los {SummaryMaxLength} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(markdown))
        {
            SummaryMarkdown = null;
            SummaryUpdatedAt = null;
        }
        else
        {
            SummaryMarkdown = markdown;
            SummaryUpdatedAt = now;
        }

        UpdatedAt = now;
    }

    /// <summary>Creates or changes the answer of a member.</summary>
    public SessionRsvp Respond(Guid userId, RsvpStatus status, string? comment, DateTimeOffset now)
    {
        if (Status == SessionStatus.Cancelled)
        {
            throw DomainException.Conflict("La sesión está cancelada.");
        }

        var rsvp = FindRsvp(userId);
        if (rsvp is null)
        {
            rsvp = SessionRsvp.Create(Id, userId, status, comment, now);
            _rsvps.Add(rsvp);
        }
        else
        {
            rsvp.Update(status, comment, now);
        }

        return rsvp;
    }

    /// <summary>
    /// Replaces the pending reminders: the previous ones are discarded and, when the session is
    /// scheduled, one is created per offset whose send time is still in the future. Reminders
    /// already sent are kept as history. Returns the discarded ones (to be deleted) and the new ones
    /// (to be inserted) so the caller can track them explicitly.
    /// </summary>
    public (IReadOnlyList<Reminder> Removed, IReadOnlyList<Reminder> Added) SyncReminders(IEnumerable<int> offsetsMinutes, DateTimeOffset now)
    {
        var removed = _reminders.Where(r => r.SentAt is null).ToList();
        foreach (var reminder in removed)
        {
            _reminders.Remove(reminder);
        }

        var added = new List<Reminder>();
        if (Status == SessionStatus.Scheduled)
        {
            foreach (var offset in offsetsMinutes.Distinct().OrderByDescending(o => o))
            {
                var sendAt = StartsAt - TimeSpan.FromMinutes(offset);
                if (sendAt > now)
                {
                    added.Add(Reminder.Create(Id, offset, sendAt));
                }
            }

            _reminders.AddRange(added);
        }

        return (removed, added);
    }

    /// <summary>True while the session has not finished (it ends at start plus its duration, or an assumed length).</summary>
    public bool IsUpcomingOrInProgress(DateTimeOffset now) =>
        StartsAt + TimeSpan.FromMinutes(DurationMinutes ?? AssumedDurationMinutes) >= now;

    private static string NormalizeTitle(string title)
    {
        var trimmed = (title ?? string.Empty).Trim();
        return trimmed.Length is 0 or > TitleMaxLength
            ? throw DomainException.RuleViolation($"El título debe tener entre 1 y {TitleMaxLength} caracteres.")
            : trimmed;
    }

    private static int? ValidateDuration(int? minutes) => minutes is null or (>= 1 and <= MaxDurationMinutes)
        ? minutes
        : throw DomainException.RuleViolation($"La duración debe estar entre 1 y {MaxDurationMinutes} minutos.");

    private static string? NormalizeOptional(string? value, int maxLength, string subject)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength
            ? throw DomainException.RuleViolation($"{subject} no puede superar los {maxLength} caracteres.")
            : trimmed;
    }
}
