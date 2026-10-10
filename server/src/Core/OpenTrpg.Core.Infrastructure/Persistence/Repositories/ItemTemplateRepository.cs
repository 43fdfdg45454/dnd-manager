using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Application.Systems;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

internal sealed class ItemTemplateRepository(AppDbContext db, IGameSystemRegistry systems) : IItemTemplateRepository
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
        query = query.WhereInCampaign(db, campaignId, BaseSources());

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

        if (filter.Categories is { Count: > 0 } categories)
        {
            query = query.Where(x => categories.Contains(x.Category));
        }

        if (filter.SubcategoryPrefix is { } prefix)
        {
            query = query.Where(x => x.Subcategory.ToLower().StartsWith(prefix));
        }

        if (filter.Indexes is { Count: > 0 } indexes)
        {
            query = query.Where(x => x.Index != null && indexes.Contains(x.Index));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Index).ThenBy(x => x.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<ItemTemplate?> GetVisibleAsync(Guid campaignId, Guid id, CancellationToken cancellationToken = default) =>
        db.ItemTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && (x.CampaignId == null || x.CampaignId == campaignId), cancellationToken);

    public Task<ItemTemplate?> GetSelectableAsync(Guid campaignId, Guid id, CancellationToken cancellationToken = default) =>
        db.ItemTemplates.AsNoTracking().Where(x => x.Id == id).WhereInCampaign(db, campaignId, BaseSources()).FirstOrDefaultAsync(cancellationToken);

    public Task<ItemTemplate?> GetHomebrewAsync(Guid campaignId, Guid id, CancellationToken cancellationToken = default) =>
        db.ItemTemplates.FirstOrDefaultAsync(x => x.Id == id && x.CampaignId == campaignId, cancellationToken);

    public async Task<IReadOnlyList<ItemTemplate>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0 ? [] : await db.ItemTemplates.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public async Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.CharacterItems.AnyAsync(x => x.TemplateId == id, cancellationToken)
        || await db.ShopItems.AnyAsync(x => x.TemplateId == id, cancellationToken);

    private List<string> BaseSources() => systems.All.SelectMany(s => s.Info.BaseCatalogSources).ToList();

    public void Add(ItemTemplate template) => db.ItemTemplates.Add(template);

    public void Remove(ItemTemplate template) => db.ItemTemplates.Remove(template);
}
