using OpenTrpg.Core.Application.Systems;

namespace OpenTrpg.Core.Api.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/systems")
            .WithTags("Systems")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/", (ListSystemsHandler handler) => TypedResults.Ok(handler.Handle()))
            .WithName("ListGameSystems")
            .WithSummary("Sistemas de juego registrados en la instancia (id, nombre, versión y cuál es el predeterminado).");

        return app;
    }
}
