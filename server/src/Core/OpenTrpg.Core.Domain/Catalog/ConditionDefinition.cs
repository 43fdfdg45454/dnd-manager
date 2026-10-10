namespace OpenTrpg.Core.Domain.Catalog;

public sealed class ConditionDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<string> Description { get; init; } = [];
}
