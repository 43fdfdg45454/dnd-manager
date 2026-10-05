namespace Dnd.Domain.Campaigns;

/// <summary>Membership of a user in a campaign. Created and changed only through <see cref="Campaign"/>.</summary>
public sealed class CampaignMember
{
    private CampaignMember()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CampaignId { get; private set; }

    public Guid UserId { get; private set; }

    public CampaignRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    internal static CampaignMember Create(Guid campaignId, Guid userId, CampaignRole role, DateTimeOffset now) => new()
    {
        CampaignId = campaignId,
        UserId = userId,
        Role = role,
        JoinedAt = now,
    };

    internal void ChangeRole(CampaignRole role) => Role = role;
}
