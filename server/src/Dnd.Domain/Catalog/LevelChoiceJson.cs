using System.Globalization;
using System.Text.Json;
using Dnd.Domain.Characters;
using Dnd.Domain.Items;

namespace Dnd.Domain.Catalog;

/// <summary>Structured prerequisites of an option. Every condition given must hold.</summary>
/// <param name="MinLevel">Minimum level in the class of the choice.</param>
/// <param name="PactBoon">Index of the pact boon option the character must have chosen.</param>
/// <param name="Cantrip">Index of a cantrip the character must know.</param>
/// <param name="Abilities">Minimum ability scores ("str" → 13).</param>
public sealed record OptionPrerequisites(int? MinLevel, string? PactBoon, string? Cantrip, IReadOnlyDictionary<string, int> Abilities)
{
    public static OptionPrerequisites None { get; } = new(null, null, null, new Dictionary<string, int>());
}

/// <summary>
/// A numeric effect of a chosen option on the sheet (same kinds as item modifiers), active only while
/// <see cref="Condition"/> holds (one of <see cref="ModifierConditions"/>; null = always).
/// </summary>
public sealed record ChoiceModifier(ItemModifierKind Kind, string? Target, int Value, string? Condition);

/// <summary>Ability increase of a feat: <see cref="Amount"/> to one ability of <see cref="From"/> (empty = any ability).</summary>
public sealed record AbilityIncrease(int Amount, IReadOnlyList<string> From);

/// <summary>A spell granted by an option, from <see cref="MinLevel"/> in the class of the choice (null = at once).</summary>
public sealed record GrantedSpell(string Index, int? MinLevel);

/// <summary>Proficiencies and spells an option (or a subclass level) grants. Spells are always prepared.</summary>
public sealed record OptionGrants(
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Cantrips,
    IReadOnlyList<GrantedSpell> Spells,
    IReadOnlyList<string> Armor,
    IReadOnlyList<string> Weapons,
    IReadOnlyList<string> Tools,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> SavingThrows)
{
    public static OptionGrants None { get; } = new([], [], [], [], [], [], [], []);

    public bool IsEmpty =>
        Skills.Count + Cantrips.Count + Spells.Count + Armor.Count + Weapons.Count + Tools.Count + Languages.Count + SavingThrows.Count == 0;
}

/// <summary>
/// Limited-use resource of an option. <see cref="Max"/> is an integer ("2") or a formula:
/// <c>proficiencyBonus</c>, <c>classLevel</c>, <c>halfClassLevel</c> or <c>mod:&lt;ability&gt;</c> (minimum 1).
/// </summary>
public sealed record OptionResource(string Key, string Name, string Max, ResourceRecharge Recharge)
{
    /// <summary>Dice rolled after a rest and kept in the resource (<c>"rollOnRest": {"dice":"d20","count":2,"rest":"long"}</c>), or null.</summary>
    public RollOnRest? RollOnRest { get; init; }

    public const string ProficiencyBonusFormula = "proficiencyBonus";
    public const string ClassLevelFormula = "classLevel";
    public const string HalfClassLevelFormula = "halfClassLevel";
    public const string ModifierFormulaPrefix = "mod:";

