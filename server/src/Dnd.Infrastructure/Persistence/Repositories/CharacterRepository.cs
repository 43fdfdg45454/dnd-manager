using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Characters;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class CharacterRepository(AppDbContext db) : ICharacterRepository
{
    public Task<Character?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Characters
            .Include(x => x.Classes)
            .Include(x => x.Proficiencies)
            .Include(x => x.Spells)
            .Include(x => x.SpellSlots)
            .Include(x => x.Resources)
            .Include(x => x.Overrides)
            // One query per collection instead of their cartesian product.
            .AsSplitQuery()
            .Where(x => x.Id == id)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Character>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        await db.Characters
            .AsNoTracking()
            .Include(x => x.Classes)
            .Include(x => x.Overrides)
            .AsSplitQuery()
            .Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public void Add(Character character) => db.Characters.Add(character);

    public void Remove(Character character) => db.Characters.Remove(character);
}
