using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Api.Tests.Items;
using OpenTrpg.Core.Application.Catalog;
using OpenTrpg.Core.Application.Characters;

namespace OpenTrpg.Core.Api.Tests.LevelUp;

/// <summary>
/// Walks every SRD class from level 1 to 20 through the level-up plan and checks, level by level, that each
/// choice the SRD grants is asked as a required, answerable choice (phase 24): ability score improvements,
/// the subclass (and its catch-up), cantrips and spells known from the class tables, the wizard's spellbook,
/// expertise, fighting styles, invocations, pact boon, metamagic, mystic arcanum, favored enemies and terrains.
/// </summary>
[Collection(CatalogCollection.Name)]
public class LevelUpWalkthroughTests(CatalogApiFactory factory)
{
    private static readonly string[] Skills = ["athletics", "perception", "stealth", "arcana"];

    /// <summary>Levels with an Ability Score Improvement (SRD text; the rogue's table in the dataset is not monotonic).</summary>
    private static int[] AsiLevels(string classIndex) => classIndex switch
    {
        "fighter" => [4, 6, 8, 12, 14, 16, 19],
        "rogue" => [4, 8, 10, 12, 16, 19],
        _ => [4, 8, 12, 16, 19],
    };

    private static int SubclassLevel(string classIndex) => classIndex switch
    {
        "cleric" or "sorcerer" or "warlock" => 1,
        "druid" or "wizard" => 2,
        _ => 3,
    };

    /// <summary>(key, kind, choose) expected at a level besides the ASI, the subclass and the spell counts.</summary>
    private static IReadOnlyList<(string Key, string Kind, int Choose)> Expected(string classIndex, int level) => (classIndex, level) switch
    {
        ("bard", 3) or ("bard", 10) => [("expertise", "Expertise", 2)],
        ("rogue", 6) => [("expertise", "Expertise", 2)],
        ("paladin", 2) or ("ranger", 2) => [("fighting-style", "OptionSet", 1)],
        ("warlock", 2) => [("eldritch-invocations", "OptionSet", 2)],
        ("warlock", 3) => [("pact-boon", "OptionSet", 1)],
        ("warlock", 5) or ("warlock", 7) or ("warlock", 9) or ("warlock", 12) or ("warlock", 15) or ("warlock", 18) =>
            [("eldritch-invocations", "OptionSet", 1)],
        ("sorcerer", 3) => [("metamagic", "OptionSet", 2)],
        ("sorcerer", 10) or ("sorcerer", 17) => [("metamagic", "OptionSet", 1)],
        ("ranger", 6) => [("favored-enemy", "OptionSet", 1), ("natural-explorer", "OptionSet", 1)],
        ("ranger", 10) => [("natural-explorer", "OptionSet", 1)],
        ("ranger", 14) => [("favored-enemy", "OptionSet", 1)],
        ("wizard", 18) => [("spell-mastery", "Custom", 2)],
        ("wizard", 20) => [("signature-spells", "Custom", 2)],
        _ => [],
    };

    private static int? MysticArcanumLevel(string classIndex, int level) =>
        classIndex == "warlock" ? level switch { 11 => 6, 13 => 7, 15 => 8, 17 => 9, _ => null } : null;

    [Theory]
    [InlineData("barbarian")]
    [InlineData("bard")]
    [InlineData("cleric")]
    [InlineData("druid")]
    [InlineData("fighter")]
    [InlineData("monk")]
    [InlineData("paladin")]
    [InlineData("ranger")]
    [InlineData("rogue")]
    [InlineData("sorcerer")]
    [InlineData("warlock")]
    [InlineData("wizard")]
    public async Task Every_level_asks_for_what_the_srd_grants(string classIndex)
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var levels = (await s.Player.Client.GetFromJsonAsync<ClassDetailDto>($"/api/v1/catalog/classes/{classIndex}"))!.Levels
            .ToDictionary(l => l.Level);
        var hero = await CreateAsync(s, classIndex);
        var problems = new List<string>();
        var subclassAsked = false;
        string? subclass = null;

