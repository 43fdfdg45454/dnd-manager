using Dnd.Domain.Catalog;
using Dnd.Domain.Items;

namespace Dnd.Domain.Characters;

/// <summary>A modifier of a chosen option, labelled for the breakdowns ("Defense (nivel 1)").</summary>
public sealed record FeatureModifier(string Label, ItemModifierKind Kind, string? Target, int Value, string? Condition);

/// <summary>An ability increase of an Ability Score Improvement or a feat ("Mejora de característica (nivel 4)").</summary>
/// <param name="Source">Breakdown source (<see cref="BreakdownSources.Feature"/>; race or background for origin feats).</param>
public sealed record AbilityIncreaseEffect(string Label, string Ability, int Amount, string Source = BreakdownSources.Feature);

/// <summary>
/// A limited-use resource granted by a chosen option or a subclass feature, tied to the class of the choice (null: the
/// character level). <see cref="Label"/> names its origin in the breakdown ("Ventaja táctica (nivel 3)").
/// </summary>
public sealed record ChoiceResourceEffect(string? ClassIndex, OptionResource Resource)
{
    public string? Label { get; init; }
}

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

    /// <summary>
    /// Ability bonuses chosen for the race or subrace (half-elf +1 to two abilities): breakdown source
    /// <see cref="BreakdownSources.Race"/>/<see cref="BreakdownSources.Subrace"/>, applied with the racial bonuses.
    /// </summary>
    public IReadOnlyList<AbilityIncreaseEffect> OriginAbilityBonuses { get; init; } = [];

    /// <summary>Label of an origin effect: "Grappler (raza)", "Grappler (trasfondo)".</summary>
    public static string OriginLabel(string name, string key) =>
        $"{name} ({(OriginChoiceKeys.IsBackground(key) ? "trasfondo" : OriginChoiceKeys.IsSubrace(key) ? "subraza" : "raza")})";

    /// <summary>Breakdown source of an origin key: race, subrace or background.</summary>
    public static string OriginSource(string key) =>
        OriginChoiceKeys.IsBackground(key) ? BreakdownSources.Background : OriginChoiceKeys.IsSubrace(key) ? BreakdownSources.Subrace : BreakdownSources.Race;

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

        var originBonuses = new List<AbilityIncreaseEffect>();
        var choices = character.Choices.Where(c => c.Key != CharacterChoice.HitPointsKey).ToList();

        void AddOption(OptionDefinition option, string? classIndex, string label, string? chosenAbility, string source)
        {
            modifiers.AddRange(option.Modifiers.Select(m => new FeatureModifier(label, m.Kind, m.Target, m.Value, m.Condition)));
            if (option.AbilityIncrease is { } increase
                && (chosenAbility ?? (increase.From.Count == 1 ? increase.From[0] : null)) is { } ability
                && Abilities.IsValid(ability))
            {
                increases.Add(new AbilityIncreaseEffect(label, ability, increase.Amount, source));
            }

            if (option.Resource is { } resource)
            {
                resources.Add(new ChoiceResourceEffect(classIndex, resource) { Label = label });
            }
        }

        // A feat swapped out later (forced replacement of an invalid feat) no longer counts.
        bool ReplacedLater(CharacterChoice choice, string index) =>
            choices.Any(c => c != choice && c.ClassIndex == choice.ClassIndex && c.Key == choice.Key
                && (c.Level > choice.Level || (c.Level == choice.Level && c.CreatedAt > choice.CreatedAt))
                && c.Selection.Replaced.Any(r => r.Index == index));

        foreach (var choice in choices.OrderBy(c => c.CreatedAt))
        {
            var selection = choice.Selection;
            foreach (var (ability, amount) in selection.Asi ?? new Dictionary<string, int>())
            {
                if (!Abilities.IsValid(ability) || amount <= 0)
                {
                    continue;
                }

                if (choice.IsOrigin)
                {
                    var source = OriginSource(choice.Key);
                    var origin = source == BreakdownSources.Subrace ? BreakdownLabels.Subrace : source == BreakdownSources.Background ? BreakdownLabels.Background : BreakdownLabels.Race;
                    originBonuses.Add(new AbilityIncreaseEffect($"{origin} ({BreakdownLabels.Chosen})", ability, amount, source));
                }
                else
                {
                    increases.Add(new AbilityIncreaseEffect(Label(AbilityScoreImprovement, choice.Level), ability, amount));
                }
            }

            if (selection.Feat is { } feat && !ReplacedLater(choice, feat.Index) && findOption(feat.Index) is { } featOption)
            {
                var label = choice.IsOrigin ? OriginLabel(featOption.Name, choice.Key) : Label(featOption.Name, choice.Level);
                AddOption(featOption, choice.ClassIndex, label, selection.Ability, choice.IsOrigin ? OriginSource(choice.Key) : BreakdownSources.Feature);
            }
        }

        foreach (var pick in character.ActivePicks().Where(p => p.SetId is not null && p.Kind is not (nameof(LevelChoiceKind.AsiOrFeat) or OriginChoiceKeys.FeatKind)))
        {
            if (findOption(pick.Item.Index) is { } option)
            {
                AddOption(option, pick.ClassIndex, Label(option.Name, pick.Level), null, BreakdownSources.Feature);
            }
        }

        return new ChoiceEffects(modifiers, increases, resources) { OriginAbilityBonuses = originBonuses };
    }

    /// <summary>
    /// Resources of the subclass features the character has reached: features of one of its subclasses whose level is
    /// not above its level in that class.
    /// </summary>
    public static IReadOnlyList<ChoiceResourceEffect> FeatureResources(Character character, IEnumerable<FeatureDefinition> features)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(features);
        return features
            .Where(f => f.SubclassIndex is not null)
            .Select(f => (Feature: f, Owner: character.Classes.FirstOrDefault(c => c.ClassIndex == f.ClassIndex && c.SubclassIndex == f.SubclassIndex)))
            .Where(f => f.Owner is not null && f.Feature.Level <= f.Owner.Level && f.Feature.Resource is not null)
            .OrderBy(f => f.Feature.Level)
            .ThenBy(f => f.Feature.Index, StringComparer.Ordinal)
            .Select(f => new ChoiceResourceEffect(f.Owner!.ClassIndex, f.Feature.Resource!) { Label = Label(f.Feature.Name, f.Feature.Level) })
            .ToList();
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
