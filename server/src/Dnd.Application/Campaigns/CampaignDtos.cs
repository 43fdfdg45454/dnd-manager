using Dnd.Domain.Campaigns;

namespace Dnd.Application.Campaigns;

public sealed record CampaignSummaryDto(
    Guid Id,
    string Name,
    string Description,
    Guid OwnerId,
    string OwnerDisplayName,
    string MyRole,
    int MemberCount,
    DateTimeOffset CreatedAt);

public sealed record CampaignDto(
    Guid Id,
    string Name,
    string Description,
    Guid OwnerId,
    string OwnerDisplayName,
    string MyRole,
    IReadOnlyList<MemberDto> Members,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <param name="members">Current members of the campaign, as returned by the repository.</param>
    public static CampaignDto From(Campaign campaign, IReadOnlyList<MemberDto> members, Guid currentUserId) => new(
        campaign.Id,
        campaign.Name,
        campaign.Description,
        campaign.OwnerId,
        members.FirstOrDefault(m => m.UserId == campaign.OwnerId)?.DisplayName ?? string.Empty,
        members.FirstOrDefault(m => m.UserId == currentUserId)?.Role ?? string.Empty,
        members,
        campaign.CreatedAt,
        campaign.UpdatedAt);
}

public sealed record MemberDto(Guid UserId, string DisplayName, string Email, string Role, DateTimeOffset JoinedAt);
