using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace Dnd.Application.Files;

/// <summary>
/// Removes files nothing points at any more (a deleted map, a replaced portrait...). Call it after
/// the change that dropped the reference has been saved. System files are never removed.
/// </summary>
public sealed class FileCleanup(IFileRepository files, IFileStorage storage, IUnitOfWork unitOfWork, ILogger<FileCleanup> logger)
{
    public async Task ReleaseAsync(IEnumerable<Guid?> fileIds, CancellationToken cancellationToken = default)
    {
        var paths = new List<string>();
        foreach (var id in fileIds.OfType<Guid>().Distinct())
        {
            var file = await files.GetAsync(id, cancellationToken);
            if (file is null || file.OwnerUserId is null || await files.IsReferencedAsync(id, cancellationToken))
            {
                continue;
            }

            files.Remove(file);
            paths.Add(file.StoragePath);
        }

        if (paths.Count == 0)
        {
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await DeleteFromDiskAsync(paths);
    }

    /// <summary>Deletes stored bytes, ignoring failures (the rows are already gone; an orphan on disk is harmless).</summary>
    public async Task DeleteFromDiskAsync(IEnumerable<string> storagePaths)
    {
        foreach (var path in storagePaths)
        {
            try
            {
                await storage.DeleteAsync(path, CancellationToken.None);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Could not delete stored file {StoragePath}.", path);
            }
        }
    }
}
