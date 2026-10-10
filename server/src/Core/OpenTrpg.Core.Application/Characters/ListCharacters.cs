using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Campaigns;

namespace OpenTrpg.Core.Application.Characters;

/// <summary>Summaries of every character of the campaign, for any member. Hit points only for owner and DMs.</summary>
public sealed class ListCharactersHandler(ICampaignAccess access, ICharacterRepository characters, CharacterViews views)
{
    public async Task<IReadOnlyList<CharacterSummaryDto>> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var list = await characters.ListByCampaignAsync(campaignId, cancellationToken);
        return await views.BuildSummariesAsync(campaignId, list, currentUserId, role.IsAtLeast(CampaignRole.DM), cancellationToken);
    }
}
