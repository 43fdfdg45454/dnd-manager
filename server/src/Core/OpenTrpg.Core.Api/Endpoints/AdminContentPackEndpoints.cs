using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.ContentPacks;

namespace OpenTrpg.Core.Api.Endpoints;

/// <summary>Multipart body of <c>POST /api/v1/admin/content-packs</c> (documentation only; the endpoint reads the request itself).</summary>
/// <param name="File">The pack JSON (docs/content-packs.md), at most 20 MB.</param>
public sealed record ImportContentPackForm(IFormFile File);

/// <summary>Administration of the private content packs of the instance (ADR 0007).</summary>
public static class AdminContentPackEndpoints
{
    public const string ValidationTitle = "El paquete de contenido no es válido.";

    public static IEndpointRouteBuilder MapAdminContentPackEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/content-packs")
            .WithTags("Admin")
            .RequireAuthorization(AuthPolicies.Admin)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("", async (ListContentPacksHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .WithName("ListContentPacks")
            .WithSummary("Paquetes de contenido de la instancia (el base de cada sistema primero): id, sistema, nombre, versión, versión de formato, isBase, fecha de importación, recuentos y requires.");

        group.MapPost("", ImportAsync)
            .DisableAntiforgery()
            .Accepts<ImportContentPackForm>("multipart/form-data", "application/json")
            .Produces<ContentPackImportResultDto>(StatusCodes.Status201Created)
            .WithName("ImportContentPack")
            .WithSummary("Importa o reemplaza un paquete de contenido (formato 3): multipart con file, o el JSON del paquete como cuerpo. No se activa en ninguna campaña. 400 con errors: lista de \"ruta: mensaje\".")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        group.MapDelete("/{id}", async (string id, DeleteContentPackHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteContentPack")
            .WithSummary("Borra el contenido del paquete y su activación en las campañas. Los personajes conservan sus índices y la ficha lo marca como no disponible (catalogMissing). 409 base-pack para el paquete base.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> ImportAsync(
        HttpRequest request,
        IFileStorage storage,
        ImportContentPackHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var content = await ReadPackAsync(request, storage, cancellationToken);
            var result = await handler.HandleAsync(content, cancellationToken);
            return TypedResults.Created("/api/v1/admin/content-packs", result);
        }
        catch (ContentPackInvalidException exception)
        {
            return Invalid(exception.Message, exception.Errors);
        }
        catch (AppException exception) when (exception.Kind == AppErrorKind.Validation)
        {
            // Same shape as the pack errors (a list of "path: message") for a missing file or a bad form.
            var errors = (exception.Errors ?? new Dictionary<string, string[]>())
                .SelectMany(e => e.Value.Select(message => $"{e.Key}: {message}"))
                .ToList();
            return Invalid(exception.Message, errors);
        }
    }

    private static IResult Invalid(string detail, IReadOnlyList<string> errors) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: ValidationTitle,
            detail: detail,
            extensions: new Dictionary<string, object?> { ["errors"] = errors });

    /// <summary>The pack from the <c>file</c> field of a multipart form or from a JSON body, buffered and size-checked.</summary>
    private static async Task<MemoryStream> ReadPackAsync(HttpRequest request, IFileStorage storage, CancellationToken cancellationToken)
    {
        if (request.HasJsonContentType())
        {
            if (request.ContentLength > ContentPackLimits.MaxFileBytes)
            {
                throw TooLarge();
            }

            return await BufferAsync(request.Body, cancellationToken);
        }

        var form = await FileEndpoints.ReadUploadFormAsync(request, storage, cancellationToken);
        var file = form.Files.GetFile("file") ?? throw AppException.Validation("file", "Falta el fichero del paquete.");
        if (file.Length > ContentPackLimits.MaxFileBytes)
        {
            throw TooLarge();
        }

        await using var stream = file.OpenReadStream();
        return await BufferAsync(stream, cancellationToken);
    }

    private static async Task<MemoryStream> BufferAsync(Stream source, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > ContentPackLimits.MaxFileBytes)
            {
                await buffer.DisposeAsync();
                throw TooLarge();
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        return buffer;
    }

    private static AppException TooLarge() =>
        AppException.PayloadTooLarge($"El paquete supera el tamaño máximo permitido ({ContentPackLimits.MaxFileBytes / (1024 * 1024)} MB).");
}
