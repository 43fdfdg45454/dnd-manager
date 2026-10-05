using Dnd.Domain.Characters;

namespace Dnd.Application.Abstractions;

/// <summary>
/// Equipped armor and shield of characters, as needed by the armor class calculation. Extension
/// point for the inventory (phase 5); until then every character wears nothing
/// (<see cref="EquippedGear.None"/>).
/// </summary>
public interface IEquippedGearProvider
{
    /// <summary>Gear by character id. Characters missing from the result have <see cref="EquippedGear.None"/>.</summary>
    Task<IReadOnlyDictionary<Guid, EquippedGear>> GetAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default);
}
