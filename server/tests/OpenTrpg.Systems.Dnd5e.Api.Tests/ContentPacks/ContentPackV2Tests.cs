using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Content packs in format 2: option sets, level choices and grants (phase 16c).</summary>
public class ContentPackV2Tests(ContentPackApiFactory factory) : IClassFixture<ContentPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";

    [Fact]
    public async Task A_v2_pack_adds_a_feat_and_a_subclass_whose_level_3_choice_and_grants_apply_on_level_up()
    {
        var admin = await factory.CreateAdminClientAsync();
        var result = await ImportAsync(admin, Example());

        Assert.Equal((1, 3, 1, 1), (result.Counts["optionSets"], result.Counts["options"], result.Counts["levelChoices"], result.Counts["subclasses"]));

        var s = await factory.CreateCampaignScenarioAsync();

        await s.EnablePacksAsync();
        var hero = await ActiveFighterAsync(s, level: 2);

        var plan = await PlanAsync(s.Player, hero.Id);
        var subclass = Assert.Single(plan.Choices, c => c.Kind == "Subclass");
        Assert.Contains(subclass.Options, o => o.Index == "tierras-ejemplo-guardia");
        Assert.Contains(subclass.Options, o => o.Index == "champion");
        var oath = Assert.Single(plan.Choices, c => c.Key == "juramento");
        Assert.Equal(("tierras-ejemplo-guardia", 1), (oath.SubclassIndex, oath.Required));
        Assert.Equal(["tierras-ejemplo-juramento-del-faro", "tierras-ejemplo-juramento-del-muro"], oath.Options.Select(o => o.Index).Order());
        Assert.Contains(plan.AutomaticFeatures, f => f.SubclassIndex == "tierras-ejemplo-guardia" && f.Feature.Name == "Juramento");

        var after = await ApplyAsync(s.Player, hero.Id, new
        {
            hitPointsRolled = 6,
            choices = new object[]
            {
                new { key = "subclass", selected = new[] { "tierras-ejemplo-guardia" } },
                new { key = "juramento", selected = new[] { "tierras-ejemplo-juramento-del-faro" } },
            },
        });

        Assert.Equal("tierras-ejemplo-guardia", after.Classes.Single().SubclassIndex);
        Assert.Contains(after.Proficiencies, p => p is { Type: "Skill", Key: "perception", Source: "Class" });
        Assert.Contains(after.Spells, sp => sp is { SpellIndex: "light", ClassIndex: "fighter", AlwaysPrepared: true });
        var flash = Assert.Single(after.Resources, r => r.Key == "tierras-ejemplo-destello");
        Assert.Equal(("Destello", 2, "LongRest", true), (flash.Name, flash.Max, flash.Recharge, flash.IsAuto));

        // Level 4: the pack's feat is offered with the SRD one and raises its fixed ability.
        await GrantAsync(s, hero.Id);
        var asi = Assert.Single((await PlanAsync(s.Player, hero.Id)).Choices, c => c.Kind == "AsiOrFeat");
        var feat = Assert.Single(asi.Options, o => o.Index == "tierras-ejemplo-vigia-incansable");
        Assert.True(feat.Eligible);
        Assert.Equal(["wis"], feat.AbilityIncrease!.From);
        Assert.Equal(("initiative", 2), (feat.EffectsPreview.Single().Field, feat.EffectsPreview.Single().Value));

        var level4 = await ApplyAsync(s.Player, hero.Id, new
        {
            hitPointsRolled = 6,
            choices = new object[] { new { key = "asi", selected = new { feat = "tierras-ejemplo-vigia-incansable" } } },
        });

        Assert.Equal(14, level4.Sheet.Abilities["wis"].Score);
        Assert.Equal(1 + 2, level4.Sheet.Initiative);
        Assert.Contains(level4.Sheet.Breakdowns["initiative"].Parts, p => p is { Source: "feature", Label: "Vigía incansable (nivel 4)", Value: 2 });
        Assert.Equal(("tierras-ejemplo-vigia-incansable", "wis"), (level4.Choices.Last().Feat!.Index, level4.Choices.Last().Ability));
    }

    [Fact]
    public async Task Invalid_v2_content_is_reported_with_its_path()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = new
        {
            formatVersion = 3,
            id = "malo-ejemplo",
            name = "Malo",
            version = "1",
            optionSets = new object[]
            {
                new
                {
                    setId = "feats",
                    options = new object[]
                    {
                        new { index = "sin-prefijo", name = "X" },
                        new { index = "malo-ejemplo-y", name = "Y", modifiers = new[] { new { kind = "ArmorClassBonus", value = 1, condition = "bajoLaLluvia" } } },
                    },
                },
            },
            classes = new object[]
            {
                new
                {
                    extends = "fighter",
                    levelChoices = new object[]
                    {
                        new { level = 3, key = "algo", name = "Algo", kind = "OptionSet", setId = "malo-ejemplo-no-existe", choose = 1 },
                        new { level = 4, key = "asi", name = "Repetida", kind = "AsiOrFeat", choose = 1 },
                    },
                },
            },
        };

        var errors = await ImportErrorsAsync(admin, JsonSerializer.Serialize(pack));

        Assert.Contains(errors, e => e.StartsWith("optionSets[0].options[0].index:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("optionSets[0].options[1].modifiers[0].condition:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("classes[0].levelChoices[0].setId:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("classes[0].levelChoices[1]:", StringComparison.Ordinal) && e.Contains("srd", StringComparison.Ordinal));

        var v1 = await ImportErrorsAsync(admin, JsonSerializer.Serialize(new { id = "viejo-ejemplo", name = "Viejo", version = "1", optionSets = Array.Empty<object>() }));
        Assert.Contains(v1, e => e.StartsWith("formatVersion:", StringComparison.Ordinal));

        var v2 = await ImportErrorsAsync(admin, JsonSerializer.Serialize(new { formatVersion = 2, id = "viejo-ejemplo", name = "Viejo", version = "1" }));
        Assert.Contains(v2, e => e.StartsWith("formatVersion:", StringComparison.Ordinal) && e.Contains("3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Race_proficiency_and_spellcasting_prerequisites_explain_what_the_character_lacks()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = new
        {
            formatVersion = 3,
            id = "requisitos-ejemplo",
            name = "Requisitos de Ejemplo",
            version = "1.0.0",
            optionSets = new object[]
            {
                new
                {
                    setId = "feats",
                    options = new object[]
                    {
                        new { index = "requisitos-ejemplo-elfica", name = "Gracia élfica", description = new[] { "Texto de ejemplo." }, prerequisites = new { races = new[] { "elf", "half-elf" } } },
                        new { index = "requisitos-ejemplo-acorazada", name = "Coraza firme", description = new[] { "Texto de ejemplo." }, prerequisites = new { proficiency = new { armor = new[] { "heavy" } } } },
                        new { index = "requisitos-ejemplo-arcana", name = "Chispa arcana", description = new[] { "Texto de ejemplo." }, prerequisites = new { spellcasting = true } },
                        new { index = "requisitos-ejemplo-esgrima", name = "Esgrima", description = new[] { "Texto de ejemplo." }, prerequisites = new { proficiency = new { weapon = new[] { "martial" } } }, abilityIncrease = new { amount = 1, from = new[] { "str", "dex" } } },
                    },
                },
            },
        };
        var result = await ImportAsync(admin, JsonSerializer.Serialize(pack));
        Assert.Equal(4, result.Counts["options"]);

        var s = await factory.CreateCampaignScenarioAsync();

        await s.EnablePacksAsync();
        var hero = await ActiveFighterAsync(s, level: 3);
        var proficiencies = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", subclassIndex = "champion", level = 3 } },
            proficiencies = new[] { new { type = "Armor", key = "all-armor", expertise = false }, new { type = "Weapon", key = "martial-weapons", expertise = false } },
        });
        Assert.Equal(HttpStatusCode.OK, proficiencies.StatusCode);
        var asi = Assert.Single((await PlanAsync(s.Player, hero.Id)).Choices, c => c.Kind == "AsiOrFeat");

        var elvish = Assert.Single(asi.Options, o => o.Index == "requisitos-ejemplo-elfica");
        Assert.False(elvish.Eligible);
        Assert.Equal("Requiere ser Elf o Half-Elf; no tienes raza.", elvish.Reason);
        Assert.Equal("Ser Elf o Half-Elf", elvish.PrerequisitesText);
        // A fighter has "all-armor", which covers heavy armor.
        var armored = Assert.Single(asi.Options, o => o.Index == "requisitos-ejemplo-acorazada");
        Assert.True(armored.Eligible);
        Assert.Equal("Competencia con armadura pesada", armored.PrerequisitesText);
        var arcane = Assert.Single(asi.Options, o => o.Index == "requisitos-ejemplo-arcana");
        Assert.False(arcane.Eligible);
        Assert.Equal("Requiere poder lanzar al menos un conjuro; no lanzas conjuros.", arcane.Reason);
        Assert.Equal("Poder lanzar al menos un conjuro", arcane.PrerequisitesText);
        var fencing = Assert.Single(asi.Options, o => o.Index == "requisitos-ejemplo-esgrima");
        Assert.True(fencing.Eligible);
        Assert.Equal(["str", "dex"], fencing.AbilityIncrease!.From);

        // A half-elf that knows a cantrip meets the race and the spellcasting prerequisites.
        var patch = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/sheet", new
        {
            raceIndex = "half-elf",
            spells = new[] { new { spellIndex = "light", classIndex = "fighter", isPrepared = true } },
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var again = Assert.Single((await PlanAsync(s.Player, hero.Id)).Choices, c => c.Kind == "AsiOrFeat");
        Assert.True(again.Options.Single(o => o.Index == "requisitos-ejemplo-elfica").Eligible);
        Assert.True(again.Options.Single(o => o.Index == "requisitos-ejemplo-arcana").Eligible);

        // A feat with several abilities to choose from raises the chosen one, naming the feat in the breakdown.
        var missingAbility = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/level-up", new
        {
            hitPointsRolled = 6,
            choices = new object[] { new { key = "asi", selected = new { feat = "requisitos-ejemplo-esgrima" } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingAbility.StatusCode);
        var after = await ApplyAsync(s.Player, hero.Id, new
        {
            hitPointsRolled = 6,
            choices = new object[] { new { key = "asi", selected = new { feat = "requisitos-ejemplo-esgrima", ability = "dex" } } },
        });
        Assert.Equal((16, 12 + 1), (after.Sheet.Abilities["str"].Score, after.Sheet.Abilities["dex"].Score));
        Assert.Contains(after.Sheet.Breakdowns["ability.dex"].Parts, p => p is { Source: "feature", Label: "Esgrima (nivel 4)", Value: 1 });
        Assert.DoesNotContain(after.Sheet.Breakdowns["ability.str"].Parts, p => p.Source == "feature");
    }

    [Fact]
    public async Task Unknown_races_and_armor_in_prerequisites_are_reported()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = new
        {
            formatVersion = 3,
            id = "requisitos-malos",
            name = "Requisitos malos",
            version = "1",
            optionSets = new object[]
            {
                new
                {
                    setId = "feats",
                    options = new object[]
                    {
                        new { index = "requisitos-malos-x", name = "X", prerequisites = new { races = new[] { "nadie" }, proficiency = new { armor = new[] { "plate" } } } },
                    },
                },
            },
        };

        var errors = await ImportErrorsAsync(admin, JsonSerializer.Serialize(pack));

        Assert.Contains(errors, e => e.StartsWith("optionSets[0].options[0].prerequisites.races[0]:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("optionSets[0].options[0].prerequisites.proficiency.armor[0]:", StringComparison.Ordinal));
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    private static string Example() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-pack-v2-example.json"));

    private static async Task<ContentPackImportResultDto> ImportAsync(HttpClient admin, string json)
    {
        var response = await admin.PostAsync(PacksUrl, new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ContentPackImportResultDto>())!;
    }

    private static async Task<List<string>> ImportErrorsAsync(HttpClient admin, string json)
    {
        var response = await admin.PostAsync(PacksUrl, new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    private static async Task<CharacterDetailDto> ActiveFighterAsync(CampaignScenario s, int level)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, "Guardia");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", level } },
            baseAbilities = new { str = 16, dex = 12, con = 14, @int = 10, wis = 13, cha = 10 },
            applyRacialBonuses = false,
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null)).StatusCode);
        await GrantAsync(s, character.Id);
        return character;
    }

    private static async Task GrantAsync(CampaignScenario s, Guid characterId)
    {
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/systems/dnd5e/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { characterId } });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
    }

    private static async Task<LevelUpPlanDto> PlanAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LevelUpPlanDto>())!;
    }

    private static async Task<CharacterDetailDto> ApplyAsync(SignedInUser actor, Guid id, object body)
    {
        var response = await actor.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up", body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }
}
