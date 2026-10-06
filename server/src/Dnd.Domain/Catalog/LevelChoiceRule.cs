namespace Dnd.Domain.Catalog;

/// <summary>What a <see cref="LevelChoiceRule"/> asks the player to choose when gaining a class level.</summary>
public enum LevelChoiceKind
{
    /// <summary>The subclass of the class (its options are the catalog subclasses of the class).</summary>
    Subclass,

    /// <summary>Options of an <see cref="OptionSetDefinition"/> (fighting styles, invocations, metamagic...).</summary>
    OptionSet,

    /// <summary>Ability Score Improvement (+2 or +1/+1, maximum 20) or a feat of the <c>feats</c> set.</summary>
    AsiOrFeat,

    /// <summary>Skills (or thieves' tools) the character is proficient with; their proficiency bonus is doubled.</summary>
    Expertise,

    /// <summary>New skill proficiencies.</summary>
    Skill,

    /// <summary>New languages (free text unless <see cref="LevelChoiceRule.FromJson"/> lists them).</summary>
    Language,

    /// <summary>New tool proficiencies (free text unless <see cref="LevelChoiceRule.FromJson"/> lists them).</summary>
    Tool,

    /// <summary>New cantrips of the class list (increase of the "cantrips known" column).</summary>
    CantripsKnown,

    /// <summary>New known spells (increase of the "spells known" column), of a castable level.</summary>
    SpellsKnown,

    /// <summary>Wizard: spells copied into the spellbook (known, not prepared).</summary>
    SpellbookSpells,

    /// <summary>Anything else; validated by count only (it may still draw its options from a set or the spellbook).</summary>
    Custom,
}

/// <summary>
/// A choice a class (or one of its subclasses) offers at one class level: the subclass, an Ability Score
/// Improvement, fighting styles, invocations, new spells... Rows come from the SRD seed
/// (<c>server/seed/srd/level-choices.json</c>) and from content packs (format 2).
/// </summary>
public sealed class LevelChoiceRule
{
    public const int KeyMaxLength = 100;
    public const int NoteMaxLength = 2000;

    /// <summary>Primary key: <see cref="IdFor"/> of class, subclass, level and key.</summary>
    public required string Id { get; init; }

    public required string ClassIndex { get; init; }

    /// <summary>Subclass that gives the choice, or null for the base class.</summary>
    public string? SubclassIndex { get; init; }

    /// <summary>Class level (1-20) at which the choice is made.</summary>
    public int Level { get; init; }

    /// <summary>Stable identifier of the choice within the class ("subclass", "asi", "eldritch-invocations"...).</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public LevelChoiceKind Kind { get; init; }

    /// <summary>Option set of <see cref="LevelChoiceKind.OptionSet"/> (and of <see cref="LevelChoiceKind.Custom"/> choices drawn from a set).</summary>
    public string? SetId { get; init; }

    /// <summary>How many new picks this level gives (0: only replacements).</summary>
    public int Choose { get; init; }

    /// <summary>JSON array with the allowed subset of option indexes (or languages/tools), or null for the whole set.</summary>
    public string? FromJson { get; init; }

    /// <summary>JSON object that narrows the candidate spells (see <see cref="ChoiceFilter"/>), or null.</summary>
    public string? FilterJson { get; init; }

    /// <summary>From this level on, one known pick of this key may be swapped at each level of the class.</summary>
    public bool Replaces { get; init; }

    /// <summary>The picks add up to those of earlier levels (invocations, metamagic, favored enemies...).</summary>
    public bool Cumulative { get; init; }

    /// <summary>Clarification for the UI (Spanish).</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = CatalogSources.Srd;

    public IReadOnlyList<string>? From => LevelChoiceJson.ParseStringList(FromJson);

    public ChoiceFilter Filter => LevelChoiceJson.ParseFilter(FilterJson);

    /// <summary>"fighter/-/3/subclass", "ranger/hunter/3/hunters-prey".</summary>
    public static string IdFor(string classIndex, string? subclassIndex, int level, string key) =>
        $"{classIndex}/{subclassIndex ?? "-"}/{level}/{key}";
}

/// <summary>A named group of options a <see cref="LevelChoiceRule"/> draws from ("fighting-styles", "feats"...).</summary>
public sealed class OptionSetDefinition
{
    public required string SetId { get; init; }

    public required string Name { get; init; }

    /// <summary>"srd" or the id of the content pack that added it. Packs may add options to sets of other sources.</summary>
    public string Source { get; init; } = CatalogSources.Srd;
}

/// <summary>
/// One option of an <see cref="OptionSetDefinition"/>: a fighting style, an invocation, a feat... Its effects are
/// stored as JSON (see <see cref="LevelChoiceJson"/>): numeric modifiers on the sheet, an ability increase, granted
/// proficiencies and spells and a limited-use resource.
/// </summary>
public sealed class OptionDefinition
{
    public required string Index { get; init; }

    public required string SetId { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<string> Description { get; init; } = [];

    /// <summary>Literal prerequisite ("Prerequisite: 5th level, Pact of the Blade feature").</summary>
    public string? PrerequisitesText { get; init; }

    /// <summary><c>{"minLevel":5,"pactBoon":"pact-of-the-blade","cantrip":"eldritch-blast","abilities":{"str":13}}</c>.</summary>
    public string? PrerequisitesJson { get; init; }

    /// <summary><c>[{"kind":"ArmorClassBonus","target":null,"value":1,"condition":"wearingArmor"}]</c>.</summary>
    public string ModifiersJson { get; init; } = "[]";

    /// <summary><c>{"amount":1,"from":["str","dex"]}</c> (feats); null when the option raises no ability.</summary>
    public string? AbilityIncreaseJson { get; init; }

    /// <summary><c>{"skills":[...],"cantrips":[...],"spells":[{"index":"hold-person","minLevel":3}],...}</c>.</summary>
    public string? GrantsJson { get; init; }

    /// <summary><c>{"key":"dreadful-word","name":"Dreadful Word","max":1,"recharge":"LongRest"}</c>.</summary>
    public string? ResourceJson { get; init; }

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = CatalogSources.Srd;

    public OptionPrerequisites Prerequisites => LevelChoiceJson.ParsePrerequisites(PrerequisitesJson);

    public IReadOnlyList<ChoiceModifier> Modifiers => LevelChoiceJson.ParseModifiers(ModifiersJson);

    public AbilityIncrease? AbilityIncrease => LevelChoiceJson.ParseAbilityIncrease(AbilityIncreaseJson);

    public OptionGrants Grants => LevelChoiceJson.ParseGrants(GrantsJson);

    public OptionResource? Resource => LevelChoiceJson.ParseResource(ResourceJson);
}

/// <summary>Well-known option sets.</summary>
public static class OptionSets
{
    /// <summary>Feats offered by every <see cref="LevelChoiceKind.AsiOrFeat"/> choice.</summary>
    public const string Feats = "feats";

    public const string FightingStyles = "fighting-styles";

    public const string PactBoons = "pact-boons";
}
