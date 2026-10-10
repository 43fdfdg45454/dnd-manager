using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Infrastructure.Email;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using static OpenTrpg.Core.Api.Tests.Sessions.SessionTestHelpers;

namespace OpenTrpg.Core.Api.Tests;

/// <summary>The public URL is the mandatory <c>App:PublicUrl</c> setting: it decides the links in the emails.</summary>
public class PublicUrlTests
{
    [Fact]
    public void Startup_fails_without_App_PublicUrl()
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();
        using var missing = factory.WithWebHostBuilder(b => b.UseSetting("App:PublicUrl", ""));

        var exception = Assert.ThrowsAny<Exception>(() => missing.CreateClient());
        Assert.Contains("App:PublicUrl is required and must be an absolute http(s) URL without path", exception.ToString());
    }

    [Theory]
    [InlineData("dnd.example.com")]
    [InlineData("https://dnd.example.com/dnd")]
    [InlineData("https://dnd.example.com?x=1")]
    [InlineData("ftp://dnd.example.com")]
    [InlineData("https://user@dnd.example.com")]
    public void Startup_fails_when_App_PublicUrl_is_not_an_origin(string value)
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();
        using var broken = factory.WithWebHostBuilder(b => b.UseSetting("App:PublicUrl", value));

        var exception = Assert.ThrowsAny<Exception>(() => broken.CreateClient());
        Assert.Contains("App:PublicUrl", exception.ToString());
    }

    [Fact]
    public void Startup_fails_when_App_PublicUrl_has_a_path() =>
        Startup_fails_when_App_PublicUrl_is_not_an_origin("https://dnd.example.com/api");

    [Fact]
    public async Task Startup_accepts_App_PublicUrl_with_a_trailing_slash_and_normalises_it()
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();
        using var slash = factory.WithWebHostBuilder(b => b.UseSetting("App:PublicUrl", "https://dnd.example.org:8443/"));

        var provider = slash.Services.GetRequiredService<IPublicUrlProvider>();
        Assert.Equal("https://dnd.example.org:8443", await provider.GetOriginAsync());
    }

    [Fact]
    public async Task Email_links_use_App_PublicUrl()
    {
        using var factory = new ApiFactory();
        var admin = await factory.CreateAdminClientAsync();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        // Neither the Host of the request nor forwarded headers change the link.
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/users")
        {
            Content = JsonContent.Create(new { email, displayName = "Test User", role = "User" }),
        };
        request.Headers.Add("X-Forwarded-Proto", "http");
        request.Headers.Add("X-Forwarded-Host", "evil.example.net");
        var response = await admin.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var link = FakeEmailSender.ExtractLink(factory.Emails.LastSentTo(email));
        Assert.StartsWith("https://dnd.example.com/set-password?token=", link);
    }

    [Fact]
    public async Task Reminders_without_a_request_use_App_PublicUrl()
    {
        using var factory = new ApiFactory();
        var scenario = await factory.CreateCampaignScenarioAsync();
        var startsAt = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(18), TimeSpan.Zero);
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: startsAt);

        // The background processor runs outside any request.
        await factory.ProcessRemindersAsync(startsAt.AddHours(-24).AddMinutes(1));

        var (_, _, url) = FakeEmailSender.ExtractSessionLink(factory.Emails.LastSentTo(scenario.Player.Email));
        Assert.StartsWith($"https://dnd.example.com/sessions/{session.Id}?token=", url);
    }

    [Fact]
    public async Task A_custom_App_PublicUrl_is_used_in_the_links()
    {
        using var factory = new CustomUrlFactory();
        var admin = await factory.CreateAdminClientAsync();
        var email = $"user-{Guid.NewGuid():N}@example.com";

        var response = await admin.PostAsJsonAsync("/api/v1/admin/users", new { email, displayName = "Test User", role = "User" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.StartsWith("http://localhost:8080/set-password?token=", FakeEmailSender.ExtractLink(factory.Emails.LastSentTo(email)));
    }

    private sealed class CustomUrlFactory : ApiFactory
    {
        protected override string PublicUrl => "http://localhost:8080/";
    }
}

public class SmtpSecurityMappingTests
{
    [Theory]
    [InlineData(SmtpSecurity.Auto, false, SecureSocketOptions.Auto)]
    [InlineData(SmtpSecurity.Auto, true, SecureSocketOptions.StartTls)]
    [InlineData(SmtpSecurity.None, false, SecureSocketOptions.None)]
    [InlineData(SmtpSecurity.None, true, SecureSocketOptions.None)]
    [InlineData(SmtpSecurity.StartTls, false, SecureSocketOptions.StartTls)]
    [InlineData(SmtpSecurity.SslOnConnect, false, SecureSocketOptions.SslOnConnect)]
    [InlineData(SmtpSecurity.SslOnConnect, true, SecureSocketOptions.SslOnConnect)]
    public void Security_maps_to_the_mailkit_socket_options(SmtpSecurity security, bool useStartTls, SecureSocketOptions expected) =>
        Assert.Equal(expected, SmtpSecurityMapper.ToSecureSocketOptions(security, useStartTls));

    [Fact]
    public void Defaults_are_auto_security_without_revocation_check()
    {
        var options = new SmtpOptions();

        Assert.Equal(SmtpSecurity.Auto, options.Security);
        Assert.False(options.UseStartTls);
        Assert.False(options.CheckCertificateRevocation);
    }
}
