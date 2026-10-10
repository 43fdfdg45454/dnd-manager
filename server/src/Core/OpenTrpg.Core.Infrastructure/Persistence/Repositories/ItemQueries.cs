using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

public static class ItemQueries
{
    /// <summary>
    /// Leaves out the items of deleted content packs, kept only because an inventory, shop or stash still uses them:
    /// homebrew items, those of the base content of the game systems (<paramref name="baseSources"/>) and those of the
    /// packs currently imported.
    /// </summary>
    public static IQueryable<ItemTemplate> WhereListed(this IQueryable<ItemTemplate> query, AppDbContext db, IReadOnlyCollection<string> baseSources)
    {
        var bases = baseSources.ToList();
        return query.Where(x =>
            x.CampaignId != null
            || bases.Contains(x.Source)
            || db.CatalogImports.Any(i => i.Ruleset == CatalogSources.PackRulesetPrefix + x.Source));
    }
}
