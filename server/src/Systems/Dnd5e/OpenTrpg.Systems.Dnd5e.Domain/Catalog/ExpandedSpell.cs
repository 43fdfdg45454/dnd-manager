using System.Text.Json;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

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
    public static string? ToJson(IReadOnlyCollection<ExpandedSpell> spells, IReadOnlyCollection<string>? tags = null)
    {
        ArgumentNullException.ThrowIfNull(spells);
        tags ??= [];
        return spells.Count + tags.Count == 0
            ? null
            : JsonSerializer.Serialize(spells.Select(s => (object)new { index = s.Index, level = s.Level }).Concat(tags.Select(t => new { tag = t })), Options);
    }

    /// <summary>The spell tags of the stored JSON (entries <c>{ "tag": "..." }</c>): every spell with one of them is added.</summary>
    public static IReadOnlyList<string> ParseTags(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind != JsonValueKind.Array
                ? []
                : document.RootElement.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("tag", out var tag) && tag.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetProperty("tag").GetString()!.Trim())
                    .Where(t => t.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
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
