namespace OpenTrpg.Core.Application.ContentPacks;

/// <summary>
/// A content pack of the instance (<c>GET /api/v1/admin/content-packs</c>). <see cref="Counts"/>: definitions per type
/// ("subclasses", "features", "items"...). <see cref="IsBase"/>: the base pack of the system (the SRD), always active
/// and never deleted. <see cref="FormatVersion"/>: 3 for imported packs, 0 for the base pack.
/// </summary>
public sealed record ContentPackDto(
    string Id,
    string SystemId,
    string Name,
    string Version,
    int FormatVersion,
    bool IsBase,
    DateTimeOffset ImportedAt,
    IReadOnlyDictionary<string, int> Counts)
{
    /// <summary>Packs this one references; a campaign must enable them too.</summary>
    public IReadOnlyList<string> Requires { get; init; } = [];
}

/// <summary>Result of <c>POST /api/v1/admin/content-packs</c>.</summary>
public sealed record ContentPackImportResultDto(string Id, string Name, string Version, IReadOnlyDictionary<string, int> Counts)
{
    public string SystemId { get; init; } = string.Empty;

    public int FormatVersion { get; init; }

    public IReadOnlyList<string> Requires { get; init; } = [];
}

/// <summary>
/// A source of catalog definitions: the base pack ("srd") or an imported content pack (id and display name).
/// <see cref="Enabled"/>: with <c>?campaignId=</c>, whether the pack is active in that campaign (the base pack always);
/// null without a campaign.
/// </summary>
public sealed record CatalogSourceDto(string Id, string Name, string? Version)
{
    public bool IsBase { get; init; }

    public bool? Enabled { get; init; }
}

/// <summary>A pack of the campaign's system as the campaign sees it (<c>GET /api/v1/campaigns/{id}/content-packs</c>).</summary>
/// <param name="Enabled">Always true for the base pack.</param>
/// <param name="Requires">Packs that must be enabled together with this one.</param>
public sealed record CampaignContentPackDto(string Id, string Name, string Version, bool IsBase, bool Enabled, IReadOnlyList<string> Requires);

/// <summary>Body of <c>PUT /api/v1/campaigns/{id}/content-packs</c>: the imported packs to enable (the base pack is ignored).</summary>
public sealed record SetCampaignContentPacksRequest(IReadOnlyList<string>? PackIds);

public static class ContentPackLimits
{
    /// <summary>Maximum size of a pack file (20 MB).</summary>
    public const long MaxFileBytes = 20L * 1024 * 1024;

    /// <summary>Most packs a campaign can enable at once.</summary>
    public const int MaxEnabledPerCampaign = 50;
}

/// <summary>
/// The pack is not valid. <see cref="Errors"/> lists every problem as "path: message" in Spanish, e.g.
/// <c>items[3].modifiers[0].kind: Tipo de modificador desconocido.</c>
/// </summary>
public sealed class ContentPackInvalidException(IReadOnlyList<string> errors)
    : Exception(errors.Count == 1 ? errors[0] : $"El paquete de contenido tiene {errors.Count} errores.")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
