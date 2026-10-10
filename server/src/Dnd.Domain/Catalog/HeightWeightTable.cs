using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Dnd.Domain.Catalog;

/// <summary>
/// Random height and weight table of a race or subrace (PHB chapter 4): height = base + height roll (inches);
/// weight = base + height roll × weight roll (pounds). The modifiers are dice expressions (<c>"2d10"</c>) or a
/// whole number (<c>"1"</c> for ×1). No mechanical effect. The SRD has no table; content packs add it
/// (<c>heightWeight</c>). A subrace's table replaces its race's.
/// </summary>
public sealed partial record HeightWeightTable(int BaseHeightInches, string HeightModifier, int BaseWeightPounds, string WeightModifier)
{
    public const int MinBase = 1;
    public const int MaxBaseHeightInches = 120;
    public const int MaxBaseWeightPounds = 1000;
    public const int MaxDiceCount = 10;
    public const int MaxDieSides = 100;
    public const int MaxConstant = 100;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Height for a fixed roll of <see cref="HeightModifier"/>.</summary>
    public int HeightFor(int heightRoll) => BaseHeightInches + heightRoll;

    /// <summary>Weight for fixed rolls of both modifiers: base + height roll × weight roll.</summary>
    public int WeightFor(int heightRoll, int weightRoll) => BaseWeightPounds + (heightRoll * weightRoll);

    /// <summary>The problem with the table in Spanish, or null when it is valid.</summary>
    public string? Validate()
    {
        if (BaseHeightInches is < MinBase or > MaxBaseHeightInches)
        {
            return $"La altura base debe estar entre {MinBase} y {MaxBaseHeightInches} pulgadas.";
        }

        if (BaseWeightPounds is < MinBase or > MaxBaseWeightPounds)
        {
            return $"El peso base debe estar entre {MinBase} y {MaxBaseWeightPounds} libras.";
        }

        if (!IsValidModifier(HeightModifier))
        {
            return $"El modificador de altura '{HeightModifier}' no es una expresión de dados válida (NdM o un entero).";
        }

        if (!IsValidModifier(WeightModifier))
        {
            return $"El modificador de peso '{WeightModifier}' no es una expresión de dados válida (NdM o un entero).";
        }

        return null;
    }

    /// <summary>
    /// <c>NdM</c> (1–<see cref="MaxDiceCount"/> dice of 2–<see cref="MaxDieSides"/> sides) or a whole number
    /// from 1 to <see cref="MaxConstant"/>.
    /// </summary>
    public static bool IsValidModifier(string? modifier)
    {
        if (string.IsNullOrWhiteSpace(modifier))
        {
            return false;
        }

        var match = ModifierPattern().Match(modifier.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (match.Groups["constant"].Success)
        {
            return int.TryParse(match.Groups["constant"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var constant)
                && constant is >= 1 and <= MaxConstant;
        }

        return int.TryParse(match.Groups["count"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            && int.TryParse(match.Groups["sides"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sides)
            && count is >= 1 and <= MaxDiceCount
            && sides is >= 2 and <= MaxDieSides;
    }

    /// <summary>Trimmed and lower-cased modifiers (<c>"2D10 "</c> → <c>"2d10"</c>).</summary>
    public HeightWeightTable Normalize() => this with
    {
        HeightModifier = HeightModifier.Trim().ToLowerInvariant(),
        WeightModifier = WeightModifier.Trim().ToLowerInvariant(),
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Tolerant: null, empty, malformed or invalid JSON gives null.</summary>
    public static HeightWeightTable? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var table = JsonSerializer.Deserialize<HeightWeightTable>(json, Options);
            return table is { HeightModifier: not null, WeightModifier: not null } && table.Validate() is null ? table : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"^(?:(?<count>\d{1,2})[dD](?<sides>\d{1,3})|(?<constant>\d{1,3}))$", RegexOptions.CultureInvariant)]
    private static partial Regex ModifierPattern();
}
