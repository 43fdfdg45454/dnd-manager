using System.Text.Json;
using Dnd.Domain.Catalog;

namespace Dnd.Application.Catalog;

public sealed record AttributionDto(string Ruleset, string License, string Text);

public sealed record AbilityBonusDto(string Ability, int Bonus);

public sealed record ClassSummaryDto(
    string Index,
    string Name,
    int HitDie,
    IReadOnlyList<string> SavingThrows,
    string? SpellcastingAbility,
    bool IsSpellcaster,
    int SpellcastingLevel,
    bool IsPactCaster,
    string SubclassFlavor)
{
    public static ClassSummaryDto From(ClassDefinition c) => new(
        c.Index, c.Name, c.HitDie, c.SavingThrows, c.SpellcastingAbility, c.IsSpellcaster, c.SpellcastingLevel, c.IsPactCaster, c.SubclassFlavor);
}

public sealed record FeatureDto(
    string Index,
    string Name,
    string ClassIndex,
    string? SubclassIndex,
    int Level,
    IReadOnlyList<string> Description)
{
    public static FeatureDto From(FeatureDefinition f) => new(f.Index, f.Name, f.ClassIndex, f.SubclassIndex, f.Level, f.Description);
}

/// <param name="SpellSlots">Slots per spell level 1..9 (always 9 entries).</param>
/// <param name="ClassSpecific">Class-specific counters of the dataset as a JSON object (e.g. <c>{"rage_count":3}</c>).</param>
/// <param name="Features">Features gained at this level, with their description.</param>
public sealed record ClassLevelDto(
    int Level,
    int ProfBonus,
    int AbilityScoreBonuses,
    int? CantripsKnown,
    int? SpellsKnown,
    IReadOnlyList<int> SpellSlots,
    JsonElement ClassSpecific,
    IReadOnlyList<FeatureDto> Features);

public sealed record SubclassLevelDto(int Level, IReadOnlyList<FeatureDto> Features);

public sealed record SubclassDto(
    string Index,
    string Name,
    string Flavor,
    IReadOnlyList<string> Description,
    IReadOnlyList<SubclassLevelDto> Levels);

public sealed record ClassDetailDto(
    string Index,
    string Name,
    int HitDie,
    IReadOnlyList<string> SavingThrows,
    IReadOnlyList<string> ProficiencyNames,
    string? SpellcastingAbility,
    bool IsSpellcaster,
    int SpellcastingLevel,
    bool IsPactCaster,
    string SubclassFlavor,
    string StartingEquipmentText,
    IReadOnlyList<ClassLevelDto> Levels,
    IReadOnlyList<SubclassDto> Subclasses);

public sealed record TraitDto(string Index, string Name, IReadOnlyList<string> Description)
{
    public static TraitDto From(TraitDefinition t) => new(t.Index, t.Name, t.Description);
}

public sealed record RaceSummaryDto(
    string Index,
    string Name,
    int Speed,
    string Size,
    IReadOnlyList<AbilityBonusDto> AbilityBonuses,
    IReadOnlyList<string> SubraceIndexes);

public sealed record SubraceDto(
    string Index,
    string Name,
    string Description,
    IReadOnlyList<AbilityBonusDto> AbilityBonuses,
    IReadOnlyList<TraitDto> Traits);

public sealed record RaceDetailDto(
    string Index,
    string Name,
    int Speed,
    string Size,
    string SizeDescription,
    IReadOnlyList<AbilityBonusDto> AbilityBonuses,
    IReadOnlyList<string> Languages,
    string Age,
    string Alignment,
    IReadOnlyList<TraitDto> Traits,
    IReadOnlyList<SubraceDto> Subraces);

public sealed record SpellSummaryDto(
    string Index,
    string Name,
    int Level,
    string School,
    string CastingTime,
    string Range,
    IReadOnlyList<string> Components,
    string Duration,
    bool Concentration,
    bool Ritual,
    IReadOnlyList<string> ClassIndexes)
{
    public static SpellSummaryDto From(SpellDefinition s) => new(
        s.Index, s.Name, s.Level, s.School, s.CastingTime, s.Range, s.Components, s.Duration, s.Concentration, s.Ritual, s.ClassIndexes);
}

