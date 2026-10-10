using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// Formula of the value of a modifier of an option, a feat or a class feature (<c>"value": "characterLevel"</c>): the
/// grammar of <see cref="ResourceFormula"/> (<c>proficiencyBonus</c>, <c>classLevel</c>, <c>halfClassLevel</c>,
/// <c>mod:&lt;ability&gt;</c>, constants, <c>n*symbol</c> and <c>+</c>) plus <c>characterLevel</c>,
/// <c>halfCharacterLevel</c> and, only in <see cref="OpenTrpg.Core.Domain.Items.ItemModifierKind.Speed"/>, <c>walk</c>
/// (the walking speed).
/// </summary>
public static class ModifierFormula
{
    public const string CharacterLevel = "characterLevel";
    public const string HalfCharacterLevel = "halfCharacterLevel";
    public const string WalkingSpeed = "walk";

    /// <summary>The terms, or null when <paramref name="formula"/> does not follow the grammar.</summary>
    public static IReadOnlyList<ResourceFormula.Term>? Parse(string? formula, bool allowWalk = false) =>
        ResourceFormula.Parse(formula, s => IsSymbol(s) || (allowWalk && s == WalkingSpeed));

    public static bool IsSymbol(string text) => ResourceFormula.IsSymbol(text) || text is CharacterLevel or HalfCharacterLevel;

    /// <summary>What the symbols of a formula are worth for a character.</summary>
    public sealed record Context(int ProficiencyBonus, int ClassLevel, int CharacterLevel, Func<string, int> AbilityModifier, int WalkingSpeed = 0);

    /// <summary>
    /// One breakdown part per term, labelled with the origin of the modifier (<paramref name="origin"/>, "Robusto (nivel
    /// 4)") and what the term counts: "Robusto (nivel 4): nivel de personaje". Null when the formula does not parse.
    /// </summary>
    public static IReadOnlyList<BreakdownPart>? Parts(string formula, string origin, Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Parse(formula, allowWalk: true) is not { } terms)
        {
            return null;
        }

        var parts = new List<BreakdownPart>();
        foreach (var term in terms)
        {
            if (term.Symbol is not { } symbol)
            {
                parts.Add(new BreakdownPart(BreakdownSources.Feature, origin, term.Factor));
                continue;
            }

            var (source, label, value) = symbol switch
            {
                CharacterLevel => (BreakdownSources.Feature, BreakdownLabels.CharacterLevel, context.CharacterLevel),
                HalfCharacterLevel => (BreakdownSources.Feature, BreakdownLabels.HalfCharacterLevel, context.CharacterLevel / 2),
                WalkingSpeed => (BreakdownSources.Feature, BreakdownLabels.WalkingSpeed, context.WalkingSpeed),
                ResourceFormula.ProficiencyBonus => (BreakdownSources.Feature, BreakdownLabels.Proficiency.ToLowerInvariant(), context.ProficiencyBonus),
                ResourceFormula.ClassLevel => (BreakdownSources.Feature, BreakdownLabels.ClassLevel.ToLowerInvariant(), context.ClassLevel),
                ResourceFormula.HalfClassLevel => (BreakdownSources.Feature, BreakdownLabels.HalfClassLevel.ToLowerInvariant(), context.ClassLevel / 2),
                _ => (BreakdownSources.Feature, BreakdownLabels.Ability(symbol[ResourceFormula.ModifierPrefix.Length..]), context.AbilityModifier(symbol[ResourceFormula.ModifierPrefix.Length..])),
            };
            var text = term.Factor == 1 ? label : $"{term.Factor} × {label}";
            parts.Add(new BreakdownPart(source, $"{origin}: {text}", term.Factor * value));
        }

        return parts;
    }
}
