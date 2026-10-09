using System.Security.Claims;
using Dnd.Api.Auth;
using Dnd.Api.Filters;
using Dnd.Application.Items;

namespace Dnd.Api.Endpoints;

/// <summary>Campaign shops, their stock, purchases, sales and the transaction log.</summary>
public static class ShopEndpoints
{
    public static IEndpointRouteBuilder MapShopEndpoints(this IEndpointRouteBuilder app)
    {
        var campaigns = app.MapGroup("/api/v1/campaigns/{campaignId:guid}")
            .WithTags("Shops")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        campaigns.MapGet("/shops", async (Guid campaignId, ClaimsPrincipal user, ListShopsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, ct)))
            .WithName("ListShops")
            .WithSummary("Tiendas de la campaña. Los jugadores solo ven las abiertas.");

        campaigns.MapPost("/shops", async (Guid campaignId, CreateShopRequest request, ClaimsPrincipal user, CreateShopHandler handler, CancellationToken ct) =>
            {
                var shop = await handler.HandleAsync(user.GetUserId(), campaignId, request, ct);
                return TypedResults.Created($"/api/v1/shops/{shop.Id}", shop);
            })
            .WithName("CreateShop")
            .WithSummary("Crea una tienda (cerrada). Requiere al menos DM.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        campaigns.MapGet("/transactions", async (Guid campaignId, [AsParameters] ListTransactionsQuery query, ClaimsPrincipal user, ListTransactionsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, query, ct)))
            .WithName("ListTransactions")
            .WithSummary("Compras y ventas de la campaña, las más recientes primero (DM: todas; jugador: las de sus personajes).")
            .ProducesValidationProblem();

        var group = app.MapGroup("/api/v1/shops/{id:guid}")
            .WithTags("Shops")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", async (Guid id, ClaimsPrincipal user, GetShopHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("GetShop")
            .WithSummary("Tienda con sus objetos efectivos, precios y stock. 404 para jugadores si está cerrada.");

        group.MapPatch("", async (Guid id, UpdateShopRequest request, ClaimsPrincipal user, UpdateShopHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("UpdateShop")
            .WithSummary("Cambia nombre, descripción, apertura o porcentaje de recompra. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapDelete("", async (Guid id, ClaimsPrincipal user, DeleteShopHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteShop")
            .WithSummary("Borra la tienda con sus objetos y su historial de operaciones. Requiere al menos DM.");

        group.MapPost("/items", async (Guid id, AddShopItemRequest request, ClaimsPrincipal user, AddShopItemHandler handler, CancellationToken ct) =>
                TypedResults.Created((string?)null, await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("AddShopItem")
            .WithSummary("Añade un objeto a la tienda (plantilla y/o overrides, precio en pc, stock opcional). Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapPost("/items/bulk", async (Guid id, AddShopItemsBulkRequest request, ClaimsPrincipal user, AddShopItemsBulkHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("AddShopItemsBulk")
            .WithSummary("Añade varios objetos del catálogo de una vez (todo o nada); sin precio usa el precio de lista. Devuelve la tienda. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapPatch("/items/{shopItemId:guid}", async (Guid id, Guid shopItemId, UpdateShopItemRequest request, ClaimsPrincipal user, UpdateShopItemHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, shopItemId, request, ct)))
            .WithName("UpdateShopItem")
            .WithSummary("Cambia precio, stock (null = ilimitado), overrides u orden de un objeto de la tienda. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapDelete("/items/{shopItemId:guid}", async (Guid id, Guid shopItemId, ClaimsPrincipal user, DeleteShopItemHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, shopItemId, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteShopItem")
            .WithSummary("Quita un objeto de la tienda. Requiere al menos DM.");

        group.MapPost("/buy", async (Guid id, BuyRequest request, ClaimsPrincipal user, BuyHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("BuyFromShop")
            .WithSummary("Compra atómica: 400 sin dinero o sin stock, 403 personaje ajeno, 409 tienda cerrada o compra concurrente.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/sell", async (Guid id, SellRequest request, ClaimsPrincipal user, SellHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("SellToShop")
            .WithSummary("Venta atómica al porcentaje de recompra de la tienda: 400 si está sintonizado o no hay cantidad suficiente, 409 tienda cerrada.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
