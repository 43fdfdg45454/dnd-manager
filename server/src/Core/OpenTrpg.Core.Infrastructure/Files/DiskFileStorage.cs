using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using OpenTrpg.Core.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace OpenTrpg.Core.Infrastructure.Files;

/// <summary>
/// Stores files under <see cref="FileStorageOptions.RootPath"/> as <c>yyyy/MM/&lt;id&gt;&lt;ext&gt;</c>.
/// Stored paths are relative to the root and are checked to stay inside it.
/// </summary>
internal sealed class DiskFileStorage(IOptions<FileStorageOptions> options) : IFileStorage
{
    private const int BufferSize = 81_920;

    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public long MaxUploadBytes => options.Value.MaxUploadMegabytes * 1024L * 1024L;

    public async Task<SavedFile> SaveAsync(Guid fileId, string extension, Stream content, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var utc = now.UtcDateTime;
        var relative = string.Create(CultureInfo.InvariantCulture, $"{utc.Year:0000}/{utc.Month:00}/{fileId:D}{extension}");
        var path = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                int read;
                while ((read = await content.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    size += read;
                }
            }
        }
        catch
        {
            TryDelete(path);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        return new SavedFile(relative, size, Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    public Stream? OpenRead(string storagePath)
    {
        var path = Resolve(storagePath);
        return File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true)
            : null;
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        File.Delete(Resolve(storagePath));
        return Task.CompletedTask;
    }

    private string Resolve(string storagePath)
    {
        var full = Path.GetFullPath(Path.Combine(_root, storagePath));
        return full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? full
            : throw new InvalidOperationException("The storage path is outside the storage root.");
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Nothing more to do: the caller reports the original failure.
        }
    }
}
