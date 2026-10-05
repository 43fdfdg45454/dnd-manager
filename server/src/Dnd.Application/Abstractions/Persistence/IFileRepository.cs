using Dnd.Domain.Files;

namespace Dnd.Application.Abstractions.Persistence;

public interface IFileRepository
{
    /// <summary>Tracked file, or null.</summary>
    Task<StoredFile?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Read-only files by id (missing ones are skipped).</summary>
    Task<IReadOnlyList<StoredFile>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Storage paths of every file of the campaign.</summary>
    Task<IReadOnlyList<string>> ListStoragePathsByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>True when a map, lore entry (cover or attachment), library document, character portrait or app release uses the file.</summary>
    Task<bool> IsReferencedAsync(Guid fileId, CancellationToken cancellationToken = default);

    void Add(StoredFile file);

    void Remove(StoredFile file);
}
