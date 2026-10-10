using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Api.Filters;
using OpenTrpg.Core.Application.Lore;

namespace OpenTrpg.Core.Api.Endpoints;

/// <summary>Campaign lore: a tree of entries with markdown content and attachments.</summary>
public static class LoreEndpoints
{
    public static IEndpointRouteBuilder MapLoreEndpoints(this IEndpointRouteBuilder app)
    {
        var campaigns = app.MapGroup("/api/v1/campaigns/{campaignId:guid}/lore")
            .WithTags("Lore")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        campaigns.MapGet("", async (Guid campaignId, [AsParameters] ListLoreQuery query, ClaimsPrincipal user, ListLoreHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, query, ct)))
            .WithName("ListLore")
            .WithSummary("Entradas de lore de la campaña en una lista plana (parentId arma el árbol). Los jugadores solo ven las de visibilidad Players.")
            .ProducesValidationProblem();

        campaigns.MapPost("", async (Guid campaignId, CreateLoreRequest request, ClaimsPrincipal user, CreateLoreHandler handler, CancellationToken ct) =>
            {
                var entry = await handler.HandleAsync(user.GetUserId(), campaignId, request, ct);
                return TypedResults.Created($"/api/v1/lore/{entry.Id}", entry);
            })
            .WithName("CreateLoreEntry")
            .WithSummary("Crea una entrada; el slug sale del título (con sufijo numérico si ya existe). Requiere al menos DM.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var group = app.MapGroup("/api/v1/lore/{id:guid}")
            .WithTags("Lore")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", async (Guid id, ClaimsPrincipal user, GetLoreHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("GetLoreEntry")
            .WithSummary("Entrada con sus adjuntos. 404 para jugadores si es DmOnly.");

        group.MapPatch("", async (Guid id, UpdateLoreRequest request, ClaimsPrincipal user, UpdateLoreHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("UpdateLoreEntry")
            .WithSummary("Edición parcial (parentId: null la pasa a la raíz). El slug no cambia. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapDelete("", async (Guid id, ClaimsPrincipal user, DeleteLoreHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteLoreEntry")
            .WithSummary("Borra la entrada y sus adjuntos; sus hijas pasan a la raíz. Requiere al menos DM.");

        group.MapPost("/attachments", async (Guid id, AddLoreAttachmentRequest request, ClaimsPrincipal user, AddLoreAttachmentHandler handler, CancellationToken ct) =>
                TypedResults.Created((string?)null, await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("AddLoreAttachment")
            .WithSummary("Adjunta un fichero LoreAttachment de la campaña. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapDelete("/attachments/{attachmentId:guid}", async (Guid id, Guid attachmentId, ClaimsPrincipal user, DeleteLoreAttachmentHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, attachmentId, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteLoreAttachment")
            .WithSummary("Quita un adjunto. Requiere al menos DM.");

        return app;
    }
}
