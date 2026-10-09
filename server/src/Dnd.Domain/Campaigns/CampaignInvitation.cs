namespace Dnd.Domain.Campaigns;

/// <summary>
/// Pending invitation of a user to a campaign. Created by <see cref="Campaign.Invite"/>; the user
/// becomes a member only when they accept it (<see cref="Campaign.AcceptInvitation"/>).
/// </summary>
public sealed class CampaignInvitation
{
    private CampaignInvitation()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CampaignId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary><see cref="CampaignRole.DM"/> or <see cref="CampaignRole.Player"/>.</summary>
    public CampaignRole Role { get; private set; }

    public Guid InvitedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static CampaignInvitation Create(Guid campaignId, Guid userId, CampaignRole role, Guid invitedByUserId, DateTimeOffset now) => new()
    {
        CampaignId = campaignId,
        UserId = userId,
        Role = role,
        InvitedByUserId = invitedByUserId,
        CreatedAt = now,
    };
}
