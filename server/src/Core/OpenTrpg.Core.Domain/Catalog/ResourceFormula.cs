using System.Globalization;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Domain.Catalog;

/// <summary>
/// Formula of the maximum of a limited-use resource: terms separated by <c>+</c>, each an integer or
/// <c>[n*]symbol</c> with symbol <c>proficiencyBonus</c>, <c>classLevel</c>, <c>halfClassLevel</c> or
/// <c>mod:&lt;ability&gt;</c> (<c>"2*classLevel+mod:int"</c>, <c>"proficiencyBonus"</c>, <c>"3"</c>).
/// </summary>
public static class ResourceFormula
{
    public const string ProficiencyBonus = "proficiencyBonus";
    public const string ClassLevel = "classLevel";
    public const string HalfClassLevel = "halfClassLevel";
    public const string ModifierPrefix = "mod:";

    /// <summary>Largest factor of a term (<c>n*symbol</c>).</summary>
    public const int MaxFactor = 99;

    /// <summary>Most terms in a formula.</summary>
    public const int MaxTerms = 10;

    /// <summary>A term: <see cref="Factor"/> × <see cref="Symbol"/>, or the constant <see cref="Factor"/> when the symbol is null.</summary>
    public sealed record Term(int Factor, string? Symbol);

    /// <summary>The terms of <paramref name="formula"/>, or null when it does not follow the grammar.</summary>
    public static IReadOnlyList<Term>? Parse(string? formula)
    {
        if (string.IsNullOrWhiteSpace(formula))
        {
            return null;
        }

        var parts = formula.Split('+');
        if (parts.Length > MaxTerms)
        {
            return null;
        }

        var terms = new List<Term>();
        foreach (var part in parts)
        {
            var text = part.Trim();
            if (text.Length == 0)
            {
                return null;
            }

            if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var constant))
            {
                if (constant > CharacterResource.MaxUses)
                {
                    return null;
                }

                terms.Add(new Term(constant, null));
                continue;
            }

            var factor = 1;
            var star = text.IndexOf('*', StringComparison.Ordinal);
            if (star >= 0)
            {
                if (!int.TryParse(text[..star].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out factor) || factor is < 1 or > MaxFactor)
                {
                    return null;
                }

                text = text[(star + 1)..].Trim();
            }

            if (!IsSymbol(text))
            {
                return null;
            }

            terms.Add(new Term(factor, text));
        }

        return terms;
    }

    /// <summary>Whether <paramref name="text"/> is one of the symbols of the grammar.</summary>
    public static bool IsSymbol(string text) =>
        text is ProficiencyBonus or ClassLevel or HalfClassLevel
        || (text.StartsWith(ModifierPrefix, StringComparison.Ordinal) && Abilities.IsValid(text[ModifierPrefix.Length..]));

    /// <summary>Value of a symbol for a character.</summary>
    public static int SymbolValue(string symbol, int proficiencyBonus, int classLevel, Func<string, int> abilityModifier) => symbol switch
    {
        ProficiencyBonus => proficiencyBonus,
        ClassLevel => classLevel,
        HalfClassLevel => classLevel / 2,
        _ => abilityModifier(symbol[ModifierPrefix.Length..]),
    };

    /// <summary>Adds one breakdown part per term; constants are labelled <paramref name="constantLabel"/>.</summary>
    internal static void AddTerms(
        BreakdownBuilder builder,
        IReadOnlyList<Term> terms,
        int proficiencyBonus,
        int classLevel,
        Func<string, int> abilityModifier,
        string constantLabel)
    {
        foreach (var term in terms)
        {
            if (term.Symbol is not { } symbol)
            {
                builder.Add(BreakdownSources.Feature, constantLabel, term.Factor);
                continue;
            }

            var (source, label) = symbol switch
            {
                ProficiencyBonus => (BreakdownSources.Proficiency, BreakdownLabels.Proficiency),
                ClassLevel => (BreakdownSources.Class, BreakdownLabels.ClassLevel),
                HalfClassLevel => (BreakdownSources.Class, BreakdownLabels.HalfClassLevel),
                _ => (BreakdownSources.Ability, BreakdownLabels.Ability(symbol[ModifierPrefix.Length..])),
            };
            var value = SymbolValue(symbol, proficiencyBonus, classLevel, abilityModifier);
            builder.Add(source, term.Factor == 1 ? label : $"{term.Factor} × {label}", term.Factor * value);
        }
    }
}
