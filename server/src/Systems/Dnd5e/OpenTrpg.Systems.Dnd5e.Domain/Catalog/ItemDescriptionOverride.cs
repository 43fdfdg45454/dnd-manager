namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// The description a content pack (<c>itemOverrides</c>) gives to an item template of the SRD or of another pack. While
/// the pack is installed the item's <c>Description</c> is this one (the most recently imported override wins when
/// several packs override the same item); <see cref="OriginalDescription"/> keeps the description the item had before
/// any override, to restore it when the last override goes away.
/// </summary>
public sealed class ItemDescriptionOverride
{
    /// <summary>Id of the content pack that defines the override.</summary>
    public required string Source { get; init; }

    /// <summary>Index of the overridden item template (SRD or content pack).</summary>
    public required string ItemIndex { get; init; }

    /// <summary>The paragraphs the item shows while the override is the active one.</summary>
    public required IReadOnlyList<string> Description { get; init; }

    /// <summary>The paragraphs the item had before any pack overrode it.</summary>
    public IReadOnlyList<string> OriginalDescription { get; set; } = [];

    public DateTimeOffset ImportedAt { get; set; }
}
