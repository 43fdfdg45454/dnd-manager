using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Files;
using OpenTrpg.Core.Domain.Users;
using Microsoft.Net.Http.Headers;

namespace OpenTrpg.Core.Api.Endpoints;

/// <summary>Multipart body of <c>POST /api/v1/files</c> (documentation only; the endpoint reads the form itself).</summary>
/// <param name="File">The content.</param>
/// <param name="Kind">MapImage | Portrait | LoreAttachment | LibraryDocument | AppRelease.</param>
/// <param name="CampaignId">Campaign of MapImage and LoreAttachment files.</param>
/// <param name="CharacterId">Character of Portrait files.</param>
public sealed record UploadFileForm(IFormFile File, string Kind, Guid? CampaignId, Guid? CharacterId);

/// <summary>Upload and download of stored files (map images, portraits, lore attachments, library PDFs, APK releases).</summary>
public static class FileEndpoints
{
    /// <summary>Room above the maximum file size for the rest of the multipart body (field values, boundaries).</summary>
    public const long MultipartOverheadBytes = 1024 * 1024;

    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/files")
            .WithTags("Files")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("", UploadAsync)
            .DisableAntiforgery()
            .Accepts<UploadFileForm>("multipart/form-data")
            .Produces<StoredFileDto>(StatusCodes.Status201Created)
            .WithName("UploadFile")
            .WithSummary("Sube un fichero (multipart: file, kind, campaignId?, characterId?). MapImage y LoreAttachment: DM; Portrait: dueño del personaje o DM; LibraryDocument y AppRelease: administrador.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        group.MapGet("/{id:guid}", DownloadAsync)
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status206PartialContent, contentType: "application/octet-stream")
            .WithName("DownloadFile")
            .WithSummary("Contenido del fichero con ETag y soporte de Range. Los de campaña, solo para miembros; los globales, para cualquier usuario.");

        return app;
    }

    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        IFileStorage storage,
        UploadFileHandler handler,
        CancellationToken cancellationToken)
    {
        var form = await ReadUploadFormAsync(request, storage, cancellationToken);
        var file = form.Files.GetFile("file") ?? throw AppException.Validation("file", "Falta el fichero.");
        var command = new UploadFileCommand(
            file.OpenReadStream(),
            file.FileName,
            form["kind"].ToString(),
            ParseGuid(form, "campaignId"),
            ParseGuid(form, "characterId"),
            user.IsInRole(nameof(UserRole.Admin)));

        await using (command.Content)
        {
            var stored = await handler.HandleAsync(user.GetUserId(), command, cancellationToken);
            return TypedResults.Created(FileUrls.For(stored.Id), stored);
        }
    }

    /// <summary>
    /// Reads a multipart upload, answering 413 when it exceeds the maximum upload size and 400 when the
    /// request is not a valid multipart form. Shared by the endpoints that accept a file.
    /// </summary>
    internal static async Task<IFormCollection> ReadUploadFormAsync(HttpRequest request, IFileStorage storage, CancellationToken cancellationToken)
    {
        var tooLarge = $"El fichero supera el tamaño máximo permitido ({storage.MaxUploadBytes / (1024 * 1024)} MB).";
        if (request.ContentLength > storage.MaxUploadBytes + MultipartOverheadBytes)
        {
            throw AppException.PayloadTooLarge(tooLarge);
        }

        if (!request.HasFormContentType)
        {
            throw AppException.Validation("file", "La petición debe ser multipart/form-data.");
        }

        try
        {
            return await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException) when (request.ContentLength is null)
        {
            // The multipart reader refused a part larger than its limit (set from the maximum upload size).
            throw AppException.PayloadTooLarge(tooLarge);
        }
        catch (InvalidDataException)
        {
            throw AppException.Validation("file", "El formulario no es válido.");
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            throw AppException.PayloadTooLarge(tooLarge);
        }
    }

    private static async Task<IResult> DownloadAsync(Guid id, ClaimsPrincipal user, HttpContext http, GetFileHandler handler, CancellationToken cancellationToken)
    {
        var file = await handler.HandleAsync(user.GetUserId(), id, cancellationToken);

        // Files never change once stored, so they can be cached by the client for a day.
        http.Response.Headers.CacheControl = "private, max-age=86400";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.File(
            file.Content,
            file.ContentType,
            lastModified: file.LastModified,
            entityTag: new EntityTagHeaderValue(file.ETag),
            enableRangeProcessing: true);
    }

    private static Guid? ParseGuid(IFormCollection form, string field)
    {
        var value = form[field].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Guid.TryParse(value, out var id) ? id : throw AppException.Validation(field, "El identificador no es válido.");
    }
}
