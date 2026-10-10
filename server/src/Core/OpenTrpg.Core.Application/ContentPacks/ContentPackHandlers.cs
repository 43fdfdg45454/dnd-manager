using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Campaigns;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Campaigns;

namespace OpenTrpg.Core.Application.ContentPacks;

/// <summary>Content packs of the instance, base packs included (admin).</summary>
public sealed class ListContentPacksHandler(IContentPackImporter importer)
{
    public Task<IReadOnlyList<ContentPackDto>> HandleAsync(CancellationToken cancellationToken = default) =>
        importer.ListAsync(cancellationToken);
}

/// <summary>Imports or replaces a content pack (admin). Throws <see cref="ContentPackInvalidException"/> when it is not valid.</summary>
public sealed class ImportContentPackHandler(IContentPackImporter importer)
{
    public Task<ContentPackImportResultDto> HandleAsync(Stream json, CancellationToken cancellationToken = default) =>
        importer.ImportAsync(json, cancellationToken);
}

/// <summary>
/// Deletes a content pack (admin): 404 when it does not exist, 409 <c>base-pack</c> for the base pack of a system.
/// Characters keep their indexes and show the content as missing; campaigns stop enabling it.
/// </summary>
public sealed class DeleteContentPackHandler(IContentPackImporter importer)
{
    public async Task HandleAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!await importer.DeleteAsync(id, cancellationToken))
        {
            throw AppException.NotFound("El paquete de contenido no existe.");
        }
    }
}

/// <summary>The packs of the campaign's system with whether the campaign enables them (members).</summary>
public sealed class ListCampaignContentPacksHandler(ICampaignAccess access, ICampaignRepository campaigns, IContentPackRepository packs)
{
    public async Task<IReadOnlyList<CampaignContentPackDto>> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var systemId = await campaigns.GetSystemIdAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        var enabled = (await packs.ListEnabledAsync(campaignId, cancellationToken)).ToHashSet(StringComparer.Ordinal);
        return (await packs.ListBySystemAsync(systemId, cancellationToken))
            .OrderByDescending(p => p.IsBase)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => new CampaignContentPackDto(p.Id, p.Name, p.Version, p.IsBase, p.IsBase || enabled.Contains(p.Id), p.Requires))
            .ToList();
    }
}

/// <summary>
/// Replaces the packs a campaign enables (Owner/DM). The base pack is ignored (always active); 400 <c>unknown-pack</c>
/// for a pack that does not exist or belongs to another system and <c>missing-requirement</c> when a pack's
/// <c>requires</c> are not enabled too. Publishes <see cref="CampaignEventTypes.CampaignUpdated"/>.
/// </summary>
public sealed class SetCampaignContentPacksHandler(
    ICampaignAccess access,
    ICampaignRepository campaigns,
    IContentPackRepository packs,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public const string UnknownPackCode = "unknown-pack";
    public const string MissingRequirementCode = "missing-requirement";

    public async Task<IReadOnlyList<CampaignContentPackDto>> HandleAsync(
        Guid currentUserId,
        Guid campaignId,
        SetCampaignContentPacksRequest request,
        CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var systemId = await campaigns.GetSystemIdAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        var available = (await packs.ListBySystemAsync(systemId, cancellationToken)).ToDictionary(p => p.Id, StringComparer.Ordinal);

        var requested = (request.PackIds ?? [])
            .Select(id => id?.Trim() ?? string.Empty)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (requested.Count > ContentPackLimits.MaxEnabledPerCampaign)
        {
            throw AppException.Validation("packIds", $"Una campaña puede activar como máximo {ContentPackLimits.MaxEnabledPerCampaign} paquetes.");
        }

        if (requested.FirstOrDefault(id => !available.ContainsKey(id)) is { } unknown)
        {
            throw AppException.Validation("packIds", $"El paquete '{unknown}' no existe para el sistema de la campaña.", UnknownPackCode);
        }

        var enabled = requested.Where(id => !available[id].IsBase).ToHashSet(StringComparer.Ordinal);
        foreach (var id in enabled)
        {
            if (available[id].Requires.FirstOrDefault(r => !enabled.Contains(r) && !(available.TryGetValue(r, out var required) && required.IsBase)) is { } missing)
            {
                throw AppException.Validation(
                    "packIds",
                    $"El paquete '{available[id].Name}' requiere '{(available.TryGetValue(missing, out var pack) ? pack.Name : missing)}': actívalo también.",
                    MissingRequirementCode);
            }
        }

        var now = clock.UtcNow;
        await packs.ReplaceEnabledAsync(campaignId, enabled, currentUserId, now, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await notifier.NotifyAsync(new CampaignEvent(CampaignEventTypes.CampaignUpdated, campaignId, null, null, now), cancellationToken);

        return available.Values
            .OrderByDescending(p => p.IsBase)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Id, StringComparer.Ordinal)
            .Select(p => new CampaignContentPackDto(p.Id, p.Name, p.Version, p.IsBase, p.IsBase || enabled.Contains(p.Id), p.Requires))
            .ToList();
    }
}
