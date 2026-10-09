using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Catalog;
using Dnd.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class CatalogRepository(AppDbContext db) : ICatalogRepository
{
    public async Task<IReadOnlyList<ClassDefinition>> ListClassesAsync(CancellationToken cancellationToken = default) =>
        await db.CatalogClasses.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public Task<ClassDefinition?> GetClassAsync(string index, CancellationToken cancellationToken = default) =>
        db.CatalogClasses.AsNoTracking().FirstOrDefaultAsync(x => x.Index == index, cancellationToken);

    public async Task<IReadOnlyList<ClassLevel>> ListClassLevelsAsync(string classIndex, CancellationToken cancellationToken = default) =>
        await db.CatalogClassLevels.AsNoTracking().Where(x => x.ClassIndex == classIndex).OrderBy(x => x.Level).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubclassDefinition>> ListSubclassesAsync(string classIndex, CancellationToken cancellationToken = default) =>
        await db.CatalogSubclasses.AsNoTracking().Where(x => x.ClassIndex == classIndex).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubclassLevel>> ListSubclassLevelsAsync(IReadOnlyCollection<string> subclassIndexes, CancellationToken cancellationToken = default) =>
        subclassIndexes.Count == 0
            ? []
            : await db.CatalogSubclassLevels.AsNoTracking()
                .Where(x => subclassIndexes.Contains(x.SubclassIndex))
                .OrderBy(x => x.SubclassIndex)
                .ThenBy(x => x.Level)
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeatureDefinition>> ListFeaturesByClassAsync(string classIndex, CancellationToken cancellationToken = default) =>
        await db.CatalogFeatures.AsNoTracking().Where(x => x.ClassIndex == classIndex).ToListAsync(cancellationToken);

    public Task<FeatureDefinition?> GetFeatureAsync(string index, CancellationToken cancellationToken = default) =>
        db.CatalogFeatures.AsNoTracking().FirstOrDefaultAsync(x => x.Index == index, cancellationToken);

    public async Task<IReadOnlyList<RaceDefinition>> ListRacesAsync(CancellationToken cancellationToken = default) =>
        await db.CatalogRaces.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public Task<RaceDefinition?> GetRaceAsync(string index, CancellationToken cancellationToken = default) =>
        db.CatalogRaces.AsNoTracking().FirstOrDefaultAsync(x => x.Index == index, cancellationToken);

    public async Task<IReadOnlyList<SubraceDefinition>> ListSubracesAsync(string raceIndex, CancellationToken cancellationToken = default) =>
        await db.CatalogSubraces.AsNoTracking().Where(x => x.RaceIndex == raceIndex).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubraceDefinition>> ListAllSubracesAsync(CancellationToken cancellationToken = default) =>
        await db.CatalogSubraces.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RaceExtensionDefinition>> ListRaceExtensionsAsync(IReadOnlyCollection<string> raceIndexes, CancellationToken cancellationToken = default) =>
        raceIndexes.Count == 0
            ? []
            : await db.CatalogRaceExtensions.AsNoTracking().Where(x => raceIndexes.Contains(x.RaceIndex)).OrderBy(x => x.Source).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TraitDefinition>> ListTraitsAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0
            ? []
            : await db.CatalogTraits.AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<SpellDefinition> Items, int Total)> SearchSpellsAsync(SpellFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = db.CatalogSpells.AsNoTracking();
        if (filter.Search is { } search)
        {
            query = query.Where(x => x.Name.ToLower().Contains(search));
        }

        if (filter.Level is { } level)
        {
            query = query.Where(x => x.Level == level);
        }

        if (filter.School is { } school)
        {
            query = query.Where(x => x.School.ToLower() == school);
        }

        if (filter.Ritual is { } ritual)
        {
            query = query.Where(x => x.Ritual == ritual);
        }

        if (filter.Concentration is { } concentration)
        {
            query = query.Where(x => x.Concentration == concentration);
        }

        var ordered = query.OrderBy(x => x.Level).ThenBy(x => x.Name).ThenBy(x => x.Index);

        if (filter.ClassIndex is not { } classIndex)
        {
            var total = await query.CountAsync(cancellationToken);
            var items = await ordered.Skip(skip).Take(take).ToListAsync(cancellationToken);
            return (items, total);
        }

        // Class indexes live in a JSON text column, which cannot be queried portably (PostgreSQL and
        // SQLite). The whole SRD has ~320 spells, so the class filter and the paging run in memory.
        var matching = (await ordered.ToListAsync(cancellationToken))
            .Where(x => x.ClassIndexes.Contains(classIndex, StringComparer.Ordinal))
            .ToList();
        return (matching.Skip(skip).Take(take).ToList(), matching.Count);
    }

    public Task<SpellDefinition?> GetSpellAsync(string index, CancellationToken cancellationToken = default) =>
        db.CatalogSpells.AsNoTracking().FirstOrDefaultAsync(x => x.Index == index, cancellationToken);

    public async Task<(IReadOnlyList<ItemTemplate> Items, int Total)> SearchSrdItemsAsync(ItemFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = db.ItemTemplates.AsNoTracking().Where(x => x.CampaignId == null).WhereListed(db);
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
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Index).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<ConditionDefinition>> ListConditionsAsync(CancellationToken cancellationToken = default) =>
        await db.CatalogConditions.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SkillDefinition>> ListSkillsAsync(CancellationToken cancellationToken = default) =>
        await db.CatalogSkills.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BackgroundDefinition>> ListBackgroundsAsync(CancellationToken cancellationToken = default) =>
        await db.CatalogBackgrounds.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClassDefinition>> ListClassesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.CatalogClasses.AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClassLevel>> ListClassLevelsByClassAsync(IReadOnlyCollection<string> classIndexes, CancellationToken cancellationToken = default) =>
        classIndexes.Count == 0
            ? []
            : await db.CatalogClassLevels.AsNoTracking()
                .Where(x => classIndexes.Contains(x.ClassIndex))
                .OrderBy(x => x.ClassIndex)
                .ThenBy(x => x.Level)
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubclassDefinition>> ListSubclassesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.CatalogSubclasses.AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubclassDefinition>> ListSubclassesWithExpandedSpellsAsync(CancellationToken cancellationToken = default) =>
        await db.CatalogSubclasses.AsNoTracking()
            .Where(x => x.ExpandedSpellListJson != null)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Index)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RaceDefinition>> ListRacesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.CatalogRaces.AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubraceDefinition>> ListSubracesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.CatalogSubraces.AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BackgroundDefinition>> ListBackgroundsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.CatalogBackgrounds.AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SpellDefinition>> ListSpellsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.CatalogSpells.AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeatureDefinition>> ListFeaturesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.CatalogFeatures.AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeatureDefinition>> ListSubclassFeatureResourcesAsync(IReadOnlyCollection<string> subclassIndexes, CancellationToken cancellationToken = default) =>
        subclassIndexes.Count == 0
            ? []
            : await db.CatalogFeatures.AsNoTracking()
                .Where(x => x.SubclassIndex != null && subclassIndexes.Contains(x.SubclassIndex) && (x.ResourceJson != null || x.CompanionJson != null))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SpellDefinition>> ListAllSpellsAsync(CancellationToken cancellationToken = default) =>
        await db.CatalogSpells.AsNoTracking().OrderBy(x => x.Level).ThenBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LevelChoiceRule>> ListLevelChoiceRulesAsync(string classIndex, CancellationToken cancellationToken = default) =>
        await db.CatalogLevelChoiceRules.AsNoTracking()
            .Where(x => x.ClassIndex == classIndex)
            .OrderBy(x => x.Level)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OptionSetDefinition>> ListOptionSetsAsync(IReadOnlyCollection<string> setIds, CancellationToken cancellationToken = default) =>
        setIds.Count == 0 ? [] : await db.CatalogOptionSets.AsNoTracking().Where(x => setIds.Contains(x.SetId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OptionDefinition>> ListOptionsBySetAsync(IReadOnlyCollection<string> setIds, CancellationToken cancellationToken = default) =>
        setIds.Count == 0
            ? []
            : await db.CatalogOptions.AsNoTracking().Where(x => setIds.Contains(x.SetId)).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OptionDefinition>> ListOptionsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.CatalogOptions.AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ItemTemplate>> ListCatalogItemsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0
            ? []
            : await db.ItemTemplates.AsNoTracking()
                .Where(x => x.CampaignId == null && x.Index != null && indexes.Contains(x.Index))
                .WhereListed(db)
                .ToListAsync(cancellationToken);

    public Task<EquipmentCategory?> GetEquipmentCategoryAsync(string index, CancellationToken cancellationToken = default) =>
        db.CatalogEquipmentCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Index == index, cancellationToken);

    public async Task<IReadOnlyList<EquipmentCategory>> ListEquipmentCategoriesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.CatalogEquipmentCategories.AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TrinketEntry>> ListTrinketsAsync(CancellationToken cancellationToken = default)
    {
        var entries = await db.CatalogTrinkets.AsNoTracking().ToListAsync(cancellationToken);
        if (entries.Count == 0)
        {
            return [];
        }

        var importedAt = await PackImportTimesAsync(cancellationToken);

        // Same roll in several packs: the most recently imported pack wins.
        return entries
            .Where(e => importedAt.ContainsKey(e.Source))
            .GroupBy(e => e.Roll)
            .Select(g => g.OrderByDescending(e => importedAt[e.Source]).ThenBy(e => e.Source, StringComparer.Ordinal).First())
            .OrderBy(e => e.Roll)
            .ToList();
    }

    public async Task<IReadOnlyList<RollTable>> ListRollTablesAsync(CancellationToken cancellationToken = default)
    {
        var tables = await db.CatalogRollTables.AsNoTracking().ToListAsync(cancellationToken);
        if (tables.Count == 0)
        {
            return [];
        }

        var importedAt = await PackImportTimesAsync(cancellationToken);

        // Same key in several packs: the most recently imported pack wins.
        return tables
            .Where(t => importedAt.ContainsKey(t.Source))
            .GroupBy(t => t.Key, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(t => importedAt[t.Source]).ThenBy(t => t.Source, StringComparer.Ordinal).First())
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.Key, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Import time of every content pack, by pack id.</summary>
    private async Task<Dictionary<string, DateTimeOffset>> PackImportTimesAsync(CancellationToken cancellationToken)
    {
        var imports = await db.CatalogImports.AsNoTracking()
            .Where(x => x.Ruleset.StartsWith(CatalogSources.PackRulesetPrefix))
            .Select(x => new { x.Ruleset, x.ImportedAt })
            .ToListAsync(cancellationToken);
        return imports.ToDictionary(
            x => x.Ruleset[CatalogSources.PackRulesetPrefix.Length..],
            x => x.ImportedAt,
            StringComparer.Ordinal);
    }
}
