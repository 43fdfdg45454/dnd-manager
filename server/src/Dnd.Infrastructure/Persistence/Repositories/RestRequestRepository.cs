using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Characters;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class RestRequestRepository(AppDbContext db) : IRestRequestRepository
{
    public Task<RestRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.RestRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<RestRequest?> GetPendingAsync(Guid characterId, CancellationToken cancellationToken = default) =>
        db.RestRequests.FirstOrDefaultAsync(x => x.CharacterId == characterId && x.Status == RestRequestStatus.Pending, cancellationToken);

    public async Task<IReadOnlyList<RestRequest>> ListPendingAsync(IReadOnlyCollection<Guid> characterIds, CancellationToken cancellationToken = default)
    {
        if (characterIds.Count == 0)
        {
            return [];
        }

        var ids = characterIds.ToList();
        return await db.RestRequests
            .Where(x => ids.Contains(x.CharacterId) && x.Status == RestRequestStatus.Pending)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RestRequestView>> ListViewsAsync(RestRequestQuery query, CancellationToken cancellationToken = default)
    {
        var requests = db.RestRequests.AsNoTracking();
        if (query.Id is { } id)
        {
            requests = requests.Where(x => x.Id == id);
        }

        if (query.CampaignId is { } campaignId)
        {
            requests = requests.Where(x => x.CampaignId == campaignId);
        }

        if (query.CharacterId is { } characterId)
        {
            requests = requests.Where(x => x.CharacterId == characterId);
        }

        if (query.Status is { } status)
        {
            requests = requests.Where(x => x.Status == status);
        }

        var characters = db.Characters.AsNoTracking();
        if (query.CharacterOwnerUserId is { } ownerId)
        {
            characters = characters.Where(c => c.OwnerUserId == ownerId);
        }

        var rows = await (
                from request in requests
                join character in characters on request.CharacterId equals character.Id
                join requester in db.Users on request.RequestedByUserId equals requester.Id
                from resolver in db.Users.Where(u => u.Id == request.ResolvedByUserId).DefaultIfEmpty()
                select new
                {
                    Request = request,
                    CharacterName = character.Name,
                    character.OwnerUserId,
                    RequesterName = requester.DisplayName,
                    ResolverName = resolver == null ? null : resolver.DisplayName,
                })
            .ToListAsync(cancellationToken);

        // Sorted in memory: SQLite cannot order by DateTimeOffset.
        return rows
            .OrderByDescending(r => r.Request.RequestedAt)
            .ThenBy(r => r.Request.Id)
            .Select(r => new RestRequestView(r.Request, r.CharacterName, r.OwnerUserId, r.RequesterName, r.ResolverName))
            .ToList();
    }

    public void Add(RestRequest request) => db.RestRequests.Add(request);
}
