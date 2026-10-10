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
using OpenTrpg.Systems.Dnd5e.Application.Campaigns;
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

    // ---- H20/H10: options of other sets and ability improvements that also offer options ---------------

    [Fact]
    public async Task A_set_choice_may_reuse_options_of_other_sets()
    {
        var s = await ScenarioAsync();
        var herald = I("heraldo");
        var hero = await DraftAsync(s, herald, 1, new { str = 14, dex = 12, con = 14, @int = 10, wis = 10, cha = 14 });
        await GrantAsync(s, hero.Id);

        var tactics = (await PlanAsync(s.Player, hero.Id, herald)).Choices.Single(c => c.Key == "tacticas");
        Assert.Equivalent(new[] { I("tactica-niebla"), "fighting-style-defense", I("senal-basica") }, tactics.Options.Select(o => o.Index).ToArray());
        Assert.Equal("fighting-styles", tactics.Options.Single(o => o.Index == "fighting-style-defense").SetId);

        hero = await ApplyAsync(s.Player, hero.Id, new { classIndex = herald, hitPointsRolled = 6, choices = new[] { new { key = "tacticas", selected = new[] { "fighting-style-defense" } } } });

        // The reused fighting style is recorded as a pick of the choice and is not offered again.
        Assert.Contains(hero.Classes, c => c.ClassIndex == herald && c.Level == 2);
        var picked = hero.Choices.Single(c => c.Key == "tacticas");
        Assert.Equal("fighting-style-defense", Assert.Single(picked.Selected).Index);
    }

    [Fact]
    public async Task An_ability_improvement_may_take_an_option_of_another_set()
    {
        var s = await ScenarioAsync();
        var keeper = I("guardian");
        var hero = await DraftAsync(s, keeper, 3, new { str = 14, dex = 12, con = 14, @int = 10, wis = 14, cha = 8 }, I("orden-llama"));
        await GrantAsync(s, hero.Id);

        var plan = await PlanAsync(s.Player, hero.Id, keeper);
        var asi = plan.Choices.Single(c => c.Key == "asi");
        Assert.Equal("AsiOrFeat", asi.Kind);
        Assert.Equal([I("senales")], asi.AlsoFrom);
        Assert.Contains(asi.Options, o => o.Index == I("senal-basica") && o.SetId == I("senales") && o.Eligible);
        Assert.Contains(asi.Options, o => o.SetId == "feats");

        hero = await ApplyAsync(s.Player, hero.Id, new { classIndex = keeper, hitPointsRolled = 6, choices = new[] { new { key = "asi", selected = (object)new { feat = I("senal-basica") } } } });

        // The signal counts as an option the character has: the double signal becomes eligible later.
        await GrantAsync(s, hero.Id);
        var fifth = await PlanAsync(s.Player, hero.Id, keeper);
        var signals = fifth.Choices.Single(c => c.Key == "senales-5").Options;
        Assert.DoesNotContain(signals, o => o.Index == I("senal-basica"));
        Assert.True(signals.Single(o => o.Index == I("senal-doble")).Eligible);
    }

    [Fact]
    public async Task Also_from_is_only_accepted_by_improvements_and_set_choices()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed(Build(), "vacio-malo-h10");
        var choices = pack["classes"]![1]!["levelChoices"]!.AsArray();
        choices[0]!["kind"] = "Skill";
        choices[0]!["setId"] = null;
        choices[0]!["alsoFrom"] = new JsonArray("vacio-malo-h10-senales");
        choices[3]!["alsoFrom"] = new JsonArray("vacio-malo-h10-no-existe");
        pack["classes"]![0]!["levelChoices"]![0]!["from"] = new JsonArray("vacio-malo-h10-ninguna");

        var errors = await ImportErrorsAsync(admin, pack);
        var all = string.Join("\n", errors);

        Assert.True(errors.Any(e => e.StartsWith("classes[1].levelChoices[0].alsoFrom:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("classes[1].levelChoices[3].alsoFrom[0]:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("classes[0].levelChoices[0].from[0]:", StringComparison.Ordinal)), all);
    }

    // ---- H11: options granted without spending a choice -------------------------------------------------

    [Fact]
    public async Task Subclass_levels_and_options_grant_options_without_spending_a_choice()
    {
        var s = await ScenarioAsync();
        var keeper = I("guardian");
        var hero = await DraftAsync(s, keeper, 1, new { str = 14, dex = 12, con = 14, @int = 10, wis = 14, cha = 8 });
        var choose = new Dictionary<string, string> { ["senales"] = I("senal-basica"), ["senales-5"] = I("senal-doble") };
        object? Pick(LevelUpChoiceDto choice) =>
            choice.Kind == "Subclass" ? new { key = choice.Key, selected = new[] { I("orden-llama") } }
            : choose.TryGetValue(choice.Key, out var index) ? new { key = choice.Key, selected = new[] { index } }
            : null;

        for (var level = 2; level <= 5; level++)
        {
            await GrantAsync(s, hero.Id);
            var plan = await PlanAsync(s.Player, hero.Id, keeper);
            if (level == 4)
            {
                // The granted lantern is not offered again.
                Assert.DoesNotContain(plan.Choices.Single(c => c.Key == "asi").Options, o => o.Index == I("farolillo"));
            }

            hero = await ApplyAsync(s.Player, hero.Id, new { classIndex = keeper, hitPointsRolled = 6, choices = Answers(plan, hero, Pick) });
            var granted = hero.Choices.Where(c => c.Key == "granted").SelectMany(c => c.Selected).Select(i => i.Index).ToList();
            Assert.Equal(level >= 3, granted.Contains(I("farolillo")));
            Assert.Equal(level >= 5, granted.Contains(I("destello")));
        }

        Assert.Empty(hero.InvalidChoices);
    }

    [Fact]
    public async Task Races_cannot_grant_options()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed(Build(), "vacio-malo-h11");
        pack["races"] = new JsonArray(new JsonObject
        {
            ["index"] = "vacio-malo-h11-sombra",
            ["name"] = "Sombra",
            ["speed"] = 30,
            ["size"] = "Medium",
            ["grants"] = new JsonObject { ["options"] = new JsonArray("vacio-malo-h11-farolillo") },
        });
        pack["optionSets"]![0]!["options"]![0]!["grants"] = new JsonObject { ["options"] = new JsonArray("vacio-malo-h11-no-existe") };

        var errors = await ImportErrorsAsync(admin, pack);
        var all = string.Join("\n", errors);

        Assert.True(errors.Any(e => e.StartsWith("races[0].grants.options[0]:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("optionSets[0].options[0].grants.options[0]:", StringComparison.Ordinal)), all);
    }

    // ---- H12/H16: choices inside options and feats, and choices options add at later levels --------------

    [Fact]
    public async Task Options_and_feats_ask_for_their_own_choices_and_options_add_choices_at_later_levels()
    {
        var s = await ScenarioAsync();
        var keeper = I("guardian");
        var choir = I("senal-coro");
        var hero = await DraftAsync(s, keeper, 1, new { str = 14, dex = 12, con = 14, @int = 10, wis = 14, cha = 8 });

        // Level 2: the choir signal asks for a tactic and a skill, only when it is picked.
        await GrantAsync(s, hero.Id);
        var plan = await PlanAsync(s.Player, hero.Id, keeper);
        var tactic = plan.Choices.Single(c => c.Key == $"{choir}/tactica");
        Assert.Equal(("senales", choir), (tactic.Requires!.ChoiceKey, tactic.Requires.Index));
        Assert.Contains(tactic.Options, o => o.Index == I("tactica-niebla"));
        var skill = plan.Choices.Single(c => c.Key == $"{choir}/habilidad");
        Assert.Equal([ "arcana", "stealth" ], skill.Options.Select(o => o.Index).Order(StringComparer.Ordinal).ToArray());

        // Picking the choir signal without answering its choices is refused.
        var other = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/level-up", new
        {
            classIndex = keeper,
            hitPointsRolled = 6,
            choices = new object[] { new { key = "senales", selected = new[] { choir } } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, other.StatusCode);

        hero = await ApplyAsync(s.Player, hero.Id, new
        {
            classIndex = keeper,
            hitPointsRolled = 6,
            choices = new object[]
            {
                new { key = "senales", selected = new[] { choir } },
                new { key = $"{choir}/tactica", selected = new[] { I("tactica-oculta") } },
                new { key = $"{choir}/habilidad", selected = new[] { "stealth" } },
            },
        });
        Assert.Contains(hero.Proficiencies, p => p.Type == "Skill" && p.Key.EndsWith("stealth", StringComparison.Ordinal));
        Assert.Contains(hero.Choices, c => c.Key == $"{choir}/tactica" && c.Selected.Single().Index == I("tactica-oculta"));

        // Level 3 (order) and 4: the feat of the improvement asks for a saving throw and an exotic weapon.
        await GrantAsync(s, hero.Id);
        var third = await PlanAsync(s.Player, hero.Id, keeper);
        hero = await ApplyAsync(s.Player, hero.Id, new { classIndex = keeper, hitPointsRolled = 6, choices = new[] { new { key = third.Choices.Single(c => c.Kind == "Subclass").Key, selected = new[] { I("orden-llama") } } } });
        await GrantAsync(s, hero.Id);
        var fourth = await PlanAsync(s.Player, hero.Id, keeper);
        var weapon = fourth.Choices.Single(c => c.Key == $"{I("vigia-del-eco")}/arma");
        Assert.Equal("Weapon", weapon.Kind);
        Assert.Equal([I("guadana-doble")], weapon.Options.Select(o => o.Index).ToArray());
        var save = fourth.Choices.Single(c => c.Key == $"{I("vigia-del-eco")}/salvacion");
        Assert.Equal(6, save.Options.Count);
        hero = await ApplyAsync(s.Player, hero.Id, new
        {
            classIndex = keeper,
            hitPointsRolled = 6,
            choices = new object[]
            {
                new { key = "asi", selected = new { feat = I("vigia-del-eco") } },
                new { key = $"{I("vigia-del-eco")}/salvacion", selected = new[] { "wis" } },
                new { key = $"{I("vigia-del-eco")}/arma", selected = new[] { I("guadana-doble") } },
            },
        });
        Assert.Contains(hero.Proficiencies, p => p.Type == "SavingThrow" && p.Key == "wis");
        Assert.Contains(hero.Proficiencies, p => p.Type == "Weapon" && p.Key == I("guadana-doble"));

        // Level 5: the choir signal adds its own choice.
        await GrantAsync(s, hero.Id);
        var fifth = await PlanAsync(s.Player, hero.Id, keeper);
        var voice = fifth.Choices.Single(c => c.Key == $"{choir}/coro-5");
        Assert.Null(voice.Requires);
        Assert.Equal(1, voice.Required);
    }

    [Fact]
    public async Task Option_choices_reject_levels_unknown_kinds_and_long_keys()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed(Build(), "vacio-malo-h12");
        var choir = pack["optionSets"]![0]!["options"]!.AsArray().Last()!;
        choir["choices"]![0]!["level"] = 3;
        choir["choices"]![1]!["kind"] = "Subclass";
        choir["levelChoices"]![0]!["key"] = new string('k', 90);
        pack["classes"]![0]!["levelChoices"]![0]!["kind"] = "Weapon";

        var errors = await ImportErrorsAsync(admin, pack);
        var all = string.Join("\n", errors);

        Assert.True(errors.Any(e => e.StartsWith("optionSets[0].options[9].choices[0].level:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("optionSets[0].options[9].choices[1].kind:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("optionSets[0].options[9].levelChoices[0].key:", StringComparison.Ordinal)), all);
        Assert.True(errors.Any(e => e.StartsWith("classes[0].levelChoices[0].kind:", StringComparison.Ordinal)), all);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    // ---- H13/H13b: heritage feats, creation-only feats, subrace spells over race spells ----------------------

    [Fact]
    public async Task Heritage_feats_come_from_their_set_and_creation_only_feats_stay_out_of_improvements()
    {
        var s = await ScenarioAsync();
        var draft = await OriginDraftAsync(s, I("medio-vacio"), null, "fighter", 1);
        var choices = await GetAsync<OriginChoicesDto>(s.Player.Client, OriginUrl(draft.Id));
        var heritage = choices.Choices.Single(c => c.Key == "race.feat");
        Assert.Equal([I("herencia-ceniza"), I("herencia-niebla")], heritage.Options.Select(o => o.Index).ToList());

        var saved = await s.Player.Client.PutAsJsonAsync(OriginUrl(draft.Id), new { choices = new object[] { new { key = "race.feat", selected = new { feat = I("herencia-niebla") } } } });
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var hero = await s.Player.GetCharacterAsync(draft.Id);
        Assert.Contains(hero.Proficiencies, p => p is { Type: "Skill", Key: "stealth" });
        Assert.Contains(hero.Feats, f => f.Index == I("herencia-niebla"));

        // At level 4 the improvement offers the pack's normal feat but neither the creation-only nor the starter one.
        var fighter = await DraftAsync(s, "fighter", 3, new { str = 15, dex = 12, con = 14, @int = 10, wis = 10, cha = 8 });
        await GrantAsync(s, fighter.Id);
        var plan = await PlanAsync(s.Player, fighter.Id, "fighter");
        var feats = plan.Choices.Single(c => c.Kind == "AsiOrFeat").Options.Select(o => o.Index).ToList();
        Assert.Contains(I("vigia-del-eco"), feats);
        Assert.DoesNotContain(I("lazo-de-sangre"), feats);
        Assert.DoesNotContain(I("chispa-temprana"), feats);
    }

    [Fact]
    public async Task A_subrace_spell_replaces_the_same_spell_of_the_race()
    {
        var s = await ScenarioAsync();
        var plain = await OriginDraftAsync(s, I("eco"), null, "fighter", 1);
        Assert.DoesNotContain(plain.Spells, sp => sp.SpellIndex == "cure-wounds");

        var early = await OriginDraftAsync(s, I("eco"), I("eco-temprano"), "fighter", 1);
        Assert.Contains(early.Spells, sp => sp is { SpellIndex: "cure-wounds", ClassIndex: "race", AlwaysPrepared: true });
    }

    [Fact]
    public async Task Heritage_sets_and_starter_feats_are_validated()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed(Build(), "vacio-malo-h13");
        pack["races"]![0]!["choices"]!["feats"]!["setId"] = "vacio-malo-h13-inexistente";
        var feats = pack["feats"]!.AsArray();
        feats[^1]!["creationOnly"] = false;

        var errors = await ImportErrorsAsync(admin, pack);

        Assert.Contains(errors, e => e.StartsWith("races[0].choices.feats.setId:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"feats[{feats.Count - 1}].creationOnly:", StringComparison.Ordinal));
    }

    // ---- H15: starter feats ---------------------------------------------------------------------------------

    [Fact]
    public async Task Starter_feat_is_an_optional_level_one_choice_when_the_campaign_enables_it()
    {
        var s = await ScenarioAsync();
        var rulesUrl = $"/api/v1/systems/dnd5e/campaigns/{s.CampaignId}/rules";
        var rules = await GetAsync<CampaignRulesDto>(s.Player.Client, rulesUrl);
        Assert.Equal(new CampaignRulesDto(false, true), rules);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.PutAsJsonAsync(rulesUrl, new { starterFeatsEnabled = true })).StatusCode);

        var draft = await OriginDraftAsync(s, "human", null, "fighter", 1);
        var starter = new { key = "campaign.starterFeat", selected = new { feat = I("chispa-temprana") } };
        Assert.DoesNotContain((await GetAsync<OriginChoicesDto>(s.Player.Client, OriginUrl(draft.Id))).Choices, c => c.Key == "campaign.starterFeat");
        var refused = await s.Player.Client.PutAsJsonAsync(OriginUrl(draft.Id), new { choices = new object[] { starter } });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("starter-feat-not-allowed", (await refused.ReadProblemAsync()).GetProperty("code").GetString());

        var enabled = await s.Dm.Client.PutAsJsonAsync(rulesUrl, new { starterFeatsEnabled = true });
        Assert.Equal(new CampaignRulesDto(true, true), await enabled.Content.ReadFromJsonAsync<CampaignRulesDto>());

        var choices = await GetAsync<OriginChoicesDto>(s.Player.Client, OriginUrl(draft.Id));
        var choice = choices.Choices.Single(c => c.Key == "campaign.starterFeat");
        Assert.Equal(("Dote inicial", "Feat", "campaign", 0), (choice.Name, choice.Kind, choice.Source, choice.Required));
        Assert.Equal([I("chispa-temprana")], choice.Options.Select(o => o.Index).ToList());
        Assert.True(choices.Complete);

        var saved = await s.Player.Client.PutAsJsonAsync(OriginUrl(draft.Id), new { choices = new object[] { starter } });
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var hero = await s.Player.GetCharacterAsync(draft.Id);
        Assert.Contains(hero.Proficiencies, p => p is { Type: "Skill", Key: "perception" });
        Assert.Contains(hero.Feats, f => f.Index == I("chispa-temprana"));

        // Only at level 1.
        var veteran = await OriginDraftAsync(s, "human", null, "fighter", 2);
        Assert.DoesNotContain((await GetAsync<OriginChoicesDto>(s.Player.Client, OriginUrl(veteran.Id))).Choices, c => c.Key == "campaign.starterFeat");
        var late = await s.Player.Client.PutAsJsonAsync(OriginUrl(veteran.Id), new { choices = new object[] { starter } });
        Assert.Equal("starter-feat-not-allowed", (await late.ReadProblemAsync()).GetProperty("code").GetString());

        // Without starter feats in the active packs the switch has nothing to offer.
        var other = await factory.CreateCampaignScenarioAsync();
        Assert.Equal(new CampaignRulesDto(false, false), await GetAsync<CampaignRulesDto>(other.Dm.Client, $"/api/v1/systems/dnd5e/campaigns/{other.CampaignId}/rules"));
    }

    // ---- H01-H04: casting without slots, rituals only, subclass pact or table, subclass on a cantrip caster -------

    [Fact]
    public async Task A_hit_point_caster_has_no_slots_and_pays_each_spell_with_a_use_and_hit_points()
    {
        var s = await ScenarioAsync();
        var penitent = I("penitente");
        var hero = await DraftAsync(s, penitent, 1, new { str = 10, dex = 12, con = 14, @int = 10, wis = 16, cha = 8 });
        var casting = hero.Sheet.Spellcasting.Single(c => c.ClassIndex == penitent);
        Assert.Equal(("hitPoints", 1, 3, I("plegarias")), (casting.Progression, casting.MaxSpellLevel, casting.PreparedMax, casting.UsesResource));
        Assert.Equal([5], casting.HitPointCost);
        Assert.All(hero.SpellSlots, slot => Assert.Equal(0, slot.Max));
        Assert.Equal(2, hero.Resources.Single(r => r.Key == I("plegarias")).Max);
        Assert.Equal(10, hero.HitPointsCurrent);

        var url = $"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/class-actions/hit-point-cast";
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync(url, new { classIndex = penitent, spellLevel = 2 })).StatusCode);
        var cast = await s.Player.Client.PostAsJsonAsync(url, new { classIndex = penitent, spellLevel = 1 });
        Assert.True(cast.StatusCode == HttpStatusCode.OK, await cast.Content.ReadAsStringAsync());
        var result = (await cast.Content.ReadFromJsonAsync<HitPointCastResultDto>())!;
        Assert.Equal(5, result.HitPointsLost);
        Assert.Equal(5, result.Character.HitPointsCurrent);
        Assert.Equal(1, result.Character.Resources.Single(r => r.Key == I("plegarias")).Used);
        Assert.Equal(HttpStatusCode.OK, (await s.Player.Client.PostAsJsonAsync(url, new { classIndex = penitent, spellLevel = 1 })).StatusCode);
        // No uses left (and no hit points either).
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync(url, new { classIndex = penitent, spellLevel = 1 })).StatusCode);

        var veteran = await DraftAsync(s, penitent, 9, new { str = 10, dex = 12, con = 14, @int = 10, wis = 16, cha = 8 });
        var later = veteran.Sheet.Spellcasting.Single(c => c.ClassIndex == penitent);
        Assert.Equal((3, 7), (later.MaxSpellLevel, later.PreparedMax));
        Assert.Equal([5, 10, 20], later.HitPointCost);
    }

    [Fact]
    public async Task A_ritual_caster_learns_only_rituals_up_to_its_table_and_spells_it_treats_as_rituals()
    {
        var s = await ScenarioAsync();
        var scribe = I("escriba");
        var hero = await DraftAsync(s, scribe, 1, new { str = 10, dex = 12, con = 14, @int = 16, wis = 12, cha = 8 });
        var casting = hero.Sheet.Spellcasting.Single(c => c.ClassIndex == scribe);
        Assert.Equal(("ritualsOnly", 1, (int?)null), (casting.Progression, casting.MaxSpellLevel, casting.PreparedMax));

        await GrantAsync(s, hero.Id);
        var plan = await PlanAsync(s.Player, hero.Id, scribe);
        var rituals = plan.Choices.Single(c => c.Key == "rituales-2").Options.Select(o => o.Index).ToList();
        Assert.Contains("detect-magic", rituals);
        Assert.Contains("mage-armor", rituals);
        Assert.DoesNotContain("magic-missile", rituals);
        Assert.DoesNotContain("gentle-repose", rituals);

        var spell = await GetAsync<SpellDetailDto>(s.Player.Client, $"{CatalogUrl}/spells/mage-armor");
        Assert.Equal([scribe], spell.RitualForClasses);
    }

    [Fact]
    public async Task A_subclass_may_cast_with_pact_magic_or_its_own_table()
    {
        var s = await ScenarioAsync();
        var smith = I("forjador");
        var scores = new { str = 14, dex = 12, con = 14, @int = 16, wis = 10, cha = 8 };
        var pact = await DraftAsync(s, smith, 3, scores, I("taller-pacto"));
        Assert.Equal(2, pact.Sheet.PactSlotLevel);
        Assert.Equal("pact", pact.Sheet.Spellcasting.Single(c => c.ClassIndex == smith).Progression);
        Assert.All(pact.SpellSlots.Where(slot => slot.Level > 0), slot => Assert.Equal(0, slot.Max));

        var table = await DraftAsync(s, smith, 3, scores, I("taller-tabla"));
        Assert.Null(table.Sheet.PactSlotLevel);
        Assert.Equal(2, table.SpellSlots.Single(slot => slot.Level == 1).Max);
        var seventh = await DraftAsync(s, smith, 7, scores, I("taller-tabla"));
        Assert.Equal((0, 3), (seventh.SpellSlots.FirstOrDefault(slot => slot.Level == 1)?.Max ?? 0, seventh.SpellSlots.Single(slot => slot.Level == 2).Max));
    }

    [Fact]
    public async Task A_subclass_adds_slots_to_a_class_that_only_knows_cantrips()
    {
        var s = await ScenarioAsync();
        var lookout = I("vigia");
        var scores = new { str = 10, dex = 14, con = 14, @int = 16, wis = 10, cha = 8 };
        var novice = await DraftAsync(s, lookout, 1, scores);
        Assert.Equal(0, novice.Sheet.Spellcasting.Single(c => c.ClassIndex == lookout).MaxSpellLevel);

        var tower = await DraftAsync(s, lookout, 3, scores, I("torre-tercio"));
        var casting = tower.Sheet.Spellcasting.Single(c => c.ClassIndex == lookout);
        Assert.Equal(("int", 1, 3), (casting.Ability, casting.MaxSpellLevel, casting.SpellsKnownMax));
        Assert.Equal(2, tower.SpellSlots.Single(slot => slot.Level == 1).Max);
    }

    [Fact]
    public async Task Casting_without_slots_and_subclass_tables_are_validated()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed(Build(), "vacio-malo-h01");
        var classes = pack["classes"]!.AsArray();
        var penitent = classes.Single(c => c!["index"]!.GetValue<string>() == "vacio-malo-h01-penitente")!;
        penitent["spellcasting"]!.AsObject().Remove("maxSpellLevel");
        penitent["spellcasting"]!["usesResource"] = "vacio-malo-h01-inexistente";
        penitent["spellcasting"]!["preparedFormula"] = "mod:wis+suerte";
        var smith = classes.Single(c => c!["index"]!.GetValue<string>() == "vacio-malo-h01-forjador")!;
        smith["subclasses"]![1]!["spellcasting"]!.AsObject().Remove("slots");
        var penitentIndex = classes.IndexOf(penitent);
        var smithIndex = classes.IndexOf(smith);

        var errors = await ImportErrorsAsync(admin, pack);

        Assert.Contains(errors, e => e.StartsWith($"classes[{penitentIndex}].spellcasting.maxSpellLevel:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"classes[{penitentIndex}].spellcasting.usesResource:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"classes[{penitentIndex}].spellcasting.preparedFormula:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"classes[{smithIndex}].subclasses[1].spellcasting.slots:", StringComparison.Ordinal));
    }

    // ---- H14: auxiliary levels ---------------------------------------------------------------------------

    [Fact]
    public async Task Auxiliary_levels_are_taken_once_after_level_one_with_their_own_prerequisites()
    {
        var s = await ScenarioAsync();
        var veil = I("velo");
        var tamer = I("domador");
        var classes = await GetAsync<List<ClassSummaryDto>>(s.Player.Client, $"{CatalogUrl}/classes");
        Assert.True(classes.Single(c => c.Index == veil).Auxiliary);
        Assert.False(classes.Single(c => c.Index == "fighter").Auxiliary);

        // Never a starting class, and never more than one level.
        var draft = await s.Player.CreateCharacterAsync(s.CampaignId, "Auxiliar");
        var start = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(draft.Id)}/sheet", new { classes = new[] { new { classIndex = veil, level = 1 } } });
        Assert.Equal(HttpStatusCode.BadRequest, start.StatusCode);

        var fighter = await DraftAsync(s, "fighter", 1, new { str = 15, dex = 12, con = 14, @int = 10, wis = 10, cha = 8 });
        await GrantAsync(s, fighter.Id);
        var early = (await PlanAsync(s.Player, fighter.Id)).Classes;
        var tamerOption = early.Single(c => c.ClassIndex == tamer);
        Assert.True(tamerOption.Auxiliary);
        Assert.False(tamerOption.Allowed);
        Assert.Equal("Nivel 3 y haber escuchado un eco (texto ficticio).", tamerOption.PrerequisitesText);
        Assert.False(early.Single(c => c.ClassIndex == veil).Allowed);

        // A wizard 2 takes the veil: wizard 2 + an auxiliary caster level = the slots of a wizard 3.
        var wizard = await DraftAsync(s, "wizard", 2, new { str = 8, dex = 14, con = 14, @int = 16, wis = 12, cha = 10 });
        await GrantAsync(s, wizard.Id);
        var plan = await PlanAsync(s.Player, wizard.Id, veil);
        Assert.True(plan.Classes.Single(c => c.ClassIndex == veil).Allowed, plan.Classes.Single(c => c.ClassIndex == veil).Reason);
        wizard = await ApplyAsync(s.Player, wizard.Id, new { classIndex = veil, hitPointsRolled = 4, choices = Answers(plan, wizard) });
        Assert.Equal(3, wizard.Sheet.TotalLevel);
        Assert.Equal((4, 2), (wizard.SpellSlots.Single(slot => slot.Level == 1).Max, wizard.SpellSlots.Single(slot => slot.Level == 2).Max));
        Assert.Contains(wizard.Proficiencies, p => p is { Type: "Tool", Key: "Kit de ejemplo" });

        await GrantAsync(s, wizard.Id);
        var again = (await PlanAsync(s.Player, wizard.Id)).Classes.Single(c => c.ClassIndex == veil);
        Assert.Equal((false, "Un nivel auxiliar se toma una sola vez."), (again.Allowed, again.Reason));
    }

    [Fact]
    public async Task Auxiliary_levels_are_validated()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed(Build(), "vacio-malo-h14");
        var classes = pack["classes"]!.AsArray();
        var veil = classes.Single(c => c!["index"]!.GetValue<string>() == "vacio-malo-h14-velo")!;
        veil["levels"]!.AsArray().Add(new JsonObject { ["level"] = 2 });
        var herald = classes[0]!;
        herald["countsAsCasterLevel"] = true;

        var errors = await ImportErrorsAsync(admin, pack);

        Assert.Contains(errors, e => e.StartsWith($"classes[{classes.IndexOf(veil)}].levels:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("classes[0].countsAsCasterLevel:", StringComparison.Ordinal));
    }

    // ---- Flow: a hit-point caster from level 1 to 20 with an auxiliary level -------------------------------

    [Fact]
    public async Task A_hit_point_caster_reaches_level_twenty_through_the_api_taking_an_auxiliary_level()
    {
        var s = await ScenarioAsync();
        var penitent = I("penitente");
        var tamer = I("domador");
        var hero = await DraftAsync(s, penitent, 1, new { str = 10, dex = 12, con = 14, @int = 10, wis = 16, cha = 8 });
        for (var level = 2; level <= 20; level++)
        {
            await GrantAsync(s, hero.Id);
            var classIndex = level == 6 ? tamer : penitent;
            var plan = await PlanAsync(s.Player, hero.Id, classIndex);
            var selected = plan.Classes.Single(c => c.ClassIndex == classIndex);
            Assert.True(selected.Allowed, $"Nivel {level}: {selected.Reason}");
            hero = await ApplyAsync(s.Player, hero.Id, new { classIndex, hitPointsRolled = 4, choices = Answers(plan, hero) });
            Assert.Empty(hero.InvalidChoices);
        }

        Assert.Equal(20, hero.Sheet.TotalLevel);
        Assert.Equal([(penitent, 19), (tamer, 1)], hero.Classes.Select(c => (c.ClassIndex, c.Level)).ToList());
        var casting = hero.Sheet.Spellcasting.Single(c => c.ClassIndex == penitent);
        Assert.Equal(("hitPoints", 5), (casting.Progression, casting.MaxSpellLevel));
        Assert.Equal([5, 10, 20, 30, 45], casting.HitPointCost);
        Assert.Equal(8, hero.Resources.Single(r => r.Key == I("plegarias")).Max);
    }

    private static string OriginUrl(Guid id) => $"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/origin-choices";

    /// <summary>A draft (origin choices still open) of <paramref name="race"/> and <paramref name="classIndex"/> at <paramref name="level"/>.</summary>
    private static async Task<CharacterDetailDto> OriginDraftAsync(CampaignScenario s, string race, string? subrace, string classIndex, int level)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, race);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            raceIndex = race,
            subraceIndex = subrace,
            classes = new[] { new { classIndex, level } },
            baseAbilities = new { str = 14, dex = 12, con = 14, @int = 10, wis = 12, cha = 10 },
            applyRacialBonuses = false,
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        return await s.Player.GetCharacterAsync(character.Id);
    }

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
