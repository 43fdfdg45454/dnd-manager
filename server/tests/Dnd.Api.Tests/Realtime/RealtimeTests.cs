using System.Net;
using System.Net.Http.Json;
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

    private async Task<HubConnection> ConnectAsync(SignedInUser user)
    {
        var connection = BuildConnection(user.Client.DefaultRequestHeaders.Authorization!.Parameter);
        await connection.StartAsync();
        return connection;
    }

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
