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
}
