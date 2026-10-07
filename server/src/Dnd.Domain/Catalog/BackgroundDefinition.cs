namespace Dnd.Domain.Catalog;

public sealed class BackgroundDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    public string FeatureName { get; init; } = string.Empty;

    public IReadOnlyList<string> FeatureDescription { get; init; } = [];

    /// <summary>Skill names, e.g. "Insight".</summary>
    public IReadOnlyList<string> SkillProficiencies { get; init; } = [];

    public string StartingEquipmentText { get; init; } = string.Empty;

    /// <summary>Structured starting equipment (see <see cref="Catalog.StartingEquipment"/>), or null when the background only has the text.</summary>
    public string? StartingEquipmentJson { get; init; }

    public StartingEquipment? StartingEquipment => Catalog.StartingEquipment.Parse(StartingEquipmentJson);

    /// <summary>Normalized <see cref="Catalog.RaceChoices"/> (decisions asked at creation), or null when there are none.</summary>
    public string? ChoicesJson { get; init; }

    public RaceChoices Choices => RaceChoices.Parse(ChoicesJson);

    /// <summary>Personality tables (<see cref="Catalog.BackgroundPersonality"/>) as JSON, or null when the background has none.</summary>
    public string? PersonalityJson { get; init; }

    public BackgroundPersonality? Personality => BackgroundPersonality.Parse(PersonalityJson);

    /// <summary>Optional tables (<see cref="BackgroundTable"/>) as a JSON list, or null when there are none.</summary>
    public string? OptionalTablesJson { get; init; }

    public IReadOnlyList<BackgroundTable> OptionalTables => BackgroundTable.ParseList(OptionalTablesJson);

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = CatalogSources.Srd;
}
