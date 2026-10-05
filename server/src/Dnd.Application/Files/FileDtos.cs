using Dnd.Domain.Files;

namespace Dnd.Application.Files;

/// <param name="Url">Relative download URL: <c>/api/v1/files/{id}</c>.</param>
public sealed record StoredFileDto(Guid Id, string FileName, string ContentType, long SizeBytes, string Url)
{
    public static StoredFileDto From(StoredFile file) => new(file.Id, file.FileName, file.ContentType, file.SizeBytes, FileUrls.For(file.Id));
}

/// <summary>A file ready to be streamed to a client. The caller disposes <see cref="Content"/> (seekable).</summary>
/// <param name="ETag">Strong entity tag (quoted), derived from the SHA-256 of the content.</param>
public sealed record FileDownload(Stream Content, string ContentType, string FileName, string ETag, DateTimeOffset LastModified);
