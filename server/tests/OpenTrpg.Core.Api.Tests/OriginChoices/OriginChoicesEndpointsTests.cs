using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Api.Tests.Items;
using OpenTrpg.Core.Application.Catalog;
using OpenTrpg.Core.Application.Characters;

namespace OpenTrpg.Core.Api.Tests.OriginChoices;

/// <summary>Race, subrace and background choices imported from the SRD and saved per character (phase 19).</summary>
[Collection(CatalogCollection.Name)]
public class OriginChoicesEndpointsTests(CatalogApiFactory factory)
{
    private static readonly object Scores = new { str = 10, dex = 14, con = 14, @int = 12, wis = 10, cha = 15 };

    [Fact]
    public async Task The_catalog_exposes_the_normalized_choices_of_races_subraces_and_backgrounds()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var halfElf = await GetAsync<RaceDetailDto>(s.Player, "/api/v1/catalog/races/half-elf");
        Assert.Equal((2, 1), (halfElf.Choices!.AbilityBonuses!.Choose, halfElf.Choices.AbilityBonuses.Amount));
        Assert.DoesNotContain(halfElf.Choices.AbilityBonuses.From, o => o.Index == "cha");
        Assert.Equal(2, halfElf.Choices.Skills!.Choose);
        Assert.Contains(halfElf.Choices.Skills.From, o => o is { Index: "stealth", Name: "Stealth" });
        Assert.Equal(1, halfElf.Choices.Languages!.Choose);

        var dragonborn = await GetAsync<RaceDetailDto>(s.Player, "/api/v1/catalog/races/dragonborn");
        var ancestry = Assert.Single(dragonborn.Choices!.TraitOptions);
        Assert.Equal(("draconic-ancestry", 1, 10), (ancestry.Key, ancestry.Choose, ancestry.Options.Count));
        var red = Assert.Single(ancestry.Options, o => o.Index == "draconic-ancestry-red");
        Assert.Equal(("fire", "15 ft. cone", "dex"), (red.DamageType, red.BreathWeapon!.Area, red.BreathWeapon.SaveAbility));

        var elf = await GetAsync<RaceDetailDto>(s.Player, "/api/v1/catalog/races/elf");
        var highElf = Assert.Single(elf.Subraces, r => r.Index == "high-elf");
        Assert.Equal(("wizard", 1), (highElf.Choices!.Cantrip!.SpellList, highElf.Choices.Cantrip.Choose));
        Assert.Contains(highElf.Choices.Cantrip.From, o => o.Index == "mage-hand");

        var dwarf = await GetAsync<RaceDetailDto>(s.Player, "/api/v1/catalog/races/dwarf");
        Assert.Equal(["poison"], dwarf.Resistances);
        Assert.Contains(dwarf.Choices!.Tools!.From, o => o is { Index: "smiths-tools", Name: "Smith's Tools" });

        var human = await GetAsync<RaceDetailDto>(s.Player, "/api/v1/catalog/races/human");
        Assert.Null(human.Choices!.AbilityBonuses);
        Assert.Equal(1, human.Choices.Languages!.Choose);

