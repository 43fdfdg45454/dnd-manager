using System.Diagnostics;
using Dnd.Application.Abstractions;
using Dnd.Domain.Catalog;
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
            Assert.Equal(237, await db.ItemTemplates.CountAsync(x => x.Category != ItemCategory.MagicItem && x.Rarity == null));

            var import = await db.CatalogImports.SingleAsync();
            Assert.Equal(CatalogImport.SrdRuleset, import.Ruleset);
            Assert.Contains("a6212beb", import.DatasetVersion);
            Assert.Contains("\"spells\":319", import.CountsJson);
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
            Assert.Equal(599, await db.ItemTemplates.CountAsync());
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
