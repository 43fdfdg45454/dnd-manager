using Dnd.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dnd.Infrastructure.Email;

/// <summary>
/// Builds the links that go into emails: the public origin (see <see cref="IPublicUrlProvider"/>) plus a path.
/// While the origin is not known yet the link is relative and a warning tells the operator how to fix it.
/// </summary>
internal sealed class PublicLinkBuilder(IPublicUrlProvider publicUrl, ILogger<PublicLinkBuilder> logger)
{
    /// <param name="pathAndQuery">Starts with <c>/</c>, for example <c>/set-password?token=abc</c>.</param>
    public async Task<string> BuildAsync(string pathAndQuery, CancellationToken cancellationToken = default)
    {
        var origin = await publicUrl.GetOriginAsync(cancellationToken);
        if (string.IsNullOrEmpty(origin))
        {
            logger.LogWarning("No se conoce aún la URL pública: abre la API a través del proxy una vez. El enlace del correo será relativo ({Path}).", pathAndQuery.Split('?')[0]);
            return pathAndQuery;
        }

        return origin.TrimEnd('/') + pathAndQuery;
    }
}
