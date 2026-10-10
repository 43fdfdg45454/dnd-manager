using System.Globalization;

namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// A resource whose values are rolled after a rest (e.g. two d20 after a long rest): <see cref="Count"/> dice of
/// <see cref="Die"/> faces after <see cref="Rest"/> (a long rest also counts for <see cref="RestKind.Short"/>). The
/// player rolls them physically and writes the results (<see cref="Character.RecordResourceRolls"/>).
/// </summary>
public sealed record RollOnRest(int Die, int Count, RestKind Rest)
{
    public const int MaxCount = 20;

    public static IReadOnlyList<int> AllowedDice { get; } = [4, 6, 8, 10, 12, 20, 100];

    /// <summary>"d20" → 20 ("1d20" also accepted); null when not a valid die.</summary>
    public static int? ParseDie(string? dice)
    {
        var text = (dice ?? string.Empty).Trim().ToLowerInvariant();
        if (text.StartsWith("1d", StringComparison.Ordinal))
        {
            text = text[1..];
        }

        return text.StartsWith('d') && int.TryParse(text[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var die) && AllowedDice.Contains(die)
            ? die
            : null;
    }

    /// <summary>"short"/"ShortRest" → Short, "long"/"LongRest" → Long; null otherwise.</summary>
    public static RestKind? ParseRest(string? rest) => (rest ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "short" or "shortrest" => RestKind.Short,
        "long" or "longrest" => RestKind.Long,
        _ => null,
    };

    /// <summary>"d20".</summary>
    public string Dice => $"d{Die}";

    /// <summary>Whether a rest of <paramref name="kind"/> asks for new rolls.</summary>
    public bool RollsAfter(RestKind kind) => kind == RestKind.Long || Rest == RestKind.Short;
}
