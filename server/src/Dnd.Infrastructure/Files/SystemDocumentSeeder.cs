using System.Security.Cryptography;
using Dnd.Application.Abstractions;
using Dnd.Application.Files;
using Dnd.Application.Systems.Dnd5e;
using Dnd.Domain.Files;
using Dnd.Domain.Library;
using Dnd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dnd.Infrastructure.Files;

/// <summary>
/// Registers the SRD 5.1 PDF (CC-BY 4.0) as a system library document when the operator has placed
/// it at <c>&lt;RootPath&gt;/system/SRD_CC_v5.1.pdf</c>. Idempotent: a file already registered under
/// that path is left alone. Without the file nothing happens (the admin can upload it instead).
/// </summary>
public sealed class SystemDocumentSeeder(
    AppDbContext db,
    IOptions<FileStorageOptions> options,
    IDateTimeProvider clock,
    ILogger<SystemDocumentSeeder> logger)
{
    public const string SrdFileName = Dnd5eSystem.SrdFileName;
    public const string SrdStoragePath = "system/" + SrdFileName;
    public const string SrdTitle = Dnd5eSystem.SrdTitle;
    public const string SrdDescription = Dnd5eSystem.SrdDescription;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Path.GetFullPath(options.Value.RootPath), "system", SrdFileName);
        if (!File.Exists(path))
        {
            return;
        }

        if (await db.StoredFiles.AnyAsync(f => f.StoragePath == SrdStoragePath, cancellationToken))
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
                logger.LogWarning("The system document {StoragePath} is not a PDF and was not registered.", SrdStoragePath);
                return;
            }

            stream.Position = 0;
            size = stream.Length;
            hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        }

        var now = clock.UtcNow;
        var file = StoredFile.Create(Guid.NewGuid(), null, null, SrdFileName, FileContent.Pdf, size, hash, FileKind.LibraryDocument, SrdStoragePath, null, null, now);
        var document = LibraryDocument.Create(SrdTitle, SrdDescription, LibraryCategory.Rules, file.Id, isSystem: true, uploadedByUserId: null, now);
        db.StoredFiles.Add(file);
        db.LibraryDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Registered the system document {Title}.", SrdTitle);
    }
}
