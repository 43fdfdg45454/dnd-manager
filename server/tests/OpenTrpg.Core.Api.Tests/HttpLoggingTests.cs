using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenTrpg.Core.Api.Tests;

/// <summary>
/// <c>Logging:Http</c> (<c>HTTP_LOGGING</c> in docker compose) logs every request and response with their JSON bodies;
/// off by default.
/// </summary>
public class HttpLoggingTests
{
    [Fact]
    public async Task Requests_and_responses_are_logged_with_their_bodies_when_enabled()
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();
        using var logging = factory.WithWebHostBuilder(builder => builder.UseSetting("Logging:Http", "true"));
        using var client = logging.CreateClient();

        var response = await client.GetAsync("/api/v1/app/info");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entries = factory.Logs.Entries.Where(e => e.Category.StartsWith("Microsoft.AspNetCore.HttpLogging", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(entries);
        Assert.All(entries, e => Assert.Equal(LogLevel.Information, e.Level));
        Assert.Contains(entries, e => e.Message.Contains("/api/v1/app/info", StringComparison.Ordinal));
        Assert.Contains(entries, e => e.Message.Contains("opentrpg-api", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e => e.Message.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Nothing_is_logged_by_default()
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/app/info")).StatusCode);

        Assert.DoesNotContain(factory.Logs.Entries, e => e.Category.StartsWith("Microsoft.AspNetCore.HttpLogging", StringComparison.Ordinal));
    }
}
