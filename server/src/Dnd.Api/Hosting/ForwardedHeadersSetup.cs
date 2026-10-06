using Microsoft.AspNetCore.HttpOverrides;

namespace Dnd.Api.Hosting;

/// <summary>
/// Forwarded headers (<c>X-Forwarded-For/Proto/Host</c>) set by the reverse proxy of the operator are honoured
/// from any source: the API is meant to sit behind the operator's proxy and must answer whoever reaches it.
/// The scheme and host they carry become the origin of the links in emails (see <c>PublicUrlProvider</c>).
/// </summary>
public static class ForwardedHeadersSetup
{
    public static IServiceCollection AddAppForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            // Empty known lists make the middleware accept the headers from every connection.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            options.ForwardLimit = null;
        });

        return services;
    }
}
