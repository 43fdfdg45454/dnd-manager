using OpenTrpg.Core.Application.Campaigns;
using OpenTrpg.Core.Domain.Campaigns;

namespace OpenTrpg.Core.Application.Abstractions.Persistence;

/// <summary>A member with what is needed to email them and to show their name.</summary>
public sealed record MemberContact(Guid CampaignId, Guid UserId, CampaignRole Role, string DisplayName, string Email, bool NotificationsEnabled, bool IsActive)
{
    /// <summary>Whether session emails go to this member: an active account that has not opted out.</summary>
    public bool WantsEmails => NotificationsEnabled && IsActive;
}

/// <summary>Name and time zone of a campaign, as needed to render its sessions.</summary>
public sealed record CampaignScheduleInfo(Guid Id, string Name, string TimeZoneId, IReadOnlyList<int> ReminderOffsetsMinutes);

public interface ICampaignRepository
{
    /// <summary>Tracked campaign with its members loaded, ready to be modified.</summary>
    Task<Campaign?> GetWithMembersAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only campaign without members.</summary>
    Task<Campaign?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The game system of the campaign, or null when the campaign does not exist.</summary>
    Task<string?> GetSystemIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Campaigns where the user is a member, ordered by name.</summary>
    Task<IReadOnlyList<CampaignSummaryDto>> ListSummariesForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Members with their user data, ordered by role (Owner, DM, Player) and display name.</summary>
    Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Members of the given campaigns with their account data (read-only).</summary>
    Task<IReadOnlyList<MemberContact>> ListMemberContactsAsync(IReadOnlyCollection<Guid> campaignIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CampaignScheduleInfo>> ListScheduleInfoAsync(IReadOnlyCollection<Guid> campaignIds, CancellationToken cancellationToken = default);

    /// <summary>Ids of the campaigns where the user is a member.</summary>
    Task<IReadOnlyList<Guid>> ListCampaignIdsOfUserAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(Campaign campaign);

    /// <summary>Deletes the campaign; members and ownership transfers are deleted in cascade.</summary>
    void Remove(Campaign campaign);

    void AddOwnershipTransfer(OwnershipTransfer transfer);

    /// <summary>Tracked invitation, or null.</summary>
    Task<CampaignInvitation?> GetInvitationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Pending invitation of the user to the campaign, or null (read-only).</summary>
    Task<CampaignInvitation?> FindInvitationAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Pending invitations of a campaign, oldest first.</summary>
    Task<IReadOnlyList<CampaignInvitationDto>> ListInvitationsForCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Pending invitations of a user, newest first.</summary>
    Task<IReadOnlyList<MyInvitationDto>> ListInvitationsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    void AddInvitation(CampaignInvitation invitation);

    void RemoveInvitation(CampaignInvitation invitation);
}
