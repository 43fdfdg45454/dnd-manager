using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Application.Abstractions.Persistence;

/// <summary>Campaign and owner of a character (null owner = non-player character).</summary>
public sealed record CharacterOwnership(Guid CampaignId, Guid? OwnerUserId);

public interface ICharacterRepository
{
    /// <summary>Campaign and owner of a character without loading it, or null when it does not exist.</summary>
    Task<CharacterOwnership?> GetOwnershipAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked character with every child collection (inventory included) loaded, ready to be modified.</summary>
    Task<Character?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only characters of the campaign with their classes and overrides loaded (what the
    /// summaries need, maximum hit points included).
    /// </summary>
    Task<IReadOnlyList<Character>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracked active characters of the campaign (the party) with every child collection loaded, ready
    /// to be modified, in no particular order.
    /// </summary>
    Task<IReadOnlyList<Character>> ListActiveWithDetailsAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Read-only equipped inventory entries of the given characters.</summary>
    Task<IReadOnlyList<CharacterItem>> ListEquippedItemsAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default);

    /// <summary>Names of the characters the user owns in the campaign, sorted by name.</summary>
    Task<IReadOnlyList<string>> ListNamesOwnedByAsync(Guid campaignId, Guid ownerUserId, CancellationToken cancellationToken = default);

    void Add(Character character);

    /// <summary>Deletes the character; children and change requests are deleted in cascade.</summary>
    void Remove(Character character);
}
