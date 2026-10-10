namespace OpenTrpg.Core.Application.Abstractions;

/// <summary>
/// Tells which public origin (<c>scheme://host[:port]</c>, no trailing slash) the instance is reached through,
/// to build absolute links in emails. It is the mandatory <c>App:PublicUrl</c> setting of the operator.
/// </summary>
public interface IPublicUrlProvider
{
    /// <summary>The configured public URL, normalised (trimmed, without trailing slash).</summary>
    Task<string> GetOriginAsync(CancellationToken cancellationToken = default);
}
