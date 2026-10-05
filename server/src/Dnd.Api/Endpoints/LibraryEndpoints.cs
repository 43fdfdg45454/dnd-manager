using System.Security.Claims;
using Dnd.Api.Auth;
using Dnd.Api.Filters;
using Dnd.Application.Library;

namespace Dnd.Api.Endpoints;

/// <summary>PDF library of the instance and the documents each campaign recommends.</summary>
public static class LibraryEndpoints
{
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        var library = app.MapGroup("/api/v1/library")
            .WithTags("Library")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        library.MapGet("", async ([AsParameters] ListLibraryQuery query, ListLibraryHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(query, ct)))
            .WithName("ListLibrary")
            .WithSummary("Documentos de la biblioteca de la instancia (cualquier usuario), con búsqueda y categoría.")
            .ProducesValidationProblem();

        library.MapPost("", async (CreateLibraryDocumentRequest request, ClaimsPrincipal user, CreateLibraryDocumentHandler handler, CancellationToken ct) =>
            {
                var document = await handler.HandleAsync(user.GetUserId(), request, ct);
                return TypedResults.Created($"/api/v1/library/{document.Id}", document);
            })
            .RequireAuthorization(AuthPolicies.Admin)
            .WithName("CreateLibraryDocument")
            .WithSummary("Publica en la biblioteca un PDF subido como LibraryDocument. Solo administradores.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        library.MapPatch("/{id:guid}", async (Guid id, UpdateLibraryDocumentRequest request, UpdateLibraryDocumentHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, request, ct)))
            .RequireAuthorization(AuthPolicies.Admin)
            .WithName("UpdateLibraryDocument")
            .WithSummary("Edita título, descripción o categoría. Solo administradores.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        library.MapDelete("/{id:guid}", async (Guid id, DeleteLibraryDocumentHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);
                return TypedResults.NoContent();
            })
            .RequireAuthorization(AuthPolicies.Admin)
            .WithName("DeleteLibraryDocument")
            .WithSummary("Borra el documento y su fichero. Los documentos del sistema no se borran (400). Solo administradores.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var campaign = app.MapGroup("/api/v1/campaigns/{campaignId:guid}/library")
            .WithTags("Library")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        campaign.MapGet("", async (Guid campaignId, ClaimsPrincipal user, ListCampaignLibraryHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, ct)))
            .WithName("ListCampaignLibrary")
            .WithSummary("Documentos que los DM recomiendan en la campaña, con su nota.");

        campaign.MapPut("/{documentId:guid}", async (Guid campaignId, Guid documentId, RecommendDocumentRequest? request, ClaimsPrincipal user, RecommendDocumentHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), campaignId, documentId, request ?? new RecommendDocumentRequest(), ct);
                return TypedResults.NoContent();
            })
            .WithName("RecommendLibraryDocument")
            .WithSummary("Recomienda un documento en la campaña o cambia su nota (cuerpo opcional). Requiere al menos DM.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        campaign.MapDelete("/{documentId:guid}", async (Guid campaignId, Guid documentId, ClaimsPrincipal user, RemoveRecommendationHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), campaignId, documentId, ct);
                return TypedResults.NoContent();
            })
            .WithName("RemoveLibraryRecommendation")
            .WithSummary("Quita la recomendación. Requiere al menos DM.")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
