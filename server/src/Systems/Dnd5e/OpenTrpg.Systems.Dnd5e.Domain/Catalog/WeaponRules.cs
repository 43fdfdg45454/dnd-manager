using System.Globalization;
using System.Text.Json;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// Second profile of a weapon (double or switch weapons): another attack of the sheet with its own damage, properties
/// and range. The item's modifiers apply to both.
/// </summary>
public sealed record AlternateWeaponProfile(string? Name, string Damage, string? DamageType)
{
    public string? VersatileDamage { get; init; }

    /// <summary>"melee" or "ranged" (the weapon's own when null).</summary>
    public string? Range { get; init; }

    public IReadOnlyList<string> Properties { get; init; } = [];

    public int? RangeNormal { get; init; }

    public int? RangeLong { get; init; }
}

/// <summary>
/// Rules of a content pack item kept in its system data (<c>ItemTemplate.SystemDataJson</c>): whether the attack adds the
/// ability modifier to the damage (firearms do not by default), the minimum ability scores to wield it, a second
/// profile, the classes that may attune to it and the firearm era.
/// </summary>
public sealed record WeaponRules
{
    public static WeaponRules None { get; } = new();

    /// <summary>Explicit <c>weapon.addAbilityToDamage</c>; null = the default (not for firearms, yes otherwise).</summary>
    public bool? AddAbilityToDamage { get; init; }

    /// <summary>Minimum scores to wield the weapon without penalty ("str" → 16); the sheet warns below them.</summary>
    public IReadOnlyDictionary<string, int> Requires { get; init; } = new Dictionary<string, int>();

    public AlternateWeaponProfile? Alternate { get; init; }

    /// <summary>Class indexes that may attune to the item; empty = any.</summary>
    public IReadOnlyList<string> AttunementClasses { get; init; } = [];

    /// <summary>Era of a firearm ("renaissance"), informative.</summary>
    public string? FirearmEra { get; init; }

    /// <summary>Tolerant parsing of the system data JSON of an item; anything malformed is ignored.</summary>
    public static WeaponRules Parse(string? systemDataJson)
    {
        if (string.IsNullOrWhiteSpace(systemDataJson))
        {
            return None;
        }

        try
        {
            using var document = JsonDocument.Parse(systemDataJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return None;
            }

            var requires = new Dictionary<string, int>(StringComparer.Ordinal);
            if (root.TryGetProperty("requires", out var req) && req.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in req.EnumerateObject())
                {
                    if (Abilities.IsValid(entry.Name) && entry.Value.ValueKind == JsonValueKind.Number && entry.Value.TryGetInt32(out var min))
                    {
                        requires[entry.Name] = min;
                    }
                }
            }

            AlternateWeaponProfile? alternate = null;
            if (root.TryGetProperty("alternate", out var alt) && alt.ValueKind == JsonValueKind.Object && Text(alt, "damage") is { } damage)
            {
                alternate = new AlternateWeaponProfile(Text(alt, "name"), damage, Text(alt, "damageType"))
                {
                    VersatileDamage = Text(alt, "versatileDamage"),
                    Range = Text(alt, "range"),
                    Properties = alt.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Array
                        ? props.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!).ToList()
                        : [],
                    RangeNormal = Int(alt, "rangeNormal"),
                    RangeLong = Int(alt, "rangeLong"),
                };
            }

            return new WeaponRules
            {
                AddAbilityToDamage = root.TryGetProperty("addAbilityToDamage", out var add) && add.ValueKind is JsonValueKind.True or JsonValueKind.False ? add.GetBoolean() : null,
                Requires = requires,
                Alternate = alternate,
                AttunementClasses = root.TryGetProperty("attunementClasses", out var classes) && classes.ValueKind == JsonValueKind.Array
                    ? classes.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.String).Select(c => c.GetString()!).ToList()
                    : [],
                FirearmEra = root.TryGetProperty("firearm", out var firearm) && firearm.ValueKind == JsonValueKind.Object ? Text(firearm, "era") : null,
            };
        }
        catch (JsonException)
        {
            return None;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString()?.Trim() is { Length: > 0 } text ? text : null;

    private static int? Int(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;

    /// <summary>"Requiere Fuerza 16; tienes 14": one warning per requirement the scores do not meet.</summary>
    public IEnumerable<string> UnmetRequirements(Func<string, int> score, Func<string, string> abilityName)
    {
        foreach (var (ability, min) in Requires.OrderBy(r => Abilities.All.ToList().IndexOf(r.Key)))
        {
            var current = score(ability);
            if (current < min)
            {
                yield return string.Create(CultureInfo.InvariantCulture, $"Requiere {abilityName(ability)} {min}; tienes {current}.");
            }
        }
    }
}
