namespace Dnd.Domain.Catalog;

public sealed class FeatureDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    public required string ClassIndex { get; init; }

    public string? SubclassIndex { get; init; }

    public int Level { get; init; }

    public IReadOnlyList<string> Description { get; init; } = [];
}
