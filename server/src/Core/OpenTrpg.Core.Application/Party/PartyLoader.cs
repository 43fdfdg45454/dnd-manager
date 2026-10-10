using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Domain.Campaigns;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Party;

/// <summary>Loads the party (active characters) of a campaign for a DM (403 for players, 404 for non-members).</summary>
public sealed class PartyLoader(ICampaignAccess access, ICharacterRepository characters)
{
    /// <summary>Most characters a group action may name.</summary>
    public const int MaxCharacters = 100;

    /// <summary>403 unless the actor is at least DM of the campaign (404 for non-members).</summary>
    public Task RequireDmAsync(Guid campaignId, Guid actorUserId, CancellationToken cancellationToken) =>
        access.RequireAsync(campaignId, actorUserId, CampaignRole.DM, cancellationToken);

    /// <summary>Tracked active core characters of the campaign.</summary>
    public async Task<IReadOnlyList<Character>> LoadAsync(Guid campaignId, Guid actorUserId, CancellationToken cancellationToken)
    {
        await RequireDmAsync(campaignId, actorUserId, cancellationToken);
        return await characters.ListActiveAsync(campaignId, cancellationToken);
    }

    /// <summary>The characters of <paramref name="party"/> with the given ids (all when empty); 404 when one is not in it.</summary>
    public static IReadOnlyList<Character> Select(IReadOnlyList<Character> party, IReadOnlyCollection<Guid>? ids) =>
        Select(party, ids, c => c.Id);

    /// <summary>
    /// The members of <paramref name="party"/> with the given ids (all when empty), in the order of the ids; 404 when
    /// one is not in it. Game systems use it with their own view of the characters.
    /// </summary>
    public static IReadOnlyList<T> Select<T>(IReadOnlyList<T> party, IReadOnlyCollection<Guid>? ids, Func<T, Guid> idOf)
    {
        if (ids is null || ids.Count == 0)
        {
            return party;
        }

        var byId = party.ToDictionary(idOf);
        return ids.Distinct().Select(id => byId.GetValueOrDefault(id) ?? throw CharacterErrors.CharacterNotFound()).ToList();
    }
}
