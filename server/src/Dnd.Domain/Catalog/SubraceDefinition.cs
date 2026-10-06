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

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = CatalogSources.Srd;
}
