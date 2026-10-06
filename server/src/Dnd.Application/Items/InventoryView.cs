using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Characters;
using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using Dnd.Domain.Items;

namespace Dnd.Application.Items;

/// <summary>Builds inventory DTOs from a character with its items loaded and the templates they reference.</summary>
public static class InventoryView
{
    /// <summary>Carrying capacity in pounds per point of Strength (SRD).</summary>
    public const int CarryCapacityPerStrength = 15;

    public static async Task<IReadOnlyDictionary<Guid, ItemTemplate>> LoadTemplatesAsync(
        IItemTemplateRepository templates,
        IEnumerable<Guid?> templateIds,
        CancellationToken cancellationToken)
    {
        var ids = templateIds.OfType<Guid>().Distinct().ToList();
        return ids.Count == 0
            ? new Dictionary<Guid, ItemTemplate>()
            : (await templates.ListByIdsAsync(ids, cancellationToken)).ToDictionary(t => t.Id);
    }

    public static ItemTemplate? TemplateOf(IReadOnlyDictionary<Guid, ItemTemplate> templates, Guid? templateId) =>
        templateId is { } id ? templates.GetValueOrDefault(id) : null;

    public static EffectiveItem Resolve(IReadOnlyDictionary<Guid, ItemTemplate> templates, CharacterItem item) =>
        EffectiveItem.Resolve(TemplateOf(templates, item.TemplateId), item.Overrides);

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
            .Select(i => (Resolve(templates, i), i.Attuned)));

    /// <param name="strengthScore">Final Strength score from the calculated sheet.</param>
    public static InventoryDto Build(Character character, IReadOnlyDictionary<Guid, ItemTemplate> templates, int strengthScore)
    {
        var items = character.Items
            .Select(i => CharacterItemDto.From(i, TemplateOf(templates, i.TemplateId)))
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Effective.Name, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(i => i.Id)
            .ToList();
        var totalWeight = items.Sum(i => (i.Effective.WeightLb ?? 0m) * i.Quantity);

        return new InventoryDto(items, character.CopperPieces, totalWeight, strengthScore * CarryCapacityPerStrength, character.AttunedCount);
    }
}

/// <summary>Inventory DTOs of a character, calculating its sheet for the carrying capacity.</summary>
public sealed class InventoryReader(IItemTemplateRepository templates, ICharacterSheetService sheets)
{
    public Task<IReadOnlyDictionary<Guid, ItemTemplate>> TemplatesAsync(Character character, CancellationToken cancellationToken) =>
        InventoryView.LoadTemplatesAsync(templates, character.Items.Select(i => i.TemplateId), cancellationToken);

    public async Task<InventoryDto> BuildAsync(Character character, CancellationToken cancellationToken)
    {
        var sheet = await sheets.CalculateAsync(character, cancellationToken);
        var loaded = await TemplatesAsync(character, cancellationToken);
        return InventoryView.Build(character, loaded, sheet.Abilities[Abilities.Str].Score);
    }

    public async Task<CharacterItemDto> BuildItemAsync(CharacterItem item, CancellationToken cancellationToken)
    {
        var template = item.TemplateId is { } id ? (await templates.ListByIdsAsync([id], cancellationToken)).SingleOrDefault() : null;
        return CharacterItemDto.From(item, template);
    }
}
