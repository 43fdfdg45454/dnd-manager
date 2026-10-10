using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Application.Catalog;

/// <summary>
/// Structured starting equipment of a class or a background with every item resolved against the catalog.
/// </summary>
/// <param name="Fixed">Items every character gets.</param>
/// <param name="Choices">(a)/(b) choices, in the order of the book.</param>
/// <param name="Gold">Alternative starting wealth of the class (roll <c>dice</c> × <c>multiplier</c> gp); null for backgrounds.</param>
/// <param name="FixedGoldCp">Money included with the equipment, in copper pieces (backgrounds); null when none.</param>
public sealed record StartingEquipmentDto(
    IReadOnlyList<StartingItemDto> Fixed,
    IReadOnlyList<StartingEquipmentChoiceDto> Choices,
    StartingGoldDto? Gold,
    int? FixedGoldCp);

/// <param name="Item">Catalog item index ("chain-mail").</param>
/// <param name="TemplateId">Id of the catalog item template, or null when the index does not resolve (the name comes from the dataset).</param>
/// <param name="Contents">What an equipment pack contains, when known; null otherwise.</param>
public sealed record StartingItemDto(string Item, Guid? TemplateId, string Name, int Quantity, IReadOnlyList<StartingItemDto>? Contents);

/// <summary>Pick <see cref="Choose"/> of <see cref="Options"/>.</summary>
public sealed record StartingEquipmentChoiceDto(string Description, int Choose, IReadOnlyList<StartingEquipmentOptionDto> Options);

/// <summary>Fixed items of the option plus <see cref="Categories"/> picks (each one asks for <c>choose</c> items of the category).</summary>
public sealed record StartingEquipmentOptionDto(string Label, IReadOnlyList<StartingItemDto> Items, IReadOnlyList<StartingCategoryPickDto> Categories);

/// <param name="Category">Equipment category index ("martial-weapons"), for <c>GET /catalog/equipment-categories/{index}</c>.</param>
public sealed record StartingCategoryPickDto(string Category, string Name, int Choose);

public sealed record StartingGoldDto(string Dice, int Multiplier);

/// <summary>Equipment category with the catalog items it contains (only those that resolve), ordered by name.</summary>
public sealed record EquipmentCategoryDto(string Index, string Name, IReadOnlyList<EquipmentCategoryItemDto> Items);

public sealed record EquipmentCategoryItemDto(Guid TemplateId, string Index, string Name);

/// <summary>Resolves the item indexes and categories of stored starting equipment with one query per table.</summary>
internal static class StartingEquipmentResolver
{
    /// <summary>Loads the items and categories referenced by <paramref name="equipment"/> and returns the mapper to DTOs.</summary>
    public static async Task<Func<StartingEquipment?, StartingEquipmentDto?>> PrepareAsync(
        ICatalogRepository catalog,
        IEnumerable<StartingEquipment?> equipment,
        CancellationToken cancellationToken)
    {
        var present = equipment.OfType<StartingEquipment>().ToList();
        var itemIndexes = present.SelectMany(e => e.ItemIndexes()).Distinct(StringComparer.Ordinal).ToList();
        var categoryIndexes = present.SelectMany(e => e.CategoryIndexes()).Distinct(StringComparer.Ordinal).ToList();

        var templates = (await catalog.ListCatalogItemsByIndexAsync(itemIndexes, cancellationToken))
            .OrderBy(t => t.Source == Dnd5eCatalogSources.Srd ? 0 : 1)
            .GroupBy(t => t.Index!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var categories = (await catalog.ListEquipmentCategoriesByIndexAsync(categoryIndexes, cancellationToken))
            .ToDictionary(c => c.Index, c => c.Name, StringComparer.Ordinal);

        StartingItemDto Item(StartingItem item)
        {
            var template = templates.GetValueOrDefault(item.Item);
            return new StartingItemDto(
                item.Item,
                template?.Id,
                template?.Name ?? item.Name ?? item.Item,
                item.Quantity,
                item.Contents is { Count: > 0 } contents ? contents.Select(Item).ToList() : null);
        }

        return stored => stored is null
            ? null
            : new StartingEquipmentDto(
                stored.Fixed.Select(Item).ToList(),
                stored.Choices
                    .Select(c => new StartingEquipmentChoiceDto(
                        c.Description,
                        c.Choose,
                        c.Options
                            .Select(o => new StartingEquipmentOptionDto(
                                o.Label,
                                o.Items.Select(Item).ToList(),
                                o.Categories.Select(k => new StartingCategoryPickDto(k.Category, categories.GetValueOrDefault(k.Category) ?? k.Category, k.Choose)).ToList()))
                            .ToList()))
                    .ToList(),
                stored.Gold is { } gold ? new StartingGoldDto(gold.Dice, gold.Multiplier) : null,
                stored.FixedGoldCp);
    }
}

/// <summary>Items of an equipment category for the "any martial weapon" picker (any signed-in user).</summary>
public sealed class GetEquipmentCategoryHandler(ICatalogRepository catalog)
{
    public async Task<EquipmentCategoryDto> HandleAsync(string index, CancellationToken cancellationToken = default)
    {
        var category = await catalog.GetEquipmentCategoryAsync(index, cancellationToken) ?? throw CatalogErrors.EquipmentCategoryNotFound();
        var items = (await catalog.ListCatalogItemsByIndexAsync(category.ItemIndexes, cancellationToken))
            .OrderBy(t => t.Source == Dnd5eCatalogSources.Srd ? 0 : 1)
            .GroupBy(t => t.Index!, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.Index, StringComparer.Ordinal)
            .Select(t => new EquipmentCategoryItemDto(t.Id, t.Index!, t.Name))
            .ToList();
        return new EquipmentCategoryDto(category.Index, category.Name, items);
    }
}
