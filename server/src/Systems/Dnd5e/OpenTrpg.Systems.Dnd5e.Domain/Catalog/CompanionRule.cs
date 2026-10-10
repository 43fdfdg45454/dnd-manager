using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// Companion granted by a subclass feature (content packs, <c>levels[].features[].companion</c>): which beasts of the
/// catalog qualify (or which creatures of a closed list, of any type), how many companions the character may have and how
/// the character improves their statblock (phase 25, block 6; extended in phase 35B3).
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

    public const int MaxCountLimit = 20;
    public const int MaxCreatureIndexes = 50;
    public const int FeatureTextMaxLength = 2000;

    /// <summary>The <c>maxCount</c> value that allows as many companions as the character's proficiency bonus.</summary>
    public const string ProficiencyBonusCount = "proficiencyBonus";

    /// <summary>Closed list of creatures of the catalog (any type) that can be the companion; empty: beasts of the filter.</summary>
    public IReadOnlyList<string> CreatureIndexes { get; init; } = [];

    /// <summary>How many companions the character may have at once (1 by default); see <see cref="MaxCountIsProficiencyBonus"/>.</summary>
    public int MaxCount { get; init; } = 1;

    /// <summary>The character may have as many companions as its proficiency bonus (instead of <see cref="MaxCount"/>).</summary>
    public bool MaxCountIsProficiencyBonus { get; init; }

    /// <summary>Total challenge rating of all the companions by level of the feature's class (the highest entry not above it).</summary>
    public IReadOnlyDictionary<int, double> ChallengeBudgetByLevel { get; init; } = new Dictionary<int, double>();

    /// <summary>
    /// Hit points as a sum of terms (<c>"beast+2*classLevel"</c>, <c>"5*characterLevel+mod:con"</c>); null for
    /// <c>"beast"</c> and <c>"max(beast, N*classLevel)"</c>.
    /// </summary>
    public IReadOnlyList<CompanionHitPointsTerm>? HitPointsTerms { get; init; }

    /// <summary>Flat bonuses the character gives to every companion (numbers or the proficiency bonus).</summary>
    public CompanionBonuses Bonuses { get; init; } = CompanionBonuses.None;

    /// <summary>Texts of what the companions gain by level of the feature's class.</summary>
    public IReadOnlyDictionary<int, string> FeaturesByLevel { get; init; } = new Dictionary<int, string>();

    /// <summary>The <c>hitPoints</c> text: <c>"beast"</c>, <c>"max(beast, N*classLevel)"</c> or the formula.</summary>
    public string HitPointsText => HitPointsTerms is { } terms
        ? string.Join("+", terms.Select(t => t.ToString()))
        : HitPointsPerClassLevel is { } n ? $"max(beast, {n}*classLevel)" : BeastHitPoints;

    /// <summary>Whether a beast of that challenge rating and size can be the companion.</summary>
    public bool Allows(double challengeRating, string size) =>
        challengeRating <= MaxChallengeRating
        && (Sizes.Count == 0 || Sizes.Contains(size, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Whether a creature of the catalog can be the companion: one of <see cref="CreatureIndexes"/> when the rule lists them
    /// (any type), else a beast of the filter.
    /// </summary>
    public bool AllowsCreature(string index, bool isBeast, double challengeRating, string size) =>
        CreatureIndexes.Count > 0
            ? CreatureIndexes.Contains(index, StringComparer.Ordinal) && Allows(challengeRating, size)
            : isBeast && Allows(challengeRating, size);

    /// <summary>How many companions the character may have with that proficiency bonus.</summary>
    public int MaxCountFor(int proficiencyBonus) => Math.Clamp(MaxCountIsProficiencyBonus ? proficiencyBonus : MaxCount, 1, MaxCountLimit);

    /// <summary>The challenge rating budget at that class level, or null when the rule has none (or not yet).</summary>
    public double? ChallengeBudgetAt(int classLevel)
    {
        var levels = ChallengeBudgetByLevel.Keys.Where(l => l <= classLevel).ToList();
        return ChallengeBudgetByLevel.Count == 0 ? null : levels.Count == 0 ? 0 : ChallengeBudgetByLevel[levels.Max()];
    }

    /// <summary>The texts of <see cref="FeaturesByLevel"/> up to that class level, lowest level first.</summary>
    public IReadOnlyList<(int Level, string Text)> FeaturesAt(int classLevel) =>
        [.. FeaturesByLevel.Where(f => f.Key <= classLevel).OrderBy(f => f.Key).Select(f => (f.Key, f.Value))];

    /// <summary>The <c>hitPoints</c> formula as terms (<c>"beast+2*classLevel"</c>); null when it is not valid.</summary>
    public static IReadOnlyList<CompanionHitPointsTerm>? ParseHitPointsFormula(string? text)
    {
        var parts = (text ?? string.Empty).Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length is 0 or > 10 || parts.Any(p => p.Length == 0))
        {
            return null;
        }

        var terms = new List<CompanionHitPointsTerm>();
        foreach (var part in parts)
        {
            if (int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var constant) && constant is >= 0 and <= 999)
            {
                terms.Add(new CompanionHitPointsTerm(CompanionHitPointsTerm.Constant, constant));
                continue;
            }

            var match = HitPointsTermPattern().Match(part);
            if (!match.Success)
            {
                return null;
            }

            var factor = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture) : 1;
            if (factor is < 1 or > 99)
            {
                return null;
            }

            terms.Add(new CompanionHitPointsTerm(match.Groups[2].Value, factor));
        }

        return terms.Count(t => t.Symbol == CompanionHitPointsTerm.Beast) > 1 ? null : terms;
    }

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
            creatureIndexes = CreatureIndexes.Count > 0 ? CreatureIndexes : null,
            maxCount = MaxCountIsProficiencyBonus ? (object)ProficiencyBonusCount : MaxCount != 1 ? MaxCount : null,
            crBudgetByLevel = ChallengeBudgetByLevel.Count > 0
                ? ChallengeBudgetByLevel.OrderBy(b => b.Key).ToDictionary(b => b.Key.ToString(CultureInfo.InvariantCulture), b => b.Value)
                : null,
            bonuses = Bonuses.IsEmpty ? null : Bonuses,
            featuresByLevel = FeaturesByLevel.Count > 0
                ? FeaturesByLevel.OrderBy(f => f.Key).ToDictionary(f => f.Key.ToString(CultureInfo.InvariantCulture), f => f.Value)
                : null,
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
            IReadOnlyList<CompanionHitPointsTerm>? terms = null;
            if (!TryParseHitPoints(hitPoints, out var perLevel))
            {
                perLevel = null;
                terms = ParseHitPointsFormula(hitPoints);
            }

            var maxCount = root.TryGetProperty("maxCount", out var count) ? count : default;
            return new CompanionRule(
                cr.GetDouble(),
                sizes,
                perLevel,
                Flag(root, "proficiencyBonusFromCharacter"),
                Flag(root, "attackBonusFromCharacter"))
            {
                CreatureIndexes = root.TryGetProperty("creatureIndexes", out var creatures) && creatures.ValueKind == JsonValueKind.Array
                    ? creatures.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList()
                    : [],
                MaxCount = maxCount.ValueKind == JsonValueKind.Number && maxCount.TryGetInt32(out var fixedCount) ? Math.Clamp(fixedCount, 1, MaxCountLimit) : 1,
                MaxCountIsProficiencyBonus = maxCount.ValueKind == JsonValueKind.String && maxCount.GetString() == ProficiencyBonusCount,
                ChallengeBudgetByLevel = root.TryGetProperty("crBudgetByLevel", out var budget) && budget.ValueKind == JsonValueKind.Object
                    ? budget.EnumerateObject()
                        .Where(p => int.TryParse(p.Name, NumberStyles.None, CultureInfo.InvariantCulture, out _) && p.Value.ValueKind == JsonValueKind.Number)
                        .ToDictionary(p => int.Parse(p.Name, CultureInfo.InvariantCulture), p => p.Value.GetDouble())
                    : new Dictionary<int, double>(),
                HitPointsTerms = terms,
                Bonuses = root.TryGetProperty("bonuses", out var bonuses) && bonuses.ValueKind == JsonValueKind.Object
                    ? new CompanionBonuses(Text(bonuses, "armorClass"), Text(bonuses, "savingThrows"), Text(bonuses, "attack"), Text(bonuses, "damage"))
                    : CompanionBonuses.None,
                FeaturesByLevel = root.TryGetProperty("featuresByLevel", out var features) && features.ValueKind == JsonValueKind.Object
                    ? features.EnumerateObject()
                        .Where(p => int.TryParse(p.Name, NumberStyles.None, CultureInfo.InvariantCulture, out _) && p.Value.ValueKind == JsonValueKind.String)
                        .ToDictionary(p => int.Parse(p.Name, CultureInfo.InvariantCulture), p => p.Value.GetString()!)
                    : new Dictionary<int, string>(),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        } : null;

    private static bool Flag(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    [GeneratedRegex(@"^(?:(\d{1,2})\*)?(beast|characterLevel|classLevel|halfClassLevel|mod:(?:str|dex|con|int|wis|cha))$", RegexOptions.CultureInvariant)]
    private static partial Regex HitPointsTermPattern();

    [GeneratedRegex(@"^max\(\s*beast\s*,\s*(\d{1,2})\s*\*\s*classLevel\s*\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HitPointsPattern();
}

/// <summary>
/// A term of a companion's hit points formula: <see cref="Factor"/> × <see cref="Symbol"/> (<c>beast</c>, <c>characterLevel</c>,
/// <c>classLevel</c>, <c>halfClassLevel</c> or <c>mod:&lt;ability&gt;</c> of the character), or the constant <see cref="Factor"/>.
/// </summary>
public sealed record CompanionHitPointsTerm(string Symbol, int Factor)
{
    public const string Constant = "constant";
    public const string Beast = "beast";

    public override string ToString() => Symbol == Constant
        ? Factor.ToString(CultureInfo.InvariantCulture)
        : Factor == 1 ? Symbol : $"{Factor.ToString(CultureInfo.InvariantCulture)}*{Symbol}";
}

/// <summary>
/// Bonuses the character gives to its companions: each one a number (<c>"2"</c>) or <c>"proficiencyBonus"</c>; null when
/// none. <see cref="SavingThrows"/> goes to every saving throw (not only the proficient ones).
/// </summary>
public sealed record CompanionBonuses(string? ArmorClass, string? SavingThrows, string? Attack, string? Damage)
{
    public const string ProficiencyBonus = "proficiencyBonus";

    public static CompanionBonuses None { get; } = new(null, null, null, null);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => ArmorClass is null && SavingThrows is null && Attack is null && Damage is null;

    /// <summary>A valid value: <c>"proficiencyBonus"</c> or an integer from −10 to 30.</summary>
    public static bool IsValid(string? value) =>
        value is null or ProficiencyBonus
        || (int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) && number is >= -10 and <= 30);

    /// <summary>The value with that proficiency bonus (0 when null or invalid).</summary>
    public static int Resolve(string? value, int proficiencyBonus) =>
        value == ProficiencyBonus ? proficiencyBonus
        : int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : 0;
}
