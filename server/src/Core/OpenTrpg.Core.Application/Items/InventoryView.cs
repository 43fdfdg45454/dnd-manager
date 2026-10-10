using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Application.Items;

/// <summary>Builds inventory DTOs from a character with its items loaded and the templates they reference.</summary>
public static class InventoryView
{
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

    /// <param name="carryingCapacity">How much the character can carry, in pounds (given by the game system).</param>
    public static InventoryDto Build(Character character, IReadOnlyDictionary<Guid, ItemTemplate> templates, int carryingCapacity)
    {
        var items = character.Items
            .Select(i => CharacterItemDto.From(i, TemplateOf(templates, i.TemplateId)))
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Effective.Name, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(i => i.Id)
            .ToList();
        var totalWeight = items.Sum(i => (i.Effective.WeightLb ?? 0m) * i.Quantity);

        return new InventoryDto(items, character.Money, totalWeight, carryingCapacity, character.AttunedCount);
    }
}

/// <summary>Inventory DTOs of a character, with the carrying capacity the game system gives.</summary>
public sealed class InventoryReader(IItemTemplateRepository templates, CampaignSystems systems)
{
    public Task<IReadOnlyDictionary<Guid, ItemTemplate>> TemplatesAsync(Character character, CancellationToken cancellationToken) =>
        InventoryView.LoadTemplatesAsync(templates, character.Items.Select(i => i.TemplateId), cancellationToken);

    public async Task<InventoryDto> BuildAsync(Character character, CancellationToken cancellationToken)
    {
        var system = await systems.ForCharacterAsync(character, cancellationToken);
        var capacity = await system.Items.CarryingCapacityAsync(new CharacterRef(character), cancellationToken);
        var loaded = await TemplatesAsync(character, cancellationToken);
        return InventoryView.Build(character, loaded, capacity);
    }

    public async Task<CharacterItemDto> BuildItemAsync(CharacterItem item, CancellationToken cancellationToken)
    {
        var template = item.TemplateId is { } id ? (await templates.ListByIdsAsync([id], cancellationToken)).SingleOrDefault() : null;
        return CharacterItemDto.From(item, template);
    }
}
