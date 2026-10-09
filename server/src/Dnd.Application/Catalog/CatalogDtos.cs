using System.Text.Json;
using Dnd.Application.Items;
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

/// <param name="Source">"srd" or the id of the content pack that added it.</param>
public sealed record SubclassDto(
    string Index,
    string Name,
    string Flavor,
    IReadOnlyList<string> Description,
    IReadOnlyList<SubclassLevelDto> Levels,
    string Source);

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
    StartingEquipmentDto? StartingEquipment,
    SkillChoicesDto SkillChoices,
    IReadOnlyList<ClassLevelDto> Levels,
    IReadOnlyList<SubclassDto> Subclasses);

/// <summary>Level-1 skill proficiency choice: pick <paramref name="Choose"/> of <paramref name="From"/> (skill indexes such as "arcana").</summary>
public sealed record SkillChoicesDto(int Choose, IReadOnlyList<string> From);

public sealed record TraitDto(string Index, string Name, IReadOnlyList<string> Description)
{
    public static TraitDto From(TraitDefinition t) => new(t.Index, t.Name, t.Description);
}

/// <param name="Source">"srd" or the id of the content pack that added it.</param>
public sealed record RaceSummaryDto(
    string Index,
    string Name,
    int Speed,
    string Size,
    IReadOnlyList<AbilityBonusDto> AbilityBonuses,
    IReadOnlyList<string> SubraceIndexes,
    string Source);

public sealed record SubraceDto(
    string Index,
    string Name,
    string Description,
    IReadOnlyList<AbilityBonusDto> AbilityBonuses,
    IReadOnlyList<TraitDto> Traits)
{
    /// <summary>Decisions the subrace asks for at creation (null when none).</summary>
    public RaceChoicesDto? Choices { get; init; }

    /// <summary>Damage types the subrace always resists.</summary>
    public IReadOnlyList<string> Resistances { get; init; } = [];

    /// <summary>Walking speed that replaces the race's, or null.</summary>
    public int? Speed { get; init; }

    /// <summary>Fixed proficiencies and spells (null when none).</summary>
    public OriginGrantsDto? Grants { get; init; }

    /// <summary>"srd" or the id of the content pack that added it (also to a race of the SRD).</summary>
    public string Source { get; init; } = "srd";
}

/// <summary>A spell a race or subrace grants from a total character level, optionally a number of times per long rest.</summary>
public sealed record GrantedSpellDto(string Index, int? MinLevel, int? UsesPerLongRest);

/// <summary>Fixed proficiencies and spells of a race or subrace (<c>grants</c> of a content pack, trait proficiencies of the SRD).</summary>
public sealed record OriginGrantsDto(
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Armor,
    IReadOnlyList<string> Weapons,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> SavingThrows,
    IReadOnlyList<string> Cantrips,
    IReadOnlyList<GrantedSpellDto> Spells,
    string? SpellcastingAbility)
{
    public static OriginGrantsDto? From(OptionGrants grants) =>
        grants.IsEmpty
            ? null
            : new OriginGrantsDto(
                grants.Skills,
                grants.Armor,
                grants.Weapons,
                grants.Tools,
                grants.Languages,
                grants.SavingThrows,
                grants.Cantrips,
                grants.Spells.Select(s => new GrantedSpellDto(s.Index, s.MinLevel, s.UsesPerLongRest)).ToList(),
                grants.SpellcastingAbility);
}

/// <summary>An option of an origin choice (<see cref="Index"/>: ability, skill, language name, tool or spell index).</summary>
public sealed record OriginOptionDto(string Index, string Name);

/// <summary>+<see cref="Amount"/> to <see cref="Choose"/> different abilities of <see cref="From"/>.</summary>
public sealed record AbilityBonusChoiceDto(int Choose, int Amount, IReadOnlyList<OriginOptionDto> From);

/// <summary><see cref="Choose"/> picks of <see cref="From"/>; empty <see cref="From"/> = any.</summary>
public sealed record PickChoiceDto(int Choose, IReadOnlyList<OriginOptionDto> From);

/// <summary>A cantrip of the <see cref="SpellList"/> class ("any" for every list), narrowed to <see cref="From"/> when not empty.</summary>
public sealed record CantripChoiceDto(int Choose, string SpellList, IReadOnlyList<OriginOptionDto> From);

public sealed record FeatChoiceDto(int Choose);

/// <summary>Breath weapon of an option (area, saving throw ability, damage dice by character level).</summary>
public sealed record BreathWeaponDto(string Name, string Area, string SaveAbility, IReadOnlyDictionary<int, string> DamageAtCharacterLevel);

