using System.Security.Claims;
using Dnd.Api.Auth;
using Dnd.Api.Filters;
using Dnd.Application.Users;

namespace Dnd.Api.Endpoints;

public static class AdminUserEndpoints
{
    public static IEndpointRouteBuilder MapAdminUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/users")
            .WithTags("Admin")
            .RequireAuthorization(AuthPolicies.Admin)
            .AddEndpointFilter<ValidationFilter>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("", async ([AsParameters] ListUsersQuery query, ListUsersHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(query, ct)))
            .WithName("ListUsers")
            .WithSummary("Lista paginada de usuarios con búsqueda por email o nombre.")
            .ProducesValidationProblem();

        group.MapPost("", async (CreateUserRequest request, CreateUserHandler handler, CancellationToken ct) =>
                TypedResults.Created((string?)null, await handler.HandleAsync(request, ct)))
            .WithName("CreateUser")
            .WithSummary("Crea un usuario y le envía el correo de alta.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}", async (Guid id, UpdateUserRequest request, ClaimsPrincipal user, UpdateUserHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(user.GetUserId(), id, request, ct)))
            .WithName("UpdateUser")
            .WithSummary("Cambia nombre, rol o estado de un usuario.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/setup-email", async (Guid id, ResendSetupEmailHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);
                return TypedResults.Accepted((string?)null);
            })
            .WithName("ResendSetupEmail")
            .WithSummary("Reenvía el correo de alta con un token nuevo (invalida el anterior).")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
