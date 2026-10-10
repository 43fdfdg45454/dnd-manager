using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>Catalog sources of the D&amp;D 5e system: its base pack is the SRD 5.1 (see <see cref="CatalogSources"/>).</summary>
public static class Dnd5eCatalogSources
{
    /// <summary>Id of the system (<c>Campaign.SystemId</c>, <c>ContentPack.SystemId</c>).</summary>
    public const string SystemId = "dnd5e";

    /// <summary><c>Source</c> of the SRD definitions and id of the base <see cref="ContentPack"/> of the system.</summary>
    public const string Srd = "srd";

    /// <summary>Display name of the base pack.</summary>
    public const string SrdName = "SRD 5.1";

    /// <summary>True for the ids content packs cannot use: the SRD and the ids reserved by the core.</summary>
    public static bool IsReserved(string id) => id is Srd || CatalogSources.IsReserved(id);
}