public sealed record TraitOptionDto(string Index, string Name, IReadOnlyList<string> Description, string? DamageType, BreathWeaponDto? BreathWeapon);

public sealed record TraitOptionChoiceDto(string Key, string Name, int Choose, IReadOnlyList<TraitOptionDto> Options);

/// <summary>
/// Decisions a race, subrace or background asks for at creation (phase 19). Every part is optional. Saved with
/// <c>PUT /characters/{id}/origin-choices</c> (see <c>GET</c> of the same route for the plan of a character).
/// </summary>
public sealed record RaceChoicesDto(
    AbilityBonusChoiceDto? AbilityBonuses,
    PickChoiceDto? Skills,
    PickChoiceDto? Languages,
    PickChoiceDto? Tools,
    CantripChoiceDto? Cantrip,
    FeatChoiceDto? Feats,
    IReadOnlyList<TraitOptionChoiceDto> TraitOptions)
{
    public static RaceChoicesDto? From(RaceChoices choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        if (choices.IsEmpty)
        {
            return null;
        }

        static IReadOnlyList<OriginOptionDto> Options(IReadOnlyList<OriginOption> options) => options.Select(o => new OriginOptionDto(o.Index, o.Name)).ToList();
        static PickChoiceDto? Pick(PickChoice? pick) => pick is null ? null : new PickChoiceDto(pick.Choose, Options(pick.From));

        return new RaceChoicesDto(
            choices.AbilityBonuses is { } a ? new AbilityBonusChoiceDto(a.Choose, a.Amount, Options(a.From)) : null,
            Pick(choices.Skills),
            Pick(choices.Languages),
            Pick(choices.Tools),
            choices.Cantrip is { } c ? new CantripChoiceDto(c.Choose, c.SpellList, Options(c.From)) : null,
            choices.Feats is { } f ? new FeatChoiceDto(f.Choose) : null,
            choices.TraitOptions
                .Select(t => new TraitOptionChoiceDto(
                    t.Key,
                    t.Name,
                    t.Choose,
                    t.Options
                        .Select(o => new TraitOptionDto(
                            o.Index,
                            o.Name,
                            o.Description,
                            o.DamageType,
                            o.BreathWeapon is { } b ? new BreathWeaponDto(b.Name, b.Area, b.SaveAbility, b.DamageAtCharacterLevel) : null))
                        .ToList()))
                .ToList());
    }
}

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
    IReadOnlyList<SubraceDto> Subraces,
    string Source)
{
    /// <summary>Decisions the race asks for at creation (null when none); the subraces carry their own.</summary>
    public RaceChoicesDto? Choices { get; init; }

    /// <summary>Damage types the race always resists ("poison" for dwarves).</summary>
    public IReadOnlyList<string> Resistances { get; init; } = [];

    /// <summary>Fixed proficiencies and spells, with those of the packs that extend the race (null when none).</summary>
    public OriginGrantsDto? Grants { get; init; }
}

/// <param name="Source">"srd" or the id of the content pack that added it.</param>
/// <param name="Category">What the spell is mainly for: a <c>SpellCategory</c> name (Healing, Damage, Control, Buff, Defense, Utility, Summoning).</param>
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
    IReadOnlyList<string> ClassIndexes,
    string Source,
    string Category,
    IReadOnlyList<SpellExpansionDto> ExpandedBy)
{
    public static SpellSummaryDto From(SpellDefinition s, IReadOnlyList<SpellExpansionDto>? expandedBy = null) => new(
        s.Index, s.Name, s.Level, s.School, s.CastingTime, s.Range, s.Components, s.Duration, s.Concentration, s.Ritual, s.ClassIndexes, s.Source, s.Category.ToString(), expandedBy ?? []);
}

/// <summary>
/// A subclass whose expanded spell list (content packs) adds a spell to its class's list for the characters with
/// it. The app labels the spell "Lista ampliada: <see cref="SubclassName"/>".
/// </summary>
public sealed record SpellExpansionDto(string SubclassIndex, string SubclassName, string ClassIndex, string Source)
{
    /// <summary>Spell index → the subclasses that expand their class's list with it.</summary>
    public static ILookup<string, SpellExpansionDto> Lookup(IEnumerable<SubclassDefinition> subclasses) =>
        subclasses
            .SelectMany(sc => sc.ExpandedSpellList.Select(e => (e.Index, Dto: new SpellExpansionDto(sc.Index, sc.Name, sc.ClassIndex, sc.Source))))
            .ToLookup(x => x.Index, x => x.Dto, StringComparer.Ordinal);
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
    IReadOnlyDictionary<int, string>? HealAtSlotLevel,
    string? DcAbility,
    string Source,
    string Category,
    IReadOnlyList<SpellExpansionDto> ExpandedBy);

