namespace Dnd.Domain.Catalog;

/// <summary>Features granted by a subclass at one level. <see cref="Index"/> is the dataset slug, e.g. "berserker-3".</summary>
public sealed class SubclassLevel
{
    public required string Index { get; init; }

    public required string SubclassIndex { get; init; }

    public int Level { get; init; }

    public IReadOnlyList<string> FeatureIndexes { get; init; } = [];
}
