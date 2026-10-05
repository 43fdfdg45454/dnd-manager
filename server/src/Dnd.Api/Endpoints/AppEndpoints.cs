namespace Dnd.Api.Endpoints;

public sealed record AppInfoResponse(string Name, string Version);

public static class AppEndpoints
{
    public static IEndpointRouteBuilder MapAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/app").WithTags("App");

        group.MapGet("/info", () =>
        {
            var version = typeof(AppEndpoints).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            return TypedResults.Ok(new AppInfoResponse("dnd-companion-api", version));
        })
        .WithName("GetAppInfo")
        .WithSummary("Nombre y versión de la API. Lo usa el cliente para comprobar conectividad.");

        return app;
    }
}
