using Dnd.Api.Filters;
using Dnd.Application.Sessions;

namespace Dnd.Api.Endpoints;

/// <summary>
/// Anonymous endpoints behind the link of the session emails: the signed <c>token</c> identifies the
/// member, so attendance can be answered without the app.
/// </summary>
public static class PublicSessionEndpoints
{
    public static IEndpointRouteBuilder MapPublicSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/public/sessions/{id:guid}")
            .WithTags("Public sessions")
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("", async (Guid id, string? token, GetPublicSessionHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, token, ct)))
            .WithName("GetPublicSession")
            .WithSummary("Sesión vista por el miembro al que se emitió el token del enlace. 401 si el token no es válido.");

        group.MapPost("/rsvp", async (Guid id, string? token, PublicRsvpRequest request, PublicRsvpHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, token, request, ct)))
            .WithName("RespondToPublicSession")
            .WithSummary("Responde Yes, No o Maybe en nombre del miembro del token. 401 si el token no es válido.")
            .ProducesValidationProblem();

        return app;
    }
}
