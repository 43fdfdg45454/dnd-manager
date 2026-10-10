using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the pack adds a sorcerer subclass with modifiers and Tides of Chaos on its features.</summary>
public sealed class FeatureModifierPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// A fictitious pack whose subclass features carry sheet modifiers and a Tides of Chaos resource that players may
/// restore by themselves (phase 25, block 7).
/// </summary>
public class FeatureModifierPackTests(FeatureModifierPackApiFactory factory) : IClassFixture<FeatureModifierPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string Subclass = "chispas-ejemplo-caos";

    private static object Pack(object level3Modifiers) => new
    {
        formatVersion = 3,
        id = "chispas-ejemplo",
        name = "Chispas de Ejemplo",
        version = "1.0.0",
        classes = new[]
        {
            new
            {
                extends = "sorcerer",
                subclasses = new object[]
                {
                    new
                    {
                        index = Subclass,
                        name = "Caos de ejemplo",
                        description = new[] { "Texto de ejemplo." },
                        levels = new object[]
                        {
                            new
                            {
                                level = 1,
                                features = new object[]
                                {
                                    new
                                    {
                                        index = "chispas-ejemplo-mareas",
                                        name = "Mareas de ejemplo",
                                        description = new[] { "Texto de ejemplo: ventaja una vez." },
                                        resource = new { key = "chispas-ejemplo-tides-of-chaos", name = "Mareas de ejemplo", max = 1, recharge = "LongRest" },
                                    },
                                },
                            },
                            new
                            {
                                level = 3,
                                features = new object[]
                                {
                                    new { index = "chispas-ejemplo-pies", name = "Pies ligeros", description = new[] { "Texto de ejemplo." }, modifiers = level3Modifiers },
                                },
                            },
                        },
                    },
                },
            },
        },
    };

    private static readonly object[] Swift =
    [
        new { kind = "InitiativeBonus", value = 1 },
        new { kind = "SpeedBonus", value = 10 },
    ];

    private async Task ImportAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        var import = await admin.PostAsync(PacksUrl, Json(Pack(Swift)));
        Assert.True(import.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, await import.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Feature_modifiers_apply_from_their_level_with_the_feature_in_the_breakdown()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        await s.EnablePacksAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Chispa");

        var second = await PatchAsync(s, hero.Id, level: 2, subclass: Subclass);
        var third = await PatchAsync(s, hero.Id, level: 3, subclass: Subclass);

        Assert.Equal(second.Sheet.Initiative + 1, third.Sheet.Initiative);
        Assert.Equal(second.Sheet.Speed + 10, third.Sheet.Speed);
        Assert.DoesNotContain(second.Sheet.Breakdowns["initiative"].Parts, p => p.Label == "Pies ligeros (nivel 3)");
        Assert.Contains(third.Sheet.Breakdowns["initiative"].Parts, p => (p.Source, p.Label, p.Value) == ("feature", "Pies ligeros (nivel 3)", 1));
        Assert.Contains(third.Sheet.Breakdowns["speed"].Parts, p => (p.Source, p.Label, p.Value) == ("feature", "Pies ligeros (nivel 3)", 10));
        Assert.Equal(third.Sheet.Speed, third.Sheet.Breakdowns["speed"].Total);

        // Another subclass loses them.
        var other = await PatchAsync(s, hero.Id, level: 3, subclass: "draconic");
        Assert.Equal(second.Sheet.Initiative, other.Sheet.Initiative);
        Assert.DoesNotContain(other.Sheet.Breakdowns["speed"].Parts, p => p.Label == "Pies ligeros (nivel 3)");
    }

    [Fact]
    public async Task A_player_restores_tides_of_chaos_by_themselves()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        await s.EnablePacksAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Marea");
        var detail = await PatchAsync(s, hero.Id, level: 3, subclass: Subclass);
        var tides = Assert.Single(detail.Resources, r => r.Key == "chispas-ejemplo-tides-of-chaos");
        Assert.True(tides.IsAuto);
        var url = $"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/resources/{tides.Id}";
        Assert.Equal(HttpStatusCode.OK, (await s.Player.Client.PostAsJsonAsync($"{url}/spend", new { amount = 1 })).StatusCode);

        var restore = await s.Player.Client.PostAsJsonAsync($"{url}/restore", new { amount = 1 });

        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        var restored = (await restore.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal(0, Assert.Single(restored.Resources, r => r.Id == tides.Id).Used);
    }

    [Theory]
    [InlineData("[{\"kind\":\"Luck\",\"value\":1}]", "modifiers[0].kind")]
    [InlineData("[{\"kind\":\"SpeedBonus\"}]", "modifiers[0].value")]
    [InlineData("[{\"kind\":\"ArmorClassBonus\",\"value\":1,\"condition\":\"raining\"}]", "modifiers[0].condition")]
    [InlineData("[{\"kind\":\"SpeedBonus\",\"value\":1},{\"kind\":\"SpeedBonus\",\"value\":1},{\"kind\":\"SpeedBonus\",\"value\":1},{\"kind\":\"SpeedBonus\",\"value\":1},{\"kind\":\"SpeedBonus\",\"value\":1},{\"kind\":\"SpeedBonus\",\"value\":1},{\"kind\":\"SpeedBonus\",\"value\":1},{\"kind\":\"SpeedBonus\",\"value\":1},{\"kind\":\"SpeedBonus\",\"value\":1},{\"kind\":\"SpeedBonus\",\"value\":1},{\"kind\":\"SpeedBonus\",\"value\":1}]", "modifiers")]
    public async Task Invalid_feature_modifiers_are_reported_with_their_path(string modifiers, string path)
    {
        var admin = await factory.CreateAdminClientAsync();

        var response = await admin.PostAsync(PacksUrl, Json(Pack(JsonSerializer.Deserialize<JsonElement>(modifiers))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains($"levels[1].features[0].{path}", await response.Content.ReadAsStringAsync());
    }

    private static StringContent Json(object pack) => new(JsonSerializer.Serialize(pack), Encoding.UTF8, "application/json");

    private static async Task<CharacterDetailDto> PatchAsync(CampaignScenario s, Guid id, int level, string? subclass)
    {
        var patch = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/sheet", new
        {
            classes = new[] { new { classIndex = "sorcerer", subclassIndex = subclass, level } },
            baseAbilities = new { str = 8, dex = 14, con = 14, @int = 10, wis = 10, cha = 16 },
            applyRacialBonuses = false,
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        return (await patch.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }
}