/// <summary>
/// Spell damage. <see cref="Dice"/> and <see cref="Type"/> describe the damage at the lowest level
/// (parts joined with " + " when the spell deals several types); the maps give the full scaling,
/// keyed by spell slot level or by character level (cantrips).
/// </summary>
public sealed record SpellDamageDto(
    string? Dice,
    string? Type,
    IReadOnlyDictionary<int, string>? AtSlotLevel,
    IReadOnlyDictionary<int, string>? AtCharacterLevel);

public sealed record SpellDetailDto(
    string Index,
    string Name,
    int Level,
    string School,
    string CastingTime,
    string Range,
    IReadOnlyList<string> Components,
    string? Material,
    string Duration,
    bool Concentration,
    bool Ritual,
    IReadOnlyList<string> Description,
    IReadOnlyList<string> HigherLevel,
    IReadOnlyList<string> ClassIndexes,
    IReadOnlyList<string> SubclassIndexes,
    string? AttackType,
    SpellDamageDto? Damage,
    string? DcAbility);

public sealed record ItemSummaryDto(
    Guid Id,
    string? Index,
    string Name,
    string Category,
    string Subcategory,
    string? Rarity,
    bool RequiresAttunement,
    int? CostCp,
    decimal? WeightLb)
{
    public static ItemSummaryDto From(ItemTemplate i) => new(
        i.Id, i.Index, i.Name, i.Category.ToString(), i.Subcategory, i.Rarity?.ToString(), i.RequiresAttunement, i.CostCp, i.WeightLb);
}

public sealed record ItemDetailDto(
    Guid Id,
    Guid? CampaignId,
    string? Index,
    string Name,
    string Category,
    string Subcategory,
    string? Rarity,
    bool RequiresAttunement,
    int? CostCp,
    decimal? WeightLb,
    string? DamageDice,
    string? DamageType,
    string? VersatileDice,
    IReadOnlyList<string> Properties,
    int? RangeNormal,
    int? RangeLong,
    int? ArmorClassBase,
    bool? AddDexModifier,
    int? MaxDexBonus,
    int? StrengthMinimum,
    bool StealthDisadvantage,
    IReadOnlyList<string> Description,
    bool IsSrd,
    DateTimeOffset CreatedAt)
{
    public static ItemDetailDto From(ItemTemplate i) => new(
        i.Id, i.CampaignId, i.Index, i.Name, i.Category.ToString(), i.Subcategory, i.Rarity?.ToString(), i.RequiresAttunement,
        i.CostCp, i.WeightLb, i.DamageDice, i.DamageType, i.VersatileDice, i.Properties, i.RangeNormal, i.RangeLong,
        i.ArmorClassBase, i.AddDexModifier, i.MaxDexBonus, i.StrengthMinimum, i.StealthDisadvantage, i.Description, i.IsSrd, i.CreatedAt);
}

public sealed record ConditionDto(string Index, string Name, IReadOnlyList<string> Description)
{
    public static ConditionDto From(ConditionDefinition c) => new(c.Index, c.Name, c.Description);
}

public sealed record SkillDto(string Index, string Name, string AbilityIndex, IReadOnlyList<string> Description)
{
    public static SkillDto From(SkillDefinition s) => new(s.Index, s.Name, s.AbilityIndex, s.Description);
}

public sealed record BackgroundDto(
    string Index,
    string Name,
    string FeatureName,
    IReadOnlyList<string> FeatureDescription,
    IReadOnlyList<string> SkillProficiencies,
    string StartingEquipmentText)
{
    public static BackgroundDto From(BackgroundDefinition b) => new(
        b.Index, b.Name, b.FeatureName, b.FeatureDescription, b.SkillProficiencies, b.StartingEquipmentText);
}
