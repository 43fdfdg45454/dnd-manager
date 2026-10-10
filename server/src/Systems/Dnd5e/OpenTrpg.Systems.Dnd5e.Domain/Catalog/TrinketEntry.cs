using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// One entry of the d100 trinket table rolled at character creation. The table is not part of the SRD, so it only
/// comes from content packs (<c>trinkets</c>); each entry points to an item template of the SRD or of a pack. When
/// several packs define the same <see cref="Roll"/>, the most recently imported one wins.
/// </summary>
public sealed class TrinketEntry
{
    public const int MinRoll = 1;
    public const int MaxRoll = 100;

    /// <summary>Id of the content pack that defines the entry.</summary>
    public required string Source { get; init; }

    /// <summary>Result of the d100 (1-100).</summary>
    public int Roll { get; init; }

    /// <summary>Index of the item template (SRD or content pack).</summary>
    public required string ItemIndex { get; init; }
}
