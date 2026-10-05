using Dnd.Application.Abstractions;
using Dnd.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Dnd.Api.Hosting;

/// <summary>
/// Resolves the public origin: the current request's, else the last one persisted (background services),
/// else the optional <c>App:PublicUrl</c>, else empty (unknown).
/// </summary>
internal sealed class PublicUrlProvider(
    IHttpContextAccessor accessor,
    PublicOriginStore store,
    IOptions<AppOptions> options,
    ILogger<PublicUrlProvider> logger) : IPublicUrlProvider
{
    public string? CurrentOrigin => accessor.HttpContext is { } http ? PublicOrigin.FromRequest(http.Request) : null;

    public async Task<string> GetOriginAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentOrigin is { } current)
        {
            return current;
        }

        try
        {
            if (await store.GetAsync(cancellationToken) is { } persisted)
            {
                return persisted;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Before the migrations ran (or with the database down) the setting cannot be read: fall through.
            logger.LogWarning(ex, "The last public origin could not be read from the database");
        }

        // Validated as an absolute http(s) URL at startup when informed; a path prefix is kept.
        return options.Value.PublicUrl?.Trim().TrimEnd('/') ?? string.Empty;
    }
}
