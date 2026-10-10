using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the pack adds height and weight tables to races and subraces.</summary>
public sealed class HeightWeightPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Phase 29, block 3: height and weight of a character (free data without mechanical effect) and the random tables of
/// races and subraces that content packs add (<c>heightWeight</c>). Fictitious values throughout.
/// </summary>
public class HeightWeightPackTests(HeightWeightPackApiFactory factory) : IClassFixture<HeightWeightPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string PackId = "talla-ejemplo";

    private static readonly object Pack = new
    {
        formatVersion = 3,
        id = PackId,
        name = "Tallas de ejemplo",
        version = "1.0.0",
        races = new object[]
        {
            new
            {
                extends = "halfling",
                heightWeight = new { baseHeightInches = 30, heightModifier = "1d6", baseWeightPounds = 40, weightModifier = "1" },
                subraces = new[]
                {
                    new
                    {
                        index = "talla-ejemplo-tall-folk",
                        name = "Tall Folk",
                        description = "Texto de ejemplo.",
                        heightWeight = new { baseHeightInches = 40, heightModifier = "2D6", baseWeightPounds = 60, weightModifier = "1d3" },
                    },
                },
            },
            new
            {
                index = "talla-ejemplo-giant-folk",
                name = "Giant Folk",
                speed = 30,
                size = "Medium",
                heightWeight = new { baseHeightInches = 70, heightModifier = "3d4", baseWeightPounds = 200, weightModifier = "2d3" },
            },
        },
    };

    [Fact]
    public async Task A_pack_adds_height_and_weight_tables_to_an_extended_race_its_subrace_and_a_new_race()
    {
        var admin = await factory.CreateAdminClientAsync();
        var import = await admin.PostAsync(PacksUrl, Json(Pack));
        Assert.True(import.StatusCode == HttpStatusCode.Created, await import.Content.ReadAsStringAsync());

        var s = await factory.CreateCampaignScenarioAsync();
        var halfling = (await s.Player.Client.GetFromJsonAsync<RaceDetailDto>("/api/v1/systems/dnd5e/catalog/races/halfling"))!;
        Assert.Equal(new HeightWeightDto(30, "1d6", 40, "1"), halfling.HeightWeight);
        var tallFolk = Assert.Single(halfling.Subraces, r => r.Index == "talla-ejemplo-tall-folk");
        Assert.Equal(new HeightWeightDto(40, "2d6", 60, "1d3"), tallFolk.HeightWeight);
        Assert.All(halfling.Subraces.Where(r => r.Source == "srd"), r => Assert.Null(r.HeightWeight));

        var giant = (await s.Player.Client.GetFromJsonAsync<RaceDetailDto>("/api/v1/systems/dnd5e/catalog/races/talla-ejemplo-giant-folk"))!;
        Assert.Equal(new HeightWeightDto(70, "3d4", 200, "2d3"), giant.HeightWeight);

        // The SRD has no table.
        var elf = (await s.Player.Client.GetFromJsonAsync<RaceDetailDto>("/api/v1/systems/dnd5e/catalog/races/elf"))!;
        Assert.Null(elf.HeightWeight);
        Assert.All(elf.Subraces, r => Assert.Null(r.HeightWeight));
    }

    [Fact]
    public async Task Invalid_height_and_weight_tables_are_reported()
    {
        var admin = await factory.CreateAdminClientAsync();
        var invalid = new
        {
            formatVersion = 3,
            id = "talla-erronea",
            name = "Tallas erróneas",
            version = "1.0.0",
            races = new object[]
            {
                new
                {
                    index = "talla-erronea-folk",
                    name = "Wrong Folk",
                    speed = 30,
                    size = "Medium",
                    heightWeight = new { baseHeightInches = 0, heightModifier = "2d", baseWeightPounds = 5000, weightModifier = "x2" },
                    subraces = new[]
                    {
                        new { index = "talla-erronea-sub", name = "Wrong Sub", heightWeight = new { baseHeightInches = 40, heightModifier = "20d6", baseWeightPounds = 60 } },
                    },
                },
            },
        };

        var errors = await ReadErrorsAsync(await admin.PostAsync(PacksUrl, Json(invalid)));

        Assert.Contains("races[0].heightWeight.baseHeightInches: Debe estar entre 1 y 120.", errors);
        Assert.Contains("races[0].heightWeight.baseWeightPounds: Debe estar entre 1 y 1000.", errors);
        Assert.Contains(errors, e => e.StartsWith("races[0].heightWeight.heightModifier: Expresión no válida", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("races[0].heightWeight.weightModifier: Expresión no válida", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("races[0].subraces[0].heightWeight.heightModifier: Expresión no válida", StringComparison.Ordinal));
        Assert.Contains("races[0].subraces[0].heightWeight.weightModifier: Campo obligatorio.", errors);
    }

    [Fact]
    public async Task The_owner_of_an_active_character_changes_height_and_weight_without_approval()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);
        var url = $"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/sheet";
        Assert.Equal(((int?)null, (int?)null), (hero.HeightInches, hero.WeightPounds));

        var direct = await s.Player.Client.PatchAsJsonAsync(url, new { heightInches = 67, weightPounds = 165 });
        Assert.True(direct.StatusCode == HttpStatusCode.OK, await direct.Content.ReadAsStringAsync());
        var detail = (await direct.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal((67, 165), (detail.HeightInches!.Value, detail.WeightPounds!.Value));

        // Mixed with another field: height and weight apply now; the rest goes to the DM without them.
        var mixed = await s.Player.Client.PatchAsJsonAsync(url, new { heightInches = 70, ideals = "Un ideal de ejemplo." });
        Assert.Equal(HttpStatusCode.Accepted, mixed.StatusCode);
        var request = (await mixed.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        Assert.False(request.Payload.TryGetProperty("heightInches", out _));
        detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal((70, 165, ""), (detail.HeightInches!.Value, detail.WeightPounds!.Value, detail.Ideals));

        // An explicit null clears; absent keeps.
        Assert.Equal(HttpStatusCode.OK, (await s.Player.Client.PatchAsJsonAsync(url, new { weightPounds = (int?)null })).StatusCode);
        detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal((70, (int?)null), (detail.HeightInches!.Value, detail.WeightPounds));

        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PatchAsJsonAsync(url, new { heightInches = 201 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PatchAsJsonAsync(url, new { weightPounds = 0 })).StatusCode);
    }

    [Fact]
    public async Task A_character_is_created_with_height_and_weight()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var created = await s.Player.Client.PostAsJsonAsync(
            $"/api/v1/campaigns/{s.CampaignId}/characters",
            new { name = "Alta", heightInches = 62, weightPounds = 120 });
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var hero = (await created.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal((62, 120), (hero.HeightInches!.Value, hero.WeightPounds!.Value));

        var invalid = await s.Player.Client.PostAsJsonAsync(
            $"/api/v1/campaigns/{s.CampaignId}/characters",
            new { name = "Imposible", heightInches = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    private static StringContent Json(object value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static async Task<List<string>> ReadErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        return problem.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
    }
}
