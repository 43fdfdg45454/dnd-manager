using System.Text.Json;
using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Application.Catalog;

/// <summary>Turns the JSON columns of catalog entities into DTOs.</summary>
internal static class CatalogJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    public static IReadOnlyList<AbilityBonusDto> AbilityBonuses(string json) =>
        TryDeserialize<List<AbilityBonus>>(json)?.Select(b => new AbilityBonusDto(b.Ability, b.Bonus)).ToList() ?? [];

    public static SkillChoicesDto SkillChoices(string json) =>
        TryDeserialize<StoredSkillChoices>(json) is { } stored
            ? new SkillChoicesDto(stored.Choose, stored.From ?? [])
            : new SkillChoicesDto(0, []);

    public static JsonElement Object(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : EmptyObject;
        }
        catch (JsonException)
        {
            return EmptyObject;
        }
    }

    public static SpellDamageDto? SpellDamage(string? json)
    {
        var parts = json is null ? null : TryDeserialize<List<StoredSpellDamage>>(json);
        if (parts is null || parts.Count == 0)
        {
            return null;
        }

        var atSlot = Merge(parts.Select(p => p.AtSlotLevel).ToList());
        var atCharacter = Merge(parts.Select(p => p.AtCharacterLevel).ToList());
        var firstDice = atSlot?.Values.FirstOrDefault() ?? atCharacter?.Values.FirstOrDefault();
        var types = parts.Select(p => p.Type).OfType<string>().Distinct().ToList();

        return new SpellDamageDto(firstDice, types.Count == 0 ? null : string.Join(" + ", types), atSlot, atCharacter);
    }

    /// <summary>A <c>{"level": "dice"}</c> map (spell healing), or null when absent or invalid.</summary>
    public static IReadOnlyDictionary<int, string>? LevelMap(string? json)
    {
        var map = json is null ? null : TryDeserialize<SortedDictionary<int, string>>(json);
        return map is null || map.Count == 0 ? null : map;
    }

    /// <summary>Joins the dice of several damage parts per level ("4d6 + 4d6").</summary>
    private static SortedDictionary<int, string>? Merge(IReadOnlyList<Dictionary<int, string>?> maps)
    {
        var present = maps.OfType<Dictionary<int, string>>().ToList();
        if (present.Count == 0)
        {
            return null;
        }

        var merged = new SortedDictionary<int, string>();
        foreach (var level in present.SelectMany(m => m.Keys).Distinct())
        {
            merged[level] = string.Join(" + ", present.Where(m => m.ContainsKey(level)).Select(m => m[level]));
        }

        return merged;
    }

    private static T? TryDeserialize<T>(string json)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record StoredSpellDamage(string? Type, Dictionary<int, string>? AtSlotLevel, Dictionary<int, string>? AtCharacterLevel);

    private sealed record StoredSkillChoices(int Choose, List<string>? From);
}
