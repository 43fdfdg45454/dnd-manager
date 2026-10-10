using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// A class or subclass feature that lets the character improve concrete items of its inventory (content packs,
/// <c>levels[].features[].itemEnhancement</c>): the options of <see cref="OptionSets"/> (options with
/// <see cref="ItemEffects"/>) are applied to one inventory entry at a time, up to <see cref="MaxPerItemByLevel"/> per item.
/// </summary>
/// <param name="OptionSets">Option sets whose options the feature applies.</param>
/// <param name="MaxPerItemByLevel">Options per item by level of the feature's class (the highest entry not above it).</param>
public sealed record ItemEnhancementRule(IReadOnlyList<string> OptionSets, IReadOnlyDictionary<int, int> MaxPerItemByLevel)
{
    public const int MaxOptionSets = 10;
    public const int MaxPerItemLimit = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Options one item may carry at that class level (0 below the first entry).</summary>
    public int MaxPerItemAt(int classLevel)
    {
        var levels = MaxPerItemByLevel.Keys.Where(l => l <= classLevel).ToList();
        return levels.Count == 0 ? 0 : MaxPerItemByLevel[levels.Max()];
    }

    public string ToJson() => JsonSerializer.Serialize(
        new Stored(OptionSets.ToList(), MaxPerItemByLevel.ToDictionary(e => e.Key.ToString(CultureInfo.InvariantCulture), e => e.Value)),
        JsonOptions);

    /// <summary>Tolerant parsing; null when the JSON is missing or malformed.</summary>
    public static ItemEnhancementRule? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Stored>(json, JsonOptions);
            if (stored?.OptionSets is not { Count: > 0 } sets)
            {
                return null;
            }

            var byLevel = new Dictionary<int, int>();
            foreach (var (key, value) in stored.MaxPerItemByLevel ?? [])
            {
                if (int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var level))
                {
                    byLevel[level] = value;
                }
            }

            return new ItemEnhancementRule(sets, byLevel);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Stored(List<string>? OptionSets, Dictionary<string, int>? MaxPerItemByLevel);
}

/// <summary>
/// What an option does to the item it is applied to (content packs, <c>options[].itemEffects</c>): bonuses to that item's
/// attack and damage or to the armor class it gives, properties it gains, steps of its damage die and a new damage type.
/// The option's <c>modifiers</c> of item kinds apply too while the item is active.
/// </summary>
public sealed record ItemEffects
{
    public const string Weapon = "weapon";
    public const string Armor = "armor";
    public const string Shield = "shield";
    public const int MaxBonus = 10;
    public const int MaxDamageSteps = 4;

    /// <summary>Kinds of item the option can be applied to.</summary>
    public static IReadOnlyList<string> Kinds { get; } = [Weapon, Armor, Shield];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    /// <summary>Kinds of item it applies to (<see cref="Kinds"/>); empty: any weapon, armor or shield.</summary>
    public IReadOnlyList<string> AppliesTo { get; init; } = [];

    public int AttackBonus { get; init; }

    public int DamageBonus { get; init; }

    /// <summary>Added to the armor class while the armor or shield is worn.</summary>
    public int ArmorClassBonus { get; init; }

    /// <summary>Properties the item gains (weapon properties or <c>reference.armorProperties</c>).</summary>
    public IReadOnlyList<string> AddProperties { get; init; } = [];

    /// <summary>Steps up (positive) or down of the damage die (see <see cref="Characters.ItemEnhancer.StepDamage"/>).</summary>
    public int DamageStep { get; init; }

    /// <summary>The damage type the weapon deals instead of its own; null keeps it.</summary>
    public string? DamageType { get; init; }

    /// <summary>The kind of an item for <see cref="AppliesTo"/>, or null when no enhancement applies to it.</summary>
    public static string? KindOf(EffectiveItem item) => item.Category switch
    {
        ItemCategory.Weapon => Weapon,
        ItemCategory.Armor => Armor,
        ItemCategory.Shield => Shield,
        _ => null,
    };

    public bool AppliesToItem(EffectiveItem item) =>
        KindOf(item) is { } kind && (AppliesTo.Count == 0 || AppliesTo.Contains(kind, StringComparer.Ordinal));

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Tolerant parsing; null when the JSON is missing or malformed.</summary>
    public static ItemEffects? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ItemEffects>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
