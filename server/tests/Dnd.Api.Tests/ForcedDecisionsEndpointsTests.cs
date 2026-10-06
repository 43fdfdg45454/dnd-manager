using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dnd.Api.Tests.Items;
using Dnd.Application.Characters;
using Dnd.Application.Party;

namespace Dnd.Api.Tests;

/// <summary>Concentration checks, Natural Recovery and forced replacement of invalid feats (phase 19).</summary>
[Collection(CatalogCollection.Name)]
public class ForcedDecisionsEndpointsTests(CatalogApiFactory factory)
{
    private static readonly object ClericScores = new { str = 10, dex = 10, con = 16, @int = 10, wis = 16, cha = 10 };

    // ---- Concentration -------------------------------------------------------------------------------

    [Fact]
    public async Task Damage_to_a_concentrating_cleric_returns_the_constitution_save_dc()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await ActiveAsync(s, "cleric", 10, ClericScores);
        await ConcentrateAsync(s.Player, cleric.Id, "bless");

        var heavy = await DamageAsync(s.Player, cleric.Id, 30);
        Assert.Equal((30, 15, false, "bless"), (heavy.Outcome.Damage, heavy.Outcome.ConcentrationCheckDc, heavy.Outcome.ConcentrationEnded, heavy.Outcome.ConcentratingOn));
        Assert.Equal("bless", heavy.Character.ConcentratingOnSpellIndex);
        Assert.Equal(heavy.Character.Sheet.HitPointsMax - 30, heavy.Character.HitPointsCurrent);

        var light = await DamageAsync(s.Player, cleric.Id, 8);
        Assert.Equal(10, light.Outcome.ConcentrationCheckDc);

