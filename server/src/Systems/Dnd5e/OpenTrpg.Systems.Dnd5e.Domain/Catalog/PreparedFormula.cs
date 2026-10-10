using System.Globalization;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// Spells prepared by a pack class as a sum of terms separated by "+": <c>mod:&lt;ability&gt;</c> (the ability modifier),
/// <c>classLevel</c>, <c>halfClassLevel</c>, <c>thirdClassLevel</c> (rounded down) and whole numbers. The result is
/// at least 1. Example: <c>mod:wis+halfClassLevel</c>.
/// </summary>
public static class PreparedFormula
{
    public const int MaxLength = 100;

    /// <summary>Null when the formula is valid; otherwise the reason, in Spanish.</summary>
    public static string? Problem(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return "La fórmula está vacía.";
        }

        if (formula.Length > MaxLength)
        {
            return $"La fórmula admite como máximo {MaxLength} caracteres.";
        }

        foreach (var term in Terms(formula))
        {
            if (!IsTerm(term))
            {
                return $"Término desconocido: «{term}». Usa mod:<característica>, classLevel, halfClassLevel, thirdClassLevel o números, separados por +.";
            }
        }

        return null;
    }

    /// <summary>The value of a valid formula (minimum 1); null when it is not valid.</summary>
    public static int? Evaluate(string? formula, int classLevel, Func<string, int> abilityModifier)
    {
        ArgumentNullException.ThrowIfNull(abilityModifier);
        if (Problem(formula) is not null)
        {
            return null;
        }

        var total = 0;
        foreach (var term in Terms(formula!))
        {
            total += term switch
            {
                "classLevel" => classLevel,
                "halfClassLevel" => classLevel / 2,
                "thirdClassLevel" => classLevel / 3,
                _ when term.StartsWith("mod:", StringComparison.Ordinal) => abilityModifier(term[4..]),
                _ => int.Parse(term, NumberStyles.None, CultureInfo.InvariantCulture),
            };
        }

        return Math.Max(1, total);
    }

    private static IEnumerable<string> Terms(string formula) =>
        formula.Split('+', StringSplitOptions.TrimEntries);

    private static bool IsTerm(string term) =>
        term is "classLevel" or "halfClassLevel" or "thirdClassLevel"
        || (term.StartsWith("mod:", StringComparison.Ordinal) && Abilities.IsValid(term[4..]))
        || (term.Length is >= 1 and <= 2 && int.TryParse(term, NumberStyles.None, CultureInfo.InvariantCulture, out _));
}
