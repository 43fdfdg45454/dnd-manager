using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;
using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

internal sealed class CharacterRepository(AppDbContext db) : ICharacterRepository
{
    public Task<Character?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Characters
            .Include(x => x.Items)
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
            .Where(x => x.CampaignId == campaignId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Character>> ListActiveAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        await db.Characters
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
