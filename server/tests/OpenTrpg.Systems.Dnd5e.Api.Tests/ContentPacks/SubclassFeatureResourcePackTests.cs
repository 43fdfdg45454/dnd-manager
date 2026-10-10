using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the pack adds a fighter subclass with resources on its features.</summary>
public sealed class FeatureResourcePackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// A fictitious pack whose subclass features carry resources: a by-level maximum with a die by level and a compound
/// formula (phase 25, block 2).
/// </summary>
public class SubclassFeatureResourcePackTests(FeatureResourcePackApiFactory factory) : IClassFixture<FeatureResourcePackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";

    private static object Pack(object tacticsResource, object wardResource) => new
    {
        formatVersion = 3,
        id = "tacticos-ejemplo",
        name = "Tácticos de Ejemplo",
        version = "1.0.0",
        classes = new[]
        {
            new
            {
                extends = "fighter",
                subclasses = new[]
                {
                    new
                    {
                        index = "tacticos-ejemplo-estratega",
                        name = "Estratega de ejemplo",
                        flavor = "Arquetipo marcial",
                        description = new[] { "Texto de ejemplo." },
                        levels = new[]
                        {
                            new
                            {
                                level = 3,
                                features = new[]
                                {
                                    new { index = "tacticos-ejemplo-ventaja", name = "Ventaja táctica", description = new[] { "Texto de ejemplo: gastas un dado de táctica." }, resource = tacticsResource },
                                    new { index = "tacticos-ejemplo-escudo", name = "Escudo de ejemplo", description = new[] { "Texto de ejemplo." }, resource = wardResource },
                                },
                            },
                        },
                    },
                },
            },
        },
    };

    private static readonly object Tactics = new
    {
        key = "tacticos-ejemplo-dados",
        name = "Dados de táctica",
        max = new { byLevel = new Dictionary<string, int> { ["3"] = 4, ["7"] = 5, ["15"] = 6 } },
        recharge = "ShortRest",
        dice = "d8",
        diceByLevel = new Dictionary<string, string> { ["10"] = "d10", ["18"] = "d12" },
    };

    private static readonly object Ward = new { key = "tacticos-ejemplo-escudo", name = "Escudo", max = "2*classLevel+mod:int", recharge = "LongRest" };

    [Fact]
    public async Task Subclass_feature_resources_appear_at_their_level_with_table_formula_and_die()
    {
        var admin = await factory.CreateAdminClientAsync();
        var import = await admin.PostAsync(PacksUrl, Json(Pack(Tactics, Ward)));
        Assert.True(import.StatusCode == HttpStatusCode.Created, await import.Content.ReadAsStringAsync());

        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Estratega");
        var below = await PatchAsync(s, hero.Id, level: 2, subclass: null);
        Assert.DoesNotContain(below.Resources, r => r.Key is "tacticos-ejemplo-dados" or "tacticos-ejemplo-escudo");

        var third = await PatchAsync(s, hero.Id, level: 3, subclass: "tacticos-ejemplo-estratega");
        var dice = Assert.Single(third.Resources, r => r.Key == "tacticos-ejemplo-dados");
        Assert.Equal((4, "ShortRest", "d8", "Ventaja táctica (nivel 3)", true), (dice.Max, dice.Recharge, dice.Dice, dice.Source, dice.IsAuto));
        Assert.Equal(4, dice.Breakdown!.Total);
        Assert.Contains(third.Combat.Resources, r => r.Key == "tacticos-ejemplo-dados" && r.Dice == "d8");

        // Intelligence 14 (+2): 2 × 3 + 2.
        var ward = Assert.Single(third.Resources, r => r.Key == "tacticos-ejemplo-escudo");
        Assert.Equal((8, "Escudo de ejemplo (nivel 3)", (string?)null), (ward.Max, ward.Source, ward.Dice));
        Assert.Equal(
            [("class", "2 × Nivel de clase", 6), ("ability", "Inteligencia", 2)],
            ward.Breakdown!.Parts.Select(p => (p.Source, p.Label, p.Value)));

        var tenth = await PatchAsync(s, hero.Id, level: 10, subclass: "tacticos-ejemplo-estratega");
        var later = Assert.Single(tenth.Resources, r => r.Key == "tacticos-ejemplo-dados");
        Assert.Equal((5, "d10"), (later.Max, later.Dice));
        Assert.Equal(22, tenth.Resources.Single(r => r.Key == "tacticos-ejemplo-escudo").Max);

        // Another subclass loses them.
        var champion = await PatchAsync(s, hero.Id, level: 10, subclass: "champion");
        Assert.DoesNotContain(champion.Resources, r => r.Key is "tacticos-ejemplo-dados" or "tacticos-ejemplo-escudo");
    }

    [Fact]
    public async Task Bardic_inspiration_exposes_its_die()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var bard = await s.Player.CreateCharacterAsync(s.CampaignId, "Juglar");
        var patch = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(bard.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "bard", level = 5 } },
            baseAbilities = new { str = 8, dex = 14, con = 12, @int = 10, wis = 10, cha = 16 },
            applyRacialBonuses = false,
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var detail = (await patch.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        var inspiration = Assert.Single(detail.Resources, r => r.Key == "bardic-inspiration");
        Assert.Equal((3, "d8", (string?)null), (inspiration.Max, inspiration.Dice, inspiration.Source));
    }

    [Theory]
    [InlineData("\"2*classLevel+\"", "resource.max")]
    [InlineData("\"classLevel-1\"", "resource.max")]
    [InlineData("\"mod:luck\"", "resource.max")]
    [InlineData("0", "resource.max")]
    [InlineData("{\"formula\":\"3*level\"}", "resource.max.formula")]
    [InlineData("{\"formula\":\"mod:wis\",\"min\":-1}", "resource.max.min")]
    [InlineData("{\"byLevel\":{\"0\":2}}", "resource.max.byLevel.0")]
    [InlineData("{\"byLevel\":{\"3\":\"x\"}}", "resource.max.byLevel.3")]
    [InlineData("{\"byLevel\":{}}", "resource.max.byLevel")]
    [InlineData("{\"byLevel\":{\"3\":2},\"formula\":\"classLevel\"}", "resource.max")]
    [InlineData("{\"maximo\":2}", "resource.max.maximo")]
    public async Task Invalid_maximums_are_reported_with_their_path(string max, string path)
    {
        var admin = await factory.CreateAdminClientAsync();
        var bad = JsonSerializer.Deserialize<JsonElement>($$"""{"key":"malo","name":"Malo","max":{{max}}}""");

        var response = await admin.PostAsync(PacksUrl, Json(Pack(bad, Ward)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains($"features[0].{path}", body);
    }

    [Fact]
    public async Task Invalid_dice_are_reported_with_their_path()
    {
        var admin = await factory.CreateAdminClientAsync();
        var bad = new { key = "malo", name = "Malo", max = 2, dice = "d7", diceByLevel = new Dictionary<string, string> { ["5"] = "d3", ["21"] = "d8" } };

        var response = await admin.PostAsync(PacksUrl, Json(Pack(bad, Ward)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        foreach (var path in new[] { "features[0].resource.dice", "features[0].resource.diceByLevel.5", "features[0].resource.diceByLevel.21" })
        {
            Assert.Contains(path, body);
        }
    }

    private static StringContent Json(object pack) => new(JsonSerializer.Serialize(pack), Encoding.UTF8, "application/json");

    private static async Task<CharacterDetailDto> PatchAsync(CampaignScenario s, Guid id, int level, string? subclass)
    {
        var patch = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", subclassIndex = subclass, level } },
            baseAbilities = new { str = 16, dex = 12, con = 14, @int = 14, wis = 10, cha = 8 },
            applyRacialBonuses = false,
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        return (await patch.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }
}
