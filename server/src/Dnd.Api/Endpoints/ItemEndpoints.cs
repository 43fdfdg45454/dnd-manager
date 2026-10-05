using System.Security.Claims;
using Dnd.Api.Auth;
using Dnd.Api.Filters;
using Dnd.Application.Items;

namespace Dnd.Api.Endpoints;

/// <summary>Item catalog of a campaign: SRD items plus the campaign's homebrew.</summary>
public static class ItemEndpoints
{
    public static IEndpointRouteBuilder MapItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/campaigns/{campaignId:guid}/items")
            .WithTags("Items")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", async (Guid campaignId, [AsParameters] SearchCampaignItemsQuery query, ClaimsPrincipal user, SearchCampaignItemsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, query, ct)))
            .WithName("SearchCampaignItems")
            .WithSummary("Objetos usables en la campaña (SRD y homebrew) paginados, con búsqueda, categoría, rareza y origen (source=all|srd|homebrew).")
            .ProducesValidationProblem();

        group.MapGet("/{templateId:guid}", async (Guid campaignId, Guid templateId, ClaimsPrincipal user, GetCampaignItemHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, templateId, ct)))
            .WithName("GetCampaignItem")
            .WithSummary("Detalle de un objeto del SRD o del homebrew de la campaña.");

        group.MapPost("", async (Guid campaignId, ItemTemplateInput input, ClaimsPrincipal user, CreateHomebrewItemHandler handler, CancellationToken ct) =>
            {
                var item = await handler.HandleAsync(user.GetUserId(), campaignId, input, ct);
                return TypedResults.Created($"/api/v1/campaigns/{campaignId}/items/{item.Id}", item);
            })
            .WithName("CreateHomebrewItem")
            .WithSummary("Crea un objeto homebrew de la campaña. Requiere al menos DM.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPatch("/{templateId:guid}", async (Guid campaignId, Guid templateId, ItemTemplatePatch patch, ClaimsPrincipal user, UpdateHomebrewItemHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, templateId, patch, ct)))
            .WithName("UpdateHomebrewItem")
            .WithSummary("Edita un objeto homebrew de la campaña (solo los campos enviados). Requiere al menos DM.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapDelete("/{templateId:guid}", async (Guid campaignId, Guid templateId, ClaimsPrincipal user, DeleteHomebrewItemHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), campaignId, templateId, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteHomebrewItem")
            .WithSummary("Borra un objeto homebrew. 409 si está en algún inventario o tienda. Requiere al menos DM.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
