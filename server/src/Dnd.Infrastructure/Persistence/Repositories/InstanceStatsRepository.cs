using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Sessions;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class InstanceStatsRepository(AppDbContext db) : IInstanceStatsRepository
{
    public async Task<InstanceCounts> GetCountsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var usersTotal = await db.Users.CountAsync(cancellationToken);
        var usersActive = await db.Users.CountAsync(x => x.IsActive, cancellationToken);
        var campaigns = await db.Campaigns.CountAsync(cancellationToken);
        var characters = await db.Characters.CountAsync(cancellationToken);
        var files = await db.StoredFiles.CountAsync(cancellationToken);
        var filesBytes = await db.StoredFiles.SumAsync(x => x.SizeBytes, cancellationToken);

        // Compared in memory (like the rest of the session queries) so it also works on SQLite.
        var scheduledStarts = await db.GameSessions.AsNoTracking()
            .Where(x => x.Status == SessionStatus.Scheduled)
            .Select(x => x.StartsAt)
            .ToListAsync(cancellationToken);

        return new InstanceCounts(
            usersTotal,
            usersActive,
            campaigns,
            characters,
            scheduledStarts.Count(startsAt => startsAt > now),
            files,
            filesBytes);
    }
}
