using System.Text.Json;
using System.Text.Json.Serialization;
using Dnd.Domain.Characters;

namespace Dnd.Domain.Catalog;

/// <summary>An option of an origin choice: an index (ability, skill, language name, tool, spell) and its display name.</summary>
public sealed record OriginOption(string Index, string Name);

/// <summary>+<see cref="Amount"/> to <see cref="Choose"/> different abilities of <see cref="From"/>.</summary>
public sealed record AbilityBonusChoice(int Choose, int Amount, IReadOnlyList<OriginOption> From);

/// <summary><see cref="Choose"/> picks of <see cref="From"/>; an empty <see cref="From"/> means any (any skill, any language, any tool).</summary>
public sealed record PickChoice(int Choose, IReadOnlyList<OriginOption> From);

/// <summary>A cantrip of <see cref="SpellList"/> (a class index, or "any"); <see cref="From"/> narrows it when not empty. Always prepared.</summary>
public sealed record CantripChoice(int Choose, string SpellList, IReadOnlyList<OriginOption> From);

/// <summary>Feats of the <c>feats</c> option set (variant human).</summary>
public sealed record FeatChoice(int Choose);

/// <summary>Breath weapon of a draconic ancestry: area ("15 ft. cone"), saving throw ability and damage dice by character level.</summary>
public sealed record BreathWeaponInfo(string Name, string Area, string SaveAbility, IReadOnlyDictionary<int, string> DamageAtCharacterLevel)
{
    /// <summary>Damage dice at a character level (the highest entry not above it).</summary>
    public string? DiceAt(int characterLevel) =>
        DamageAtCharacterLevel.Where(d => d.Key <= Math.Max(1, characterLevel)).OrderByDescending(d => d.Key).Select(d => d.Value).FirstOrDefault();
}

/// <summary>An option of a trait choice (a draconic ancestry): its damage type gives a resistance; its breath weapon goes to the sheet.</summary>
public sealed record TraitOption(string Index, string Name, IReadOnlyList<string> Description)
{
    /// <summary>Damage type index ("fire"): the character resists it.</summary>
    public string? DamageType { get; init; }

    public BreathWeaponInfo? BreathWeapon { get; init; }
}

/// <summary>A trait that asks to choose among options (Draconic Ancestry).</summary>
public sealed record TraitOptionChoice(string Key, string Name, int Choose, IReadOnlyList<TraitOption> Options);

/// <summary>
/// Decisions a race, subrace or background asks for when the character is created (normalized from the SRD
/// <c>ability_bonus_options</c>, <c>language_options</c>, proficiency choices of traits and <c>trait_specific</c>, or
/// declared by content packs). Every part is optional. Saved per character as <see cref="CharacterChoice"/> of level 0
/// with the keys of <see cref="OriginChoiceKeys"/>.
/// </summary>
public sealed record RaceChoices
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static RaceChoices None { get; } = new();

    public AbilityBonusChoice? AbilityBonuses { get; init; }

    public PickChoice? Skills { get; init; }

    public PickChoice? Languages { get; init; }

    public PickChoice? Tools { get; init; }

    public CantripChoice? Cantrip { get; init; }

    public FeatChoice? Feats { get; init; }

    public IReadOnlyList<TraitOptionChoice> TraitOptions { get; init; } = [];

    [JsonIgnore]
    public bool IsEmpty =>
        AbilityBonuses is null && Skills is null && Languages is null && Tools is null && Cantrip is null && Feats is null && TraitOptions.Count == 0;

    /// <summary>Null when there is nothing to choose (stored as a null column).</summary>
    public string? ToJson() => IsEmpty ? null : JsonSerializer.Serialize(this, Options);

    /// <summary>Tolerant: null, empty or malformed JSON gives <see cref="None"/>.</summary>
    public static RaceChoices Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return None;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<RaceChoices>(json, Options) ?? None;
            return parsed with { TraitOptions = parsed.TraitOptions ?? [] };
        }
        catch (JsonException)
        {
            return None;
        }
    }
}

/// <summary>
/// Keys of the origin choices of a character (<see cref="CharacterChoice"/> with level 0 and no class): the race ones
/// start with <c>race.</c> (the subrace ones with <c>race.subrace.</c>), the background ones with <c>background.</c>.
/// </summary>
public static class OriginChoiceKeys
{
    public const string RacePrefix = "race.";
    public const string SubracePrefix = "race.subrace.";
    public const string BackgroundPrefix = "background.";

    public const string AbilityBonuses = "abilityBonuses";
    public const string Skills = "skills";
    public const string Languages = "languages";
    public const string Tools = "tools";
    public const string Cantrip = "cantrip";
    public const string Feat = "feat";
    public const string TraitPrefix = "trait.";

    /// <summary>Kinds of the origin choices (<see cref="ChoiceSelection.Kind"/> and the plan).</summary>
    public const string AbilityBonusKind = "AbilityBonus";
    public const string SkillKind = "Skill";
    public const string LanguageKind = "Language";
    public const string ToolKind = "Tool";
    public const string CantripKind = "Cantrip";
    public const string FeatKind = "Feat";
    public const string TraitOptionKind = "TraitOption";

    public static bool IsOrigin(string key) =>
        key.StartsWith(RacePrefix, StringComparison.Ordinal) || key.StartsWith(BackgroundPrefix, StringComparison.Ordinal);

    public static bool IsBackground(string key) => key.StartsWith(BackgroundPrefix, StringComparison.Ordinal);

    public static bool IsSubrace(string key) => key.StartsWith(SubracePrefix, StringComparison.Ordinal);
}

/// <summary>SRD languages (names as stored in the language proficiencies).</summary>
public static class SrdLanguages
{
    public static IReadOnlyList<string> All { get; } =
    [
        "Common", "Dwarvish", "Elvish", "Giant", "Gnomish", "Goblin", "Halfling", "Orc",
        "Abyssal", "Celestial", "Draconic", "Deep Speech", "Infernal", "Primordial", "Sylvan", "Undercommon",
    ];
}
