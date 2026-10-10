using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Characters;
using Microsoft.EntityFrameworkCore;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Persistence;
using OpenTrpg.Core.Infrastructure.Persistence.Repositories;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Repositories;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Persistence.Repositories;

internal sealed class CharacterCompanionRepository(AppDbContext db) : ICharacterCompanionRepository
{
    public Task<CharacterCompanion?> GetByCharacterAsync(Guid characterId, CancellationToken cancellationToken = default) =>
        db.Set<CharacterCompanion>().FirstOrDefaultAsync(x => x.CharacterId == characterId, cancellationToken);

    public async Task<IReadOnlyList<CharacterCompanion>> ListByCharactersAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default)
    {
        if (characterIds.Count == 0)
        {
            return [];
        }

        var ids = characterIds.ToList();
        return await db.Set<CharacterCompanion>().Where(x => ids.Contains(x.CharacterId)).ToListAsync(cancellationToken);
    }

    public void Add(CharacterCompanion companion) => db.Set<CharacterCompanion>().Add(companion);

    public void Remove(CharacterCompanion companion) => db.Set<CharacterCompanion>().Remove(companion);
}
