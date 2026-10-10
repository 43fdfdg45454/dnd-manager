using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>Features granted by a subclass at one level. <see cref="Index"/> is the dataset slug, e.g. "berserker-3".</summary>
public sealed class SubclassLevel
{
    public required string Index { get; init; }

    public required string SubclassIndex { get; init; }

    public int Level { get; init; }

    public IReadOnlyList<string> FeatureIndexes { get; init; } = [];

    /// <summary>
    /// Proficiencies and always-prepared spells the subclass grants from this level (content packs, format 2),
    /// as in <see cref="OptionDefinition.GrantsJson"/>; null when it grants nothing.
    /// </summary>
    public string? GrantsJson { get; init; }

    public OptionGrants Grants => LevelChoiceJson.ParseGrants(GrantsJson);

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = Dnd5eCatalogSources.Srd;
}
