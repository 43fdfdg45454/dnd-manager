using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using static OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks.CreationPack;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the fictitious creation pack of phase 35B1.</summary>
public sealed class ContentPackCreationApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Creation and level-up gaps of the format 3 (phase 35B1) with the fictitious pack of <see cref="CreationPack"/>:
/// exotic categories and new proficiencies, multiclass prerequisites, option prerequisites, granted and nested
/// choices, new casting progressions and auxiliary levels.
/// </summary>
public class ContentPackCreationTests(ContentPackCreationApiFactory factory) : IClassFixture<ContentPackCreationApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string CatalogUrl = "/api/v1/systems/dnd5e/catalog";

    private static readonly SemaphoreSlim ImportLock = new(1, 1);

    // ---- H07: exotic categories, firearms, bombs ----------------------------------------------------------

    [Fact]
    public async Task Exotic_weapons_firearms_and_bombs_need_their_own_proficiencies()
    {
        var admin = await factory.CreateAdminClientAsync();
        await ImportAsync(admin);

        var scythe = await ItemAsync(admin, "guadaña doble");
        Assert.Equal("Exotic Melee", scythe.Subcategory);
        var armor = await ItemAsync(admin, "coraza de runas");
        Assert.Equal("Medium Armor", armor.Subcategory);
        Assert.Contains("exotic", armor.Properties);

        var s = await ScenarioAsync();
        // Martial weapons do not cover exotic weapons, firearms or bombs.
        var martial = await ArmedAsync(s, "martial-weapons");
        Assert.All(martial.Combat.Attacks.Where(a => a.ItemId is not null), a => Assert.DoesNotContain(a.AttackBreakdown.Parts, p => p.Source == "proficiency"));

        var expert = await ArmedAsync(s, "exotic-weapons", "exotic-firearms", "bombs");
        Assert.All(expert.Combat.Attacks.Where(a => a.ItemId is not null), a => Assert.Contains(a.AttackBreakdown.Parts, p => p.Source == "proficiency"));

        // A martial firearm proficiency does not cover an exotic firearm.
        var wrongGroup = await ArmedAsync(s, "martial-firearms");
        Assert.DoesNotContain(wrongGroup.Combat.Attacks.Single(a => a.Name == "Trabuco de bruma").AttackBreakdown.Parts, p => p.Source == "proficiency");
    }

    [Fact]
    public async Task Exotic_shields_and_unknown_weapon_categories_are_rejected()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Renamed(Build(), "vacio-malo-h07");
        pack["items"]![0]!["weapon"]!["category"] = "legendaria";
        pack["items"]![3]!["armor"]!["category"] = "shield";

        var errors = await ImportErrorsAsync(admin, pack);

        Assert.Contains(errors, e => e.StartsWith("items[0].weapon.category:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("items[3].armor.exotic:", StringComparison.Ordinal));
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    /// <summary>Imports the pack once per database.</summary>
    private async Task ImportAsync(HttpClient admin)
    {
        await ImportLock.WaitAsync();
        try
        {
            if ((await GetAsync<List<ContentPackDto>>(admin, PacksUrl)).Any(p => p.Id == Id))
            {
                return;
            }

            var response = await admin.PostAsync(PacksUrl, Json(Build()));
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            ImportLock.Release();
        }
    }

    private async Task<CampaignScenario> ScenarioAsync()
    {
        await ImportAsync(await factory.CreateAdminClientAsync());
        var s = await factory.CreateCampaignScenarioAsync();
        await s.EnablePacksAsync(Id);
        return s;
    }

    private static async Task<ItemDetailDto> ItemAsync(HttpClient client, string name)
    {
        var summary = Assert.Single((await GetAsync<PagedResult<ItemSummaryDto>>(client, $"{CatalogUrl}/items?search={Uri.EscapeDataString(name)}")).Items);
        return await GetAsync<ItemDetailDto>(client, $"{CatalogUrl}/items/{summary.Id}");
    }

    /// <summary>A fighter with the given weapon proficiencies carrying the pack's scythe, bomb and firearm equipped.</summary>
    private static async Task<CharacterDetailDto> ArmedAsync(CampaignScenario s, params string[] weapons)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, "Armado");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", level = 1 } },
            baseAbilities = new { str = 14, dex = 14, con = 12, @int = 10, wis = 10, cha = 10 },
            applyRacialBonuses = false,
            proficiencies = weapons.Select(key => new { type = "Weapon", key, expertise = false }).ToArray(),
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        foreach (var name in new[] { "Guadaña doble", "Petardo del vacío", "Trabuco de bruma" })
        {
            var item = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync(name), quantity = 1 });
            Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, item.Id, new { equipped = true })).StatusCode);
        }

        return await s.Player.GetCharacterAsync(character.Id);
    }

    private static StringContent Json(JsonNode pack) => new(pack.ToJsonString(), Encoding.UTF8, "application/json");

    private static async Task<List<string>> ImportErrorsAsync(HttpClient admin, JsonNode pack)
    {
        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
