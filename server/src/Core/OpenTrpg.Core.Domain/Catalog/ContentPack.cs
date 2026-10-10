using OpenTrpg.Core.Domain.Campaigns;

namespace OpenTrpg.Core.Domain.Catalog;

/// <summary>
/// A content pack of the instance (<c>docs/content-packs.md</c>): the base pack of a game system (the SRD in D&amp;D 5e,
/// <see cref="IsBase"/>: always active, never deleted) or a pack the administrator imported. Its definitions carry
/// <c>Source</c> = <see cref="Id"/>. Imported packs are active only in the campaigns that enable them
/// (<see cref="CampaignContentPack"/>).
/// </summary>
public sealed class ContentPack
{
    public const int IdMaxLength = CatalogSources.MaxLength;
    public const int NameMaxLength = 200;
    public const int VersionMaxLength = 40;

    /// <summary><c>srd</c>, <c>phb-2014</c>… (<c>[a-z0-9-]{3,40}</c> for imported packs).</summary>
    public required string Id { get; init; }

    /// <summary>Game system that understands the pack (<see cref="Campaign.SystemId"/>, for example <c>dnd5e</c>).</summary>
    public required string SystemId { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    /// <summary>Format of the pack file (3 for imported packs); 0 for the base pack loaded by the system's seeder.</summary>
    public int FormatVersion { get; init; }

    /// <summary>The base pack of the system: always active in every campaign of the system, cannot be deleted.</summary>
    public bool IsBase { get; init; }

    public DateTimeOffset ImportedAt { get; init; }

    /// <summary>JSON object with the number of definitions per type.</summary>
    public string CountsJson { get; init; } = "{}";

    /// <summary>Ids of the packs this one references (<c>"requires"</c>); a campaign must enable them too.</summary>
    public IReadOnlyList<string> Requires { get; init; } = [];
}

/// <summary>An imported content pack enabled in a campaign. The base pack of the system is never stored (always active).</summary>
public sealed class CampaignContentPack
{
    public required Guid CampaignId { get; init; }

    public required string PackId { get; init; }

    public DateTimeOffset EnabledAt { get; init; }

    public Guid EnabledByUserId { get; init; }
}
