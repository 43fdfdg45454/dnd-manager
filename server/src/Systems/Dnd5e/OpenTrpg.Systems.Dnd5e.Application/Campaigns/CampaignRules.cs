using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Rules;

namespace OpenTrpg.Systems.Dnd5e.Application.Campaigns;

/// <summary>
/// Optional rules of a 5e campaign (<c>GET/PUT /systems/dnd5e/campaigns/{id}/rules</c>), set next to its content packs.
/// </summary>
/// <param name="StarterFeatsEnabled">Characters of level 1 may take one starter feat on top of everything else.</param>
/// <param name="StarterFeatsAvailable">Some pack the campaign enables has feats marked <c>starter</c> (the app only shows the
/// switch then).</param>
public sealed record CampaignRulesDto(bool StarterFeatsEnabled, bool StarterFeatsAvailable);

/// <summary>Body of <c>PUT /systems/dnd5e/campaigns/{id}/rules</c>; null fields keep their value.</summary>
public sealed record UpdateCampaignRulesRequest(bool? StarterFeatsEnabled);

/// <summary>Reads and changes the optional rules of a campaign: members read them, the Owner and DMs change them.</summary>
public sealed class CampaignRulesHandler(
    ICampaignAccess access,
    ICampaignRulesRepository rules,
    ICatalogRepository catalog,
    CatalogScopeContext scope,
    IUnitOfWork unitOfWork,
    ICampaignNotifier notifier,
    IDateTimeProvider clock)
{
    public async Task<CampaignRulesDto> GetAsync(Guid currentUserId, Guid campaignId, CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.Player, cancellationToken);
        return await ToDtoAsync(campaignId, await rules.GetAsync(campaignId, cancellationToken), cancellationToken);
    }

    public async Task<CampaignRulesDto> UpdateAsync(Guid currentUserId, Guid campaignId, UpdateCampaignRulesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await access.RequireAsync(campaignId, currentUserId, CampaignRole.DM, cancellationToken);
        var now = clock.UtcNow;
        var current = await rules.GetAsync(campaignId, cancellationToken);
        if (current is null)
        {
            current = Dnd5eCampaignRules.Create(campaignId, now);
            rules.Add(current);
        }

        var changed = request.StarterFeatsEnabled is { } starter && current.SetStarterFeats(starter, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (changed)
        {
            await notifier.NotifyAsync(new CampaignEvent(CampaignEventTypes.CampaignUpdated, campaignId, null, null, now), cancellationToken);
        }

        return await ToDtoAsync(campaignId, current, cancellationToken);
    }

    /// <summary>Whether the campaign enables starter feats (false without a row).</summary>
    public static async Task<bool> StarterFeatsEnabledAsync(ICampaignRulesRepository rules, Guid campaignId, CancellationToken cancellationToken) =>
        (await rules.GetAsync(campaignId, cancellationToken))?.StarterFeatsEnabled ?? false;

    private async Task<CampaignRulesDto> ToDtoAsync(Guid campaignId, Dnd5eCampaignRules? current, CancellationToken cancellationToken)
    {
        await scope.UseCampaignAsync(campaignId, cancellationToken);
        var feats = await catalog.ListOptionsBySetAsync([OptionSets.Feats], cancellationToken);
        return new CampaignRulesDto(current?.StarterFeatsEnabled ?? false, feats.Any(f => f.Starter));
    }
}
