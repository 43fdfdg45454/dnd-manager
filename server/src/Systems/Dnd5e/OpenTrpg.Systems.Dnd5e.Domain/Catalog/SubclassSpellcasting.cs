using System.Globalization;
using System.Text.Json;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// Spellcasting a subclass adds to a base class that does not cast spells (content packs: a fighter or rogue
/// subclass that learns wizard spells). The class becomes a caster of <see cref="SpellcastingLevel"/> (3 third,
/// 2 half, 1 full) from <see cref="FromLevel"/>, casting with <see cref="Ability"/>.
/// </summary>
/// <param name="SpellcastingLevel">1 full, 2 half, 3 third (as <see cref="ClassDefinition.SpellcastingLevel"/>).</param>
/// <param name="Ability">"int", "wis" or "cha".</param>
/// <param name="FromLevel">Class level (1-20) from which the class casts spells.</param>
/// <param name="SpellList">Class whose spell list is used ("wizard").</param>
/// <param name="CantripsKnown">Cantrips known by class level (entries of the levels where the number changes).</param>
/// <param name="SpellsKnown">Spells known by class level (entries of the levels where the number changes).</param>
public sealed record SubclassSpellcasting(
    int SpellcastingLevel,
    string Ability,
    int FromLevel,
    string SpellList,
    IReadOnlyDictionary<int, int> CantripsKnown,
    IReadOnlyDictionary<int, int> SpellsKnown)
{
    public const string Third = "third";
    public const string Half = "half";
    public const string Full = "full";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Names of the progressions accepted in the pack JSON.</summary>
    public static IReadOnlyList<string> Progressions { get; } = [Third, Half, Full];

    /// <summary>"third", "half" or "full".</summary>
    public string Progression => SpellcastingLevel switch
    {
        1 => Full,
        2 => Half,
        _ => Third,
    };

    /// <summary>1 for "full", 2 for "half", 3 for "third"; null for anything else.</summary>
    public static int? LevelOf(string? progression) => progression?.Trim().ToLowerInvariant() switch
    {
        Full => 1,
        Half => 2,
        Third => 3,
        _ => null,
    };

    /// <summary>Slots (spell levels 1-9) of a single-classed character of this progression at a class level.</summary>
    public IReadOnlyList<int> SlotsAt(int classLevel) =>
        classLevel < FromLevel ? new int[ClassLevel.SpellSlotLevels] : SpellSlotTables.ProgressionSlots(SpellcastingLevel, classLevel);

    /// <summary>Cantrips known at a class level (highest entry not above it), or null when the table is empty.</summary>
    public int? CantripsKnownAt(int classLevel) => At(CantripsKnown, classLevel);

    /// <summary>Spells known at a class level (highest entry not above it), or null when the table is empty.</summary>
    public int? SpellsKnownAt(int classLevel) => At(SpellsKnown, classLevel);

    /// <summary>The value of the highest entry not above <paramref name="classLevel"/>; 0 below the first entry, null for an empty table.</summary>
    public static int? At(IReadOnlyDictionary<int, int> table, int classLevel)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Count == 0)
        {
            return null;
        }

        var levels = table.Keys.Where(l => l <= classLevel).ToList();
        return levels.Count == 0 ? 0 : table[levels.Max()];
    }

    /// <summary>Serialized form stored in <see cref="SubclassDefinition.SpellcastingJson"/>.</summary>
    public string ToJson() => JsonSerializer.Serialize(
        new
        {
            progression = Progression,
            ability = Ability,
            fromLevel = FromLevel,
            spellList = SpellList,
            cantripsKnown = CantripsKnown.OrderBy(e => e.Key).ToDictionary(e => e.Key.ToString(CultureInfo.InvariantCulture), e => e.Value),
            spellsKnown = SpellsKnown.OrderBy(e => e.Key).ToDictionary(e => e.Key.ToString(CultureInfo.InvariantCulture), e => e.Value),
        },
        Options);

    /// <summary>Tolerant parsing of the stored JSON: null when missing or malformed.</summary>
    public static SubclassSpellcasting? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || LevelOf(Text(root, "progression")) is not { } level
                || Text(root, "ability")?.ToLowerInvariant() is not { } ability
                || !Abilities.IsValid(ability)
                || Text(root, "spellList") is not { } spellList)
            {
                return null;
            }

            var fromLevel = root.TryGetProperty("fromLevel", out var from) && from.ValueKind == JsonValueKind.Number && from.TryGetInt32(out var parsed)
                ? Math.Clamp(parsed, 1, 20)
                : 1;
            return new SubclassSpellcasting(level, ability, fromLevel, spellList, Table(root, "cantripsKnown"), Table(root, "spellsKnown"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString()?.Trim() is { Length: > 0 } text ? text : null;

    private static Dictionary<int, int> Table(JsonElement root, string name)
    {
        var result = new Dictionary<int, int>();
        if (!root.TryGetProperty(name, out var table) || table.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var entry in table.EnumerateObject())
        {
            if (int.TryParse(entry.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var level)
                && level is >= 1 and <= 20
                && entry.Value.ValueKind == JsonValueKind.Number
                && entry.Value.TryGetInt32(out var value))
            {
                result[level] = value;
            }
        }

        return result;
    }
}
