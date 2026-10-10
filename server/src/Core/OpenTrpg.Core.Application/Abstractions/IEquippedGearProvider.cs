using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Abstractions;

/// <summary>
/// Equipped armor and shield of characters, as needed by the armor class calculation (implemented
/// over the inventory by <c>InventoryEquippedGearProvider</c>).
/// </summary>
public interface IEquippedGearProvider
{
    /// <summary>Gear by character id. Characters missing from the result have <see cref="EquippedGear.None"/>.</summary>
    Task<IReadOnlyDictionary<Guid, EquippedGear>> GetAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default);
}
