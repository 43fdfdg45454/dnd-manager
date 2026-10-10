using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;

/// <summary>The D&amp;D 5e part of the characters, loaded together with their core character.</summary>
public interface IDnd5eCharacterRepository
{
    /// <summary>
    /// Tracked character with every child collection loaded (its core character and inventory included),
    /// ready to be modified; null when it does not exist.
    /// </summary>
    Task<Dnd5eCharacter?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read-only characters with the given ids, with their core character, classes, overrides and choices loaded
    /// (what the roster needs, maximum hit points included).
    /// </summary>
    Task<IReadOnlyList<Dnd5eCharacter>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracked active characters of the campaign (the party) with every child collection loaded, ready
    /// to be modified, in no particular order.
    /// </summary>
    Task<IReadOnlyList<Dnd5eCharacter>> ListActiveWithDetailsAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>Adds the 5e part of a new character (and the core character it references, when new).</summary>
    void Add(Dnd5eCharacter character);
}