/// <summary>Origin of an item template as shown by the API.</summary>
public static class ItemSources
{
    public const string Srd = CatalogSources.Srd;
    public const string Homebrew = CatalogSources.Homebrew;

    /// <summary>"srd", "homebrew" or the id of the content pack.</summary>
    public static string Of(ItemTemplate item) => item.IsSrd ? item.Source : Homebrew;
}

/// <param name="Source">"srd", "homebrew" (campaign item) or the id of the content pack.</param>
public sealed record ItemSummaryDto(
    Guid Id,
    string? Index,
    string Name,
    string Category,
    string Subcategory,
    string? Rarity,
    bool RequiresAttunement,
    int? CostCp,
    decimal? WeightLb,
    string Source)
{
    public static ItemSummaryDto From(ItemTemplate i) => new(
        i.Id, i.Index, i.Name, i.Category.ToString(), i.Subcategory, i.Rarity?.ToString(), i.RequiresAttunement, i.CostCp, i.WeightLb, ItemSources.Of(i));
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
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Effects,
    string Source,
    IReadOnlyList<ItemModifierDto> Modifiers)
{
    public static ItemDetailDto From(ItemTemplate i) => new(
        i.Id, i.CampaignId, i.Index, i.Name, i.Category.ToString(), i.Subcategory, i.Rarity?.ToString(), i.RequiresAttunement,
        i.CostCp, i.WeightLb, i.DamageDice, i.DamageType, i.VersatileDice, i.Properties, i.RangeNormal, i.RangeLong,
        i.ArmorClassBase, i.AddDexModifier, i.MaxDexBonus, i.StrengthMinimum, i.StealthDisadvantage, i.Description, i.IsSrd, i.CreatedAt,
        i.Effects, ItemSources.Of(i), ItemModifierDto.FromAll(i.Modifiers));
}

public sealed record ConditionDto(string Index, string Name, IReadOnlyList<string> Description)
{
    public static ConditionDto From(ConditionDefinition c) => new(c.Index, c.Name, c.Description);
}

public sealed record SkillDto(string Index, string Name, string AbilityIndex, IReadOnlyList<string> Description)
{
    public static SkillDto From(SkillDefinition s) => new(s.Index, s.Name, s.AbilityIndex, s.Description);
}

/// <param name="StartingEquipment">Structured starting equipment, or null when the background only has <paramref name="StartingEquipmentText"/>.</param>
/// <param name="Source">"srd" or the id of the content pack that added it.</param>
public sealed record BackgroundDto(
    string Index,
    string Name,
    string FeatureName,
    IReadOnlyList<string> FeatureDescription,
    IReadOnlyList<string> SkillProficiencies,
    string StartingEquipmentText,
    StartingEquipmentDto? StartingEquipment,
    string Source)
{
    /// <summary>Decisions the background asks for at creation (languages, tools...), or null.</summary>
    public RaceChoicesDto? Choices { get; init; }

    /// <summary>Personality tables (traits, ideals, bonds, flaws), or null when the background has none.</summary>
    public BackgroundPersonalityDto? Personality { get; init; }

    /// <summary>Optional tables (specialty, scheme...); empty when there are none.</summary>
    public IReadOnlyList<BackgroundTableDto> OptionalTables { get; init; } = [];

    public static BackgroundDto From(BackgroundDefinition b, StartingEquipmentDto? startingEquipment) => new(
        b.Index, b.Name, b.FeatureName, b.FeatureDescription, b.SkillProficiencies, b.StartingEquipmentText, startingEquipment, b.Source)
    {
        Choices = RaceChoicesDto.From(b.Choices),
        Personality = b.Personality is { } p
            ? new BackgroundPersonalityDto(p.Traits, p.Ideals.Select(i => new BackgroundIdealDto(i.Text, i.Alignment)).ToList(), p.Bonds, p.Flaws)
            : null,
        OptionalTables = b.OptionalTables.Select(t => new BackgroundTableDto(t.Key, t.Name, t.Entries)).ToList(),
    };
}

/// <param name="Alignment">Alignment the ideal points to ("Lawful", "Any"...), or null.</param>
public sealed record BackgroundIdealDto(string Text, string? Alignment);

/// <summary>Personality tables of a background; the die of each one is the number of its entries.</summary>
public sealed record BackgroundPersonalityDto(
    IReadOnlyList<string> Traits,
    IReadOnlyList<BackgroundIdealDto> Ideals,
    IReadOnlyList<string> Bonds,
    IReadOnlyList<string> Flaws);

public sealed record BackgroundTableDto(string Key, string Name, IReadOnlyList<string> Entries);
