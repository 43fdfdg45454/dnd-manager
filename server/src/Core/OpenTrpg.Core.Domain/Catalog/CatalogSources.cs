namespace OpenTrpg.Core.Domain.Catalog;

/// <summary>
/// Origin of a catalog definition (the <c>Source</c> column): the base content of a game system (its own
/// id, for example the SRD of D&amp;D 5e), a campaign's homebrew (items only) or the id of a content pack
/// imported by the administrator of the instance.
/// </summary>
public static class CatalogSources
{
    public const string Homebrew = "homebrew";

    public const int MaxLength = 60;

    /// <summary>Prefix of the <see cref="CatalogImport.Ruleset"/> of content pack imports.</summary>
    public const string PackRulesetPrefix = "pack:";

    /// <summary><see cref="CatalogImport.Ruleset"/> of the content pack <paramref name="packId"/>.</summary>
    public static string PackRuleset(string packId) => PackRulesetPrefix + packId;

    /// <summary>
    /// True for the ids reserved by the core, which content packs cannot use. Game systems reserve the
    /// ids of their base content on top of these.
    /// </summary>
    public static bool IsReserved(string id) => id is Homebrew;
}
