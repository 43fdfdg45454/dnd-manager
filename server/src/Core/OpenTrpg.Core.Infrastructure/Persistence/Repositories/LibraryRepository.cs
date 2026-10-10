using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Library;
using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Infrastructure.Persistence.Repositories;

internal sealed class LibraryRepository(AppDbContext db) : ILibraryRepository
{
    public async Task<IReadOnlyList<LibraryDocument>> ListAsync(string? search, LibraryCategory? category, CancellationToken cancellationToken = default)
    {
        var query = db.LibraryDocuments.AsNoTracking();
        if (category is { } value)
        {
            query = query.Where(x => x.Category == value);
        }

        if (search is not null)
        {
            query = query.Where(x => x.Title.ToLower().Contains(search) || (x.Description != null && x.Description.ToLower().Contains(search)));
        }

        return await query.OrderBy(x => x.Title).ThenBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public Task<LibraryDocument?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.LibraryDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RecommendedDocument>> ListRecommendedAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        (await db.CampaignDocuments
            .AsNoTracking()
            .Where(c => c.CampaignId == campaignId)
            .Join(db.LibraryDocuments, c => c.DocumentId, d => d.Id, (c, d) => new { Document = d, c.Note })
            .OrderBy(x => x.Document.Title)
            .ThenBy(x => x.Document.Id)
            .ToListAsync(cancellationToken))
        .Select(x => new RecommendedDocument(x.Document, x.Note))
        .ToList();

    public Task<CampaignDocument?> GetRecommendationAsync(Guid campaignId, Guid documentId, CancellationToken cancellationToken = default) =>
        db.CampaignDocuments.FirstOrDefaultAsync(x => x.CampaignId == campaignId && x.DocumentId == documentId, cancellationToken);

    public void Add(LibraryDocument document) => db.LibraryDocuments.Add(document);

    public void Remove(LibraryDocument document) => db.LibraryDocuments.Remove(document);

    public void Add(CampaignDocument recommendation) => db.CampaignDocuments.Add(recommendation);

    public void Remove(CampaignDocument recommendation) => db.CampaignDocuments.Remove(recommendation);
}
