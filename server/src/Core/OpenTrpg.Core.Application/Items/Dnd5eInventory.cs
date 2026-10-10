using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Application.Items;

/// <summary>What the D&amp;D 5e sheet takes from an inventory: the equipped gear and the carrying capacity.</summary>
public static class Dnd5eInventory
{
    /// <summary>Carrying capacity in pounds per point of Strength (SRD).</summary>
    public const int CarryCapacityPerStrength = 15;

    /// <summary>Carrying capacity of a final Strength score.</summary>
    public static int CarryingCapacity(int strengthScore) => strengthScore * CarryCapacityPerStrength;

    /// <summary>
    /// Gear of the equipped entries among <paramref name="items"/> (in inventory order): armor, shield
    /// and the modifiers of the active items (attunement included).
    /// </summary>
    public static EquippedGear Gear(IEnumerable<CharacterItem> items, IReadOnlyDictionary<Guid, ItemTemplate> templates) =>
        EquippedGear.FromEquipped(items
            .Where(i => i.Equipped)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .Select(i => (InventoryView.Resolve(templates, i), i.Attuned)));
}
