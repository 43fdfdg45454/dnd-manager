using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;

public interface ICharacterCompanionRepository
{
    /// <summary>Tracked companion of a character, or null.</summary>
    Task<CharacterCompanion?> GetByCharacterAsync(Guid characterId, CancellationToken cancellationToken = default);

    /// <summary>Tracked companions of the given characters.</summary>
    Task<IReadOnlyList<CharacterCompanion>> ListByCharactersAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default);

    void Add(CharacterCompanion companion);

    void Remove(CharacterCompanion companion);
}
