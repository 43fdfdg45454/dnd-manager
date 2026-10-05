using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Catalog;
using Dnd.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class ItemTemplateRepository(AppDbContext db) : IItemTemplateRepository
{
    public async Task<(IReadOnlyList<ItemTemplate> Items, int Total)> SearchAsync(
        Guid campaignId,
        ItemFilter filter,
        ItemSource source,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = source switch
        {
            ItemSource.Srd => db.ItemTemplates.AsNoTracking().Where(x => x.CampaignId == null),
            ItemSource.Homebrew => db.ItemTemplates.AsNoTracking().Where(x => x.CampaignId == campaignId),
            _ => db.ItemTemplates.AsNoTracking().Where(x => x.CampaignId == null || x.CampaignId == campaignId),
        };

        if (filter.Search is { } search)
        {
            query = query.Where(x => x.Name.ToLower().Contains(search));
        }

        if (filter.Category is { } category)
        {
            query = query.Where(x => x.Category == category);
        }

        if (filter.Rarity is { } rarity)
        {
            query = query.Where(x => x.Rarity == rarity);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Index).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<ItemTemplate?> GetVisibleAsync(Guid campaignId, Guid id, CancellationToken cancellationToken = default) =>
        db.ItemTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && (x.CampaignId == null || x.CampaignId == campaignId), cancellationToken);

    public Task<ItemTemplate?> GetHomebrewAsync(Guid campaignId, Guid id, CancellationToken cancellationToken = default) =>
        db.ItemTemplates.FirstOrDefaultAsync(x => x.Id == id && x.CampaignId == campaignId, cancellationToken);

    public async Task<IReadOnlyList<ItemTemplate>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0 ? [] : await db.ItemTemplates.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public async Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.CharacterItems.AnyAsync(x => x.TemplateId == id, cancellationToken)
        || await db.ShopItems.AnyAsync(x => x.TemplateId == id, cancellationToken);

    public void Add(ItemTemplate template) => db.ItemTemplates.Add(template);

    public void Remove(ItemTemplate template) => db.ItemTemplates.Remove(template);
}
