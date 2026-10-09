using System.Security.Claims;
using Dnd.Api.Auth;
using Dnd.Api.Filters;
using Dnd.Application.Campaigns;
using Dnd.Application.Sessions;

namespace Dnd.Api.Endpoints;

public static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaignEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/campaigns")
            .WithTags("Campaigns")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("", async (ClaimsPrincipal user, ListMyCampaignsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), ct)))
            .WithName("ListMyCampaigns")
            .WithSummary("Campañas en las que participa el usuario, ordenadas por nombre.");

        group.MapPost("", async (CreateCampaignRequest request, ClaimsPrincipal user, CreateCampaignHandler handler, CancellationToken ct) =>
            {
                var campaign = await handler.HandleAsync(user.GetUserId(), request, ct);
                return TypedResults.Created($"/api/v1/campaigns/{campaign.Id}", campaign);
            })
            .WithName("CreateCampaign")
            .WithSummary("Crea una campaña; el creador queda como propietario (Owner).")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, GetCampaignHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("GetCampaign")
            .WithSummary("Detalle de la campaña con sus miembros. 404 si no eres miembro.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", async (Guid id, UpdateCampaignRequest request, ClaimsPrincipal user, UpdateCampaignHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("UpdateCampaign")
            .WithSummary("Cambia nombre o descripción. Requiere al menos DM.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/settings", async (Guid id, UpdateCampaignSettingsRequest request, ClaimsPrincipal user, UpdateCampaignSettingsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("UpdateCampaignSettings")
            .WithSummary("Cambia la zona horaria (IANA) y los recordatorios (minutos antes de cada sesión). Requiere al menos DM.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, DeleteCampaignHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteCampaign")
            .WithSummary("Elimina la campaña y todo su contenido. Solo el propietario.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/members", async (Guid id, ClaimsPrincipal user, ListMembersHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("ListCampaignMembers")
            .WithSummary("Miembros de la campaña con su rol.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest request, ClaimsPrincipal user, InviteMemberHandler handler, CancellationToken ct) =>
                TypedResults.Accepted((string?)null, await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("InviteCampaignMember")
            .WithSummary("Invita a un usuario activo como DM o Player; entra en la campaña cuando acepta. Al menos DM; invitar DMs solo el propietario.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/invitations", async (Guid id, ClaimsPrincipal user, ListCampaignInvitationsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("ListCampaignInvitations")
            .WithSummary("Invitaciones pendientes de la campaña. Al menos DM.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/invitations/{invitationId:guid}", async (Guid id, Guid invitationId, ClaimsPrincipal user, CancelInvitationHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, invitationId, ct);
                return TypedResults.NoContent();
            })
            .WithName("CancelCampaignInvitation")
            .WithSummary("Cancela una invitación pendiente. Al menos DM.")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, ChangeMemberRoleRequest request, ClaimsPrincipal user, ChangeMemberRoleHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, userId, request, ct)))
            .WithName("ChangeCampaignMemberRole")
            .WithSummary("Cambia el rol de un miembro entre DM y Player. Solo el propietario. 409 si el jugador que pasa a DM tiene personajes en la campaña.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}/members/{userId:guid}", async (Guid id, Guid userId, ClaimsPrincipal user, RemoveMemberHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, userId, ct);
                return TypedResults.NoContent();
            })
            .WithName("RemoveCampaignMember")
            .WithSummary("Quita a un miembro: el propietario a cualquiera salvo a sí mismo; un DM solo a jugadores.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/leave", async (Guid id, ClaimsPrincipal user, LeaveCampaignHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, ct);
                return TypedResults.NoContent();
            })
            .WithName("LeaveCampaign")
            .WithSummary("Sale de la campaña. El propietario debe transferirla antes.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/transfer-ownership", async (Guid id, TransferOwnershipRequest request, ClaimsPrincipal user, TransferOwnershipHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("TransferCampaignOwnership")
            .WithSummary("Transfiere la propiedad a otro miembro; el propietario saliente queda como DM o Player. 409 si el destinatario tiene personajes en la campaña.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
