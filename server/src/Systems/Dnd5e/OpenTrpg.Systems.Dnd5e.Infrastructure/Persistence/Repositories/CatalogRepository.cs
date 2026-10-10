using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Persistence;
using OpenTrpg.Core.Infrastructure.Persistence.Repositories;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Items;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Repositories;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Repositories;

/// <summary>
/// <see cref="ICatalogRepository"/> over the catalog tables. The list and search queries follow the catalog scope of the
/// request (<see cref="CatalogScopeContext"/>: the SRD, the packs the campaign enabled and its homebrew); the lookups
/// by index do not, so what a character already has resolves even when its pack is disabled.
/// </summary>
internal sealed class CatalogRepository(AppDbContext db, CatalogScopeContext scope) : ICatalogRepository
{
    /// <summary>Rows of the current scope (every row without one).</summary>
    private IQueryable<T> Scoped<T>(IQueryable<T> query)
        where T : class
    {
        if (scope.Current is not { } current)
        {
            return query;
        }

        var sources = current.Sources.ToList();
        return query.Where(x => sources.Contains(EF.Property<string>(x, "Source")));
    }

    private bool InScope(string source) => scope.Allows(source);

    /// <summary>The spells with the classes whose <see cref="ClassDefinition.SpellList"/> includes them added to their lists.</summary>
    private async Task<List<SpellDefinition>> WithPackSpellListsAsync(List<SpellDefinition> spells, CancellationToken cancellationToken)
    {
        var lists = (await db.Set<ClassDefinition>().AsNoTracking()
                .Where(x => x.SpellListJson != null)
                .Select(x => new { x.Index, x.SpellListJson })
                .ToListAsync(cancellationToken))
            .Select(x => (x.Index, List: ClassSpellList.Parse(x.SpellListJson)))
            .Where(x => x.List is not null)
            .ToList();
        if (lists.Count == 0)
        {
            return spells;
        }

        return spells
            .Select(spell =>
            {
                var extra = lists
                    .Where(l => !spell.ClassIndexes.Contains(l.Index, StringComparer.Ordinal) && l.List!.Contains(spell.Index, spell.ClassIndexes))
                    .Select(l => l.Index)
                    .ToList();
                return extra.Count == 0 ? spell : spell.WithClassIndexes([.. spell.ClassIndexes, .. extra]);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ClassDefinition>> ListClassesAsync(CancellationToken cancellationToken = default) =>
        await Scoped(db.Set<ClassDefinition>().AsNoTracking()).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public Task<ClassDefinition?> GetClassAsync(string index, CancellationToken cancellationToken = default) =>
        db.Set<ClassDefinition>().AsNoTracking().FirstOrDefaultAsync(x => x.Index == index, cancellationToken);

    public async Task<IReadOnlyList<ClassLevel>> ListClassLevelsAsync(string classIndex, CancellationToken cancellationToken = default) =>
        await db.Set<ClassLevel>().AsNoTracking().Where(x => x.ClassIndex == classIndex).OrderBy(x => x.Level).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubclassDefinition>> ListSubclassesAsync(string classIndex, CancellationToken cancellationToken = default) =>
        await Scoped(db.Set<SubclassDefinition>().AsNoTracking()).Where(x => x.ClassIndex == classIndex).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubclassLevel>> ListSubclassLevelsAsync(IReadOnlyCollection<string> subclassIndexes, CancellationToken cancellationToken = default) =>
        subclassIndexes.Count == 0
            ? []
            : await db.Set<SubclassLevel>().AsNoTracking()
                .Where(x => subclassIndexes.Contains(x.SubclassIndex))
                .OrderBy(x => x.SubclassIndex)
                .ThenBy(x => x.Level)
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeatureDefinition>> ListFeaturesByClassAsync(string classIndex, CancellationToken cancellationToken = default) =>
        await Scoped(db.Set<FeatureDefinition>().AsNoTracking()).Where(x => x.ClassIndex == classIndex).ToListAsync(cancellationToken);

    public Task<FeatureDefinition?> GetFeatureAsync(string index, CancellationToken cancellationToken = default) =>
        db.Set<FeatureDefinition>().AsNoTracking().FirstOrDefaultAsync(x => x.Index == index, cancellationToken);

    public async Task<IReadOnlyList<RaceDefinition>> ListRacesAsync(CancellationToken cancellationToken = default) =>
        await Scoped(db.Set<RaceDefinition>().AsNoTracking()).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public Task<RaceDefinition?> GetRaceAsync(string index, CancellationToken cancellationToken = default) =>
        db.Set<RaceDefinition>().AsNoTracking().FirstOrDefaultAsync(x => x.Index == index, cancellationToken);

    public async Task<IReadOnlyList<SubraceDefinition>> ListSubracesAsync(string raceIndex, CancellationToken cancellationToken = default) =>
        await Scoped(db.Set<SubraceDefinition>().AsNoTracking()).Where(x => x.RaceIndex == raceIndex).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubraceDefinition>> ListAllSubracesAsync(CancellationToken cancellationToken = default) =>
        await Scoped(db.Set<SubraceDefinition>().AsNoTracking()).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RaceExtensionDefinition>> ListRaceExtensionsAsync(IReadOnlyCollection<string> raceIndexes, CancellationToken cancellationToken = default) =>
        raceIndexes.Count == 0
            ? []
            : await db.Set<RaceExtensionDefinition>().AsNoTracking().Where(x => raceIndexes.Contains(x.RaceIndex)).OrderBy(x => x.Source).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TraitDefinition>> ListTraitsAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0
            ? []
            : await db.Set<TraitDefinition>().AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<SpellDefinition> Items, int Total)> SearchSpellsAsync(SpellFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Scoped(db.Set<SpellDefinition>().AsNoTracking());
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
        var matching = (await WithPackSpellListsAsync(await ordered.ToListAsync(cancellationToken), cancellationToken))
            .Where(x => x.ClassIndexes.Contains(classIndex, StringComparer.Ordinal))
            .ToList();
        return (matching.Skip(skip).Take(take).ToList(), matching.Count);
    }

    public Task<SpellDefinition?> GetSpellAsync(string index, CancellationToken cancellationToken = default) =>
        db.Set<SpellDefinition>().AsNoTracking().FirstOrDefaultAsync(x => x.Index == index, cancellationToken);

    public async Task<(IReadOnlyList<ItemTemplate> Items, int Total)> SearchSrdItemsAsync(ItemFilter filter, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = scope.Current is { } current
            ? db.ItemTemplates.AsNoTracking().Where(x => x.CampaignId == null).WhereInScope(current)
            : db.ItemTemplates.AsNoTracking().Where(x => x.CampaignId == null).WhereListed(db);
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
        await Scoped(db.Set<ConditionDefinition>().AsNoTracking()).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SkillDefinition>> ListSkillsAsync(CancellationToken cancellationToken = default) =>
        await db.Set<SkillDefinition>().AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BackgroundDefinition>> ListBackgroundsAsync(CancellationToken cancellationToken = default) =>
        await Scoped(db.Set<BackgroundDefinition>().AsNoTracking()).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClassDefinition>> ListClassesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.Set<ClassDefinition>().AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ClassLevel>> ListClassLevelsByClassAsync(IReadOnlyCollection<string> classIndexes, CancellationToken cancellationToken = default) =>
        classIndexes.Count == 0
            ? []
            : await db.Set<ClassLevel>().AsNoTracking()
                .Where(x => classIndexes.Contains(x.ClassIndex))
                .OrderBy(x => x.ClassIndex)
                .ThenBy(x => x.Level)
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubclassDefinition>> ListSubclassesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.Set<SubclassDefinition>().AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubclassDefinition>> ListSubclassesWithExpandedSpellsAsync(CancellationToken cancellationToken = default) =>
        await Scoped(db.Set<SubclassDefinition>().AsNoTracking())
            .Where(x => x.ExpandedSpellListJson != null)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Index)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RaceDefinition>> ListRacesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.Set<RaceDefinition>().AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SubraceDefinition>> ListSubracesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.Set<SubraceDefinition>().AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BackgroundDefinition>> ListBackgroundsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.Set<BackgroundDefinition>().AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SpellDefinition>> ListSpellsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.Set<SpellDefinition>().AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeatureDefinition>> ListFeaturesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.Set<FeatureDefinition>().AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FeatureDefinition>> ListFeatureEffectsAsync(IReadOnlyCollection<string> classIndexes, IReadOnlyCollection<string> subclassIndexes, CancellationToken cancellationToken = default) =>
        classIndexes.Count == 0 && subclassIndexes.Count == 0
            ? []
            : await db.Set<FeatureDefinition>().AsNoTracking()
                .Where(x => (x.SubclassIndex != null ? subclassIndexes.Contains(x.SubclassIndex) : classIndexes.Contains(x.ClassIndex))
                    && (x.ResourceJson != null || x.CompanionJson != null || x.ModifiersJson != null))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SpellDefinition>> ListAllSpellsAsync(CancellationToken cancellationToken = default) =>
        await WithPackSpellListsAsync(
            await Scoped(db.Set<SpellDefinition>().AsNoTracking()).OrderBy(x => x.Level).ThenBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken),
            cancellationToken);

    public async Task<IReadOnlyList<LevelChoiceRule>> ListLevelChoiceRulesAsync(string classIndex, CancellationToken cancellationToken = default) =>
        await Scoped(db.Set<LevelChoiceRule>().AsNoTracking())
            .Where(x => x.ClassIndex == classIndex)
            .OrderBy(x => x.Level)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OptionSetDefinition>> ListOptionSetsAsync(IReadOnlyCollection<string> setIds, CancellationToken cancellationToken = default) =>
        setIds.Count == 0 ? [] : await db.Set<OptionSetDefinition>().AsNoTracking().Where(x => setIds.Contains(x.SetId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OptionDefinition>> ListOptionsBySetAsync(IReadOnlyCollection<string> setIds, CancellationToken cancellationToken = default) =>
        setIds.Count == 0
            ? []
            : await Scoped(db.Set<OptionDefinition>().AsNoTracking()).Where(x => setIds.Contains(x.SetId)).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OptionDefinition>> ListOptionsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.Set<OptionDefinition>().AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ItemTemplate>> ListCatalogItemsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0
            ? []
            : scope.Current is { } current
                ? await db.ItemTemplates.AsNoTracking()
                    .Where(x => x.CampaignId == null && x.Index != null && indexes.Contains(x.Index))
                    .WhereInScope(current)
                    .ToListAsync(cancellationToken)
                : await db.ItemTemplates.AsNoTracking()
                    .Where(x => x.CampaignId == null && x.Index != null && indexes.Contains(x.Index))
                    .WhereListed(db)
                    .ToListAsync(cancellationToken);

    public Task<EquipmentCategory?> GetEquipmentCategoryAsync(string index, CancellationToken cancellationToken = default) =>
        db.Set<EquipmentCategory>().AsNoTracking().FirstOrDefaultAsync(x => x.Index == index, cancellationToken);

    public async Task<IReadOnlyList<EquipmentCategory>> ListEquipmentCategoriesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default) =>
        indexes.Count == 0 ? [] : await db.Set<EquipmentCategory>().AsNoTracking().Where(x => indexes.Contains(x.Index)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TrinketEntry>> ListTrinketsAsync(CancellationToken cancellationToken = default)
    {
        var entries = await db.Set<TrinketEntry>().AsNoTracking().ToListAsync(cancellationToken);
        if (entries.Count == 0)
        {
            return [];
        }

        var importedAt = await PackImportTimesAsync(cancellationToken);

        // Same roll in several packs: the most recently imported pack wins.
        return entries
            .Where(e => importedAt.ContainsKey(e.Source) && InScope(e.Source))
            .GroupBy(e => e.Roll)
            .Select(g => g.OrderByDescending(e => importedAt[e.Source]).ThenBy(e => e.Source, StringComparer.Ordinal).First())
            .OrderBy(e => e.Roll)
            .ToList();
    }

    public async Task<IReadOnlyList<RollTable>> ListRollTablesAsync(CancellationToken cancellationToken = default)
    {
        var tables = await db.Set<RollTable>().AsNoTracking().ToListAsync(cancellationToken);
        if (tables.Count == 0)
        {
            return [];
        }

        var importedAt = await PackImportTimesAsync(cancellationToken);

        // Same key in several packs: the most recently imported pack wins.
        return tables
            .Where(t => importedAt.ContainsKey(t.Source) && InScope(t.Source))
            .GroupBy(t => t.Key, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(t => importedAt[t.Source]).ThenBy(t => t.Source, StringComparer.Ordinal).First())
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.Key, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<RuleDefinition>> ListRulesAsync(CancellationToken cancellationToken = default) =>
        await Scoped(db.Set<RuleDefinition>().AsNoTracking()).OrderBy(x => x.Title).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    public Task<RuleDefinition?> GetRuleAsync(string index, CancellationToken cancellationToken = default) =>
        db.Set<RuleDefinition>().AsNoTracking().FirstOrDefaultAsync(x => x.Index == index, cancellationToken);

    public async Task<IReadOnlyList<ReferenceEntry>> ListReferenceEntriesAsync(string kind, CancellationToken cancellationToken = default) =>
        await Scoped(db.Set<ReferenceEntry>().AsNoTracking()).Where(x => x.Kind == kind).OrderBy(x => x.Name).ThenBy(x => x.Index).ToListAsync(cancellationToken);

    /// <summary>Import time of every content pack, by pack id.</summary>
    private async Task<Dictionary<string, DateTimeOffset>> PackImportTimesAsync(CancellationToken cancellationToken)
    {
        var imports = await db.ContentPacks.AsNoTracking()
            .Where(x => !x.IsBase)
            .Select(x => new { x.Id, x.ImportedAt })
            .ToListAsync(cancellationToken);
        return imports.ToDictionary(x => x.Id, x => x.ImportedAt, StringComparer.Ordinal);
    }
}
