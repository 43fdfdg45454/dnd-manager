using System.Net;
using System.Net.Http.Json;
using Dnd.Api.Tests.Items;
using Dnd.Application.Characters;

namespace Dnd.Api.Tests.LevelUp;

/// <summary>Level-up plan and application with the SRD level choice catalog (phase 16c).</summary>
[Collection(CatalogCollection.Name)]
public class LevelUpEndpointsTests(CatalogApiFactory factory)
{
    private static readonly object FighterScores = new { str = 16, dex = 12, con = 14, @int = 10, wis = 10, cha = 10 };

    [Fact]
    public async Task Fighter_1_to_2_has_no_choices_and_gains_action_surge_and_the_rolled_hit_points()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveAsync(s, "fighter", 1, FighterScores);

        var plan = await PlanAsync(s.Player, hero.Id);

        Assert.Equal((1, 2, "fighter", 2, 10, 2), (plan.CurrentLevel, plan.TargetLevel, plan.ClassIndex, plan.ClassLevel, plan.HitDie, plan.ConModifier));
        Assert.Empty(plan.Choices);
        Assert.Contains(plan.AutomaticFeatures, f => f.Feature.Name.StartsWith("Action Surge", StringComparison.Ordinal) && f.SubclassIndex is null);
        Assert.Null(plan.Spellcasting);

        var after = await ApplyAsync(s.Player, hero.Id, new { classIndex = "fighter", hitPointsRolled = 7, choices = Array.Empty<object>() });

