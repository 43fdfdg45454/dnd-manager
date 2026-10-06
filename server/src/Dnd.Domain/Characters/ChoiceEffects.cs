using Dnd.Domain.Catalog;
using Dnd.Domain.Items;

namespace Dnd.Domain.Characters;

/// <summary>A modifier of a chosen option, labelled for the breakdowns ("Defense (nivel 1)").</summary>
public sealed record FeatureModifier(string Label, ItemModifierKind Kind, string? Target, int Value, string? Condition);

/// <summary>An ability increase of an Ability Score Improvement or a feat ("Mejora de característica (nivel 4)").</summary>
public sealed record AbilityIncreaseEffect(string Label, string Ability, int Amount);

/// <summary>A limited-use resource granted by a chosen option, tied to the class of the choice.</summary>
public sealed record ChoiceResourceEffect(string ClassIndex, OptionResource Resource);

/// <summary>
/// What the level choices of a character change in its sheet: the modifiers of the chosen options and feats,
/// the ability increases (Ability Score Improvements and feats) and the resources of the options. Built from
/// <see cref="Character.Choices"/> and the catalog options (<see cref="Build"/>); input of <see cref="SheetCalculator"/>.
/// </summary>
public sealed record ChoiceEffects(
    IReadOnlyList<FeatureModifier> Modifiers,
    IReadOnlyList<AbilityIncreaseEffect> AbilityIncreases,
    IReadOnlyList<ChoiceResourceEffect> Resources)
{
    public const string AbilityScoreImprovement = "Mejora de característica";

    public static ChoiceEffects None { get; } = new([], [], []);

    /// <summary>"Defense (nivel 1)".</summary>
    public static string Label(string name, int level) => $"{name} (nivel {level})";

    /// <summary>
    /// Effects of the character's current picks (<see cref="Character.ActivePicks"/>) and feats, looking up the
    /// options with <paramref name="findOption"/> (missing ones are skipped).
    /// </summary>
    public static ChoiceEffects Build(Character character, Func<string, OptionDefinition?> findOption)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(findOption);

        var modifiers = new List<FeatureModifier>();
        var increases = new List<AbilityIncreaseEffect>();
        var resources = new List<ChoiceResourceEffect>();

        void AddOption(OptionDefinition option, string classIndex, int level, string? chosenAbility)
        {
            var label = Label(option.Name, level);
            modifiers.AddRange(option.Modifiers.Select(m => new FeatureModifier(label, m.Kind, m.Target, m.Value, m.Condition)));
            if (option.AbilityIncrease is { } increase
                && (chosenAbility ?? (increase.From.Count == 1 ? increase.From[0] : null)) is { } ability
                && Abilities.IsValid(ability))
            {
                increases.Add(new AbilityIncreaseEffect(label, ability, increase.Amount));
            }

            if (option.Resource is { } resource)
            {
                resources.Add(new ChoiceResourceEffect(classIndex, resource));
            }
        }

        foreach (var choice in character.Choices.Where(c => c.Key != CharacterChoice.HitPointsKey).OrderBy(c => c.CreatedAt))
        {
            var selection = choice.Selection;
            foreach (var (ability, amount) in selection.Asi ?? new Dictionary<string, int>())
            {
                if (Abilities.IsValid(ability) && amount > 0)
                {
                    increases.Add(new AbilityIncreaseEffect(Label(AbilityScoreImprovement, choice.Level), ability, amount));
                }
            }

            if (selection.Feat is { } feat && findOption(feat.Index) is { } featOption)
            {
                AddOption(featOption, choice.ClassIndex, choice.Level, selection.Ability);
            }
        }

        foreach (var pick in character.ActivePicks().Where(p => p.SetId is not null && p.Kind != nameof(LevelChoiceKind.AsiOrFeat)))
        {
            if (findOption(pick.Item.Index) is { } option)
            {
                AddOption(option, pick.ClassIndex, pick.Level, null);
            }
        }

        return new ChoiceEffects(modifiers, increases, resources);
    }

    /// <summary>Option indexes the effects of <paramref name="character"/> may need (picks of option sets and feats).</summary>
    public static IEnumerable<string> OptionIndexes(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return character.ActivePicks()
            .Where(p => p.SetId is not null)
            .Select(p => p.Item.Index)
            .Concat(character.Choices.Select(c => c.Selection.Feat?.Index).OfType<string>());
    }
}
