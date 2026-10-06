using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dnd.Application.Abstractions;
using Dnd.Application.Campaigns;
using Dnd.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dnd.Api.Tests;

/// <summary>A user inserted straight into the database with a client carrying a valid access token.</summary>
public sealed record SignedInUser(Guid Id, string Email, string DisplayName, HttpClient Client);

/// <summary>A campaign with one user per campaign role plus an authenticated non-member.</summary>
public sealed record CampaignScenario(Guid CampaignId, SignedInUser Owner, SignedInUser Dm, SignedInUser Player, SignedInUser Outsider)
{
    public const string OwnerRole = "Owner";
    public const string DmRole = "DM";
    public const string PlayerRole = "Player";
    public const string OutsiderRole = "Outsider";

    public string Url => $"/api/v1/campaigns/{CampaignId}";

    /// <summary>User acting as <paramref name="role"/>: "Owner", "DM", "Player" or "Outsider".</summary>
    public SignedInUser As(string role) => role switch
    {
        OwnerRole => Owner,
        DmRole => Dm,
        PlayerRole => Player,
        OutsiderRole => Outsider,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}

public static class TestUsers
{
    /// <summary>
    /// Creates a user without going through the admin API, password hashing or login: the access
    /// token is minted with the app's <see cref="ITokenService"/>. Much faster than
    /// <see cref="ApiFactory.CreateUserAsync"/> when a test needs many users.
    /// </summary>
    public static async Task<SignedInUser> CreateSignedInUserAsync(this ApiFactory factory, string? displayName = null, string? email = null)
    {
        // The first user of an instance is its admin: it must exist before users are inserted directly.
        await factory.EnsureInitialAdminAsync();
        var user = User.Create(email ?? $"user-{Guid.NewGuid():N}@example.com", displayName ?? "Test User", UserRole.User, DateTimeOffset.UtcNow);
        await factory.WithDbAsync(async db =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });

        var token = factory.Services.GetRequiredService<ITokenService>().CreateAccessToken(user);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return new SignedInUser(user.Id, user.Email, user.DisplayName, client);
    }

    public static Task SetUserActiveAsync(this ApiFactory factory, Guid userId, bool isActive) =>
        factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            user.SetActive(isActive);
            await db.SaveChangesAsync();
        });

    /// <summary>Creates a campaign through the API as <paramref name="owner"/>.</summary>
    public static async Task<CampaignDto> CreateCampaignAsync(this SignedInUser owner, string? name = null, string description = "")
    {
        var response = await owner.Client.PostAsJsonAsync("/api/v1/campaigns", new { name = name ?? $"Campaign {Guid.NewGuid():N}", description });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CampaignDto>())!;
    }

    /// <summary>Adds <paramref name="user"/> to the campaign through the API as <paramref name="actor"/>.</summary>
    public static async Task<MemberDto> AddMemberAsync(this SignedInUser actor, Guid campaignId, SignedInUser user, string role)
    {
        var response = await actor.Client.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/members", new { userId = user.Id, role });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MemberDto>())!;
    }

    /// <summary>New campaign with an Owner, a DM, a Player and an authenticated outsider.</summary>
    public static async Task<CampaignScenario> CreateCampaignScenarioAsync(this ApiFactory factory)
    {
        var owner = await factory.CreateSignedInUserAsync("Owner User");
        var dm = await factory.CreateSignedInUserAsync("Dm User");
        var player = await factory.CreateSignedInUserAsync("Player User");
        var outsider = await factory.CreateSignedInUserAsync("Outsider User");

        var campaign = await owner.CreateCampaignAsync();
        await owner.AddMemberAsync(campaign.Id, dm, CampaignScenario.DmRole);
        await owner.AddMemberAsync(campaign.Id, player, CampaignScenario.PlayerRole);

        return new CampaignScenario(campaign.Id, owner, dm, player, outsider);
    }
}
