using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dnd.Application.Catalog;
using Dnd.Application.Characters;

namespace Dnd.Api.Tests.Items;

[Collection(CatalogCollection.Name)]
public class ItemModifierEndpointsTests(CatalogApiFactory factory)
{
    private static readonly object AgileGloves = new
    {
        name = "Guantes ágiles",
        category = "MagicItem",
        modifiers = new[] { new { kind = "AbilityBonus", target = "dex", value = 3 } },
    };

    [Fact]
    public async Task Homebrew_returns_its_modifiers()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var item = await s.Dm.CreateHomebrewAsync(s.CampaignId, AgileGloves);

        var modifier = Assert.Single(item.Modifiers);
        Assert.Equal(("AbilityBonus", "dex", 3), (modifier.Kind, modifier.Target, modifier.Value));
        var detail = await s.Player.Client.GetFromJsonAsync<ItemDetailDto>($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{item.Id}");
        Assert.Equal(item.Modifiers, detail!.Modifiers);
    }

    [Fact]
    public async Task Equipping_a_dex_bonus_item_raises_dex_and_unequipping_lowers_it()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await SetupAsync(s.Player, s.CampaignId, dex: 14);
        var template = await s.Dm.CreateHomebrewAsync(s.CampaignId, AgileGloves);
        var gloves = await s.Player.AddItemAsync(character.Id, new { templateId = template.Id, quantity = 1 });
        Assert.Equal("AbilityBonus", Assert.Single(gloves.Effective.Modifiers).Kind);
        Assert.Equal(14, (await s.Player.GetCharacterAsync(character.Id)).Sheet.Abilities["dex"].Score);

        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, gloves.Id, new { equipped = true })).StatusCode);

        var equipped = (await s.Player.GetCharacterAsync(character.Id)).Sheet;
        Assert.Equal((17, 3, false), (equipped.Abilities["dex"].Score, equipped.Abilities["dex"].Modifier, equipped.Abilities["dex"].Overridden));
        Assert.Equal(13, equipped.ArmorClass);
        var effect = Assert.Single(equipped.ItemEffects);
        Assert.Equal(("Guantes ágiles", "AbilityBonus", "dex", 3), (effect.ItemName, effect.Kind, effect.Target, effect.Value));
        Assert.Equal("17 = base:Puntuación base 14, item:Guantes ágiles 3", Text(equipped.Breakdowns["ability.dex"]));
        Assert.Equal(equipped.ArmorClass, equipped.Breakdowns["armorClass"].Total);

        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, gloves.Id, new { equipped = false })).StatusCode);

        var unequipped = (await s.Player.GetCharacterAsync(character.Id)).Sheet;
        Assert.Equal(14, unequipped.Abilities["dex"].Score);
        Assert.Empty(unequipped.ItemEffects);
    }

    [Fact]
    public async Task An_item_that_requires_attunement_applies_only_when_attuned()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await SetupAsync(s.Player, s.CampaignId, str: 10);
        var template = await s.Dm.CreateHomebrewAsync(s.CampaignId, new
        {
            name = "Cinturón de fuerza",
            category = "MagicItem",
            requiresAttunement = true,
            modifiers = new[] { new { kind = "AbilitySet", target = "str", value = 21 } },
        });
        var belt = await s.Player.AddItemAsync(character.Id, new { templateId = template.Id, quantity = 1 });

        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, belt.Id, new { equipped = true })).StatusCode);
        Assert.Equal(10, (await s.Player.GetCharacterAsync(character.Id)).Sheet.Abilities["str"].Score);

        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, belt.Id, new { attuned = true })).StatusCode);
        var detail = await s.Player.GetCharacterAsync(character.Id);
        Assert.Equal(21, detail.Sheet.Abilities["str"].Score);
        Assert.Equal(21 * InventoryCapacityPerStrength, detail.Inventory.CarryCapacityLb);
    }

    [Fact]
    public async Task Losing_a_hit_points_item_caps_the_current_hit_points()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await SetupAsync(s.Player, s.CampaignId, con: 10);
        var template = await s.Dm.CreateHomebrewAsync(s.CampaignId, new
        {
            name = "Amuleto vital",
            category = "MagicItem",
            modifiers = new[] { new { kind = "HitPointsMaxBonus", target = (string?)null, value = 10 } },
        });
        var amulet = await s.Player.AddItemAsync(character.Id, new { templateId = template.Id, quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, amulet.Id, new { equipped = true })).StatusCode);
        var activated = await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        var active = (await activated.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal((20, 20), (active.Sheet.HitPointsMax, active.HitPointsCurrent));

        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, amulet.Id, new { equipped = false })).StatusCode);

        var detail = await s.Player.GetCharacterAsync(character.Id);
        Assert.Equal((10, 10), (detail.Sheet.HitPointsMax, detail.HitPointsCurrent));
        var heal = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/combat", new { hitPointsCurrent = 10 });
        Assert.Equal(HttpStatusCode.OK, heal.StatusCode);
    }

    [Fact]
    public async Task Overrides_distinguish_absent_modifiers_from_an_empty_list()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var template = await s.Dm.CreateHomebrewAsync(s.CampaignId, AgileGloves);

        var kept = await s.Player.AddItemAsync(character.Id, new { templateId = template.Id, quantity = 1, overrides = new { name = "Guantes" } });
        var removed = await s.Player.Client.PostAsJsonAsync(ItemTestHelpers.InventoryUrl(character.Id), new
        {
            templateId = template.Id,
            quantity = 1,
            overrides = new { modifiers = Array.Empty<object>() },
        });

        Assert.Null(kept.Overrides.Modifiers);
        Assert.Single(kept.Effective.Modifiers);
        Assert.Equal(HttpStatusCode.Created, removed.StatusCode);
        using var json = JsonDocument.Parse(await removed.Content.ReadAsStringAsync());
        Assert.Equal(0, json.RootElement.GetProperty("overrides").GetProperty("modifiers").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("effective").GetProperty("modifiers").GetArrayLength());
        Assert.True(json.RootElement.GetProperty("isCustom").GetBoolean());
        var inventory = await s.Player.GetInventoryAsync(character.Id);
        Assert.Equal(2, inventory.Items.Count);
        Assert.Contains(inventory.Items, i => i.Overrides.Modifiers is { Count: 0 });
    }

    [Fact]
    public async Task Patching_a_homebrew_replaces_its_modifiers()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var item = await s.Dm.CreateHomebrewAsync(s.CampaignId, AgileGloves);
        var url = $"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{item.Id}";

        var replaced = await s.Dm.Client.PatchAsJsonAsync(url, new { modifiers = new[] { new { kind = "SpeedBonus", value = 10 } } });
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        Assert.Equal("SpeedBonus", Assert.Single((await replaced.Content.ReadFromJsonAsync<ItemDetailDto>())!.Modifiers).Kind);

        var name = await s.Dm.Client.PatchAsJsonAsync(url, new { name = "Botas" });
        Assert.Single((await name.Content.ReadFromJsonAsync<ItemDetailDto>())!.Modifiers);

        var cleared = await s.Dm.Client.PatchAsJsonAsync(url, new { modifiers = (object?)null });
        Assert.Empty((await cleared.Content.ReadFromJsonAsync<ItemDetailDto>())!.Modifiers);
    }

    [Theory]
    [InlineData("{\"kind\":\"AbilityBonus\",\"value\":2}")]
    [InlineData("{\"kind\":\"AbilityBonus\",\"target\":\"luck\",\"value\":2}")]
    [InlineData("{\"kind\":\"Flying\",\"value\":2}")]
    [InlineData("{\"kind\":\"ArmorClassBonus\",\"value\":31}")]
    [InlineData("{\"kind\":\"ArmorClassBonus\",\"target\":\"dex\",\"value\":1}")]
    [InlineData("{\"kind\":\"AbilitySet\",\"target\":\"str\",\"value\":0}")]
    public async Task Invalid_modifiers_return_400(string modifier)
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var body = $"{{\"name\":\"Objeto\",\"category\":\"MagicItem\",\"modifiers\":[{modifier}]}}";

        var homebrew = await s.Dm.Client.PostAsync(ItemTestHelpers.ItemsUrl(s.CampaignId), new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        var custom = await s.Player.Client.PostAsync(
            ItemTestHelpers.InventoryUrl(character.Id),
            new StringContent($"{{\"quantity\":1,\"overrides\":{body}}}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, homebrew.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, custom.StatusCode);
    }

    [Fact]
    public async Task Too_many_modifiers_return_400()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var modifiers = Enumerable.Repeat(new { kind = "SpeedBonus", value = 5 }, 11).ToArray();

        var response = await s.Dm.Client.PostAsJsonAsync(ItemTestHelpers.ItemsUrl(s.CampaignId), new { name = "Botas", category = "MagicItem", modifiers });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Attacks_include_breakdowns_and_bonuses_of_non_weapon_items()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await SetupAsync(s.Player, s.CampaignId, str: 16, weaponProficiency: "martial-weapons");
        var sword = await s.Player.AddItemAsync(character.Id, new
        {
            templateId = await s.Player.SrdItemIdAsync("Longsword"),
            quantity = 1,
            overrides = new { name = "Espada bendita", modifiers = new[] { new { kind = "AttackBonus", value = 1 }, new { kind = "DamageBonus", value = 1 } } },
        });
        var ringTemplate = await s.Dm.CreateHomebrewAsync(s.CampaignId, new
        {
            name = "Anillo de puntería",
            category = "MagicItem",
            modifiers = new[] { new { kind = "AttackBonus", value = 1 } },
        });
        var ring = await s.Player.AddItemAsync(character.Id, new { templateId = ringTemplate.Id, quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, sword.Id, new { equipped = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, ring.Id, new { equipped = true })).StatusCode);

        var detail = await s.Player.GetCharacterAsync(character.Id);

        var attack = detail.Combat.Attacks[0];
        Assert.Equal((7, "1d8+4"), (attack.AttackBonus, attack.Damage));
        Assert.Equal("7 = ability:Fuerza 3, proficiency:Competencia 2, item:Espada bendita 1, item:Anillo de puntería 1", Text(attack.AttackBreakdown));
        Assert.Equal("4 = ability:Fuerza 3, item:Espada bendita 1", Text(attack.DamageBreakdown));
        Assert.Equal("6 = ability:Fuerza 3, proficiency:Competencia 2, item:Anillo de puntería 1", Text(detail.Combat.Attacks[^1].AttackBreakdown));
        var effect = Assert.Single(detail.Sheet.ItemEffects);
        Assert.Equal(("Anillo de puntería", "AttackBonus"), (effect.ItemName, effect.Kind));
    }

    private const int InventoryCapacityPerStrength = 15;

    /// <summary>"7 = ability:Fuerza 3, proficiency:Competencia 2, ..."; also checks that the parts add up.</summary>
    private static string Text(ValueBreakdownDto breakdown)
    {
        Assert.Equal(breakdown.Total, breakdown.Parts.Sum(p => p.Value));
        return $"{breakdown.Total} = {string.Join(", ", breakdown.Parts.Select(p => $"{p.Source}:{p.Label} {p.Value}"))}";
    }

    private static async Task<CharacterDetailDto> SetupAsync(
        SignedInUser player,
        Guid campaignId,
        int str = 10,
        int dex = 10,
        int con = 10,
        string? weaponProficiency = null)
    {
        var character = await player.CreateCharacterAsync(campaignId);
        var patch = await player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", level = 1 } },
            baseAbilities = new { str, dex, con, @int = 10, wis = 10, cha = 10 },
            applyRacialBonuses = false,
            proficiencies = weaponProficiency is null
                ? null
                : new[] { new { type = "Weapon", key = weaponProficiency } },
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        return (await patch.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }
}
