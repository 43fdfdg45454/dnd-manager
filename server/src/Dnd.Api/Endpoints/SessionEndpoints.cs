using System.Security.Claims;
using Dnd.Api.Auth;
using Dnd.Api.Filters;
using Dnd.Application.Sessions;

namespace Dnd.Api.Endpoints;

/// <summary>Calendar of sessions, attendance, emailed notices and the campaign journal.</summary>
public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var campaigns = app.MapGroup("/api/v1/campaigns/{campaignId:guid}")
            .WithTags("Sessions")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        campaigns.MapGet("/sessions", async (Guid campaignId, [AsParameters] ListSessionsQuery query, ClaimsPrincipal user, ListSessionsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, query, ct)))
            .WithName("ListSessions")
            .WithSummary("Sesiones de la campaña por fecha. Por defecto solo las próximas; includePast=true añade las pasadas y from fija el límite inferior. to es exclusivo.")
            .ProducesValidationProblem();

        campaigns.MapPost("/sessions", async (Guid campaignId, CreateSessionRequest request, ClaimsPrincipal user, CreateSessionHandler handler, CancellationToken ct) =>
            {
                var session = await handler.HandleAsync(user.GetUserId(), campaignId, request, ct);
                return TypedResults.Created($"/api/v1/sessions/{session.Id}", session);
            })
            .WithName("CreateSession")
            .WithSummary("Programa una sesión (número correlativo por campaña) y genera sus recordatorios. Requiere al menos DM. 409 si dos creaciones simultáneas chocan: reintentar.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        campaigns.MapGet("/journal", async (Guid campaignId, [AsParameters] JournalQuery query, ClaimsPrincipal user, GetJournalHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), campaignId, query, ct)))
            .WithName("GetJournal")
            .WithSummary("Diario: sesiones con resumen en orden cronológico (más antigua primero), paginado. Los jugadores no ven las canceladas.")
            .ProducesValidationProblem();

        var group = app.MapGroup("/api/v1/sessions/{id:guid}")
            .WithTags("Sessions")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", async (Guid id, ClaimsPrincipal user, GetSessionHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, ct)))
            .WithName("GetSession")
            .WithSummary("Sesión con las respuestas de asistencia; los recordatorios solo los ve un DM.");

        group.MapPatch("", async (Guid id, UpdateSessionRequest request, ClaimsPrincipal user, UpdateSessionHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("UpdateSession")
            .WithSummary("Edición parcial; cambiar startsAt o status regenera los recordatorios pendientes. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapDelete("", async (Guid id, ClaimsPrincipal user, DeleteSessionHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DeleteSession")
            .WithSummary("Borra la sesión con sus respuestas y recordatorios. Requiere al menos DM.");

        group.MapPut("/rsvp", async (Guid id, RsvpRequest request, ClaimsPrincipal user, RespondToSessionHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("RespondToSession")
            .WithSummary("Responde Yes, No o Maybe a la sesión (cualquier miembro). 409 si está cancelada.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/summary", async (Guid id, SetSessionSummaryRequest request, ClaimsPrincipal user, SetSessionSummaryHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("SetSessionSummary")
            .WithSummary("Escribe el resumen (markdown) de la sesión para el diario; vacío lo borra. Requiere al menos DM.")
            .ProducesValidationProblem();

        group.MapPost("/notify", async (Guid id, NotifySessionRequest request, ClaimsPrincipal user, NotifySessionHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.GetUserId(), id, request, ct);
                return TypedResults.Accepted((string?)null);
            })
            .WithName("NotifySession")
            .WithSummary("Envía ahora un correo con texto libre a los miembros con las notificaciones activadas. Requiere al menos DM.")
            .ProducesValidationProblem();

        app.MapGet("/api/v1/me/sessions", async ([AsParameters] MySessionsQuery query, ClaimsPrincipal user, ListMySessionsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), query, ct)))
            .WithTags("Sessions")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .WithName("ListMySessions")
            .WithSummary("Próximas sesiones programadas de todas mis campañas. from fija el límite inferior y to es exclusivo.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
