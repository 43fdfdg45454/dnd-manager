using Dnd.Application.Catalog;
using Dnd.Domain.Catalog;

namespace Dnd.Application.Abstractions.Persistence;

/// <summary>Read-only access to the rules catalog. Every result is untracked.</summary>
public interface ICatalogRepository
{
    Task<IReadOnlyList<ClassDefinition>> ListClassesAsync(CancellationToken cancellationToken = default);

    Task<ClassDefinition?> GetClassAsync(string index, CancellationToken cancellationToken = default);

    /// <summary>Levels of the class ordered by level.</summary>
    Task<IReadOnlyList<ClassLevel>> ListClassLevelsAsync(string classIndex, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubclassDefinition>> ListSubclassesAsync(string classIndex, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubclassLevel>> ListSubclassLevelsAsync(IReadOnlyCollection<string> subclassIndexes, CancellationToken cancellationToken = default);

    /// <summary>Features of the class, including those of its subclasses.</summary>
    Task<IReadOnlyList<FeatureDefinition>> ListFeaturesByClassAsync(string classIndex, CancellationToken cancellationToken = default);

    Task<FeatureDefinition?> GetFeatureAsync(string index, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RaceDefinition>> ListRacesAsync(CancellationToken cancellationToken = default);

    Task<RaceDefinition?> GetRaceAsync(string index, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubraceDefinition>> ListSubracesAsync(string raceIndex, CancellationToken cancellationToken = default);

    /// <summary>Every subrace of the catalog (the SRD ones and those packs add to any race).</summary>
    Task<IReadOnlyList<SubraceDefinition>> ListAllSubracesAsync(CancellationToken cancellationToken = default);

    /// <summary>What content packs add to the given races (<c>races[].extends</c>): traits and grants.</summary>
    Task<IReadOnlyList<RaceExtensionDefinition>> ListRaceExtensionsAsync(IReadOnlyCollection<string> raceIndexes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraitDefinition>> ListTraitsAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default);

    /// <summary>Spells matching the filter, ordered by level and name.</summary>
    Task<(IReadOnlyList<SpellDefinition> Items, int Total)> SearchSpellsAsync(SpellFilter filter, int skip, int take, CancellationToken cancellationToken = default);

    Task<SpellDefinition?> GetSpellAsync(string index, CancellationToken cancellationToken = default);

    /// <summary>SRD items (no campaign) matching the filter, ordered by name.</summary>
    Task<(IReadOnlyList<ItemTemplate> Items, int Total)> SearchSrdItemsAsync(ItemFilter filter, int skip, int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConditionDefinition>> ListConditionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SkillDefinition>> ListSkillsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackgroundDefinition>> ListBackgroundsAsync(CancellationToken cancellationToken = default);

    // Lookups by index used by the character sheet. Unknown indexes are left out of the result.

    Task<IReadOnlyList<ClassDefinition>> ListClassesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default);

    /// <summary>Levels of every given class, ordered by class and level.</summary>
    Task<IReadOnlyList<ClassLevel>> ListClassLevelsByClassAsync(IReadOnlyCollection<string> classIndexes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubclassDefinition>> ListSubclassesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default);

    /// <summary>Subclasses with an expanded spell list (content packs), ordered by name.</summary>
    Task<IReadOnlyList<SubclassDefinition>> ListSubclassesWithExpandedSpellsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RaceDefinition>> ListRacesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubraceDefinition>> ListSubracesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackgroundDefinition>> ListBackgroundsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SpellDefinition>> ListSpellsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeatureDefinition>> ListFeaturesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default);

    /// <summary>Features of the given subclasses that grant a limited-use resource (content packs).</summary>
    Task<IReadOnlyList<FeatureDefinition>> ListSubclassFeatureResourcesAsync(IReadOnlyCollection<string> subclassIndexes, CancellationToken cancellationToken = default);

    // Level choices (phase 16c).

    /// <summary>Every spell of the catalog (a few hundred), for the spell choices of the level-up plan.</summary>
    Task<IReadOnlyList<SpellDefinition>> ListAllSpellsAsync(CancellationToken cancellationToken = default);

    /// <summary>Level choice rules of a class and its subclasses, every level.</summary>
    Task<IReadOnlyList<LevelChoiceRule>> ListLevelChoiceRulesAsync(string classIndex, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OptionSetDefinition>> ListOptionSetsAsync(IReadOnlyCollection<string> setIds, CancellationToken cancellationToken = default);

    /// <summary>Options of the given sets, ordered by name.</summary>
    Task<IReadOnlyList<OptionDefinition>> ListOptionsBySetAsync(IReadOnlyCollection<string> setIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OptionDefinition>> ListOptionsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default);

    // Starting equipment (phase 17).

    /// <summary>Listed catalog items (SRD and imported content packs, no homebrew) with the given indexes.</summary>
    Task<IReadOnlyList<ItemTemplate>> ListCatalogItemsByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default);

    Task<EquipmentCategory?> GetEquipmentCategoryAsync(string index, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EquipmentCategory>> ListEquipmentCategoriesByIndexAsync(IReadOnlyCollection<string> indexes, CancellationToken cancellationToken = default);

    // Trinkets (phase 21).

    /// <summary>Effective trinket table ordered by roll: one entry per roll, from the most recently imported pack.</summary>
    Task<IReadOnlyList<TrinketEntry>> ListTrinketsAsync(CancellationToken cancellationToken = default);

    // Roll tables (phase 22).

    /// <summary>Effective roll tables ordered by name: one per key, from the most recently imported pack.</summary>
    Task<IReadOnlyList<RollTable>> ListRollTablesAsync(CancellationToken cancellationToken = default);
}
