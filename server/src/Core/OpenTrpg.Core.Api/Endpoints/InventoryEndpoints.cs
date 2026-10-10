using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Api.Filters;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Items;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OpenTrpg.Core.Application.Common;

namespace OpenTrpg.Core.Api.Endpoints;

/// <summary>Inventory and money of a character.</summary>
public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/characters/{id:guid}")
            .WithTags("Inventory")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/inventory", async (Guid id, ClaimsPrincipal user, GetInventoryHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("GetInventory")
            .WithSummary("Inventario del personaje con dinero, peso total, capacidad de carga y objetos sintonizados. Dueño y DMs.");

        group.MapPost("/inventory", async Task<Results<Created<CharacterItemDto>, Accepted<ChangeRequestDto>>> (
                Guid id, AddInventoryItemRequest request, ClaimsPrincipal user, AddInventoryItemHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(user.GetUserId(), id, request, ct);
                return result.ChangeRequest is { } changeRequest
                    ? TypedResults.Accepted($"/api/v1/change-requests/{changeRequest.Id}", changeRequest)
                    : TypedResults.Created($"/api/v1/characters/{id}/inventory/{result.Item!.Id}", result.Item);
            })
            .WithName("AddInventoryItem")
            .WithSummary("Añade un objeto: 201 si se aplica (DM o dueño en borrador); 202 con la solicitud AddItem/CustomItem si el personaje está activo.")
            .ProducesValidationProblem();

        group.MapPatch("/inventory/{itemId:guid}", async (Guid id, Guid itemId, UpdateInventoryItemRequest request, ClaimsPrincipal user, UpdateInventoryItemHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, itemId, request, ct)))
            .WithName("UpdateInventoryItem")
            .WithSummary("Equipa, sintoniza (máx. 3), anota, reordena o fija las cargas de un objeto, sin aprobación (dueño o DM).")
            .ProducesValidationProblem();

        group.MapPost("/inventory/{itemId:guid}/use", async (Guid id, Guid itemId, AmountRequest? request, ClaimsPrincipal user, UseInventoryItemHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, itemId, request, ct)))
            .WithName("UseInventoryItem")
            .WithSummary("Usa un objeto: resta cargas o cantidad (un consumible agotado desaparece). Devuelve el inventario.")
            .ProducesValidationProblem();

        group.MapDelete("/inventory/{itemId:guid}", async Task<Results<NoContent, Accepted<ChangeRequestDto>>> (
                Guid id,
                Guid itemId,
                [FromBody] RemoveInventoryItemRequest? body,
                int? quantity,
                ClaimsPrincipal user,
                RemoveInventoryItemHandler handler,
                CancellationToken ct) =>
            {
                // The quantity travels in the body ({ quantity }); ?quantity= is accepted for clients that cannot send a DELETE body.
                var request = body ?? (quantity is null ? null : new RemoveInventoryItemRequest(quantity));
                var changeRequest = await handler.HandleAsync(user.GetUserId(), id, itemId, request, ct);
                return changeRequest is null
                    ? TypedResults.NoContent()
                    : TypedResults.Accepted($"/api/v1/change-requests/{changeRequest.Id}", changeRequest);
            })
            .WithName("RemoveInventoryItem")
            .WithSummary("Quita unidades de un objeto (todas si no se indica quantity): 204 si se aplica (DM o borrador); 202 con la solicitud RemoveItem si el personaje está activo.")
            .ProducesValidationProblem();

        group.MapPost("/money", async Task<Results<Ok<InventoryDto>, Accepted<ChangeRequestDto>>> (
                Guid id, AdjustMoneyRequest request, ClaimsPrincipal user, AdjustMoneyHandler handler, CancellationToken ct) =>
            {
                var result = await handler.HandleAsync(user.GetUserId(), id, request, ct);
                return result.ChangeRequest is { } changeRequest
                    ? TypedResults.Accepted($"/api/v1/change-requests/{changeRequest.Id}", changeRequest)
                    : TypedResults.Ok(result.Inventory!);
            })
            .WithName("AdjustMoney")
            .WithSummary("Ajusta el dinero (deltaCp en piezas de cobre): 200 con el inventario si se aplica (DM o borrador); 202 con la solicitud AdjustMoney si el personaje está activo.")
            .ProducesValidationProblem();

        return app;
    }
}
