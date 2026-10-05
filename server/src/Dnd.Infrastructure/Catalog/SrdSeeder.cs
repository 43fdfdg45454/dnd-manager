using System.Diagnostics;
using System.Text.Json;
using Dnd.Application.Abstractions;
using Dnd.Domain.Catalog;
using Dnd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dnd.Infrastructure.Catalog;

/// <summary>
/// Imports the embedded SRD 5.1 dataset into the catalog tables in a single transaction.
/// Idempotent: nothing happens when a <see cref="CatalogImport"/> exists for the same ruleset and
/// dataset version. When an older version was imported, the definition tables are replaced and SRD
/// item templates are updated in place by index, so their ids (referenced by inventories) survive.
/// </summary>
internal sealed class SrdSeeder(AppDbContext db, IDateTimeProvider clock, ILogger<SrdSeeder> logger) : ISrdSeeder
{
    public async Task<bool> SeedAsync(CancellationToken cancellationToken = default)
    {
        var alreadyImported = await db.CatalogImports.AnyAsync(
            x => x.Ruleset == CatalogImport.SrdRuleset && x.DatasetVersion == SrdDataset.Version,
            cancellationToken);
        if (alreadyImported)
        {
            return false;
        }

        var stopwatch = Stopwatch.StartNew();
        var catalog = SrdDataset.Load();
        var now = clock.UtcNow;

        // Bulk insert: change detection over thousands of tracked entities is the slow part.
        var autoDetectChanges = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await DeleteDefinitionsAsync(cancellationToken);

            var counts = new Dictionary<string, int>
            {
                ["classes"] = await InsertAsync(catalog.Classes, cancellationToken),
                ["classLevels"] = await InsertAsync(catalog.ClassLevels, cancellationToken),
                ["subclasses"] = await InsertAsync(catalog.Subclasses, cancellationToken),
                ["subclassLevels"] = await InsertAsync(catalog.SubclassLevels, cancellationToken),
                ["features"] = await InsertAsync(catalog.Features, cancellationToken),
                ["races"] = await InsertAsync(catalog.Races, cancellationToken),
                ["subraces"] = await InsertAsync(catalog.Subraces, cancellationToken),
                ["traits"] = await InsertAsync(catalog.Traits, cancellationToken),
                ["spells"] = await InsertAsync(catalog.Spells, cancellationToken),
                ["items"] = await UpsertItemsAsync(catalog.Items, now, cancellationToken),
                ["conditions"] = await InsertAsync(catalog.Conditions, cancellationToken),
                ["skills"] = await InsertAsync(catalog.Skills, cancellationToken),
                ["backgrounds"] = await InsertAsync(catalog.Backgrounds, cancellationToken),
            };

            db.CatalogImports.Add(new CatalogImport
            {
                Ruleset = CatalogImport.SrdRuleset,
                DatasetVersion = SrdDataset.Version,
                ImportedAt = now,
                CreatedAt = now,
                CountsJson = JsonSerializer.Serialize(counts),
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "SRD catalog {Version} imported in {ElapsedMs} ms: {Counts}",
                SrdDataset.Version,
                stopwatch.ElapsedMilliseconds,
                string.Join(", ", counts.Select(c => $"{c.Key}={c.Value}")));
            return true;
        }
        finally
        {
            db.ChangeTracker.Clear();
            db.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
    }

    /// <summary>Empties the definition tables (dependents first). Item templates are upserted instead.</summary>
    private async Task DeleteDefinitionsAsync(CancellationToken cancellationToken)
    {
        await db.CatalogFeatures.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSubclassLevels.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSubclasses.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogClassLevels.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogClasses.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSubraces.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogRaces.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogTraits.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSpells.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogConditions.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSkills.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogBackgrounds.ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>One <c>AddRange</c> and one <c>SaveChanges</c> per table.</summary>
    private async Task<int> InsertAsync<T>(IReadOnlyList<T> rows, CancellationToken cancellationToken)
        where T : class
    {
        db.Set<T>().AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return rows.Count;
    }

    private async Task<int> UpsertItemsAsync(IReadOnlyList<(string Index, ItemTemplateData Data)> items, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await db.ItemTemplates
            .Where(x => x.CampaignId == null && x.Index != null)
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
                added.Add(ItemTemplate.CreateSrd(index, data, now));
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
}
