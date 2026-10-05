using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Campaigns;

namespace Dnd.Application.Characters;

/// <summary>Summaries of every character of the campaign, for any member. Hit points only for owner and DMs.</summary>
public sealed class ListCharactersHandler(ICampaignAccess access, ICharacterRepository characters, ICharacterSheetService sheets)
{
    public async Task<IReadOnlyList<CharacterSummaryDto>> HandleAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        var role = await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        var list = await characters.ListByCampaignAsync(campaignId, cancellationToken);
        return await sheets.BuildSummariesAsync(list, currentUserId, role.IsAtLeast(CampaignRole.DM), cancellationToken);
    }
}
