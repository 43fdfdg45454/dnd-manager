using OpenTrpg.Core.Domain.Campaigns;

namespace OpenTrpg.Core.Application.Abstractions;

/// <summary>
/// Resolves the role of a user inside a campaign. Entry check for every campaign-scoped use case.
/// </summary>
public interface ICampaignAccess
{
    /// <summary>Role of the user in the campaign, or null when the user is not a member (or the campaign does not exist).</summary>
    Task<CampaignRole?> GetRoleAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the role of the user when it is at least <paramref name="minimumRole"/>. Throws a
    /// NotFound <see cref="Common.AppException"/> (404) when the user is not a member, so the
    /// campaign's existence is not revealed, and a Forbidden one (403) when the role is lower.
    /// </summary>
    Task<CampaignRole> RequireAsync(Guid campaignId, Guid userId, CampaignRole minimumRole, CancellationToken cancellationToken = default);
}
