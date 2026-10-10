using OpenTrpg.Core.Application.Files;
using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Library;

namespace OpenTrpg.Core.Application.Library;

/// <param name="Url">Relative download URL of the PDF: <c>/api/v1/files/{fileId}</c>.</param>
/// <param name="Note">DM's note; only set in the recommended documents of a campaign.</param>
public sealed record LibraryDocumentDto(
    Guid Id,
    string Title,
    string? Description,
    string Category,
    Guid FileId,
    string Url,
    string FileName,
    long SizeBytes,
    int? PageCount,
    bool IsSystem,
    DateTimeOffset CreatedAt,
    string? Note)
{
    public static LibraryDocumentDto From(LibraryDocument document, StoredFile file, string? note = null) => new(
        document.Id,
        document.Title,
        document.Description,
        document.Category.ToString(),
        document.FileId,
        FileUrls.For(document.FileId),
        file.FileName,
        file.SizeBytes,
        document.PageCount,
        document.IsSystem,
        document.CreatedAt,
        note);
}
