using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dnd.Api.Tests.Items;
using Dnd.Application.Catalog;
using Dnd.Application.Characters;

namespace Dnd.Api.Tests.ContentPacks;

/// <summary>Own database: the pack adds a feat to the shared <c>feats</c> set.</summary>
public sealed class OriginPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// A fictitious pack with a variant-human-like race (ability bonuses, a skill and a feat), a background with a tool choice
/// and a feat whose resource is rolled after a long rest (Portent-like). Phase 19.
/// </summary>
public class OriginAndRestRollPackTests(OriginPackApiFactory factory) : IClassFixture<OriginPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";

    private static readonly object Pack = new
    {
        formatVersion = 2,
        id = "augurios-ejemplo",
        name = "Augurios de Ejemplo",
        version = "1.0.0",
        optionSets = new[]
        {
            new
            {
                setId = "feats",
                options = new[]
                {
                    new
                    {
                        index = "augurios-ejemplo-presagio",
                        name = "Presagio de ejemplo",
                        description = new[] { "Texto de ejemplo: tras un descanso largo tiras dos d20 y guardas los resultados." },
                        resource = new
                        {
                            key = "augurios-ejemplo-dados",
                            name = "Dados de augurio",
                            max = 2,
                            recharge = "LongRest",
                            rollOnRest = new { dice = "d20", count = 2, rest = "long" },
                        },
                    },
                },
            },
        },
        races = new[]
        {
            new
            {
                index = "augurios-ejemplo-viajero",
                name = "Viajero de ejemplo",
                speed = 30,
                size = "Medium",
                languages = new[] { "Common" },
                resistances = new[] { "cold" },
                choices = new
                {
                    abilityBonuses = new { choose = 2, amount = 1 },
                    skills = new { choose = 1 },
                    feats = new { choose = 1 },
                },
            },
        },
        backgrounds = new[]
        {
            new
            {
                index = "augurios-ejemplo-vigia",
                name = "Vigía de ejemplo",
                skillProficiencies = new[] { "perception" },
                choices = new { tools = new { choose = 1, from = new[] { "Catalejo de ejemplo", "Brújula de ejemplo" } } },
            },
        },
    };

    [Fact]
    public async Task A_pack_race_asks_for_bonuses_a_skill_and_a_feat_whose_dice_are_rolled_after_long_rests()
    {
        var admin = await factory.CreateAdminClientAsync();
        var import = await admin.PostAsync(PacksUrl, new StringContent(JsonSerializer.Serialize(Pack), Encoding.UTF8, "application/json"));
        Assert.True(import.StatusCode == HttpStatusCode.Created, await import.Content.ReadAsStringAsync());

        var s = await factory.CreateCampaignScenarioAsync();
        var race = await s.Player.Client.GetFromJsonAsync<RaceDetailDto>("/api/v1/catalog/races/augurios-ejemplo-viajero");
        Assert.Equal((2, 6, 1), (race!.Choices!.AbilityBonuses!.Choose, race.Choices.AbilityBonuses.From.Count, race.Choices.Feats!.Choose));
        Assert.Equal(["cold"], race.Resistances);

        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Viajera");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/sheet", new
        {
            raceIndex = "augurios-ejemplo-viajero",
            backgroundIndex = "augurios-ejemplo-vigia",
            classes = new[] { new { classIndex = "wizard", level = 1 } },
            baseAbilities = new { str = 8, dex = 14, con = 13, @int = 15, wis = 12, cha = 10 },
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var plan = await s.Player.Client.GetFromJsonAsync<OriginChoicesDto>(Url(hero.Id));
        Assert.Equal(
            ["background.tools", "race.abilityBonuses", "race.feat", "race.skills"],
            plan!.Choices.Where(c => c.Required > 0).Select(c => c.Key).Order());
        Assert.Contains(Assert.Single(plan.Choices, c => c.Key == "race.feat").Options, o => o.Index == "augurios-ejemplo-presagio");

        var saved = await s.Player.Client.PutAsJsonAsync(Url(hero.Id), new
        {
            choices = new object[]
            {
                new { key = "race.abilityBonuses", selected = new[] { "int", "con" } },
                new { key = "race.skills", selected = new[] { "arcana" } },
                new { key = "race.feat", selected = new { feat = "augurios-ejemplo-presagio" } },
                new { key = "background.tools", selected = new[] { "Catalejo de ejemplo" } },
            },
        });
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        Assert.True((await saved.Content.ReadFromJsonAsync<OriginChoicesDto>())!.Complete);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/activate", null)).StatusCode);

        var detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal(16, detail.Sheet.Abilities["int"].Score);
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Tool", Key: "Catalejo de ejemplo", Source: "Background" });
        Assert.Contains(detail.Sheet.Resistances, r => r is { DamageType: "cold", Source: "race" });
        var dice = Assert.Single(detail.Resources, r => r.Key == "augurios-ejemplo-dados");
        Assert.Equal((2, "d20", 2, "long", true), (dice.Max, dice.RollOnRest!.Dice, dice.RollOnRest.Count, dice.RollOnRest.Rest, dice.RollsPending));
        Assert.True(detail.RestRollsPending);

        Assert.Equal(HttpStatusCode.BadRequest, (await RollAsync(s.Player, hero.Id, dice.Id, 21, 3)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RollAsync(s.Player, hero.Id, dice.Id, 14)).StatusCode);
        var rolled = await RollAsync(s.Player, hero.Id, dice.Id, 14, 3);
        Assert.Equal(HttpStatusCode.OK, rolled.StatusCode);
        var afterRoll = (await rolled.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal([14, 3], afterRoll.Resources.Single(r => r.Id == dice.Id).Rolls);
        Assert.False(afterRoll.RestRollsPending);

        // A short rest keeps the values; a long rest asks for new ones.
        var shortRest = await s.Dm.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/rest/short", new { });
        Assert.Equal(HttpStatusCode.OK, shortRest.StatusCode);
        Assert.False((await shortRest.Content.ReadFromJsonAsync<CharacterDetailDto>())!.RestRollsPending);
        var longRest = await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/rest/long", null);
        Assert.Equal(HttpStatusCode.OK, longRest.StatusCode);
        var rested = (await longRest.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.True(rested.RestRollsPending);
        Assert.Empty(rested.Resources.Single(r => r.Id == dice.Id).Rolls);
    }

    [Fact]
    public async Task Invalid_choices_and_roll_on_rest_are_reported_with_their_path()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = new
        {
            formatVersion = 2,
            id = "malos-ejemplo",
            name = "Malos de Ejemplo",
            version = "1.0.0",
            optionSets = new[]
            {
                new
                {
                    setId = "malos-ejemplo-set",
                    name = "Set",
                    options = new[]
                    {
                        new
                        {
                            index = "malos-ejemplo-opcion",
                            name = "Opción",
                            resource = new { key = "malos-ejemplo-dados", name = "Dados", max = 1, recharge = "LongRest", rollOnRest = new { dice = "d7", count = 0, rest = "siesta" } },
                        },
                    },
                },
            },
            races = new[]
            {
                new
                {
                    index = "malos-ejemplo-raza",
                    name = "Raza",
                    speed = 30,
                    size = "Medium",
                    resistances = new[] { "slime" },
                    choices = new { abilityBonuses = new { choose = 2, from = new[] { "luck" } }, skills = new { choose = 1, from = new[] { "juggling" } } },
                },
            },
        };

        var response = await admin.PostAsync(PacksUrl, new StringContent(JsonSerializer.Serialize(pack), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        foreach (var path in new[] { "rollOnRest.dice", "rollOnRest.count", "rollOnRest.rest", "races[0].resistances[0]", "races[0].choices.abilityBonuses.from[0]", "races[0].choices.skills.from[0]" })
        {
            Assert.Contains(path, body);
        }
    }

    private static string Url(Guid id) => $"{ItemTestHelpers.CharacterUrl(id)}/origin-choices";

    private static Task<HttpResponseMessage> RollAsync(SignedInUser actor, Guid characterId, Guid resourceId, params int[] values) =>
        actor.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(characterId)}/resources/{resourceId}/rolls", new { values });
}
