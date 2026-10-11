using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

public sealed class TraitDefinition
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<string> Description { get; init; } = [];

    public IReadOnlyList<string> RaceIndexes { get; init; } = [];

    public IReadOnlyList<string> SubraceIndexes { get; init; } = [];

    /// <summary>
    /// Limited-use resource the trait gives to the characters of its races and subraces (content packs, as in
    /// <see cref="OptionDefinition.ResourceJson"/>; <c>classLevel</c> is the character level); null when it gives none.
    /// </summary>
    public string? ResourceJson { get; init; }

    public OptionResource? Resource => LevelChoiceJson.ParseResource(ResourceJson);

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = Dnd5eCatalogSources.Srd;
}
