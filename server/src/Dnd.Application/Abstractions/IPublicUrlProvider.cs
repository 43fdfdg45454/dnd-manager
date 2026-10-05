namespace Dnd.Application.Abstractions;

/// <summary>
/// Tells which public origin (<c>scheme://host[:port]</c>, no trailing slash) the instance is reached
/// through, to build absolute links in emails and logs. The API does not own a public URL: the reverse
/// proxy of the operator decides it, and the server learns it from the requests that go through it.
/// </summary>
public interface IPublicUrlProvider
{
    /// <summary>Origin of the request being handled, or null outside a request (background services, startup).</summary>
    string? CurrentOrigin { get; }

    /// <summary>
    /// Origin of the current request; else the last origin seen in any request (persisted); else the optional
    /// <c>App:PublicUrl</c>; else an empty string, meaning "not known yet": callers then fall back to a
    /// relative link.
    /// </summary>
    Task<string> GetOriginAsync(CancellationToken cancellationToken = default);
}
