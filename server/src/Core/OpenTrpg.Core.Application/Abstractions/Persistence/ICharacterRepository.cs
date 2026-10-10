using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Application.Abstractions.Persistence;

/// <summary>Campaign and owner of a character (null owner = non-player character).</summary>
public sealed record CharacterOwnership(Guid CampaignId, Guid? OwnerUserId);

public interface ICharacterRepository
{
    /// <summary>Campaign and owner of a character without loading it, or null when it does not exist.</summary>
    Task<CharacterOwnership?> GetOwnershipAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracked core character with its inventory loaded, ready to be modified. The game system loads its own
    /// part of the character when it needs it.
    /// </summary>
    Task<Character?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only core characters of the campaign.</summary>
    Task<IReadOnlyList<Character>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Tracked active core characters of the campaign (the party), in no particular order.</summary>
    Task<IReadOnlyList<Character>> ListActiveAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Read-only equipped inventory entries of the given characters.</summary>
    Task<IReadOnlyList<CharacterItem>> ListEquippedItemsAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default);

    /// <summary>Names of the characters the user owns in the campaign, sorted by name.</summary>
    Task<IReadOnlyList<string>> ListNamesOwnedByAsync(Guid campaignId, Guid ownerUserId, CancellationToken cancellationToken = default);

    /// <summary>Adds a new core character; the game system adds its own part in the same unit of work.</summary>
    void Add(Character character);

    /// <summary>Deletes the character (with its game system part); children and change requests are deleted in cascade.</summary>
    void Remove(Character character);
}
