using Microsoft.AspNetCore.Hosting;

namespace OpenTrpg.Core.Api.Tests;

public class StartupConfigurationTests
{
    [Fact]
    public void Startup_fails_when_the_jwt_secret_is_too_short()
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();
        using var shortSecret = factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:Secret", "too-short"));

        var exception = Assert.ThrowsAny<Exception>(() => shortSecret.CreateClient());
        Assert.Contains("Jwt:Secret", exception.ToString());
    }
}
