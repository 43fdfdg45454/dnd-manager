namespace Dnd.Domain.Catalog;

public sealed class FeatureDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    public required string ClassIndex { get; init; }

    public string? SubclassIndex { get; init; }

    public int Level { get; init; }

    public IReadOnlyList<string> Description { get; init; } = [];

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = CatalogSources.Srd;
}
