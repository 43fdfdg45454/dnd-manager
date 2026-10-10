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

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the format-3 example pack and the packs that require it.</summary>
public sealed class ContentPackV3ApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Format 3 of the content packs (phase 34): a fictitious pack with a full class (20 levels, own slots table,
/// resources and a subclass), a feat, a race, a firearm, a creature, a condition, a rule and vocabularies; the
/// activation of packs per campaign and the dependencies between packs.
/// </summary>
public class ContentPackV3Tests(ContentPackV3ApiFactory factory) : IClassFixture<ContentPackV3ApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string CatalogUrl = "/api/v1/systems/dnd5e/catalog";
    private const string PackId = "ecos-ejemplo";
    private const string Weaver = "ecos-ejemplo-tejedor";
    private const string Choir = "ecos-ejemplo-coro-del-alba";
    private const string Feat = "ecos-ejemplo-oido-fino";
    private const string Echoes = "ecos-ejemplo-ecos";

    private static readonly SemaphoreSlim ImportLock = new(1, 1);

    private static string Example() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-pack-v3-example.json"));

    [Fact]
    public async Task A_v3_pack_imports_every_section_and_the_compendium_serves_them()
    {
        var admin = await factory.CreateAdminClientAsync();
        var result = await ImportExampleAsync(admin);
        if (result is not null)
        {
            Assert.Equal((PackId, 3, "dnd5e"), (result.Id, result.FormatVersion, result.SystemId));
            Assert.Empty(result.Requires);
            Assert.Equal(
                (1, 1, 1, 1, 1, 2, 2, 2, 1, 1),
                (result.Counts["classes"], result.Counts["subclasses"], result.Counts["feats"], result.Counts["creatures"], result.Counts["rules"],
                    result.Counts["reference"], result.Counts["items"], result.Counts["spells"], result.Counts["races"], result.Counts["conditions"]));
        }

        // The class: 20 levels with its own slots table, the resource from classSpecific and the generated choices.
        var weaver = await GetAsync<ClassDetailDto>(admin, $"{CatalogUrl}/classes/{Weaver}");
        Assert.Equal((PackId, 8, 2, "Coro"), (weaver.Source, weaver.HitDie, weaver.SubclassLevel, weaver.SubclassFlavor));
        Assert.Equal(Enumerable.Range(1, 20), weaver.Levels.Select(l => l.Level));
        Assert.Equal(("table", "prepared", true), (weaver.Spellcasting!.Progression, weaver.Spellcasting.Preparation, weaver.Spellcasting.Ritual));
        Assert.True(weaver.IsSpellcaster);
        Assert.Equal([2, 0, 0, 0, 0, 0, 0, 0, 0], weaver.Levels[0].SpellSlots);
        Assert.Equal([4, 2, 0, 0, 0, 0, 0, 0, 0], weaver.Levels[3].SpellSlots);
        Assert.Equal([4, 3, 2, 0, 0, 0, 0, 0, 0], weaver.Levels[4].SpellSlots);
        Assert.Equal([4, 3, 3, 3, 3, 2, 1, 1, 1], weaver.Levels[19].SpellSlots);
        Assert.Equal((2, 3, 4), (weaver.Levels[0].CantripsKnown, weaver.Levels[3].CantripsKnown, weaver.Levels[9].CantripsKnown));
        Assert.Equal((2, 3, 6), (weaver.Levels[0].ProfBonus, weaver.Levels[4].ProfBonus, weaver.Levels[19].ProfBonus));
        Assert.Equal((0, 1, 5), (weaver.Levels[2].AbilityScoreBonuses, weaver.Levels[3].AbilityScoreBonuses, weaver.Levels[19].AbilityScoreBonuses));
        Assert.Contains(weaver.Levels[0].Features, f => f.Index == "ecos-ejemplo-eco-resonante");
        var resource = Assert.Single(weaver.Resources);
        Assert.Equal((Echoes, "ShortRest", 2, 3, 5), (resource.Key, resource.Recharge, resource.MaxByLevel![1], resource.MaxByLevel[5], resource.MaxByLevel[20]));
        Assert.Equal(13, weaver.Multiclassing!.Prerequisites["int"]);
        var subclass = Assert.Single(weaver.Subclasses, x => x.Source == PackId);
        Assert.Equal((Choir, PackId), (subclass.Index, subclass.Source));
        Assert.Contains((await GetAsync<List<ClassSummaryDto>>(admin, $"{CatalogUrl}/classes")), c => c.Index == Weaver && c.Source == PackId);

        // The class list: its own spells and the wizard's.
        var firstLevel = await GetAsync<PagedResult<SpellSummaryDto>>(admin, $"{CatalogUrl}/spells?class={Weaver}&level=1&pageSize=200");
        Assert.Contains(firstLevel.Items, s => s.Index == "ecos-ejemplo-onda");
        Assert.Contains(firstLevel.Items, s => s.Index == "magic-missile");
        Assert.DoesNotContain(firstLevel.Items, s => s.Index == "cure-wounds");

        // The firearm keeps its reload and misfire in the system data.
        var gun = Assert.Single((await GetAsync<PagedResult<ItemSummaryDto>>(admin, $"{CatalogUrl}/items?search=trueno%20de%20mano")).Items);
        var gunDetail = await GetAsync<ItemDetailDto>(admin, $"{CatalogUrl}/items/{gun.Id}");
        Assert.Equal(("1d10", "piercing", 30, 90), (gunDetail.DamageDice, gunDetail.DamageType, gunDetail.RangeNormal, gunDetail.RangeLong));
        Assert.Contains("firearm", gunDetail.Properties);
        Assert.Equal(2, gunDetail.SystemData!.Value.GetProperty("firearm").GetProperty("misfire").GetInt32());
        var bullet = Assert.Single((await GetAsync<PagedResult<ItemSummaryDto>>(admin, $"{CatalogUrl}/items?search=bala%20de%20eco")).Items);
        Assert.True((await GetAsync<ItemDetailDto>(admin, $"{CatalogUrl}/items/{bullet.Id}")).SystemData!.Value.GetProperty("ammunition").GetBoolean());

        // Creature, condition, rule and vocabularies.
        var bat = Assert.Single((await GetAsync<List<BeastSummaryDto>>(admin, $"{CatalogUrl}/beasts?search=eco")), b => b.Index == "ecos-ejemplo-murcielago-eco");
        Assert.Equal((PackId, "beast"), (bat.Source, bat.Type));
        var batDetail = await GetAsync<BeastDto>(admin, $"{CatalogUrl}/beasts/ecos-ejemplo-murcielago-eco");
        Assert.Equal((12, 3, PackId), (batDetail.ArmorClass, batDetail.HitPoints, batDetail.Source));
        Assert.Contains(await GetAsync<List<ConditionDto>>(admin, $"{CatalogUrl}/conditions"), c => c.Index == "ecos-ejemplo-resonando" && c.Source == PackId);
        Assert.Contains(await GetAsync<List<RuleSummaryDto>>(admin, $"{CatalogUrl}/rules"), r => r.Index == "ecos-ejemplo-resonancia" && r.Category == "variant");
        Assert.Single(await GetAsync<List<RuleSummaryDto>>(admin, $"{CatalogUrl}/rules?category=Variant&q=ecos"));
        Assert.Empty(await GetAsync<List<RuleSummaryDto>>(admin, $"{CatalogUrl}/rules?category=equipment"));
        var rule = await GetAsync<RuleDto>(admin, $"{CatalogUrl}/rules/ecos-ejemplo-resonancia");
        Assert.Equal(PackId, rule.Source);
        Assert.Equal(["ecos"], rule.Tags);
        Assert.Single(rule.Body);
        var languages = await GetAsync<List<ReferenceEntryDto>>(admin, $"{CatalogUrl}/reference/languages");
        Assert.Contains(languages, l => l.Index == "common" && l.Source == "srd");
        Assert.Contains(languages, l => l.Index == "ecos-ejemplo-lengua-del-eco" && l.Source == PackId);
        Assert.Contains(await GetAsync<List<ReferenceEntryDto>>(admin, $"{CatalogUrl}/reference/damageTypes"), d => d.Index == "ecos-ejemplo-sonico");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{CatalogUrl}/reference/monsters")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{CatalogUrl}/rules/no-such-rule")).StatusCode);
    }

    [Fact]
    public async Task A_character_of_the_new_class_is_created_and_levelled_to_5_through_the_api()
    {
        var admin = await factory.CreateAdminClientAsync();
        await ImportExampleAsync(admin);
        var s = await ScenarioWithPackAsync();
        var hero = await DraftAsync(s, Weaver, level: 1, intelligence: 15, race: "ecos-ejemplo-resonante");
        Assert.Equal(("Tejedor de ecos", "Resonante"), (hero.Classes.Single().ClassName, hero.RaceName));
        Assert.Equal(17, hero.Sheet.Abilities["int"].Score);
        Assert.Equal(2, hero.SpellSlots.Single(sl => sl.Level == 1).Max);
        Assert.Equal(2, hero.Resources.Single(r => r.Key == Echoes).Max);

        string? subclass = null;
        for (var level = 2; level <= 5; level++)
        {
            await GrantAsync(s, hero.Id);
            var plan = await PlanAsync(s.Player, hero.Id);
            Assert.Equal((Weaver, level, 8), (plan.ClassIndex, plan.ClassLevel, plan.HitDie));
            switch (level)
            {
                case 2:
                    var choir = Assert.Single(plan.Choices, c => c.Kind == "Subclass");
                    Assert.Equal(("Coro", 1), (choir.Name, choir.Required));
                    Assert.Equal([Choir], choir.Options.Select(o => o.Index));
                    break;
                case 4:
                    var asi = Assert.Single(plan.Choices, c => c.Kind == "AsiOrFeat");
                    Assert.Contains(asi.Options, o => o.Index == Feat);
                    var cantrips = Assert.Single(plan.Choices, c => c.Kind == "CantripsKnown");
                    Assert.Equal(1, cantrips.Required);
                    Assert.Contains(cantrips.Options, o => o.Index == "ecos-ejemplo-susurro");
                    Assert.Contains(cantrips.Options, o => o.Index == "fire-bolt");
                    Assert.All(cantrips.Options, o => Assert.Equal(0, o.SpellLevel));
                    break;
                default:
                    Assert.DoesNotContain(plan.Choices, c => c.Kind is "Subclass" or "AsiOrFeat");
                    break;
            }

            hero = await ApplyAsync(s.Player, hero.Id, new { classIndex = Weaver, hitPointsRolled = 5, choices = Answers(plan, hero) });
            subclass ??= hero.Classes.Single().SubclassIndex;
            Assert.Equal(level, hero.Classes.Single().Level);
        }

        var weaver = hero.Classes.Single();
        Assert.Equal((Choir, "Coro del Alba", 5, false), (weaver.SubclassIndex, weaver.SubclassName, weaver.Level, weaver.CatalogMissing));
        Assert.Equal([(1, 4), (2, 3), (3, 2)], hero.SpellSlots.Where(sl => sl.Max > 0).OrderBy(sl => sl.Level).Select(sl => (sl.Level, sl.Max)));
        Assert.Equal(3, hero.Resources.Single(r => r.Key == Echoes).Max);
        Assert.False(hero.CatalogMissing);
    }

    [Fact]
    public async Task Packs_are_seen_only_where_a_dm_enables_them_and_disabling_one_keeps_the_sheet()
    {
        var admin = await factory.CreateAdminClientAsync();
        await ImportExampleAsync(admin);
        var s = await factory.CreateCampaignScenarioAsync();
        var scoped = $"?campaignId={s.CampaignId}";

        // Imported packs start disabled: the compendium of the campaign and the sheet do not offer them.
        var packs = await GetAsync<List<CampaignContentPackDto>>(s.Player.Client, $"{s.Url}/content-packs");
        Assert.Contains(packs, p => p is { Id: "srd", IsBase: true, Enabled: true });
        Assert.Contains(packs, p => p is { Id: PackId, IsBase: false, Enabled: false });
        Assert.DoesNotContain(await GetAsync<List<ClassSummaryDto>>(s.Player.Client, $"{CatalogUrl}/classes{scoped}"), c => c.Index == Weaver);
        Assert.Contains(await GetAsync<List<ClassSummaryDto>>(s.Player.Client, $"{CatalogUrl}/classes"), c => c.Index == Weaver);
        var sources = await GetAsync<List<CatalogSourceDto>>(s.Player.Client, $"{CatalogUrl}/sources{scoped}");
        Assert.Equal(("srd", true, true), (sources[0].Id, sources[0].IsBase, sources[0].Enabled));
        Assert.Contains(sources, x => x.Id == PackId && x.Enabled == false);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync($"{CatalogUrl}/classes{scoped}")).StatusCode);

        var draft = await s.Player.CreateCharacterAsync(s.CampaignId, "Sin paquete");
        var refused = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(draft.Id)}/sheet", new { classes = new[] { new { classIndex = Weaver, level = 1 } } });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("paquete desactivado", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // Only the DM enables packs.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.PutAsJsonAsync($"{s.Url}/content-packs", new { packIds = new[] { PackId } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PutAsJsonAsync($"{s.Url}/content-packs", new { packIds = new[] { "no-such-pack" } })).StatusCode);
        await s.EnablePacksAsync(PackId);
        Assert.Contains(await GetAsync<List<ClassSummaryDto>>(s.Player.Client, $"{CatalogUrl}/classes{scoped}"), c => c.Index == Weaver);
        Assert.Contains(await GetAsync<List<CatalogSourceDto>>(s.Player.Client, $"{CatalogUrl}/sources{scoped}"), x => x.Id == PackId && x.Enabled == true);

        // A weaver, and a fighter who takes the pack feat at level 4.
        var weaver = await DraftAsync(s, Weaver, level: 1, intelligence: 15);
        var fighter = await DraftAsync(s, "fighter", level: 3, intelligence: 10, subclass: "champion");
        await GrantAsync(s, fighter.Id);
        var plan = await PlanAsync(s.Player, fighter.Id);
        var asi = Assert.Single(plan.Choices, c => c.Kind == "AsiOrFeat");
        Assert.Contains(asi.Options, o => o.Index == Feat && o.Eligible);
        fighter = await ApplyAsync(s.Player, fighter.Id, new { classIndex = "fighter", hitPointsRolled = 5, choices = new object[] { new { key = "asi", selected = new { feat = Feat } } } });
        Assert.Empty(fighter.InvalidChoices);

        // Disabled again: what the characters have still resolves; the feat is flagged.
        await s.Dm.Client.PutAsJsonAsync($"{s.Url}/content-packs", new { packIds = Array.Empty<string>() });
        var weaverAfter = await s.Player.GetCharacterAsync(weaver.Id);
        Assert.Equal(("Tejedor de ecos", false), (weaverAfter.Classes.Single().ClassName, weaverAfter.CatalogMissing));
        Assert.Equal(2, weaverAfter.Resources.Single(r => r.Key == Echoes).Max);
        var fighterAfter = await s.Player.GetCharacterAsync(fighter.Id);
        var invalid = Assert.Single(fighterAfter.InvalidChoices);
        Assert.Equal((Feat, "pack-disabled"), (invalid.Item.Index, invalid.Code));
        Assert.Contains("paquete desactivado", invalid.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(await GetAsync<List<ClassSummaryDto>>(s.Player.Client, $"{CatalogUrl}/classes{scoped}"), c => c.Index == Weaver);

        // Keeping the class still works and it keeps levelling up; a new class must be enabled.
        var keep = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(weaver.Id)}/sheet", new { classes = new[] { new { classIndex = Weaver, level = 1 } } });
        Assert.Equal(HttpStatusCode.OK, keep.StatusCode);
        await GrantAsync(s, weaver.Id);
        var next = await PlanAsync(s.Player, weaver.Id);
        Assert.Equal((Weaver, 2), (next.ClassIndex, next.ClassLevel));
        Assert.Contains(next.Classes, c => c.ClassIndex == Weaver);
        Assert.DoesNotContain(next.Classes, c => c.ClassIndex != Weaver && c.ClassIndex.StartsWith("ecos-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_pack_can_require_another_one_whose_content_it_extends()
    {
        var admin = await factory.CreateAdminClientAsync();
        await ImportExampleAsync(admin);

        JsonObject Extra(string id, params string[] requires) => JsonSerializer.SerializeToNode(new
        {
            formatVersion = 3,
            id,
            name = $"Extra {id}",
            version = "1.0.0",
            requires,
            classes = new[]
            {
                new
                {
                    extends = Weaver,
                    subclasses = new[] { new { index = $"{id}-coro-del-ocaso", name = "Coro del Ocaso", levels = Array.Empty<object>() } },
                },
            },
        })!.AsObject();

        // Without requires the pack cannot see the class it extends; an unknown requirement is rejected.
        var orphan = await ImportErrorsAsync(admin, Extra("ecos-huerfano"));
        Assert.Contains(orphan, e => e.StartsWith("classes[0].extends:", StringComparison.Ordinal));
        var unknown = await ImportErrorsAsync(admin, Extra("ecos-desconocido", "no-importado"));
        Assert.Contains(unknown, e => e.StartsWith("requires[0]:", StringComparison.Ordinal));

        var created = await admin.PostAsync(PacksUrl, Json(Extra("ecos-ocaso", PackId)));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var result = (await created.Content.ReadFromJsonAsync<ContentPackImportResultDto>())!;
        Assert.Equal([PackId], result.Requires);
        Assert.Contains(await GetAsync<List<ContentPackDto>>(admin, PacksUrl), p => p.Id == "ecos-ocaso" && p.Requires.SequenceEqual(new[] { PackId }));
        var weaver = await GetAsync<ClassDetailDto>(admin, $"{CatalogUrl}/classes/{Weaver}");
        Assert.Contains(weaver.Subclasses, x => x.Index == "ecos-ocaso-coro-del-ocaso" && x.Source == "ecos-ocaso");

        // A campaign cannot enable it without the pack it requires.
        var s = await factory.CreateCampaignScenarioAsync();
        var missing = await s.Dm.Client.PutAsJsonAsync($"{s.Url}/content-packs", new { packIds = new[] { "ecos-ocaso" } });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("missing-requirement", await missing.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var both = await s.Dm.Client.PutAsJsonAsync($"{s.Url}/content-packs", new { packIds = new[] { "ecos-ocaso", PackId } });
        Assert.Equal(HttpStatusCode.OK, both.StatusCode);
        var classes = await GetAsync<ClassDetailDto>(s.Player.Client, $"{CatalogUrl}/classes/{Weaver}?campaignId={s.CampaignId}");
        Assert.Equal(2, classes.Subclasses.Count);

        // A pack other packs require cannot be deleted before them.
        var required = await admin.DeleteAsync($"{PacksUrl}/{PackId}");
        Assert.Equal(HttpStatusCode.Conflict, required.StatusCode);
        Assert.Contains("ecos-ocaso", await required.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_v3_classes_items_and_creatures_are_reported_with_their_path()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = JsonNode.Parse(Example())!.AsObject();
        pack["id"] = "ecos-malos";
        var json = pack.ToJsonString().Replace("ecos-ejemplo-", "ecos-malos-", StringComparison.Ordinal);
        pack = JsonNode.Parse(json)!.AsObject();
        var weaver = pack["classes"]![0]!.AsObject();
        weaver["levels"]!.AsArray().RemoveAt(19);
        weaver["hitDie"] = 7;
        weaver["spellcasting"]!["slots"] = null;
        weaver["resources"]![0]!["max"] = "classSpecific:nada";
        weaver["subclassFlavor"] = new string('x', 500);
        pack["items"]![0]!["firearm"]!["misfire"] = 0;
        pack["items"]![1]!["weapon"] = JsonNode.Parse("""{ "category": "exotic", "range": "melee", "damage": "1d4", "damageType": "piercing" }""");
        pack["creatures"]![0]!["challengeRating"] = 40;
        pack["creatures"]![0]!["abilities"]!["luck"] = 3;
        pack["reference"]!["languages"]![0]!["index"] = "lengua-sin-prefijo";

        var errors = await ImportErrorsAsync(admin, pack);
        var all = string.Join("\n", errors);

        Assert.Contains(errors, e => e.StartsWith("classes[0].levels: Faltan niveles: 20", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("classes[0].hitDie:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("classes[0].spellcasting.slots:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("classes[0].resources[0].max:", StringComparison.Ordinal));
        Assert.True(errors.Any(e => e.StartsWith("classes[0].subclassFlavor:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("items[0].firearm.misfire:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("items[1].weapon.category:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("creatures[0].challengeRating:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("creatures[0].abilities.luck:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("reference.languages[0].index:", StringComparison.Ordinal)), all);

        // Extensions of an existing class only add subclasses and level choices.
        var extension = JsonSerializer.SerializeToNode(new
        {
            formatVersion = 3,
            id = "ecos-extension-mala",
            name = "Extensión mala",
            version = "1",
            classes = new object[] { new { extends = "wizard", hitDie = 6, levels = Array.Empty<object>() } },
        })!;
        var extensionErrors = await ImportErrorsAsync(admin, extension);
        Assert.Contains(extensionErrors, e => e.StartsWith("classes[0].hitDie:", StringComparison.Ordinal));
        Assert.Contains(extensionErrors, e => e.StartsWith("classes[0].levels:", StringComparison.Ordinal));

        // Only installed systems.
        var otherSystem = await ImportErrorsAsync(admin, JsonSerializer.SerializeToNode(new { formatVersion = 3, system = "otro-sistema", id = "ecos-otro", name = "Otro", version = "1" })!);
        Assert.Contains(otherSystem, e => e.StartsWith("system:", StringComparison.Ordinal));
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    /// <summary>Imports the example once per database; returns the result when this call imported it.</summary>
    private async Task<ContentPackImportResultDto?> ImportExampleAsync(HttpClient admin)
    {
        await ImportLock.WaitAsync();
        try
        {
            if ((await GetAsync<List<ContentPackDto>>(admin, PacksUrl)).Any(p => p.Id == PackId))
            {
                return null;
            }

            var response = await admin.PostAsync(PacksUrl, new StringContent(Example(), Encoding.UTF8, "application/json"));
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<ContentPackImportResultDto>())!;
        }
        finally
        {
            ImportLock.Release();
        }
    }

    private async Task<CampaignScenario> ScenarioWithPackAsync()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        await s.EnablePacksAsync(PackId);
        return s;
    }

    /// <summary>An active character of <paramref name="classIndex"/> at <paramref name="level"/>.</summary>
    private static async Task<CharacterDetailDto> DraftAsync(
        CampaignScenario s, string classIndex, int level, int intelligence, string? race = null, string? subclass = null)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, classIndex);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            raceIndex = race,
            classes = new[] { new { classIndex, subclassIndex = subclass, level } },
            baseAbilities = new { str = 12, dex = 14, con = 13, @int = intelligence, wis = 10, cha = 8 },
            applyRacialBonuses = race is not null,
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null)).StatusCode);
        return await s.Player.GetCharacterAsync(character.Id);
    }

    /// <summary>Answers every required choice with its first eligible options (an ASI as +1 to the two lowest abilities).</summary>
    private static object[] Answers(LevelUpPlanDto plan, CharacterDetailDto hero)
    {
        var chosenSubclass = plan.Choices.FirstOrDefault(c => c.Kind == "Subclass")?.Options.FirstOrDefault()?.Index ?? hero.Classes.Single().SubclassIndex;
        var answers = new List<object>();
        foreach (var choice in plan.Choices.Where(c => c.Required > 0 && (c.SubclassIndex is null || c.SubclassIndex == chosenSubclass)))
        {
            if (choice.Kind == "AsiOrFeat")
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

    private static async Task<LevelUpPlanDto> PlanAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up");
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
