using System.Net;
using System.Net.Http.Json;
using Dnd.Api.Tests.Items;
using Dnd.Application.Characters;
using Dnd.Application.Party;

namespace Dnd.Api.Tests.Party;

[Collection(CatalogCollection.Name)]
public class PartyEndpointsTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task Players_cannot_use_the_party_tools_and_outsiders_do_not_see_them()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Héroe");

        foreach (var actor in new[] { s.Player, s.Outsider })
        {
            var expected = actor == s.Player ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound;
            Assert.Equal(expected, (await actor.Client.GetAsync(PartyUrl(s.CampaignId))).StatusCode);
            Assert.Equal(expected, (await actor.Client.PostAsJsonAsync($"{PartyUrl(s.CampaignId)}/rest", new { kind = "long" })).StatusCode);
            Assert.Equal(expected, (await actor.Client.PostAsJsonAsync($"{PartyUrl(s.CampaignId)}/adjust", new[] { new { characterId = hero.Id, hitPointsDelta = -1 } })).StatusCode);
        }
    }

    [Fact]
    public async Task The_party_lists_the_active_characters_by_name_with_their_vitals()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Zora");
        var aldo = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Aldo");
        await s.Player.CreateCharacterAsync(s.CampaignId, "Borrador");

        var party = await GetPartyAsync(s.Dm, s.CampaignId);

        Assert.Equal(["Aldo", "Zora"], party.Characters.Select(c => c.Name));
        var member = party.Characters[0];
        Assert.Equal((aldo.Id, s.Player.Id, "Player User", 3), (member.Id, member.OwnerUserId, member.OwnerDisplayName, member.Level));
        Assert.Equal(("fighter", "Fighter"), (member.Classes[0].ClassIndex, member.Classes[0].ClassName));
        Assert.Equal(aldo.Sheet.HitPointsMax, member.HitPointsMax);
        Assert.Equal(member.HitPointsMax, member.HitPointsCurrent);
        Assert.Equal((aldo.Sheet.ArmorClass, aldo.Sheet.Initiative, aldo.Sheet.PassivePerception, aldo.Sheet.Speed),
            (member.ArmorClass, member.Initiative, member.PassivePerception, member.Speed));
        Assert.Empty(member.SpellSlots);
        Assert.Null(member.PactSlots);
    }

    [Fact]
    public async Task A_long_rest_restores_the_hit_points_of_everyone()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var a = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "A");
        var b = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "B");
        await AdjustAsync(s.Dm, s.CampaignId, new[] { new { characterId = a.Id, hitPointsDelta = -5 }, new { characterId = b.Id, hitPointsDelta = -9 } });

        var party = await RestAsync(s.Dm, s.CampaignId, new { kind = "long" });

        Assert.All(party.Characters, c => Assert.Equal(c.HitPointsMax, c.HitPointsCurrent));
    }

    [Fact]
    public async Task A_short_rest_for_some_characters_recovers_their_short_rest_resources_without_spending_hit_dice()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var rested = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Descansa");
        var tired = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "No descansa");
        foreach (var character in new[] { rested, tired })
        {
            var secondWind = character.Resources.Single(r => r.Key == "second-wind");
            Assert.Equal(HttpStatusCode.OK, (await s.Player.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/resources/{secondWind.Id}/spend", null)).StatusCode);
        }

        await AdjustAsync(s.Dm, s.CampaignId, new[] { new { characterId = rested.Id, hitPointsDelta = -4 } });
        var party = await RestAsync(s.Dm, s.CampaignId, new { kind = "short", characterIds = new[] { rested.Id } });

        var restedDetail = await s.Player.GetCharacterAsync(rested.Id);
        var tiredDetail = await s.Player.GetCharacterAsync(tired.Id);
        Assert.Equal(0, restedDetail.Resources.Single(r => r.Key == "second-wind").Used);
        Assert.Equal(1, tiredDetail.Resources.Single(r => r.Key == "second-wind").Used);
        Assert.Empty(restedDetail.HitDiceUsed);
        Assert.Equal(restedDetail.Sheet.HitPointsMax - 4, party.Characters.Single(c => c.Id == rested.Id).HitPointsCurrent);
    }

    [Fact]
    public async Task Resting_a_character_of_another_campaign_is_not_found()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateCampaignScenarioAsync();
        var stranger = await ActiveFighterAsync(other.Player, other.Dm, other.CampaignId, "Ajeno");

        var response = await s.Dm.Client.PostAsJsonAsync($"{PartyUrl(s.CampaignId)}/rest", new { kind = "long", characterIds = new[] { stranger.Id } });
        var invalid = await s.Dm.Client.PostAsJsonAsync($"{PartyUrl(s.CampaignId)}/rest", new { kind = "nap" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Damage_beyond_the_temporary_hit_points_lowers_the_current_ones()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Héroe");
        await AdjustAsync(s.Dm, s.CampaignId, new[] { new { characterId = hero.Id, temporaryHitPoints = 5 } });

        var party = await AdjustAsync(s.Dm, s.CampaignId, new[] { new { characterId = hero.Id, hitPointsDelta = -8 } });

        var member = Assert.Single(party.Characters);
        Assert.Equal((0, member.HitPointsMax - 3), (member.TemporaryHitPoints, member.HitPointsCurrent));

        var healed = Assert.Single((await AdjustAsync(s.Dm, s.CampaignId, new[] { new { characterId = hero.Id, hitPointsDelta = 100 } })).Characters);
        Assert.Equal(healed.HitPointsMax, healed.HitPointsCurrent);
    }

    [Fact]
    public async Task Hit_points_max_overrides_the_maximum_caps_the_current_and_zero_removes_it()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Héroe");
        var calculated = hero.Sheet.HitPointsMax;

        var raised = Assert.Single((await AdjustAsync(s.Dm, s.CampaignId, new[] { new { characterId = hero.Id, hitPointsMax = 50 } })).Characters);
        Assert.Equal((50, calculated), (raised.HitPointsMax, raised.HitPointsCurrent));
        var detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal(50, Assert.Single(detail.Overrides, o => o.Field == "hitPointsMax").Value);
        Assert.Contains("hitPointsMax", detail.Sheet.OverriddenFields);

        var lowered = Assert.Single((await AdjustAsync(s.Dm, s.CampaignId, new[] { new { characterId = hero.Id, hitPointsMax = 10 } })).Characters);
        Assert.Equal((10, 10), (lowered.HitPointsMax, lowered.HitPointsCurrent));

        var removed = Assert.Single((await AdjustAsync(s.Dm, s.CampaignId, new[] { new { characterId = hero.Id, hitPointsMax = 0 } })).Characters);
        Assert.Equal((calculated, 10), (removed.HitPointsMax, removed.HitPointsCurrent));
        Assert.DoesNotContain((await s.Player.GetCharacterAsync(hero.Id)).Overrides, o => o.Field == "hitPointsMax");
    }

    [Fact]
    public async Task Conditions_are_added_without_duplicates_and_removed_by_index()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Héroe");
        await AdjustAsync(s.Dm, s.CampaignId, new[]
        {
            new { characterId = hero.Id, addConditions = new[] { new { index = "poisoned", note = (string?)"Veneno de araña" }, new { index = "prone", note = (string?)null } } },
        });

        var party = await AdjustAsync(s.Dm, s.CampaignId, new[]
        {
            new
            {
                characterId = hero.Id,
                addConditions = new[] { new { index = "poisoned", note = (string?)"Otra" }, new { index = "blinded", note = (string?)null } },
                removeConditions = new[] { "prone" },
            },
        });

        var conditions = Assert.Single(party.Characters).Conditions;
        Assert.Equal(["poisoned", "blinded"], conditions.Select(c => c.Index));
        Assert.Equal("Veneno de araña", conditions[0].Note);
    }

    [Fact]
    public async Task Invalid_adjustments_are_rejected()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Héroe");
        var url = $"{PartyUrl(s.CampaignId)}/adjust";

        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PostAsJsonAsync(url, new[] { new { characterId = hero.Id, hitPointsDelta = -1000 } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PostAsJsonAsync(url, new[] { new { characterId = hero.Id }, new { characterId = hero.Id } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PostAsJsonAsync(url, new[] { new { characterId = hero.Id, addConditions = new[] { new { index = " " } } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PostAsJsonAsync(url, Array.Empty<object>())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Dm.Client.PostAsJsonAsync(url, new[] { new { characterId = Guid.NewGuid(), hitPointsDelta = -1 } })).StatusCode);
    }

    internal static string PartyUrl(Guid campaignId) => $"/api/v1/campaigns/{campaignId}/party";

    /// <summary>A level 3 fighter (Con 14) of <paramref name="owner"/>, activated by <paramref name="dm"/>.</summary>
    internal static async Task<CharacterDetailDto> ActiveFighterAsync(SignedInUser owner, SignedInUser dm, Guid campaignId, string name)
    {
        var character = await owner.CreateCharacterAsync(campaignId, name);
        var patch = await owner.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", level = 3 } },
            baseAbilities = new { str = 16, dex = 12, con = 14, @int = 10, wis = 10, cha = 10 },
            applyRacialBonuses = false,
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var activate = await dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        return (await activate.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    internal static async Task<PartyDto> RestAsync(SignedInUser dm, Guid campaignId, object body)
    {
        var response = await dm.Client.PostAsJsonAsync($"{PartyUrl(campaignId)}/rest", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyDto>())!;
    }

    private static async Task<PartyDto> GetPartyAsync(SignedInUser dm, Guid campaignId)
    {
        var response = await dm.Client.GetAsync(PartyUrl(campaignId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyDto>())!;
    }

    private static async Task<PartyDto> AdjustAsync(SignedInUser dm, Guid campaignId, object body)
    {
        var response = await dm.Client.PostAsJsonAsync($"{PartyUrl(campaignId)}/adjust", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyDto>())!;
    }
}
