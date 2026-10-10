using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Characters;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: a fictitious wizard subclass whose feature grants several companions of a closed list.</summary>
public sealed class CompanionsPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Gap H30 of phase 35B3 with a fictitious pack: several companions at once (<c>maxCount</c>), a total challenge rating
/// budget by level, a closed list of creatures of any type, a hit point formula with the character's modifier, bonuses
/// with the proficiency bonus and texts of what the companions gain by level.
/// </summary>
public class CompanionsPackTests(CompanionsPackApiFactory factory) : IClassFixture<CompanionsPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string Id = "sequito-ejemplo";
    private const string Subclass = "sequito-ejemplo-tradicion";
    private const string Puppet = "sequito-ejemplo-titere";
    private const string Shade = "sequito-ejemplo-penumbra";

    private static readonly SemaphoreSlim ImportLock = new(1, 1);

    private static JsonObject Companion() => new()
    {
        ["creatureIndexes"] = new JsonArray(Puppet, Shade),
        ["hitPoints"] = "beast+classLevel+mod:int",
        ["maxCount"] = "proficiencyBonus",
        ["crBudgetByLevel"] = new JsonObject { ["2"] = 0.5, ["6"] = 1 },
        ["bonuses"] = new JsonObject { ["armorClass"] = 1, ["savingThrows"] = "proficiencyBonus", ["attack"] = "proficiencyBonus", ["damage"] = 2 },
        ["featuresByLevel"] = new JsonObject { ["2"] = "Texto de ejemplo: obedecen órdenes sencillas.", ["6"] = "Texto de ejemplo: actúan por su cuenta." },
    };

    private static JsonObject Creature(string index, string name, string type, double cr, int hitPoints) => new()
    {
        ["index"] = index,
        ["name"] = name,
        ["size"] = "Small",
        ["type"] = type,
        ["alignment"] = "unaligned",
        ["armorClass"] = 12,
        ["hitPoints"] = hitPoints,
        ["hitDice"] = "3d6",
        ["speed"] = new JsonObject { ["walk"] = 30 },
        ["abilities"] = new JsonObject { ["str"] = 14, ["dex"] = 12, ["con"] = 10, ["int"] = 3, ["wis"] = 8, ["cha"] = 1 },
        ["passivePerception"] = 9,
        ["challengeRating"] = cr,
        ["xp"] = 50,
        ["actions"] = new JsonArray(new JsonObject
        {
            ["name"] = "Golpe",
            ["description"] = "Texto de ejemplo.",
            ["attackBonus"] = 3,
            ["damage"] = new JsonArray(new JsonObject { ["dice"] = "1d6+1", ["type"] = "bludgeoning" }),
        }),
    };

    private static JsonObject Pack(JsonNode companion) => new()
    {
        ["formatVersion"] = 3,
        ["id"] = Id,
        ["name"] = "Séquito de ejemplo",
        ["version"] = "1.0.0",
        ["classes"] = new JsonArray(new JsonObject
        {
            ["extends"] = "wizard",
            ["subclasses"] = new JsonArray(new JsonObject
            {
                ["index"] = Subclass,
                ["name"] = "Tradición de ejemplo",
                ["flavor"] = "Tradición arcana",
                ["description"] = new JsonArray("Texto de ejemplo."),
                ["levels"] = new JsonArray(new JsonObject
                {
                    ["level"] = 2,
                    ["features"] = new JsonArray(new JsonObject
                    {
                        ["index"] = "sequito-ejemplo-sequito",
                        ["name"] = "Séquito",
                        ["description"] = new JsonArray("Texto de ejemplo: criaturas a tus órdenes."),
                        ["companion"] = companion,
                    }),
                }),
            }),
        }),
        ["creatures"] = new JsonArray(
            Creature(Puppet, "Títere", "construct", 0.25, 10),
            Creature(Shade, "Penumbra", "undead", 0.5, 12)),
    };

    [Fact]
    public async Task A_character_keeps_several_companions_of_its_list_within_the_count_and_the_challenge_budget()
    {
        var s = await ScenarioAsync();
        var hero = await WizardAsync(s, 2);
        var feature = hero.CompanionFeature!;
        Assert.Equal((2, 0.5, "1/2"), (feature.MaxCount, feature.ChallengeBudget, feature.ChallengeBudgetText));
        Assert.Equal([Puppet, Shade], feature.CreatureIndexes);
        Assert.Equal([("construct", "1/4"), ("undead", "1/2")], feature.Creatures.Select(c => (c.Type, c.ChallengeRatingText)));
        Assert.Equal(("1", "proficiencyBonus", "proficiencyBonus", "2"), (feature.Bonuses!.ArmorClass, feature.Bonuses.SavingThrows, feature.Bonuses.Attack, feature.Bonuses.Damage));
        Assert.Equal(2, Assert.Single(feature.Features).Level);
        Assert.True(hero.CompanionPending);

        // A beast of the SRD is not in the list.
        Assert.Equal(HttpStatusCode.BadRequest, (await AddAsync(s.Player.Client, hero.Id, "wolf", "Lobo")).StatusCode);

        var first = await AddOkAsync(s.Player.Client, hero.Id, Puppet, "Uno");
        var puppet = Assert.Single(first.Companions);
        // 10 of the creature + wizard level 2 + Int +3; AC 12 + 1; Str save +2 of the creature + 2 of proficiency.
        Assert.Equal(("construct", 15, 13), (puppet.Type, puppet.HitPointsMax, puppet.ArmorClass));
        Assert.Equal([10, 2, 3], puppet.Breakdowns["hitPointsMax"].Parts.Select(p => p.Value));
        Assert.Equal(6, puppet.SavingThrows.Count);
        Assert.Equal((4, -3), (puppet.SavingThrows["str"], puppet.SavingThrows["cha"]));
        var hit = Assert.Single(puppet.Attacks);
        Assert.Equal((5, "1d6+3"), (hit.AttackBonus, hit.Damage[0].Dice));
        Assert.Equal(2, Assert.Single(puppet.Features).Level);

        var second = await AddOkAsync(s.Player.Client, hero.Id, Puppet, "Dos");
        Assert.Equal(["Uno", "Dos"], second.Companions.Select(c => c.Name));
        Assert.False(second.CompanionPending);
        Assert.Equal(HttpStatusCode.Conflict, (await AddAsync(s.Player.Client, hero.Id, Puppet, "Tres")).StatusCode);

        // Each companion is tracked and dismissed by its id.
        var url = $"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/companions/{second.Companions[1].Id}";
        var hurt = await s.Player.Client.PostAsJsonAsync($"{url}/hp", new { delta = -4 });
        Assert.True(hurt.StatusCode == HttpStatusCode.OK, await hurt.Content.ReadAsStringAsync());
        var tracked = (await hurt.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal([15, 11], tracked.Companions.Select(c => c.HitPointsCurrent));
        Assert.Equal(HttpStatusCode.NoContent, (await s.Player.Client.DeleteAsync(url)).StatusCode);

        // 1/4 + 1/2 is over the budget of level 2.
        var over = await AddAsync(s.Player.Client, hero.Id, Shade, "Sombra");
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Contains("beastIndex", await over.Content.ReadAsStringAsync());

        // Level 6: budget 1, proficiency +3 and the second text.
        var sixth = await PatchAsync(s, hero.Id, 6);
        Assert.Equal((1.0, 2), (sixth.CompanionFeature!.ChallengeBudget, sixth.CompanionFeature!.Features.Count));
        var added = await AddOkAsync(s.Player.Client, hero.Id, Shade, "Sombra");
        Assert.Equal([Puppet, Shade], added.Companions.Select(c => c.BeastIndex));
        Assert.Equal(12 + 6 + 3, added.Companions[1].HitPointsMax);
        Assert.Equal(6, added.Companions[0].Attacks[0].AttackBonus);
    }

    [Theory]
    [InlineData("{\"creatureIndexes\":[\"sequito-ejemplo-nadie\"]}", "companion.creatureIndexes[0]")]
    [InlineData("{\"creatureIndexes\":[\"sequito-ejemplo-titere\"],\"maxCount\":0}", "companion.maxCount")]
    [InlineData("{\"creatureIndexes\":[\"sequito-ejemplo-titere\"],\"maxCount\":\"todos\"}", "companion.maxCount")]
    [InlineData("{\"creatureIndexes\":[\"sequito-ejemplo-titere\"],\"crBudgetByLevel\":{\"0\":1}}", "companion.crBudgetByLevel.0")]
    [InlineData("{\"creatureIndexes\":[\"sequito-ejemplo-titere\"],\"crBudgetByLevel\":{\"3\":-1}}", "companion.crBudgetByLevel.3")]
    [InlineData("{\"creatureIndexes\":[\"sequito-ejemplo-titere\"],\"bonuses\":{\"attack\":\"mucho\"}}", "companion.bonuses.attack")]
    [InlineData("{\"creatureIndexes\":[\"sequito-ejemplo-titere\"],\"featuresByLevel\":{\"21\":\"x\"}}", "companion.featuresByLevel.21")]
    [InlineData("{\"creatureIndexes\":[\"sequito-ejemplo-titere\"],\"hitPoints\":\"beast+mod:suerte\"}", "companion.hitPoints")]
    [InlineData("{\"creatureIndexes\":[\"sequito-ejemplo-titere\"],\"hitPoints\":\"beast+beast\"}", "companion.hitPoints")]
    public async Task Invalid_companion_extensions_are_reported_with_their_path(string companion, string path)
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = JsonNode.Parse(Pack(JsonNode.Parse(companion)!).ToJsonString().Replace(Id, "sequito-malo", StringComparison.Ordinal))!;

        var response = await admin.PostAsync(PacksUrl, Json(pack));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.True(body.Contains($"features[0].{path}", StringComparison.Ordinal), body);
    }

    private async Task<CampaignScenario> ScenarioAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        await ImportLock.WaitAsync();
        try
        {
            if (!(await admin.GetStringAsync(PacksUrl)).Contains($"\"{Id}\"", StringComparison.Ordinal))
            {
                var response = await admin.PostAsync(PacksUrl, Json(Pack(Companion())));
                Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            }
        }
        finally
        {
            ImportLock.Release();
        }

        var s = await factory.CreateCampaignScenarioAsync();
        await s.EnablePacksAsync(Id);
        return s;
    }

    private static async Task<CharacterDetailDto> WizardAsync(CampaignScenario s, int level)
    {
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Titiritera");
        await PatchAsync(s, hero.Id, level);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.ActivateAsync(hero.Id)).StatusCode);
        return await s.Player.GetCharacterAsync(hero.Id);
    }

    private static async Task<CharacterDetailDto> PatchAsync(CampaignScenario s, Guid id, int level)
    {
        var patch = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/sheet", new
        {
            classes = new[] { new { classIndex = "wizard", subclassIndex = Subclass, level } },
            baseAbilities = new { str = 8, dex = 14, con = 12, @int = 16, wis = 12, cha = 10 },
            applyRacialBonuses = false,
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        return (await patch.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, Guid id, string beastIndex, string name) =>
        client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/companions", new { beastIndex, name });

    private static async Task<CharacterDetailDto> AddOkAsync(HttpClient client, Guid id, string beastIndex, string name)
    {
        var response = await AddAsync(client, id, beastIndex, name);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static StringContent Json(JsonNode pack) => new(pack.ToJsonString(), Encoding.UTF8, "application/json");
}
