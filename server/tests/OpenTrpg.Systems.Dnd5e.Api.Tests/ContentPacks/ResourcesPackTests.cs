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
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using static OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks.ResourcesPack;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the fictitious resources pack of phase 35B3.</summary>
public sealed class ResourcesPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Gaps of phase 35B3 with the fictitious packs of <see cref="ResourcesPack"/>: combined resource maximums and their
/// bonuses (H34), recharge on initiative and recharge dice (H35), trait resources (H36), granted spells with their own
/// ability (H37), spell tags (H38), repeatable feats (H39), spellcasting ability overrides (H41) and the leftovers of
/// 35B2 (saving throw lost by a level-up feat, lost skills, conditions of required packs).
/// </summary>
public class ResourcesPackTests(ResourcesPackApiFactory factory) : IClassFixture<ResourcesPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string CatalogUrl = "/api/v1/systems/dnd5e/catalog";

    private static readonly SemaphoreSlim ImportLock = new(1, 1);

    private static readonly object FighterScores = new { str = 15, dex = 12, con = 14, @int = 10, wis = 10, cha = 8 };

    // ---- H34, H35 ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_resource_adds_its_table_formula_and_bonuses_and_explains_each_part()
    {
        var s = await ScenarioAsync();
        var hero = await DraftAsync(s, "fighter", 3, FighterScores, I("senda"));

        var brio = Resource(hero, I("brio"));
        // Table 2 at level 3 + Con 2 + 1 of the bonus feature.
        Assert.Equal(5, brio.Max);
        Assert.Equal(5, brio.Breakdown!.Total);
        Assert.Contains(brio.Breakdown.Parts, p => p.Label.StartsWith("Reserva", StringComparison.Ordinal) && p.Value == 1);
        Assert.Contains(brio.Breakdown.Parts, p => p.Value == 2 && p.Label.Contains("Constitución", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Initiative_and_recharge_dice_bring_the_uses_back()
    {
        var s = await ScenarioAsync();
        var hero = await DraftAsync(s, "fighter", 3, FighterScores, I("senda"));
        var url = ItemTestHelpers.Dnd5eCharacterUrl(hero.Id);
        var surge = Resource(hero, I("impulso"));
        var breath = Resource(hero, I("resuello"));
        Assert.Equal("Initiative", surge.Recharge);
        Assert.Equal(("d6", 5), (breath.RechargeRoll!.Dice, breath.RechargeRoll.Min));
        Assert.Null(surge.RechargeRoll);

        await SpendAsync(s, url, surge.Id);
        await SpendAsync(s, url, breath.Id);

        var initiative = await s.Player.Client.PostAsync($"{url}/resources/initiative", null);
        Assert.True(initiative.StatusCode == HttpStatusCode.OK, await initiative.Content.ReadAsStringAsync());
        var after = (await initiative.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal(0, Resource(after, I("impulso")).Used);
        Assert.Equal(1, Resource(after, I("resuello")).Used);

        var low = await RechargeAsync(s, url, breath.Id, 3);
        Assert.False(low.Recharged);
        Assert.Equal(1, Resource(low.Character, I("resuello")).Used);
        var high = await RechargeAsync(s, url, breath.Id, 5);
        Assert.True(high.Recharged);
        Assert.Equal(0, Resource(high.Character, I("resuello")).Used);

        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync($"{url}/resources/{breath.Id}/recharge-roll", new { value = 7 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync($"{url}/resources/{surge.Id}/recharge-roll", new { value = 6 })).StatusCode);

        // A short rest also brings back the initiative resource.
        await SpendAsync(s, url, surge.Id);
        var rested = await s.Dm.Client.PostAsJsonAsync($"/api/v1/systems/dnd5e/campaigns/{s.CampaignId}/party/rest", new { kind = "short", characterIds = new[] { hero.Id } });
        Assert.True(rested.StatusCode == HttpStatusCode.OK, await rested.Content.ReadAsStringAsync());
        Assert.Equal(0, Resource(await s.Player.GetCharacterAsync(hero.Id), I("impulso")).Used);
    }

    // ---- H36 and leftovers: race ---------------------------------------------------------------------------------

    [Fact]
    public async Task A_race_trait_gives_a_resource_takes_a_skill_away_and_grants_an_immunity_of_the_required_pack()
    {
        var s = await ScenarioAsync();
        var draft = await s.Player.CreateCharacterAsync(s.CampaignId, "Pétreo");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(draft.Id)}/sheet", new
        {
            raceIndex = I("petreo"),
            classes = new[] { new { classIndex = "fighter", level = 5 } },
            baseAbilities = FighterScores,
            applyRacialBonuses = false,
            proficiencies = new object[]
            {
                new { type = "Skill", key = "athletics", expertise = false },
                new { type = "Skill", key = "perception", expertise = false },
            },
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        var hero = await s.Player.GetCharacterAsync(draft.Id);

        var tenacity = Resource(hero, I("tenacidad"));
        Assert.Equal(3, tenacity.Max);
        Assert.Equal("Tenacidad (raza)", tenacity.Source);
        Assert.False(hero.Sheet.Skills.Single(k => k.Index == "athletics").Proficient);
        Assert.True(hero.Sheet.Skills.Single(k => k.Index == "perception").Proficient);
        Assert.Equal(("athletics", "Pétreo"), (Assert.Single(hero.Sheet.LostSkills).Type, hero.Sheet.LostSkills[0].Label));
        Assert.Equal(Numb, Assert.Single(hero.Sheet.ConditionImmunities).Type);
    }

    // ---- H37, H39, leftover: level-up feats -------------------------------------------------------------------------

    [Fact]
    public async Task A_feat_grants_spells_cast_with_its_own_ability_and_uses_per_long_rest()
    {
        var s = await ScenarioAsync();
        var hero = await DraftAsync(s, "fighter", 3, new { str = 15, dex = 12, con = 14, @int = 16, wis = 10, cha = 8 }, I("senda"));

        var leveled = await LevelUpAsync(s, hero.Id, new { feat = I("chispa") });

        var casting = leveled.Sheet.Spellcasting.Single(c => c.ClassIndex == I("chispa"));
        Assert.Equal(("Chispa arcana", "int", 8 + 2 + 3), (casting.Name, casting.Ability, casting.SaveDc));
        Assert.Contains(leveled.Spells, sp => sp.SpellIndex == "magic-missile" && sp.ClassIndex == I("chispa") && sp.AlwaysPrepared);
        Assert.Contains(leveled.Spells, sp => sp.SpellIndex == "light" && sp.ClassIndex == I("chispa"));
        var uses = Resource(leveled, $"{I("chispa")}.magic-missile");
        Assert.Equal((1, "LongRest"), (uses.Max, uses.Recharge));
    }

    [Fact]
    public async Task A_repeatable_feat_is_offered_again_and_counts_twice()
    {
        var s = await ScenarioAsync();
        var hero = await DraftAsync(s, "fighter", 3, FighterScores, I("senda"));
        var initiative = hero.Sheet.Initiative;

        await LevelUpAsync(s, hero.Id, new { feat = I("versatil") });
        await LevelUpAsync(s, hero.Id, null);
        var plan = await GrantAndPlanAsync(s, hero.Id);
        var asi = plan.Choices.Single(c => c.Kind == "AsiOrFeat");
        Assert.Contains(asi.Options, o => o.Index == I("versatil") && o.Eligible);
        Assert.DoesNotContain(asi.Options, o => o.Index == I("sacrificio") && !o.Eligible);

        var response = await PostLevelUpAsync(s, hero.Id, plan, new { feat = I("versatil") });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var twice = (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal(initiative + 2, twice.Sheet.Initiative);
        Assert.Equal(2, twice.Sheet.Breakdowns["initiative"].Parts.Count(p => p.Label.StartsWith("Versátil", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_level_up_feat_asks_which_saving_throw_is_lost()
    {
        var s = await ScenarioAsync();
        var hero = await DraftAsync(s, "fighter", 3, FighterScores, I("senda"));
        var plan = await GrantAndPlanAsync(s, hero.Id);
        var feat = plan.Choices.Single(c => c.Kind == "AsiOrFeat").Options.Single(o => o.Index == I("sacrificio"));
        Assert.Equal(1, feat.RemoveSavingThrows!.Choose);
        Assert.Equal(["str", "con"], feat.RemoveSavingThrows.From);

        var missing = await PostLevelUpAsync(s, hero.Id, plan, new { feat = I("sacrificio") });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var wrong = await PostLevelUpAsync(s, hero.Id, plan, new { feat = I("sacrificio"), removedSavingThrows = new[] { "wis" } });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var applied = await PostLevelUpAsync(s, hero.Id, plan, new { feat = I("sacrificio"), removedSavingThrows = new[] { "str" } });
        Assert.True(applied.StatusCode == HttpStatusCode.OK, await applied.Content.ReadAsStringAsync());

        var leveled = (await applied.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.False(leveled.Sheet.SavingThrows["str"].Proficient);
        Assert.True(leveled.Sheet.SavingThrows["con"].Proficient);
        Assert.Equal("Sacrificio (nivel 4)", Assert.Single(leveled.Sheet.LostSavingThrows).Label);
    }

    // ---- H38: tags ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Spell_tags_filter_the_compendium_and_the_spell_choices_and_expand_lists()
    {
        var s = await ScenarioAsync();
        var page = await GetAsync<PagedResult<SpellSummaryDto>>(s.Player.Client, $"{CatalogUrl}/spells?tag={I("luz")}&pageSize=100&campaignId={s.CampaignId}");
        Assert.Equal(["light", "magic-missile", I("destello")], page.Items.Select(i => i.Index).Order(StringComparer.Ordinal).ToArray());
        Assert.All(page.Items, i => Assert.Contains(I("luz"), i.Tags));
        Assert.Contains(page.Items.Single(i => i.Index == I("destello")).ExpandedBy, e => e.SubclassIndex == I("lumen"));

        var detail = await GetAsync<SpellDetailDto>(s.Player.Client, $"{CatalogUrl}/spells/light");
        Assert.Equal([I("luz")], detail.Tags);
        var tags = await GetAsync<List<ReferenceEntryDto>>(s.Player.Client, $"{CatalogUrl}/reference/spellTags");
        Assert.Contains(tags, t => t.Index == I("luz") && t.Name == "Luz");

        var hero = await DraftAsync(s, "fighter", 3, FighterScores, I("senda"));
        var plan = await GrantAndPlanAsync(s, hero.Id);
        var choice = plan.Choices.Single(c => c.Key == "chispazo");
        Assert.Equal(["magic-missile", I("destello")], choice.Options.Select(o => o.Index).Order(StringComparer.Ordinal).ToArray());

        var wizard = await DraftAsync(s, "wizard", 3, new { str = 8, dex = 14, con = 12, @int = 16, wis = 12, cha = 10 }, I("lumen"));
        var wizardPlan = await GrantAndPlanAsync(s, wizard.Id, "wizard");
        Assert.Contains(wizardPlan.Choices.Where(c => c.Kind == "SpellbookSpells").SelectMany(c => c.Options), o => o.Index == I("destello"));
    }

    // ---- H41 -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_spellcasting_ability_can_be_overridden_and_is_marked()
    {
        var s = await ScenarioAsync();
        var wizard = await DraftAsync(s, "wizard", 3, new { str = 8, dex = 14, con = 12, @int = 16, wis = 12, cha = 10 });
        var url = $"{ItemTestHelpers.Dnd5eCharacterUrl(wizard.Id)}/sheet";

        var response = await s.Dm.Client.PatchAsJsonAsync(url, new { overrides = new[] { new { field = "spellcastingAbility.wizard", value = 5, note = "Regla de mesa" } } });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var overridden = await s.Player.GetCharacterAsync(wizard.Id);
        var casting = overridden.Sheet.Spellcasting.Single(c => c.ClassIndex == "wizard");
        Assert.Equal(("wis", true, 8 + 2 + 1), (casting.Ability, casting.AbilityOverridden, casting.SaveDc));

        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PatchAsJsonAsync(url, new { overrides = new[] { new { field = "spellcastingAbility.wizard", value = 7 } } })).StatusCode);
    }

    // ---- Validation ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Invalid_resources_tags_and_grants_are_reported_with_their_path()
    {
        var admin = await factory.CreateAdminClientAsync();
        await ImportAsync(admin);
        var pack = new JsonObject
        {
            ["formatVersion"] = 3,
            ["id"] = "recursos-malos",
            ["name"] = "Recursos malos",
            ["version"] = "1.0.0",
            ["feats"] = new JsonArray(new JsonObject
            {
                ["index"] = "recursos-malos-dote",
                ["name"] = "Dote",
                ["resource"] = new JsonObject
                {
                    ["key"] = "recursos-malos-r",
                    ["name"] = "R",
                    ["max"] = new JsonObject { ["byLevel"] = new JsonObject { ["1"] = 1 }, ["min"] = 1 },
                    ["rechargeRoll"] = new JsonObject { ["dice"] = "d7", ["min"] = 1 },
                },
                ["modifiers"] = new JsonArray(new JsonObject { ["kind"] = "ResourceMaxBonus", ["value"] = 1 }),
                ["grants"] = new JsonObject { ["removeSkills"] = new JsonArray("juggling"), ["conditionImmunities"] = new JsonArray(Numb) },
            }),
            ["spells"] = new JsonArray(new JsonObject
            {
                ["index"] = "recursos-malos-conjuro",
                ["name"] = "Conjuro",
                ["level"] = 1,
                ["school"] = "evocation",
                ["castingTime"] = "1 action",
                ["range"] = "Self",
                ["components"] = new JsonArray("V"),
                ["duration"] = "Instantaneous",
                ["tags"] = new JsonArray("recursos-malos-sin-etiqueta"),
            }),
            ["reference"] = new JsonObject
            {
                ["spellTags"] = new JsonArray(new JsonObject { ["index"] = "recursos-malos-t", ["name"] = "T", ["spells"] = new JsonArray("no-existe") }),
                ["languages"] = new JsonArray(new JsonObject { ["index"] = "recursos-malos-l", ["name"] = "L", ["spells"] = new JsonArray("light") }),
            },
        };

        var errors = await ImportErrorsAsync(admin, pack);
        var all = string.Join("\n", errors);
        foreach (var path in new[]
                 {
                     "feats[0].resource.max.min",
                     "feats[0].resource.rechargeRoll.dice",
                     "feats[0].modifiers[0]",
                     "feats[0].grants.removeSkills[0]",
                     "feats[0].grants.conditionImmunities[0]",
                     "spells[0].tags[0]",
                     "reference.spellTags[0].spells[0]",
                     "reference.languages[0].spells",
                 })
        {
            Assert.True(errors.Any(e => e.StartsWith($"{path}:", StringComparison.Ordinal)), $"{path}\n{all}");
        }
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    private static CharacterResourceDto Resource(CharacterDetailDto character, string key) =>
        character.Resources.SingleOrDefault(r => r.Key == key)
        ?? throw new Xunit.Sdk.XunitException($"Sin recurso {key}: {string.Join(", ", character.Resources.Select(r => r.Key))}");

    private static async Task SpendAsync(CampaignScenario s, string url, Guid resourceId)
    {
        var response = await s.Player.Client.PostAsJsonAsync($"{url}/resources/{resourceId}/spend", new { amount = 1 });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<RechargeRollResultDto> RechargeAsync(CampaignScenario s, string url, Guid resourceId, int value)
    {
        var response = await s.Player.Client.PostAsJsonAsync($"{url}/resources/{resourceId}/recharge-roll", new { value });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<RechargeRollResultDto>())!;
    }

    private static async Task<LevelUpPlanDto> PlanAsync(CampaignScenario s, Guid characterId, string classIndex = "fighter") =>
        await GetAsync<LevelUpPlanDto>(s.Player.Client, $"{ItemTestHelpers.Dnd5eCharacterUrl(characterId)}/level-up?classIndex={classIndex}");

    private static async Task<LevelUpPlanDto> GrantAndPlanAsync(CampaignScenario s, Guid characterId, string classIndex = "fighter")
    {
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/systems/dnd5e/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { characterId } });
        Assert.True(granted.StatusCode == HttpStatusCode.OK, await granted.Content.ReadAsStringAsync());
        return await PlanAsync(s, characterId, classIndex);
    }

    private static Task<HttpResponseMessage> PostLevelUpAsync(CampaignScenario s, Guid characterId, LevelUpPlanDto plan, object? improvement)
    {
        var choices = new List<object>();
        foreach (var choice in plan.Choices.Where(c => c.Required > 0))
        {
            if (choice.Kind == "AsiOrFeat")
            {
                choices.Add(new { key = choice.Key, selected = improvement ?? new { asi = new { str = 1, con = 1 } } });
            }
            else
            {
                choices.Add(new { key = choice.Key, selected = choice.Options.Where(o => o.Eligible && o.Requires is null).Take(choice.Required).Select(o => o.Index).ToArray() });
            }
        }

        return s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(characterId)}/level-up", new { classIndex = plan.ClassIndex, hitPointsRolled = 6, choices });
    }

    /// <summary>The DM grants a level and the player takes it with <paramref name="improvement"/> in the Ability Score Improvement.</summary>
    private static async Task<CharacterDetailDto> LevelUpAsync(CampaignScenario s, Guid characterId, object? improvement)
    {
        var plan = await GrantAndPlanAsync(s, characterId);
        var response = await PostLevelUpAsync(s, characterId, plan, improvement);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private async Task ImportAsync(HttpClient admin)
    {
        await ImportLock.WaitAsync();
        try
        {
            var existing = await GetAsync<List<ContentPackDto>>(admin, PacksUrl);
            foreach (var (id, pack) in new[] { (BaseId, BuildBase()), (Id, Build()) })
            {
                if (existing.Any(p => p.Id == id))
                {
                    continue;
                }

                var response = await admin.PostAsync(PacksUrl, Json(pack));
                Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            }
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
        await s.EnablePacksAsync(BaseId, Id);
        return s;
    }

    /// <summary>An active character of <paramref name="classIndex"/> at <paramref name="level"/> with the given base scores.</summary>
    private static async Task<CharacterDetailDto> DraftAsync(CampaignScenario s, string classIndex, int level, object scores, string? subclass = null)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, classIndex);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            raceIndex = "human",
            classes = new[] { new { classIndex, subclassIndex = subclass, level } },
            baseAbilities = scores,
            applyRacialBonuses = false,
            proficiencies = (classIndex == "wizard" ? new[] { "int", "wis" } : ["str", "con"])
                .Select(key => (object)new { type = "SavingThrow", key, expertise = false })
                .ToArray(),
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.ActivateAsync(character.Id)).StatusCode);
        return await s.Player.GetCharacterAsync(character.Id);
    }

    private static async Task<List<string>> ImportErrorsAsync(HttpClient admin, JsonObject pack)
    {
        var response = await admin.PostAsync(PacksUrl, Json(pack));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    private static StringContent Json(JsonNode pack) => new(pack.ToJsonString(), Encoding.UTF8, "application/json");

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
