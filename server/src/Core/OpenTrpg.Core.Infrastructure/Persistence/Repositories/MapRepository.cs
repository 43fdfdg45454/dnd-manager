using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Maps;
using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

internal sealed class MapRepository(AppDbContext db) : IMapRepository
{
    public async Task<IReadOnlyList<Map>> ListByCampaignAsync(Guid campaignId, bool playersOnly, CancellationToken cancellationToken = default)
    {
        var query = db.Maps.AsNoTracking().Where(x => x.CampaignId == campaignId);
        if (playersOnly)
        {
            query = query.Where(x => x.Visibility == ContentVisibility.Players);
        }

        return await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public Task<Map?> GetWithPinsAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Maps.Include(x => x.Pins).AsSplitQuery().Where(x => x.Id == id).OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task<int> MaxSortOrderAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        await db.Maps.Where(x => x.CampaignId == campaignId).Select(x => (int?)x.SortOrder).MaxAsync(cancellationToken) ?? -1;

    public void Add(Map map) => db.Maps.Add(map);

    public void Remove(Map map) => db.Maps.Remove(map);
}
