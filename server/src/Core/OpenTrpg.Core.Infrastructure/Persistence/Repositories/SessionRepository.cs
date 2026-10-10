using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

// Sessions are sorted and filtered by date in memory: SQLite cannot compare or order DateTimeOffset
// (same as change requests and transactions). A campaign has at most a few hundred sessions.
internal sealed class SessionRepository(AppDbContext db) : ISessionRepository
{
    public Task<GameSession?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.GameSessions.Include(x => x.Rsvps).Include(x => x.Reminders).AsSplitQuery()
            .Where(x => x.Id == id).OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<GameSession>> ListByCampaignsAsync(IReadOnlyCollection<Guid> campaignIds, CancellationToken cancellationToken = default)
    {
        var rows = await db.GameSessions.AsNoTracking()
            .Include(x => x.Rsvps).Include(x => x.Reminders).AsSplitQuery()
            .Where(x => campaignIds.Contains(x.CampaignId))
            .ToListAsync(cancellationToken);
        return Order(rows);
    }

    public async Task<IReadOnlyList<GameSession>> ListWithSummaryAsync(Guid campaignId, bool includeCancelled, CancellationToken cancellationToken = default)
    {
        var query = db.GameSessions.AsNoTracking().Where(x => x.CampaignId == campaignId && x.SummaryMarkdown != null);
        if (!includeCancelled)
        {
            query = query.Where(x => x.Status != SessionStatus.Cancelled);
        }

        return Order(await query.ToListAsync(cancellationToken));
    }

    public async Task<IReadOnlyList<GameSession>> ListUpcomingScheduledAsync(Guid campaignId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var rows = await db.GameSessions.Include(x => x.Reminders)
            .Where(x => x.CampaignId == campaignId && x.Status == SessionStatus.Scheduled)
            .ToListAsync(cancellationToken);
        return rows.Where(x => x.StartsAt > now).ToList();
    }

    public async Task<int> NextNumberAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        (await db.GameSessions.Where(x => x.CampaignId == campaignId).MaxAsync(x => (int?)x.Number, cancellationToken) ?? 0) + 1;

    public async Task<IReadOnlyList<DueReminder>> ListDueRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var pending = await db.Reminders.Where(x => x.SentAt == null && x.FailedAt == null).ToListAsync(cancellationToken);
        var due = pending.Where(x => x.IsDue(now)).OrderBy(x => x.SendAt).ThenBy(x => x.Id).ToList();
        if (due.Count == 0)
        {
            return [];
        }

        var sessionIds = due.Select(x => x.SessionId).Distinct().ToList();
        var sessions = (await db.GameSessions.AsNoTracking().Include(x => x.Rsvps)
                .Where(x => sessionIds.Contains(x.Id)).AsSplitQuery().ToListAsync(cancellationToken))
            .ToDictionary(x => x.Id);
        var campaignIds = sessions.Values.Select(x => x.CampaignId).Distinct().ToList();
        var campaigns = (await db.Campaigns.AsNoTracking().Where(x => campaignIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Name, x.TimeZoneId }).ToListAsync(cancellationToken))
            .ToDictionary(x => x.Id);

        return due
            .Where(x => sessions.ContainsKey(x.SessionId))
            .Select(x =>
            {
                var session = sessions[x.SessionId];
                var campaign = campaigns[session.CampaignId];
                return new DueReminder(x, session, campaign.Id, campaign.Name, campaign.TimeZoneId);
            })
            .ToList();
    }

    public void Add(GameSession session) => db.GameSessions.Add(session);

    public void Remove(GameSession session) => db.GameSessions.Remove(session);

    public void AddReminders(IEnumerable<Reminder> reminders) => db.Reminders.AddRange(reminders);

    public void RemoveReminders(IEnumerable<Reminder> reminders) => db.Reminders.RemoveRange(reminders);

    private static List<GameSession> Order(List<GameSession> sessions) =>
        sessions.OrderBy(x => x.StartsAt).ThenBy(x => x.Number).ThenBy(x => x.Id).ToList();
}
