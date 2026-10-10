using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Persistence;
using OpenTrpg.Core.Infrastructure.Persistence.Repositories;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Repositories;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Repositories;

internal static class CatalogItemQueries
{
    /// <summary>
    /// Leaves out the items of deleted content packs, kept only because an inventory, shop or stash still
    /// uses them: SRD and homebrew items, plus those of the packs currently imported.
    /// </summary>
    public static IQueryable<ItemTemplate> WhereListed(this IQueryable<ItemTemplate> query, AppDbContext db) =>
        query.WhereListed(db, [Dnd5eCatalogSources.Srd]);
}
