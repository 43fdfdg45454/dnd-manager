using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>An ideal of a background table with the alignment it points to ("Lawful", "Any"...), if any.</summary>
public sealed record BackgroundIdeal(string Text, string? Alignment);

/// <summary>
/// Personality tables of a background (phase 22), stored as JSON in <see cref="BackgroundDefinition.PersonalityJson"/>:
/// the character rolls (or picks) two traits, one ideal, one bond and one flaw. The die of each table is implicit:
/// the number of its entries (d8 for eight traits, d6 for six ideals...).
/// </summary>
public sealed record BackgroundPersonality(
    IReadOnlyList<string> Traits,
    IReadOnlyList<BackgroundIdeal> Ideals,
    IReadOnlyList<string> Bonds,
    IReadOnlyList<string> Flaws)
{
    /// <summary>Entries of each table at most (packs).</summary>
    public const int MaxEntries = 20;

    /// <summary>Length of each entry at most (packs).</summary>
    public const int EntryMaxLength = 500;

    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool IsEmpty => Traits.Count == 0 && Ideals.Count == 0 && Bonds.Count == 0 && Flaws.Count == 0;

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Tolerant parse: null when the column is empty or malformed; blank entries are dropped.</summary>
    public static BackgroundPersonality? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<BackgroundPersonality>(json, Options);
            if (stored is null)
            {
                return null;
            }

            static List<string> Clean(IReadOnlyList<string>? list) =>
                (list ?? []).Where(e => !string.IsNullOrWhiteSpace(e)).ToList();

            var result = new BackgroundPersonality(
                Clean(stored.Traits),
                (stored.Ideals ?? []).Where(i => i is not null && !string.IsNullOrWhiteSpace(i.Text)).ToList(),
                Clean(stored.Bonds),
                Clean(stored.Flaws));
            return result.IsEmpty ? null : result;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}

/// <summary>
/// Optional table of a background (specialty, scheme, origin...): the character keeps one entry as
/// <c>Character.BackgroundDetail</c>. Stored as a JSON list in <see cref="BackgroundDefinition.OptionalTablesJson"/>.
/// </summary>
/// <param name="Key">Identifier of the table within the background ("specialty").</param>
public sealed record BackgroundTable(string Key, string Name, IReadOnlyList<string> Entries)
{
    /// <summary>Optional tables of a background at most (packs).</summary>
    public const int MaxTables = 20;

    public static string ToJson(IReadOnlyList<BackgroundTable> tables) => JsonSerializer.Serialize(tables, BackgroundPersonality.Options);

    /// <summary>Tolerant parse: empty when the column is empty or malformed.</summary>
    public static IReadOnlyList<BackgroundTable> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return (JsonSerializer.Deserialize<List<BackgroundTable>>(json, BackgroundPersonality.Options) ?? [])
                .Where(t => t is not null && !string.IsNullOrWhiteSpace(t.Key))
                .Select(t => t with { Entries = (t.Entries ?? []).Where(e => !string.IsNullOrWhiteSpace(e)).ToList() })
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
        catch (NotSupportedException)
        {
            return [];
        }
    }
}
