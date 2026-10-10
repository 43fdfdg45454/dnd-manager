using OpenTrpg.Core.Api.Filters;
using OpenTrpg.Core.Application.Setup;

namespace OpenTrpg.Core.Api.Endpoints;

/// <summary>First-boot setup of the instance. Anonymous: it only works while there are no users.</summary>
public static class SetupEndpoints
{
    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/setup")
            .WithTags("Setup")
            .AddEndpointFilter<ValidationFilter>();

        group.MapGet("/status", async (GetSetupStatusHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .AllowAnonymous()
            .WithName("GetSetupStatus")
            .WithSummary("Indica si la instancia necesita crear su primer administrador.");

        group.MapPost("/admin", async (CreateInitialAdminRequest request, CreateInitialAdminHandler handler, CancellationToken ct) =>
                TypedResults.Created((string?)null, await handler.HandleAsync(request, ct)))
            .AllowAnonymous()
            .WithName("CreateInitialAdmin")
            .WithSummary("Crea el primer administrador (solo si aún no hay usuarios).")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
