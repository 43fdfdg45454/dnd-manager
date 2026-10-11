using OpenTrpg.Core.Domain.Catalog;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>What a game system adds to items: the rules of its fields and what an inventory change does to a sheet.</summary>
public interface IItemSystem
{
    /// <summary>Checks the system's fields of an item template (homebrew); each problem goes to <paramref name="errors"/>.</summary>
    void ValidateTemplate(JsonElement systemFields, IValidationSink errors);

    /// <summary>The system's fields of a resolved item (damage, armor class, rarity, attunement, modifiers…).</summary>
    JsonObject DescribeEffective(ItemRef item);

    /// <summary>
    /// Called after an inventory operation that can change the sheet (equip, attune, remove an equipped item, approved
    /// requests): the system recalculates what depends on the equipment.
    /// </summary>
    Task OnInventoryChangedAsync(CharacterRef character, InventoryChange change, CancellationToken cancellationToken = default);

    /// <summary>How much the character can carry, in pounds (shown with the inventory).</summary>
    Task<int> CarryingCapacityAsync(CharacterRef character, CancellationToken cancellationToken = default);

    /// <summary>
    /// Why the character may not attune to an item of <paramref name="template"/> (items restricted to some classes), as
    /// a user-facing message, or null when it may.
    /// </summary>
    Task<string?> AttunementProblemAsync(CharacterRef character, ItemTemplate template, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);

    /// <summary>
    /// Why the character may not equip the inventory entry <paramref name="item"/> (D&amp;D 5e: a graft in a body slot
    /// another equipped graft already uses), as a user-facing message, or null when it may.
    /// </summary>
    /// <param name="templates">Templates of the character's inventory entries.</param>
    Task<string?> EquipProblemAsync(CharacterRef character, CharacterItem item, IReadOnlyDictionary<Guid, ItemTemplate> templates, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
}

/// <summary>A resolved item (template plus overrides) handed to the system.</summary>
public sealed record ItemRef(EffectiveItem Item);

/// <summary>What changed in an inventory.</summary>
/// <param name="Kind">One of <see cref="InventoryChangeKinds"/>.</param>
/// <param name="ItemId">The inventory entry involved, when there is one.</param>
public sealed record InventoryChange(string Kind, Guid? ItemId = null);

public static class InventoryChangeKinds
{
    public const string Updated = "updated";

    public const string Removed = "removed";

    public const string Sold = "sold";

    public const string Returned = "returned";

    public const string Approved = "approved";
}

/// <summary>Collects validation problems of a request body (field and message in Spanish).</summary>
public interface IValidationSink
{
    void Add(string field, string message);
}
