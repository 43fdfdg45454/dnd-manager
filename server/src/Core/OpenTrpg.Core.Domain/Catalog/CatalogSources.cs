namespace OpenTrpg.Core.Domain.Catalog;

/// <summary>
/// Origin of a catalog definition (the <c>Source</c> column): the id of a <see cref="ContentPack"/> (the base pack of a
/// game system, for example the SRD of D&amp;D 5e, or a pack imported by the administrator) or a campaign's homebrew
/// (items only).
/// </summary>
public static class CatalogSources
{
    public const string Homebrew = "homebrew";

    public const int MaxLength = 60;

    /// <summary>
    /// True for the ids reserved by the core, which content packs cannot use. Game systems reserve the
    /// ids of their base content on top of these.
    /// </summary>
    public static bool IsReserved(string id) => id is Homebrew;
}
