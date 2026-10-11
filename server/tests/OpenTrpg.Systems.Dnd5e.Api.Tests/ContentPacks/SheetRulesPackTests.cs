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
using static OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks.SheetRulesPack;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the fictitious sheet rules pack of phase 35B2.</summary>
public sealed class SheetRulesPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Sheet gaps of the format 3 (phase 35B2) with the fictitious pack of <see cref="SheetRulesPack"/>: formula
/// modifiers, armor class formulas, defenses, critical ranges, class DCs, passives, ability maximum, size, death saves,
/// hit dice, lost saving throws, speeds, natural weapons and weapon rules.
/// </summary>
public class SheetRulesPackTests(SheetRulesPackApiFactory factory) : IClassFixture<SheetRulesPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string CatalogUrl = "/api/v1/systems/dnd5e/catalog";

    private static readonly SemaphoreSlim ImportLock = new(1, 1);

    private static readonly object LookoutScores = new { str = 10, dex = 14, con = 14, @int = 10, wis = 16, cha = 8 };

    // ---- H21, H22, H25, H26, H27, H29, H30b, H32: class features ----------------------------------------------

    [Fact]
    public async Task Class_features_change_the_sheet_and_explain_every_point()
    {
        var s = await ScenarioAsync();
        var lookout = I("vigia");
        var hero = await DraftAsync(s, lookout, 1, LookoutScores);
        var sheet = hero.Sheet;

        // H22: 10 + Dex 2 + Wis 3 beats the unarmored 10 + Dex 2, and the breakdown says which.
        Assert.Equal(15, sheet.ArmorClass);
        Assert.Contains(sheet.Breakdowns["armorClass"].Parts, p => p.Label == "Piel de corteza (nivel 1)" && p.Value == 10);
        Assert.Equal(sheet.ArmorClass, sheet.Breakdowns["armorClass"].Total);

        // H21: + character level to the hit points, labelled with the feature and the term.
        Assert.Contains(sheet.Breakdowns["hitPointsMax"].Parts, p => p.Label == "Aguante (nivel 1): nivel de personaje" && p.Value == 1);
        Assert.Equal(10 + 2 + 1, sheet.HitPointsMax);

        // H25: class save DC 8 + proficiency 2 + Wis 3.
        var dc = Assert.Single(sheet.ClassSaveDcs);
        Assert.Equal((lookout, "CD de ejemplo", "wis", 13), (dc.ClassIndex, dc.Name, dc.Ability, dc.Dc));
        Assert.Equal(13, sheet.Breakdowns[dc.BreakdownKey].Total);

        // H26: passive investigation 10 + Int 0 + 5.
        Assert.Equal(15, sheet.PassiveInvestigation);
        Assert.Contains(sheet.Breakdowns["passiveInvestigation"].Parts, p => p.Label == "Ojo certero (nivel 1)" && p.Value == 5);

        // H27, H29, H30b, H32.
        Assert.Equal(22, sheet.AbilityMaxScores["con"]);
        Assert.Equal(20, sheet.AbilityMaxScores["str"]);
        Assert.Equal(4, sheet.DeathSaveFailuresToDie);
        Assert.True(sheet.RecoversAllHitDice);
        Assert.Equal(30, sheet.Speeds["swim"]);
        Assert.Equal(20, sheet.Speeds["fly"]);
        Assert.False(sheet.Speeds.ContainsKey("climb"));
        Assert.Equal(30, sheet.Breakdowns["speed.swim"].Total);

        // Four failed death saves are allowed, five are not.
        var url = $"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/combat";
        Assert.Equal(HttpStatusCode.OK, (await s.Player.Client.PatchAsJsonAsync(url, new { deathSaveFailures = 4 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PatchAsJsonAsync(url, new { deathSaveFailures = 5 })).StatusCode);
    }

    [Fact]
    public async Task Armor_wins_over_the_formula_when_it_is_higher_and_the_formula_needs_no_armor()
    {
        var s = await ScenarioAsync();
        var hero = await DraftAsync(s, I("vigia"), 1, LookoutScores);
        var armor = await s.Dm.AddItemAsync(hero.Id, new { templateId = await s.Player.SrdItemIdAsync("Splint Armor"), quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(hero.Id, armor.Id, new { equipped = true })).StatusCode);

        var armored = await s.Player.GetCharacterAsync(hero.Id);

        Assert.Equal(17, armored.Sheet.ArmorClass);
        Assert.DoesNotContain(armored.Sheet.Breakdowns["armorClass"].Parts, p => p.Label.StartsWith("Piel de corteza", StringComparison.Ordinal));
    }

    // ---- H24: critical range ---------------------------------------------------------------------------------

    [Fact]
    public async Task Weapon_and_spell_attacks_crit_from_the_lowered_range()
    {
        var s = await ScenarioAsync();
        var hero = await DraftAsync(s, I("vigia"), 1, LookoutScores);
        var dagger = await s.Dm.AddItemAsync(hero.Id, new { templateId = await s.Player.SrdItemIdAsync("Dagger"), quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(hero.Id, dagger.Id, new { equipped = true })).StatusCode);
        var armed = await s.Player.GetCharacterAsync(hero.Id);
        var attack = armed.Combat.Attacks.Single(a => a.ItemId == dagger.Id);
        Assert.Equal((19, "Ojo certero (nivel 1)"), (attack.CriticalRange, attack.CriticalRangeSource));

        var sorcerer = await DraftAsync(s, "sorcerer", 1, new { str = 8, dex = 14, con = 14, @int = 10, wis = 10, cha = 16 }, I("chispa"));
        var casting = sorcerer.Sheet.Spellcasting.Single(c => c.ClassIndex == "sorcerer");

        // H25: +1 to the DC of the cantrips only, +2 to every spell attack of the class.
        Assert.Equal(13, casting.SaveDc);
        Assert.Equal(14, casting.CantripSaveDc);
        Assert.Equal(2 + 3 + 2, casting.AttackBonus);
        Assert.Contains(sorcerer.Sheet.Breakdowns["spellAttackBonus.sorcerer"].Parts, p => p.Label == "Chispa afilada (nivel 1)" && p.Value == 2);
        Assert.Contains(sorcerer.Sheet.Breakdowns["spellSaveDc.sorcerer.cantrip"].Parts, p => p.Label == "Chispa afilada (nivel 1)" && p.Value == 1);
        Assert.Equal(19, casting.CriticalRange);
        Assert.Equal(20, sorcerer.Combat.Attacks.First().CriticalRange);
    }

    // ---- H23, H28, H31c (race), H33: race grants -------------------------------------------------------------

    [Fact]
    public async Task A_race_grants_defenses_size_natural_weapons_and_takes_a_saving_throw_away()
    {
        var s = await ScenarioAsync();
        var hero = await DraftAsync(s, "fighter", 5, new { str = 14, dex = 16, con = 12, @int = 10, wis = 10, cha = 10 }, race: I("escamado"));
        var sheet = hero.Sheet;

        Assert.Contains(sheet.Resistances, r => (r.DamageType, r.Label) == ("fire", "Escamado"));
        Assert.Equal(("poison", "Escamado"), (Assert.Single(sheet.Immunities).Type, sheet.Immunities[0].Label));
        Assert.Equal("charmed", Assert.Single(sheet.ConditionImmunities).Type);
        Assert.Equal("small", sheet.Size);
        Assert.Equal("str", Assert.Single(sheet.LostSavingThrows).Type);
        Assert.False(sheet.SavingThrows["str"].Proficient);
        Assert.True(sheet.SavingThrows["con"].Proficient);

        // H33: finesse claws with the higher of Str and Dex, damage by character level.
        var claws = Assert.Single(hero.Combat.Attacks, a => a.Natural);
        Assert.Equal(("Garras de ejemplo", "1d6+3", "slashing"), (claws.Name, claws.Damage, claws.DamageType));
        Assert.Equal("Escamado", claws.Notes);
        Assert.Contains(claws.AttackBreakdown.Parts, p => p.Source == "proficiency");

        // H28: a heavy weapon warns a small character.
        var mace = await s.Dm.AddItemAsync(hero.Id, new { templateId = await s.Player.SrdItemIdAsync("Maza doble de ejemplo"), quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(hero.Id, mace.Id, new { equipped = true })).StatusCode);
        var armed = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Contains("Desventaja: arma pesada y tamaño pequeño.", armed.Combat.Attacks.First(a => a.ItemId == mace.Id).Warnings);
    }

    // ---- H31c (to choose) and H23 (feat): starter feat ------------------------------------------------------------

    [Fact]
    public async Task A_starter_feat_asks_which_saving_throw_is_lost()
    {
        var s = await ScenarioAsync();
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PutAsJsonAsync($"/api/v1/systems/dnd5e/campaigns/{s.CampaignId}/rules", new { starterFeatsEnabled = true })).StatusCode);
        var draft = await s.Player.CreateCharacterAsync(s.CampaignId, "Renuncia");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(draft.Id)}/sheet", new
        {
            raceIndex = "human",
            classes = new[] { new { classIndex = "fighter", level = 1 } },
            baseAbilities = new { str = 14, dex = 12, con = 14, @int = 10, wis = 12, cha = 10 },
            applyRacialBonuses = false,
            proficiencies = Saves("fighter"),
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        var url = $"{ItemTestHelpers.Dnd5eCharacterUrl(draft.Id)}/origin-choices";

        var choices = await GetAsync<OriginChoicesDto>(s.Player.Client, url);
        var feat = choices.Choices.Single(c => c.Key == "campaign.starterFeat").Options.Single(o => o.Index == I("renuncia"));
        Assert.Equal((1, -1), (feat.RemoveSavingThrows!.Choose, feat.SkillChoicesDelta));
        Assert.Equal(["str", "con"], feat.RemoveSavingThrows.From);

        var missing = await s.Player.Client.PutAsJsonAsync(url, new { choices = new object[] { new { key = "campaign.starterFeat", selected = new { feat = I("renuncia") } } } });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var wrong = await s.Player.Client.PutAsJsonAsync(url, new { choices = new object[] { new { key = "campaign.starterFeat", selected = new { feat = I("renuncia"), removedSavingThrows = new[] { "wis" } } } } });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var saved = await s.Player.Client.PutAsJsonAsync(url, new { choices = new object[] { new { key = "campaign.starterFeat", selected = new { feat = I("renuncia"), removedSavingThrows = new[] { "con" } } } } });
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var answered = (await saved.Content.ReadFromJsonAsync<OriginChoicesDto>())!.Choices.Single(c => c.Key == "campaign.starterFeat");
        Assert.Equal(["con"], answered.RemovedSavingThrows);

        var hero = await s.Player.GetCharacterAsync(draft.Id);
        Assert.False(hero.Sheet.SavingThrows["con"].Proficient);
        Assert.True(hero.Sheet.SavingThrows["str"].Proficient);
        Assert.Equal("Renuncia de ejemplo (dote inicial)", Assert.Single(hero.Sheet.LostSavingThrows).Label);
        Assert.Contains(hero.Sheet.Resistances, r => r.DamageType == "cold");
    }

    // ---- H23: resistance to choose ----------------------------------------------------------------------------

    [Fact]
    public async Task A_feat_resistance_choice_offers_damage_types_and_the_sheet_shows_the_pick()
    {
        var s = await ScenarioAsync();
        var hero = await DraftAsync(s, "fighter", 3, new { str = 15, dex = 12, con = 14, @int = 10, wis = 10, cha = 8 }, "champion");
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/systems/dnd5e/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { hero.Id } });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        var levelUp = $"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/level-up";
        var plan = await GetAsync<LevelUpPlanDto>(s.Player.Client, $"{levelUp}?classIndex=fighter");
        var element = plan.Choices.Single(c => c.Key == $"{I("elemental")}/elemento");
        Assert.Equal("Resistance", element.Kind);
        Assert.Equal(["cold", "fire", "lightning"], element.Options.Select(o => o.Index).ToArray());
        Assert.Equal("Frío", element.Options[0].Name);

        var applied = await s.Player.Client.PostAsJsonAsync(levelUp, new
        {
            classIndex = "fighter",
            hitPointsRolled = 6,
            choices = new object[]
            {
                new { key = plan.Choices.Single(c => c.Kind == "AsiOrFeat").Key, selected = new { feat = I("elemental") } },
                new { key = $"{I("elemental")}/elemento", selected = new[] { "lightning" } },
            },
        });
        Assert.True(applied.StatusCode == HttpStatusCode.OK, await applied.Content.ReadAsStringAsync());
        var leveled = (await applied.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Contains(leveled.Sheet.Resistances, r => r.DamageType == "lightning" && r.Label.StartsWith("Aguante elemental", StringComparison.Ordinal));
    }

    // ---- H40: weapon rules --------------------------------------------------------------------------------------

    [Fact]
    public async Task Firearms_double_weapons_and_requirements_show_in_the_attacks()
    {
        var s = await ScenarioAsync();
        var hero = await DraftAsync(s, "fighter", 1, new { str = 14, dex = 14, con = 12, @int = 10, wis = 10, cha = 10 });
        var pistol = await EquipAsync(s, hero.Id, "Pistola de ejemplo");
        var mace = await EquipAsync(s, hero.Id, "Maza doble de ejemplo");

        var armed = await s.Player.GetCharacterAsync(hero.Id);

        var shot = Assert.Single(armed.Combat.Attacks, a => a.ItemId == pistol);
        Assert.Equal("1d8", shot.Damage);
        Assert.DoesNotContain(shot.DamageBreakdown.Parts, p => p.Source == "ability");
        Assert.Contains("Requiere Destreza 16; tienes 14.", shot.Warnings);
        var profiles = armed.Combat.Attacks.Where(a => a.ItemId == mace).Select(a => a.Name).ToList();
        Assert.Equal(["Maza doble de ejemplo", "Maza doble (mango)"], profiles);
        Assert.StartsWith("1d4", armed.Combat.Attacks.Single(a => a.Name == "Maza doble (mango)").Damage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_the_listed_classes_attune_to_a_restricted_item()
    {
        var s = await ScenarioAsync();
        var fighter = await DraftAsync(s, "fighter", 1, LookoutScores);
        var lookout = await DraftAsync(s, I("vigia"), 1, LookoutScores);
        var amulet = await s.Player.SrdItemIdAsync("Amuleto de ejemplo");

        var foreign = await s.Dm.AddItemAsync(fighter.Id, new { templateId = amulet, quantity = 1 });
        var refused = await s.Player.PatchItemAsync(fighter.Id, foreign.Id, new { attuned = true });
        Assert.True(refused.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity, refused.StatusCode.ToString());
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        Assert.Contains("Vigía de ejemplo", problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);

        var own = await s.Dm.AddItemAsync(lookout.Id, new { templateId = amulet, quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(lookout.Id, own.Id, new { attuned = true })).StatusCode);
    }

    // ---- H07: catalog filter by weapon group -----------------------------------------------------------------

    [Theory]
    [InlineData("firearm", "Pistola de ejemplo")]
    [InlineData("bomb", "Granada de ejemplo")]
    [InlineData("exotic", "Maza doble de ejemplo")]
    public async Task The_item_catalog_filters_exotic_weapons_firearms_and_bombs(string group, string expected)
    {
        await ImportAsync(await factory.CreateAdminClientAsync());
        var s = await factory.CreateCampaignScenarioAsync();
        await s.EnablePacksAsync(Id);

        var page = await GetAsync<PagedResult<ItemSummaryDto>>(s.Player.Client, $"{CatalogUrl}/items?group={group}&pageSize=100");

        Assert.Contains(page.Items, i => i.Name == expected);
        Assert.All(page.Items, i => Assert.Equal(Id, i.Source));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.GetAsync($"{CatalogUrl}/items?group=legendary")).StatusCode);
    }

    [Theory]
    [InlineData("tiny", 75)]
    [InlineData("small", 150)]
    [InlineData("medium", 150)]
    [InlineData("large", 300)]
    [InlineData("gargantuan", 1200)]
    public void Carrying_capacity_depends_on_the_size(string size, int pounds) =>
        Assert.Equal(pounds, OpenTrpg.Systems.Dnd5e.Application.Items.Dnd5eInventory.CarryingCapacity(10, size));

    // ---- Validation ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Invalid_sheet_rules_are_reported_with_their_path()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Build();
        pack["id"] = "reglas-malas";
        var lookout = pack["classes"]![0]!;
        lookout["index"] = "reglas-malas-vigia";
        var features = lookout["levels"]![0]!["features"]!.AsArray();
        features[0]!["modifiers"]![0]!["condition"] = "wearingArmor";
        features[1]!["modifiers"]![0]!["value"] = "nivel + 2";
        features[2]!["modifiers"]![0]!["value"] = 1;
        features[3]!["modifiers"]![1]!["target"] = "burrow";
        features[4]!["modifiers"]![0]!["value"] = 11;
        lookout["saveDc"] = new JsonObject { ["name"] = "CD", ["ability"] = "luck" };
        var race = pack["races"]![0]!;
        race["index"] = "reglas-malas-escamado";
        race["grants"]!["immunities"] = new JsonArray("lava");
        race["grants"]!["conditionImmunities"] = new JsonArray("sleepy");
        race["grants"]!["size"] = "colossal";
        race["grants"]!["naturalWeapons"]![0]!["damage"] = "1d7";
        pack["feats"]![1]!["choices"]![0]!["from"] = new JsonArray("cold", "lava");
        var items = pack["items"]!.AsArray();
        items[0]!["weapon"]!["requires"] = new JsonObject { ["luck"] = 12 };
        items[1]!["weapon"]!["alternate"]!["range"] = "orbital";
        items[3]!["requiresAttunement"] = false;
        pack["feats"]![0]!["index"] = "reglas-malas-renuncia";
        pack["feats"]![1]!["index"] = "reglas-malas-elemental";
        for (var i = 0; i < items.Count; i++)
        {
            items[i]!["index"] = $"reglas-malas-{i}";
        }

        var errors = await ImportErrorsAsync(admin, pack);
        var all = string.Join("\n", errors);

        foreach (var path in new[]
                 {
                     "classes[0].levels[0].features[0].modifiers[0].condition",
                     "classes[0].levels[0].features[1].modifiers[0].value",
                     "classes[0].levels[0].features[2].modifiers[0]",
                     "classes[0].levels[0].features[3].modifiers[1]",
                     "classes[0].levels[0].features[4].modifiers[0]",
                     "classes[0].saveDc[0].ability",
                     "races[0].grants.immunities[0]",
                     "races[0].grants.conditionImmunities[0]",
                     "races[0].grants.size",
                     "races[0].grants.naturalWeapons[0].damage",
                     "feats[1].choices[0].from[1]",
                     "items[0].weapon.requires.luck",
                     "items[1].weapon.alternate.range",
                     "items[3].attunementClasses",
                 })
        {
            Assert.True(errors.Any(e => e.StartsWith($"{path}:", StringComparison.Ordinal)), $"{path}\n{all}");
        }
    }

    [Fact]
    public async Task Character_rule_modifiers_are_refused_on_items()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Build();
        pack["id"] = "reglas-objeto";
        pack.Remove("classes");
        pack.Remove("races");
        pack.Remove("feats");
        var amulet = pack["items"]![3]!;
        amulet["attunementClasses"] = null;
        amulet["modifiers"] = new JsonArray(new JsonObject { ["kind"] = "CriticalRange", ["value"] = 19 });
        var items = pack["items"]!.AsArray();
        for (var i = 0; i < items.Count; i++)
        {
            items[i]!["index"] = $"reglas-objeto-{i}";
        }

        var errors = await ImportErrorsAsync(admin, pack);

        Assert.Contains(errors, e => e.StartsWith("items[3].modifiers[0]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_firearm_without_misfire_imports_and_attunement_takes_a_base_class()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Build();
        pack["id"] = "reglas-sueltas";
        pack.Remove("classes");
        pack.Remove("races");
        pack.Remove("feats");
        var pistol = pack["items"]![0]!;
        pistol["index"] = "reglas-sueltas-pistola";
        pistol["name"] = "Pistola sin fallo";
        pistol["firearm"] = new JsonObject { ["reload"] = 6 };
        var amulet = pack["items"]![3]!;
        amulet["index"] = "reglas-sueltas-amuleto";
        amulet["name"] = "Amuleto del mago";
        amulet["attunementClasses"] = new JsonArray("wizard");
        pack["items"] = new JsonArray(pistol.DeepClone(), amulet.DeepClone());

        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        var gun = Assert.Single((await GetAsync<PagedResult<ItemSummaryDto>>(admin, $"{CatalogUrl}/items?search=pistola%20sin%20fallo")).Items);
        var gunDetail = await GetAsync<ItemDetailDto>(admin, $"{CatalogUrl}/items/{gun.Id}");
        Assert.Contains("firearm", gunDetail.Properties);
        var firearm = gunDetail.SystemData!.Value.GetProperty("firearm");
        Assert.Equal(6, firearm.GetProperty("reload").GetInt32());
        Assert.False(firearm.TryGetProperty("misfire", out _));

        var charm = Assert.Single((await GetAsync<PagedResult<ItemSummaryDto>>(admin, $"{CatalogUrl}/items?search=amuleto%20del%20mago")).Items);
        var charmDetail = await GetAsync<ItemDetailDto>(admin, $"{CatalogUrl}/items/{charm.Id}");
        Assert.Equal(["wizard"], charmDetail.SystemData!.Value.GetProperty("attunementClasses").EnumerateArray().Select(c => c.GetString()));
    }

    [Fact]
    public async Task Attunement_classes_must_exist_in_the_pack_or_the_base_packs()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Build();
        pack["id"] = "reglas-sintonia";
        pack.Remove("races");
        pack.Remove("feats");
        pack.Remove("classes");
        var amulet = pack["items"]![3]!.DeepClone();
        amulet["index"] = "reglas-sintonia-amuleto";
        amulet["attunementClasses"] = new JsonArray("wizard", "reglas-sintonia-fantasma");
        pack["items"] = new JsonArray(amulet);

        var errors = await ImportErrorsAsync(admin, pack);

        Assert.Contains(errors, e => e.StartsWith("items[0].attunementClasses[1]:", StringComparison.Ordinal) && e.Contains("reglas-sintonia-fantasma", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, e => e.StartsWith("items[0].attunementClasses[0]", StringComparison.Ordinal));
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

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

    /// <summary>An active character of <paramref name="classIndex"/> at <paramref name="level"/> with the given base scores.</summary>
    private static async Task<CharacterDetailDto> DraftAsync(CampaignScenario s, string classIndex, int level, object scores, string? subclass = null, string? race = null)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, classIndex);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            raceIndex = race,
            classes = new[] { new { classIndex, subclassIndex = subclass, level } },
            baseAbilities = scores,
            applyRacialBonuses = false,
            proficiencies = Saves(classIndex),
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.ActivateAsync(character.Id)).StatusCode);
        return await s.Player.GetCharacterAsync(character.Id);
    }

    /// <summary>The saving throw proficiencies of the class (the sheet patch does not add them by itself).</summary>
    private static object[] Saves(string classIndex) =>
        (classIndex switch { "sorcerer" => new[] { "con", "cha" }, _ => ["str", "con"] })
        .Select(key => (object)new { type = "SavingThrow", key, expertise = false })
        .ToArray();

    private static async Task<Guid> EquipAsync(CampaignScenario s, Guid characterId, string name)
    {
        var item = await s.Dm.AddItemAsync(characterId, new { templateId = await s.Player.SrdItemIdAsync(name), quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(characterId, item.Id, new { equipped = true })).StatusCode);
        return item.Id;
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
