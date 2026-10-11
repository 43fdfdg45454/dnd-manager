using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Items;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Items;

/// <summary>What the D&amp;D 5e sheet takes from an inventory: the equipped gear and the carrying capacity.</summary>
public static class Dnd5eInventory
{
    /// <summary>Carrying capacity in pounds per point of Strength (SRD).</summary>
    public const int CarryCapacityPerStrength = 15;

    /// <summary>Carrying capacity of a final Strength score.</summary>
    /// <param name="size">Size of the character ("tiny".."gargantuan"); Medium when null or unknown.</param>
    public static int CarryingCapacity(int strengthScore, string? size = null) => size switch
    {
        "tiny" => strengthScore * CarryCapacityPerStrength / 2,
        "large" => strengthScore * CarryCapacityPerStrength * 2,
        "huge" => strengthScore * CarryCapacityPerStrength * 4,
        "gargantuan" => strengthScore * CarryCapacityPerStrength * 8,
        _ => strengthScore * CarryCapacityPerStrength,
    };

    /// <summary>
    /// Gear of the equipped entries among <paramref name="items"/> (in inventory order): armor, shield
    /// and the modifiers of the active items (attunement included), with the <paramref name="enhancements"/> of each entry
    /// applied (their bonuses labeled with the option).
    /// </summary>
    public static EquippedGear Gear(
        IEnumerable<CharacterItem> items,
        IReadOnlyDictionary<Guid, ItemTemplate> templates,
        Func<CharacterItem, IReadOnlyList<AppliedEnhancement>>? enhancements = null)
    {
        var entries = items
            .Where(i => i.Equipped)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .Select(i => (Entry: i, Enhanced: Enhance(i, templates, enhancements)))
            .ToList();
        var gear = EquippedGear.FromEquipped(entries.Select(e => (e.Enhanced.Item, e.Entry.Attuned)));
        var bonuses = entries
            .Where(e => e.Enhanced.Item.IsActive(equipped: true, e.Entry.Attuned))
            .SelectMany(e => e.Enhanced.Bonuses.Select(b => new ActiveModifier(b.Label, b.Modifier, e.Enhanced.Item.Category == ItemCategory.Weapon)))
            .ToList();
        return bonuses.Count == 0 ? gear : gear with { Modifiers = [.. gear.Modifiers, .. bonuses] };
    }

    /// <summary>The entry resolved with its overrides and then its enhancements.</summary>
    public static EnhancedItem Enhance(CharacterItem item, IReadOnlyDictionary<Guid, ItemTemplate> templates, Func<CharacterItem, IReadOnlyList<AppliedEnhancement>>? enhancements) =>
        ItemEnhancer.Apply(InventoryView.Resolve(templates, item), enhancements?.Invoke(item) ?? []);
}
