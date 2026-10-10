using System.Text.Json;
using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Infrastructure.Catalog;

// Structured starting equipment of the backgrounds of a pack (phase 17): the same schema as the catalog
// (docs/content-packs.md), with items of the SRD or of the pack itself and equipment categories of the catalog.
// Item references are checked at the end, once every item of the pack is known.
internal sealed partial class ContentPackValidator
{
    public const int MaxFixedGoldCp = 10_000_000;

    private readonly List<(string Path, string Index)> _itemReferences = [];

    /// <summary>Validates and normalizes <c>startingEquipment</c>; returns the JSON stored in the catalog.</summary>
    private string ParseStartingEquipment(string path, PackStartingEquipmentJson equipment)
    {
        if (equipment.Gold is { ValueKind: not JsonValueKind.Null })
        {
            AddError($"{path}.gold", "Solo las clases tienen riqueza inicial alternativa; en un trasfondo usa \"fixedGoldCp\".");
        }

        var fixedItems = StartingItems($"{path}.fixed", equipment.Fixed);
        var choices = new List<StartingEquipmentChoice>();
        ForEach($"{path}.choices", equipment.Choices, (choicePath, choice) =>
        {
            var options = new List<StartingEquipmentOption>();
            if (choice.Options is not { Count: > 0 })
            {
                AddError($"{choicePath}.options", "Indica al menos una opción.");
            }

            ForEach($"{choicePath}.options", choice.Options, (optionPath, option) =>
            {
                var label = RequiredText($"{optionPath}.label", option.Label, NameMaxLength);
                var items = StartingItems($"{optionPath}.items", option.Items);
                var categories = new List<StartingCategoryPick>();
                if (option.Category is not null || option.CategoryChoose is not null)
                {
                    if (CategoryPick($"{optionPath}.category", $"{optionPath}.categoryChoose", option.Category, option.CategoryChoose) is { } pick)
                    {
                        categories.Add(pick);
                    }
                }

                ForEach($"{optionPath}.categories", option.Categories, (pickPath, pick) =>
                {
                    if (CategoryPick($"{pickPath}.category", $"{pickPath}.choose", pick.Category, pick.Choose) is { } valid)
                    {
                        categories.Add(valid);
                    }
                });

                if (option.Items is not { Count: > 0 } && option.Category is null && option.Categories is not { Count: > 0 })
                {
                    AddError(optionPath, "La opción debe incluir objetos (\"items\") o una categoría (\"category\" o \"categories\").");
                }

                options.Add(new StartingEquipmentOption(label, items, categories));
            });

            var choose = OptionalInt($"{choicePath}.choose", choice.Choose, 1, Math.Max(1, options.Count)) ?? 1;
            var description = OptionalText($"{choicePath}.description", choice.Description, LongTextMaxLength);
            choices.Add(new StartingEquipmentChoice(
                description.Length > 0 ? description : string.Join(" o ", options.Select(o => o.Label)),
                choose,
                options));
        });

        var fixedGold = OptionalInt($"{path}.fixedGoldCp", equipment.FixedGoldCp, 0, MaxFixedGoldCp);
        return new StartingEquipment(fixedItems, choices, null, fixedGold is > 0 ? fixedGold : null).ToJson();
    }

    private List<StartingItem> StartingItems(string path, List<PackStartingItemJson?>? items)
    {
        var result = new List<StartingItem>();
        ForEach(path, items, (itemPath, item) =>
        {
            var index = RequiredText($"{itemPath}.item", item.Item, IndexMaxLength);
            var quantity = OptionalInt($"{itemPath}.quantity", item.Quantity, 1, Domain.Catalog.StartingEquipment.MaxQuantity) ?? 1;
            if (index.Length == 0)
            {
                return;
            }

            _itemReferences.Add(($"{itemPath}.item", index));
            result.Add(new StartingItem(index, quantity, null, SrdDataset.PackContents.GetValueOrDefault(index)));
        });
        return result;
    }

    private StartingCategoryPick? CategoryPick(string categoryPath, string choosePath, string? category, int? choose)
    {
        var index = RequiredText(categoryPath, category, IndexMaxLength);
        var count = OptionalInt(choosePath, choose, 1, Domain.Catalog.StartingEquipment.MaxChoose) ?? 1;
        if (index.Length == 0)
        {
            return null;
        }

        if (_context.EquipmentCategories is not { } categories || !categories.Contains(index))
        {
            AddError(categoryPath, $"La categoría de equipo '{index}' no existe (usa índices como \"martial-weapons\" o \"holy-symbols\").");
            return null;
        }

        return new StartingCategoryPick(index, count);
    }

    private void CheckStartingEquipmentReferences(ContentPackRows rows)
    {
        var packItems = rows.Items.Select(i => i.Index).ToHashSet(StringComparer.Ordinal);
        foreach (var (path, index) in _itemReferences)
        {
            if (!packItems.Contains(index) && _context.SrdItems?.Contains(index) != true)
            {
                AddError(path, $"El objeto '{index}' no existe en el SRD ni en el paquete.");
            }
        }
    }
}
