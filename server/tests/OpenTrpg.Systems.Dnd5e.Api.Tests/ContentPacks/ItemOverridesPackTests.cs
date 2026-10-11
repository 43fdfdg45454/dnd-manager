using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Application.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the overrides change the SRD items of the whole instance.</summary>
public sealed class ItemOverridesPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary><c>itemOverrides</c>: content packs giving a new description to items of the SRD or of other packs.</summary>
public class ItemOverridesPackTests(ItemOverridesPackApiFactory factory) : IClassFixture<ItemOverridesPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string Supplies = "alchemists-supplies";
    private const string SmithsTools = "smiths-tools";
    private static readonly object Scores = new { str = 10, dex = 14, con = 14, @int = 12, wis = 10, cha = 15 };

    [Fact]
    public async Task Overrides_replace_the_description_while_installed_and_the_last_pack_wins()
    {
        var admin = await factory.CreateAdminClientAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        var srdSupplies = (await ItemAsync(admin, Supplies)).Description;
        var srdSmiths = (await ItemAsync(admin, SmithsTools)).Description;
        Assert.Contains("artisan's tools", srdSupplies.Single(), StringComparison.OrdinalIgnoreCase);

        // The first pack overrides two tools: the catalog item and the origin tool option show its text.
        var first = Pack("notas-uno",
            new { item = Supplies, description = new[] { "Primer párrafo de ejemplo.", "Segundo párrafo de ejemplo." } },
            new { item = SmithsTools, description = new[] { "Herramientas de herrero de ejemplo." } });
        var result = await ImportAsync(admin, first);
        Assert.Equal(2, result.Counts["itemOverrides"]);
        Assert.Equal(["Primer párrafo de ejemplo.", "Segundo párrafo de ejemplo."], (await ItemAsync(admin, Supplies)).Description);

        var dwarf = await DraftAsync(s, "dwarf", "hill-dwarf", "barbarian");
        var tools = Assert.Single((await OriginAsync(s.Player, dwarf.Id)).Choices, c => c.Key == "race.tools");
        var smiths = Assert.Single(tools.Options, o => o.Index == SmithsTools);
        Assert.Equal(["Herramientas de herrero de ejemplo."], smiths.Description);

        // A second pack overriding the same item wins while installed; deleting it brings the first one back.
        await ImportAsync(admin, Pack("notas-dos", new { item = Supplies, description = new[] { "Texto del segundo paquete." } }));
        Assert.Equal(["Texto del segundo paquete."], (await ItemAsync(admin, Supplies)).Description);
        await factory.WithDbAsync(async db =>
        {
            var rows = await db.Set<ItemDescriptionOverride>().Where(x => x.ItemIndex == Supplies).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(srdSupplies, r.OriginalDescription));
        });

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{PacksUrl}/notas-dos")).StatusCode);
        Assert.Equal(["Primer párrafo de ejemplo.", "Segundo párrafo de ejemplo."], (await ItemAsync(admin, Supplies)).Description);

        // Re-importing the SRD (a new dataset version) resets its items, and the overrides are applied again.
        await factory.WithDbAsync(db => db.ContentPacks.Where(x => x.Id == Dnd5eCatalogSources.Srd).ExecuteDeleteAsync());
        using (var scope = factory.Services.CreateScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IDnd5eCatalogSystem>().ImportBasePackAsync());
        }

        Assert.Equal(["Primer párrafo de ejemplo.", "Segundo párrafo de ejemplo."], (await ItemAsync(admin, Supplies)).Description);
        Assert.Equal(["Herramientas de herrero de ejemplo."], (await ItemAsync(admin, SmithsTools)).Description);
        await factory.WithDbAsync(async db =>
        {
            var row = await db.Set<ItemDescriptionOverride>().SingleAsync(x => x.ItemIndex == Supplies);
            Assert.Equal("notas-uno", row.Source);
            Assert.Equal(srdSupplies, row.OriginalDescription);
        });

        // Re-importing a pack without the override restores the item; deleting the last pack restores the other one.
        await ImportAsync(admin, Pack("notas-uno", new { item = SmithsTools, description = new[] { "Herrero, segunda versión." } }));
        Assert.Equal(srdSupplies, (await ItemAsync(admin, Supplies)).Description);
        Assert.Equal(["Herrero, segunda versión."], (await ItemAsync(admin, SmithsTools)).Description);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{PacksUrl}/notas-uno")).StatusCode);
        Assert.Equal(srdSmiths, (await ItemAsync(admin, SmithsTools)).Description);
        await factory.WithDbAsync(async db => Assert.False(await db.Set<ItemDescriptionOverride>().AnyAsync()));
    }

    [Fact]
    public async Task Invalid_overrides_are_reported_with_their_path()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = new
        {
            formatVersion = 3,
            id = "notas-malas",
            name = "Notas malas",
            version = "1",
            items = new object[]
            {
                new { index = "notas-malas-lupa", name = "Lupa de ejemplo", category = "Tool", description = new[] { "Texto de ejemplo." } },
            },
            itemOverrides = new object?[]
            {
                new { item = "no-existe", description = new[] { "Texto." } },
                new { item = "dagger", description = new[] { "Texto." } },
                new { item = "dagger", description = new[] { "Otro texto." } },
                new { item = "notas-malas-lupa", description = new[] { "Texto." } },
                new { item = "shield", description = Array.Empty<string>() },
                new { item = "torch", description = new[] { "Texto.", "  " } },
                new { item = "lute" },
                new { description = new[] { "Texto." } },
            },
        };

        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();

        Assert.Contains("itemOverrides[0].item: El objeto 'no-existe' no existe en el SRD ni en los paquetes alcanzables.", errors);
        Assert.DoesNotContain(errors, e => e.StartsWith("itemOverrides[1]", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("itemOverrides[2].item:", StringComparison.Ordinal) && e.Contains("repetido", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("itemOverrides[3].item:", StringComparison.Ordinal) && e.Contains("propio paquete", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("itemOverrides[4].description:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("itemOverrides[5].description[1]:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("itemOverrides[6].description:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("itemOverrides[7].item:", StringComparison.Ordinal));
    }

    [Fact]
    public void The_base_pack_cannot_override_items()
    {
        var context = new ContentPackContext(
            new Dictionary<string, string>(), new Dictionary<string, string>(), new Dictionary<string, string>())
        {
            BaseId = Dnd5eCatalogSources.Srd,
        };
        var validator = new ContentPackValidator(context);
        validator.Validate(new PackJson
        {
            FormatVersion = 3,
            Id = Dnd5eCatalogSources.Srd,
            Name = "Base",
            Version = "1",
            ItemOverrides = [new PackItemOverrideJson { Item = "dagger", Description = ["Texto."] }],
        });

        Assert.Contains("itemOverrides: El paquete base del sistema no sobreescribe objetos.", validator.Errors);
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    private static object Pack(string id, params object[] itemOverrides) => new
    {
        formatVersion = 3,
        id,
        name = $"Pack {id}",
        version = "1",
        itemOverrides,
    };

    private static StringContent Json(object value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static async Task<ContentPackImportResultDto> ImportAsync(HttpClient admin, object pack)
    {
        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ContentPackImportResultDto>())!;
    }

    /// <summary>The catalog detail of the SRD item <paramref name="index"/>.</summary>
    private static async Task<ItemDetailDto> ItemAsync(HttpClient client, string index)
    {
        var search = index == Supplies ? "Alchemist" : "Smith";
        var page = (await client.GetFromJsonAsync<PagedResult<ItemSummaryDto>>($"/api/v1/systems/dnd5e/catalog/items?search={search}&pageSize=50"))!;
        var summary = Assert.Single(page.Items, i => i.Index == index);
        return (await client.GetFromJsonAsync<ItemDetailDto>($"/api/v1/systems/dnd5e/catalog/items/{summary.Id}"))!;
    }

    private static async Task<CharacterDetailDto> DraftAsync(CampaignScenario s, string race, string subrace, string classIndex)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var response = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            raceIndex = race,
            subraceIndex = subrace,
            classes = new[] { new { classIndex, level = 1 } },
            baseAbilities = Scores,
            backgroundIndex = "acolyte",
        });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await s.Player.GetCharacterAsync(character.Id);
    }

    private static async Task<OriginChoicesDto> OriginAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/origin-choices");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<OriginChoicesDto>())!;
    }
}
