using Microsoft.EntityFrameworkCore;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

internal sealed class ContentPackRepository(AppDbContext db) : IContentPackRepository
{
    public async Task<IReadOnlyList<ContentPack>> ListBySystemAsync(string systemId, CancellationToken cancellationToken = default) =>
        await db.ContentPacks.AsNoTracking().Where(x => x.SystemId == systemId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListEnabledAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        await db.CampaignContentPacks.AsNoTracking().Where(x => x.CampaignId == campaignId).Select(x => x.PackId).ToListAsync(cancellationToken);

    public async Task ReplaceEnabledAsync(Guid campaignId, IReadOnlyCollection<string> packIds, Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var current = await db.CampaignContentPacks.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken);
        db.CampaignContentPacks.RemoveRange(current.Where(x => !packIds.Contains(x.PackId)));
        db.CampaignContentPacks.AddRange(packIds
            .Where(id => current.All(x => x.PackId != id))
            .Select(id => new CampaignContentPack { CampaignId = campaignId, PackId = id, EnabledAt = now, EnabledByUserId = userId }));
    }
}
