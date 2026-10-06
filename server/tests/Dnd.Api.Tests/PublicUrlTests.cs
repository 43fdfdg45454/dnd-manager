using System.Net;
using System.Net.Http.Json;
using Dnd.Infrastructure.Email;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static Dnd.Api.Tests.Sessions.SessionTestHelpers;

namespace Dnd.Api.Tests;

/// <summary>The public URL is decided by the reverse proxy: the API reads it from the requests it receives.</summary>
public class PublicUrlTests
{
    private const string ForwardedOrigin = "https://dnd.example.com";

    private static HttpRequestMessage Forwarded(HttpMethod method, string url, string proto = "https", string host = "dnd.example.com")
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Forwarded-Proto", proto);
        request.Headers.Add("X-Forwarded-Host", host);
        return request;
    }

    /// <summary>Creates a user through the admin API (optionally with forwarded headers) and returns the set-password link emailed to it.</summary>
    private static async Task<string> CreateUserAndGetLinkAsync(ApiFactory factory, Action<HttpRequestMessage>? configure = null)
    {
        var admin = await factory.CreateAdminClientAsync();
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/users")
        {
            Content = JsonContent.Create(new { email, displayName = "Test User", role = "User" }),
        };
        configure?.Invoke(request);

        var response = await admin.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return FakeEmailSender.ExtractLink(factory.Emails.LastSentTo(email));
    }

    private static async Task<string?> StoredOriginAsync(ApiFactory factory)
    {
        string? value = null;
        await factory.WithDbAsync(async db => value = (await db.InstanceSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Key == "public-origin"))?.Value);
        return value;
    }

    [Fact]
    public async Task Forwarded_headers_from_a_trusted_proxy_decide_the_link_in_the_email()
    {
        using var factory = new ApiFactory();

        var link = await CreateUserAndGetLinkAsync(factory, r =>
        {
            r.Headers.Add("X-Forwarded-Proto", "https");
            r.Headers.Add("X-Forwarded-Host", "dnd.example.com");
        });

        Assert.StartsWith($"{ForwardedOrigin}/set-password?token=", link);
    }

    [Fact]
    public async Task Without_forwarded_headers_the_host_of_the_request_is_used()
    {
        using var factory = new ApiFactory();

        var link = await CreateUserAndGetLinkAsync(factory);

        Assert.StartsWith($"{ApiFactory.TestOrigin}/set-password?token=", link);
    }

    [Fact]
    public async Task Forwarded_host_from_an_untrusted_source_is_ignored_when_no_proxy_is_trusted()
    {
        using var factory = new NoTrustedProxiesFactory();

        var link = await CreateUserAndGetLinkAsync(factory, r =>
        {
            r.Headers.Add("X-Forwarded-Proto", "https");
            r.Headers.Add("X-Forwarded-Host", "evil.example.net");
        });

        Assert.StartsWith($"{ApiFactory.TestOrigin}/set-password?token=", link);
        Assert.DoesNotContain("evil.example.net", link);
    }

    [Fact]
    public async Task Forwarded_host_from_an_ip_outside_the_trusted_proxies_is_ignored()
    {
        using var factory = new PublicClientFactory();

        var link = await CreateUserAndGetLinkAsync(factory, r =>
        {
            r.Headers.Add("X-Forwarded-Proto", "https");
            r.Headers.Add("X-Forwarded-Host", "evil.example.net");
        });

        Assert.StartsWith($"{ApiFactory.TestOrigin}/set-password?token=", link);
        Assert.Equal(ApiFactory.TestOrigin, await StoredOriginAsync(factory));
    }

    [Fact]
    public async Task Trusted_proxies_can_be_configured_as_a_comma_separated_list_of_cidr_ranges()
    {
        using var factory = new CustomProxyFactory();

        var link = await CreateUserAndGetLinkAsync(factory, r =>
        {
            r.Headers.Add("X-Forwarded-Proto", "https");
            r.Headers.Add("X-Forwarded-Host", "dnd.example.com");
        });

        Assert.StartsWith($"{ForwardedOrigin}/set-password?token=", link);
    }

    [Fact]
    public void Startup_fails_when_a_trusted_proxy_is_not_an_ip_range()
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();
        using var broken = factory.WithWebHostBuilder(b => b.UseSetting("App:TrustedProxies", "10.0.0.0/8,not-an-ip"));

        var exception = Assert.ThrowsAny<Exception>(() => broken.CreateClient());
        Assert.Contains("App:TrustedProxies", exception.ToString());
    }

    [Fact]
    public async Task The_origin_is_persisted_only_when_it_changes_and_health_probes_are_ignored()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        (await client.SendAsync(Forwarded(HttpMethod.Get, "/api/v1/app/info"))).EnsureSuccessStatusCode();
        Assert.Equal(ForwardedOrigin, await StoredOriginAsync(factory));
        var firstWrite = await UpdatedAtAsync(factory);

        // Same origin again: nothing is written.
        await Task.Delay(20);
        (await client.SendAsync(Forwarded(HttpMethod.Get, "/api/v1/app/info"))).EnsureSuccessStatusCode();
        Assert.Equal(firstWrite, await UpdatedAtAsync(factory));

        // Health probes never change it.
        (await client.SendAsync(Forwarded(HttpMethod.Get, "/health", host: "probe.internal"))).EnsureSuccessStatusCode();
        Assert.Equal(ForwardedOrigin, await StoredOriginAsync(factory));

        // A different origin replaces it.
        (await client.SendAsync(Forwarded(HttpMethod.Get, "/api/v1/app/info", "http", "dnd.example.org:8443"))).EnsureSuccessStatusCode();
        Assert.Equal("http://dnd.example.org:8443", await StoredOriginAsync(factory));
    }

    private static async Task<DateTimeOffset?> UpdatedAtAsync(ApiFactory factory)
    {
        DateTimeOffset? value = null;
        await factory.WithDbAsync(async db => value = (await db.InstanceSettings.AsNoTracking().SingleAsync(s => s.Key == "public-origin")).UpdatedAt);
        return value;
    }

    [Fact]
    public async Task Reminders_without_a_request_use_the_last_persisted_origin()
    {
        using var factory = new ApiFactory();
        var scenario = await factory.CreateCampaignScenarioAsync();
        var startsAt = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(18), TimeSpan.Zero);
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: startsAt);

        // The last request that reached the API came through the proxy.
        (await factory.CreateClient().SendAsync(Forwarded(HttpMethod.Get, "/api/v1/app/info"))).EnsureSuccessStatusCode();

        // The background processor runs outside any request.
        await factory.ProcessRemindersAsync(startsAt.AddHours(-24).AddMinutes(1));

        var (_, _, url) = FakeEmailSender.ExtractSessionLink(factory.Emails.LastSentTo(scenario.Player.Email));
        Assert.StartsWith($"{ForwardedOrigin}/sessions/{session.Id}?token=", url);
    }

    [Fact]
    public void Without_any_known_origin_the_email_carries_a_relative_link_and_a_warning_is_logged()
    {
        using var factory = new ApiFactory();

        // Nothing has gone through the proxy yet: only the startup bootstrap ran.
        _ = factory.Server;

        var link = FakeEmailSender.ExtractLink(factory.Emails.SentTo(ApiFactory.AdminEmail)[0]);
        Assert.StartsWith("/set-password?token=", link);
        Assert.Contains(factory.Logs.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains("No se conoce aún la URL pública: abre la API a través del proxy una vez"));
        Assert.Contains(factory.Logs.Entries, e =>
            e.Level == LogLevel.Warning && e.Message.Contains(link) && e.Message.Contains("prepend the public URL"));
    }

    [Fact]
    public async Task App_PublicUrl_is_an_optional_fallback_that_requests_override()
    {
        using var factory = new FallbackFactory();
        _ = factory.Server;

        Assert.StartsWith("https://fallback.example.com/set-password?token=", FakeEmailSender.ExtractLink(factory.Emails.SentTo(ApiFactory.AdminEmail)[0]));
        Assert.DoesNotContain(factory.Logs.Entries, e => e.Message.Contains("No se conoce aún la URL pública"));

        var link = await CreateUserAndGetLinkAsync(factory);
        Assert.StartsWith($"{ApiFactory.TestOrigin}/set-password?token=", link);
    }

    [Fact]
    public void Startup_fails_when_App_PublicUrl_is_set_but_is_not_an_absolute_url()
    {
        using var factory = new ApiFactoryWithoutInitialAdmin();
        using var broken = factory.WithWebHostBuilder(b => b.UseSetting("App:PublicUrl", "dnd.example.com"));

        var exception = Assert.ThrowsAny<Exception>(() => broken.CreateClient());
        Assert.Contains("App:PublicUrl", exception.ToString());
    }

    [Fact]
    public async Task Trusted_proxies_can_be_given_as_an_indexed_list_like_docker_compose_does()
    {
        // App__TrustedProxies__0=... is a section with children: the options binder must not choke on it.
        using var factory = new IndexedProxyListFactory();

        var link = await CreateUserAndGetLinkAsync(factory, r =>
        {
            r.Headers.Add("X-Forwarded-Proto", "https");
            r.Headers.Add("X-Forwarded-Host", "dnd.example.com");
        });

        Assert.StartsWith("https://dnd.example.com/set-password?token=", link);
    }

    private sealed class NoTrustedProxiesFactory : ApiFactory
    {
        protected override string? TrustedProxies => string.Empty;
    }

    private sealed class PublicClientFactory : ApiFactory
    {
        protected override string RemoteIpAddress => "203.0.113.9";
    }

    private sealed class CustomProxyFactory : ApiFactory
    {
        protected override string? TrustedProxies => "203.0.113.0/24, 2001:db8::/32";

        protected override string RemoteIpAddress => "203.0.113.9";
    }

    private sealed class IndexedProxyListFactory : ApiFactory
    {
        protected override string RemoteIpAddress => "203.0.113.9";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("App:TrustedProxies:0", "203.0.113.0/24");
            builder.UseSetting("App:TrustedProxies:1", "10.0.0.0/8");
        }
    }

    private sealed class FallbackFactory : ApiFactory
    {
        protected override string? PublicUrlFallback => "https://fallback.example.com/";
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
    public void Defaults_are_auto_security_with_revocation_check()
    {
        var options = new SmtpOptions();

        Assert.Equal(SmtpSecurity.Auto, options.Security);
        Assert.False(options.UseStartTls);
        Assert.True(options.CheckCertificateRevocation);
    }
}
