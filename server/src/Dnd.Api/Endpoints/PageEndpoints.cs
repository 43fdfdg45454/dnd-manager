namespace Dnd.Api.Endpoints;

/// <summary>Minimal web pages served by the API outside <c>/api</c>.</summary>
public static class PageEndpoints
{
    public static IEndpointRouteBuilder MapPageEndpoints(this IEndpointRouteBuilder app)
    {
        // Opened from the setup/reset emails: /set-password?token=...
        app.MapGet("/set-password", (HttpContext context, IWebHostEnvironment environment) =>
            {
                var file = environment.WebRootFileProvider.GetFileInfo("set-password.html");
                if (!file.Exists)
                {
                    return Results.NotFound();
                }

                // The URL carries a secret token: never cache the page nor leak it as a referrer.
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                return Results.Stream(file.CreateReadStream(), "text/html; charset=utf-8");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        // Opened from the reminder emails: /sessions/{id}?token=... Shows the session and lets the member
        // answer attendance through the public API; the token is the only credential.
        app.MapGet("/sessions/{id:guid}", (HttpContext context, IWebHostEnvironment environment) =>
            {
                var file = environment.WebRootFileProvider.GetFileInfo("session.html");
                if (!file.Exists)
                {
                    return Results.NotFound();
                }

                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                return Results.Stream(file.CreateReadStream(), "text/html; charset=utf-8");
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }
}
