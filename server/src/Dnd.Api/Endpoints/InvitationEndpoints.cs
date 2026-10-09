using System.Security.Claims;
using Dnd.Api.Auth;
using Dnd.Application.Campaigns;

namespace Dnd.Api.Endpoints;

/// <summary>Campaign invitations of the current user (the DM side lives in <see cref="CampaignEndpoints"/>).</summary>
public static class InvitationEndpoints
{
    public static IEndpointRouteBuilder MapInvitationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1")
            .WithTags("Invitations")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me/invitations", async (ClaimsPrincipal user, ListMyInvitationsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), ct)))
            .WithName("ListMyInvitations")
            .WithSummary("Invitaciones a campañas pendientes del usuario actual.");

        group.MapPost("/invitations/{id:guid}/accept", async (Guid id, ClaimsPrincipal user, AcceptInvitationHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("AcceptInvitation")
            .WithSummary("Acepta la invitación: el usuario entra en la campaña con el rol invitado.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/invitations/{id:guid}/decline", async (Guid id, ClaimsPrincipal user, DeclineInvitationHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeclineInvitation")
            .WithSummary("Rechaza la invitación.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
