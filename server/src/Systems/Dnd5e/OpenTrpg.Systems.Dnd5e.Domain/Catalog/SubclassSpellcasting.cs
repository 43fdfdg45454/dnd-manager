using System.Globalization;
using System.Text.Json;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// Spellcasting a subclass adds to a base class that does not cast spells, or only knows cantrips (content packs: a
/// fighter or rogue subclass that learns wizard spells). The class becomes a caster of <see cref="SpellcastingLevel"/>
/// (3 third, 2 half, 1 full; 0 for Pact Magic and an own table, which do not add to the multiclass spellcaster level)
/// from <see cref="FromLevel"/>, casting with <see cref="Ability"/>.
/// </summary>
/// <param name="SpellcastingLevel">1 full, 2 half, 3 third, 0 pact or table (as <see cref="ClassDefinition.SpellcastingLevel"/>).</param>
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
    public const string Pact = "pact";
    public const string Table = "table";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Names of the progressions accepted in the pack JSON.</summary>
    public static IReadOnlyList<string> Progressions { get; } = [Third, Half, Full, Pact, Table];

    /// <summary>Pact Magic: slots of a single level that come back on a short rest (<see cref="Slots"/> or the warlock table).</summary>
    public bool IsPact { get; init; }

    /// <summary>Own slots by class level (rows of spell levels 1-9; the highest key not above the level applies), for "pact" and "table".</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<int>> Slots { get; init; } = new Dictionary<int, IReadOnlyList<int>>();

    /// <summary>"third", "half", "full", "pact" or "table".</summary>
    public string Progression => IsPact ? Pact : SpellcastingLevel switch
    {
        0 => Table,
        1 => Full,
        2 => Half,
        _ => Third,
    };

    /// <summary>1 for "full", 2 for "half", 3 for "third", 0 for "pact" and "table"; null for anything else.</summary>
    public static int? LevelOf(string? progression) => progression?.Trim().ToLowerInvariant() switch
    {
        Full => 1,
        Half => 2,
        Third => 3,
        Pact or Table => 0,
        _ => null,
    };

    /// <summary>Slots (spell levels 1-9) of a single-classed character of this progression at a class level.</summary>
    public IReadOnlyList<int> SlotsAt(int classLevel)
    {
        if (classLevel < FromLevel)
        {
            return new int[ClassLevel.SpellSlotLevels];
        }

        if (SpellcastingLevel > 0)
        {
            return SpellSlotTables.ProgressionSlots(SpellcastingLevel, classLevel);
        }

        var keys = Slots.Keys.Where(k => k <= classLevel).ToList();
        return keys.Count > 0 ? Slots[keys.Max()] : IsPact && Slots.Count == 0 ? SpellSlotTables.PactSlots(classLevel) : new int[ClassLevel.SpellSlotLevels];
    }

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
            slots = Slots.Count == 0 ? null : Slots.OrderBy(e => e.Key).ToDictionary(e => e.Key.ToString(CultureInfo.InvariantCulture), e => e.Value),
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
            return new SubclassSpellcasting(level, ability, fromLevel, spellList, KnownTable(root, "cantripsKnown"), KnownTable(root, "spellsKnown"))
            {
                IsPact = string.Equals(Text(root, "progression"), Pact, StringComparison.OrdinalIgnoreCase),
                Slots = SlotRows(root),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString()?.Trim() is { Length: > 0 } text ? text : null;

    private static Dictionary<int, IReadOnlyList<int>> SlotRows(JsonElement root)
    {
        var result = new Dictionary<int, IReadOnlyList<int>>();
        if (!root.TryGetProperty("slots", out var table) || table.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var entry in table.EnumerateObject())
        {
            if (int.TryParse(entry.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var level) && level is >= 1 and <= 20
                && entry.Value.ValueKind == JsonValueKind.Array)
            {
                var row = new int[ClassLevel.SpellSlotLevels];
                var i = 0;
                foreach (var value in entry.Value.EnumerateArray())
                {
                    if (i < row.Length && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var count))
                    {
                        row[i] = count;
                    }

                    i++;
                }

                result[level] = row;
            }
        }

        return result;
    }

    private static Dictionary<int, int> KnownTable(JsonElement root, string name)
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
