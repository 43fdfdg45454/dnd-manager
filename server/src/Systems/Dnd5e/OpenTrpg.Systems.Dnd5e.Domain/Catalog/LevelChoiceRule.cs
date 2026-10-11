using System.Text.Json;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

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

    /// <summary>New saving throw proficiencies (abilities the character is not proficient with).</summary>
    SavingThrow,

    /// <summary>Proficiency with specific weapons of the catalog (narrowed by the weapon filter or <c>from</c>).</summary>
    Weapon,

    /// <summary>Resistance to a damage type (of <c>from</c>, or any): shown in the defenses of the sheet.</summary>
    Resistance,
}

/// <summary>
/// A choice an option or a feat carries (content packs): made when the option is taken (<c>choices</c>, no level) or
/// at later levels of the class of the pick (<c>levelChoices</c>, with <see cref="Level"/>). Turned into a
/// <see cref="LevelChoiceRule"/> of the class with the key <see cref="KeyFor"/>.
/// </summary>
public sealed record OptionChoiceRule(
    int? Level,
    string Key,
    string Name,
    LevelChoiceKind Kind,
    string? SetId,
    int Choose,
    string? FromJson,
    string? FilterJson,
    string Note,
    bool Replaces,
    bool Cumulative,
    string? AlsoFromJson)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Key of the choice in the character: "&lt;option index&gt;/&lt;key&gt;".</summary>
    public static string KeyFor(string optionIndex, string key) => $"{optionIndex}/{key}";

    /// <summary>The rule of the class for a character with the option, at a class level.</summary>
    public LevelChoiceRule ToRule(string optionIndex, string classIndex, string? subclassIndex, int level) => new()
    {
        Id = $"option/{optionIndex}/{level}/{Key}",
        ClassIndex = classIndex,
        SubclassIndex = subclassIndex,
        Level = level,
        Key = KeyFor(optionIndex, Key),
        Name = Name,
        Kind = Kind,
        SetId = SetId,
        Choose = Choose,
        FromJson = FromJson,
        FilterJson = FilterJson,
        Replaces = Replaces,
        Cumulative = Cumulative,
        Note = Note,
        AlsoFromJson = AlsoFromJson,
    };

    public static string Serialize(IEnumerable<OptionChoiceRule> rules) => JsonSerializer.Serialize(rules.ToList(), JsonOptions);

    public static IReadOnlyList<OptionChoiceRule> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<OptionChoiceRule>>(json, JsonOptions)?
                .Where(r => r is not null && !string.IsNullOrEmpty(r.Key))
                .Select(r => r with { Note = r.Note ?? string.Empty })
                .ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>
/// A choice a class (or one of its subclasses) offers at one class level: the subclass, an Ability Score
/// Improvement, fighting styles, invocations, new spells... Rows come from content packs: the SRD base
/// pack (generated from <c>server/src/Systems/Dnd5e/seed/srd/level-choices.json</c>) and the packs of the instance.
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

    /// <summary>
    /// Key of another choice of the same level that must be resolved before this one (content packs). Without it,
    /// skills (<see cref="LevelChoiceKind.Skill"/>, the subclass and options that grant skills) go before
    /// <see cref="LevelChoiceKind.Expertise"/>.
    /// </summary>
    public string? After { get; init; }

    /// <summary>
    /// JSON array of other option sets whose options are offered too (content packs): an ability improvement that may
    /// be swapped for an option of a class set, or an option choice that also draws from another set. Null = none.
    /// </summary>
    public string? AlsoFromJson { get; init; }

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = Dnd5eCatalogSources.Srd;

    public IReadOnlyList<string>? From => LevelChoiceJson.ParseStringList(FromJson);

    public IReadOnlyList<string> AlsoFrom => LevelChoiceJson.ParseStringList(AlsoFromJson) ?? [];

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
    public string Source { get; init; } = Dnd5eCatalogSources.Srd;
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

    /// <summary><c>{"resource":"ki","amount":2}</c>: uses of a resource spent each time the option is used; null when free.</summary>
    public string? CostJson { get; init; }

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = Dnd5eCatalogSources.Srd;

    public const int CategoryMaxLength = 40;

    /// <summary>Free label of a feat of a content pack ("starter"...), or null.</summary>
    public string? Category { get; init; }

    /// <summary>
    /// Only offered by origin choices at creation (race, subrace or background feats, or the starter feat): never by
    /// a level-up choice.
    /// </summary>
    public bool CreationOnly { get; init; }

    /// <summary>
    /// Starter feat: offered only by the optional "Dote inicial" choice at level 1 when the campaign enables it
    /// (implies <see cref="CreationOnly"/>).
    /// </summary>
    public bool Starter { get; init; }

    /// <summary>Repeatable feat: an Ability Score Improvement offers it again after it is taken (each pick counts on its own).</summary>
    public bool Repeatable { get; init; }

    /// <summary>
    /// What the option does to the inventory entry it is applied to (<see cref="Catalog.ItemEffects"/>); null for options that
    /// are not item enhancements.
    /// </summary>
    public string? ItemEffectsJson { get; init; }

    public ItemEffects? ItemEffects => Catalog.ItemEffects.Parse(ItemEffectsJson);

    public OptionPrerequisites Prerequisites => LevelChoiceJson.ParsePrerequisites(PrerequisitesJson);

    public IReadOnlyList<ChoiceModifier> Modifiers => LevelChoiceJson.ParseModifiers(ModifiersJson);

    public AbilityIncrease? AbilityIncrease => LevelChoiceJson.ParseAbilityIncrease(AbilityIncreaseJson);

    public OptionGrants Grants => LevelChoiceJson.ParseGrants(GrantsJson);

    public OptionResource? Resource => LevelChoiceJson.ParseResource(ResourceJson);

    public OptionCost? Cost => LevelChoiceJson.ParseCost(CostJson);

    /// <summary>Choices made when the option is taken (<see cref="OptionChoiceRule"/> list as JSON); null = none.</summary>
    public string? ChoicesJson { get; init; }

    /// <summary>Choices the option adds at later levels of the class of the pick; null = none.</summary>
    public string? LevelChoicesJson { get; init; }

    public IReadOnlyList<OptionChoiceRule> Choices => OptionChoiceRule.ParseList(ChoicesJson);

    public IReadOnlyList<OptionChoiceRule> LevelChoices => OptionChoiceRule.ParseList(LevelChoicesJson);
}

/// <summary>Well-known option sets.</summary>
public static class OptionSets
{
    /// <summary>Feats offered by every <see cref="LevelChoiceKind.AsiOrFeat"/> choice.</summary>
    public const string Feats = "feats";

    public const string FightingStyles = "fighting-styles";

    public const string PactBoons = "pact-boons";
}