    /// <summary>True when <paramref name="max"/> is a positive integer or one of the formulas.</summary>
    public static bool IsValidMax(string max) =>
        (int.TryParse(max, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= CharacterResource.MaxUses)
        || max is ProficiencyBonusFormula or ClassLevelFormula or HalfClassLevelFormula
        || (max.StartsWith(ModifierFormulaPrefix, StringComparison.Ordinal) && Abilities.IsValid(max[ModifierFormulaPrefix.Length..]));

    /// <summary>Evaluates <see cref="Max"/> (1-999; unknown formulas count as 1).</summary>
    public int Evaluate(int proficiencyBonus, int classLevel, Func<string, int> abilityModifier)
    {
        ArgumentNullException.ThrowIfNull(abilityModifier);
        var value = Max switch
        {
            ProficiencyBonusFormula => proficiencyBonus,
            ClassLevelFormula => classLevel,
            HalfClassLevelFormula => classLevel / 2,
            _ when Max.StartsWith(ModifierFormulaPrefix, StringComparison.Ordinal) && Abilities.IsValid(Max[ModifierFormulaPrefix.Length..]) =>
                abilityModifier(Max[ModifierFormulaPrefix.Length..]),
            _ => int.TryParse(Max, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : 1,
        };
        return Math.Clamp(value, 1, CharacterResource.MaxUses);
    }
}

/// <summary>Narrows the candidate spells of a spell choice.</summary>
/// <param name="SpellList">Class whose spell list is used ("wizard"), "any" for every list, or null for the class of the choice.</param>
/// <param name="SpellLevels">Exact spell levels allowed (overrides <paramref name="MaxSpellLevelBySlots"/>); empty when not given.</param>
/// <param name="MaxSpellLevelBySlots">Spell levels 1 up to the highest slot level of the class at the new level.</param>
/// <param name="Source">"list" (default), "spellbook" or "known": where the candidates come from.</param>
/// <param name="CantripsOnly">Only cantrips.</param>
public sealed record ChoiceFilter(string? SpellList, IReadOnlyList<int> SpellLevels, bool MaxSpellLevelBySlots, string? Source, bool CantripsOnly)
{
    public const string AnyList = "any";
    public const string SpellbookSource = "spellbook";
    public const string KnownSource = "known";
    public const string ListSource = "list";

    public static ChoiceFilter None { get; } = new(null, [], false, null, false);
}

/// <summary>Values of <see cref="ChoiceModifier.Condition"/>.</summary>
public static class ModifierConditions
{
    /// <summary>Wearing armor (Defense).</summary>
    public const string WearingArmor = "wearingArmor";

    /// <summary>Attacking with a ranged weapon (Archery).</summary>
    public const string RangedWeapon = "rangedWeapon";

    /// <summary>Wielding a melee weapon in one hand and no other weapon (Dueling).</summary>
    public const string OneHandedMeleeNoOtherWeapon = "oneHandedMeleeNoOtherWeapon";

    /// <summary>Attacking with a melee weapon held in two hands.</summary>
    public const string TwoHandedMelee = "twoHandedMelee";

    /// <summary>The off-hand attack of two-weapon fighting (not calculated: shown as text).</summary>
    public const string TwoWeaponFighting = "twoWeaponFighting";

    public static IReadOnlyList<string> All { get; } =
        [WearingArmor, RangedWeapon, OneHandedMeleeNoOtherWeapon, TwoHandedMelee, TwoWeaponFighting];

    public static bool IsValid(string? condition) => condition is null || All.Contains(condition, StringComparer.Ordinal);

    /// <summary>Spanish description for the UI ("con armadura").</summary>
    public static string Describe(string condition) => condition switch
    {
        WearingArmor => "con armadura",
        RangedWeapon => "con armas a distancia",
        OneHandedMeleeNoOtherWeapon => "con un arma cuerpo a cuerpo a una mano y ninguna otra arma",
        TwoHandedMelee => "con un arma cuerpo a cuerpo a dos manos",
        TwoWeaponFighting => "con la segunda arma al combatir con dos armas",
        _ => condition,
    };
}

/// <summary>
/// Tolerant parsing of the JSON columns of <see cref="OptionDefinition"/> and <see cref="LevelChoiceRule"/>
/// (camelCase, case-insensitive): malformed or missing values give empty results instead of failing, so a
/// bad catalog row never breaks a character sheet.
/// </summary>
public static class LevelChoiceJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<string>? ParseStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return Parse<IReadOnlyList<string>>(json, root => root.ValueKind == JsonValueKind.Array ? Strings(root) : null);
    }

    public static OptionPrerequisites ParsePrerequisites(string? json) =>
        Parse(json, root =>
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return OptionPrerequisites.None;
            }

            var abilities = new Dictionary<string, int>(StringComparer.Ordinal);
            if (Property(root, "abilities") is { ValueKind: JsonValueKind.Object } scores)
            {
                foreach (var score in scores.EnumerateObject())
                {
                    if (score.Value.ValueKind == JsonValueKind.Number && score.Value.TryGetInt32(out var minimum))
                    {
                        abilities[score.Name.ToLowerInvariant()] = minimum;
                    }
                }
            }

