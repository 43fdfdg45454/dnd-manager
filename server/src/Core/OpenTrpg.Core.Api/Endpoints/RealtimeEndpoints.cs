using System.Security.Claims;
using OpenTrpg.Core.Api.Auth;
using OpenTrpg.Core.Api.Realtime;

namespace OpenTrpg.Core.Api.Endpoints;

/// <summary>Realtime diagnostics for the app's "Probar conexión".</summary>
public static class RealtimeEndpoints
{
    public static IEndpointRouteBuilder MapRealtimeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/realtime/status", (ClaimsPrincipal user, ConnectionTracker tracker) =>
                TypedResults.Ok(tracker.GetStatus(user.GetUserId())))
            .WithTags("Realtime")
            .RequireAuthorization()
            .WithName("GetRealtimeStatus")
            .WithSummary("Conexiones abiertas del usuario al hub de tiempo real y transporte de la última (WebSockets, ServerSentEvents o LongPolling).")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }
}
