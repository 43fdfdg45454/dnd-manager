namespace OpenTrpg.Core.Domain.Catalog;

public sealed class SkillDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    /// <summary>Ability index ("str", "dex", ...).</summary>
    public required string AbilityIndex { get; init; }

    public IReadOnlyList<string> Description { get; init; } = [];
}
