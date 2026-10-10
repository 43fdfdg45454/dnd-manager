using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>A modifier of a chosen option, labelled for the breakdowns ("Defense (nivel 1)").</summary>
public sealed record FeatureModifier(string Label, ItemModifierKind Kind, string? Target, int Value, string? Condition)
{
    /// <summary><see cref="ModifierFormula"/> of the value (null: <see cref="Value"/>); <see cref="SheetCalculator"/> resolves it.</summary>
    public string? Formula { get; init; }

    /// <summary>Class whose level <c>classLevel</c> counts in <see cref="Formula"/> (null: the character level).</summary>
    public string? ClassIndex { get; init; }

    /// <summary>How the value of a formula was obtained (one part per term); null for a constant.</summary>
    public IReadOnlyList<BreakdownPart>? Parts { get; init; }

    /// <summary><see cref="ItemModifierKind.ArmorClassFormula"/>: abilities added to the base.</summary>
    public IReadOnlyList<string> Abilities { get; init; } = [];

    /// <summary><see cref="ItemModifierKind.ArmorClassFormula"/>: whether the shield is added.</summary>
    public bool AllowShield { get; init; } = true;

    /// <summary>The breakdown parts of the value: <see cref="Parts"/>, or one part with the label and the value.</summary>
    public IReadOnlyList<BreakdownPart> ValueParts => Parts ?? [new BreakdownPart(BreakdownSources.Feature, Label, Value)];

    /// <summary>A modifier of the catalog labelled with its origin, tied to the class of its choice or feature.</summary>
    public static FeatureModifier From(string label, ChoiceModifier modifier, string? classIndex)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        return new FeatureModifier(label, modifier.Kind, modifier.Target, modifier.Value, modifier.Condition)
        {
            Formula = modifier.Formula,
            ClassIndex = classIndex,
            Abilities = modifier.Abilities,
            AllowShield = modifier.AllowShield,
        };
    }
}

/// <summary>
/// Grants of a chosen option or feat that change the sheet (defenses, size, natural weapons, lost saving throws),
/// labelled with their origin; <see cref="RemovedSavingThrows"/> are the abilities the player chose to lose.
/// </summary>
public sealed record GrantEffect(string Label, string Source, OptionGrants Grants)
{
    public IReadOnlyList<string> RemovedSavingThrows { get; init; } = [];
}

