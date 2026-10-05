using Dnd.Api.Filters;
using Dnd.Application.Users;

namespace Dnd.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users")
            .WithTags("Users")
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/search", async ([AsParameters] SearchUsersQuery query, SearchUsersHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(query, ct)))
            .WithName("SearchUsers")
            .WithSummary("Busca usuarios activos por email o nombre (mínimo 2 caracteres).")
            .ProducesValidationProblem();

        return app;
    }
}
