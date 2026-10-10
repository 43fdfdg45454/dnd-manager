using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Characters;
using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

internal sealed class CharacterCompanionRepository(AppDbContext db) : ICharacterCompanionRepository
{
    public Task<CharacterCompanion?> GetByCharacterAsync(Guid characterId, CancellationToken cancellationToken = default) =>
        db.CharacterCompanions.FirstOrDefaultAsync(x => x.CharacterId == characterId, cancellationToken);

    public async Task<IReadOnlyList<CharacterCompanion>> ListByCharactersAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default)
    {
        if (characterIds.Count == 0)
        {
            return [];
        }

        var ids = characterIds.ToList();
        return await db.CharacterCompanions.Where(x => ids.Contains(x.CharacterId)).ToListAsync(cancellationToken);
    }

    public void Add(CharacterCompanion companion) => db.CharacterCompanions.Add(companion);

    public void Remove(CharacterCompanion companion) => db.CharacterCompanions.Remove(companion);
}
