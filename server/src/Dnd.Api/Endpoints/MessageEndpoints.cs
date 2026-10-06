using System.Security.Claims;
using Dnd.Api.Auth;
using Dnd.Api.Filters;
using Dnd.Application.Messages;

namespace Dnd.Api.Endpoints;

/// <summary>Secret messages from a DM to the players of some characters.</summary>
public static class MessageEndpoints
{
    public static IEndpointRouteBuilder MapMessageEndpoints(this IEndpointRouteBuilder app)
    {
        var campaigns = app.MapGroup("/api/v1/campaigns/{campaignId:guid}/messages")
            .WithTags("Messages")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        campaigns.MapPost("", async (Guid campaignId, SendMessageRequest request, ClaimsPrincipal user, SendMessageHandler handler, CancellationToken ct) =>
                TypedResults.Created($"/api/v1/campaigns/{campaignId}/messages", await handler.HandleAsync(user.GetUserId(), campaignId, request, ct)))
            .WithName("SendMessage")
            .WithSummary("Envía un mensaje secreto (markdown) al jugador de cada personaje indicado: un mensaje por personaje. Requiere al menos DM.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        campaigns.MapGet("", async (Guid campaignId, [AsParameters] ListMessagesQuery query, ClaimsPrincipal user, ListMessagesHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, query, ct)))
            .WithName("ListMessages")
            .WithSummary("Mensajes secretos, los más recientes primero: el DM ve los que ha enviado; el jugador, los que ha recibido.")
            .ProducesValidationProblem();

        campaigns.MapGet("/unread-count", async (Guid campaignId, ClaimsPrincipal user, GetUnreadCountHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, ct)))
            .WithName("GetUnreadMessageCount")
            .WithSummary("Número de mensajes secretos sin leer del usuario en la campaña (0 para los DMs).");

        app.MapPost("/api/v1/messages/{id:guid}/read", async (Guid id, ClaimsPrincipal user, MarkMessageReadHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithTags("Messages")
            .RequireAuthorization()
            .WithName("MarkMessageRead")
            .WithSummary("Marca un mensaje como leído. Solo su destinatario.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
