namespace Dnd.Domain.Catalog;

public sealed class TraitDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<string> Description { get; init; } = [];

    public IReadOnlyList<string> RaceIndexes { get; init; } = [];

    public IReadOnlyList<string> SubraceIndexes { get; init; } = [];
}
