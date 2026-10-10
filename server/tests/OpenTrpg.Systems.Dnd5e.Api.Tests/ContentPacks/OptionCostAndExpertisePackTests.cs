using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the pack adds options with a cost and choices that order skills before expertise.</summary>
public sealed class OptionCostPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// A fictitious pack (phase 25, block 5): monk techniques that spend ki or a resource of the pack, a cleric domain
/// that grants two skills at level 1 and asks for expertise in them at the same level, and a fighter subclass whose
/// level 3 asks for a skill, a language (<c>after</c> the skill) and expertise.
/// </summary>
public class OptionCostAndExpertisePackTests(OptionCostPackApiFactory factory) : IClassFixture<OptionCostPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string Techniques = "artes-ejemplo-tecnicas";
    private const string CalmStrike = "artes-ejemplo-golpe-sereno";
    private const string Domain = "artes-ejemplo-dominio";
    private const string Scholar = "artes-ejemplo-erudito";

    private static JsonObject Pack() => JsonSerializer.SerializeToNode(new
    {
        formatVersion = 3,
        id = "artes-ejemplo",
        name = "Artes de Ejemplo",
        version = "1.0.0",
        optionSets = new object[]
        {
            new
            {
                setId = Techniques,
                name = "Técnicas de ejemplo",
                options = new object[]
                {
                    new { index = CalmStrike, name = "Golpe sereno", description = new[] { "Texto de ejemplo." }, cost = new { resource = "ki", amount = 2 } },
                    new { index = "artes-ejemplo-paso-veloz", name = "Paso veloz", description = new[] { "Texto de ejemplo." }, cost = new { resource = "ki", amount = 1 } },
                    new
                    {
                        index = "artes-ejemplo-foco-interior",
                        name = "Foco interior",
                        description = new[] { "Texto de ejemplo." },
                        resource = new { key = "artes-ejemplo-concentracion", name = "Concentración", max = 3, recharge = "ShortRest" },
                    },
                    new { index = "artes-ejemplo-golpe-centrado", name = "Golpe centrado", description = new[] { "Texto de ejemplo." }, cost = new { resource = "artes-ejemplo-concentracion", amount = 1 } },
                },
            },
        },
        classes = new object[]
        {
            new
            {
                extends = "monk",
                levelChoices = new[]
                {
                    new { level = 2, key = "tecnica-ejemplo", name = "Técnica", kind = "OptionSet", setId = Techniques, choose = 1 },
                },
            },
            new
            {
                extends = "cleric",
                subclasses = new[]
                {
                    new
                    {
                        index = Domain,
                        name = "Dominio del Saber de Ejemplo",
                        description = new[] { "Texto de ejemplo." },
                        levels = new[] { new { level = 1, grants = new { skills = new[] { "arcana", "nature" } } } },
                        levelChoices = new[]
                        {
                            new { level = 1, key = "pericia-dominio", name = "Pericia del dominio", kind = "Expertise", choose = 2, from = new[] { "arcana", "nature" } },
                        },
                    },
                },
            },
            new
            {
                extends = "fighter",
                subclasses = new[]
                {
                    new
                    {
                        index = Scholar,
                        name = "Erudito de Ejemplo",
                        description = new[] { "Texto de ejemplo." },
                        levelChoices = new object[]
                        {
                            new { level = 3, key = "idioma-erudito", name = "Idioma", kind = "Language", choose = 1, after = "habilidad-erudito" },
                            new { level = 3, key = "pericia-erudito", name = "Pericia", kind = "Expertise", choose = 1 },
                            new { level = 3, key = "habilidad-erudito", name = "Habilidad", kind = "Skill", choose = 1, from = new[] { "arcana", "history" } },
                        },
                    },
                },
            },
        },
    })!.AsObject();

    private async Task ImportAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        if ((await admin.GetStringAsync(PacksUrl)).Contains("artes-ejemplo", StringComparison.Ordinal))
        {
            return;
        }

        var import = await admin.PostAsync(PacksUrl, Json(Pack()));
        Assert.True(import.StatusCode == HttpStatusCode.Created, await import.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_monk_sees_the_cost_of_the_technique_and_spends_it()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        var monk = await LevelAsync(s, "monk", null, 1, "Monja serena");

        var choice = Assert.Single((await PlanAsync(s.Player, monk.Id)).Choices, c => c.Key == "tecnica-ejemplo");
        var strike = choice.Options.Single(o => o.Index == CalmStrike);
        Assert.Equal(("ki", "Ki", 2, "2 Ki"), (strike.Cost!.Resource, strike.Cost.ResourceName, strike.Cost.Amount, strike.Cost.Label));
        Assert.Equal(("artes-ejemplo-concentracion", "Concentración", 1),
            (choice.Options.Single(o => o.Index == "artes-ejemplo-golpe-centrado").Cost!.Resource,
             choice.Options.Single(o => o.Index == "artes-ejemplo-golpe-centrado").Cost!.ResourceName,
             choice.Options.Single(o => o.Index == "artes-ejemplo-golpe-centrado").Cost!.Amount));
        Assert.Null(choice.Options.Single(o => o.Index == "artes-ejemplo-foco-interior").Cost);

        var after = await ApplyAsync(s.Player, monk.Id, 6, new { key = "tecnica-ejemplo", selected = new[] { CalmStrike } });
        var cost = Assert.Single(after.OptionCosts);
        Assert.Equal((CalmStrike, "Golpe sereno", "ki", "Ki", 2), (cost.Index, cost.Name, cost.Resource, cost.ResourceName, cost.Amount));
        var ki = Assert.Single(after.Resources, r => r.Key == "ki");
        Assert.Equal(2, ki.Max);
        Assert.Equal([CalmStrike], ki.Options.Select(o => o.Index));
        Assert.Equal([CalmStrike], Assert.Single(after.Combat.Resources, r => r.Key == "ki").Options.Select(o => o.Index));

        // "Usar": the player spends the amount through the usual endpoint, without approval.
        var spend = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(monk.Id)}/resources/{ki.Id}/spend", new { amount = cost.Amount });
        Assert.True(spend.IsSuccessStatusCode, await spend.Content.ReadAsStringAsync());
        Assert.Equal(2, (await s.Player.GetCharacterAsync(monk.Id)).Resources.Single(r => r.Key == "ki").Used);

        var again = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(monk.Id)}/resources/{ki.Id}/spend", new { amount = cost.Amount });
        Assert.False(again.IsSuccessStatusCode);
    }

    [Fact]
    public async Task A_cleric_choosing_the_domain_gets_expertise_in_the_skills_it_grants_at_level_1()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await LevelAsync(s, "cleric", null, 1, "Clériga sabia");

        var plan = await PlanAsync(s.Player, cleric.Id);
        var keys = plan.Choices.Select(c => c.Key).ToList();
        var subclassKey = plan.Choices.Single(c => c.Kind == "Subclass").Key;
        Assert.True(keys.IndexOf(subclassKey) < keys.IndexOf("pericia-dominio"));
        var expertise = plan.Choices.Single(c => c.Key == "pericia-dominio");
        Assert.Equal(["arcana", "nature"], expertise.Options.Select(o => o.Index).Order());
        Assert.All(expertise.Options, o => Assert.Equal((subclassKey, Domain), (o.Requires!.ChoiceKey, o.Requires.Index)));

        // Expertise in a skill the domain does not grant is refused.
        var wrong = await PostLevelUpAsync(s.Player, cleric.Id, 5,
            new { key = subclassKey, selected = new[] { Domain } },
            new { key = "pericia-dominio", selected = new[] { "arcana", "religion" } });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var after = await ApplyAsync(s.Player, cleric.Id, 5,
            new { key = subclassKey, selected = new[] { Domain } },
            new { key = "pericia-dominio", selected = new[] { "arcana", "nature" } });
        Assert.All(new[] { "arcana", "nature" }, skill => Assert.Contains(after.Proficiencies, p => p.Type == "Skill" && SkillKey(p.Key) == skill && p.Expertise));
    }

    [Fact]
    public async Task A_cleric_created_with_the_domain_is_asked_for_the_expertise_at_the_next_level()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await LevelAsync(s, "cleric", Domain, 1, "Clérigo erudito");

        var expertise = Assert.Single((await PlanAsync(s.Player, cleric.Id)).Choices, c => c.Key == "pericia-dominio");
        Assert.Equal(["arcana", "nature"], expertise.Options.Select(o => o.Index).Order());
        Assert.All(expertise.Options, o => Assert.Null(o.Requires));

        var after = await ApplyAsync(s.Player, cleric.Id, 5, new { key = "pericia-dominio", selected = new[] { "arcana", "nature" } });
        Assert.All(new[] { "arcana", "nature" }, skill => Assert.Contains(after.Proficiencies, p => p.Type == "Skill" && SkillKey(p.Key) == skill && p.Expertise));
    }

    [Fact]
    public async Task A_skill_picked_at_the_same_level_can_take_the_expertise_and_after_orders_the_choices()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        var fighter = await LevelAsync(s, "fighter", null, 2, "Guerrera letrada");

        var plan = await PlanAsync(s.Player, fighter.Id);
        var keys = plan.Choices.Where(c => c.SubclassIndex == Scholar).Select(c => c.Key).ToList();
        Assert.Equal(["habilidad-erudito", "idioma-erudito", "pericia-erudito"], keys);
        var expertise = plan.Choices.Single(c => c.Key == "pericia-erudito");
        var arcana = expertise.Options.Single(o => o.Index == "arcana");
        Assert.Equal(("habilidad-erudito", "arcana"), (arcana.Requires!.ChoiceKey, arcana.Requires.Index));

        // Expertise in the skill that was not picked is refused.
        var subclassKey = plan.Choices.Single(c => c.Kind == "Subclass").Key;
        var wrong = await PostLevelUpAsync(s.Player, fighter.Id, 7,
            new { key = subclassKey, selected = new[] { Scholar } },
            new { key = "habilidad-erudito", selected = new[] { "arcana" } },
            new { key = "idioma-erudito", selected = new[] { "Lengua de ejemplo" } },
            new { key = "pericia-erudito", selected = new[] { "history" } });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var after = await ApplyAsync(s.Player, fighter.Id, 7,
            new { key = subclassKey, selected = new[] { Scholar } },
            new { key = "habilidad-erudito", selected = new[] { "arcana" } },
            new { key = "idioma-erudito", selected = new[] { "Lengua de ejemplo" } },
            new { key = "pericia-erudito", selected = new[] { "arcana" } });
        Assert.Contains(after.Proficiencies, p => p.Type == "Skill" && SkillKey(p.Key) == "arcana" && p.Expertise);
        Assert.DoesNotContain(after.Proficiencies, p => p.Type == "Skill" && SkillKey(p.Key) == "history");
    }

    [Theory]
    [InlineData("""{"resource":"ki","amount":0}""", "optionSets[0].options[0].cost.amount", null)]
    [InlineData("""{"resource":"ki","amount":21}""", "optionSets[0].options[0].cost.amount", null)]
    [InlineData("""{"amount":2}""", "optionSets[0].options[0].cost.resource", "obligatorio")]
    [InlineData("""{"resource":"Ki Points","amount":2}""", "optionSets[0].options[0].cost.resource", "no válido")]
    [InlineData("""{"resource":"recurso-inexistente","amount":2}""", "optionSets[0].options[0].cost.resource", "no existe")]
    [InlineData("""{"resource":"rage","amount":1}""", "optionSets[0].options[0].cost.resource", "barbarian")]
    public async Task Invalid_costs_are_reported_with_their_path(string cost, string path, string? message)
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed("artes-malas");
        pack["optionSets"]![0]!["options"]![0]!["cost"] = JsonNode.Parse(cost);

        var errors = await ImportErrorsAsync(admin, pack);

        Assert.Contains(errors, e => e.StartsWith($"{path}:", StringComparison.Ordinal) && (message is null || e.Contains(message, StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("habilidad-inexistente", "No hay ninguna elección")]
    [InlineData("idioma-erudito", "sí misma")]
    [InlineData("Habilidad Erudito", "Debe ser la key")]
    public async Task Invalid_after_is_reported(string after, string message)
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed("artes-orden");
        pack["classes"]![2]!["subclasses"]![0]!["levelChoices"]![0]!["after"] = after;

        var errors = await ImportErrorsAsync(admin, pack);

        Assert.Contains(errors, e => e.StartsWith("classes[2].subclasses[0].levelChoices[0].after:", StringComparison.Ordinal) && e.Contains(message, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_cycle_of_after_is_reported()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed("artes-ciclo");
        pack["classes"]![2]!["subclasses"]![0]!["levelChoices"]![2]!["after"] = "idioma-erudito";

        var errors = await ImportErrorsAsync(admin, pack);

        Assert.Contains(errors, e => e.Contains(".after:", StringComparison.Ordinal) && e.Contains("ciclo", StringComparison.Ordinal));
    }

    /// <summary>The pack with another id and indexes, so that it does not collide with the imported one.</summary>
    private static JsonObject Renamed(string id)
    {
        var text = Pack().ToJsonString().Replace("artes-ejemplo", id, StringComparison.Ordinal);
        var pack = JsonNode.Parse(text)!.AsObject();
        pack["classes"]![0]!["levelChoices"]![0]!["key"] = $"tecnica-{id}";
        return pack;
    }

    private static string SkillKey(string key) => key.StartsWith("skill-", StringComparison.Ordinal) ? key["skill-".Length..] : key;

    private static StringContent Json(JsonNode pack) => new(pack.ToJsonString(), Encoding.UTF8, "application/json");

    private static async Task<List<string>> ImportErrorsAsync(HttpClient admin, JsonNode pack)
    {
        var response = await admin.PostAsync(PacksUrl, Json(pack));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    /// <summary>An active character of the class at <paramref name="level"/> (Wis 16) with a level granted by the DM.</summary>
    private static async Task<CharacterDetailDto> LevelAsync(CampaignScenario s, string classIndex, string? subclass, int level, string name)
    {
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, name);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/sheet", new
        {
            classes = new[] { new { classIndex, subclassIndex = subclass, level } },
            baseAbilities = new { str = 14, dex = 14, con = 14, @int = 12, wis = 16, cha = 10 },
            applyRacialBonuses = false,
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/activate", null)).StatusCode);
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/systems/dnd5e/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { hero.Id } });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        return await s.Player.GetCharacterAsync(hero.Id);
    }

    private static async Task<LevelUpPlanDto> PlanAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LevelUpPlanDto>())!;
    }

    private static Task<HttpResponseMessage> PostLevelUpAsync(SignedInUser actor, Guid id, int hitPoints, params object[] choices) =>
        actor.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up", new { hitPointsRolled = hitPoints, choices });

    private static async Task<CharacterDetailDto> ApplyAsync(SignedInUser actor, Guid id, int hitPoints, params object[] choices)
    {
        var response = await PostLevelUpAsync(actor, id, hitPoints, choices);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<CharacterDetailDto>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