            return new OptionPrerequisites(Int(Property(root, "minLevel")), Text(Property(root, "pactBoon")), Text(Property(root, "cantrip")), abilities);
        }) ?? OptionPrerequisites.None;

    public static IReadOnlyList<ChoiceModifier> ParseModifiers(string? json) =>
        Parse<IReadOnlyList<ChoiceModifier>>(json, root =>
        {
            if (root.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<ChoiceModifier>();
            foreach (var entry in root.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object))
            {
                if (Text(Property(entry, "kind")) is { } kindName
                    && Enum.TryParse<ItemModifierKind>(kindName, ignoreCase: true, out var kind)
                    && Enum.IsDefined(kind)
                    && Int(Property(entry, "value")) is { } value)
                {
                    result.Add(new ChoiceModifier(kind, Text(Property(entry, "target"))?.ToLowerInvariant(), value, Text(Property(entry, "condition"))));
                }
            }

            return result;
        }) ?? [];

    public static AbilityIncrease? ParseAbilityIncrease(string? json) =>
        Parse<AbilityIncrease>(json, root =>
        {
            if (root.ValueKind != JsonValueKind.Object || Int(Property(root, "amount")) is not { } amount || amount < 1)
            {
                return null;
            }

            var from = Property(root, "from") is { ValueKind: JsonValueKind.Array } list
                ? Strings(list).Select(a => a.ToLowerInvariant()).Where(Abilities.IsValid).Distinct(StringComparer.Ordinal).ToList()
                : [];
            return new AbilityIncrease(amount, from);
        });

    public static OptionGrants ParseGrants(string? json) =>
        Parse(json, root =>
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return OptionGrants.None;
            }

            IReadOnlyList<string> List(string name) =>
                Property(root, name) is { ValueKind: JsonValueKind.Array } list ? Strings(list) : [];

            var spells = new List<GrantedSpell>();
            if (Property(root, "spells") is { ValueKind: JsonValueKind.Array } spellList)
            {
                foreach (var entry in spellList.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.String && entry.GetString() is { Length: > 0 } plain)
                    {
                        spells.Add(new GrantedSpell(plain, null));
                    }
                    else if (entry.ValueKind == JsonValueKind.Object && Text(Property(entry, "index")) is { } index)
                    {
                        spells.Add(new GrantedSpell(index, Int(Property(entry, "minLevel"))));
                    }
                }
            }

            return new OptionGrants(
                List("skills"),
                List("cantrips"),
                spells,
                List("armor"),
                List("weapons"),
                List("tools"),
                List("languages"),
                List("savingThrows"));
        }) ?? OptionGrants.None;

    public static OptionResource? ParseResource(string? json) =>
        Parse<OptionResource>(json, root =>
        {
            if (root.ValueKind != JsonValueKind.Object || Text(Property(root, "key")) is not { } key || Text(Property(root, "name")) is not { } name)
            {
                return null;
            }

            var max = Property(root, "max") switch
            {
                { ValueKind: JsonValueKind.Number } number when number.TryGetInt32(out var value) => value.ToString(CultureInfo.InvariantCulture),
                { ValueKind: JsonValueKind.String } text => text.GetString() ?? "1",
                _ => "1",
            };
            var recharge = Text(Property(root, "recharge")) is { } rechargeName
                && Enum.TryParse<ResourceRecharge>(rechargeName, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                    ? parsed
                    : ResourceRecharge.LongRest;
            RollOnRest? roll = null;
            if (Property(root, "rollOnRest") is { ValueKind: JsonValueKind.Object } rollJson
                && RollOnRest.ParseDie(Text(Property(rollJson, "dice"))) is { } die
                && Int(Property(rollJson, "count")) is { } count and >= 1 and <= RollOnRest.MaxCount
                && RollOnRest.ParseRest(Text(Property(rollJson, "rest"))) is { } rest)
            {
                roll = new RollOnRest(die, count, rest);
            }

            return new OptionResource(key, name, max, recharge) { RollOnRest = roll };
        });

    public static ChoiceFilter ParseFilter(string? json) =>
        Parse(json, root =>
        {
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ChoiceFilter.None;
            }

            var levels = Property(root, "spellLevels") is { ValueKind: JsonValueKind.Array } list
                ? list.EnumerateArray().Select(e => Int(e)).OfType<int>().Where(l => l is >= 0 and <= 9).Distinct().Order().ToList()
                : [];
            return new ChoiceFilter(
                Text(Property(root, "spellList")),
                levels,
                Bool(Property(root, "maxSpellLevelBySlots")),
                Text(Property(root, "source")),
                Bool(Property(root, "cantripsOnly")));
        }) ?? ChoiceFilter.None;

    /// <summary>Serializes a value with the camelCase options used by the catalog JSON columns.</summary>
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    private static T? Parse<T>(string? json, Func<JsonElement, T?> map)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return map(document.RootElement);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>Case-insensitive property lookup; null when missing or JSON null.</summary>
    private static JsonElement? Property(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.Null ? null : property.Value;
            }
        }

        return null;
    }

    private static string? Text(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.String } value && value.GetString()?.Trim() is { Length: > 0 } text ? text : null;

    private static int? Int(JsonElement? element) =>
        element is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number) ? number : null;

    private static bool Bool(JsonElement? element) => element is { ValueKind: JsonValueKind.True };

    private static List<string> Strings(JsonElement array) =>
        array.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()?.Trim())
            .OfType<string>()
            .Where(s => s.Length > 0)
            .ToList();
}
