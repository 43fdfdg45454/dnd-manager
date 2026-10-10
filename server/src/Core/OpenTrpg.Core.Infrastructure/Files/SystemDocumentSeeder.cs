using System.Security.Cryptography;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Files;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Library;
using OpenTrpg.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenTrpg.Core.Infrastructure.Files;

/// <summary>
/// Registers the freely licensed documents of the game systems (<see cref="GameSystemInfo.SystemDocuments"/>, for
/// example the SRD 5.1 PDF under CC-BY 4.0) as system library documents when the operator has placed them at
/// <c>&lt;RootPath&gt;/system/&lt;FileName&gt;</c>. Idempotent: a file already registered under that path is left
/// alone. Without the file nothing happens (the admin can upload it instead).
/// </summary>
public sealed class SystemDocumentSeeder(
    AppDbContext db,
    IGameSystemRegistry systems,
    IOptions<FileStorageOptions> options,
    IDateTimeProvider clock,
    ILogger<SystemDocumentSeeder> logger)
{
    public const string Folder = "system";

    public static string StoragePathOf(SystemDocumentInfo document) => $"{Folder}/{document.FileName}";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var document in systems.All.SelectMany(s => s.Info.SystemDocuments))
        {
            await SeedAsync(document, cancellationToken);
        }
    }

    private async Task SeedAsync(SystemDocumentInfo document, CancellationToken cancellationToken)
    {
        var storagePath = StoragePathOf(document);
        var path = Path.Combine(Path.GetFullPath(options.Value.RootPath), Folder, document.FileName);
        if (!File.Exists(path))
        {
            return;
        }

        if (await db.StoredFiles.AnyAsync(f => f.StoragePath == storagePath, cancellationToken))
        {
            return;
        }

        string hash;
        long size;
        await using (var stream = File.OpenRead(path))
        {
            var head = new byte[5];
            if (await stream.ReadAtLeastAsync(head, head.Length, throwOnEndOfStream: false, cancellationToken) < head.Length || !head.AsSpan().SequenceEqual("%PDF-"u8))
            {
                logger.LogWarning("The system document {StoragePath} is not a PDF and was not registered.", storagePath);
                return;
            }

            stream.Position = 0;
            size = stream.Length;
            hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        }

        var now = clock.UtcNow;
        var file = StoredFile.Create(Guid.NewGuid(), null, null, document.FileName, FileContent.Pdf, size, hash, FileKind.LibraryDocument, storagePath, null, null, now);
        var entry = LibraryDocument.Create(document.Title, document.Description, LibraryCategory.Rules, file.Id, isSystem: true, uploadedByUserId: null, now);
        db.StoredFiles.Add(file);
        db.LibraryDocuments.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Registered the system document {Title}.", document.Title);
    }
}
