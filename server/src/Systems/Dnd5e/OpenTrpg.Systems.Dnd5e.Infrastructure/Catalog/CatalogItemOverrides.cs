using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

/// <summary>
/// The item descriptions overridden by content packs (<c>itemOverrides</c>), applied at write time: the catalog item
/// template itself carries the active description, so every read path sees it. The rows of
/// <see cref="ItemDescriptionOverride"/> remember who overrides what and the description the item had before.
/// </summary>
internal static class CatalogItemOverrides
{
    /// <summary>
    /// Replaces the overrides of the pack <paramref name="source"/> with <paramref name="overrides"/> (restoring the items
    /// its previous overrides no longer cover) and applies the result to the items.
    /// </summary>
    public static async Task ReplaceAsync(
        AppDbContext db,
        string source,
        IReadOnlyList<ItemDescriptionOverride> overrides,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await RemoveAsync(db, source, cancellationToken);
        if (overrides.Count > 0)
        {
            var indexes = overrides.Select(o => o.ItemIndex).ToList();
            // The original survives chains of overrides: it is copied from any row of the item, or captured now.
            var originals = (await db.Set<ItemDescriptionOverride>().AsNoTracking()
                    .Where(x => indexes.Contains(x.ItemIndex))
                    .ToListAsync(cancellationToken))
                .GroupBy(x => x.ItemIndex, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().OriginalDescription, StringComparer.Ordinal);
            var current = await CatalogDescriptionsAsync(db, indexes, cancellationToken);
            foreach (var entry in overrides)
            {
                entry.ImportedAt = now;
                entry.OriginalDescription = originals.TryGetValue(entry.ItemIndex, out var original)
                    ? original
                    : current.GetValueOrDefault(entry.ItemIndex, []);
            }

            db.Set<ItemDescriptionOverride>().AddRange(overrides);
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        await ApplyAsync(db, cancellationToken);
    }

    /// <summary>
    /// Deletes the overrides of the pack <paramref name="source"/>, restoring the original description of the items only
    /// it overrode, and applies the overrides of the remaining packs.
    /// </summary>
    public static async Task DeleteAsync(AppDbContext db, string source, CancellationToken cancellationToken)
    {
        await RemoveAsync(db, source, cancellationToken);
        await ApplyAsync(db, cancellationToken);
    }

    /// <summary>
    /// After the base pack re-imported its items (resetting their descriptions): the originals of the overrides of
    /// <paramref name="baseSource"/> items are the freshly imported descriptions, and the overrides are applied again.
    /// </summary>
    public static async Task RefreshOriginalsAsync(AppDbContext db, string baseSource, CancellationToken cancellationToken)
    {
        var rows = await db.Set<ItemDescriptionOverride>().ToListAsync(cancellationToken);
        if (rows.Count > 0)
        {
            var indexes = rows.Select(r => r.ItemIndex).Distinct(StringComparer.Ordinal).ToList();
            var baseItems = await db.ItemTemplates.AsNoTracking()
                .Where(x => x.CampaignId == null && x.Source == baseSource && x.Index != null && indexes.Contains(x.Index))
                .Select(x => new { Index = x.Index!, x.Description })
                .ToDictionaryAsync(x => x.Index, x => x.Description, StringComparer.Ordinal, cancellationToken);
            foreach (var row in rows)
            {
                if (baseItems.TryGetValue(row.ItemIndex, out var description))
                {
                    row.OriginalDescription = description;
                }
            }

            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        await ApplyAsync(db, cancellationToken);
    }

    /// <summary>
    /// Gives every overridden item the description of its most recently imported override (ties broken by pack id, for
    /// determinism). Items whose overrides are all gone were restored when their last override was removed.
    /// </summary>
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var winners = (await db.Set<ItemDescriptionOverride>().AsNoTracking().ToListAsync(cancellationToken))
            .GroupBy(x => x.ItemIndex, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(x => x.ImportedAt).ThenByDescending(x => x.Source, StringComparer.Ordinal).First())
            .ToList();
        if (winners.Count == 0)
        {
            return;
        }

        var indexes = winners.Select(w => w.ItemIndex).ToList();
        var items = await db.ItemTemplates
            .Where(x => x.CampaignId == null && x.Index != null && indexes.Contains(x.Index))
            .ToListAsync(cancellationToken);
        foreach (var winner in winners)
        {
            foreach (var item in items.Where(i => i.Index == winner.ItemIndex))
            {
                item.OverrideDescription(winner.Description);
            }
        }

        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    /// <summary>Deletes the rows of <paramref name="source"/>, restoring the items no other pack overrides.</summary>
    private static async Task RemoveAsync(AppDbContext db, string source, CancellationToken cancellationToken)
    {
        var own = await db.Set<ItemDescriptionOverride>().AsNoTracking()
            .Where(x => x.Source == source)
            .ToListAsync(cancellationToken);
        if (own.Count == 0)
        {
            return;
        }

        var indexes = own.Select(o => o.ItemIndex).ToList();
        var stillOverridden = (await db.Set<ItemDescriptionOverride>().AsNoTracking()
                .Where(x => x.Source != source && indexes.Contains(x.ItemIndex))
                .Select(x => x.ItemIndex)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        var restored = own.Where(o => !stillOverridden.Contains(o.ItemIndex)).ToDictionary(o => o.ItemIndex, o => o.OriginalDescription, StringComparer.Ordinal);
        if (restored.Count > 0)
        {
            var restoredIndexes = restored.Keys.ToList();
            var items = await db.ItemTemplates
                .Where(x => x.CampaignId == null && x.Index != null && restoredIndexes.Contains(x.Index))
                .ToListAsync(cancellationToken);
            foreach (var item in items)
            {
                item.OverrideDescription(restored[item.Index!]);
            }

            db.ChangeTracker.DetectChanges();
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }

        await db.Set<ItemDescriptionOverride>().Where(x => x.Source == source).ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, IReadOnlyList<string>>> CatalogDescriptionsAsync(
        AppDbContext db, IReadOnlyCollection<string> indexes, CancellationToken cancellationToken)
    {
        var rows = await db.ItemTemplates.AsNoTracking()
            .Where(x => x.CampaignId == null && x.Index != null && indexes.Contains(x.Index))
            .Select(x => new { Index = x.Index!, x.Description })
            .ToListAsync(cancellationToken);
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            result.TryAdd(row.Index, row.Description);
        }

        return result;
    }
}
