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

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = CatalogSources.Srd;
}