        for (var level = 2; level <= 20; level++)
        {
            await GrantAsync(s, hero.Id);
            var plan = await PlanAsync(s.Player, hero.Id);
            Assert.Equal((classIndex, level), (plan.ClassIndex, plan.ClassLevel));

            void Check(string key, string kind, int choose, string? subclassIndex = null)
            {
                var choice = plan.Choices.FirstOrDefault(c => c.Key == key && c.Kind == kind && c.SubclassIndex == subclassIndex);
                if (choice is null)
                {
                    problems.Add($"{classIndex} {level}: falta la elección {key} ({kind}, {choose}).");
                }
                else if (choice.Choose != choose || choice.Required != choose)
                {
                    problems.Add($"{classIndex} {level}: {key} ofrece {choice.Choose} y exige {choice.Required}; el SRD da {choose}. {choice.Warning}");
                }
            }

            if (AsiLevels(classIndex).Contains(level))
            {
                Check("asi", "AsiOrFeat", 1);
            }

            if (level >= SubclassLevel(classIndex) && !subclassAsked)
            {
                // Characters created without a subclass (level-1 subclasses included) get it at the next level-up.
                Check("subclass", "Subclass", 1);
                subclassAsked = true;
                subclass = plan.Choices.FirstOrDefault(c => c.Kind == "Subclass")?.Options.FirstOrDefault()?.Index;
            }

            var cantrips = (levels[level].CantripsKnown ?? 0) - (levels[level - 1].CantripsKnown ?? 0);
            if (cantrips > 0)
            {
                Check("cantrips", "CantripsKnown", cantrips);
            }

            var spells = (levels[level].SpellsKnown ?? 0) - (levels[level - 1].SpellsKnown ?? 0);
            if (spells > 0)
            {
                var asked = plan.Choices.Where(c => c.SubclassIndex is null && c.Kind == "SpellsKnown" && c.Key is "spells-known" or "magical-secrets").ToList();
                if (asked.Sum(c => c.Required) != spells || asked.Any(c => c.Required != c.Choose))
                {
                    problems.Add($"{classIndex} {level}: conjuros conocidos pedidos {asked.Sum(c => c.Required)} (SRD +{spells}).");
                }
            }

            if (classIndex == "wizard")
            {
                Check("spellbook", "SpellbookSpells", 2);
            }

            if (MysticArcanumLevel(classIndex, level) is { } arcanum)
            {
                Check("mystic-arcanum", "SpellsKnown", 1);
                var options = plan.Choices.First(c => c.Key == "mystic-arcanum").Options;
                Assert.All(options, o => Assert.Equal(arcanum, o.SpellLevel));
            }

            foreach (var (key, kind, choose) in Expected(classIndex, level))
            {
                Check(key, kind, choose);
            }

            // Every choice of the level must be answerable: as many eligible options as it asks for.
            foreach (var choice in plan.Choices.Where(c => c.Choose > 0 && (c.SubclassIndex is null || c.SubclassIndex == subclass)))
            {
                if (choice.Required < choice.Choose)
                {
                    problems.Add($"{classIndex} {level}: {choice.Key} exige {choice.Required} de {choice.Choose}: {choice.Warning}");
                }
            }

            hero = await ApplyAsync(s.Player, hero.Id, new
            {
                classIndex,
                hitPointsRolled = 1,
                choices = Answers(plan, hero, subclass),
            });
            Assert.Equal(level, hero.Classes.Single().Level);
            subclass = hero.Classes.Single().SubclassIndex;
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
        Assert.Equal(20, hero.Classes.Sum(c => c.Level));
    }

    [Theory]
    [InlineData("bard", 2, 4, "spells-known")]
    [InlineData("cleric", 3, 0, null)]
    [InlineData("druid", 2, 0, null)]
    [InlineData("sorcerer", 4, 2, "spells-known")]
    [InlineData("warlock", 2, 2, "spells-known")]
    [InlineData("wizard", 3, 6, "spellbook")]
    public async Task Multiclassing_into_a_caster_asks_for_its_level_1_cantrips_and_spells(string classIndex, int cantrips, int spells, string? spellsKey)
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await CreateAsync(s, "fighter", scores: new { str = 16, dex = 14, con = 14, @int = 14, wis = 14, cha = 14 });
        await GrantAsync(s, hero.Id);

        var plan = await PlanAsync(s.Player, hero.Id, classIndex);

        Assert.Equal((classIndex, 1), (plan.ClassIndex, plan.ClassLevel));
        var cantripChoice = Assert.Single(plan.Choices, c => c.Key == "cantrips");
        Assert.Equal(("CantripsKnown", cantrips, cantrips), (cantripChoice.Kind, cantripChoice.Choose, cantripChoice.Required));
        Assert.All(cantripChoice.Options, o => Assert.Equal(0, o.SpellLevel));
        Assert.NotEmpty(cantripChoice.Options);
        if (spellsKey is null)
        {
            Assert.DoesNotContain(plan.Choices, c => c.Kind is "SpellsKnown" or "SpellbookSpells");
        }
        else
        {
            var spellChoice = Assert.Single(plan.Choices, c => c.Key == spellsKey);
            Assert.Equal((spells, spells), (spellChoice.Choose, spellChoice.Required));
            Assert.All(spellChoice.Options, o => Assert.Equal(1, o.SpellLevel));
        }

        var after = await ApplyAsync(s.Player, hero.Id, new { classIndex, hitPointsRolled = 1, choices = Answers(plan, hero, null) });

