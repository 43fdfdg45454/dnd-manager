namespace Dnd.Domain.Catalog;

public sealed class SubraceDefinition
{
    public required string Index { get; init; }

    public required string RaceIndex { get; init; }

    public required string Name { get; init; }

    public string Description { get; init; } = string.Empty;

    /// <summary>JSON array of <see cref="AbilityBonus"/>.</summary>
    public string AbilityBonusesJson { get; init; } = "[]";

    public IReadOnlyList<string> TraitIndexes { get; init; } = [];

    /// <summary>Normalized <see cref="Catalog.RaceChoices"/> (decisions asked at creation), or null when there are none.</summary>
    public string? ChoicesJson { get; init; }

    public RaceChoices Choices => RaceChoices.Parse(ChoicesJson);

    /// <summary>Damage types the race always resists ("poison" for dwarves).</summary>
    public IReadOnlyList<string> Resistances { get; init; } = [];

    /// <summary>Walking speed in feet that replaces the race's (a fast subrace), or null to keep it.</summary>
    public int? Speed { get; init; }

    /// <summary>Fixed proficiencies and spells of the subrace (<see cref="OptionGrants"/> JSON), or null.</summary>
    public string? GrantsJson { get; init; }

    public OptionGrants Grants => LevelChoiceJson.ParseGrants(GrantsJson);

    /// <summary><see cref="HeightWeightTable"/> JSON (random height and weight), or null when there is none.</summary>
    public string? HeightWeightJson { get; init; }

    public HeightWeightTable? HeightWeight => HeightWeightTable.Parse(HeightWeightJson);

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = CatalogSources.Srd;
}
