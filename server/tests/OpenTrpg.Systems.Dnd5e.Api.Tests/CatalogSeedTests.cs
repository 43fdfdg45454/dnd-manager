using System.Diagnostics;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests;

[Collection(CatalogCollection.Name)]
public class CatalogSeedTests(CatalogApiFactory factory, ITestOutputHelper output)
{
    [Fact]
    public async Task Startup_imports_the_whole_srd()
    {
        factory.CreateClient().Dispose();

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(12, await db.Set<ClassDefinition>().CountAsync());
            Assert.Equal(12, await db.Set<SubclassDefinition>().CountAsync());
            Assert.Equal(240, await db.Set<ClassLevel>().CountAsync());
            Assert.Equal(50, await db.Set<SubclassLevel>().CountAsync());
            Assert.Equal(290, await db.Set<ClassLevel>().CountAsync() + await db.Set<SubclassLevel>().CountAsync());
            Assert.Equal(407, await db.Set<FeatureDefinition>().CountAsync());
            Assert.Equal(9, await db.Set<RaceDefinition>().CountAsync());
            Assert.Equal(4, await db.Set<SubraceDefinition>().CountAsync());
            Assert.Equal(38, await db.Set<TraitDefinition>().CountAsync());
            Assert.Equal(319, await db.Set<SpellDefinition>().CountAsync());
            Assert.Equal(15, await db.Set<ConditionDefinition>().CountAsync());
            Assert.Equal(18, await db.Set<SkillDefinition>().CountAsync());
            Assert.Equal(1, await db.Set<BackgroundDefinition>().CountAsync());

            // Phase 34: the SRD beasts, its vocabularies and no rules document live in tables of their own.
            Assert.Equal(87, await db.Set<CreatureDefinition>().CountAsync());
            Assert.Equal(0, await db.Set<RuleDefinition>().CountAsync());
            Assert.Equal(16 + 11 + 39 + 13 + 8 + 31, await db.Set<ReferenceEntry>().CountAsync());
            Assert.Equal(
                [(ReferenceEntry.DamageTypes, 13), (ReferenceEntry.EquipmentCategories, 39), (ReferenceEntry.Languages, 16), (ReferenceEntry.MagicSchools, 8), (ReferenceEntry.Tools, 31), (ReferenceEntry.WeaponProperties, 11)],
                (await db.Set<ReferenceEntry>().GroupBy(e => e.Kind).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                    .OrderBy(g => g.Key, StringComparer.Ordinal)
                    .Select(g => (g.Key, g.Count)));
            Assert.True(await db.Set<CreatureDefinition>().AllAsync(c => c.Source == Dnd5eCatalogSources.Srd));

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

            var import = await db.ContentPacks.SingleAsync();
            Assert.Equal((Dnd5eCatalogSources.Srd, "dnd5e", "SRD 5.1", 3, true), (import.Id, import.SystemId, import.Name, import.FormatVersion, import.IsBase));
            Assert.Matches(@"^5\.1\.\d+$", import.Version);
            Assert.True(import.Version.Length <= ContentPack.VersionMaxLength);
            Assert.Contains("\"spells\":319", import.CountsJson);
        });
    }

    [Fact]
    public async Task Startup_imports_the_srd_level_choice_catalog()
    {
        factory.CreateClient().Dispose();

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(13, await db.Set<OptionSetDefinition>().CountAsync(x => x.Source == Dnd5eCatalogSources.Srd));
            Assert.Equal(99, await db.Set<OptionDefinition>().CountAsync(x => x.Source == Dnd5eCatalogSources.Srd));
            Assert.Equal(197, await db.Set<LevelChoiceRule>().CountAsync(x => x.Source == Dnd5eCatalogSources.Srd));

            var defense = await db.Set<OptionDefinition>().SingleAsync(x => x.Index == "fighting-style-defense");
            Assert.Equal([new ChoiceModifier(ItemModifierKind.ArmorClassBonus, null, 1, ModifierConditions.WearingArmor)], defense.Modifiers);
            var rule = await db.Set<LevelChoiceRule>().SingleAsync(x => x.Id == "warlock/-/2/eldritch-invocations");
            Assert.Equal((LevelChoiceKind.OptionSet, 2, true, true), (rule.Kind, rule.Choose, rule.Replaces, rule.Cumulative));
            Assert.True((await db.Set<LevelChoiceRule>().SingleAsync(x => x.Id == "wizard/-/2/spellbook")).Filter.MaxSpellLevelBySlots);
            Assert.Contains("\"levelChoices\":197", (await db.ContentPacks.SingleAsync()).CountsJson);
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
        var seeder = scope.ServiceProvider.GetRequiredService<IDnd5eCatalogSystem>();

        Assert.False(await seeder.ImportBasePackAsync());

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.ContentPacks.CountAsync());
            Assert.Equal(319, await db.Set<SpellDefinition>().CountAsync());
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
            await db.ContentPacks.ExecuteDeleteAsync();
        });

        using var scope = factory.Services.CreateScope();
        var stopwatch = Stopwatch.StartNew();
        Assert.True(await scope.ServiceProvider.GetRequiredService<IDnd5eCatalogSystem>().ImportBasePackAsync());
        output.WriteLine($"SRD re-import (SQLite in memory): {stopwatch.ElapsedMilliseconds} ms");

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.ContentPacks.CountAsync());
            Assert.Equal(12, await db.Set<ClassDefinition>().CountAsync());
            Assert.Equal(319, await db.Set<SpellDefinition>().CountAsync());
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
        Assert.True(await scope.ServiceProvider.GetRequiredService<IDnd5eCatalogSystem>().ImportBasePackAsync());
        stopwatch.Stop();
        output.WriteLine($"SRD import (SQLite in memory): {stopwatch.ElapsedMilliseconds} ms");

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"The SRD import took {stopwatch.Elapsed}.");
    }
}
