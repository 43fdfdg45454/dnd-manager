using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: roll tables and pack backgrounds are global to the instance.</summary>
public sealed class RollTablePackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>Background personality, optional tables and generic roll tables of fictitious content packs (phase 22).</summary>
public class RollTablePackTests(RollTablePackApiFactory factory) : IClassFixture<RollTablePackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string RollTablesUrl = "/api/v1/catalog/roll-tables";

    [Fact]
    public async Task A_pack_background_brings_its_personality_and_optional_table()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = new
        {
            id = "tablas-trasfondo",
            name = "Tablas de trasfondo",
            version = "1",
            backgrounds = new[]
            {
                new
                {
                    index = "tablas-trasfondo-cartografo",
                    name = "Cartógrafo de ejemplo",
                    personality = new
                    {
                        traits = new[] { "Rasgo A.", "Rasgo B.", "Rasgo C." },
                        ideals = new object[] { new { text = "Ideal A.", alignment = "Lawful" }, new { text = "Ideal B." } },
                        bonds = new[] { "Vínculo A." },
                        flaws = new[] { "Defecto A.", "Defecto B." },
                    },
                    optionalTables = new[] { new { key = "specialty", name = "Especialidad", entries = new[] { "Mapas de costa", "Mapas de cuevas" } } },
                },
            },
        };

        var created = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var backgrounds = await admin.GetFromJsonAsync<List<BackgroundDto>>("/api/v1/catalog/backgrounds");
        var background = Assert.Single(backgrounds!, b => b.Index == "tablas-trasfondo-cartografo");
        Assert.Equal(["Rasgo A.", "Rasgo B.", "Rasgo C."], background.Personality!.Traits);
        Assert.Equal([("Ideal A.", (string?)"Lawful"), ("Ideal B.", null)], background.Personality.Ideals.Select(i => (i.Text, i.Alignment)));
        var table = Assert.Single(background.OptionalTables);
        Assert.Equal(("specialty", "Especialidad", 2), (table.Key, table.Name, table.Entries.Count));
    }

    [Fact]
    public async Task Invalid_personality_and_optional_tables_are_reported_with_their_path()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = new
        {
            id = "tablas-malas",
            name = "Tablas malas",
            version = "1",
            backgrounds = new[]
            {
                new
                {
                    index = "tablas-malas-uno",
                    name = "Uno",
                    personality = new
                    {
                        traits = Array.Empty<string>(),
                        ideals = new object[] { new { alignment = "Any" } },
                        bonds = Enumerable.Range(1, 21).Select(i => $"Vínculo {i}.").ToArray(),
                        flaws = new[] { new string('x', 501) },
                    },
                    optionalTables = new object[]
                    {
                        new { key = "Mal Clave", name = "Mala", entries = new[] { "a" } },
                        new { key = "vacia", name = "Vacía", entries = Array.Empty<string>() },
                        new { key = "doble", name = "Doble", entries = new[] { "a" } },
                        new { key = "doble", name = "Doble", entries = new[] { "a" } },
                    },
                },
            },
        };

        var errors = await ErrorsAsync(admin, pack);
        Assert.Contains(errors, e => e.StartsWith("backgrounds[0].personality.traits:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("backgrounds[0].personality.ideals[0].text:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("backgrounds[0].personality.bonds:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("backgrounds[0].personality.flaws[0]:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("backgrounds[0].optionalTables[0].key:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("backgrounds[0].optionalTables[1].entries:", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, e => e.StartsWith("backgrounds[0].optionalTables[2]", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("backgrounds[0].optionalTables[3].key:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Roll_tables_are_served_by_subclass_and_the_last_imported_pack_wins_each_key()
    {
        var admin = await factory.CreateAdminClientAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        Assert.Empty((await s.Player.Client.GetFromJsonAsync<List<RollTableDto>>($"{RollTablesUrl}?subclass=oleada-uno-salvaje"))!);

        var first = new
        {
            id = "oleada-uno",
            name = "Oleada uno",
            version = "1",
            classesExtended = new[]
            {
                new { classIndex = "sorcerer", subclasses = new[] { new { index = "oleada-uno-salvaje", name = "Magia de ejemplo" } } },
            },
            rollTables = new object[]
            {
                new
                {
                    key = "surge-example",
                    name = "Oleada de ejemplo",
                    dice = "d100",
                    subclassIndex = "oleada-uno-salvaje",
                    entries = new object[]
                    {
                        new { from = 1, to = 2, text = "Efecto uno." },
                        new { from = 3, to = 99, text = "Efecto medio." },
                        new { from = 100, text = "Efecto final." },
                    },
                },
                new { key = "general-example", name = "Tabla general", dice = "d4", entries = new object[] { new { from = 1, to = 4, text = "Todo." } } },
            },
        };
        var created = await admin.PostAsync(PacksUrl, Json(first));
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        using (var result = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
        {
            Assert.Equal(2, result.RootElement.GetProperty("counts").GetProperty("rollTables").GetInt32());
        }

        var tables = (await s.Player.Client.GetFromJsonAsync<List<RollTableDto>>($"{RollTablesUrl}?subclass=oleada-uno-salvaje"))!;
        var surge = Assert.Single(tables);
        Assert.Equal(("surge-example", "d100", "sorcerer", "oleada-uno"), (surge.Key, surge.Dice, surge.ClassIndex, surge.Source));
        Assert.Equal([(1, 2), (3, 99), (100, 100)], surge.Entries.Select(e => (e.From, e.To)));
        Assert.Equal(2, (await s.Player.Client.GetFromJsonAsync<List<RollTableDto>>(RollTablesUrl))!.Count);

        // Another pack redefines the same key for the subclass of the first pack.
        var second = new
        {
            id = "oleada-dos",
            name = "Oleada dos",
            version = "1",
            rollTables = new[]
            {
                new { key = "surge-example", name = "Oleada nueva", dice = "d20", subclassIndex = "oleada-uno-salvaje", entries = new object[] { new { from = 1, to = 20, text = "Otro efecto." } } },
            },
        };
        var replaced = await admin.PostAsync(PacksUrl, Json(second));
        Assert.True(replaced.StatusCode == HttpStatusCode.Created, await replaced.Content.ReadAsStringAsync());
        surge = Assert.Single((await s.Player.Client.GetFromJsonAsync<List<RollTableDto>>($"{RollTablesUrl}?subclass=oleada-uno-salvaje"))!);
        Assert.Equal(("Oleada nueva", "d20"), (surge.Name, surge.Dice));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{PacksUrl}/oleada-dos")).StatusCode);
        surge = Assert.Single((await s.Player.Client.GetFromJsonAsync<List<RollTableDto>>($"{RollTablesUrl}?subclass=oleada-uno-salvaje"))!);
        Assert.Equal("Oleada de ejemplo", surge.Name);
    }

    [Fact]
    public async Task Roll_tables_with_gaps_overlaps_or_bad_references_are_rejected()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = new
        {
            id = "oleada-mala",
            name = "Oleada mala",
            version = "1",
            rollTables = new object[]
            {
                new { key = "hueco", name = "Hueco", dice = "d10", entries = new object[] { new { from = 1, to = 4, text = "a" }, new { from = 6, to = 10, text = "b" } } },
                new { key = "solape", name = "Solape", dice = "d10", entries = new object[] { new { from = 1, to = 6, text = "a" }, new { from = 5, to = 10, text = "b" } } },
                new { key = "corta", name = "Corta", dice = "d6", entries = new object[] { new { from = 1, to = 5, text = "a" } } },
                new { key = "dado", name = "Dado", dice = "d7", entries = new object[] { new { from = 1, to = 7, text = "a" } } },
                new { key = "fuera", name = "Fuera", dice = "d4", entries = new object[] { new { from = 1, to = 5, text = "a" } } },
                new { key = "sub", name = "Sub", dice = "d4", subclassIndex = "no-existe", entries = new object[] { new { from = 1, to = 4, text = "a" } } },
                new { key = "clase", name = "Clase", dice = "d4", classIndex = "wizard", subclassIndex = "draconic", entries = new object[] { new { from = 1, to = 4, text = "a" } } },
                new { key = "hueco", name = "Repetida", dice = "d4", entries = new object[] { new { from = 1, to = 4, text = "a" } } },
            },
        };

        var errors = await ErrorsAsync(admin, pack);
        Assert.Contains(errors, e => e.StartsWith("rollTables[0].entries:", StringComparison.Ordinal) && e.Contains("5", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("rollTables[1].entries[1]:", StringComparison.Ordinal) && e.Contains("solapa", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("rollTables[2].entries:", StringComparison.Ordinal) && e.Contains("6", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("rollTables[3].dice:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("rollTables[4].entries[0].to:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("rollTables[5].subclassIndex:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("rollTables[6].subclassIndex:", StringComparison.Ordinal) && e.Contains("sorcerer", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("rollTables[7].key:", StringComparison.Ordinal));
    }

    private static async Task<List<string>> ErrorsAsync(HttpClient admin, object pack)
    {
        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    private static StringContent Json(object value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
