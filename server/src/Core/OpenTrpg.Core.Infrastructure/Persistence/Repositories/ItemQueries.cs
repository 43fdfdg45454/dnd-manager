using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

public static class ItemQueries
{
    /// <summary>
    /// Leaves out the items of deleted content packs, kept only because an inventory, shop or stash still uses them:
    /// homebrew items, those of the base content of the game systems (<paramref name="baseSources"/>) and those of the
    /// packs currently registered.
    /// </summary>
    public static IQueryable<ItemTemplate> WhereListed(this IQueryable<ItemTemplate> query, AppDbContext db, IReadOnlyCollection<string> baseSources)
    {
        var bases = baseSources.ToList();
        return query.Where(x =>
            x.CampaignId != null
            || bases.Contains(x.Source)
            || db.ContentPacks.Any(p => p.Id == x.Source));
    }

    /// <summary>
    /// The items a campaign sees: its homebrew, those of the base content of the game systems
    /// (<paramref name="baseSources"/>, plus the registered base packs) and those of the packs the campaign enables.
    /// </summary>
    public static IQueryable<ItemTemplate> WhereInCampaign(this IQueryable<ItemTemplate> query, AppDbContext db, Guid campaignId, IReadOnlyCollection<string> baseSources)
    {
        var bases = baseSources.ToList();
        return query.Where(x =>
            x.CampaignId == campaignId
            || (x.CampaignId == null
                && (bases.Contains(x.Source)
                    || db.ContentPacks.Any(p => p.Id == x.Source && p.IsBase)
                    || db.CampaignContentPacks.Any(p => p.CampaignId == campaignId && p.PackId == x.Source))));
    }

    /// <summary>The items of a <see cref="CatalogScope"/>: catalog items of its sources and, with a campaign, its homebrew.</summary>
    public static IQueryable<ItemTemplate> WhereInScope(this IQueryable<ItemTemplate> query, CatalogScope scope)
    {
        var sources = scope.Sources.ToList();
        var campaignId = scope.CampaignId;
        return query.Where(x => (x.CampaignId == null && sources.Contains(x.Source)) || (campaignId != null && x.CampaignId == campaignId));
    }
}
