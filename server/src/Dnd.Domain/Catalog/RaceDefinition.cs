namespace Dnd.Domain.Catalog;

public sealed class RaceDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    public int Speed { get; init; }

    public string Size { get; init; } = string.Empty;

    /// <summary>JSON array of <see cref="AbilityBonus"/> (<c>[{"ability":"con","bonus":2}]</c>).</summary>
    public string AbilityBonusesJson { get; init; } = "[]";

    public IReadOnlyList<string> TraitIndexes { get; init; } = [];

    /// <summary>Language names, e.g. "Common".</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    public string Age { get; init; } = string.Empty;

    public string Alignment { get; init; } = string.Empty;

    public string SizeDescription { get; init; } = string.Empty;

    public IReadOnlyList<string> SubraceIndexes { get; init; } = [];

    /// <summary>Normalized <see cref="Catalog.RaceChoices"/> (decisions asked at creation), or null when there are none.</summary>
    public string? ChoicesJson { get; init; }

    public RaceChoices Choices => RaceChoices.Parse(ChoicesJson);

    /// <summary>Damage types the race always resists ("poison" for dwarves).</summary>
    public IReadOnlyList<string> Resistances { get; init; } = [];

    /// <summary>
    /// Fixed proficiencies and spells of the race (<see cref="OptionGrants"/> JSON, the <c>grants</c> of a content pack or
    /// the trait proficiencies of the SRD), or null.
    /// </summary>
    public string? GrantsJson { get; init; }

    public OptionGrants Grants => LevelChoiceJson.ParseGrants(GrantsJson);

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = CatalogSources.Srd;
}
