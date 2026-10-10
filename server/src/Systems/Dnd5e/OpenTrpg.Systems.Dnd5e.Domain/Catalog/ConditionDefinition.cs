using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

public sealed class ConditionDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<string> Description { get; init; } = [];

    /// <summary>"srd" or the id of the content pack that defines the condition.</summary>
    public string Source { get; init; } = Dnd5eCatalogSources.Srd;
}
