using System.Text.Json;

namespace OpenTrpg.Core.Domain.Catalog;

/// <summary>
/// A spell a subclass adds to its class's spell list (content packs, <c>subclasses[].expandedSpellList</c>): the
/// characters with that subclass may learn or prepare it as if it were on the class list. It is not granted.
/// </summary>
/// <param name="Index">Spell index (SRD or a pack).</param>
/// <param name="Level">Spell level (0-9), checked against the spell when the pack is imported.</param>
public sealed record ExpandedSpell(string Index, int Level)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Serialized form stored in <see cref="SubclassDefinition.ExpandedSpellListJson"/>; null for an empty list.</summary>
    public static string? ToJson(IReadOnlyCollection<ExpandedSpell> spells)
    {
        ArgumentNullException.ThrowIfNull(spells);
        return spells.Count == 0 ? null : JsonSerializer.Serialize(spells.Select(s => new { index = s.Index, level = s.Level }), Options);
    }

    /// <summary>Tolerant parsing of the stored JSON: empty when missing or malformed; invalid entries are skipped.</summary>
    public static IReadOnlyList<ExpandedSpell> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<ExpandedSpell>();
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object
                    && entry.TryGetProperty("index", out var index)
                    && index.ValueKind == JsonValueKind.String
                    && index.GetString()?.Trim() is { Length: > 0 } text
                    && entry.TryGetProperty("level", out var level)
                    && level.ValueKind == JsonValueKind.Number
                    && level.TryGetInt32(out var number)
                    && number is >= 0 and <= 9)
                {
                    result.Add(new ExpandedSpell(text, number));
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
