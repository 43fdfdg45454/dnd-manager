using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

/// <summary>Bulk upsert of catalog item templates by index, shared by the SRD seeder and the content pack importer.</summary>
internal static class CatalogItems
{
    /// <summary>
    /// Updates in place the items of <paramref name="source"/> whose index is in <paramref name="items"/> (their
    /// ids, referenced by inventories, shops and stashes, survive) and inserts the new ones. Items of the source
    /// missing from <paramref name="items"/> are left untouched. Returns the number of items given.
    /// </summary>
    public static async Task<int> UpsertAsync(
        AppDbContext db,
        string source,
        IReadOnlyList<(string Index, ItemTemplateData Data)> items,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await db.ItemTemplates
            .Where(x => x.CampaignId == null && x.Index != null && x.Source == source)
            .ToDictionaryAsync(x => x.Index!, StringComparer.Ordinal, cancellationToken);

        var added = new List<ItemTemplate>();
        foreach (var (index, data) in items)
        {
            if (existing.TryGetValue(index, out var item))
            {
                item.UpdateSrd(data);
            }
            else
            {
                added.Add(ItemTemplate.CreateCatalog(source, index, data, now));
            }
        }

        db.ItemTemplates.AddRange(added);
        if (existing.Count > 0)
        {
            db.ChangeTracker.DetectChanges();
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return items.Count;
    }

    /// <summary>
    /// Deletes the items of <paramref name="source"/> whose index is not in <paramref name="keep"/> and that no
    /// inventory, shop or party stash references. Referenced items are kept so those entries keep working.
    /// Returns the number of items kept although they were not in <paramref name="keep"/>.
    /// </summary>
    public static async Task<int> DeleteUnusedAsync(AppDbContext db, string source, IReadOnlyCollection<string> keep, CancellationToken cancellationToken)
    {
        var candidates = db.ItemTemplates.Where(x => x.CampaignId == null && x.Source == source && !keep.Contains(x.Index!));
        var unused = candidates.Where(x =>
            !db.CharacterItems.Any(i => i.TemplateId == x.Id)
            && !db.ShopItems.Any(i => i.TemplateId == x.Id)
            && !db.PartyStashItems.Any(i => i.TemplateId == x.Id));

        await unused.ExecuteDeleteAsync(cancellationToken);
        return await candidates.CountAsync(cancellationToken);
    }
}
