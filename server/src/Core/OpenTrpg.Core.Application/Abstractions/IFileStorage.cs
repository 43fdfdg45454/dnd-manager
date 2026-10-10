namespace OpenTrpg.Core.Application.Abstractions;

/// <summary>Where a file was stored and what was measured while writing it.</summary>
/// <param name="StoragePath">Path relative to the storage root, with forward slashes.</param>
/// <param name="Sha256">Lowercase hexadecimal SHA-256 of the content.</param>
public sealed record SavedFile(string StoragePath, long SizeBytes, string Sha256);

/// <summary>Binary storage of uploaded files (a directory on disk in the default implementation).</summary>
public interface IFileStorage
{
    /// <summary>Maximum size of an uploaded file, in bytes.</summary>
    long MaxUploadBytes { get; }

    /// <summary>
    /// Writes the content as <c>&lt;yyyy&gt;/&lt;MM&gt;/&lt;fileId&gt;&lt;extension&gt;</c> under the storage root and
    /// returns its location, size and SHA-256. Nothing is left behind when the write fails.
    /// </summary>
    Task<SavedFile> SaveAsync(Guid fileId, string extension, Stream content, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Opens the stored file for reading (seekable), or null when it does not exist.</summary>
    Stream? OpenRead(string storagePath);

    /// <summary>Deletes the stored file; a missing file is not an error.</summary>
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
