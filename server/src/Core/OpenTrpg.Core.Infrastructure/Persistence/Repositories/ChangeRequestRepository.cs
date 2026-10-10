using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Characters;
using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

internal sealed class ChangeRequestRepository(AppDbContext db) : IChangeRequestRepository
{
    public Task<ChangeRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.ChangeRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ChangeRequest>> ListPendingAsync(Guid characterId, ChangeRequestType? type, CancellationToken cancellationToken = default)
    {
        var query = db.ChangeRequests.Where(x => x.CharacterId == characterId && x.Status == ChangeRequestStatus.Pending);
        if (type is { } requestType)
        {
            query = query.Where(x => x.Type == requestType);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChangeRequestView>> ListViewsAsync(ChangeRequestQuery query, CancellationToken cancellationToken = default)
    {
        var requests = db.ChangeRequests.AsNoTracking();
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

        if (query.RequestedByUserId is { } requestedBy)
        {
            requests = requests.Where(x => x.RequestedByUserId == requestedBy);
        }

        if (query.Status is { } status)
        {
            requests = requests.Where(x => x.Status == status);
        }

        var rows = await (
                from request in requests
                join character in db.Characters on request.CharacterId equals character.Id
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
            .OrderByDescending(r => r.Request.CreatedAt)
            .ThenBy(r => r.Request.Id)
            .Select(r => new ChangeRequestView(r.Request, r.CharacterName, r.OwnerUserId, r.RequesterName, r.ResolverName))
            .ToList();
    }

    public void Add(ChangeRequest request) => db.ChangeRequests.Add(request);
}
