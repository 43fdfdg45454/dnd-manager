using System.Net;
using System.Net.Http.Json;
using Dnd.Application.Catalog;
using Dnd.Application.Common;

namespace Dnd.Api.Tests;

[Collection(CatalogCollection.Name)]
public class CatalogEndpointsTests(CatalogApiFactory factory)
{
    private const string Base = "/api/v1/catalog";

    private async Task<HttpClient> ClientAsync() => (await factory.CreateSignedInUserAsync()).Client;

    private async Task<T> GetAsync<T>(string url)
    {
        var client = await ClientAsync();
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    [Theory]
    [InlineData("/attribution")]
    [InlineData("/classes")]
    [InlineData("/spells")]
    [InlineData("/items")]
    [InlineData("/conditions")]
    public async Task Catalog_requires_authentication(string path)
    {
        var response = await factory.CreateClient().GetAsync(Base + path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Attribution_contains_the_cc_by_notice()
    {
        var attribution = await GetAsync<AttributionDto>($"{Base}/attribution");

        Assert.Equal("srd-5.1", attribution.Ruleset);
        Assert.Equal("CC-BY-4.0", attribution.License);
        Assert.Contains("Creative Commons", attribution.Text);
        Assert.Contains("System Reference Document 5.1", attribution.Text);
    }

    [Fact]
    public async Task Classes_are_listed_by_name_with_spellcasting_progression()
    {
        var classes = await GetAsync<List<ClassSummaryDto>>($"{Base}/classes");

        Assert.Equal(12, classes.Count);
        Assert.Equal(classes.Select(c => c.Name).Order(StringComparer.Ordinal), classes.Select(c => c.Name));

        var byIndex = classes.ToDictionary(c => c.Index);
        Assert.Equal((1, false, "int"), (byIndex["wizard"].SpellcastingLevel, byIndex["wizard"].IsPactCaster, byIndex["wizard"].SpellcastingAbility));
        Assert.Equal((1, true), (byIndex["warlock"].SpellcastingLevel, byIndex["warlock"].IsPactCaster));
        Assert.Equal(2, byIndex["paladin"].SpellcastingLevel);
        Assert.Equal(2, byIndex["ranger"].SpellcastingLevel);
        Assert.Equal((0, false, (string?)null), (byIndex["fighter"].SpellcastingLevel, byIndex["fighter"].IsSpellcaster, byIndex["fighter"].SpellcastingAbility));
        Assert.Equal("Primal Path", byIndex["barbarian"].SubclassFlavor);
        Assert.Equal(["str", "con"], byIndex["barbarian"].SavingThrows);
    }

    [Fact]
    public async Task Wizard_detail_has_20_levels_with_first_level_slots()
    {
        var wizard = await GetAsync<ClassDetailDto>($"{Base}/classes/wizard");

        Assert.Equal(6, wizard.HitDie);
        Assert.Equal(20, wizard.Levels.Count);
        Assert.Equal(Enumerable.Range(1, 20), wizard.Levels.Select(l => l.Level));
        var first = wizard.Levels[0];
        Assert.Equal([2, 0, 0, 0, 0, 0, 0, 0, 0], first.SpellSlots);
        Assert.Equal(3, first.CantripsKnown);
        Assert.Equal(2, first.ProfBonus);
        Assert.Contains(first.Features, f => f.Index == "arcane-recovery" && f.Description.Count > 0);
        Assert.Equal([4, 3, 3, 3, 3, 2, 2, 1, 1], wizard.Levels[19].SpellSlots);
        Assert.DoesNotContain(wizard.ProficiencyNames, p => p.StartsWith("Saving Throw", StringComparison.Ordinal));
        Assert.Contains("Spellbook", wizard.StartingEquipmentText);

        var evocation = Assert.Single(wizard.Subclasses);
        Assert.Equal("evocation", evocation.Index);
        Assert.Contains(evocation.Levels, l => l.Level == 2 && l.Features.Any(f => f.Index == "sculpt-spells"));
    }

    [Theory]
    [InlineData("rogue", 4, 11)]
    [InlineData("wizard", 2, 6)]
    [InlineData("bard", 3, 18)]
    [InlineData("monk", 2, 6)]
    public async Task Class_detail_exposes_the_level_one_skill_choice(string index, int choose, int count)
    {
        var detail = await GetAsync<ClassDetailDto>($"{Base}/classes/{index}");

        Assert.Equal(choose, detail.SkillChoices.Choose);
        Assert.Equal(count, detail.SkillChoices.From.Count);
        Assert.All(detail.SkillChoices.From, s => Assert.DoesNotContain("skill-", s));
    }

    [Fact]
    public async Task Rogue_skill_choice_lists_its_skills_and_ignores_tool_choices()
    {
        var rogue = await GetAsync<ClassDetailDto>($"{Base}/classes/rogue");
        var wizard = await GetAsync<ClassDetailDto>($"{Base}/classes/wizard");

        Assert.Contains("acrobatics", rogue.SkillChoices.From);
        Assert.Contains("sleight-of-hand", rogue.SkillChoices.From);
        Assert.DoesNotContain("arcana", rogue.SkillChoices.From);
        Assert.Equal(["arcana", "history", "insight", "investigation", "medicine", "religion"], wizard.SkillChoices.From);
    }

    [Fact]
    public async Task Class_specific_counters_are_exposed_as_an_object()
    {
        var barbarian = await GetAsync<ClassDetailDto>($"{Base}/classes/barbarian");

        Assert.Equal(3, barbarian.Levels[2].ClassSpecific.GetProperty("rage_count").GetInt32());
        Assert.All(barbarian.Levels, l => Assert.Equal(9, l.SpellSlots.Count));
        Assert.All(barbarian.Levels, l => Assert.All(l.SpellSlots, s => Assert.Equal(0, s)));
    }

    [Fact]
    public async Task Unknown_catalog_entries_return_404()
    {
        var client = await ClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/classes/artificer")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/races/kender")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/spells/nope")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/items/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/features/nope")).StatusCode);
    }

    [Fact]
    public async Task Races_include_ability_bonuses_traits_and_subraces()
    {
        var races = await GetAsync<List<RaceSummaryDto>>($"{Base}/races");
        Assert.Equal(9, races.Count);

        var dwarf = await GetAsync<RaceDetailDto>($"{Base}/races/dwarf");
        Assert.Equal(25, dwarf.Speed);
        Assert.Equal([new AbilityBonusDto("con", 2)], dwarf.AbilityBonuses);
        Assert.Contains(dwarf.Traits, t => t.Index == "darkvision" && t.Description.Count > 0);
        Assert.Contains("Dwarvish", dwarf.Languages);
        var hillDwarf = Assert.Single(dwarf.Subraces);
        Assert.Equal("hill-dwarf", hillDwarf.Index);
        Assert.Equal([new AbilityBonusDto("wis", 1)], hillDwarf.AbilityBonuses);
        Assert.Contains(hillDwarf.Traits, t => t.Index == "dwarven-toughness");
    }

    [Fact]
    public async Task Spells_filter_by_level_and_class()
    {
        var page = await GetAsync<PagedResult<SpellSummaryDto>>($"{Base}/spells?level=3&class=wizard&pageSize=200");

        Assert.NotEmpty(page.Items);
        Assert.Equal(page.Total, page.Items.Count);
        Assert.All(page.Items, s => Assert.Equal(3, s.Level));
        Assert.All(page.Items, s => Assert.Contains("wizard", s.ClassIndexes));
        Assert.Contains(page.Items, s => s.Index == "fireball");
        Assert.DoesNotContain(page.Items, s => s.Index == "spirit-guardians");
    }

    [Fact]
    public async Task Spells_are_paged_and_ordered_by_level_and_name()
    {
        var first = await GetAsync<PagedResult<SpellSummaryDto>>($"{Base}/spells?pageSize=20");
        var second = await GetAsync<PagedResult<SpellSummaryDto>>($"{Base}/spells?page=2&pageSize=20");

        Assert.Equal(319, first.Total);
        Assert.Equal((1, 20), (first.Page, first.PageSize));
        Assert.Equal(20, first.Items.Count);
        Assert.Empty(first.Items.Select(s => s.Index).Intersect(second.Items.Select(s => s.Index)));
        Assert.All(first.Items, s => Assert.Equal(0, s.Level));

        var withClass = await GetAsync<PagedResult<SpellSummaryDto>>($"{Base}/spells?class=cleric&page=2&pageSize=10");
        Assert.Equal(2, withClass.Page);
        Assert.Equal(10, withClass.Items.Count);
        Assert.True(withClass.Total > 20);
    }

    [Fact]
    public async Task Spells_filter_by_search_school_ritual_and_concentration()
    {
        var search = await GetAsync<PagedResult<SpellSummaryDto>>($"{Base}/spells?search=FIRE");
        Assert.Contains(search.Items, s => s.Name == "Fireball");
        Assert.All(search.Items, s => Assert.Contains("fire", s.Name, StringComparison.OrdinalIgnoreCase));

        var rituals = await GetAsync<PagedResult<SpellSummaryDto>>($"{Base}/spells?ritual=true&school=divination&pageSize=200");
        Assert.NotEmpty(rituals.Items);
        Assert.All(rituals.Items, s => Assert.True(s.Ritual && s.School == "Divination"));

        var concentration = await GetAsync<PagedResult<SpellSummaryDto>>($"{Base}/spells?concentration=true&level=1&pageSize=200");
        Assert.NotEmpty(concentration.Items);
        Assert.All(concentration.Items, s => Assert.True(s.Concentration && s.Level == 1));
    }

    [Fact]
    public async Task Spell_query_is_validated()
    {
        var client = await ClientAsync();

        var response = await client.GetAsync($"{Base}/spells?level=12&pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.True(problem.HasFieldError("level"));
        Assert.True(problem.HasFieldError("pageSize"));
    }

    [Fact]
    public async Task Spell_detail_exposes_damage_and_saving_throw()
    {
        var fireball = await GetAsync<SpellDetailDto>($"{Base}/spells/fireball");

        Assert.Equal(3, fireball.Level);
        Assert.Equal("Evocation", fireball.School);
        Assert.Equal(["V", "S", "M"], fireball.Components);
        Assert.NotNull(fireball.Material);
        Assert.Equal("dex", fireball.DcAbility);
        Assert.NotEmpty(fireball.HigherLevel);
        Assert.NotNull(fireball.Damage);
        Assert.Equal("8d6", fireball.Damage.Dice);
        Assert.Equal("Fire", fireball.Damage.Type);
        Assert.Equal("14d6", fireball.Damage.AtSlotLevel![9]);

        var acidSplash = await GetAsync<SpellDetailDto>($"{Base}/spells/acid-splash");
        Assert.Equal("1d6", acidSplash.Damage!.Dice);
        Assert.Equal("4d6", acidSplash.Damage.AtCharacterLevel![17]);
    }

    [Fact]
    public async Task Spell_detail_exposes_healing_per_slot_level()
    {
        var cureWounds = await GetAsync<SpellDetailDto>($"{Base}/spells/cure-wounds");
        var fireball = await GetAsync<SpellDetailDto>($"{Base}/spells/fireball");

        Assert.NotNull(cureWounds.HealAtSlotLevel);
        Assert.Equal("1d8 + MOD", cureWounds.HealAtSlotLevel[1]);
        Assert.Equal("9d8 + MOD", cureWounds.HealAtSlotLevel[9]);
        Assert.Null(fireball.HealAtSlotLevel);
    }

    [Fact]
    public async Task Item_search_is_case_insensitive()
    {
        var page = await GetAsync<PagedResult<ItemSummaryDto>>($"{Base}/items?search=SWORD&pageSize=200");

        Assert.Contains(page.Items, i => i.Name == "Longsword");
        Assert.All(page.Items, i => Assert.Contains("sword", i.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Longsword_costs_1500_copper_and_is_versatile()
    {
        var longsword = await FindItemAsync("Longsword");

        Assert.Equal("Weapon", longsword.Category);
        Assert.Equal("Martial Melee", longsword.Subcategory);
        Assert.Equal(1500, longsword.CostCp);
        Assert.Equal(3m, longsword.WeightLb);
        Assert.Equal(("1d8", "Slashing", "1d10"), (longsword.DamageDice, longsword.DamageType, longsword.VersatileDice));
        Assert.Contains("versatile", longsword.Properties);
        Assert.True(longsword.IsSrd);
        Assert.Null(longsword.Rarity);
    }

    [Fact]
    public async Task Thrown_and_ranged_weapons_have_ranges()
    {
        var dagger = await FindItemAsync("Dagger");
        Assert.Equal((20, 60), (dagger.RangeNormal, dagger.RangeLong));

        var longbow = await FindItemAsync("Longbow");
        Assert.Equal((150, 600), (longbow.RangeNormal, longbow.RangeLong));
    }

    [Fact]
    public async Task Chain_mail_has_base_ac_16_without_dex_and_needs_strength_13()
    {
        var chainMail = await FindItemAsync("Chain Mail");

        Assert.Equal("Armor", chainMail.Category);
        Assert.Equal("Heavy Armor", chainMail.Subcategory);
        Assert.Equal(16, chainMail.ArmorClassBase);
        Assert.False(chainMail.AddDexModifier);
        Assert.Equal(13, chainMail.StrengthMinimum);
        Assert.True(chainMail.StealthDisadvantage);
        Assert.Equal(7500, chainMail.CostCp);

        var shield = await FindItemAsync("Shield");
        Assert.Equal("Shield", shield.Category);
        Assert.Equal(2, shield.ArmorClassBase);
    }

    [Fact]
    public async Task Magic_items_with_variants_import_each_variant_as_an_item()
    {
        var page = await GetAsync<PagedResult<ItemSummaryDto>>($"{Base}/items?search=healing&category=Consumable&pageSize=200");
        var names = page.Items.Select(i => i.Name).ToList();

        Assert.Contains("Potion of Greater Healing", names);
        Assert.Contains("Potion of Superior Healing", names);
        Assert.Contains("Potion of Supreme Healing", names);
        Assert.Equal(2, names.Count(n => n == "Potion of Healing"));
        Assert.Contains(page.Items, i => i.Index == "potion-of-healing" && i.Rarity == "Varies");
        Assert.Contains(page.Items, i => i.Index == "potion-of-healing-common" && i.Rarity == "Common");
        Assert.Contains(page.Items, i => i.Index == "potion-of-healing-supreme" && i.Rarity == "VeryRare");
        Assert.All(page.Items.Where(i => i.Name.StartsWith("Potion", StringComparison.Ordinal)), i => Assert.Equal("Potion", i.Subcategory));

        var ammunition = await GetAsync<PagedResult<ItemSummaryDto>>($"{Base}/items?search=ammunition,&pageSize=200");
        Assert.Contains(ammunition.Items, i => i.Name == "Ammunition, +3" && i.Rarity == "VeryRare");
    }

    [Fact]
    public async Task Magic_weapons_keep_their_category_and_rarity()
    {
        var page = await GetAsync<PagedResult<ItemSummaryDto>>($"{Base}/items?search=vorpal");
        var vorpal = Assert.Single(page.Items);

        Assert.Equal("Weapon", vorpal.Category);
        Assert.Equal("Legendary", vorpal.Rarity);
        Assert.True(vorpal.RequiresAttunement);
        Assert.Null(vorpal.CostCp);

        var detail = await GetAsync<ItemDetailDto>($"{Base}/items/{vorpal.Id}");
        Assert.NotEmpty(detail.Description);
    }

    [Fact]
    public async Task Items_filter_by_category_and_rarity()
    {
        var tools = await GetAsync<PagedResult<ItemSummaryDto>>($"{Base}/items?category=tool&pageSize=200");
        Assert.Equal(31, tools.Total);
        Assert.All(tools.Items, i => Assert.Equal("Tool", i.Category));

        var veryRare = await GetAsync<PagedResult<ItemSummaryDto>>($"{Base}/items?rarity=very-rare&pageSize=10&page=2");
        Assert.Equal(10, veryRare.Items.Count);
        Assert.All(veryRare.Items, i => Assert.Equal("VeryRare", i.Rarity));
        Assert.True(veryRare.Total > 20);
    }

    [Fact]
    public async Task Item_query_is_validated()
    {
        var client = await ClientAsync();

        var response = await client.GetAsync($"{Base}/items?category=spaceship&rarity=mythic");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.True(problem.HasFieldError("category"));
        Assert.True(problem.HasFieldError("rarity"));
    }

    [Fact]
    public async Task Reference_tables_are_complete()
    {
        var conditions = await GetAsync<List<ConditionDto>>($"{Base}/conditions");
        Assert.Equal(15, conditions.Count);
        Assert.Contains(conditions, c => c.Index == "blinded" && c.Description.Count > 0);

        var skills = await GetAsync<List<SkillDto>>($"{Base}/skills");
        Assert.Equal(18, skills.Count);
        Assert.Contains(skills, s => s.Index == "acrobatics" && s.AbilityIndex == "dex");

        var background = Assert.Single(await GetAsync<List<BackgroundDto>>($"{Base}/backgrounds"));
        Assert.Equal("acolyte", background.Index);
        Assert.Equal("Shelter of the Faithful", background.FeatureName);
        Assert.Equal(["Insight", "Religion"], background.SkillProficiencies);
        Assert.Contains("15 gp", background.StartingEquipmentText);

        var rage = await GetAsync<FeatureDto>($"{Base}/features/rage");
        Assert.Equal(("Rage", "barbarian", 1), (rage.Name, rage.ClassIndex, rage.Level));
        Assert.NotEmpty(rage.Description);
    }

    private async Task<ItemDetailDto> FindItemAsync(string name)
    {
        var page = await GetAsync<PagedResult<ItemSummaryDto>>($"{Base}/items?search={Uri.EscapeDataString(name)}&pageSize=200");
        var summary = Assert.Single(page.Items, i => i.Name == name);
        return await GetAsync<ItemDetailDto>($"{Base}/items/{summary.Id}");
    }
}
