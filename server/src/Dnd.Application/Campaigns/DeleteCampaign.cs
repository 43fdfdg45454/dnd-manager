using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Files;
using Dnd.Domain.Campaigns;

namespace Dnd.Application.Campaigns;

/// <summary>Physically deletes the campaign and everything that belongs to it (stored files included). Owner only.</summary>
public sealed class DeleteCampaignHandler(ICampaignAccess access, ICampaignRepository campaigns, IFileRepository files, FileCleanup cleanup, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Owner, cancellationToken);

        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        var storedPaths = await files.ListStoragePathsByCampaignAsync(campaignId, cancellationToken);
        campaigns.Remove(campaign);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The rows of the campaign's files go with it (cascade); the bytes on disk are removed afterwards.
        await cleanup.DeleteFromDiskAsync(storedPaths);
    }
}
