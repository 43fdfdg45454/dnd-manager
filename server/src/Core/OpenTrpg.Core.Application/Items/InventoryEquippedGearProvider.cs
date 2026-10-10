using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Items;

/// <summary>
/// <see cref="IEquippedGearProvider"/> backed by the inventory: the equipped armor, shield and active item
/// modifiers of each character, with their overrides applied, as stored in the database.
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
                g => Dnd5eInventory.Gear(g, loaded));
    }
}
