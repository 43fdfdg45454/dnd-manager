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
/// dataset version. When an older version was imported, the SRD definitions are replaced and SRD
/// item templates are updated in place by index, so their ids (referenced by inventories) survive.
/// Definitions of content packs (<c>Source</c> other than "srd") are left intact: classes, which
/// pack subclasses and features reference, are updated in place instead of deleted. The level choice
/// catalog (option sets, options and rules, <see cref="SrdLevelChoices"/>) is replaced the same way: only
/// the SRD rows, so options that packs add to SRD sets survive.
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
        var levelChoices = SrdLevelChoices.Load();
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
                ["classes"] = await UpsertClassesAsync(catalog.Classes, cancellationToken),
                ["classLevels"] = await InsertAsync(catalog.ClassLevels, cancellationToken),
                ["subclasses"] = await InsertAsync(catalog.Subclasses, cancellationToken),
                ["subclassLevels"] = await InsertAsync(catalog.SubclassLevels, cancellationToken),
                ["features"] = await InsertAsync(catalog.Features, cancellationToken),
                ["races"] = await UpsertRacesAsync(catalog.Races, cancellationToken),
                ["subraces"] = await InsertAsync(catalog.Subraces, cancellationToken),
                ["traits"] = await InsertAsync(catalog.Traits, cancellationToken),
                ["spells"] = await InsertAsync(catalog.Spells, cancellationToken),
                ["items"] = await CatalogItems.UpsertAsync(db, CatalogSources.Srd, catalog.Items, now, cancellationToken),
                ["conditions"] = await InsertAsync(catalog.Conditions, cancellationToken),
                ["skills"] = await InsertAsync(catalog.Skills, cancellationToken),
                ["backgrounds"] = await InsertAsync(catalog.Backgrounds, cancellationToken),
                ["equipmentCategories"] = await InsertAsync(catalog.EquipmentCategories, cancellationToken),
                ["optionSets"] = await InsertAsync(levelChoices.Sets, cancellationToken),
                ["options"] = await InsertAsync(levelChoices.Options, cancellationToken),
                ["levelChoiceRules"] = await InsertAsync(levelChoices.Rules, cancellationToken),
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

    /// <summary>
    /// Deletes the SRD definitions (dependents first). Classes are upserted and item templates are
    /// upserted instead; the class levels, conditions and skills only exist in the SRD.
    /// </summary>
    private async Task DeleteDefinitionsAsync(CancellationToken cancellationToken)
    {
        const string srd = CatalogSources.Srd;
        await db.CatalogFeatures.Where(x => x.Source == srd).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSubclassLevels.Where(x => x.Source == srd).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSubclasses.Where(x => x.Source == srd).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogClassLevels.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSubraces.Where(x => x.Source == srd).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogTraits.Where(x => x.Source == srd).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSpells.Where(x => x.Source == srd).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogConditions.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogSkills.ExecuteDeleteAsync(cancellationToken);
        await db.CatalogBackgrounds.Where(x => x.Source == srd).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogEquipmentCategories.Where(x => x.Source == srd).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogLevelChoiceRules.Where(x => x.Source == srd).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogOptions.Where(x => x.Source == srd).ExecuteDeleteAsync(cancellationToken);
        await db.CatalogOptionSets.Where(x => x.Source == srd).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Updates the SRD races in place, inserts the new ones and deletes those no longer in the dataset: deleting a race
    /// would cascade over the subraces and extensions content packs add to it (<c>races[].extends</c>).
    /// </summary>
    private async Task<int> UpsertRacesAsync(IReadOnlyList<RaceDefinition> races, CancellationToken cancellationToken)
    {
        const string srd = CatalogSources.Srd;
        var indexes = races.Select(r => r.Index).ToList();
        await db.CatalogRaces.Where(x => x.Source == srd && !indexes.Contains(x.Index)).ExecuteDeleteAsync(cancellationToken);
        var existing = (await db.CatalogRaces.AsNoTracking().Where(x => x.Source == srd).Select(x => x.Index).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        db.CatalogRaces.UpdateRange(races.Where(r => existing.Contains(r.Index)));
        db.CatalogRaces.AddRange(races.Where(r => !existing.Contains(r.Index)));
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return races.Count;
    }

    /// <summary>
    /// Updates the existing classes in place, inserts the new ones and deletes those no longer in the
    /// dataset. Deleting a class would cascade over the subclasses and features of content packs.
    /// </summary>
    private async Task<int> UpsertClassesAsync(IReadOnlyList<ClassDefinition> classes, CancellationToken cancellationToken)
    {
        var indexes = classes.Select(c => c.Index).ToList();
        await db.CatalogClasses.Where(x => !indexes.Contains(x.Index)).ExecuteDeleteAsync(cancellationToken);
        var existing = (await db.CatalogClasses.AsNoTracking().Select(x => x.Index).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

        db.CatalogClasses.UpdateRange(classes.Where(c => existing.Contains(c.Index)));
        db.CatalogClasses.AddRange(classes.Where(c => !existing.Contains(c.Index)));
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return classes.Count;
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
}
