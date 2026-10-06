namespace Dnd.Domain.Catalog;

public sealed class SubclassDefinition
{
    public required string Index { get; init; }

    public required string ClassIndex { get; init; }

    public required string Name { get; init; }

    /// <summary>Name of the subclass choice, e.g. "Primal Path".</summary>
    public string Flavor { get; init; } = string.Empty;

    public IReadOnlyList<string> Description { get; init; } = [];

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = CatalogSources.Srd;
}
