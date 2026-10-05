using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Campaigns;

namespace Dnd.Application.Campaigns;

/// <summary>Physically deletes the campaign and everything that belongs to it. Owner only.</summary>
public sealed class DeleteCampaignHandler(ICampaignAccess access, ICampaignRepository campaigns, IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Owner, cancellationToken);

        var campaign = await campaigns.GetWithMembersAsync(campaignId, cancellationToken) ?? throw CampaignErrors.CampaignNotFound();
        campaigns.Remove(campaign);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
