using OpenTrpg.Core.Application.Abstractions;

namespace OpenTrpg.Core.Infrastructure.Email;

/// <summary>Builds the links that go into emails: the public URL (see <see cref="IPublicUrlProvider"/>) plus a path.</summary>
internal sealed class PublicLinkBuilder(IPublicUrlProvider publicUrl)
{
    /// <param name="pathAndQuery">Starts with <c>/</c>, for example <c>/set-password?token=abc</c>.</param>
    public async Task<string> BuildAsync(string pathAndQuery, CancellationToken cancellationToken = default) =>
        await publicUrl.GetOriginAsync(cancellationToken) + pathAndQuery;
}