/// <summary>A damage resistance the player chose in a <c>Resistance</c> choice of an option or a feat.</summary>
public sealed record ChosenResistance(string Label, string DamageType);

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

    /// <summary>Sheet grants of the chosen options and feats and of the subclass levels reached.</summary>
    public IReadOnlyList<GrantEffect> Grants { get; init; } = [];

    /// <summary>Resistances chosen in <c>Resistance</c> choices.</summary>
    public IReadOnlyList<ChosenResistance> ChosenResistances { get; init; } = [];

    /// <summary>Label of an origin effect: "Grappler (raza)", "Grappler (trasfondo)", "Grappler (dote inicial)".</summary>
    public static string OriginLabel(string name, string key) =>
        $"{name} ({(key == OriginChoiceKeys.StarterFeat ? "dote inicial" : OriginChoiceKeys.IsBackground(key) ? "trasfondo" : OriginChoiceKeys.IsSubrace(key) ? "subraza" : "raza")})";

    /// <summary>Breakdown source of an origin key: race, subrace or background (a feature for the starter feat).</summary>
    public static string OriginSource(string key) =>
        key == OriginChoiceKeys.StarterFeat ? BreakdownSources.Feature
        : OriginChoiceKeys.IsBackground(key) ? BreakdownSources.Background : OriginChoiceKeys.IsSubrace(key) ? BreakdownSources.Subrace : BreakdownSources.Race;

    public static ChoiceEffects None { get; } = new([], [], []);

    /// <summary>"Defense (nivel 1)".</summary>
    public static string Label(string name, int level) => $"{name} (nivel {level})";

    /// <summary>
    /// Effects of the character's current picks (<see cref="Character.ActivePicks"/>) and feats, looking up the
    /// options with <paramref name="findOption"/> (missing ones are skipped).
    /// </summary>
    public static ChoiceEffects Build(Dnd5eCharacter character, Func<string, OptionDefinition?> findOption)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(findOption);

        var modifiers = new List<FeatureModifier>();
        var increases = new List<AbilityIncreaseEffect>();
        var resources = new List<ChoiceResourceEffect>();

        var originBonuses = new List<AbilityIncreaseEffect>();
        var choices = character.Choices.Where(c => c.Key != CharacterChoice.HitPointsKey).ToList();

        var grants = new List<GrantEffect>();
        void AddOption(OptionDefinition option, string? classIndex, string label, string? chosenAbility, string source, IReadOnlyList<string>? removedSaves = null)
        {
            modifiers.AddRange(option.Modifiers.Select(m => FeatureModifier.From(label, m, classIndex)));
            if (option.Grants is { HasSheetEffects: true } sheetGrants)
            {
                grants.Add(new GrantEffect(label, source, sheetGrants) { RemovedSavingThrows = removedSaves ?? [] });
            }

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
                AddOption(featOption, choice.ClassIndex, label, selection.Ability, choice.IsOrigin ? OriginSource(choice.Key) : BreakdownSources.Feature, selection.RemovedSavingThrows);
            }
        }

        foreach (var pick in character.ActivePicks().Where(p => p.SetId is not null && p.Kind is not (nameof(LevelChoiceKind.AsiOrFeat) or OriginChoiceKeys.FeatKind)))
        {
            if (findOption(pick.Item.Index) is { } option)
            {
                AddOption(option, pick.ClassIndex, Label(option.Name, pick.Level), null, BreakdownSources.Feature);
            }
        }

        // A choice inside an option or feat ("<option>/<key>") is labelled with the option ("Aguante (nivel 4)").
        string ResistanceLabel(ActivePick pick)
        {
            var slash = pick.Key.IndexOf('/', StringComparison.Ordinal);
            if (slash > 0 && findOption(pick.Key[..slash]) is { } owner)
            {
                return Label(owner.Name, pick.Level);
            }

            return choices.FirstOrDefault(c => c.ClassIndex == pick.ClassIndex && c.Key == pick.Key) is { } choice
                ? (choice.IsOrigin ? OriginLabel(choice.Selection.Name, choice.Key) : Label(choice.Selection.Name, pick.Level))
                : pick.Key;
        }

        var resistances = character.ActivePicks()
            .Where(p => p.Kind == nameof(LevelChoiceKind.Resistance))
            .Select(p => new ChosenResistance(ResistanceLabel(p), p.Item.Index))
            .ToList();

        return new ChoiceEffects(modifiers, increases, resources)
        {
            OriginAbilityBonuses = originBonuses,
            Grants = grants,
            ChosenResistances = resistances,
        };
    }

    /// <summary>
    /// Resources of the classes of the character defined by content packs (<see cref="ClassDefinition.Resources"/>),
    /// labelled with the class name; <paramref name="findClass"/> returns null for unknown classes.
    /// </summary>
    public static IReadOnlyList<ChoiceResourceEffect> ClassResources(Dnd5eCharacter character, Func<string, ClassDefinition?> findClass)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(findClass);
        return character.Classes
            .Select(c => findClass(c.ClassIndex))
            .OfType<ClassDefinition>()
            .SelectMany(d => d.Resources.Select(r => new ChoiceResourceEffect(d.Index, r) { Label = d.Name }))
            .ToList();
    }

    /// <summary>Whether a class (or subclass) feature belongs to a class entry of the character.</summary>
    private static bool Owns(CharacterClassLevel entry, FeatureDefinition feature) =>
        entry.ClassIndex == feature.ClassIndex && (feature.SubclassIndex is null || entry.SubclassIndex == feature.SubclassIndex);

    /// <summary>
    /// Resources of the class and subclass features the character has reached: features of one of its classes (or of
    /// its subclass in that class) whose level is not above its level in that class.
    /// </summary>
    public static IReadOnlyList<ChoiceResourceEffect> FeatureResources(Dnd5eCharacter character, IEnumerable<FeatureDefinition> features)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(features);
        return features
            .Select(f => (Feature: f, Owner: character.Classes.FirstOrDefault(c => Owns(c, f))))
            .Where(f => f.Owner is not null && f.Feature.Level <= f.Owner.Level && f.Feature.Resource is not null)
            .OrderBy(f => f.Feature.Level)
            .ThenBy(f => f.Feature.Index, StringComparer.Ordinal)
            .Select(f => new ChoiceResourceEffect(f.Owner!.ClassIndex, f.Feature.Resource!) { Label = Label(f.Feature.Name, f.Feature.Level) })
            .ToList();
    }

    /// <summary>
    /// Sheet modifiers of the subclass features the character has reached (same rule as <see cref="FeatureResources"/>),
    /// labelled with the feature and its level ("Pies ligeros (nivel 3)").
    /// </summary>
    public static IReadOnlyList<FeatureModifier> FeatureModifiers(Dnd5eCharacter character, IEnumerable<FeatureDefinition> features)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(features);
        return ReachedFeatures(character, features)
            .SelectMany(f => f.Modifiers.Select(m => FeatureModifier.From(Label(f.Name, f.Level), m, f.ClassIndex)))
            .ToList();
    }

    /// <summary>
    /// Sheet grants (defenses, size, natural weapons) of the subclass levels the character has reached, labelled with
    /// the subclass name and the level ("Círculo del ejemplo (nivel 6)").
    /// </summary>
    public static IReadOnlyList<GrantEffect> SubclassLevelGrants(Dnd5eCharacter character, IEnumerable<SubclassLevel> levels, Func<string, string?> subclassName)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(subclassName);
        return levels
            .Where(l => l.Grants.HasSheetEffects && character.Classes.Any(c => c.SubclassIndex == l.SubclassIndex && l.Level <= c.Level))
            .OrderBy(l => l.Level)
            .Select(l => new GrantEffect(Label(subclassName(l.SubclassIndex) ?? l.SubclassIndex, l.Level), BreakdownSources.Class, l.Grants))
            .ToList();
    }

    /// <summary>Class and subclass features of the character whose level is not above its level in that class.</summary>
    private static IEnumerable<FeatureDefinition> ReachedFeatures(Dnd5eCharacter character, IEnumerable<FeatureDefinition> features) =>
        features
            .Where(f => character.Classes.Any(c => Owns(c, f) && f.Level <= c.Level))
            .OrderBy(f => f.Level)
            .ThenBy(f => f.Index, StringComparer.Ordinal);

    /// <summary>Option indexes the effects of <paramref name="character"/> may need (picks of option sets and feats).</summary>
    public static IEnumerable<string> OptionIndexes(Dnd5eCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return character.ActivePicks()
            .Where(p => p.SetId is not null)
            .Select(p => p.Item.Index)
            .Concat(character.Choices.Select(c => c.Selection.Feat?.Index).OfType<string>());
    }
}
