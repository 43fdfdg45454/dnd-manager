using System.Text.Json;

namespace OpenTrpg.Core.Domain.Catalog;

/// <summary>One range of a roll table: the results <see cref="From"/>..<see cref="To"/> of the die give <see cref="Text"/>.</summary>
public sealed record RollTableEntry(int From, int To, string Text);

/// <summary>
/// Generic roll table of a content pack (phase 22), e.g. the d100 Wild Magic Surge of a sorcerer subclass. The
/// entries cover every result of the die exactly once. When several packs define the same <see cref="Key"/>, the
/// most recently imported one wins.
/// </summary>
public sealed class RollTable
{
    public const int KeyMaxLength = 100;
    public const int EntryMaxLength = 2000;

    /// <summary>Dice a roll table may use.</summary>
    public static readonly IReadOnlyList<int> AllowedDice = [2, 3, 4, 6, 8, 10, 12, 20, 100];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Id of the content pack that defines the table.</summary>
    public required string Source { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>"d100", "d20"...</summary>
    public required string Dice { get; init; }

    /// <summary>Class the table belongs to (null for a general table).</summary>
    public string? ClassIndex { get; init; }

    /// <summary>Subclass the table belongs to (e.g. a Wild Magic subclass), or null.</summary>
    public string? SubclassIndex { get; init; }

    /// <summary>Persisted form of <see cref="Entries"/>.</summary>
    public string EntriesJson { get; init; } = "[]";

    public IReadOnlyList<RollTableEntry> Entries
    {
        get
        {
            try
            {
                return JsonSerializer.Deserialize<List<RollTableEntry>>(EntriesJson, JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }

    public static string SerializeEntries(IEnumerable<RollTableEntry> entries) =>
        JsonSerializer.Serialize(entries.OrderBy(e => e.From).ToList(), JsonOptions);

    /// <summary>Faces of a die written "dN" ("d100" → 100), or null when it is not an allowed die.</summary>
    public static int? Faces(string? dice)
    {
        var text = dice?.Trim().ToLowerInvariant();
        return text is { Length: > 1 } && text[0] == 'd' && int.TryParse(text[1..], out var faces) && AllowedDice.Contains(faces)
            ? faces
            : null;
    }
}