        Assert.Equal(2, after.Classes.Single().Level);
        Assert.Null(after.PendingLevelUpTo);
        Assert.Equal(12 + 7 + 2, after.Sheet.HitPointsMax);
        Assert.Equal(after.Sheet.HitPointsMax, after.HitPointsCurrent);
        Assert.Contains("tiradas 7", after.Sheet.Breakdowns["hitPointsMax"].Parts[0].Label);
        Assert.Contains(after.Combat.Resources, r => r.Key == "action-surge");
        Assert.Empty(after.Choices);
    }

    [Fact]
    public async Task Fighter_2_to_3_asks_for_the_subclass()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveAsync(s, "fighter", 2, FighterScores);

        var plan = await PlanAsync(s.Player, hero.Id);

        var subclass = Assert.Single(plan.Choices);
        Assert.Equal(("subclass", "Subclass", 1, 1), (subclass.Key, subclass.Kind, subclass.Choose, subclass.Required));
        Assert.Contains(subclass.Options, o => o.Index == "champion");
        Assert.Contains(plan.AutomaticFeatures, f => f.SubclassIndex == "champion");

        var missing = await PostAsync(s.Player, hero.Id, new { classIndex = "fighter", hitPointsRolled = 5, choices = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var after = await ApplyAsync(s.Player, hero.Id, new
        {
            classIndex = "fighter",
            hitPointsRolled = 5,
            choices = new object[] { new { key = "subclass", selected = new[] { "champion" } } },
        });

        Assert.Equal(("champion", 3), (after.Classes.Single().SubclassIndex, after.Classes.Single().Level));
        var choice = Assert.Single(after.Choices);
        Assert.Equal(("subclass", 3, "Champion"), (choice.Key, choice.Level, choice.Selected.Single().Name));
    }

    [Fact]
    public async Task Fighter_3_to_4_offers_an_improvement_or_a_feat_and_rejects_a_feat_without_its_prerequisite()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveAsync(s, "fighter", 3, new { str = 12, dex = 16, con = 14, @int = 10, wis = 10, cha = 10 }, subclass: "champion");

        var plan = await PlanAsync(s.Player, hero.Id);

        var asi = Assert.Single(plan.Choices);
        Assert.Equal(("asi", "AsiOrFeat", 1), (asi.Key, asi.Kind, asi.Required));
        var grappler = Assert.Single(asi.Options, o => o.Index == "grappler");
        Assert.False(grappler.Eligible);
        Assert.Equal("Requiere Fuerza 13; tienes 12.", grappler.Reason);

        var feat = await PostAsync(s.Player, hero.Id, new
        {
            classIndex = "fighter",
            hitPointsRolled = 6,
            choices = new object[] { new { key = "asi", selected = new { feat = "grappler" } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, feat.StatusCode);
        var tooMuch = await PostAsync(s.Player, hero.Id, new
        {
            classIndex = "fighter",
            hitPointsRolled = 6,
            choices = new object[] { new { key = "asi", selected = new { asi = new { str = 2, dex = 1 } } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);

        var after = await ApplyAsync(s.Player, hero.Id, new
        {
            classIndex = "fighter",
            hitPointsRolled = 6,
            choices = new object[] { new { key = "asi", selected = new { asi = new { str = 1, dex = 1 } } } },
        });

        Assert.Equal((13, 17), (after.Sheet.Abilities["str"].Score, after.Sheet.Abilities["dex"].Score));
        Assert.Contains(after.Sheet.Breakdowns["ability.str"].Parts, p => p is { Source: "feature", Label: "Mejora de característica (nivel 4)", Value: 1 });
        var choice = Assert.Single(after.Choices);
        Assert.Equal(1, choice.Asi!["dex"]);
    }

    [Fact]
    public async Task Warlock_2_asks_for_two_invocations_filtered_by_their_prerequisites()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var scores = new { str = 8, dex = 14, con = 14, @int = 10, wis = 10, cha = 16 };
        var warlock = await ActiveAsync(s, "warlock", 1, scores, subclass: "fiend");

        var plan = await PlanAsync(s.Player, warlock.Id);

        var invocations = Assert.Single(plan.Choices, c => c.Key == "eldritch-invocations");
        Assert.Equal(("OptionSet", 2, 2, true), (invocations.Kind, invocations.Choose, invocations.Required, invocations.Replaces));
        var agonizing = Assert.Single(invocations.Options, o => o.Index == "eldritch-invocation-agonizing-blast");
        Assert.False(agonizing.Eligible);
        Assert.Contains("Eldritch Blast", agonizing.Reason);
        Assert.False(Assert.Single(invocations.Options, o => o.Index == "eldritch-invocation-thirsting-blade").Eligible);
        Assert.True(Assert.Single(invocations.Options, o => o.Index == "eldritch-invocation-armor-of-shadows").Eligible);
        var spells = Assert.Single(plan.Choices, c => c.Key == "spells-known");
        Assert.All(spells.Options, o => Assert.Equal(1, o.SpellLevel));

        // Choosing the cantrip in the sheet makes Agonizing Blast eligible.
        await PatchSheetAsync(s.Dm, warlock.Id, new { spells = new[] { new { spellIndex = "eldritch-blast", classIndex = "warlock", isPrepared = true } } });
        var withCantrip = await PlanAsync(s.Player, warlock.Id);
        Assert.True(withCantrip.Choices.Single(c => c.Key == "eldritch-invocations").Options.Single(o => o.Index == "eldritch-invocation-agonizing-blast").Eligible);

        var rejected = await PostAsync(s.Player, warlock.Id, new
        {
            classIndex = "warlock",
            hitPointsRolled = 4,
            choices = new object[]
            {
                new { key = "eldritch-invocations", selected = new[] { "eldritch-invocation-agonizing-blast", "eldritch-invocation-thirsting-blade" } },
                new { key = "spells-known", selected = new[] { spells.Options[0].Index } },
            },
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var after = await ApplyAsync(s.Player, warlock.Id, new
        {
            classIndex = "warlock",
            hitPointsRolled = 4,
            choices = new object[]
            {
                new { key = "eldritch-invocations", selected = new[] { "eldritch-invocation-agonizing-blast", "eldritch-invocation-armor-of-shadows" } },
                new { key = "spells-known", selected = new[] { spells.Options[0].Index } },
            },
        });

        Assert.Contains(after.Spells, sp => sp is { SpellIndex: "mage-armor", AlwaysPrepared: true });
        Assert.Contains(after.Spells, sp => sp.SpellIndex == spells.Options[0].Index && sp.IsPrepared && !sp.AlwaysPrepared);
        Assert.Equal(
            ["Agonizing Blast", "Armor of Shadows"],
            after.Choices.Single(c => c.Key == "eldritch-invocations").Selected.Select(x => x.Name).Order());
    }

    [Fact]
    public async Task Warlock_3_may_replace_an_invocation_and_loses_the_spell_it_granted()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var warlock = await ActiveAsync(s, "warlock", 1, new { str = 8, dex = 14, con = 14, @int = 10, wis = 10, cha = 16 }, subclass: "fiend");
        var level2 = await PlanAsync(s.Player, warlock.Id);
        var spell2 = level2.Choices.Single(c => c.Key == "spells-known").Options[0].Index;
        await ApplyAsync(s.Player, warlock.Id, new
        {
            hitPointsRolled = 4,
            choices = new object[]
            {
                new { key = "eldritch-invocations", selected = new[] { "eldritch-invocation-armor-of-shadows", "eldritch-invocation-devils-sight" } },
                new { key = "spells-known", selected = new[] { spell2 } },
            },
        });
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { warlock.Id } });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);

        var plan = await PlanAsync(s.Player, warlock.Id);

        var invocations = Assert.Single(plan.Choices, c => c.Key == "eldritch-invocations");
        Assert.Equal((0, 0, true), (invocations.Choose, invocations.Required, invocations.Replaces));
        Assert.Equal(["eldritch-invocation-armor-of-shadows", "eldritch-invocation-devils-sight"], invocations.Known.Select(k => k.Index).Order());
        Assert.DoesNotContain(invocations.Options, o => o.Index == "eldritch-invocation-armor-of-shadows");
        var boon = Assert.Single(plan.Choices, c => c.Key == "pact-boon");
        var spell3 = plan.Choices.Single(c => c.Key == "spells-known").Options.First(o => o.Index != spell2).Index;

        var after = await ApplyAsync(s.Player, warlock.Id, new
        {
            hitPointsRolled = 4,
            choices = new object[]
            {
                new { key = "pact-boon", selected = new[] { "pact-of-the-chain" } },
                new { key = "spells-known", selected = new[] { spell3 } },
                new { key = "eldritch-invocations", selected = new[] { "eldritch-invocation-beast-speech" }, replaced = new[] { "eldritch-invocation-armor-of-shadows" } },
            },
        });

        Assert.DoesNotContain(after.Spells, sp => sp.SpellIndex == "mage-armor");
        Assert.Contains(after.Spells, sp => sp is { SpellIndex: "speak-with-animals", AlwaysPrepared: true });
        Assert.Contains(after.Spells, sp => sp is { SpellIndex: "find-familiar", AlwaysPrepared: true });
        var replacement = after.Choices.Single(c => c.Key == "eldritch-invocations" && c.Level == 3);
        Assert.Equal(("Beast Speech", "Armor of Shadows"), (replacement.Selected.Single().Name, replacement.Replaced.Single().Name));
        Assert.Contains(boon.Options, o => o.Index == "pact-of-the-tome");
    }

    [Fact]
    public async Task Wizard_2_adds_two_spellbook_spells_of_a_castable_level_known_but_not_prepared()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var wizard = await ActiveAsync(s, "wizard", 1, new { str = 8, dex = 14, con = 12, @int = 16, wis = 10, cha = 10 });

        var plan = await PlanAsync(s.Player, wizard.Id);

        var spellbook = Assert.Single(plan.Choices, c => c.Key == "spellbook");
        Assert.Equal(("SpellbookSpells", 2), (spellbook.Kind, spellbook.Required));
        Assert.NotEmpty(spellbook.Options);
        Assert.All(spellbook.Options, o => Assert.Equal(1, o.SpellLevel));
        Assert.Contains(spellbook.Options, o => o.Index == "magic-missile");
        Assert.DoesNotContain(spellbook.Options, o => o.Index == "cure-wounds");
        Assert.Equal((1, 3), (plan.Spellcasting!.MaxSpellLevel, plan.Spellcasting.SpellSlots[0]));
        Assert.Contains(plan.Choices, c => c.Kind == "Subclass");

        var after = await ApplyAsync(s.Player, wizard.Id, new
        {
            classIndex = "wizard",
            hitPointsRolled = 3,
            choices = new object[]
            {
                new { key = "subclass", selected = new[] { "evocation" } },
                new { key = "spellbook", selected = new[] { "magic-missile", "shield" } },
            },
        });

        Assert.Equal(["magic-missile", "shield"], after.Spells.Where(sp => sp.ClassIndex == "wizard").Select(sp => sp.SpellIndex).Order());
        Assert.All(after.Spells, sp => Assert.False(sp.IsPrepared));
        Assert.Equal("evocation", after.Classes.Single().SubclassIndex);
    }

    [Fact]
    public async Task Multiclassing_into_wizard_needs_intelligence_13()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveAsync(s, "fighter", 2, new { str = 16, dex = 12, con = 14, @int = 12, wis = 10, cha = 10 });

        var plan = await PlanAsync(s.Player, hero.Id, "wizard");

        var wizard = Assert.Single(plan.Classes, c => c.ClassIndex == "wizard");
        Assert.Equal((false, true, 6), (wizard.Allowed, wizard.IsNew, wizard.HitDie));
        Assert.Contains("Inteligencia 13", wizard.Reason);
        Assert.True(Assert.Single(plan.Classes, c => c.ClassIndex == "fighter").Allowed);
        Assert.True(Assert.Single(plan.Classes, c => c.ClassIndex == "barbarian").Allowed);
        Assert.Contains("Destreza 13", Assert.Single(plan.Classes, c => c.ClassIndex == "rogue").Reason);

        var response = await PostAsync(s.Player, hero.Id, new { classIndex = "wizard", hitPointsRolled = 4, choices = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Multiclassing_into_rogue_adds_the_class_its_proficiencies_and_expertise()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveAsync(s, "fighter", 2, new { str = 16, dex = 14, con = 14, @int = 10, wis = 10, cha = 10 }, skills: ["athletics", "perception"]);

        var plan = await PlanAsync(s.Player, hero.Id, "rogue");
        var expertise = Assert.Single(plan.Choices, c => c.Key == "expertise");
        Assert.Equal(["athletics", "perception"], expertise.Options.Select(o => o.Index).Order());

        // Multiclassing into a rogue asks for one skill of the rogue list (PHB), without the ones the character has.
        var skill = Assert.Single(plan.Choices, c => c.Key == "multiclass-skill");
        Assert.Equal(("Skill", 1), (skill.Kind, skill.Required));
        Assert.Contains(skill.Options, o => o.Index == "stealth");
        Assert.DoesNotContain(skill.Options, o => o.Index is "perception" or "athletics" or "arcana");

        var missing = await PostAsync(s.Player, hero.Id, new
        {
            classIndex = "rogue",
            hitPointsRolled = 8,
            choices = new object[] { new { key = "expertise", selected = new[] { "athletics", "perception" } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var after = await ApplyAsync(s.Player, hero.Id, new
        {
            classIndex = "rogue",
            hitPointsRolled = 8,
            choices = new object[]
            {
                new { key = "expertise", selected = new[] { "athletics", "perception" } },
                new { key = "multiclass-skill", selected = new[] { "stealth" } },
            },
        });

        Assert.Equal([("fighter", 2), ("rogue", 1)], after.Classes.Select(c => (c.ClassIndex, c.Level)));
        Assert.Contains(after.Proficiencies, p => p is { Type: "Tool", Key: "thieves-tools" });
        Assert.True(after.Sheet.Skills.Single(k => k.Index == "athletics").Expertise);
        Assert.True(after.Sheet.Skills.Single(k => k.Index == "stealth").Proficient);
    }

    [Fact]
    public async Task Hit_points_out_of_the_die_range_are_rejected()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveAsync(s, "fighter", 1, FighterScores);

        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(s.Player, hero.Id, new { classIndex = "fighter", hitPointsRolled = 11 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(s.Player, hero.Id, new { classIndex = "fighter", hitPointsRolled = 0 })).StatusCode);
        Assert.Equal(2, (await s.Player.GetCharacterAsync(hero.Id)).PendingLevelUpTo);
    }

    [Fact]
    public async Task Players_need_a_granted_level_but_dms_level_up_directly()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveAsync(s, "fighter", 1, FighterScores, grant: false);

        Assert.Equal(HttpStatusCode.Conflict, (await s.Player.Client.GetAsync(Url(hero.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(s.Player, hero.Id, new { hitPointsRolled = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync(Url(hero.Id))).StatusCode);

        var after = await ApplyAsync(s.Dm, hero.Id, new { hitPointsRolled = 5 });

        Assert.Equal(2, after.Classes.Single().Level);
    }

    [Fact]
    public async Task Defense_adds_1_to_the_armor_class_only_with_armor_and_shows_in_the_breakdown()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var paladin = await ActiveAsync(s, "paladin", 1, new { str = 16, dex = 10, con = 14, @int = 8, wis = 10, cha = 14 });

        var plan = await PlanAsync(s.Player, paladin.Id);
        var style = Assert.Single(plan.Choices, c => c.Key == "fighting-style");
        Assert.Equal(
            ["fighting-style-defense", "fighting-style-dueling", "fighting-style-great-weapon-fighting", "fighting-style-protection"],
            style.Options.Select(o => o.Index).Order());
        var preview = Assert.Single(style.Options.Single(o => o.Index == "fighting-style-defense").EffectsPreview);
        Assert.Equal(("armorClass", 1, "con armadura"), (preview.Field, preview.Value, preview.Condition));

        var after = await ApplyAsync(s.Player, paladin.Id, new
        {
            hitPointsRolled = 6,
            choices = new object[] { new { key = "fighting-style", selected = new[] { "paladin-fighting-style-defense" } } },
        });
        Assert.Equal(10, after.Sheet.ArmorClass);
        Assert.DoesNotContain(after.Sheet.Breakdowns["armorClass"].Parts, p => p.Source == "feature");

        var mail = await s.Dm.AddItemAsync(paladin.Id, new { templateId = await s.Player.SrdItemIdAsync("Chain Mail"), quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(paladin.Id, mail.Id, new { equipped = true })).StatusCode);
        var armored = await s.Player.GetCharacterAsync(paladin.Id);

        Assert.Equal(17, armored.Sheet.ArmorClass);
        Assert.Contains(armored.Sheet.Breakdowns["armorClass"].Parts, p => p is { Source: "feature", Label: "Defense (nivel 2)", Value: 1 });
        Assert.Equal("fighting-style-defense", Assert.Single(armored.Choices).Selected.Single().Index);
    }

    [Fact]
    public async Task Bard_3_expertise_doubles_the_chosen_skills()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var bard = await ActiveAsync(
            s, "bard", 2, new { str = 8, dex = 14, con = 12, @int = 10, wis = 10, cha = 16 }, skills: ["stealth", "perception", "persuasion"]);

        var plan = await PlanAsync(s.Player, bard.Id);

        var expertise = Assert.Single(plan.Choices, c => c.Key == "expertise");
        Assert.Equal(["perception", "persuasion", "stealth"], expertise.Options.Select(o => o.Index).Order());
        var bonus = Assert.Single(plan.Choices, c => c.Key == "bonus-proficiencies");
        Assert.Equal(("lore", 3), (bonus.SubclassIndex, bonus.Required));
        Assert.DoesNotContain(bonus.Options, o => o.Index == "stealth");
        var spells = Assert.Single(plan.Choices, c => c.Key == "spells-known");

        var after = await ApplyAsync(s.Player, bard.Id, new
        {
            classIndex = "bard",
            hitPointsRolled = 5,
            choices = new object[]
            {
                new { key = "subclass", selected = new[] { "lore" } },
                new { key = "bonus-proficiencies", selected = new[] { "arcana", "history", "insight" } },
                new { key = "expertise", selected = new[] { "stealth", "perception" } },
                new { key = "spells-known", selected = new[] { spells.Options[0].Index } },
            },
        });

        var stealth = after.Sheet.Skills.Single(k => k.Index == "stealth");
        Assert.True(stealth.Expertise);
        Assert.Equal(2 + 2 + 2, stealth.Value);
        Assert.Contains(after.Sheet.Breakdowns["skill.stealth"].Parts, p => p.Source == "expertise");
        Assert.False(after.Sheet.Skills.Single(k => k.Index == "persuasion").Expertise);
        Assert.True(after.Sheet.Skills.Single(k => k.Index == "arcana").Proficient);
        Assert.Equal("lore", after.Classes.Single().SubclassIndex);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    private static string Url(Guid id) => $"{ItemTestHelpers.CharacterUrl(id)}/level-up";

    /// <summary>Character of the player, activated by the DM and (unless <paramref name="grant"/> is false) granted the next level.</summary>
    private static async Task<CharacterDetailDto> ActiveAsync(
        CampaignScenario s,
        string classIndex,
        int level,
        object scores,
        string? subclass = null,
        string[]? skills = null,
        bool grant = true)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, classIndex);
        await PatchSheetAsync(s.Player, character.Id, new
        {
            classes = new[] { new { classIndex, subclassIndex = subclass, level } },
            baseAbilities = scores,
            applyRacialBonuses = false,
            proficiencies = (skills ?? []).Select(key => new { type = "Skill", key, expertise = false }).ToArray(),
        });
        var activate = await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        if (grant)
        {
            var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { character.Id } });
            Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        }

        return await s.Player.GetCharacterAsync(character.Id);
    }

    private static async Task PatchSheetAsync(SignedInUser actor, Guid id, object patch)
    {
        var response = await actor.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(id)}/sheet", patch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<LevelUpPlanDto> PlanAsync(SignedInUser actor, Guid id, string? classIndex = null)
    {
        var response = await actor.Client.GetAsync(classIndex is null ? Url(id) : $"{Url(id)}?classIndex={classIndex}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LevelUpPlanDto>())!;
    }

    private static Task<HttpResponseMessage> PostAsync(SignedInUser actor, Guid id, object body) =>
        actor.Client.PostAsJsonAsync(Url(id), body);

    private static async Task<CharacterDetailDto> ApplyAsync(SignedInUser actor, Guid id, object body)
    {
        var response = await PostAsync(actor, id, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }
}
