using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Party;
using static OpenTrpg.Systems.Dnd5e.Api.Tests.Party.PartyEndpointsTests;
using OpenTrpg.Systems.Dnd5e.Application.Party;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.Party;

/// <summary>Level-ups granted by the DM (phase 16b).</summary>
[Collection(CatalogCollection.Name)]
public class LevelGrantEndpointsTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task Granting_a_level_to_two_characters_leaves_both_pending()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var a = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "A");
        var b = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "B");
        var c = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "C");

        var party = await GrantAsync(s.Dm, s.CampaignId, new { characterIds = new[] { a.Id, b.Id } });

        var pending = party.Characters.ToDictionary(m => m.Id, m => m.PendingLevelUpTo);
        Assert.Equal((4, 4, (int?)null), (pending[a.Id], pending[b.Id], pending[c.Id]));
        Assert.Equal(4, (await s.Player.GetCharacterAsync(a.Id)).PendingLevelUpTo);
        Assert.Equal(4, (await s.Player.GetCharacterAsync(b.Id)).PendingLevelUpTo);
    }

    [Fact]
    public async Task Grants_do_not_accumulate_and_can_be_withdrawn()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var a = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "A");
        var b = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "B");

        await GrantAsync(s.Dm, s.CampaignId, null);
        var twice = await GrantAsync(s.Dm, s.CampaignId, new { });
        Assert.All(twice.Characters, m => Assert.Equal(4, m.PendingLevelUpTo));

        var one = await RevokeAsync(s.Dm, s.CampaignId, new { characterIds = new[] { a.Id } });
        Assert.Equal(((int?)null, (int?)4), (one.Characters.Single(m => m.Id == a.Id).PendingLevelUpTo, one.Characters.Single(m => m.Id == b.Id).PendingLevelUpTo));

        var all = await RevokeAsync(s.Dm, s.CampaignId, null);
        Assert.All(all.Characters, m => Assert.Null(m.PendingLevelUpTo));
    }

    [Fact]
    public async Task A_level_20_character_gets_no_grant_and_players_cannot_grant()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Leyenda");
        var patch = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/sheet", new { classes = new[] { new { classIndex = "fighter", level = 20 } } });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var party = await GrantAsync(s.Dm, s.CampaignId, null);

        Assert.Null(party.Characters.Single().PendingLevelUpTo);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.PostAsJsonAsync($"{PartyUrl(s.CampaignId)}/grant-level", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.PostAsJsonAsync($"{PartyUrl(s.CampaignId)}/grant-level", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Dm.Client.PostAsJsonAsync($"{PartyUrl(s.CampaignId)}/grant-level", new { characterIds = new[] { Guid.NewGuid() } })).StatusCode);
    }

    private static async Task<PartyDto> GrantAsync(SignedInUser dm, Guid campaignId, object? body)
    {
        var url = $"{PartyUrl(campaignId)}/grant-level";
        var response = body is null ? await dm.Client.PostAsync(url, null) : await dm.Client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyDto>())!;
    }

    private static async Task<PartyDto> RevokeAsync(SignedInUser dm, Guid campaignId, object? body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{PartyUrl(campaignId)}/grant-level")
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        var response = await dm.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyDto>())!;
    }
}
