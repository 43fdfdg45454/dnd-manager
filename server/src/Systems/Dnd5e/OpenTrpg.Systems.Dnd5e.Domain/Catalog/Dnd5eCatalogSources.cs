using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>Catalog sources of the D&amp;D 5e system: its base content is the SRD 5.1 (see <see cref="CatalogSources"/>).</summary>
public static class Dnd5eCatalogSources
{
    /// <summary><c>Source</c> of the SRD definitions (the base content of the system).</summary>
    public const string Srd = "srd";

    /// <summary><see cref="CatalogImport.Ruleset"/> of the SRD import.</summary>
    public const string SrdRuleset = "srd-5.1";

    /// <summary>True for the ids content packs cannot use: the SRD and the ids reserved by the core.</summary>
    public static bool IsReserved(string id) => id is Srd || CatalogSources.IsReserved(id);
}