        Assert.Equal(cantrips, after.Spells.Count(sp => sp.ClassIndex == classIndex && sp.SpellLevel == 0));
        Assert.Equal(spells, after.Spells.Count(sp => sp.ClassIndex == classIndex && sp.SpellLevel > 0));
    }

    [Fact]
    public async Task A_sorcerer_that_takes_its_origin_late_also_picks_the_draconic_ancestor()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await CreateAsync(s, "sorcerer");
        await GrantAsync(s, hero.Id);

        var plan = await PlanAsync(s.Player, hero.Id);

        var subclass = Assert.Single(plan.Choices, c => c.Kind == "Subclass");
        Assert.Contains(subclass.Options, o => o.Index == "draconic");
        var ancestor = Assert.Single(plan.Choices, c => c.Key == "dragon-ancestor");
        Assert.Equal(("draconic", 1, 1), (ancestor.SubclassIndex, ancestor.Choose, ancestor.Required));
    }

    [Fact]
    public async Task A_draconic_sorcerer_created_with_its_origin_picks_the_ancestor_at_the_next_level_and_only_once()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, "sorcerer");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "sorcerer", subclassIndex = "draconic", level = 1 } },
            baseAbilities = new { str = 10, dex = 10, con = 10, @int = 10, wis = 10, cha = 16 },
            applyRacialBonuses = false,
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null)).StatusCode);
        await GrantAsync(s, character.Id);
        var hero = await s.Player.GetCharacterAsync(character.Id);

        var plan = await PlanAsync(s.Player, hero.Id);

        Assert.DoesNotContain(plan.Choices, c => c.Kind == "Subclass");
        var ancestor = Assert.Single(plan.Choices, c => c.Key == "dragon-ancestor");
        Assert.Equal(("draconic", 1, 1), (ancestor.SubclassIndex, ancestor.Choose, ancestor.Required));

        hero = await ApplyAsync(s.Player, hero.Id, new { hitPointsRolled = 1, choices = Answers(plan, hero, "draconic") });
        Assert.Contains(hero.Choices, c => c.Key == "dragon-ancestor");

        await GrantAsync(s, hero.Id);
        Assert.DoesNotContain((await PlanAsync(s.Player, hero.Id)).Choices, c => c.Key == "dragon-ancestor");
    }

    [Fact]
    public async Task A_choice_without_eligible_options_stays_in_the_plan_with_a_warning()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        // A rogue without skill proficiencies (nor thieves' tools) has nothing to take expertise in at level 6.
        var hero = await CreateAsync(s, "rogue", level: 5, skills: []);
        await GrantAsync(s, hero.Id);

        var plan = await PlanAsync(s.Player, hero.Id);

        var expertise = Assert.Single(plan.Choices, c => c.Key == "expertise");
        Assert.Equal((2, 0), (expertise.Choose, expertise.Required));
        Assert.Empty(expertise.Options);
        Assert.NotNull(expertise.Warning);
        Assert.Contains("ninguna", expertise.Warning, StringComparison.OrdinalIgnoreCase);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    /// <summary>Answers every visible choice with its first eligible options (an ASI as +1 to the two lowest abilities).</summary>
    private static object[] Answers(LevelUpPlanDto plan, CharacterDetailDto hero, string? subclass)
    {
        var chosenSubclass = plan.Choices.FirstOrDefault(c => c.Kind == "Subclass")?.Options.FirstOrDefault()?.Index ?? subclass;
        var answers = new List<object>();
        foreach (var choice in plan.Choices.Where(c => c.Required > 0 && (c.SubclassIndex is null || c.SubclassIndex == chosenSubclass)))
        {
            if (choice.Kind == "AsiOrFeat")
            {
                var lowest = hero.Sheet.Abilities.Where(a => a.Value.Score < 20).OrderBy(a => a.Value.Score).ThenBy(a => a.Key).Take(2).Select(a => a.Key).ToList();
                answers.Add(new { key = choice.Key, selected = new { asi = lowest.ToDictionary(a => a, _ => 1) } });
            }
            else if (choice.FreeText)
            {
                answers.Add(new { key = choice.Key, selected = Enumerable.Range(1, choice.Required).Select(i => $"Texto {i}").ToArray() });
            }
            else
            {
                // Spells: the highest levels first, so the spellbook has 3rd-level spells for Signature Spells at 20.
                var picks = choice.Options.Where(o => o.Eligible).OrderByDescending(o => o.SpellLevel ?? 0).Take(choice.Required).Select(o => o.Index).ToArray();
                answers.Add(new { key = choice.Key, selected = picks });
            }
        }

        return answers.ToArray();
    }

    private static async Task<CharacterDetailDto> CreateAsync(CampaignScenario s, string classIndex, int level = 1, object? scores = null, string[]? skills = null)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, classIndex);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex, level } },
            baseAbilities = scores ?? new { str = 10, dex = 10, con = 10, @int = 10, wis = 10, cha = 10 },
            applyRacialBonuses = false,
            proficiencies = (skills ?? Skills).Select(key => new { type = "Skill", key, expertise = false }).ToArray(),
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null)).StatusCode);
        return await s.Player.GetCharacterAsync(character.Id);
    }

    private static async Task GrantAsync(CampaignScenario s, Guid characterId)
    {
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { characterId } });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
    }

    private static async Task<LevelUpPlanDto> PlanAsync(SignedInUser actor, Guid id, string? classIndex = null)
    {
        var url = $"{ItemTestHelpers.CharacterUrl(id)}/level-up";
        var response = await actor.Client.GetAsync(classIndex is null ? url : $"{url}?classIndex={classIndex}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LevelUpPlanDto>())!;
    }

    private static async Task<CharacterDetailDto> ApplyAsync(SignedInUser actor, Guid id, object body)
    {
        var response = await actor.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(id)}/level-up", body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }
}
