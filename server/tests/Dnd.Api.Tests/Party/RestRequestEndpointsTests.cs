using System.Net;
using System.Net.Http.Json;
using Dnd.Api.Tests.Items;
using Dnd.Application.Characters;
using Dnd.Application.Party;
using Microsoft.AspNetCore.Mvc;
using static Dnd.Api.Tests.Party.PartyEndpointsTests;

namespace Dnd.Api.Tests.Party;

/// <summary>Rests asked to the DM (phase 16b).</summary>
[Collection(CatalogCollection.Name)]
public class RestRequestEndpointsTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task A_player_asks_for_a_short_rest_and_the_dm_approval_spends_the_dice()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Guerrera");
        await HurtAsync(s.Player, hero.Id, 1);

        var request = await AskAsync(s.Player, hero.Id, new { kind = "short", hitDice = new Dictionary<string, int> { ["fighter"] = 2 } });

        Assert.Equal(("Short", "Pending", hero.Id, s.Player.Id), (request.Kind, request.Status, request.CharacterId, request.RequestedByUserId));
        Assert.Equal(new Dictionary<string, int> { ["fighter"] = 2 }, request.HitDice);
        var pending = (await s.Player.GetCharacterAsync(hero.Id)).PendingRest;
        Assert.Equal((request.Id, "Short"), (pending?.Id, pending?.Kind));
        Assert.Equal(1, (await s.Player.GetCharacterAsync(hero.Id)).HitPointsCurrent);
        var listed = Assert.Single(await ListAsync(s.Dm, s.CampaignId, "Pending"));
        Assert.Equal((request.Id, "Guerrera", "Player User"), (listed.Id, listed.CharacterName, listed.RequestedByDisplayName));
        Assert.Equal(request.Id, (await GetPartyAsync(s.Dm, s.CampaignId)).Characters.Single().PendingRest?.Id);

        var approved = await ResolveAsync(s.Dm, request.Id, "approve", new { comment = "Descansad junto al fuego." });

        Assert.Equal(("Approved", (Guid?)s.Dm.Id, "Descansad junto al fuego."), (approved.Status, approved.ResolvedByUserId, approved.Comment));
        var rested = await s.Player.GetCharacterAsync(hero.Id);
        // Two d10 + Con (+2) each: at least 3 hit points each.
        Assert.InRange(rested.HitPointsCurrent, 7, 25);
        Assert.Equal(2, rested.HitDiceUsed["fighter"]);
        Assert.Null(rested.PendingRest);
        Assert.Empty(await ListAsync(s.Dm, s.CampaignId, "Pending"));
    }

    [Fact]
    public async Task An_approved_long_rest_restores_the_hit_points()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Cansado");
        await HurtAsync(s.Player, hero.Id, 2);
        var request = await AskAsync(s.Player, hero.Id, new { kind = "LONG" });

        await ResolveAsync(s.Dm, request.Id, "approve", null);

        var rested = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal(rested.Sheet.HitPointsMax, rested.HitPointsCurrent);
    }

    [Fact]
    public async Task Players_cannot_rest_directly()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Impaciente");

        var longRest = await s.Player.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/rest/long", null);
        var shortRest = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/rest/short", new { });

        Assert.Equal(HttpStatusCode.Forbidden, longRest.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, shortRest.StatusCode);
        Assert.Contains("Pide el descanso al DM", (await longRest.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
    }

    [Fact]
    public async Task There_is_one_pending_request_per_character_and_the_owner_can_cancel_it()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Dormilón");
        var first = await AskAsync(s.Player, hero.Id, new { kind = "long" });

        var second = await s.Player.Client.PostAsJsonAsync(RequestsUrl(hero.Id), new { kind = "short" });
        var dmCancel = await s.Dm.Client.DeleteAsync(RequestsUrl(hero.Id));
        var cancel = await s.Player.Client.DeleteAsync(RequestsUrl(hero.Id));
        var again = await s.Player.Client.DeleteAsync(RequestsUrl(hero.Id));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, dmCancel.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        var cancelled = await GetAsync(s.Player, first.Id);
        Assert.Equal(("Cancelled", (Guid?)s.Player.Id), (cancelled.Status, cancelled.ResolvedByUserId));
        await AskAsync(s.Player, hero.Id, new { kind = "short" });
    }

    [Fact]
    public async Task Only_the_owner_asks_and_only_a_dm_resolves()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var otherPlayer = await factory.CreateSignedInUserAsync("Other Player");
        await s.Owner.AddMemberAsync(s.CampaignId, otherPlayer, CampaignScenario.PlayerRole);
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Propio");

        Assert.Equal(HttpStatusCode.Forbidden, (await otherPlayer.Client.PostAsJsonAsync(RequestsUrl(hero.Id), new { kind = "long" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.PostAsJsonAsync(RequestsUrl(hero.Id), new { kind = "long" })).StatusCode);
        var request = await AskAsync(s.Player, hero.Id, new { kind = "long" });

        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.PostAsync($"/api/v1/rest-requests/{request.Id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherPlayer.Client.GetAsync($"/api/v1/rest-requests/{request.Id}")).StatusCode);
        Assert.Empty(await ListAsync(otherPlayer, s.CampaignId, null));
        Assert.Single(await ListAsync(s.Player, s.CampaignId, null));

        var rejected = await ResolveAsync(s.Dm, request.Id, "reject", null);
        var late = await s.Dm.Client.PostAsync($"/api/v1/rest-requests/{request.Id}/approve", null);

        Assert.Equal(("Rejected", (string?)null), (rejected.Status, rejected.Comment));
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
    }

    [Fact]
    public async Task Invalid_requests_are_refused()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Validado");
        var draft = await s.Player.CreateCharacterAsync(s.CampaignId, "Borrador");

        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync(RequestsUrl(hero.Id), new { kind = "nap" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync(RequestsUrl(hero.Id), new { kind = "long", hitDice = new Dictionary<string, int> { ["fighter"] = 1 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync(RequestsUrl(hero.Id), new { kind = "short", hitDice = new Dictionary<string, int> { ["wizard"] = 1 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync(RequestsUrl(hero.Id), new { kind = "short", hitDice = new Dictionary<string, int> { ["fighter"] = 4 } })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Player.Client.PostAsJsonAsync(RequestsUrl(draft.Id), new { kind = "long" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.GetAsync($"/api/v1/campaigns/{s.CampaignId}/rest-requests?status=pending")).StatusCode);
    }

    [Fact]
    public async Task Approval_spends_only_the_dice_that_remain()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Menguante");
        await HurtAsync(s.Player, hero.Id, 1);
        var request = await AskAsync(s.Player, hero.Id, new { kind = "short", hitDice = new Dictionary<string, int> { ["fighter"] = 3 } });

        // The DM lowers the level before approving: only 2 dice remain.
        var patch = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/sheet", new { classes = new[] { new { classIndex = "fighter", level = 2 } } });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        await ResolveAsync(s.Dm, request.Id, "approve", null);

        Assert.Equal(2, (await s.Player.GetCharacterAsync(hero.Id)).HitDiceUsed["fighter"]);
    }

    [Fact]
    public async Task A_forced_party_rest_cancels_the_pending_requests()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Paciente");
        var request = await AskAsync(s.Player, hero.Id, new { kind = "short" });

        var party = await RestAsync(s.Dm, s.CampaignId, new { kind = "long" });

        Assert.Null(party.Characters.Single().PendingRest);
        var cancelled = await GetAsync(s.Player, request.Id);
        Assert.Equal(("Cancelled", (Guid?)s.Dm.Id), (cancelled.Status, cancelled.ResolvedByUserId));
    }

    [Fact]
    public async Task A_direct_rest_by_the_dm_cancels_the_pending_request()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Atendido");
        var request = await AskAsync(s.Player, hero.Id, new { kind = "long" });

        var response = await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/rest/long", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!.PendingRest);
        Assert.Equal("Cancelled", (await GetAsync(s.Dm, request.Id)).Status);
    }

    private static string RequestsUrl(Guid characterId) => $"{ItemTestHelpers.CharacterUrl(characterId)}/rest-requests";

    private static async Task HurtAsync(SignedInUser player, Guid characterId, int hitPoints)
    {
        var response = await player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(characterId)}/combat", new { hitPointsCurrent = hitPoints });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<RestRequestDto> AskAsync(SignedInUser player, Guid characterId, object body)
    {
        var response = await player.Client.PostAsJsonAsync(RequestsUrl(characterId), body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<RestRequestDto>())!;
        Assert.Equal($"/api/v1/rest-requests/{created.Id}", response.Headers.Location?.OriginalString);
        return created;
    }

    private static async Task<RestRequestDto> ResolveAsync(SignedInUser dm, Guid requestId, string action, object? body)
    {
        var url = $"/api/v1/rest-requests/{requestId}/{action}";
        var response = body is null ? await dm.Client.PostAsync(url, null) : await dm.Client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RestRequestDto>())!;
    }

    private static async Task<RestRequestDto> GetAsync(SignedInUser actor, Guid requestId)
    {
        var response = await actor.Client.GetAsync($"/api/v1/rest-requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RestRequestDto>())!;
    }

    private static async Task<IReadOnlyList<RestRequestDto>> ListAsync(SignedInUser actor, Guid campaignId, string? status)
    {
        var response = await actor.Client.GetAsync($"/api/v1/campaigns/{campaignId}/rest-requests{(status is null ? string.Empty : $"?status={status}")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<RestRequestDto>>())!;
    }

    private static async Task<PartyDto> GetPartyAsync(SignedInUser dm, Guid campaignId)
    {
        var response = await dm.Client.GetAsync(PartyUrl(campaignId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyDto>())!;
    }
}