        var acolyte = Assert.Single(await GetAsync<List<BackgroundDto>>(s.Player, "/api/v1/catalog/backgrounds"), b => b.Index == "acolyte");
        Assert.Equal((2, 0), (acolyte.Choices!.Languages!.Choose, acolyte.Choices.Languages.From.Count));
    }

    [Fact]
    public async Task A_half_elf_asks_for_two_ability_bonuses_and_two_skills_before_activation()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await DraftAsync(s, "half-elf", null, "rogue");

        var plan = await OriginAsync(s.Player, hero.Id);
        Assert.False(plan.Complete);
        var abilities = Assert.Single(plan.Choices, c => c.Key == "race.abilityBonuses");
        Assert.Equal(("AbilityBonus", "race", 2, 2, 1), (abilities.Kind, abilities.Source, abilities.Choose, abilities.Required, abilities.Amount));
        var skills = Assert.Single(plan.Choices, c => c.Key == "race.skills");
        Assert.Equal(("Skill", 2, 2), (skills.Kind, skills.Choose, skills.Required));
        var languages = Assert.Single(plan.Choices, c => c.Key == "race.languages");
        Assert.Equal(0, languages.Required);

        // Activation (and submission) are refused while the required choices are missing.
        var activate = await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/activate", null);
        Assert.Equal(HttpStatusCode.BadRequest, activate.StatusCode);
        Assert.Equal("origin-choices-incomplete", (await activate.ReadProblemAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/submit", null)).StatusCode);

        var wrong = await SaveAsync(s.Player, hero.Id, new { key = "race.abilityBonuses", selected = new[] { "dex" } });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var charisma = await SaveAsync(s.Player, hero.Id, new { key = "race.abilityBonuses", selected = new[] { "cha", "dex" } });
        Assert.Equal(HttpStatusCode.BadRequest, charisma.StatusCode);

        var saved = await SaveOkAsync(s.Player, hero.Id,
            new { key = "race.abilityBonuses", selected = new[] { "dex", "con" } },
            new { key = "race.skills", selected = new[] { "stealth", "insight" } },
            new { key = "race.languages", selected = new[] { "Dwarvish" } });
        Assert.True(saved.Complete);

        var detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal(14 + 1, detail.Sheet.Abilities["dex"].Score);
        Assert.Equal(15 + 2, detail.Sheet.Abilities["cha"].Score);
        Assert.Contains(detail.Sheet.Breakdowns["ability.dex"].Parts, p => p is { Source: "race", Label: "Raza (elección)", Value: 1 });
        Assert.Contains(detail.Sheet.Breakdowns["ability.con"].Parts, p => p is { Source: "race", Value: 1 });
        Assert.True(detail.Sheet.Skills.Single(k => k.Index == "stealth").Proficient);
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Skill", Key: "insight", Source: "Race" });
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Language", Key: "Dwarvish", Source: "Race" });
        var choice = Assert.Single(detail.Choices, c => c.Key == "race.abilityBonuses");
        Assert.Equal((0, (string?)null), (choice.Level, choice.ClassIndex));

        // A full sheet edit of the proficiencies keeps the ones chosen for the race.
        await PatchSheetAsync(s.Player, hero.Id, new { proficiencies = new[] { new { type = "Skill", key = "acrobatics", expertise = false } } });
        Assert.True((await s.Player.GetCharacterAsync(hero.Id)).Sheet.Skills.Single(k => k.Index == "insight").Proficient);

        var activated = await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
    }

    [Fact]
    public async Task A_dragonborn_asks_for_its_ancestry_and_gets_the_resistance_and_breath_weapon()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await DraftAsync(s, "dragonborn", null, "fighter");

        var plan = await OriginAsync(s.Player, hero.Id);
        var ancestry = Assert.Single(plan.Choices, c => c.Required > 0);
        Assert.Equal(("race.trait.draconic-ancestry", "TraitOption", 1), (ancestry.Key, ancestry.Kind, ancestry.Required));
        Assert.Equal("fire", Assert.Single(ancestry.Options, o => o.Index == "draconic-ancestry-red").DamageType);
        Assert.Empty((await s.Player.GetCharacterAsync(hero.Id)).Sheet.Resistances);

        await SaveOkAsync(s.Player, hero.Id, new { key = "race.trait.draconic-ancestry", selected = new[] { "draconic-ancestry-red" } });

        var sheet = (await s.Player.GetCharacterAsync(hero.Id)).Sheet;
        var resistance = Assert.Single(sheet.Resistances);
        Assert.Equal(("fire", "race", "Draconic Ancestry (Red)"), (resistance.DamageType, resistance.Source, resistance.Label));
        var breath = sheet.BreathWeapon!;
        Assert.Equal(("fire", "2d6", "dex", "15 ft. cone"), (breath.DamageType, breath.Dice, breath.SaveAbility, breath.Area));
        Assert.Equal(8 + 2 + 2, breath.Dc); // 8 + Con (14: +2) + proficiency (+2)
        Assert.Equal(breath.Dc, sheet.Breakdowns["breathWeapon.dc"].Total);
    }

    [Fact]
    public async Task A_high_elf_asks_for_a_wizard_cantrip_that_is_always_prepared()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await DraftAsync(s, "elf", "high-elf", "fighter");

        var plan = await OriginAsync(s.Player, hero.Id);
        var cantrip = Assert.Single(plan.Choices, c => c.Key == "race.subrace.cantrip");
        Assert.Equal(("Cantrip", "subrace", 1), (cantrip.Kind, cantrip.Source, cantrip.Required));
        Assert.Contains(cantrip.Options, o => o.Index == "mage-hand");
        Assert.DoesNotContain(cantrip.Options, o => o.Index == "sacred-flame");
        Assert.Single(plan.Choices, c => c.Key == "race.subrace.languages");

        var notInList = await SaveAsync(s.Player, hero.Id, new { key = "race.subrace.cantrip", selected = new[] { "sacred-flame" } });
        Assert.Equal(HttpStatusCode.BadRequest, notInList.StatusCode);
        Assert.True((await SaveOkAsync(s.Player, hero.Id, new { key = "race.subrace.cantrip", selected = new[] { "mage-hand" } })).Complete);

        var detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Contains(detail.Spells, sp => sp is { SpellIndex: "mage-hand", ClassIndex: "race", AlwaysPrepared: true });

        // Changing the subrace drops its choices and their effects.
        await PatchSheetAsync(s.Player, hero.Id, new { raceIndex = "human", subraceIndex = "" });
        var human = await s.Player.GetCharacterAsync(hero.Id);
        Assert.DoesNotContain(human.Spells, sp => sp.SpellIndex == "mage-hand");
        Assert.DoesNotContain(human.Choices, c => c.Key.StartsWith("race.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_human_asks_for_nothing_and_can_be_activated()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await DraftAsync(s, "human", null, "fighter");

        var plan = await OriginAsync(s.Player, hero.Id);

        Assert.True(plan.Complete);
        Assert.DoesNotContain(plan.Choices, c => c.Required > 0);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/activate", null)).StatusCode);
    }

    [Fact]
    public async Task Only_the_owner_of_a_draft_or_a_dm_change_the_choices()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await DraftAsync(s, "dwarf", "hill-dwarf", "fighter");
        var tools = Assert.Single((await OriginAsync(s.Player, hero.Id)).Choices, c => c.Key == "race.tools");
        Assert.Equal(1, tools.Required);

        Assert.Equal(HttpStatusCode.NotFound, (await SaveAsync(s.Outsider, hero.Id, new { key = "race.tools", selected = new[] { "smiths-tools" } })).StatusCode);
        await SaveOkAsync(s.Player, hero.Id, new { key = "race.tools", selected = new[] { "smiths-tools" } });
        var detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Tool", Key: "smiths-tools", Source: "Race" });
        Assert.Equal(["poison"], detail.Sheet.Resistances.Select(r => r.DamageType));

        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/activate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SaveAsync(s.Player, hero.Id, new { key = "race.tools", selected = new[] { "masons-tools" } })).StatusCode);
        await SaveOkAsync(s.Dm, hero.Id, new { key = "race.tools", selected = new[] { "masons-tools" } });

        var after = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Contains(after.Proficiencies, p => p is { Type: "Tool", Key: "masons-tools" });
        Assert.DoesNotContain(after.Proficiencies, p => p is { Type: "Tool", Key: "smiths-tools" });
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Origin_languages_accept_up_to_the_offered_number()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await DraftAsync(s, "elf", "high-elf", "wizard");
        await PatchSheetAsync(s.Player, hero.Id, new { backgroundIndex = "acolyte" });

        var plan = await OriginAsync(s.Player, hero.Id);
        Assert.Equal(1, Assert.Single(plan.Choices, c => c.Key == "race.subrace.languages").Choose);
        var background = Assert.Single(plan.Choices, c => c.Key == "background.languages");
        Assert.Equal((2, 0), (background.Choose, background.Required));

        // Fewer than offered is fine (the wizard warns); more is refused.
        var tooMany = await SaveAsync(s.Player, hero.Id, new { key = "race.subrace.languages", selected = new[] { "Dwarvish", "Giant" } });
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        await SaveOkAsync(s.Player, hero.Id,
            new { key = "race.subrace.languages", selected = new[] { "Dwarvish" } },
            new { key = "background.languages", selected = new[] { "Giant" } });

        var detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Language", Key: "Dwarvish" });
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Language", Key: "Giant", Source: "Background" });
    }

    [Fact]
    public async Task Only_the_dm_adds_languages_to_an_active_character_through_the_sheet()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);
        var patch = new { proficiencies = new[] { new { type = "Language", key = "Orc", expertise = false } } };

        var owner = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/sheet", patch);
        Assert.Equal(HttpStatusCode.Accepted, owner.StatusCode);
        Assert.DoesNotContain((await s.Player.GetCharacterAsync(hero.Id)).Proficiencies, p => p is { Type: "Language", Key: "Orc" });

        await PatchSheetAsync(s.Dm, hero.Id, patch);
        Assert.Contains((await s.Player.GetCharacterAsync(hero.Id)).Proficiencies, p => p is { Type: "Language", Key: "Orc" });
    }

    [Theory]
    [InlineData("elf", "high-elf", "wizard")]
    [InlineData("half-elf", null, "rogue")]
    [InlineData("dwarf", "hill-dwarf", "fighter")]
    [InlineData("dragonborn", null, "fighter")]
    public async Task Every_option_of_every_origin_choice_has_a_description(string race, string? subrace, string classIndex)
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await DraftAsync(s, race, subrace, classIndex);
        await PatchSheetAsync(s.Player, hero.Id, new { backgroundIndex = "acolyte" });

        var plan = await OriginAsync(s.Player, hero.Id);
        Assert.NotEmpty(plan.Choices);
        foreach (var choice in plan.Choices)
        {
            Assert.NotEmpty(choice.Options);
            Assert.All(choice.Options, o => Assert.True(
                o.Description.Count > 0 && o.Description.All(d => !string.IsNullOrWhiteSpace(d)),
                $"{choice.Key}: «{o.Index}» no tiene descripción."));
        }
    }

    [Fact]
    public async Task Origin_languages_and_tools_explain_themselves()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var elf = await DraftAsync(s, "elf", "high-elf", "wizard");
        await PatchSheetAsync(s.Player, elf.Id, new { backgroundIndex = "acolyte" });
        var plan = await OriginAsync(s.Player, elf.Id);

        var languages = Assert.Single(plan.Choices, c => c.Key == "background.languages");
        var common = Assert.Single(languages.Options, o => o.Index == "Common");
        Assert.Equal("Idioma estándar. Hablantes típicos: humanos. Escritura común.", common.Description[0]);
        var deep = Assert.Single(languages.Options, o => o.Index == "Deep Speech");
        Assert.StartsWith("Idioma exótico.", deep.Description[0]);
        Assert.EndsWith("Sin escritura.", deep.Description[0]);
        var cantrip = Assert.Single(plan.Choices, c => c.Key == "race.subrace.cantrip");
        Assert.Single(Assert.Single(cantrip.Options, o => o.Index == "mage-hand").Description);

        var dwarf = await DraftAsync(s, "dwarf", "hill-dwarf", "fighter");
        var tools = Assert.Single((await OriginAsync(s.Player, dwarf.Id)).Choices, c => c.Key == "race.tools");
        Assert.Contains("artisan's tools", Assert.Single(tools.Options, o => o.Index == "smiths-tools").Description[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_language_descriptions_cover_the_srd_dataset()
    {
        using var stream = File.OpenRead(SrdFile("5e-SRD-Languages.json"));
        using var json = System.Text.Json.JsonDocument.Parse(stream);
        var dataset = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(dataset.Count, OriginOptionDescriptions.Languages.Count);
        foreach (var language in dataset)
        {
            var name = language.GetProperty("name").GetString()!;
            var info = Assert.Single(OriginOptionDescriptions.Languages, l => l.Name == name);
            Assert.Equal(language.GetProperty("type").GetString() == "Exotic", info.Exotic);
            Assert.Equal(language.TryGetProperty("script", out var script) && script.ValueKind == System.Text.Json.JsonValueKind.String, info.Script is not null);
            var desc = language.TryGetProperty("desc", out var d) && d.ValueKind == System.Text.Json.JsonValueKind.String ? d.GetString() : null;
            Assert.Equal(desc, info.Description);
        }

        Assert.Equal(OpenTrpg.Core.Domain.Catalog.SrdLanguages.All.Order(), OriginOptionDescriptions.Languages.Select(l => l.Name).Order());
    }

    /// <summary>A file of <c>server/seed/srd</c>, found by walking up from the test binaries.</summary>
    private static string SrdFile(string name)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "seed", "srd", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(name);
    }

    private static string Url(Guid id) => $"{ItemTestHelpers.CharacterUrl(id)}/origin-choices";

    private static async Task<CharacterDetailDto> DraftAsync(CampaignScenario s, string race, string? subrace, string classIndex)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        await PatchSheetAsync(s.Player, character.Id, new
        {
            raceIndex = race,
            subraceIndex = subrace,
            classes = new[] { new { classIndex, level = 1 } },
            baseAbilities = Scores,
        });
        return await s.Player.GetCharacterAsync(character.Id);
    }

    private static async Task PatchSheetAsync(SignedInUser actor, Guid id, object patch)
    {
        var response = await actor.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(id)}/sheet", patch);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<OriginChoicesDto> OriginAsync(SignedInUser actor, Guid id) => await GetAsync<OriginChoicesDto>(actor, Url(id));

    private static Task<HttpResponseMessage> SaveAsync(SignedInUser actor, Guid id, params object[] choices) =>
        actor.Client.PutAsJsonAsync(Url(id), new { choices });

    private static async Task<OriginChoicesDto> SaveOkAsync(SignedInUser actor, Guid id, params object[] choices)
    {
        var response = await SaveAsync(actor, id, choices);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<OriginChoicesDto>())!;
    }

    private static async Task<T> GetAsync<T>(SignedInUser actor, string url)
    {
        var response = await actor.Client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
