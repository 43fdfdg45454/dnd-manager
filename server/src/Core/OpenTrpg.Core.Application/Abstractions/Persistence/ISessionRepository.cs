using OpenTrpg.Core.Domain.Sessions;

namespace OpenTrpg.Core.Application.Abstractions.Persistence;

/// <summary>A pending reminder that is due, with the session it belongs to (read-only) and its campaign.</summary>
public sealed record DueReminder(Reminder Reminder, GameSession Session, Guid CampaignId, string CampaignName, string TimeZoneId);

public interface ISessionRepository
{
    /// <summary>Tracked session with its RSVPs and reminders.</summary>
    Task<GameSession?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only sessions of the campaign with RSVPs and reminders, ordered by start and number.</summary>
    Task<IReadOnlyList<GameSession>> ListByCampaignsAsync(IReadOnlyCollection<Guid> campaignIds, CancellationToken cancellationToken = default);

    /// <summary>Read-only sessions of the campaign that have a summary, with no RSVPs/reminders, ordered by start and number.</summary>
    Task<IReadOnlyList<GameSession>> ListWithSummaryAsync(Guid campaignId, bool includeCancelled, CancellationToken cancellationToken = default);

    /// <summary>Tracked scheduled sessions of the campaign that start after <paramref name="now"/>, with their reminders.</summary>
    Task<IReadOnlyList<GameSession>> ListUpcomingScheduledAsync(Guid campaignId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Number for the next session of the campaign: highest existing plus one (1 for the first).</summary>
    Task<int> NextNumberAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Tracked reminders due at <paramref name="now"/> (pending, send time reached, retry delay elapsed), oldest first.</summary>
    Task<IReadOnlyList<DueReminder>> ListDueRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    void Add(GameSession session);

    /// <summary>Deletes the session; its RSVPs and reminders are deleted in cascade.</summary>
    void Remove(GameSession session);

    void AddReminders(IEnumerable<Reminder> reminders);

    void RemoveReminders(IEnumerable<Reminder> reminders);
}
