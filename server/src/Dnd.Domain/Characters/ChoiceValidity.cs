using Dnd.Domain.Catalog;

namespace Dnd.Domain.Characters;

/// <summary>
/// An option or feat the character has whose prerequisites no longer hold (an invocation whose pact boon or cantrip is
/// gone, a feat whose minimum ability score is no longer met...). It must be replaced (phase 19).
/// </summary>
/// <param name="ClassIndex">Class of the choice that picked it (null for an origin feat).</param>
/// <param name="Key">Key of that choice ("eldritch-invocations", "asi", "race.feat").</param>
/// <param name="Kind">Kind of that choice (a <c>LevelChoiceKind</c> name, or "Feat" for origin feats).</param>
/// <param name="SetId">Option set of the pick (<c>feats</c> for feats).</param>
/// <param name="Reason">Spanish explanation of the prerequisites that fail.</param>
public sealed record InvalidChoice(string? ClassIndex, string Key, int Level, string Kind, string SetId, ChoiceItem Item, string Reason)
{
    public bool IsFeat => SetId == OptionSets.Feats;
}

/// <summary>Checks the prerequisites of the options and feats a character has (<see cref="Character.ActivePicks"/>).</summary>
public static class ChoiceValidity
{
    /// <summary>The picks whose prerequisites fail, using the calculated sheet and the catalog options (missing ones are skipped).</summary>
    public static IReadOnlyList<InvalidChoice> Find(Character character, CharacterSheet sheet, Func<string, OptionDefinition?> findOption)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(findOption);

        var picks = character.ActivePicks();
        var result = new List<InvalidChoice>();
        foreach (var pick in picks.Where(p => p.SetId is not null))
        {
            if (findOption(pick.Item.Index) is not { } option)
            {
                continue;
            }

            var ownLabel = pick.ClassIndex is null ? ChoiceEffects.OriginLabel(option.Name, pick.Key) : ChoiceEffects.Label(option.Name, pick.Level);
            var reason = WhyNot(option, character, sheet, pick.ClassIndex, picks.Select(p => p.Item.Index).ToHashSet(StringComparer.Ordinal), findOption, ownLabel);
            if (reason is not null)
            {
                result.Add(new InvalidChoice(pick.ClassIndex, pick.Key, pick.Level, pick.Kind, pick.SetId!, pick.Item, reason));
            }
        }

        return result;
    }

    /// <summary>
    /// Why the character does not meet the prerequisites of <paramref name="option"/> (Spanish), or null when it does.
    /// The minimum level is checked against the level in <paramref name="classIndex"/> (the total level when null);
    /// the ability scores exclude what the option itself adds (<paramref name="ownLabel"/> in the breakdowns).
    /// </summary>
    public static string? WhyNot(
        OptionDefinition option,
        Character character,
        CharacterSheet sheet,
        string? classIndex,
        IReadOnlySet<string> picks,
        Func<string, OptionDefinition?> findOption,
        string? ownLabel = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(picks);
        ArgumentNullException.ThrowIfNull(findOption);

        var prerequisites = option.Prerequisites;
        var reasons = new List<string>();
        var level = classIndex is null ? character.TotalLevel : character.Classes.FirstOrDefault(c => c.ClassIndex == classIndex)?.Level ?? 0;
        if (prerequisites.MinLevel is { } minLevel && level < minLevel)
        {
            reasons.Add(classIndex is null ? $"Requiere nivel {minLevel}." : $"Requiere nivel {minLevel} de {BreakdownLabels.Class(classIndex)}.");
        }

        if (prerequisites.PactBoon is { } pactBoon && !picks.Contains(pactBoon))
        {
            reasons.Add($"Requiere {findOption(pactBoon)?.Name ?? pactBoon}.");
        }

        if (prerequisites.Cantrip is { } cantrip && !character.Spells.Any(s => s.SpellIndex == cantrip))
        {
            reasons.Add($"Requiere el truco {cantrip}.");
        }

        foreach (var (ability, minimum) in prerequisites.Abilities)
        {
            if (!Abilities.IsValid(ability))
            {
                continue;
            }

            var own = ownLabel is not null && sheet.Breakdowns.TryGetValue(OverrideFields.Ability(ability), out var breakdown)
                ? breakdown.Parts.Where(p => p.Label == ownLabel).Sum(p => p.Value)
                : 0;
            if (sheet.Abilities[ability].Score - own < minimum)
            {
                reasons.Add($"Requiere {BreakdownLabels.Ability(ability)} {minimum}.");
            }
        }

        return reasons.Count == 0 ? null : string.Join(" ", reasons);
    }
}
