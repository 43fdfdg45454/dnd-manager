using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Items;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class ShopRepository(AppDbContext db) : IShopRepository
{
    public async Task<IReadOnlyList<Shop>> ListByCampaignAsync(Guid campaignId, bool openOnly, CancellationToken cancellationToken = default)
    {
        var query = db.Shops.AsNoTracking().Include(x => x.Items).Where(x => x.CampaignId == campaignId);
        if (openOnly)
        {
            query = query.Where(x => x.IsOpen);
        }

        return await query.OrderBy(x => x.Name).ThenBy(x => x.Id).AsSplitQuery().ToListAsync(cancellationToken);
    }

    public Task<Shop?> GetWithItemsAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Shops.Include(x => x.Items).AsSplitQuery().Where(x => x.Id == id).OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);

    public void Add(Shop shop) => db.Shops.Add(shop);

    public void Remove(Shop shop) => db.Shops.Remove(shop);
}
