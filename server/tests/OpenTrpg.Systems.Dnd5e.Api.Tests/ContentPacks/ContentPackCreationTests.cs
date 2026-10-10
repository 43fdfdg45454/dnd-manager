using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using static OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks.CreationPack;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the fictitious creation pack of phase 35B1.</summary>
public sealed class ContentPackCreationApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Creation and level-up gaps of the format 3 (phase 35B1) with the fictitious pack of <see cref="CreationPack"/>:
/// exotic categories and new proficiencies, multiclass prerequisites, option prerequisites, granted and nested
/// choices, new casting progressions and auxiliary levels.
/// </summary>
public class ContentPackCreationTests(ContentPackCreationApiFactory factory) : IClassFixture<ContentPackCreationApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string CatalogUrl = "/api/v1/systems/dnd5e/catalog";

    private static readonly SemaphoreSlim ImportLock = new(1, 1);

    // ---- H07: exotic categories, firearms, bombs ----------------------------------------------------------

    [Fact]
    public async Task Exotic_weapons_firearms_and_bombs_need_their_own_proficiencies()
    {
        var admin = await factory.CreateAdminClientAsync();
        await ImportAsync(admin);

        var scythe = await ItemAsync(admin, "guadaña doble");
        Assert.Equal("Exotic Melee", scythe.Subcategory);
        var armor = await ItemAsync(admin, "coraza de runas");
        Assert.Equal("Medium Armor", armor.Subcategory);
        Assert.Contains("exotic", armor.Properties);

        var s = await ScenarioAsync();
        // Martial weapons do not cover exotic weapons, firearms or bombs.
        var martial = await ArmedAsync(s, "martial-weapons");
        Assert.All(martial.Combat.Attacks.Where(a => a.ItemId is not null), a => Assert.DoesNotContain(a.AttackBreakdown.Parts, p => p.Source == "proficiency"));

        var expert = await ArmedAsync(s, "exotic-weapons", "exotic-firearms", "bombs");
        Assert.All(expert.Combat.Attacks.Where(a => a.ItemId is not null), a => Assert.Contains(a.AttackBreakdown.Parts, p => p.Source == "proficiency"));

        // A martial firearm proficiency does not cover an exotic firearm.
        var wrongGroup = await ArmedAsync(s, "martial-firearms");
        Assert.DoesNotContain(wrongGroup.Combat.Attacks.Single(a => a.Name == "Trabuco de bruma").AttackBreakdown.Parts, p => p.Source == "proficiency");
    }

    [Fact]
    public async Task Exotic_shields_and_unknown_weapon_categories_are_rejected()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed(Build(), "vacio-malo-h07");
        pack["items"]![0]!["weapon"]!["category"] = "legendaria";
        pack["items"]![3]!["armor"]!["category"] = "shield";

        var errors = await ImportErrorsAsync(admin, pack);

        Assert.Contains(errors, e => e.StartsWith("items[0].weapon.category:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("items[3].armor.exotic:", StringComparison.Ordinal));
    }

    // ---- H05/H06: multiclass prerequisites with alternatives, growing multiclass proficiencies ------------

    [Fact]
    public async Task Multiclass_prerequisites_accept_alternatives_and_proficiencies_grow_with_the_class()
    {
        var s = await ScenarioAsync();
        var herald = I("heraldo");
        var detail = await GetAsync<ClassDetailDto>(s.Dm.Client, $"{CatalogUrl}/classes/{herald}");
        Assert.Equal("Fuerza 13 o Destreza 13 y Carisma 13", detail.Multiclassing!.PrerequisitesText);
        Assert.Equal(2, detail.Multiclassing.AnyOf.Count);

        // Cha 13 alone is not enough; Dex 14 + Cha 13 is.
        var weak = await DraftAsync(s, "fighter", 2, new { str = 15, dex = 10, con = 12, @int = 10, wis = 10, cha = 12 });
        await GrantAsync(s, weak.Id);
        var weakPlan = await PlanAsync(s.Player, weak.Id);
        var refused = weakPlan.Classes.Single(c => c.ClassIndex == herald);
        Assert.False(refused.Allowed);
        Assert.Contains("Carisma 13", refused.Reason, StringComparison.Ordinal);

        var hero = await DraftAsync(s, "fighter", 1, new { str = 10, dex = 14, con = 12, @int = 10, wis = 10, cha = 13 });
        for (var level = 1; level <= 3; level++)
        {
            await GrantAsync(s, hero.Id);
            var plan = await PlanAsync(s.Player, hero.Id, herald);
            var option = plan.Classes.Single(c => c.ClassIndex == herald);
            Assert.True(option.Allowed, option.Reason);
            hero = await ApplyAsync(s.Player, hero.Id, new { classIndex = herald, hitPointsRolled = 5, choices = Answers(plan, hero) });
            var armor = hero.Proficiencies.Where(p => p.Type == "Armor").Select(p => p.Key).ToList();
            Assert.Contains("medium-armor", armor);
            Assert.Equal(level >= 2, armor.Contains("heavy-armor"));
            Assert.Equal(level >= 3, armor.Contains("exotic-armor"));
        }
    }

    [Fact]
    public async Task Multiclass_alternatives_and_level_proficiencies_are_validated()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed(Build(), "vacio-malo-h05");
        var multiclassing = pack["classes"]![0]!["multiclassing"]!;
        multiclassing["prerequisites"]!["anyOf"] = new JsonArray(new JsonObject { ["suerte"] = 13 });
        multiclassing["proficienciesByLevel"]!["1"] = new JsonObject { ["armor"] = new JsonArray("heavy") };

        var errors = await ImportErrorsAsync(admin, pack);

        Assert.Contains(errors, e => e.StartsWith("classes[0].multiclassing.prerequisites.anyOf:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("classes[0].multiclassing.proficienciesByLevel.1:", StringComparison.Ordinal));
    }

    // ---- H08/H09: option and subclass prerequisites ------------------------------------------------------

    [Fact]
    public async Task Option_prerequisites_check_options_subclasses_class_features_alternatives_level_and_size()
    {
        var s = await ScenarioAsync();
        var keeper = I("guardian");
        var hero = await DraftAsync(s, keeper, 1, new { str = 14, dex = 12, con = 14, @int = 10, wis = 14, cha = 8 });
        await GrantAsync(s, hero.Id);

        var plan = await PlanAsync(s.Player, hero.Id, keeper);
        var signals = plan.Choices.Single(c => c.Key == "senales").Options.ToDictionary(o => o.Index);

        Assert.True(signals[I("senal-basica")].Eligible);
        Assert.Equal("Requiere Señal básica; no lo tienes.", signals[I("senal-doble")].Reason);
        Assert.Equal("Señal básica", signals[I("senal-doble")].PrerequisitesText);
        Assert.Equal("Requiere la subclase Orden del Yunque.", signals[I("faro-del-yunque")].Reason);
        Assert.Equal("Requiere el rasgo Lanzamiento de conjuros.", signals[I("luz-arcana")].Reason);
        Assert.True(signals[I("destello")].Eligible); // Wisdom 14 meets the second alternative.
        Assert.Equal("Requiere nivel de personaje 5; tienes 2.", signals[I("veterano")].Reason);
        Assert.Equal("Requiere 15 en tu característica de lanzamiento; no lanzas conjuros.", signals[I("foco-firme")].Reason);
        Assert.True(signals[I("farolillo")].Eligible); // No race: the size is not checked.
        Assert.Equal("Requiere un nivel de Heraldo de bruma.", signals[I("del-heraldo")].Reason);

        // An option picked in the same request meets the prerequisite of another one; an ineligible pick is refused.
        var refused = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/level-up", new
        {
            classIndex = keeper,
            hitPointsRolled = 6,
            choices = new[] { new { key = "senales", selected = new[] { I("senal-doble") } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        hero = await ApplyAsync(s.Player, hero.Id, new { classIndex = keeper, hitPointsRolled = 6, choices = new[] { new { key = "senales", selected = new[] { I("senal-basica") } } } });

        // Level 3: the order of the Anvil needs Strength 15.
        await GrantAsync(s, hero.Id);
        var third = await PlanAsync(s.Player, hero.Id, keeper);
        var orders = third.Choices.Single(c => c.Kind == "Subclass").Options.ToDictionary(o => o.Index);
        Assert.True(orders[I("orden-llama")].Eligible);
        Assert.False(orders[I("orden-yunque")].Eligible);
        Assert.Equal("Requiere Fuerza 15; tienes 14.", orders[I("orden-yunque")].Reason);
        Assert.Equal("Fuerza 15", orders[I("orden-yunque")].PrerequisitesText);
        var detail = await GetAsync<ClassDetailDto>(s.Dm.Client, $"{CatalogUrl}/classes/{keeper}");
        Assert.Equal("Fuerza 15", detail.Subclasses.Single(x => x.Index == I("orden-yunque")).PrerequisitesText);
    }

    [Fact]
    public async Task Option_prerequisites_reject_unknown_references_and_nested_alternatives()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed(Build(), "vacio-malo-h08");
        var options = pack["optionSets"]![0]!["options"]!.AsArray();
        options[1]!["prerequisites"] = new JsonObject { ["options"] = new JsonArray("vacio-malo-h08-no-existe") };
        options[2]!["prerequisites"] = new JsonObject { ["subclasses"] = new JsonArray("vacio-malo-h08-tampoco") };
        options[3]!["prerequisites"] = new JsonObject { ["classFeature"] = "magiaRara" };
        options[4]!["prerequisites"] = new JsonObject
        {
            ["anyOf"] = new JsonArray(new JsonObject { ["anyOf"] = new JsonArray() }, new JsonObject { ["characterLevel"] = 3 }),
        };
        options[7]!["prerequisites"] = new JsonObject { ["sizes"] = new JsonArray("colosal") };
        options[8]!["prerequisites"] = new JsonObject { ["classes"] = new JsonArray("vacio-malo-h08-sin-clase") };
        pack["classes"]![1]!["subclasses"]![1]!["prerequisites"] = new JsonObject { ["characterLevel"] = 30 };

        var errors = await ImportErrorsAsync(admin, pack);
        var all = string.Join("\n", errors);

        Assert.True(errors.Any(e => e.StartsWith("optionSets[0].options[1].prerequisites.options[0]:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("optionSets[0].options[2].prerequisites.subclasses[0]:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("optionSets[0].options[3].prerequisites.classFeature:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("optionSets[0].options[4].prerequisites.anyOf[0].anyOf:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("optionSets[0].options[7].prerequisites.sizes[0]:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("optionSets[0].options[8].prerequisites.classes[0]:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("classes[1].subclasses[1].prerequisites.characterLevel:", StringComparison.Ordinal)), all);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    /// <summary>Imports the pack once per database.</summary>
    private async Task ImportAsync(HttpClient admin)
    {
        await ImportLock.WaitAsync();
        try
        {
            if ((await GetAsync<List<ContentPackDto>>(admin, PacksUrl)).Any(p => p.Id == Id))
            {
                return;
            }

            var response = await admin.PostAsync(PacksUrl, Json(Build()));
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            ImportLock.Release();
        }
    }

    private async Task<CampaignScenario> ScenarioAsync()
    {
        await ImportAsync(await factory.CreateAdminClientAsync());
        var s = await factory.CreateCampaignScenarioAsync();
        await s.EnablePacksAsync(Id);
        return s;
    }

    private static async Task<ItemDetailDto> ItemAsync(HttpClient client, string name)
    {
        var summary = Assert.Single((await GetAsync<PagedResult<ItemSummaryDto>>(client, $"{CatalogUrl}/items?search={Uri.EscapeDataString(name)}")).Items);
        return await GetAsync<ItemDetailDto>(client, $"{CatalogUrl}/items/{summary.Id}");
    }

    /// <summary>A fighter with the given weapon proficiencies carrying the pack's scythe, bomb and firearm equipped.</summary>
    private static async Task<CharacterDetailDto> ArmedAsync(CampaignScenario s, params string[] weapons)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, "Armado");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", level = 1 } },
            baseAbilities = new { str = 14, dex = 14, con = 12, @int = 10, wis = 10, cha = 10 },
            applyRacialBonuses = false,
            proficiencies = weapons.Select(key => new { type = "Weapon", key, expertise = false }).ToArray(),
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        foreach (var name in new[] { "Guadaña doble", "Petardo del vacío", "Trabuco de bruma" })
        {
            var item = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync(name), quantity = 1 });
            Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, item.Id, new { equipped = true })).StatusCode);
        }

        return await s.Player.GetCharacterAsync(character.Id);
    }

    /// <summary>An active character of <paramref name="classIndex"/> at <paramref name="level"/> with the given base scores.</summary>
    private static async Task<CharacterDetailDto> DraftAsync(CampaignScenario s, string classIndex, int level, object scores, string? subclass = null)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, classIndex);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex, subclassIndex = subclass, level } },
            baseAbilities = scores,
            applyRacialBonuses = false,
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null)).StatusCode);
        return await s.Player.GetCharacterAsync(character.Id);
    }

    /// <summary>Answers every required choice with its first eligible options (an ASI as +1 to the two lowest abilities).</summary>
    private static object[] Answers(LevelUpPlanDto plan, CharacterDetailDto hero, Func<LevelUpChoiceDto, object?>? custom = null)
    {
        var chosenSubclass = plan.Choices.FirstOrDefault(c => c.Kind == "Subclass")?.Options.FirstOrDefault(o => o.Eligible)?.Index
            ?? hero.Classes.FirstOrDefault(c => c.ClassIndex == plan.ClassIndex)?.SubclassIndex;
        var answers = new List<object>();
        foreach (var choice in plan.Choices.Where(c => c.Required > 0 && (c.SubclassIndex is null || c.SubclassIndex == chosenSubclass)))
        {
            if (custom?.Invoke(choice) is { } answer)
            {
                answers.Add(answer);
            }
            else if (choice.Kind == "AsiOrFeat")
            {
                var lowest = hero.Sheet.Abilities.Where(a => a.Value.Score < 20).OrderBy(a => a.Value.Score).ThenBy(a => a.Key).Take(2).Select(a => a.Key).ToList();
                answers.Add(new { key = choice.Key, selected = new { asi = lowest.ToDictionary(a => a, _ => 1) } });
            }
            else
            {
                var picks = choice.Options.Where(o => o.Eligible).Take(choice.Required).Select(o => o.Index).ToArray();
                answers.Add(new { key = choice.Key, selected = picks });
            }
        }

        return answers.ToArray();
    }

    private static async Task GrantAsync(CampaignScenario s, Guid characterId)
    {
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/systems/dnd5e/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { characterId } });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
    }

    private static async Task<LevelUpPlanDto> PlanAsync(SignedInUser actor, Guid id, string? classIndex = null)
    {
        var query = classIndex is null ? string.Empty : $"?classIndex={classIndex}";
        var response = await actor.Client.GetAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up{query}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LevelUpPlanDto>())!;
    }

    private static async Task<CharacterDetailDto> ApplyAsync(SignedInUser actor, Guid id, object body)
    {
        var response = await actor.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up", body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static StringContent Json(JsonNode pack) => new(pack.ToJsonString(), Encoding.UTF8, "application/json");

    private static async Task<List<string>> ImportErrorsAsync(HttpClient admin, JsonNode pack)
    {
        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
