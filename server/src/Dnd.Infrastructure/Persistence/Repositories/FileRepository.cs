using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Files;
using Microsoft.EntityFrameworkCore;

namespace Dnd.Infrastructure.Persistence.Repositories;

internal sealed class FileRepository(AppDbContext db) : IFileRepository
{
    public Task<StoredFile?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.StoredFiles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<StoredFile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count == 0
            ? []
            : await db.StoredFiles.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ListStoragePathsByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        await db.StoredFiles.AsNoTracking().Where(x => x.CampaignId == campaignId).Select(x => x.StoragePath).ToListAsync(cancellationToken);

    public async Task<bool> IsReferencedAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        await db.Maps.AnyAsync(x => x.FileId == fileId, cancellationToken)
        || await db.LoreEntries.AnyAsync(x => x.CoverFileId == fileId, cancellationToken)
        || await db.LoreAttachments.AnyAsync(x => x.FileId == fileId, cancellationToken)
        || await db.LibraryDocuments.AnyAsync(x => x.FileId == fileId, cancellationToken)
        || await db.Characters.AnyAsync(x => x.PortraitFileId == fileId, cancellationToken)
        || await db.AppReleases.AnyAsync(x => x.FileId == fileId, cancellationToken);

    public void Add(StoredFile file) => db.StoredFiles.Add(file);

    public void Remove(StoredFile file) => db.StoredFiles.Remove(file);
}
