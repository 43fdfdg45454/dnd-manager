namespace Dnd.Api.Hosting;

public static class PublicOriginMiddleware
{
    /// <summary>
    /// Remembers the origin (<c>scheme://host</c>, already corrected by the forwarded headers) of each request
    /// as the last public origin of the instance. Health probes are skipped: they usually come straight from
    /// the container runtime with an internal host name. Must run after <c>UseForwardedHeaders</c>.
    /// </summary>
    public static IApplicationBuilder UsePublicOriginTracking(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/health") && PublicOrigin.FromRequest(context.Request) is { } origin)
            {
                var store = context.RequestServices.GetRequiredService<PublicOriginStore>();
                try
                {
                    await store.RememberAsync(origin, context.RequestAborted);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Tracking is best effort: a failing write must never break the request.
                    context.RequestServices.GetRequiredService<ILogger<PublicOriginStore>>()
                        .LogWarning(ex, "The public origin {Origin} could not be saved", origin);
                }
            }

            await next(context);
        });
}
