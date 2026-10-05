namespace Dnd.Domain.Campaigns;

/// <summary>Audit record of a change of campaign owner. Created by <see cref="Campaign.TransferOwnership"/>.</summary>
public sealed class OwnershipTransfer
{
    private OwnershipTransfer()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CampaignId { get; private set; }

    public Guid FromUserId { get; private set; }

    public Guid ToUserId { get; private set; }

    /// <summary>Role kept by the previous owner: <see cref="CampaignRole.DM"/> or <see cref="CampaignRole.Player"/>.</summary>
    public CampaignRole PreviousOwnerNewRole { get; private set; }

    public DateTimeOffset TransferredAt { get; private set; }

    internal static OwnershipTransfer Create(Guid campaignId, Guid fromUserId, Guid toUserId, CampaignRole previousOwnerNewRole, DateTimeOffset now) => new()
    {
        CampaignId = campaignId,
        FromUserId = fromUserId,
        ToUserId = toUserId,
        PreviousOwnerNewRole = previousOwnerNewRole,
        TransferredAt = now,
    };
}
