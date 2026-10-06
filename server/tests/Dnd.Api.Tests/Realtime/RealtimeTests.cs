using System.Net;
using System.Net.Http.Json;
using Dnd.Api.Realtime;
using Dnd.Api.Tests.Items;
using Dnd.Application.Abstractions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Dnd.Api.Tests.Realtime;

/// <summary>
/// The campaign hub over the test server. Long polling is the transport that works with TestServer;
/// every test joins the campaign (which also guarantees the connection finished connecting) before
/// triggering the change.
/// </summary>
public sealed class RealtimeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task A_member_joins_their_campaign_but_not_another_one()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateCampaignScenarioAsync();
        await using var connection = await ConnectAsync(s.Player);

        await connection.InvokeAsync("JoinCampaign", s.CampaignId);
        var exception = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("JoinCampaign", other.CampaignId));
        await connection.InvokeAsync("LeaveCampaign", s.CampaignId);

        Assert.Contains("No perteneces a esta campaña.", exception.Message);
    }

    [Fact]
    public async Task The_player_receives_the_secret_message_sent_by_the_dm()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId);
        await using var connection = await ConnectAsync(s.Player);
        var received = Expect(connection, CampaignEventTypes.MessageReceived);
        await connection.InvokeAsync("JoinCampaign", s.CampaignId);

        var response = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/messages", new { characterIds = new[] { hero.Id }, body = "Psst." });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var e = await received.WaitAsync(EventTimeout);
        Assert.Equal((s.CampaignId, (Guid?)hero.Id), (e.CampaignId, e.CharacterId));
        Assert.NotNull(e.EntityId);
    }

    [Fact]
    public async Task Messages_only_reach_their_recipient()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId);
        await using var dmConnection = await ConnectAsync(s.Dm);
        await using var playerConnection = await ConnectAsync(s.Player);
        var dmEvents = new List<CampaignEvent>();
        dmConnection.On<CampaignEvent>("campaignEvent", e => { lock (dmEvents) { dmEvents.Add(e); } });
        var received = Expect(playerConnection, CampaignEventTypes.MessageReceived);
        await dmConnection.InvokeAsync("JoinCampaign", s.CampaignId);
        await playerConnection.InvokeAsync("JoinCampaign", s.CampaignId);

        await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/messages", new { characterIds = new[] { hero.Id }, body = "Solo para ti." });
        await received.WaitAsync(EventTimeout);

        lock (dmEvents)
        {
            Assert.DoesNotContain(dmEvents, e => e.Type == CampaignEventTypes.MessageReceived);
        }
    }

    [Fact]
    public async Task A_forced_rest_reaches_the_campaign()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);
        await using var connection = await ConnectAsync(s.Player);
        var rest = Expect(connection, CampaignEventTypes.PartyRest);
        await connection.InvokeAsync("JoinCampaign", s.CampaignId);

        var response = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/rest", new { kind = "long" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var e = await rest.WaitAsync(EventTimeout);
        Assert.Equal((s.CampaignId, (Guid?)null, (Guid?)null), (e.CampaignId, e.CharacterId, e.EntityId));
    }

    [Fact]
    public async Task Combat_changes_and_stash_changes_reach_the_campaign()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);
        await using var connection = await ConnectAsync(s.Player);
        var updated = Expect(connection, CampaignEventTypes.CharacterUpdated);
        var stash = Expect(connection, CampaignEventTypes.PartyStashUpdated);
        await connection.InvokeAsync("JoinCampaign", s.CampaignId);

        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/combat", new { inspiration = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/stash/gold", new { deltaCp = 10 })).StatusCode);

        Assert.Equal((Guid?)hero.Id, (await updated.WaitAsync(EventTimeout)).CharacterId);
        Assert.Equal(s.CampaignId, (await stash.WaitAsync(EventTimeout)).CampaignId);
    }

    [Fact]
    public async Task Connecting_without_a_token_fails_with_401()
    {
        var connection = BuildConnection(token: null);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => connection.StartAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        await connection.DisposeAsync();
    }

    [Fact]
    public async Task The_access_token_query_parameter_is_only_accepted_on_the_hub_paths()
    {
        var user = await factory.CreateSignedInUserAsync();
        var token = user.Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var anonymous = factory.CreateClient();

        var hub = await anonymous.PostAsync($"/hubs/campaign/negotiate?negotiateVersion=1&access_token={token}", null);
        var api = await anonymous.GetAsync($"/api/v1/auth/me?access_token={token}");

        Assert.Equal(HttpStatusCode.OK, hub.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }

    [Fact]
    public async Task The_sixth_connection_of_a_user_is_refused()
    {
        var user = await factory.CreateSignedInUserAsync();
        var open = new List<HubConnection>();
        try
        {
            for (var i = 0; i < 5; i++)
            {
                open.Add(await ConnectAsync(user));
            }

            var status = await user.Client.GetFromJsonAsync<RealtimeStatusDto>("/api/v1/realtime/status");
            Assert.Equal((5, "LongPolling"), (status!.Connections, status.Transport));

            await using var sixth = BuildConnection(user.Client.DefaultRequestHeaders.Authorization!.Parameter);
            var closed = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
            sixth.Closed += e =>
            {
                closed.TrySetResult(e);
                return Task.CompletedTask;
            };

            Exception? error;
            try
            {
                await sixth.StartAsync();
                error = await closed.Task.WaitAsync(EventTimeout);
            }
            catch (Exception e) when (e is not TimeoutException)
            {
                error = e;
            }

            Assert.Contains("Demasiadas conexiones abiertas.", error?.Message);
            Assert.Equal(5, (await user.Client.GetFromJsonAsync<RealtimeStatusDto>("/api/v1/realtime/status"))!.Connections);
        }
        finally
        {
            foreach (var connection in open)
            {
                await connection.DisposeAsync();
            }
        }

        // Closed connections free their slots.
        await WaitUntilAsync(async () => (await user.Client.GetFromJsonAsync<RealtimeStatusDto>("/api/v1/realtime/status"))!.Connections == 0);
        Assert.Null((await user.Client.GetFromJsonAsync<RealtimeStatusDto>("/api/v1/realtime/status"))!.Transport);
    }

    [Fact]
    public async Task A_removed_member_stops_receiving_the_campaign_events()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        await using var playerConnection = await ConnectAsync(s.Player);
        await using var dmConnection = await ConnectAsync(s.Dm);
        var playerEvents = new List<CampaignEvent>();
        playerConnection.On<CampaignEvent>("campaignEvent", e => { lock (playerEvents) { playerEvents.Add(e); } });
        var removed = Expect(playerConnection, CampaignEventTypes.MembershipRemoved);
        var dmStash = Expect(dmConnection, CampaignEventTypes.PartyStashUpdated);
        await playerConnection.InvokeAsync("JoinCampaign", s.CampaignId);
        await dmConnection.InvokeAsync("JoinCampaign", s.CampaignId);

        Assert.Equal(HttpStatusCode.NoContent, (await s.Owner.Client.DeleteAsync($"/api/v1/campaigns/{s.CampaignId}/members/{s.Player.Id}")).StatusCode);
        Assert.Equal(s.CampaignId, (await removed.WaitAsync(EventTimeout)).CampaignId);

        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/stash/gold", new { deltaCp = 5 })).StatusCode);
        await dmStash.WaitAsync(EventTimeout);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        lock (playerEvents)
        {
            Assert.DoesNotContain(playerEvents, e => e.Type == CampaignEventTypes.PartyStashUpdated);
        }

        var rejoin = await Assert.ThrowsAsync<HubException>(() => playerConnection.InvokeAsync("JoinCampaign", s.CampaignId));
        Assert.Contains("No perteneces a esta campaña.", rejoin.Message);
    }

    [Fact]
    public async Task Deactivating_a_user_closes_their_connections_and_refuses_new_ones()
    {
        var user = await factory.CreateSignedInUserAsync();
        var admin = await factory.CreateAdminClientAsync();
        await using var connection = await ConnectAsync(user);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        Assert.Equal(HttpStatusCode.OK, (await admin.PatchAsJsonAsync($"/api/v1/admin/users/{user.Id}", new { isActive = false })).StatusCode);

        await closed.Task.WaitAsync(EventTimeout);
        await using var again = BuildConnection(user.Client.DefaultRequestHeaders.Authorization!.Parameter);
        var refused = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        again.Closed += e =>
        {
            refused.TrySetResult(e);
            return Task.CompletedTask;
        };
        Exception? error;
        try
        {
            await again.StartAsync();
            error = await refused.Task.WaitAsync(EventTimeout);
        }
        catch (Exception e) when (e is not TimeoutException)
        {
            error = e;
        }

        Assert.Contains("La cuenta está desactivada.", error?.Message);
    }

    [Fact]
    public async Task The_owner_hears_about_a_granted_level()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);
        await using var connection = await ConnectAsync(s.Player);
        var granted = Expect(connection, CampaignEventTypes.LevelUpGranted);

        // Not joined to the campaign: the event still reaches the owner through their user group.
        var response = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { hero.Id } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal((Guid?)hero.Id, (await granted.WaitAsync(EventTimeout)).CharacterId);
    }

    [Fact]
    public async Task A_rest_request_reaches_the_campaign()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);
        await using var connection = await ConnectAsync(s.Dm);
        var updated = Expect(connection, CampaignEventTypes.RestRequestUpdated);
        await connection.InvokeAsync("JoinCampaign", s.CampaignId);

        var response = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/rest-requests", new { kind = "long" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.Equal((Guid?)hero.Id, (await updated.WaitAsync(EventTimeout)).CharacterId);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + EventTimeout;
        while (!await condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not met in time.");
            await Task.Delay(50);
        }
    }

    /// <summary>Completes with the first event of <paramref name="type"/> received by the connection.</summary>
    private static Task<CampaignEvent> Expect(HubConnection connection, string type)
    {
        var source = new TaskCompletionSource<CampaignEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<CampaignEvent>("campaignEvent", e =>
        {
            if (e.Type == type)
            {
                source.TrySetResult(e);
            }
        });
        return source.Task;
    }

    /// <summary>
    /// Starts a connection and waits until the hub registered it: <c>StartAsync</c> completes with the handshake,
    /// before <c>OnConnectedAsync</c> runs, and the test database (one shared SQLite connection) must not be used
    /// by two requests at once.
    /// </summary>
    private async Task<HubConnection> ConnectAsync(SignedInUser user)
    {
        var before = await ConnectionCountAsync(user);
        var connection = BuildConnection(user.Client.DefaultRequestHeaders.Authorization!.Parameter);
        await connection.StartAsync();
        await WaitUntilAsync(async () => await ConnectionCountAsync(user) > before);
        return connection;
    }

    private static async Task<int> ConnectionCountAsync(SignedInUser user) =>
        (await user.Client.GetFromJsonAsync<RealtimeStatusDto>("/api/v1/realtime/status"))!.Connections;

    private HubConnection BuildConnection(string? token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/campaign"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (token is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .Build();
}
