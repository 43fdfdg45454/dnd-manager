using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Domain.Rules;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Items;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Rules;

namespace OpenTrpg.Systems.Dnd5e.Application;

/// <summary>
/// D&amp;D 5e items (<see cref="IItemSystem"/>). The 5e fields of templates and inventory entries still live in the
/// core item types (they are mapped on the same tables); this part checks them, describes them and recalculates the
/// sheet when the equipment changes.
/// </summary>
public sealed class Dnd5eItemSystem(
    Dnd5eCharacterParts parts,
    ICharacterSheetService sheets,
    IValidator<ItemTemplateInput> templateValidator) : IItemSystem
{
    private static readonly HashSet<string> SystemFields = new(StringComparer.Ordinal)
    {
        "rarity", "requiresAttunement", "damage", "properties", "range", "armor", "attackBonus", "damageBonus", "effects", "modifiers",
    };

    public void ValidateTemplate(JsonElement systemFields, IValidationSink errors)
    {
        ItemTemplateInput? input;
        try
        {
            input = systemFields.Deserialize<ItemTemplateInput>(JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            errors.Add("item", "Los datos del objeto no son válidos.");
            return;
        }

        if (input is null)
        {
            errors.Add("item", "Los datos del objeto no son válidos.");
            return;
        }

        foreach (var error in templateValidator.Validate(input).Errors)
        {
            errors.Add(JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName), error.ErrorMessage);
        }
    }

    public JsonObject DescribeEffective(ItemRef item)
    {
        var all = Dnd5eCharacterParts.ToJsonObject(EffectiveItemDto.From(item.Item));
        foreach (var key in all.Select(p => p.Key).Where(k => !SystemFields.Contains(k)).ToList())
        {
            all.Remove(key);
        }

        return all;
    }

    /// <summary>Item modifiers, armor and shields change the sheet: recalculate it (capping the current hit points).</summary>
    public async Task OnInventoryChangedAsync(CharacterRef character, InventoryChange change, CancellationToken cancellationToken = default) =>
        await sheets.RecalculateAsync(await parts.LoadAsync(character, cancellationToken), cancellationToken);

    /// <summary>15 lb per point of final Strength (SRD).</summary>
    public async Task<int> CarryingCapacityAsync(CharacterRef character, CancellationToken cancellationToken = default)
    {
        var sheet = await sheets.CalculateAsync(await parts.LoadAsync(character, cancellationToken), cancellationToken);
        return Dnd5eInventory.CarryingCapacity(sheet.Abilities[Abilities.Str].Score);
    }
}
