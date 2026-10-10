using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Items;

/// <summary>Tells the game system of a character that its inventory changed (<see cref="IItemSystem.OnInventoryChangedAsync"/>).</summary>
public sealed class InventoryHooks(CampaignSystems systems)
{
    public async Task ChangedAsync(Character character, string kind, Guid? itemId, CancellationToken cancellationToken)
    {
        var system = await systems.ForCharacterAsync(character, cancellationToken);
        await system.Items.OnInventoryChangedAsync(new CharacterRef(character), new InventoryChange(kind, itemId), cancellationToken);
    }
}
