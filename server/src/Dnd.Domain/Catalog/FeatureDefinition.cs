namespace Dnd.Domain.Catalog;

public sealed class FeatureDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    public required string ClassIndex { get; init; }

    public string? SubclassIndex { get; init; }

    public int Level { get; init; }

    public IReadOnlyList<string> Description { get; init; } = [];

    /// <summary>
    /// Limited-use resource the feature grants from its level to the characters with its subclass (content packs, as in
    /// <see cref="OptionDefinition.ResourceJson"/>); null when it grants none.
    /// </summary>
    public string? ResourceJson { get; init; }

    public OptionResource? Resource => LevelChoiceJson.ParseResource(ResourceJson);

    /// <summary>
    /// Animal companion the feature grants from its level to the characters with its subclass (content packs,
    /// <see cref="CompanionRule.ToJson"/>); null when it grants none.
    /// </summary>
    public string? CompanionJson { get; init; }

    public CompanionRule? Companion => CompanionRule.Parse(CompanionJson);

    /// <summary>
    /// Numeric modifiers the feature applies to the sheet from its level to the characters with its subclass (content
    /// packs, same JSON as <see cref="OptionDefinition.ModifiersJson"/>); null when it has none.
    /// </summary>
    public string? ModifiersJson { get; init; }

    public IReadOnlyList<ChoiceModifier> Modifiers => LevelChoiceJson.ParseModifiers(ModifiersJson);

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = CatalogSources.Srd;
}
