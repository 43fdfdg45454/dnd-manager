using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dnd.Application.Catalog;

namespace Dnd.Infrastructure.Catalog;

/// <summary>
/// <see cref="IBeastCatalog"/> over <c>server/seed/srd/5e-SRD-Beasts.json</c>, the beast subset
/// (<c>type == "beast"</c>) of <c>5e-SRD-Monsters.json</c> from the same 5e-database snapshot as the
/// rest of the SRD dataset (<see cref="SrdDataset.Version"/>). Embedded in this assembly, parsed once
/// and served from memory: no table, no migration.
/// </summary>
public sealed partial class SrdBeastCatalog : IBeastCatalog
{
    private const string FileName = "5e-SRD-Beasts.json";

    private static readonly Lazy<IReadOnlyList<BeastDto>> Beasts = new(Load);

    public IReadOnlyList<BeastDto> All => Beasts.Value;

    private static IReadOnlyList<BeastDto> Load()
    {
        var assembly = typeof(SrdBeastCatalog).Assembly;
        var resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith($".{FileName}", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"SRD dataset file '{FileName}' is not embedded in {assembly.GetName().Name}.");
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateArray()
            .Select(Map)
            .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static BeastDto Map(JsonElement m)
    {
        var cr = Number(m, "challenge_rating") ?? 0;
        var armor = m.TryGetProperty("armor_class", out var ac) && ac.ValueKind == JsonValueKind.Array && ac.GetArrayLength() > 0
            ? ac[0]
            : default;
        var proficiencies = Array(m, "proficiencies")
            .Select(p => (Index: Text(p.TryGetProperty("proficiency", out var prof) ? prof : default, "index") ?? string.Empty, Value: (int)(Number(p, "value") ?? 0)))
            .ToList();
        var senses = new Dictionary<string, string>(StringComparer.Ordinal);
        var passive = 0;
        if (m.TryGetProperty("senses", out var sensesJson) && sensesJson.ValueKind == JsonValueKind.Object)
        {
            foreach (var sense in sensesJson.EnumerateObject())
            {
                if (sense.Name == "passive_perception")
                {
                    passive = sense.Value.ValueKind == JsonValueKind.Number ? sense.Value.GetInt32() : 0;
                }
                else
                {
                    senses[sense.Name] = sense.Value.ToString();
                }
            }
        }

        return new BeastDto(
            Index: Text(m, "index") ?? string.Empty,
            Name: Text(m, "name") ?? Text(m, "index") ?? string.Empty,
            Size: Text(m, "size") ?? string.Empty,
            Alignment: Text(m, "alignment") ?? string.Empty,
            ChallengeRating: cr,
            ChallengeRatingText: BeastDto.FormatChallengeRating(cr),
            Xp: (int)(Number(m, "xp") ?? 0),
            ProficiencyBonus: (int)(Number(m, "proficiency_bonus") ?? 2),
            ArmorClass: (int)(Number(armor, "value") ?? 10),
            ArmorClassType: Text(armor, "type"),
            HitPoints: (int)(Number(m, "hit_points") ?? 1),
            HitDice: Text(m, "hit_dice") ?? string.Empty,
            HitPointsRoll: Text(m, "hit_points_roll"),
            Speeds: Speeds(m),
            Abilities: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["str"] = (int)(Number(m, "strength") ?? 10),
                ["dex"] = (int)(Number(m, "dexterity") ?? 10),
                ["con"] = (int)(Number(m, "constitution") ?? 10),
                ["int"] = (int)(Number(m, "intelligence") ?? 10),
                ["wis"] = (int)(Number(m, "wisdom") ?? 10),
                ["cha"] = (int)(Number(m, "charisma") ?? 10),
            },
            SavingThrows: proficiencies
                .Where(p => p.Index.StartsWith("saving-throw-", StringComparison.Ordinal))
                .ToDictionary(p => p.Index["saving-throw-".Length..], p => p.Value, StringComparer.Ordinal),
            Skills: proficiencies
                .Where(p => p.Index.StartsWith("skill-", StringComparison.Ordinal))
                .ToDictionary(p => p.Index["skill-".Length..], p => p.Value, StringComparer.Ordinal),
            Senses: senses,
            PassivePerception: passive,
            Languages: Text(m, "languages") ?? string.Empty,
            DamageVulnerabilities: Names(m, "damage_vulnerabilities"),
            DamageResistances: Names(m, "damage_resistances"),
            DamageImmunities: Names(m, "damage_immunities"),
            ConditionImmunities: Names(m, "condition_immunities"),
            Traits: Array(m, "special_abilities")
                .Select(t => new BeastTraitDto(Text(t, "name") ?? string.Empty, Text(t, "desc") ?? string.Empty, Save(t)))
                .ToList(),
            Actions: Array(m, "actions").Select(MapAction).ToList(),
            Description: Text(m, "desc"));
    }

    private static BeastActionDto MapAction(JsonElement a)
    {
        var damage = Array(a, "damage")
            .Where(d => Text(d, "damage_dice") is not null)
            .Select(d => new BeastDamageDto(
                CompactDice(Text(d, "damage_dice")!),
                Text(d.TryGetProperty("damage_type", out var type) ? type : default, "name")))
            .ToList();
        var bonus = Number(a, "attack_bonus");
        return new BeastActionDto(
            Text(a, "name") ?? string.Empty,
            Text(a, "desc") ?? string.Empty,
            bonus is null ? null : (int)bonus,
            damage,
            Save(a),
            a.TryGetProperty("multiattack_type", out _));
    }

    /// <summary>The structured "dc" of the entry or, when the dataset lacks it, the first "DC N Ability saving throw" of its text.</summary>
    private static BeastSaveDto? Save(JsonElement e)
    {
        if (e.TryGetProperty("dc", out var dc) && dc.ValueKind == JsonValueKind.Object)
        {
            var ability = Text(dc.TryGetProperty("dc_type", out var type) ? type : default, "index");
            if (Number(dc, "dc_value") is { } value && ability is not null)
            {
                return new BeastSaveDto((int)value, ability);
            }
        }

        var match = SavePattern().Match(Text(e, "desc") ?? string.Empty);
        return match.Success
            ? new BeastSaveDto(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), match.Groups[2].Value[..3].ToLowerInvariant())
            : null;
    }

    private static IReadOnlyDictionary<string, int> Speeds(JsonElement m)
    {
        var speeds = new Dictionary<string, int>(StringComparer.Ordinal);
        if (m.TryGetProperty("speed", out var speed) && speed.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in speed.EnumerateObject())
            {
                var match = FeetPattern().Match(entry.Value.ToString());
                if (match.Success)
                {
                    speeds[entry.Name] = int.Parse(match.Value, CultureInfo.InvariantCulture);
                }
            }
        }

        return speeds;
    }

    /// <summary>"1d6 + 3" → "1d6+3".</summary>
    private static string CompactDice(string dice) => dice.Replace(" ", string.Empty, StringComparison.Ordinal);

    private static IEnumerable<JsonElement> Array(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : [];

    private static IReadOnlyList<string> Names(JsonElement e, string name) =>
        Array(e, name)
            .Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : Text(v, "name"))
            .OfType<string>()
            .ToList();

    private static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? Number(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    [GeneratedRegex(@"\d+")]
    private static partial Regex FeetPattern();

    [GeneratedRegex(@"DC (\d+) (Strength|Dexterity|Constitution|Intelligence|Wisdom|Charisma) saving throw")]
    private static partial Regex SavePattern();
}
