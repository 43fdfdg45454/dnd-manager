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
}
