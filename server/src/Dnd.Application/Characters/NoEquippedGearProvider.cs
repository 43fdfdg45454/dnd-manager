using Dnd.Application.Abstractions;
using Dnd.Domain.Characters;

namespace Dnd.Application.Characters;

/// <summary>
/// Default <see cref="IEquippedGearProvider"/> until the inventory exists (phase 5): every character
/// wears nothing, so the armor class is the unarmored one.
/// </summary>
public sealed class NoEquippedGearProvider : IEquippedGearProvider
{
    private static readonly IReadOnlyDictionary<Guid, EquippedGear> Empty = new Dictionary<Guid, EquippedGear>();

    public Task<IReadOnlyDictionary<Guid, EquippedGear>> GetAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default) =>
        Task.FromResult(Empty);
}
