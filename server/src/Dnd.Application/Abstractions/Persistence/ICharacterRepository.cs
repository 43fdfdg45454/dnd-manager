using Dnd.Domain.Characters;
using Dnd.Domain.Items;

namespace Dnd.Application.Abstractions.Persistence;

public interface ICharacterRepository
{
    /// <summary>Tracked character with every child collection (inventory included) loaded, ready to be modified.</summary>
    Task<Character?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only characters of the campaign with their classes and overrides loaded (what the
    /// summaries need, maximum hit points included).
    /// </summary>
    Task<IReadOnlyList<Character>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Read-only equipped inventory entries of the given characters.</summary>
    Task<IReadOnlyList<CharacterItem>> ListEquippedItemsAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default);

    void Add(Character character);

    /// <summary>Deletes the character; children and change requests are deleted in cascade.</summary>
    void Remove(Character character);
}
