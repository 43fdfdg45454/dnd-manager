using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>Resolves the game system of a campaign (by <c>Campaign.SystemId</c>), once per campaign and request.</summary>
public sealed class CampaignSystems(ICampaignRepository campaigns, IGameSystemRegistry registry)
{
    private readonly Dictionary<Guid, IGameSystem> _byCampaign = [];

    /// <summary>The system of the campaign; 404 when the campaign does not exist, 409 when its system is not registered.</summary>
    public async Task<IGameSystem> ForCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        if (_byCampaign.TryGetValue(campaignId, out var cached))
        {
            return cached;
        }

        var systemId = await campaigns.GetSystemIdAsync(campaignId, cancellationToken)
            ?? throw AppException.NotFound("La campaña no existe.");
        var system = registry.Find(systemId)
            ?? throw AppException.Conflict($"El sistema de juego '{systemId}' de la campaña no está disponible en esta instancia.");
        _byCampaign[campaignId] = system;
        return system;
    }

    /// <summary>The system of the character's campaign.</summary>
    public Task<IGameSystem> ForCharacterAsync(Character character, CancellationToken cancellationToken = default) =>
        ForCampaignAsync(character.CampaignId, cancellationToken);
}
