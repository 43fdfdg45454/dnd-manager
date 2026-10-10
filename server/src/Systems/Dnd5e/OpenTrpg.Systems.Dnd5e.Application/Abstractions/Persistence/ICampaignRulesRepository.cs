using OpenTrpg.Systems.Dnd5e.Domain.Rules;

namespace OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;

public interface ICampaignRulesRepository
{
    /// <summary>Tracked rules of the campaign, or null when it never changed them (defaults).</summary>
    Task<Dnd5eCampaignRules?> GetAsync(Guid campaignId, CancellationToken cancellationToken = default);

    void Add(Dnd5eCampaignRules rules);
}
