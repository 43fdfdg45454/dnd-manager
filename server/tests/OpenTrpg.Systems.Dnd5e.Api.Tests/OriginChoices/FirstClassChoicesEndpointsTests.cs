using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Characters;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.OriginChoices;

/// <summary>
/// The level-1 choices of the first class (fighting style, expertise, favored enemy...) are asked with the origin choices
/// (<c>class.*</c> keys), recorded as level choices of class level 1, block the activation while missing, and are caught
/// up by the next level-up of a character that never answered them.
/// </summary>
[Collection(CatalogCollection.Name)]
public class FirstClassChoicesEndpointsTests(CatalogApiFactory factory)
{
    private static readonly object FighterScores = new { str = 16, dex = 10, con = 14, @int = 8, wis = 10, cha = 10 };

    [Fact]
    public async Task A_fighter_chooses_its_fighting_style_at_creation_and_defense_adds_to_the_armor_class()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await DraftAsync(s, "fighter", 1, FighterScores);

        var plan = await OriginAsync(s.Player, hero.Id);
        Assert.False(plan.Complete);
        var style = Assert.Single(plan.Choices, c => c.Key == "class.fighting-style");
        Assert.Equal(("OptionSet", "class", "fighter", 1, 1, 1, "fighting-styles"), (style.Kind, style.Source, style.ClassIndex, style.Level, style.Choose, style.Required, style.SetId));
        Assert.Contains(style.Options, o => o.Index == "fighting-style-archery");
        var preview = Assert.Single(style.Options.Single(o => o.Index == "fighting-style-defense").EffectsPreview);
        Assert.Equal(("armorClass", 1, "con armadura"), (preview.Field, preview.Value, preview.Condition));

