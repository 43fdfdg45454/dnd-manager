namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// A spell tagged by a content pack (<c>spells[].tags</c>, or <c>reference.spellTags[].spells</c> for spells of the SRD or
/// of another pack). The tag is an entry of the <see cref="ReferenceEntry.SpellTags"/> vocabulary; the row belongs to the
/// pack that tags the spell and goes away with it.
/// </summary>
public sealed class SpellTag
{
    /// <summary>Most tags a spell of a pack can list.</summary>
    public const int MaxTagsPerSpell = 20;

    public required string Tag { get; init; }

    public required string SpellIndex { get; init; }

    /// <summary>The pack that tags the spell.</summary>
    public required string Source { get; init; }
}