        // The player failed the save: the existing endpoint ends the concentration.
        var ended = await ConcentrateAsync(s.Player, cleric.Id, null);
        Assert.Null(ended.ConcentratingOnSpellIndex);
        var noCheck = await DamageAsync(s.Player, cleric.Id, 8);
        Assert.Null(noCheck.Outcome.ConcentrationCheckDc);
    }

    [Fact]
    public async Task Dropping_to_0_hit_points_ends_the_concentration()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await ActiveAsync(s, "cleric", 1, ClericScores);
        await ConcentrateAsync(s.Player, cleric.Id, "bless");

        var result = await DamageAsync(s.Player, cleric.Id, 50);

        Assert.Equal((0, (int?)null, true), (result.Outcome.HitPointsCurrent, result.Outcome.ConcentrationCheckDc, result.Outcome.ConcentrationEnded));
        Assert.Null(result.Character.ConcentratingOnSpellIndex);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync(DamageUrl(cleric.Id), new { amount = 0 })).StatusCode);
    }

    [Fact]
    public async Task Party_damage_reports_the_concentration_check_of_each_character()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await ActiveAsync(s, "cleric", 10, ClericScores);
        await ConcentrateAsync(s.Player, cleric.Id, "bless");

        var response = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/adjust", new[] { new { characterId = cleric.Id, hitPointsDelta = -30 } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var party = (await response.Content.ReadFromJsonAsync<PartyDto>())!;
        var damage = Assert.Single(party.Damage);
        Assert.Equal((cleric.Id, 30, 15, false), (damage.CharacterId, damage.Damage, damage.ConcentrationCheckDc, damage.ConcentrationEnded));
    }

    // ---- Natural Recovery ----------------------------------------------------------------------------

    [Fact]
    public async Task Natural_recovery_is_for_circle_of_the_land_druids_once_per_long_rest()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var land = await ActiveAsync(s, "druid", 4, ClericScores, subclass: "land");
        var plain = await ActiveAsync(s, "druid", 4, ClericScores);
        Assert.Contains(land.Resources, r => r is { Key: "natural-recovery", Max: 1, Recharge: "LongRest" });
        Assert.DoesNotContain(plain.Resources, r => r.Key == "natural-recovery");
        var panel = (JsonElement)Assert.Single(land.Combat.ClassPanels, p => p.ClassIndex == "druid").Data;
        Assert.Equal(2, panel.GetProperty("naturalRecovery").GetProperty("slotLevelsRecoverable").GetInt32());

        foreach (var level in new[] { 1, 1, 2 })
        {
            Assert.Equal(HttpStatusCode.OK, (await s.Player.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(land.Id)}/spell-slots/{level}/spend", null)).StatusCode);
        }

        var excess = await s.Player.Client.PostAsJsonAsync(ActionUrl(land.Id), new { slotLevels = new[] { 2, 1 } });
        var notLand = await s.Player.Client.PostAsJsonAsync(ActionUrl(plain.Id), new { slotLevels = new[] { 1 } });
        var recovered = await s.Player.Client.PostAsJsonAsync(ActionUrl(land.Id), new { slotLevels = new[] { 1, 1 } });
        var again = await s.Player.Client.PostAsJsonAsync(ActionUrl(land.Id), new { slotLevels = new[] { 2 } });

        Assert.Equal(HttpStatusCode.BadRequest, excess.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notLand.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        var detail = (await recovered.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal([(1, 0), (2, 1)], detail.Combat.SpellSlots.Where(x => x.Level <= 2).Select(x => (x.Level, x.Used)));
        Assert.Equal(1, detail.Resources.Single(r => r.Key == "natural-recovery").Used);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    // ---- Invalid choices -----------------------------------------------------------------------------

    [Fact]
    public async Task A_feat_whose_prerequisite_is_lost_is_marked_and_must_be_replaced()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveAsync(s, "fighter", 3, new { str = 13, dex = 14, con = 14, @int = 10, wis = 10, cha = 10 }, subclass: "champion", grant: true);
        var level4 = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/level-up", new
        {
            classIndex = "fighter",
            hitPointsRolled = 6,
            choices = new object[] { new { key = "asi", selected = new { feat = "grappler" } } },
        });
        Assert.True(level4.StatusCode == HttpStatusCode.OK, await level4.Content.ReadAsStringAsync());
        Assert.Empty((await s.Player.GetCharacterAsync(hero.Id)).InvalidChoices);

        // The DM lowers Strength: Grappler (Strength 13) is no longer valid.
        var patch = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/sheet", new { baseAbilities = new { str = 10, dex = 14, con = 14, @int = 10, wis = 10, cha = 10 } });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var detail = await s.Player.GetCharacterAsync(hero.Id);
        var invalid = Assert.Single(detail.InvalidChoices);
        Assert.Equal(("replace.grappler", "fighter", "asi", "feats"), (invalid.ReplaceKey, invalid.ClassIndex, invalid.Key, invalid.SetId));
        Assert.Contains("Fuerza 13", invalid.Reason);

        var forced = await s.Player.Client.GetFromJsonAsync<InvalidChoicesDto>($"{ItemTestHelpers.CharacterUrl(hero.Id)}/invalid-choices");
        var replacement = Assert.Single(forced!.Choices);
        Assert.Equal(("replace.grappler", "AsiOrFeat", true), (replacement.Key, replacement.Kind, replacement.Replaces));
        Assert.Equal("grappler", Assert.Single(replacement.Known).Index);

        // The next level-up asks for it too.
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { hero.Id } })).StatusCode);
        var plan = await s.Player.Client.GetFromJsonAsync<LevelUpPlanDto>($"{ItemTestHelpers.CharacterUrl(hero.Id)}/level-up");
        Assert.Contains(plan!.Choices, c => c.Key == "replace.grappler");

        // The SRD has no other feat: the replacement drops it.
        var replaced = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/invalid-choices", new { choices = Array.Empty<object>() });
        Assert.True(replaced.StatusCode == HttpStatusCode.OK, await replaced.Content.ReadAsStringAsync());
        var after = (await replaced.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Empty(after.InvalidChoices);
        Assert.Contains(after.Choices, c => c.Replaced.Any(r => r.Index == "grappler"));
        Assert.Equal(HttpStatusCode.Conflict, (await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/invalid-choices", new { choices = Array.Empty<object>() })).StatusCode);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    private static string DamageUrl(Guid id) => $"{ItemTestHelpers.CharacterUrl(id)}/damage";

    private static string ActionUrl(Guid id) => $"{ItemTestHelpers.CharacterUrl(id)}/class-actions/natural-recovery";

    private static async Task<CharacterDetailDto> ActiveAsync(CampaignScenario s, string classIndex, int level, object scores, string? subclass = null, bool grant = false)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, classIndex);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex, subclassIndex = subclass, level } },
            baseAbilities = scores,
            applyRacialBonuses = false,
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null)).StatusCode);
        if (grant)
        {
            Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { character.Id } })).StatusCode);
        }

        return await s.Player.GetCharacterAsync(character.Id);
    }

    private static async Task<CharacterDetailDto> ConcentrateAsync(SignedInUser actor, Guid id, string? spellIndex)
    {
        var response = await actor.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(id)}/concentration", new { spellIndex });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static async Task<DamageResultDto> DamageAsync(SignedInUser actor, Guid id, int amount)
    {
        var response = await actor.Client.PostAsJsonAsync(DamageUrl(id), new { amount });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<DamageResultDto>())!;
    }
}