        // Activation and submission wait for the fighting style.
        var refused = await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/activate", null);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("origin-choices-incomplete", (await refused.ReadProblemAsync()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/submit", null)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(s.Player, hero.Id, new { key = "class.fighting-style", selected = new[] { "fighting-style-unknown" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(s.Player, hero.Id, new { key = "class.fighting-style", selected = new[] { "fighting-style-archery", "fighting-style-defense" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(s.Player, hero.Id, new { key = "class.unknown", selected = new[] { "x" } })).StatusCode);

        await SaveOkAsync(s.Player, hero.Id, new { key = "class.fighting-style", selected = new[] { "fighting-style-archery" } });
        var saved = await SaveOkAsync(s.Player, hero.Id, new { key = "class.fighting-style", selected = new[] { "fighting-style-defense" } });
        Assert.True(saved.Complete);
        var answered = Assert.Single(saved.Choices, c => c.Key == "class.fighting-style");
        Assert.Equal("fighting-style-defense", Assert.Single(answered.Selected).Index);
        // The recorded style stays offered (and eligible) to answer again.
        Assert.True(answered.Options.Single(o => o.Index == "fighting-style-defense").Eligible);

        var detail = await s.Player.GetCharacterAsync(hero.Id);
        var choice = Assert.Single(detail.System().Choices);
        Assert.Equal(("fighting-style", 1, "fighter", "OptionSet"), (choice.Key, choice.Level, choice.ClassIndex, choice.Kind));
        Assert.Empty(detail.System().InvalidChoices);

        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/activate", null)).StatusCode);
        var mail = await s.Dm.AddItemAsync(hero.Id, new { templateId = await s.Player.SrdItemIdAsync("Chain Mail"), quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(hero.Id, mail.Id, new { equipped = true })).StatusCode);
        var armored = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal(16 + 1, armored.Sheet.ArmorClass);
        Assert.Contains(armored.Sheet.Breakdowns["armorClass"].Parts, p => p is { Source: "feature", Label: "Defense (nivel 1)", Value: 1 });

        // Once active only a DM changes it, directly.
        Assert.Equal(HttpStatusCode.Conflict, (await SaveAsync(s.Player, hero.Id, new { key = "class.fighting-style", selected = new[] { "fighting-style-dueling" } })).StatusCode);
        await SaveOkAsync(s.Dm, hero.Id, new { key = "class.fighting-style", selected = new[] { "fighting-style-dueling" } });
        var dueling = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal(16, dueling.Sheet.ArmorClass);
        Assert.Equal("fighting-style-dueling", Assert.Single(dueling.System().Choices).Selected.Single().Index);

        // The level-up does not ask for it again.
        await GrantAsync(s, hero.Id);
        Assert.DoesNotContain((await PlanAsync(s.Player, hero.Id)).Choices, c => c.Key == "fighting-style");
    }

    [Fact]
    public async Task A_rogue_chooses_its_expertise_among_its_skills_and_can_change_it_while_a_draft()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var proficiencies = new[] { "stealth", "perception", "acrobatics" }.Select(key => new { type = "Skill", key, expertise = false }).ToArray();
        var hero = await DraftAsync(s, "rogue", 1, new { str = 10, dex = 16, con = 12, @int = 10, wis = 12, cha = 10 }, proficiencies);

        var expertise = Assert.Single((await OriginAsync(s.Player, hero.Id)).Choices, c => c.Key == "class.expertise");
        Assert.Equal(("Expertise", 2, 2), (expertise.Kind, expertise.Choose, expertise.Required));
        Assert.Equal(["acrobatics", "perception", "stealth"], expertise.Options.Select(o => o.Index).Order());

        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(s.Player, hero.Id, new { key = "class.expertise", selected = new[] { "stealth" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveAsync(s.Player, hero.Id, new { key = "class.expertise", selected = new[] { "stealth", "arcana" } })).StatusCode);
        await SaveOkAsync(s.Player, hero.Id, new { key = "class.expertise", selected = new[] { "stealth", "perception" } });
        var detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.True(detail.Sheet.Skills.Single(k => k.Index == "stealth").Expertise);
        Assert.Equal(3 + 2 * 2, detail.Sheet.Skills.Single(k => k.Index == "stealth").Value); // Dex +3, proficiency +2 doubled
        Assert.True(detail.Sheet.Skills.Single(k => k.Index == "perception").Expertise);

        // Changing the answer undoes the expertise it no longer picks.
        await SaveOkAsync(s.Player, hero.Id, new { key = "class.expertise", selected = new[] { "stealth", "acrobatics" } });
        detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.False(detail.Sheet.Skills.Single(k => k.Index == "perception").Expertise);
        Assert.True(detail.Sheet.Skills.Single(k => k.Index == "acrobatics").Expertise);

        // The wizard sends the full sheet at the end and then the same answers again: the expertise comes back.
        await PatchSheetAsync(s.Player, hero.Id, new { proficiencies });
        Assert.False((await s.Player.GetCharacterAsync(hero.Id)).Sheet.Skills.Single(k => k.Index == "stealth").Expertise);
        await SaveOkAsync(s.Player, hero.Id, new { key = "class.expertise", selected = new[] { "stealth", "acrobatics" } });
        detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.True(detail.Sheet.Skills.Single(k => k.Index == "stealth").Expertise);
        Assert.Equal("expertise", Assert.Single(detail.System().Choices).Key);

        // An empty answer removes it; changing the class drops the choices of the old one.
        await SaveOkAsync(s.Player, hero.Id, new { key = "class.expertise", selected = Array.Empty<string>() });
        Assert.False((await s.Player.GetCharacterAsync(hero.Id)).Sheet.Skills.Single(k => k.Index == "stealth").Expertise);
        await SaveOkAsync(s.Player, hero.Id, new { key = "class.expertise", selected = new[] { "stealth", "acrobatics" } });
        await PatchSheetAsync(s.Player, hero.Id, new { classes = new[] { new { classIndex = "barbarian", level = 1 } } });
        var barbarian = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Empty(barbarian.System().Choices);
        Assert.True((await OriginAsync(s.Player, hero.Id)).Complete);
    }

    [Fact]
    public async Task A_character_without_its_level_one_choices_gets_them_at_the_next_level_up()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        // A DM turns an active character into a level 5 ranger with a sheet edit: nothing asked its level-1 choices.
        var hero = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId, "Exploradora");
        await PatchSheetAsync(s.Dm, hero.Id, new
        {
            classes = new[] { new { classIndex = "ranger", subclassIndex = "hunter", level = 5 } },
            baseAbilities = new { str = 10, dex = 16, con = 12, @int = 10, wis = 14, cha = 10 },
            applyRacialBonuses = false,
        });
        var origin = await OriginAsync(s.Player, hero.Id);
        Assert.False(origin.Complete);
        Assert.Equal(["class.favored-enemy", "class.natural-explorer"], origin.Choices.Where(c => c.Required > 0).Select(c => c.Key).Order());

        await GrantAsync(s, hero.Id);
        var plan = await PlanAsync(s.Player, hero.Id);
        // Level 6 grants one more of each: the missed ones of level 1 add up.
        var enemy = Assert.Single(plan.Choices, c => c.Key == "favored-enemy");
        var terrain = Assert.Single(plan.Choices, c => c.Key == "natural-explorer");
        Assert.Equal((2, 2), (enemy.Choose, enemy.Required));
        Assert.Equal((2, 2), (terrain.Choose, terrain.Required));

        var after = await ApplyLevelUpAsync(s.Player, hero.Id, new
        {
            hitPointsRolled = 5,
            // The hunter's choice of level 3, also missed, is caught up as well.
            choices = plan.Choices.Where(c => c.Required > 0)
                .Select(c => new { key = c.Key, selected = c.Options.Where(o => o.Eligible).Take(c.Required).Select(o => o.Index).ToArray() })
                .ToArray(),
        });
        Assert.Equal(2, after.Choices.Single(c => c.Key == "favored-enemy").Selected.Count);
        Assert.True((await OriginAsync(s.Player, hero.Id)).Complete);

        await GrantAsync(s, hero.Id);
        var next = await PlanAsync(s.Player, hero.Id);
        Assert.DoesNotContain(next.Choices, c => c.Key is "favored-enemy" or "natural-explorer");
    }

    [Fact]
    public async Task A_fighter_activated_without_a_style_by_a_class_change_catches_it_up_on_level_2()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId, "Guerrero");
        await PatchSheetAsync(s.Dm, hero.Id, new { classes = new[] { new { classIndex = "fighter", level = 1 } }, baseAbilities = FighterScores, applyRacialBonuses = false });
        await GrantAsync(s, hero.Id);

        var plan = await PlanAsync(s.Player, hero.Id);
        var style = Assert.Single(plan.Choices, c => c.Key == "fighting-style");
        Assert.Equal((1, 1), (style.Choose, style.Required));
        var missing = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/level-up", new { hitPointsRolled = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var after = await ApplyLevelUpAsync(s.Player, hero.Id, new { hitPointsRolled = 5, choices = new object[] { new { key = "fighting-style", selected = new[] { "fighting-style-defense" } } } });
        Assert.Equal(("fighting-style", 2), (after.Choices.Single().Key, after.Choices.Single().Level));
        var origin = await OriginAsync(s.Player, hero.Id);
        Assert.True(origin.Complete);
        Assert.Equal("fighting-style-defense", origin.Choices.Single(c => c.Key == "class.fighting-style").Selected.Single().Index);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    private static string Url(Guid id) => $"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/origin-choices";

    private static async Task<CharacterDetailDto> DraftAsync(CampaignScenario s, string classIndex, int level, object scores, object[]? proficiencies = null)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, classIndex);
        await PatchSheetAsync(s.Player, character.Id, new
        {
            classes = new[] { new { classIndex, level } },
            baseAbilities = scores,
            applyRacialBonuses = false,
            proficiencies = proficiencies ?? [],
        });
        return await s.Player.GetCharacterAsync(character.Id);
    }

    private static async Task PatchSheetAsync(SignedInUser actor, Guid id, object patch)
    {
        var response = await actor.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/sheet", patch);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<OriginChoicesDto> OriginAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync(Url(id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OriginChoicesDto>())!;
    }

    private static Task<HttpResponseMessage> SaveAsync(SignedInUser actor, Guid id, params object[] choices) =>
        actor.Client.PutAsJsonAsync(Url(id), new { choices });

    private static async Task<OriginChoicesDto> SaveOkAsync(SignedInUser actor, Guid id, params object[] choices)
    {
        var response = await SaveAsync(actor, id, choices);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<OriginChoicesDto>())!;
    }

    private static async Task GrantAsync(CampaignScenario s, Guid id)
    {
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/systems/dnd5e/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { id } });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
    }

    private static async Task<LevelUpPlanDto> PlanAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LevelUpPlanDto>())!;
    }

    private static async Task<CharacterDetailDto> ApplyLevelUpAsync(SignedInUser actor, Guid id, object body)
    {
        var response = await actor.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up", body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }
}
