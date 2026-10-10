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

internal sealed class Dnd5eCharacterRepository(AppDbContext db) : IDnd5eCharacterRepository
{
    public Task<Dnd5eCharacter?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithDetails()
            .Where(x => x.Id == id)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Dnd5eCharacter>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        await db.Set<Dnd5eCharacter>()
            .AsNoTracking()
            .Include(x => x.Character)
            .Include(x => x.Classes)
            .Include(x => x.Overrides)
            .Include(x => x.Choices)
            .AsSplitQuery()
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Dnd5eCharacter>> ListActiveWithDetailsAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        await WithDetails()
            .Where(x => x.Character.CampaignId == campaignId && x.Character.Status == CharacterStatus.Active)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public void Add(Dnd5eCharacter character) => db.Set<Dnd5eCharacter>().Add(character);

    private IQueryable<Dnd5eCharacter> WithDetails() =>
        db.Set<Dnd5eCharacter>()
            .Include(x => x.Character)
            .ThenInclude(x => x.Items)
            .Include(x => x.Classes)
            .Include(x => x.Proficiencies)
            .Include(x => x.Spells)
            .Include(x => x.SpellSlots)
            .Include(x => x.Resources)
            .Include(x => x.Overrides)
            .Include(x => x.Choices)
            // One query per collection instead of their cartesian product.
            .AsSplitQuery();
}
