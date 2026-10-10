namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// Multiclassing (SRD 5.1): ability score prerequisites of each class and the proficiencies gained when a
/// class is taken as a new class. To take a new class the character must meet the prerequisites of the new
/// class and of every class it already has.
/// </summary>
public static class MulticlassRules
{
    public const int MinimumScore = 13;

    /// <summary>Alternatives of required abilities per class: every group must be met by one of its abilities.</summary>
    private static readonly Dictionary<string, string[][]> Prerequisites = new(StringComparer.Ordinal)
    {
        ["barbarian"] = [[Abilities.Str]],
        ["bard"] = [[Abilities.Cha]],
        ["cleric"] = [[Abilities.Wis]],
        ["druid"] = [[Abilities.Wis]],
        ["fighter"] = [[Abilities.Str, Abilities.Dex]],
        ["monk"] = [[Abilities.Dex], [Abilities.Wis]],
        ["paladin"] = [[Abilities.Str], [Abilities.Cha]],
        ["ranger"] = [[Abilities.Dex], [Abilities.Wis]],
        ["rogue"] = [[Abilities.Dex]],
        ["sorcerer"] = [[Abilities.Cha]],
        ["warlock"] = [[Abilities.Cha]],
        ["wizard"] = [[Abilities.Int]],
    };

    /// <summary>Proficiencies gained when the class is taken as a new class (dataset indexes).</summary>
    private static readonly Dictionary<string, (ProficiencyType Type, string Key)[]> Proficiencies = new(StringComparer.Ordinal)
    {
        ["barbarian"] = [(ProficiencyType.Armor, "shields"), (ProficiencyType.Weapon, "simple-weapons"), (ProficiencyType.Weapon, "martial-weapons")],
        ["bard"] = [(ProficiencyType.Armor, "light-armor")],
        ["cleric"] = [(ProficiencyType.Armor, "light-armor"), (ProficiencyType.Armor, "medium-armor"), (ProficiencyType.Armor, "shields")],
        ["druid"] = [(ProficiencyType.Armor, "light-armor"), (ProficiencyType.Armor, "medium-armor"), (ProficiencyType.Armor, "shields")],
        ["fighter"] =
        [
            (ProficiencyType.Armor, "light-armor"), (ProficiencyType.Armor, "medium-armor"), (ProficiencyType.Armor, "shields"),
            (ProficiencyType.Weapon, "simple-weapons"), (ProficiencyType.Weapon, "martial-weapons"),
        ],
        ["monk"] = [(ProficiencyType.Weapon, "simple-weapons"), (ProficiencyType.Weapon, "shortswords")],
        ["paladin"] =
        [
            (ProficiencyType.Armor, "light-armor"), (ProficiencyType.Armor, "medium-armor"), (ProficiencyType.Armor, "shields"),
            (ProficiencyType.Weapon, "simple-weapons"), (ProficiencyType.Weapon, "martial-weapons"),
        ],
        ["ranger"] =
        [
            (ProficiencyType.Armor, "light-armor"), (ProficiencyType.Armor, "medium-armor"), (ProficiencyType.Armor, "shields"),
            (ProficiencyType.Weapon, "simple-weapons"), (ProficiencyType.Weapon, "martial-weapons"),
        ],
        ["rogue"] = [(ProficiencyType.Armor, "light-armor"), (ProficiencyType.Tool, "thieves-tools")],
        ["warlock"] = [(ProficiencyType.Armor, "light-armor"), (ProficiencyType.Weapon, "simple-weapons")],
    };

    /// <summary>
    /// Null when the class's prerequisites are met with the given scores; otherwise the missing requirement
    /// ("Inteligencia 13", "Fuerza 13 o Destreza 13"). Classes without known prerequisites (content) pass.
    /// </summary>
    public static string? Missing(string classIndex, Func<string, int> score)
    {
        ArgumentNullException.ThrowIfNull(score);
        if (!Prerequisites.TryGetValue(classIndex, out var groups))
        {
            return null;
        }

        var missing = groups
            .Where(group => !group.Any(ability => score(ability) >= MinimumScore))
            .Select(group => string.Join(" o ", group.Select(a => $"{BreakdownLabels.Ability(a)} {MinimumScore}")))
            .ToList();
        return missing.Count == 0 ? null : string.Join(" y ", missing);
    }

    /// <summary>
    /// Why the character cannot take <paramref name="newClassIndex"/> as a new class (Spanish), or null when it can:
    /// the prerequisites of the new class and of every current class must be met.
    /// </summary>
    public static string? WhyNot(string newClassIndex, IEnumerable<string> currentClassIndexes, Func<string, int> score)
    {
        ArgumentNullException.ThrowIfNull(currentClassIndexes);
        var reasons = new List<string>();
        if (Missing(newClassIndex, score) is { } missingNew)
        {
            reasons.Add($"{BreakdownLabels.Class(newClassIndex)} requiere {missingNew}.");
        }

        foreach (var current in currentClassIndexes.Where(c => c != newClassIndex).Distinct(StringComparer.Ordinal))
        {
            if (Missing(current, score) is { } missingCurrent)
            {
                reasons.Add($"Para salir de {BreakdownLabels.Class(current)} hace falta {missingCurrent}.");
            }
        }

        return reasons.Count == 0 ? null : string.Join(" ", reasons);
    }

    /// <summary>Proficiencies gained when <paramref name="classIndex"/> is taken as a new class.</summary>
    public static IReadOnlyList<(ProficiencyType Type, string Key)> ProficienciesFor(string classIndex) =>
        Proficiencies.TryGetValue(classIndex, out var list) ? list : [];

    /// <summary>
    /// Skills gained when the class is taken as a new class (PHB multiclassing): bard one skill of any kind, ranger
    /// and rogue one skill of their class list (<c>true</c> in <c>FromClassList</c>). Null for the other classes.
    /// </summary>
    public static (int Choose, bool FromClassList)? SkillsFor(string classIndex) => classIndex switch
    {
        "bard" => (1, false),
        "ranger" or "rogue" => (1, true),
        _ => null,
    };
}
