using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Api.Filters;
using OpenTrpg.Core.Application.Items;

namespace OpenTrpg.Core.Api.Endpoints;

/// <summary>Party stash of a campaign: shared loot and gold.</summary>
public static class PartyStashEndpoints
{
    public static IEndpointRouteBuilder MapPartyStashEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/campaigns/{campaignId:guid}/stash")
            .WithTags("Party stash")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", async (Guid campaignId, ClaimsPrincipal user, GetStashHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, ct)))
            .WithName("GetPartyStash")
            .WithSummary("Alijo del grupo: oro común y objetos que aún no son de nadie. Cualquier miembro.");

        group.MapPost("/items", async (Guid campaignId, AddStashItemRequest request, ClaimsPrincipal user, AddStashItemHandler handler, CancellationToken ct) =>
            {
                var item = await handler.HandleAsync(user.GetUserId(), campaignId, request, ct);
                return TypedResults.Created($"/api/v1/campaigns/{campaignId}/stash/items/{item.Id}", item);
            })
            .WithName("AddPartyStashItem")
            .WithSummary("Añade botín al alijo (objeto del catálogo de la campaña y/o personalizado). Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapPatch("/items/{itemId:guid}", async (Guid campaignId, Guid itemId, UpdateStashItemRequest request, ClaimsPrincipal user, UpdateStashItemHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, itemId, request, ct)))
            .WithName("UpdatePartyStashItem")
            .WithSummary("Cambia la cantidad o las notas de un objeto del alijo. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapDelete("/items/{itemId:guid}", async (Guid campaignId, Guid itemId, ClaimsPrincipal user, DeleteStashItemHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), campaignId, itemId, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeletePartyStashItem")
            .WithSummary("Quita un objeto del alijo. Requiere al menos DM.");

        group.MapPost("/items/{itemId:guid}/take", async (Guid campaignId, Guid itemId, TakeStashItemRequest request, ClaimsPrincipal user, TakeStashItemHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, itemId, request, ct)))
            .WithName("TakePartyStashItem")
            .WithSummary("Pasa unidades del alijo al inventario de un personaje: el jugador con uno propio si la campaña lo permite; el DM con cualquiera. Devuelve el alijo.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/items/return", async (Guid campaignId, ReturnStashItemRequest request, ClaimsPrincipal user, ReturnStashItemHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, request, ct)))
            .WithName("ReturnPartyStashItem")
            .WithSummary("Devuelve unidades de un objeto del inventario al alijo (mismos permisos que tomar; no sintonizados). Devuelve el alijo.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/gold", async (Guid campaignId, StashGoldRequest request, ClaimsPrincipal user, StashGoldHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, request, ct)))
            .WithName("AdjustPartyStashGold")
            .WithSummary("Añade (deltaCp positivo) o retira (negativo, sin bajar de 0) oro común en piezas de cobre. Requiere al menos DM.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/gold/split", async (Guid campaignId, SplitStashGoldRequest? request, ClaimsPrincipal user, SplitStashGoldHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, request ?? new SplitStashGoldRequest(), ct)))
            .WithName("SplitPartyStashGold")
            .WithSummary("Reparte el oro común a partes iguales (en pc) entre los personajes activos o los indicados; el resto queda en el alijo. Requiere al menos DM.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
