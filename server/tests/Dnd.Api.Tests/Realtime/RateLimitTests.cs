using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace Dnd.Api.Tests.Realtime;

/// <summary>Rate limits with the real defaults: hub connection attempts per user, login per IP and per account.</summary>
public sealed class RateLimitTests(RateLimitApiFactory factory) : IClassFixture<RateLimitApiFactory>
{
    private const int HubLimit = 30;
    private const int LoginLimit = 10;

    [Fact]
    public async Task The_hub_answers_429_after_too_many_connection_attempts()
    {
        var user = await factory.CreateSignedInUserAsync();
        var other = await factory.CreateSignedInUserAsync();

        for (var i = 0; i < HubLimit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await NegotiateAsync(user)).StatusCode);
        }

        var limited = await NegotiateAsync(user);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        var problem = (await limited.Content.ReadFromJsonAsync<ProblemDetails>())!;
        Assert.Equal((429, "Demasiadas peticiones."), (problem.Status, problem.Title));
        Assert.True(limited.Headers.RetryAfter is not null);

        // The limit is per user.
        Assert.Equal(HttpStatusCode.OK, (await NegotiateAsync(other)).StatusCode);
    }

    [Fact]
    public async Task Login_answers_429_after_too_many_attempts_from_the_same_address()
    {
        var client = factory.CreateClient();
        for (var i = 0; i < LoginLimit; i++)
        {
            var attempt = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = $"nobody-{i}@example.com", password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody-else@example.com", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(429, (await limited.Content.ReadFromJsonAsync<ProblemDetails>())!.Status);
    }

    [Fact]
    public async Task Login_answers_429_after_too_many_attempts_on_the_same_account_from_any_address()
    {
        var email = $"target-{Guid.NewGuid():N}@example.com";
        for (var i = 0; i < LoginLimit; i++)
        {
            var attempt = await LoginFromAsync($"203.0.113.{i + 1}", i % 2 == 0 ? email : $"  {email.ToUpperInvariant()} ");
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }

        var limited = await LoginFromAsync("198.51.100.7", email);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<HttpResponseMessage> NegotiateAsync(SignedInUser user) =>
        await user.Client.PostAsync("/hubs/campaign/negotiate?negotiateVersion=1", null);

    /// <summary>Login as if forwarded by the proxy for <paramref name="address"/> (each address has its own IP limit).</summary>
    private async Task<HttpResponseMessage> LoginFromAsync(string address, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password = "wrong-password" }),
        };
        request.Headers.Add("X-Forwarded-For", address);
        return await factory.CreateClient().SendAsync(request);
    }
}

/// <summary>API host with the real (default) rate limits.</summary>
public sealed class RateLimitApiFactory : ApiFactory
{
    protected override bool RelaxLoginRateLimits => false;
}
