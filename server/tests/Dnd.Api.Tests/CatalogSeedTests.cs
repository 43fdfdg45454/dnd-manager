using System.Diagnostics;
using Dnd.Application.Abstractions;
using Dnd.Domain.Catalog;
using Dnd.Domain.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Dnd.Api.Tests;

[Collection(CatalogCollection.Name)]
public class CatalogSeedTests(CatalogApiFactory factory, ITestOutputHelper output)
{
    [Fact]
    public async Task Startup_imports_the_whole_srd()
    {
        factory.CreateClient().Dispose();

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(12, await db.CatalogClasses.CountAsync());
            Assert.Equal(12, await db.CatalogSubclasses.CountAsync());
            Assert.Equal(240, await db.CatalogClassLevels.CountAsync());
            Assert.Equal(50, await db.CatalogSubclassLevels.CountAsync());
            Assert.Equal(290, await db.CatalogClassLevels.CountAsync() + await db.CatalogSubclassLevels.CountAsync());
            Assert.Equal(407, await db.CatalogFeatures.CountAsync());
            Assert.Equal(9, await db.CatalogRaces.CountAsync());
            Assert.Equal(4, await db.CatalogSubraces.CountAsync());
            Assert.Equal(38, await db.CatalogTraits.CountAsync());
            Assert.Equal(319, await db.CatalogSpells.CountAsync());
            Assert.Equal(15, await db.CatalogConditions.CountAsync());
            Assert.Equal(18, await db.CatalogSkills.CountAsync());
            Assert.Equal(1, await db.CatalogBackgrounds.CountAsync());

            var srdItems = await db.ItemTemplates.CountAsync(x => x.CampaignId == null);
            Assert.True(srdItems >= 590, $"Expected at least 590 SRD items, got {srdItems}.");
            Assert.Equal(237 + 362, srdItems);
            Assert.Equal(237, await db.ItemTemplates.CountAsync(x => x.CampaignId == null && x.Category != ItemCategory.MagicItem && x.Rarity == null));

            // Potions, scrolls, oils and ammunition are consumables, magic or not.
            Assert.Equal(ItemCategory.Consumable, (await db.ItemTemplates.SingleAsync(x => x.CampaignId == null && x.Index == "potion-of-healing")).Category);
            Assert.Equal(ItemCategory.Consumable, (await db.ItemTemplates.SingleAsync(x => x.CampaignId == null && x.Index == "spell-scroll-3rd")).Category);
            Assert.Equal(ItemCategory.Consumable, (await db.ItemTemplates.SingleAsync(x => x.CampaignId == null && x.Index == "oil-of-sharpness")).Category);
            Assert.Equal(ItemCategory.Consumable, (await db.ItemTemplates.SingleAsync(x => x.CampaignId == null && x.Index == "arrow-of-slaying")).Category);
            Assert.Equal(ItemCategory.Consumable, (await db.ItemTemplates.SingleAsync(x => x.CampaignId == null && x.Index == "arrow")).Category);
            Assert.Equal(ItemCategory.MagicItem, (await db.ItemTemplates.SingleAsync(x => x.CampaignId == null && x.Index == "wand-of-lightning-bolts")).Category);
            Assert.True(await db.ItemTemplates.AnyAsync(x => x.CampaignId == null && x.Category == ItemCategory.Consumable));

            var import = await db.CatalogImports.SingleAsync();
            Assert.Equal(CatalogImport.SrdRuleset, import.Ruleset);
            Assert.Contains("a6212beb", import.DatasetVersion);
            Assert.Contains("skill choices", import.DatasetVersion);
            Assert.Contains("personality", import.DatasetVersion);
            Assert.Contains("race grants", import.DatasetVersion);
            Assert.Contains("\"spells\":319", import.CountsJson);
        });
    }

    [Fact]
    public async Task Startup_imports_the_srd_level_choice_catalog()
    {
        factory.CreateClient().Dispose();

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(13, await db.CatalogOptionSets.CountAsync(x => x.Source == CatalogSources.Srd));
            Assert.Equal(99, await db.CatalogOptions.CountAsync(x => x.Source == CatalogSources.Srd));
            Assert.Equal(197, await db.CatalogLevelChoiceRules.CountAsync(x => x.Source == CatalogSources.Srd));

            var defense = await db.CatalogOptions.SingleAsync(x => x.Index == "fighting-style-defense");
            Assert.Equal([new ChoiceModifier(ItemModifierKind.ArmorClassBonus, null, 1, ModifierConditions.WearingArmor)], defense.Modifiers);
            var rule = await db.CatalogLevelChoiceRules.SingleAsync(x => x.Id == "warlock/-/2/eldritch-invocations");
            Assert.Equal((LevelChoiceKind.OptionSet, 2, true, true), (rule.Kind, rule.Choose, rule.Replaces, rule.Cumulative));
            Assert.True((await db.CatalogLevelChoiceRules.SingleAsync(x => x.Id == "wizard/-/2/spellbook")).Filter.MaxSpellLevelBySlots);
            Assert.Contains("\"levelChoiceRules\":197", (await db.CatalogImports.SingleAsync()).CountsJson);
        });
    }

    [Fact]
    public async Task Srd_items_with_numeric_effects_get_structured_modifiers()
    {
        factory.CreateClient().Dispose();

        await factory.WithDbAsync(async db =>
        {
            var gauntlets = await db.ItemTemplates.SingleAsync(x => x.CampaignId == null && x.Index == "gauntlets-of-ogre-power");
            Assert.Equal([new ItemModifier(ItemModifierKind.AbilitySet, "str", 19)], gauntlets.Modifiers);
            Assert.Equal(ItemCategory.MagicItem, gauntlets.Category);

            var cloak = await db.ItemTemplates.SingleAsync(x => x.CampaignId == null && x.Index == "cloak-of-protection");
            Assert.Equal(
                [new ItemModifier(ItemModifierKind.ArmorClassBonus, null, 1), new ItemModifier(ItemModifierKind.SaveBonus, null, 1)],
                cloak.Modifiers);

            var weapon = await db.ItemTemplates.SingleAsync(x => x.CampaignId == null && x.Index == "weapon-2");
            Assert.Equal((2, 2), (weapon.Modifiers.Single(m => m.Kind == ItemModifierKind.AttackBonus).Value, weapon.Modifiers.Single(m => m.Kind == ItemModifierKind.DamageBonus).Value));
            Assert.Equal(29, (await db.ItemTemplates.SingleAsync(x => x.CampaignId == null && x.Index == "belt-of-giant-strength-storm")).Modifiers.Single().Value);
            Assert.Empty((await db.ItemTemplates.SingleAsync(x => x.CampaignId == null && x.Index == "longsword")).Modifiers);
        });
    }

    [Fact]
    public async Task Seeding_again_does_nothing()
    {
        factory.CreateClient().Dispose();
        using var scope = factory.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<ISrdSeeder>();

        Assert.False(await seeder.SeedAsync());

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.CatalogImports.CountAsync());
            Assert.Equal(319, await db.CatalogSpells.CountAsync());
            Assert.Equal(599, await db.ItemTemplates.CountAsync(x => x.CampaignId == null));
        });
    }

    [Fact]
    public async Task Reimporting_a_dataset_replaces_definitions_and_keeps_item_ids()
    {
        factory.CreateClient().Dispose();
        Dictionary<string, Guid> idsBefore = [];
        await factory.WithDbAsync(async db =>
        {
            idsBefore = await db.ItemTemplates.Where(x => x.Index != null).ToDictionaryAsync(x => x.Index!, x => x.Id);

            // Simulates a dataset version that was never imported.
            await db.CatalogImports.ExecuteDeleteAsync();
        });

        using var scope = factory.Services.CreateScope();
        var stopwatch = Stopwatch.StartNew();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISrdSeeder>().SeedAsync());
        output.WriteLine($"SRD re-import (SQLite in memory): {stopwatch.ElapsedMilliseconds} ms");

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.CatalogImports.CountAsync());
            Assert.Equal(12, await db.CatalogClasses.CountAsync());
            Assert.Equal(319, await db.CatalogSpells.CountAsync());
            var idsAfter = await db.ItemTemplates.Where(x => x.Index != null).ToDictionaryAsync(x => x.Index!, x => x.Id);
            Assert.Equal(idsBefore.OrderBy(x => x.Key), idsAfter.OrderBy(x => x.Key));
        });
    }

    [Fact]
    public async Task Fresh_import_takes_a_few_seconds()
    {
        using var fresh = new ApiFactoryWithoutInitialAdmin();
        fresh.CreateClient().Dispose();
        using var scope = fresh.Services.CreateScope();

        var stopwatch = Stopwatch.StartNew();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ISrdSeeder>().SeedAsync());
        stopwatch.Stop();
        output.WriteLine($"SRD import (SQLite in memory): {stopwatch.ElapsedMilliseconds} ms");

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"The SRD import took {stopwatch.Elapsed}.");
    }
}
