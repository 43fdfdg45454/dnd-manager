using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

public sealed class SubclassDefinition
{
    public required string Index { get; init; }

    public required string ClassIndex { get; init; }

    public required string Name { get; init; }

    /// <summary>Name of the subclass choice, e.g. "Primal Path".</summary>
    public string Flavor { get; init; } = string.Empty;

    public IReadOnlyList<string> Description { get; init; } = [];

    /// <summary>
    /// Spellcasting the subclass adds to a non-casting base class (content packs, see <see cref="SubclassSpellcasting"/>);
    /// null when it adds none.
    /// </summary>
    public string? SpellcastingJson { get; init; }

    public SubclassSpellcasting? Spellcasting => SubclassSpellcasting.Parse(SpellcastingJson);

    /// <summary>
    /// Spells the subclass adds to its class's spell list (content packs, see <see cref="ExpandedSpell"/>); null when
    /// it adds none.
    /// </summary>
    public string? ExpandedSpellListJson { get; init; }

    public IReadOnlyList<ExpandedSpell> ExpandedSpellList => ExpandedSpell.Parse(ExpandedSpellListJson);

    /// <summary>Indexes of the spells of <see cref="ExpandedSpellList"/>.</summary>
    public IReadOnlySet<string> ExpandedSpellIndexes => ExpandedSpellList.Select(s => s.Index).ToHashSet(StringComparer.Ordinal);

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = Dnd5eCatalogSources.Srd;
}
