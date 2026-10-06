namespace Dnd.Domain.Catalog;

/// <summary>
/// Origin of a catalog definition (the <c>Source</c> column): the SRD, a campaign's homebrew (items
/// only) or the id of a content pack imported by the administrator of the instance.
/// </summary>
public static class CatalogSources
{
    public const string Srd = "srd";

    public const string Homebrew = "homebrew";

    public const int MaxLength = 60;

    /// <summary>Prefix of the <see cref="CatalogImport.Ruleset"/> of content pack imports.</summary>
    public const string PackRulesetPrefix = "pack:";

    /// <summary><see cref="CatalogImport.Ruleset"/> of the content pack <paramref name="packId"/>.</summary>
    public static string PackRuleset(string packId) => PackRulesetPrefix + packId;

    /// <summary>True for the ids reserved by the application, which content packs cannot use.</summary>
    public static bool IsReserved(string id) => id is Srd or Homebrew;
}
