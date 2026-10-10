using OpenTrpg.Core.Application.Common;

namespace OpenTrpg.Core.Application.Files;

public static class FileErrors
{
    /// <summary>Also used for users who are not members of the campaign the file belongs to.</summary>
    public static AppException FileNotFound() => AppException.NotFound("Fichero no encontrado.");
}

/// <summary>Where the clients download a stored file from.</summary>
public static class FileUrls
{
    public static string For(Guid fileId) => $"/api/v1/files/{fileId}";

    public static string? For(Guid? fileId) => fileId is { } id ? For(id) : null;
}
