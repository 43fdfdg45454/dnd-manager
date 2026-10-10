using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Catalog;

namespace OpenTrpg.Core.Api.Tests;

[Collection(CatalogCollection.Name)]
public class BeastEndpointsTests(CatalogApiFactory factory)
{
    private const string Base = "/api/v1/catalog/beasts";

    private async Task<T> GetAsync<T>(string url)
    {
        var client = (await factory.CreateSignedInUserAsync()).Client;
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Fact]
    public async Task Beasts_require_authentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Base)).StatusCode);
    }

    [Fact]
    public async Task Lists_every_srd_beast_ordered_by_challenge_rating()
    {
        var beasts = await GetAsync<List<BeastSummaryDto>>(Base);

        Assert.Equal(87, beasts.Count);
        Assert.Equal(beasts.OrderBy(b => b.ChallengeRating).Select(b => b.Index), beasts.Select(b => b.Index));
        var wolf = Assert.Single(beasts, b => b.Index == "wolf");
        Assert.Equal(("Wolf", "Medium", 0.25, "1/4", 13, 11), (wolf.Name, wolf.Size, wolf.ChallengeRating, wolf.ChallengeRatingText, wolf.ArmorClass, wolf.HitPoints));
        Assert.Equal(40, wolf.Speeds["walk"]);
    }

    [Fact]
    public async Task Filters_by_challenge_rating_and_movement_like_wild_shape()
    {
        var level2 = await GetAsync<List<BeastSummaryDto>>($"{Base}?maxCr=0.25&fly=false&swim=false");
        var moon = await GetAsync<List<BeastSummaryDto>>($"{Base}?maxCr=1&fly=false");
        var flyers = await GetAsync<List<BeastSummaryDto>>($"{Base}?fly=true&q=eagle");

        Assert.Equal(31, level2.Count);
        Assert.All(level2, b => Assert.True(b.ChallengeRating <= 0.25 && !b.Speeds.ContainsKey("fly") && !b.Speeds.ContainsKey("swim")));
        Assert.Contains(level2, b => b.Index == "wolf");
        Assert.Contains(moon, b => b.Index == "brown-bear");
        Assert.Contains(moon, b => b.Index == "giant-octopus");
        Assert.DoesNotContain(moon, b => b.Index == "giant-eagle");
        Assert.Equal(["eagle", "giant-eagle"], flyers.Select(b => b.Index).Order());
    }

    [Fact]
    public async Task Statblock_has_abilities_skills_senses_traits_and_rollable_actions()
    {
        var wolf = await GetAsync<BeastDto>($"{Base}/wolf");

        Assert.Equal(("2d8", "2d8+2", "natural"), (wolf.HitDice, wolf.HitPointsRoll, wolf.ArmorClassType));
        Assert.Equal(new Dictionary<string, int> { ["str"] = 12, ["dex"] = 15, ["con"] = 12, ["int"] = 3, ["wis"] = 12, ["cha"] = 6 }, wolf.Abilities);
        Assert.Equal(new Dictionary<string, int> { ["perception"] = 3, ["stealth"] = 4 }, wolf.Skills);
        Assert.Equal(13, wolf.PassivePerception);
        Assert.Contains(wolf.Traits, t => t.Name == "Pack Tactics");
        var bite = Assert.Single(wolf.Actions);
        Assert.Equal(("Bite", 4, false), (bite.Name, bite.AttackBonus, bite.IsMultiattack));
        Assert.Equal(new BeastDamageDto("2d4+2", "Piercing"), Assert.Single(bite.Damage));
        Assert.Equal(new BeastSaveDto(11, "str"), bite.Save);
    }

    [Fact]
    public async Task Statblock_includes_special_senses_and_multiattack()
    {
        var bear = await GetAsync<BeastDto>($"{Base}/brown-bear");
        var bat = await GetAsync<BeastDto>($"{Base}/giant-bat");

        Assert.Contains(bear.Actions, a => a.IsMultiattack && a.AttackBonus is null);
        Assert.Equal(("1", 1.0), (bear.ChallengeRatingText, bear.ChallengeRating));
        Assert.Equal("60 ft.", bat.Senses["blindsight"]);
        Assert.Equal(60, bat.Speeds["fly"]);
    }

    [Fact]
    public async Task Unknown_beast_returns_404_and_invalid_filters_400()
    {
        var client = (await factory.CreateSignedInUserAsync()).Client;

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/tarrasque")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"{Base}?maxCr=-1")).StatusCode);
    }
}
