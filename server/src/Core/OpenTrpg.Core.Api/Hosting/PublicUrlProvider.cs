using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace OpenTrpg.Core.Api.Hosting;

/// <summary>The public origin is always the configured <c>App:PublicUrl</c> (validated at startup).</summary>
internal sealed class PublicUrlProvider(IOptions<AppOptions> options) : IPublicUrlProvider
{
    public Task<string> GetOriginAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(options.Value.PublicUrl.Trim().TrimEnd('/'));
}
