using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dnd.Api.Tests.Items;
using Dnd.Application.ChangeRequests;
using Dnd.Application.Characters;

namespace Dnd.Api.Tests.ContentPacks;

/// <summary>Own database: the pack adds a ranger subclass whose level 3 feature grants an animal companion.</summary>
public sealed class CompanionPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>A fictitious pack with <c>features[].companion</c>: choosing, tracking and changing the companion (phase 25, block 6).</summary>
public class CompanionPackTests(CompanionPackApiFactory factory) : IClassFixture<CompanionPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string Subclass = "compas-ejemplo-guardian";

    private static readonly object Companion = new
    {
        beastFilter = new { maxChallengeRating = 0.25, sizes = new[] { "Medium", "Small" } },
        hitPoints = "max(beast, 4*classLevel)",
        proficiencyBonusFromCharacter = true,
        attackBonusFromCharacter = true,
    };

    private static object Pack(object companion) => new
    {
        formatVersion = 2,
        id = "compas-ejemplo",
        name = "Compañeros de Ejemplo",
        version = "1.0.0",
        classesExtended = new[]
        {
            new
            {
                classIndex = "ranger",
                subclasses = new[]
                {
                    new
                    {
                        index = Subclass,
                        name = "Guardián de ejemplo",
                        flavor = "Arquetipo de explorador",
                        description = new[] { "Texto de ejemplo." },
                        levels = new[]
                        {
                            new
                            {
                                level = 3,
                                features = new[]
                                {
                                    new { index = "compas-ejemplo-vinculo", name = "Vínculo de ejemplo", description = new[] { "Texto de ejemplo: una bestia te acompaña." }, companion },
                                },
                            },
                        },
                    },
                },
            },
        },
    };

    private async Task ImportAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        if ((await admin.GetStringAsync(PacksUrl)).Contains("compas-ejemplo", StringComparison.Ordinal))
        {
            return;
        }

        var response = await admin.PostAsync(PacksUrl, Json(Pack(Companion)));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_ranger_reaching_the_feature_chooses_a_beast_within_the_filter_with_its_recalculated_statblock()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Rastreadora");

        // Level 2: no feature yet.
        var second = await PatchAsync(s, hero.Id, level: 2, subclass: null);
        Assert.Null(second.CompanionFeature);
        Assert.False(second.CompanionPending);
        Assert.Equal(HttpStatusCode.Conflict, (await PutAsync(s.Player.Client, hero.Id, "wolf", "Ceniza")).StatusCode);

        var third = await PatchAsync(s, hero.Id, level: 3, subclass: Subclass);
        Assert.True(third.CompanionPending);
        Assert.Null(third.Companion);
        var feature = third.CompanionFeature!;
        Assert.Equal(("compas-ejemplo-vinculo", "ranger", 3, 0.25, "1/4", "max(beast, 4*classLevel)"),
            (feature.FeatureIndex, feature.ClassIndex, feature.ClassLevel, feature.MaxChallengeRating, feature.MaxChallengeRatingText, feature.HitPoints));
        Assert.Equal(["Small", "Medium"], feature.Sizes);

        // Outside the filter: CR 1 and Large; unknown beasts.
        var bear = await PutAsync(s.Player.Client, hero.Id, "brown-bear", "Oso");
        Assert.Equal(HttpStatusCode.BadRequest, bear.StatusCode);
        Assert.Contains("beastIndex", await bear.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await PutAsync(s.Player.Client, hero.Id, "dragon-de-ejemplo", "Nada")).StatusCode);

        var chosen = await PutAsync(s.Player.Client, hero.Id, "wolf", "Ceniza");
        Assert.True(chosen.StatusCode == HttpStatusCode.OK, await chosen.Content.ReadAsStringAsync());
        var detail = (await chosen.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.False(detail.CompanionPending);
        var wolf = detail.Companion!;

        // Wolf: AC 13, 11 HP, Perception +3, Stealth +4, Bite +4 (2d4+2); ranger 3 has proficiency +2.
        Assert.Equal(("wolf", "Wolf", "Ceniza", false), (wolf.BeastIndex, wolf.BeastName, wolf.Name, wolf.BeastMissing));
        Assert.Equal((15, 12, 12), (wolf.ArmorClass, wolf.HitPointsMax, wolf.HitPointsCurrent));
        Assert.Equal([13, 2], wolf.Breakdowns["armorClass"].Parts.Select(p => p.Value));
        Assert.Equal([("base", 11), ("class", 1)], wolf.Breakdowns["hitPointsMax"].Parts.Select(p => (p.Source, p.Value)));
        Assert.Equal((5, 6), (wolf.Skills["perception"], wolf.Skills["stealth"]));
        Assert.Equal(5, wolf.Breakdowns["skill.perception"].Total);
        var bite = Assert.Single(wolf.Attacks, a => a.Name == "Bite");
        Assert.Equal((6, "2d4+4"), (bite.AttackBonus, bite.Damage[0].Dice));
        Assert.Equal([4, 2], bite.AttackBreakdown!.Parts.Select(p => p.Value));
        Assert.Equal([2, 2], bite.DamageBreakdown!.Parts.Select(p => p.Value));

        // Higher level: 4 × 6 = 24 hit points and proficiency +3.
        var sixth = await PatchAsync(s, hero.Id, level: 6, subclass: Subclass);
        Assert.Equal((24, 16), (sixth.Companion!.HitPointsMax, sixth.Companion.ArmorClass));
    }

    [Fact]
    public async Task Companion_hit_points_are_tracked_without_approval()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveRangerWithWolfAsync(s);

        var hurt = await HpAsync(s.Player.Client, hero, new { delta = -5 });
        Assert.Equal(7, hurt.Companion!.HitPointsCurrent);
        Assert.Equal(0, (await HpAsync(s.Player.Client, hero, new { delta = -50 })).Companion!.HitPointsCurrent);
        Assert.Equal(12, (await HpAsync(s.Player.Client, hero, new { delta = 40 })).Companion!.HitPointsCurrent);
        Assert.Equal(9, (await HpAsync(s.Dm.Client, hero, new { current = 9 })).Companion!.HitPointsCurrent);

        var url = $"{ItemTestHelpers.CharacterUrl(hero)}/companion/hp";
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync(url, new { current = 13 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync(url, new { delta = 1, current = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync(url, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.PostAsJsonAsync(url, new { delta = -1 })).StatusCode);

        // A long rest brings it back to full hit points.
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero)}/rest/long", null)).StatusCode);
        var rested = await s.Player.Client.GetFromJsonAsync<CharacterDetailDto>(ItemTestHelpers.CharacterUrl(hero));
        Assert.Equal(12, rested!.Companion!.HitPointsCurrent);
    }

    [Fact]
    public async Task A_player_renames_directly_but_changing_the_beast_needs_the_dm_who_applies_directly()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveRangerWithWolfAsync(s);

        var renamed = await PutAsync(s.Player.Client, hero, "wolf", "Brasa");
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Brasa", (await renamed.Content.ReadFromJsonAsync<CharacterDetailDto>())!.Companion!.Name);

        var asked = await PutAsync(s.Player.Client, hero, "panther", "Sombra");
        Assert.Equal(HttpStatusCode.Accepted, asked.StatusCode);
        var request = (await asked.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        Assert.Equal(("Companion", "Pending"), (request.Type, request.Status));
        Assert.Equal("panther", request.Payload.GetProperty("beastIndex").GetString());
        Assert.Equal(("wolf", "Brasa"), (request.Before!.Value.GetProperty("beastIndex").GetString(), request.Before.Value.GetProperty("name").GetString()));

        // Nothing changes until the DM approves.
        var pending = await s.Player.Client.GetFromJsonAsync<CharacterDetailDto>(ItemTestHelpers.CharacterUrl(hero));
        Assert.Equal("wolf", pending!.Companion!.BeastIndex);
        Assert.Contains(pending.PendingChangeRequests, r => r.Id == request.Id);

        var approved = await s.Dm.Client.PostAsJsonAsync($"/api/v1/change-requests/{request.Id}/approve", new { });
        Assert.True(approved.StatusCode == HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        var panther = (await s.Player.Client.GetFromJsonAsync<CharacterDetailDto>(ItemTestHelpers.CharacterUrl(hero)))!.Companion!;
        Assert.Equal(("panther", "Sombra", 13, 13), (panther.BeastIndex, panther.Name, panther.HitPointsMax, panther.HitPointsCurrent));

        // The DM changes the beast directly; a player cannot remove the companion, the DM can.
        var direct = await PutAsync(s.Dm.Client, hero, "boar", "Colmillo");
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        Assert.Equal("boar", (await direct.Content.ReadFromJsonAsync<CharacterDetailDto>())!.Companion!.BeastIndex);
        var url = $"{ItemTestHelpers.CharacterUrl(hero)}/companion";
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await s.Dm.Client.DeleteAsync(url)).StatusCode);
        var removed = await s.Player.Client.GetFromJsonAsync<CharacterDetailDto>(ItemTestHelpers.CharacterUrl(hero));
        Assert.Null(removed!.Companion);
        Assert.True(removed.CompanionPending);
    }

    [Theory]
    [InlineData("{\"hitPoints\":\"beast\"}", "companion.beastFilter")]
    [InlineData("{\"beastFilter\":{\"maxChallengeRating\":31}}", "companion.beastFilter.maxChallengeRating")]
    [InlineData("{\"beastFilter\":{\"maxChallengeRating\":-1}}", "companion.beastFilter.maxChallengeRating")]
    [InlineData("{\"beastFilter\":{\"sizes\":[\"Medium\"]}}", "companion.beastFilter.maxChallengeRating")]
    [InlineData("{\"beastFilter\":{\"maxChallengeRating\":1,\"sizes\":[\"Enorme\"]}}", "companion.beastFilter.sizes[0]")]
    [InlineData("{\"beastFilter\":{\"maxChallengeRating\":1},\"hitPoints\":\"max(beast, 21*classLevel)\"}", "companion.hitPoints")]
    [InlineData("{\"beastFilter\":{\"maxChallengeRating\":1},\"hitPoints\":\"4*classLevel\"}", "companion.hitPoints")]
    public async Task Invalid_companions_are_reported_with_their_path(string companion, string path)
    {
        var admin = await factory.CreateAdminClientAsync();

        var response = await admin.PostAsync(PacksUrl, Json(Pack(JsonSerializer.Deserialize<JsonElement>(companion))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"features[0].{path}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Companions_require_format_2()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = JsonSerializer.SerializeToNode(Pack(Companion))!;
        pack["formatVersion"] = 1;

        var response = await admin.PostAsync(PacksUrl, new StringContent(pack.ToJsonString(), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("features[0].companion", await response.Content.ReadAsStringAsync());
    }

    /// <summary>An active ranger 3 of the pack's subclass with a wolf named "Ceniza" (12 HP).</summary>
    private static async Task<Guid> ActiveRangerWithWolfAsync(CampaignScenario s)
    {
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Guardiana");
        await PatchAsync(s, hero.Id, level: 3, subclass: Subclass, client: s.Player.Client);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/activate", null)).StatusCode);
        var chosen = await PutAsync(s.Player.Client, hero.Id, "wolf", "Ceniza");
        Assert.True(chosen.StatusCode == HttpStatusCode.OK, await chosen.Content.ReadAsStringAsync());
        return hero.Id;
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, Guid id, string beastIndex, string name) =>
        client.PutAsJsonAsync($"{ItemTestHelpers.CharacterUrl(id)}/companion", new { beastIndex, name });

    private static async Task<CharacterDetailDto> HpAsync(HttpClient client, Guid id, object body)
    {
        var response = await client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(id)}/companion/hp", body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static StringContent Json(object pack) => new(JsonSerializer.Serialize(pack), Encoding.UTF8, "application/json");

    private static async Task<CharacterDetailDto> PatchAsync(CampaignScenario s, Guid id, int level, string? subclass, HttpClient? client = null)
    {
        var patch = await (client ?? s.Dm.Client).PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(id)}/sheet", new
        {
            classes = new[] { new { classIndex = "ranger", subclassIndex = subclass, level } },
            baseAbilities = new { str = 12, dex = 16, con = 14, @int = 10, wis = 14, cha = 8 },
            applyRacialBonuses = false,
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        return (await patch.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }
}
