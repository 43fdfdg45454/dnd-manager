using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Campaigns;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;

namespace OpenTrpg.Core.Api.Tests;

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

    /// <summary>Invites <paramref name="user"/> to the campaign as <paramref name="actor"/>; the user accepts, so they end up as a member.</summary>
    public static async Task<MemberDto> AddMemberAsync(this SignedInUser actor, Guid campaignId, SignedInUser user, string role)
    {
        var invitation = await actor.InviteAsync(campaignId, user, role);
        var response = await user.Client.PostAsync($"/api/v1/invitations/{invitation.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<MemberDto>())!;
    }

    /// <summary>Invites <paramref name="user"/> to the campaign through the API as <paramref name="actor"/>.</summary>
    public static async Task<CampaignInvitationDto> InviteAsync(this SignedInUser actor, Guid campaignId, SignedInUser user, string role)
    {
        var response = await actor.Client.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/members", new { userId = user.Id, role });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CampaignInvitationDto>())!;
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

    /// <summary>
    /// Activates <paramref name="packIds"/> in the campaign as its DM, keeping the packs already active
    /// (imported packs start inactive in every campaign). Without ids, activates every imported pack.
    /// </summary>
    public static async Task EnablePacksAsync(this CampaignScenario scenario, params string[] packIds)
    {
        var current = await scenario.Dm.Client.GetFromJsonAsync<List<CampaignContentPackDto>>($"{scenario.Url}/content-packs");
        var enabled = current!
            .Where(p => !p.IsBase && (p.Enabled || packIds.Length == 0))
            .Select(p => p.Id)
            .Union(packIds)
            .ToArray();
        var response = await scenario.Dm.Client.PutAsJsonAsync($"{scenario.Url}/content-packs", new { packIds = enabled });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }
}
