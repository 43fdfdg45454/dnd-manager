using System.Net;
using System.Net.Http.Json;
using Dnd.Api.Tests.Items;
using Dnd.Application.Messages;

namespace Dnd.Api.Tests.Messages;

public sealed class MessageEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_message_to_two_characters_creates_one_row_per_player_and_each_sees_only_theirs()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var second = await factory.CreateSignedInUserAsync("Second Player");
        var third = await factory.CreateSignedInUserAsync("Third Player");
        await s.Owner.AddMemberAsync(s.CampaignId, second, CampaignScenario.PlayerRole);
        await s.Owner.AddMemberAsync(s.CampaignId, third, CampaignScenario.PlayerRole);
        var mine = await s.Player.CreateCharacterAsync(s.CampaignId, "Mío");
        var theirs = await second.CreateCharacterAsync(s.CampaignId, "Suyo");
        await third.CreateCharacterAsync(s.CampaignId, "Ajeno");

        var sent = await SendAsync(s.Dm, s.CampaignId, [mine.Id, theirs.Id], "  El posadero **miente**.  ");

        Assert.Equal(2, sent.Count);
        Assert.All(sent, m => Assert.Equal(("El posadero **miente**.", s.Dm.Id, "Dm User"), (m.Body, m.SenderUserId, m.SenderDisplayName)));
        var forMe = Assert.Single(await ListAsync(s.Player, s.CampaignId));
        Assert.Equal((mine.Id, "Mío", s.Player.Id, (DateTimeOffset?)null), (forMe.CharacterId, forMe.CharacterName, forMe.RecipientUserId, forMe.ReadAt));
        Assert.Equal(theirs.Id, Assert.Single(await ListAsync(second, s.CampaignId)).CharacterId);
        Assert.Empty(await ListAsync(third, s.CampaignId));
        Assert.Equal(2, (await ListAsync(s.Dm, s.CampaignId)).Count);
        Assert.Empty(await ListAsync(s.Owner, s.CampaignId));
    }

    [Fact]
    public async Task Only_the_recipient_marks_a_message_as_read_and_the_unread_count_drops()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId);
        var message = Assert.Single(await SendAsync(s.Dm, s.CampaignId, [hero.Id], "Psst."));
        Assert.Equal(1, await UnreadAsync(s.Player, s.CampaignId));
        Assert.Equal(0, await UnreadAsync(s.Dm, s.CampaignId));

        Assert.Equal(HttpStatusCode.Forbidden, (await s.Dm.Client.PostAsync($"/api/v1/messages/{message.Id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.PostAsync($"/api/v1/messages/{message.Id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Player.Client.PostAsync($"/api/v1/messages/{Guid.NewGuid()}/read", null)).StatusCode);
        var read = await s.Player.Client.PostAsync($"/api/v1/messages/{message.Id}/read", null);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.NotNull((await read.Content.ReadFromJsonAsync<MessageDto>())!.ReadAt);
        Assert.Equal(0, await UnreadAsync(s.Player, s.CampaignId));
        Assert.Empty(await ListAsync(s.Player, s.CampaignId, "unreadOnly=true"));
        Assert.Single(await ListAsync(s.Player, s.CampaignId));
    }

    [Fact]
    public async Task Invalid_messages_are_rejected()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId);
        var stranger = await other.Player.CreateCharacterAsync(other.CampaignId);
        var url = $"/api/v1/campaigns/{s.CampaignId}/messages";

        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PostAsJsonAsync(url, new { characterIds = new[] { hero.Id }, body = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PostAsJsonAsync(url, new { characterIds = Array.Empty<Guid>(), body = "Hola" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PostAsJsonAsync(url, new { characterIds = new[] { hero.Id }, body = new string('a', 2001) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Dm.Client.PostAsJsonAsync(url, new { characterIds = new[] { stranger.Id }, body = "Hola" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.PostAsJsonAsync(url, new { characterIds = new[] { hero.Id }, body = "Hola" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.GetAsync($"{url}?limit=0")).StatusCode);
        Assert.Empty(await ListAsync(s.Player, s.CampaignId));
    }

    [Fact]
    public async Task Characters_without_a_player_cannot_receive_messages()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        Guid npcId = Guid.Empty;
        await factory.WithDbAsync(async db =>
        {
            var npc = Dnd.Domain.Characters.Character.Create(s.CampaignId, null, "PNJ", DateTimeOffset.UtcNow);
            db.Characters.Add(npc);
            await db.SaveChangesAsync();
            npcId = npc.Id;
        });

        var response = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/messages", new { characterIds = new[] { npcId }, body = "Hola" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<IReadOnlyList<MessageDto>> SendAsync(SignedInUser dm, Guid campaignId, Guid[] characterIds, string body)
    {
        var response = await dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/messages", new { characterIds, body });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<MessageDto>>())!;
    }

    private static async Task<IReadOnlyList<MessageDto>> ListAsync(SignedInUser actor, Guid campaignId, string query = "")
    {
        var response = await actor.Client.GetAsync($"/api/v1/campaigns/{campaignId}/messages?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<MessageDto>>())!;
    }

    private static async Task<int> UnreadAsync(SignedInUser actor, Guid campaignId)
    {
        var response = await actor.Client.GetAsync($"/api/v1/campaigns/{campaignId}/messages/unread-count");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UnreadCountDto>())!.Count;
    }
}
