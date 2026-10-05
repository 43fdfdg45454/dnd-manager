using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Characters;

namespace Dnd.Application.Items;

/// <summary>
/// <see cref="IEquippedGearProvider"/> backed by the inventory: the equipped armor and shield of each
/// character, with their overrides applied, as stored in the database.
/// </summary>
public sealed class InventoryEquippedGearProvider(ICharacterRepository characters, IItemTemplateRepository templates) : IEquippedGearProvider
{
    public async Task<IReadOnlyDictionary<Guid, EquippedGear>> GetAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default)
    {
        if (characterIds.Count == 0)
        {
            return new Dictionary<Guid, EquippedGear>();
        }

        var equipped = await characters.ListEquippedItemsAsync(characterIds, cancellationToken);
        if (equipped.Count == 0)
        {
            return new Dictionary<Guid, EquippedGear>();
        }

        var loaded = await InventoryView.LoadTemplatesAsync(templates, equipped.Select(i => i.TemplateId), cancellationToken);
        return equipped
            .GroupBy(i => i.CharacterId)
            .ToDictionary(
                g => g.Key,
                g => EquippedGear.FromEquipped(g.OrderBy(i => i.SortOrder).Select(i => InventoryView.Resolve(loaded, i))));
    }
}
