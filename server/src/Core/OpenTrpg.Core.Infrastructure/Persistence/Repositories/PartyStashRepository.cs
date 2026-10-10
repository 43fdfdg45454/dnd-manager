using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Items;
using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

internal sealed class PartyStashRepository(AppDbContext db) : IPartyStashRepository
{
    public async Task<IReadOnlyList<PartyStashItem>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        // Sorted in memory: SQLite cannot order by DateTimeOffset.
        var items = await db.PartyStashItems.Where(x => x.CampaignId == campaignId).ToListAsync(cancellationToken);
        return items.OrderBy(x => x.AddedAt).ThenBy(x => x.Id).ToList();
    }

    public Task<PartyStashItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.PartyStashItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(PartyStashItem item) => db.PartyStashItems.Add(item);

    public void Remove(PartyStashItem item) => db.PartyStashItems.Remove(item);
}
