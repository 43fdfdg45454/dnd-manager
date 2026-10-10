using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// Animal companion granted by a subclass feature (content packs, <c>levels[].features[].companion</c>): which beasts of
/// the catalog qualify and how the character improves its statblock (phase 25, block 6).
/// </summary>
/// <param name="MaxChallengeRating">Highest challenge rating of the beast (0.25 for 1/4).</param>
/// <param name="Sizes">Sizes allowed ("Medium", "Small"); empty: any size.</param>
/// <param name="HitPointsPerClassLevel">
/// N of <c>"max(beast, N*classLevel)"</c>: the companion has at least N × the level of the feature's class; null for
/// <c>"beast"</c> (the beast's own hit points).
/// </param>
/// <param name="ProficiencyBonusFromCharacter">The character's proficiency bonus is added to the AC and to the saving throws and skills the beast is proficient in.</param>
/// <param name="AttackBonusFromCharacter">The character's proficiency bonus is added to the attack and damage rolls of the beast.</param>
public sealed partial record CompanionRule(
    double MaxChallengeRating,
    IReadOnlyList<string> Sizes,
    int? HitPointsPerClassLevel,
    bool ProficiencyBonusFromCharacter,
    bool AttackBonusFromCharacter)
{
    public const double MaxChallengeRatingLimit = 30;
    public const int MinHitPointsMultiplier = 1;
    public const int MaxHitPointsMultiplier = 20;

    /// <summary>The <c>hitPoints</c> value that keeps the beast's own hit points.</summary>
    public const string BeastHitPoints = "beast";

    /// <summary>Creature sizes, smallest first (the sizes of the beast catalog).</summary>
    public static IReadOnlyList<string> AllSizes { get; } = ["Tiny", "Small", "Medium", "Large", "Huge", "Gargantuan"];

    /// <summary>The <c>hitPoints</c> text: <c>"beast"</c> or <c>"max(beast, N*classLevel)"</c>.</summary>
    public string HitPointsText => HitPointsPerClassLevel is { } n ? $"max(beast, {n}*classLevel)" : BeastHitPoints;

    /// <summary>Whether a beast of that challenge rating and size can be the companion.</summary>
    public bool Allows(double challengeRating, string size) =>
        challengeRating <= MaxChallengeRating
        && (Sizes.Count == 0 || Sizes.Contains(size, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Parses a <c>hitPoints</c> text: <c>"beast"</c> (null multiplier) or <c>"max(beast, N*classLevel)"</c> with N between
    /// <see cref="MinHitPointsMultiplier"/> and <see cref="MaxHitPointsMultiplier"/>; false when it is neither.
    /// </summary>
    public static bool TryParseHitPoints(string? text, out int? perClassLevel)
    {
        perClassLevel = null;
        var trimmed = text?.Trim() ?? string.Empty;
        if (string.Equals(trimmed, BeastHitPoints, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var match = HitPointsPattern().Match(trimmed);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            || n is < MinHitPointsMultiplier or > MaxHitPointsMultiplier)
        {
            return false;
        }

        perClassLevel = n;
        return true;
    }

    /// <summary>The stored JSON of a rule (camelCase, <c>hitPoints</c> as text).</summary>
    public string ToJson() => JsonSerializer.Serialize(
        new
        {
            beastFilter = new { maxChallengeRating = MaxChallengeRating, sizes = Sizes },
            hitPoints = HitPointsText,
            proficiencyBonusFromCharacter = ProficiencyBonusFromCharacter,
            attackBonusFromCharacter = AttackBonusFromCharacter,
        },
        JsonOptions);

    /// <summary>The rule stored by <see cref="ToJson"/>; null when missing or invalid.</summary>
    public static CompanionRule? Parse(string? json)
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
                || !root.TryGetProperty("beastFilter", out var filter) || filter.ValueKind != JsonValueKind.Object
                || !filter.TryGetProperty("maxChallengeRating", out var cr) || cr.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            var sizes = filter.TryGetProperty("sizes", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
                : [];
            var hitPoints = root.TryGetProperty("hitPoints", out var hp) && hp.ValueKind == JsonValueKind.String ? hp.GetString() : BeastHitPoints;
            if (!TryParseHitPoints(hitPoints, out var perLevel))
            {
                perLevel = null;
            }

            return new CompanionRule(
                cr.GetDouble(),
                sizes,
                perLevel,
                Flag(root, "proficiencyBonusFromCharacter"),
                Flag(root, "attackBonusFromCharacter"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Flag(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [GeneratedRegex(@"^max\(\s*beast\s*,\s*(\d{1,2})\s*\*\s*classLevel\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HitPointsPattern();
}
