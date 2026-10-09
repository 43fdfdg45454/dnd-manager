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

    /// <summary>Race indexes, one of which the character must be ("elf", "half-elf"); empty = any race.</summary>
    public IReadOnlyList<string> Races { get; init; } = [];

    /// <summary>Armor proficiencies the character must have, all of them: <see cref="ProficiencyKeys.Armor"/> values.</summary>
    public IReadOnlyList<string> ArmorProficiencies { get; init; } = [];

    /// <summary>Weapon proficiencies the character must have, all of them: <see cref="ProficiencyKeys.Weapons"/> values or a weapon index.</summary>
    public IReadOnlyList<string> WeaponProficiencies { get; init; } = [];

    /// <summary>The character must be able to cast at least one spell (a spellcasting class, or a spell from any source).</summary>
    public bool Spellcasting { get; init; }

    public bool IsEmpty =>
        MinLevel is null && PactBoon is null && Cantrip is null && Abilities.Count == 0
        && Races.Count == 0 && ArmorProficiencies.Count == 0 && WeaponProficiencies.Count == 0 && !Spellcasting;
}

/// <summary>Catalog keys of the armor and weapon proficiencies, as the SRD classes list them.</summary>
public static class ProficiencyKeys
{
    public const string LightArmor = "light-armor";
    public const string MediumArmor = "medium-armor";
    public const string HeavyArmor = "heavy-armor";
    public const string AllArmor = "all-armor";
    public const string Shields = "shields";
    public const string SimpleWeapons = "simple-weapons";
    public const string MartialWeapons = "martial-weapons";

    /// <summary>Armor keys a prerequisite may name ("heavy" and "heavy-armor" both normalize to <see cref="HeavyArmor"/>).</summary>
    public static IReadOnlyList<string> Armor { get; } = [LightArmor, MediumArmor, HeavyArmor, Shields];

    /// <summary>Weapon group keys ("simple" and "simple-weapons" both normalize to <see cref="SimpleWeapons"/>); any other value is a weapon index.</summary>
    public static IReadOnlyList<string> Weapons { get; } = [SimpleWeapons, MartialWeapons];

    /// <summary>"heavy" → "heavy-armor", "shield" → "shields"; null when the value is not an armor key.</summary>
    public static string? NormalizeArmor(string value)
    {
        var key = value.Trim().ToLowerInvariant();
        return key switch
        {
            "light" or LightArmor => LightArmor,
            "medium" or MediumArmor => MediumArmor,
            "heavy" or HeavyArmor => HeavyArmor,
            "shield" or Shields => Shields,
            _ => null,
        };
    }

    /// <summary>"simple" → "simple-weapons", "martial" → "martial-weapons"; other values (a weapon index) are kept lowercase.</summary>
    public static string NormalizeWeapon(string value)
    {
        var key = value.Trim().ToLowerInvariant();
        return key switch
        {
            "simple" or SimpleWeapons => SimpleWeapons,
            "martial" or MartialWeapons => MartialWeapons,
            _ => key,
        };
    }

    /// <summary>True when the proficiency keys the character has (any type) cover <paramref name="armor"/>: "all-armor" covers every armor.</summary>
    public static bool HasArmor(IEnumerable<string> keys, string armor)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var list = keys.ToList();
        return list.Contains(armor, StringComparer.Ordinal) || (armor != Shields && list.Contains(AllArmor, StringComparer.Ordinal));
    }

    /// <summary>Spanish label: "armadura pesada", "escudos", "armas marciales", or the key itself.</summary>
    public static string Describe(string key) => key switch
    {
        LightArmor => "armadura ligera",
        MediumArmor => "armadura intermedia",
        HeavyArmor => "armadura pesada",
        AllArmor => "todas las armaduras",
        Shields => "escudos",
        SimpleWeapons => "armas sencillas",
        MartialWeapons => "armas marciales",
        _ => key,
    };
}

/// <summary>
/// A numeric effect of a chosen option on the sheet (same kinds as item modifiers), active only while
/// <see cref="Condition"/> holds (one of <see cref="ModifierConditions"/>; null = always).
/// </summary>
public sealed record ChoiceModifier(ItemModifierKind Kind, string? Target, int Value, string? Condition);

