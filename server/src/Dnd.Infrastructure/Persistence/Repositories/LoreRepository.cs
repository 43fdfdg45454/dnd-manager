using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Common;
using Dnd.Domain.Lore;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class LoreRepository(AppDbContext db) : ILoreRepository
{
    public async Task<IReadOnlyList<LoreEntry>> ListByCampaignAsync(Guid campaignId, LoreFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.LoreEntries.AsNoTracking().Where(x => x.CampaignId == campaignId);
        if (filter.PlayersOnly)
        {
            query = query.Where(x => x.Visibility == ContentVisibility.Players);
        }

        if (filter.Category is { } category)
        {
            query = query.Where(x => x.Category == category);
        }

        if (filter.ParentId is { } parentId)
        {
            query = query.Where(x => x.ParentId == parentId);
        }

        if (filter.Search is { } search)
        {
            query = query.Where(x => x.Title.ToLower().Contains(search) || x.ContentMarkdown.ToLower().Contains(search));
        }

        return await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public Task<LoreEntry?> GetWithAttachmentsAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.LoreEntries.Include(x => x.Attachments).AsSplitQuery().Where(x => x.Id == id).OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);

    public Task<LoreEntry?> FindInCampaignAsync(Guid campaignId, Guid id, CancellationToken cancellationToken = default) =>
        db.LoreEntries.AsNoTracking().Where(x => x.CampaignId == campaignId && x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public Task<bool> SlugExistsAsync(Guid campaignId, string slug, CancellationToken cancellationToken = default) =>
        db.LoreEntries.AnyAsync(x => x.CampaignId == campaignId && x.Slug == slug, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Guid?>> ListParentLinksAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        (await db.LoreEntries.AsNoTracking().Where(x => x.CampaignId == campaignId).Select(x => new { x.Id, x.ParentId }).ToListAsync(cancellationToken))
        .ToDictionary(x => x.Id, x => x.ParentId);

    public void Add(LoreEntry entry) => db.LoreEntries.Add(entry);

    public void Remove(LoreEntry entry) => db.LoreEntries.Remove(entry);
}
