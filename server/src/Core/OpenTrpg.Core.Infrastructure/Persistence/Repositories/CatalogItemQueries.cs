using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

internal static class CatalogItemQueries
{
    /// <summary>
    /// Leaves out the items of deleted content packs, kept only because an inventory, shop or stash still
    /// uses them: SRD and homebrew items, plus those of the packs currently imported.
    /// </summary>
    public static IQueryable<ItemTemplate> WhereListed(this IQueryable<ItemTemplate> query, AppDbContext db) =>
        query.Where(x =>
            x.CampaignId != null
            || x.Source == Dnd5eCatalogSources.Srd
            || db.CatalogImports.Any(i => i.Ruleset == CatalogSources.PackRulesetPrefix + x.Source));
}