/// <summary>Ability increase of a feat: <see cref="Amount"/> to one ability of <see cref="From"/> (empty = any ability).</summary>
public sealed record AbilityIncrease(int Amount, IReadOnlyList<string> From);

/// <summary>
/// A spell granted by an option, from <see cref="MinLevel"/> in the class of the choice (null = at once). Granted by a
/// race or subrace, <see cref="MinLevel"/> is the total character level.
/// </summary>
public sealed record GrantedSpell(string Index, int? MinLevel)
{
    /// <summary>Races and subraces: casts per long rest without a slot (an automatic resource named after the spell), or null.</summary>
    public int? UsesPerLongRest { get; init; }
}

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

    /// <summary>
    /// Races and subraces: ability ("cha") the granted spells and cantrips are cast with (the "Raza" spellcasting of
    /// the sheet), or null.
    /// </summary>
    public string? SpellcastingAbility { get; init; }

    public bool IsEmpty =>
        Skills.Count + Cantrips.Count + Spells.Count + Armor.Count + Weapons.Count + Tools.Count + Languages.Count + SavingThrows.Count == 0;

    /// <summary>Whether it grants spells or cantrips.</summary>
    public bool HasSpells => Spells.Count + Cantrips.Count > 0;

    /// <summary>
    /// Both grants together (a race and the packs that extend it): lists without duplicates, the first spell entry of
    /// an index wins and so does the first spellcasting ability.
    /// </summary>
    public OptionGrants Merge(OptionGrants other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.IsEmpty && other.SpellcastingAbility is null)
        {
            return this;
        }

        static IReadOnlyList<string> Union(IReadOnlyList<string> a, IReadOnlyList<string> b) => [.. a.Concat(b).Distinct(StringComparer.Ordinal)];

        return new OptionGrants(
            Union(Skills, other.Skills),
            Union(Cantrips, other.Cantrips),
            [.. Spells.Concat(other.Spells).GroupBy(s => s.Index, StringComparer.Ordinal).Select(g => g.First())],
            Union(Armor, other.Armor),
            Union(Weapons, other.Weapons),
            Union(Tools, other.Tools),
            Union(Languages, other.Languages),
            Union(SavingThrows, other.SavingThrows))
        {
            SpellcastingAbility = SpellcastingAbility ?? other.SpellcastingAbility,
        };
    }
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

            var armor = new List<string>();
            var weapons = new List<string>();
            if (Property(root, "proficiency") is { ValueKind: JsonValueKind.Object } proficiency)
            {
                if (Property(proficiency, "armor") is { ValueKind: JsonValueKind.Array } armorList)
                {
                    armor.AddRange(Strings(armorList).Select(ProficiencyKeys.NormalizeArmor).OfType<string>().Distinct(StringComparer.Ordinal));
                }

                if (Property(proficiency, "weapon") is { ValueKind: JsonValueKind.Array } weaponList)
                {
                    weapons.AddRange(Strings(weaponList).Select(ProficiencyKeys.NormalizeWeapon).Distinct(StringComparer.Ordinal));
                }
            }

            return new OptionPrerequisites(Int(Property(root, "minLevel")), Text(Property(root, "pactBoon")), Text(Property(root, "cantrip")), abilities)
            {
                Races = Property(root, "races") is { ValueKind: JsonValueKind.Array } races
                    ? Strings(races).Select(r => r.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToList()
                    : [],
                ArmorProficiencies = armor,
                WeaponProficiencies = weapons,
                Spellcasting = Bool(Property(root, "spellcasting")),
            };
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
                        spells.Add(new GrantedSpell(index, Int(Property(entry, "minLevel"))) { UsesPerLongRest = Int(Property(entry, "usesPerLongRest")) });
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
                List("savingThrows"))
            {
                SpellcastingAbility = Text(Property(root, "spellcastingAbility")) is { } ability && Abilities.IsValid(ability) ? ability : null,
            };
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
