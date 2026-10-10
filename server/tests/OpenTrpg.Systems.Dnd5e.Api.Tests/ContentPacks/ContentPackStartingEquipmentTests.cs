using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Structured starting equipment of the backgrounds of a content pack (phase 17).</summary>
public class ContentPackStartingEquipmentTests(ContentPackApiFactory factory) : IClassFixture<ContentPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";

    [Fact]
    public async Task A_background_with_valid_starting_equipment_resolves_srd_and_pack_items()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = new
        {
            formatVersion = 3,
            id = "equipo-ejemplo",
            name = "Equipo de ejemplo",
            version = "1",
            items = new object[] { new { index = "equipo-ejemplo-amuleto", name = "Amuleto de ejemplo", category = "AdventuringGear" } },
            backgrounds = new object[]
            {
                new
                {
                    index = "equipo-ejemplo-peregrino",
                    name = "Peregrino de ejemplo",
                    startingEquipmentText = "Un bastón, un amuleto y 10 po",
                    startingEquipment = new
                    {
                        @fixed = new object[]
                        {
                            new { item = "quarterstaff", quantity = 1 },
                            new { item = "equipo-ejemplo-amuleto" },
                            new { item = "explorers-pack" },
                        },
                        choices = new object[]
                        {
                            new
                            {
                                description = "(a) un arma marcial o (b) dos dagas",
                                options = new object[]
                                {
                                    new { label = "Cualquier arma marcial", category = "martial-weapons" },
                                    new { label = "Dos dagas", items = new[] { new { item = "dagger", quantity = 2 } } },
                                },
                            },
                        },
                        fixedGoldCp = 1000,
                    },
                },
                new { index = "equipo-ejemplo-sin-equipo", name = "Sin equipo de ejemplo", startingEquipmentText = "Lo que lleve puesto" },
            },
        };

        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        var backgrounds = await (await factory.CreateSignedInUserAsync()).Client.GetFromJsonAsync<List<BackgroundDto>>("/api/v1/systems/dnd5e/catalog/backgrounds");
        var pilgrim = Assert.Single(backgrounds!, b => b.Index == "equipo-ejemplo-peregrino");
        var equipment = pilgrim.StartingEquipment!;

        Assert.Equal(["quarterstaff", "equipo-ejemplo-amuleto", "explorers-pack"], equipment.Fixed.Select(i => i.Item));
        Assert.All(equipment.Fixed, i => Assert.NotNull(i.TemplateId));
        Assert.Equal("Amuleto de ejemplo", equipment.Fixed[1].Name);
        Assert.Contains(equipment.Fixed[2].Contents!, c => c.Item == "bedroll");
        Assert.Equal(1000, equipment.FixedGoldCp);
        Assert.Null(equipment.Gold);

        var choice = Assert.Single(equipment.Choices);
        Assert.Equal(1, choice.Choose);
        Assert.Equal(new StartingCategoryPickDto("martial-weapons", "Martial Weapons", 1), Assert.Single(choice.Options[0].Categories));
        Assert.Equal(("dagger", 2), (choice.Options[1].Items[0].Item, choice.Options[1].Items[0].Quantity));

        // Backgrounds without structured equipment keep only their text.
        var plain = Assert.Single(backgrounds!, b => b.Index == "equipo-ejemplo-sin-equipo");
        Assert.Null(plain.StartingEquipment);
        Assert.Equal("Lo que lleve puesto", plain.StartingEquipmentText);
    }

    [Fact]
    public async Task Invalid_starting_equipment_is_reported_with_its_path()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = new
        {
            formatVersion = 3,
            id = "equipo-malo",
            name = "Equipo malo",
            version = "1",
            backgrounds = new object[]
            {
                new
                {
                    index = "equipo-malo-errante",
                    name = "Errante",
                    startingEquipment = new
                    {
                        @fixed = new object[] { new { item = "no-existe" }, new { item = "dagger", quantity = 0 } },
                        choices = new object[]
                        {
                            new
                            {
                                choose = 3,
                                options = new object[]
                                {
                                    new { label = "Algo", categories = new[] { new { category = "armas-raras", choose = 1 } } },
                                    new { label = "Nada" },
                                },
                            },
                            new { description = "Vacía", options = Array.Empty<object>() },
                        },
                        gold = new { dice = "5d4", multiplier = 10 },
                        fixedGoldCp = -5,
                    },
                },
            },
        };

        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();

        const string path = "backgrounds[0].startingEquipment";
        Assert.Contains(errors, e => e.StartsWith($"{path}.fixed[0].item:", StringComparison.Ordinal) && e.Contains("no-existe", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"{path}.fixed[1].quantity:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"{path}.choices[0].choose:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"{path}.choices[0].options[0].categories[0].category:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"{path}.choices[0].options[1]:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"{path}.choices[1].options:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"{path}.gold:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"{path}.fixedGoldCp:", StringComparison.Ordinal));
    }

    private static StringContent Json(object value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");
}
