using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the trinket table is global to the instance.</summary>
public sealed class TrinketPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>Trinket tables of fictitious content packs and the trinket of a draft character (phase 21).</summary>
public class TrinketPackTests(TrinketPackApiFactory factory) : IClassFixture<TrinketPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string TrinketsUrl = "/api/v1/systems/dnd5e/catalog/trinkets";

    [Fact]
    public async Task Trinket_tables_resolve_their_items_and_the_last_imported_pack_wins_each_roll()
    {
        var admin = await factory.CreateAdminClientAsync();
        var s = await factory.CreateCampaignScenarioAsync();

        // Pure SRD instance: no table.
        Assert.Empty((await s.Player.Client.GetFromJsonAsync<List<TrinketDto>>(TrinketsUrl))!);

        var first = Pack("baratijas-uno", "baratijas-uno-canica", "Canica de ejemplo", new { roll = 1, item = "baratijas-uno-canica" }, new { roll = 2, item = "dagger" });
        var created = await admin.PostAsync(PacksUrl, Json(first));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        using (var result = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            Assert.Equal(2, result.RootElement.GetProperty("counts").GetProperty("trinkets").GetInt32());
        }

        var table = (await s.Player.Client.GetFromJsonAsync<List<TrinketDto>>(TrinketsUrl))!;
        Assert.Equal([1, 2], table.Select(t => t.Roll));
        Assert.Equal(("baratijas-uno-canica", "Canica de ejemplo", "Texto de ejemplo."), (table[0].Index, table[0].Name, table[0].Description));
        Assert.Equal("dagger", table[1].Index);

        // A second pack redefines roll 2; re-importing the first one takes it back.
        var second = Pack("baratijas-dos", "baratijas-dos-llave", "Llave de ejemplo", new { roll = 2, item = "baratijas-dos-llave" });
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsync(PacksUrl, Json(second))).StatusCode);
        table = (await s.Player.Client.GetFromJsonAsync<List<TrinketDto>>(TrinketsUrl))!;
        Assert.Equal(["baratijas-uno-canica", "baratijas-dos-llave"], table.Select(t => t.Index));

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsync(PacksUrl, Json(first))).StatusCode);
        table = (await s.Player.Client.GetFromJsonAsync<List<TrinketDto>>(TrinketsUrl))!;
        Assert.Equal(["baratijas-uno-canica", "dagger"], table.Select(t => t.Index));

        // The trinket of a draft goes to its inventory: the catalog item, or a custom "Baratija" without a table entry.
        await s.EnablePacksAsync("baratijas-uno", "baratijas-dos");
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId);
        var marble = await s.Player.AddItemAsync(hero.Id, new { templateId = table[0].TemplateId, quantity = 1 });
        Assert.Equal("Canica de ejemplo", marble.Effective.Name);
        var custom = await s.Player.AddItemAsync(hero.Id, new { quantity = 1, overrides = new { name = "Baratija", description = new[] { "Un botón de ejemplo." } } });
        Assert.Equal(("Baratija", (Guid?)null), (custom.Effective.Name, custom.TemplateId));

        // Deleting a pack removes its entries.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{PacksUrl}/baratijas-uno")).StatusCode);
        table = (await s.Player.Client.GetFromJsonAsync<List<TrinketDto>>(TrinketsUrl))!;
        Assert.Equal(["baratijas-dos-llave"], table.Select(t => t.Index));
    }

    [Fact]
    public async Task Invalid_trinket_tables_are_reported_with_their_path()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = new
        {
            formatVersion = 3,
            id = "baratijas-malas",
            name = "Baratijas malas",
            version = "1",
            trinkets = new object?[]
            {
                new { roll = 0, item = "dagger" },
                new { roll = 101, item = "dagger" },
                new { roll = 5, item = "dagger" },
                new { roll = 5, item = "dagger" },
                new { roll = 6, item = "no-existe" },
                new { item = "dagger" },
                new { roll = 7 },
            },
        };

        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();

        Assert.Contains(errors, e => e.StartsWith("trinkets[0].roll:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("trinkets[1].roll:", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, e => e.StartsWith("trinkets[2]", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("trinkets[3].roll:", StringComparison.Ordinal) && e.Contains("repetida", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("trinkets[4].item:", StringComparison.Ordinal) && e.Contains("no-existe", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("trinkets[5].roll:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("trinkets[6].item:", StringComparison.Ordinal));
    }

    private static object Pack(string id, string itemIndex, string itemName, params object[] trinkets) => new
    {
        formatVersion = 3,
        id,
        name = $"Pack {id}",
        version = "1",
        items = new object[]
        {
            new { index = itemIndex, name = itemName, category = "Other", subcategory = "Trinket", description = new[] { "Texto de ejemplo." } },
        },
        trinkets,
    };

    private static StringContent Json(object value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
