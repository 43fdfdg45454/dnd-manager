using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Characters;
using Dnd.Domain.Items;
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
            .Include(x => x.Items)
            .Include(x => x.Choices)
            // One query per collection instead of their cartesian product.
            .AsSplitQuery()
            .Where(x => x.Id == id)
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<CharacterOwnership?> GetOwnershipAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Characters
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new CharacterOwnership(x.CampaignId, x.OwnerUserId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Character>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        await db.Characters
            .AsNoTracking()
            .Include(x => x.Classes)
            .Include(x => x.Overrides)
            .Include(x => x.Choices)
            .AsSplitQuery()
            .Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Character>> ListActiveWithDetailsAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        await db.Characters
            .Include(x => x.Classes)
            .Include(x => x.Proficiencies)
            .Include(x => x.Spells)
            .Include(x => x.SpellSlots)
            .Include(x => x.Resources)
            .Include(x => x.Overrides)
            .Include(x => x.Items)
            .Include(x => x.Choices)
            .AsSplitQuery()
            .Where(x => x.CampaignId == campaignId && x.Status == CharacterStatus.Active)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CharacterItem>> ListEquippedItemsAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default) =>
        characterIds.Count == 0
            ? []
            : await db.CharacterItems.AsNoTracking().Where(x => x.Equipped && characterIds.Contains(x.CharacterId)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListNamesOwnedByAsync(Guid campaignId, Guid ownerUserId, CancellationToken cancellationToken = default) =>
        await db.Characters
            .AsNoTracking()
            .Where(x => x.CampaignId == campaignId && x.OwnerUserId == ownerUserId)
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync(cancellationToken);

    public void Add(Character character) => db.Characters.Add(character);

    public void Remove(Character character) => db.Characters.Remove(character);
}
