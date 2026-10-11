using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Systems.Dnd5e.Application.Characters;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: a fictitious fighter subclass that enhances items, and fictitious grafts.</summary>
public sealed class ItemEnhancementsPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Gaps H31 and H31b of phase 35B3 with a fictitious pack: enhancement options applied to one inventory entry by a
/// player whose character has the feature (a change request without it), shown in the attack and armor class breakdowns;
/// and grafts, one equipped per body slot.
/// </summary>
public class ItemEnhancementsPackTests(ItemEnhancementsPackApiFactory factory) : IClassFixture<ItemEnhancementsPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string Id = "forja-ejemplo";
    private const string Subclass = "forja-ejemplo-senda";
    private const string Sharp = "forja-ejemplo-afilado";
    private const string Burning = "forja-ejemplo-ardiente";
    private const string Reinforced = "forja-ejemplo-reforzado";

    private static readonly SemaphoreSlim ImportLock = new(1, 1);

    private static JsonObject Pack() => new()
    {
        ["formatVersion"] = 3,
        ["id"] = Id,
        ["name"] = "Forja de ejemplo",
        ["version"] = "1.0.0",
        ["classes"] = new JsonArray(new JsonObject
        {
            ["extends"] = "fighter",
            ["subclasses"] = new JsonArray(new JsonObject
            {
                ["index"] = Subclass,
                ["name"] = "Senda de la forja",
                ["flavor"] = "Arquetipo marcial",
                ["description"] = new JsonArray("Texto de ejemplo."),
                ["levels"] = new JsonArray(new JsonObject
                {
                    ["level"] = 3,
                    ["features"] = new JsonArray(new JsonObject
                    {
                        ["index"] = "forja-ejemplo-mano-experta",
                        ["name"] = "Mano experta",
                        ["description"] = new JsonArray("Texto de ejemplo: mejoras tus armas y armaduras."),
                        ["itemEnhancement"] = new JsonObject
                        {
                            ["optionSets"] = new JsonArray("forja-ejemplo-mejoras"),
                            ["maxPerItemByLevel"] = new JsonObject { ["3"] = 1, ["5"] = 2 },
                        },
                    }),
                }),
            }),
        }),
        ["optionSets"] = new JsonArray(new JsonObject
        {
            ["setId"] = "forja-ejemplo-mejoras",
            ["name"] = "Mejoras de ejemplo",
            ["options"] = new JsonArray(
                new JsonObject
                {
                    ["index"] = Sharp,
                    ["name"] = "Afilado",
                    ["description"] = new JsonArray("Texto de ejemplo."),
                    ["itemEffects"] = new JsonObject
                    {
                        ["appliesTo"] = new JsonArray("weapon"),
                        ["attackBonus"] = 1,
                        ["damageStep"] = 1,
                        ["addProperties"] = new JsonArray("finesse"),
                    },
                },
                new JsonObject
                {
                    ["index"] = Burning,
                    ["name"] = "Ardiente",
                    ["description"] = new JsonArray("Texto de ejemplo."),
                    ["prerequisites"] = new JsonObject { ["minLevel"] = 5, ["options"] = new JsonArray(Sharp) },
                    ["itemEffects"] = new JsonObject { ["appliesTo"] = new JsonArray("weapon"), ["damageType"] = "fire", ["damageBonus"] = 1 },
                },
                new JsonObject
                {
                    ["index"] = Reinforced,
                    ["name"] = "Reforzado",
                    ["description"] = new JsonArray("Texto de ejemplo."),
                    ["modifiers"] = new JsonArray(new JsonObject { ["kind"] = "SaveBonus", ["target"] = "con", ["value"] = 1 }),
                    ["itemEffects"] = new JsonObject
                    {
                        ["appliesTo"] = new JsonArray("armor", "shield"),
                        ["armorClassBonus"] = 1,
                        ["addProperties"] = new JsonArray("forja-ejemplo-liviano"),
                    },
                }),
        }),
        ["items"] = new JsonArray(Graft("forja-ejemplo-brazo-de-hierro", "Brazo de hierro"), Graft("forja-ejemplo-brazo-de-cobre", "Brazo de cobre")),
        ["reference"] = new JsonObject
        {
            ["armorProperties"] = new JsonArray(new JsonObject { ["index"] = "forja-ejemplo-liviano", ["name"] = "Liviano", ["description"] = new JsonArray("Propiedad ficticia.") }),
        },
    };

    private static JsonObject Graft(string index, string name) => new()
    {
        ["index"] = index,
        ["name"] = name,
        ["category"] = "MagicItem",
        ["rarity"] = "Uncommon",
        ["description"] = new JsonArray("Injerto ficticio."),
        ["effects"] = new JsonArray("Texto de ejemplo."),
        ["graft"] = new JsonObject { ["slot"] = "brazos" },
    };

    [Fact]
    public async Task A_player_with_the_feature_enhances_one_item_within_its_limits_and_the_sheet_explains_it()
    {
        var s = await ScenarioAsync();
        var (hero, sword, shield) = await FighterAsync(s, Subclass, 3);
        var feature = hero.System().ItemEnhancementFeature!;
        Assert.Equal(("forja-ejemplo-mano-experta", 1), (feature.FeatureIndex, feature.MaxPerItem));
        Assert.Equal([Sharp, Burning, Reinforced], feature.Options.Select(o => o.Index).Order(StringComparer.Ordinal));
        Assert.False(feature.Options.Single(o => o.Index == Burning).Available);
        var before = hero.Combat.Attacks.Single(a => a.ItemId == sword);

        var sharp = await EnhanceOkAsync(s.Player.Client, hero.Id, sword, Sharp);
        var attack = sharp.Combat.Attacks.Single(a => a.ItemId == sword);
        Assert.Equal(before.AttackBonus + 1, attack.AttackBonus);
        Assert.Contains(attack.AttackBreakdown!.Parts, p => p.Label == "Afilado (Longsword)" && p.Value == 1);
        Assert.StartsWith("1d10", attack.Damage, StringComparison.Ordinal);
        Assert.Contains("finesse", attack.Properties);
        var enhanced = Assert.Single(sharp.System().ItemEnhancements);
        Assert.Equal((sword, "1d10", "Afilado"), (enhanced.ItemId, enhanced.DamageDice, enhanced.Options.Single().Name));
        Assert.Equal(1, enhanced.Breakdowns["attack"].Total);

        // One option per item at level 3; Ardiente needs level 5 and Afilado; a shield is not a weapon.
        Assert.Equal(HttpStatusCode.BadRequest, (await EnhanceAsync(s.Player.Client, hero.Id, sword, Sharp, Burning)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnhanceAsync(s.Player.Client, hero.Id, shield, Sharp)).StatusCode);

        var armorClass = sharp.Sheet.ArmorClass;
        var reinforced = await EnhanceOkAsync(s.Player.Client, hero.Id, shield, Reinforced);
        Assert.Equal(armorClass + 1, reinforced.Sheet.ArmorClass);
        Assert.Contains(reinforced.Sheet.Breakdowns["armorClass"].Parts, p => p.Label == "Reforzado (Shield)" && p.Value == 1);
        Assert.Contains(reinforced.Sheet.Breakdowns["save.con"].Parts, p => p.Label == "Reforzado (Shield)" && p.Value == 1);
        Assert.Contains("forja-ejemplo-liviano", reinforced.System().ItemEnhancements.Single(e => e.ItemId == shield).Properties);

        // Level 5: two options; Ardiente changes the damage type and adds to the damage.
        var patch = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/sheet", new { classes = new[] { new { classIndex = "fighter", subclassIndex = Subclass, level = 5 } } });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await EnhanceAsync(s.Player.Client, hero.Id, sword, Burning)).StatusCode);
        var burning = await EnhanceOkAsync(s.Player.Client, hero.Id, sword, Sharp, Burning);
        var fiery = burning.Combat.Attacks.Single(a => a.ItemId == sword);
        Assert.Equal("fire", fiery.DamageType);
        Assert.Contains(fiery.DamageBreakdown!.Parts, p => p.Label == "Ardiente (Longsword)" && p.Value == 1);

        // Removing them.
        var cleared = await EnhanceOkAsync(s.Player.Client, hero.Id, sword);
        Assert.DoesNotContain(cleared.System().ItemEnhancements, e => e.ItemId == sword);
    }

    [Fact]
    public async Task Without_the_feature_the_enhancement_is_a_request_the_dm_approves()
    {
        var s = await ScenarioAsync();
        var (hero, sword, _) = await FighterAsync(s, null, 3);
        Assert.Null(hero.System().ItemEnhancementFeature);

        var asked = await EnhanceAsync(s.Player.Client, hero.Id, sword, Sharp);
        Assert.Equal(HttpStatusCode.Accepted, asked.StatusCode);
        var request = (await asked.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        Assert.Equal(("ItemEnhancement", "Longsword"), (request.Type, request.Payload.GetProperty("itemName").GetString()));
        Assert.Empty((await s.Player.GetCharacterAsync(hero.Id)).System().ItemEnhancements);

        await s.Dm.ApproveAsync(request.Id);
        Assert.Equal(Sharp, Assert.Single((await s.Player.GetCharacterAsync(hero.Id)).System().ItemEnhancements).Options.Single().Index);

        // The DM applies directly, and unknown options are rejected.
        Assert.Equal(HttpStatusCode.OK, (await EnhanceAsync(s.Dm.Client, hero.Id, sword)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnhanceAsync(s.Dm.Client, hero.Id, sword, "forja-ejemplo-nada")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EnhanceAsync(s.Dm.Client, hero.Id, Guid.NewGuid(), Sharp)).StatusCode);
    }

    [Fact]
    public async Task Only_one_graft_is_equipped_per_body_slot()
    {
        var s = await ScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Injertada");
        var iron = await s.Player.AddItemAsync(hero.Id, new { templateId = await ItemIdAsync(s, "Brazo de hierro"), quantity = 1 });
        var copper = await s.Player.AddItemAsync(hero.Id, new { templateId = await ItemIdAsync(s, "Brazo de cobre"), quantity = 1 });

        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(hero.Id, iron.Id, new { equipped = true })).StatusCode);
        var second = await s.Player.PatchItemAsync(hero.Id, copper.Id, new { equipped = true });
        Assert.NotEqual(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("brazos", await second.Content.ReadAsStringAsync());

        var detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal([("Brazo de hierro", "brazos", true), ("Brazo de cobre", "brazos", false)],
            detail.System().Grafts.Select(g => (g.Name, g.Slot, g.Equipped)).OrderByDescending(g => g.Equipped));

        // Taking the first one off frees the slot.
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(hero.Id, iron.Id, new { equipped = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(hero.Id, copper.Id, new { equipped = true })).StatusCode);
    }

    [Theory]
    [InlineData("feature", "{\"maxPerItemByLevel\":{\"3\":1}}", "itemEnhancement.optionSets")]
    [InlineData("feature", "{\"optionSets\":[\"forja-malo-ninguno\"],\"maxPerItemByLevel\":{\"3\":1}}", "itemEnhancement.optionSets[0]")]
    [InlineData("feature", "{\"optionSets\":[\"forja-malo-mejoras\"],\"maxPerItemByLevel\":{\"0\":1}}", "itemEnhancement.maxPerItemByLevel.0")]
    [InlineData("feature", "{\"optionSets\":[\"forja-malo-mejoras\"]}", "itemEnhancement.maxPerItemByLevel")]
    [InlineData("option", "{\"appliesTo\":[\"ring\"]}", "itemEffects.appliesTo[0]")]
    [InlineData("option", "{\"damageType\":\"tedio\"}", "itemEffects.damageType")]
    [InlineData("option", "{\"damageStep\":9}", "itemEffects.damageStep")]
    [InlineData("item", "{\"slot\":\" \"}", "graft.slot")]
    public async Task Invalid_enhancements_and_grafts_are_reported_with_their_path(string where, string json, string path)
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = JsonNode.Parse(Pack().ToJsonString().Replace(Id, "forja-malo", StringComparison.Ordinal))!;
        var value = JsonNode.Parse(json)!;
        var prefix = where switch
        {
            "feature" => "classes[0].subclasses[0].levels[0].features[0]",
            "option" => "optionSets[0].options[0]",
            _ => "items[0]",
        };
        switch (where)
        {
            case "feature":
                pack["classes"]![0]!["subclasses"]![0]!["levels"]![0]!["features"]![0]!["itemEnhancement"] = value;
                break;
            case "option":
                pack["optionSets"]![0]!["options"]![0]!["itemEffects"] = value;
                break;
            default:
                pack["items"]![0]!["graft"] = value;
                break;
        }

        var response = await admin.PostAsync(PacksUrl, new StringContent(pack.ToJsonString(), Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.True(body.Contains($"{prefix}.{path}", StringComparison.Ordinal), body);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------------------

    private async Task<CampaignScenario> ScenarioAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        await ImportLock.WaitAsync();
        try
        {
            if (!(await admin.GetStringAsync(PacksUrl)).Contains($"\"{Id}\"", StringComparison.Ordinal))
            {
                var response = await admin.PostAsync(PacksUrl, new StringContent(Pack().ToJsonString(), Encoding.UTF8, "application/json"));
                Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            }
        }
        finally
        {
            ImportLock.Release();
        }

        var s = await factory.CreateCampaignScenarioAsync();
        await s.EnablePacksAsync(Id);
        return s;
    }

    private static async Task<Guid> ItemIdAsync(CampaignScenario s, string name)
    {
        var page = await s.Player.Client.GetFromJsonAsync<PagedResult<ItemSummaryDto>>(
            $"/api/v1/systems/dnd5e/catalog/items?search={Uri.EscapeDataString(name)}&pageSize=50&campaignId={s.CampaignId}");
        return page!.Items.First(i => i.Name == name).Id;
    }

    /// <summary>An active fighter with a longsword and a shield equipped (and the subclass, when given).</summary>
    private static async Task<(CharacterDetailDto Hero, Guid Sword, Guid Shield)> FighterAsync(CampaignScenario s, string? subclass, int level)
    {
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Herrera");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", subclassIndex = subclass, level } },
            baseAbilities = new { str = 16, dex = 12, con = 14, @int = 10, wis = 10, cha = 8 },
            applyRacialBonuses = false,
            proficiencies = new object[] { new { type = "Weapon", key = "martial", expertise = false } },
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        var sword = await s.Player.AddItemAsync(hero.Id, new { templateId = await s.Player.SrdItemIdAsync("Longsword"), quantity = 1 });
        var shield = await s.Player.AddItemAsync(hero.Id, new { templateId = await s.Player.SrdItemIdAsync("Shield"), quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(hero.Id, sword.Id, new { equipped = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(hero.Id, shield.Id, new { equipped = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.ActivateAsync(hero.Id)).StatusCode);
        return (await s.Player.GetCharacterAsync(hero.Id), sword.Id, shield.Id);
    }

    private static Task<HttpResponseMessage> EnhanceAsync(HttpClient client, Guid characterId, Guid itemId, params string[] options) =>
        client.PutAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(characterId)}/items/{itemId}/enhancements", new { options });

    private static async Task<CharacterDetailDto> EnhanceOkAsync(HttpClient client, Guid characterId, Guid itemId, params string[] options)
    {
        var response = await EnhanceAsync(client, characterId, itemId, options);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }
}
