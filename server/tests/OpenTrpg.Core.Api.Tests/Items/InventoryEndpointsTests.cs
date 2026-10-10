using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Systems.Dnd5e.Application.Items;

namespace OpenTrpg.Core.Api.Tests.Items;

[Collection(CatalogCollection.Name)]
public class InventoryEndpointsTests(CatalogApiFactory factory)
{
    private static readonly object MagicRing = new { name = "Anillo", category = "MagicItem", requiresAttunement = true };

    [Fact]
    public async Task Owner_of_a_draft_adds_items_directly()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var swordId = await s.Player.SrdItemIdAsync("Longsword");

        var response = await s.Player.Client.PostAsJsonAsync(ItemTestHelpers.InventoryUrl(character.Id), new { templateId = swordId, quantity = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = (await response.Content.ReadFromJsonAsync<CharacterItemDto>())!;
        Assert.Equal((swordId, "Longsword", 1, false), (item.TemplateId, item.TemplateName, item.Quantity, item.IsCustom));
        Assert.Equal(("Longsword", "Weapon", "1d8", "1d10"), (item.Effective.Name, item.Effective.Category, item.Effective.Damage!.Dice, item.Effective.Damage.Versatile));
        var inventory = await s.Player.GetInventoryAsync(character.Id);
        Assert.Equal(item.Id, Assert.Single(inventory.Items).Id);
        Assert.Equal(3m, inventory.TotalWeightLb);
        Assert.Equal(150, inventory.CarryCapacityLb);
        var detail = await s.Player.GetCharacterAsync(character.Id);
        Assert.Equal(item.Id, Assert.Single(detail.Inventory.Items).Id);
    }

    [Fact]
    public async Task Overrides_only_include_the_defined_fields()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var swordId = await s.Player.SrdItemIdAsync("Longsword");

        var response = await s.Player.Client.PostAsJsonAsync(ItemTestHelpers.InventoryUrl(character.Id), new
        {
            templateId = swordId,
            quantity = 1,
            overrides = new { name = "Longsword +1", attackBonus = 1, damageBonus = 1, effects = new[] { "+1 a ataque y daño" } },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var overrides = json.RootElement.GetProperty("overrides");
        Assert.Equal(["name", "attackBonus", "damageBonus", "effects"], overrides.EnumerateObject().Select(p => p.Name));
        Assert.True(json.RootElement.GetProperty("isCustom").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("effective").GetProperty("attackBonus").GetInt32());
        Assert.Equal("1d8", json.RootElement.GetProperty("effective").GetProperty("damage").GetProperty("dice").GetString());
    }

    [Fact]
    public async Task Custom_item_without_template_needs_a_name()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);

        var response = await s.Player.Client.PostAsJsonAsync(ItemTestHelpers.InventoryUrl(character.Id), new { quantity = 1, overrides = new { category = "Other" } });
        var unknown = await s.Player.Client.PostAsJsonAsync(ItemTestHelpers.InventoryUrl(character.Id), new { templateId = Guid.NewGuid(), quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.True((await unknown.ReadProblemAsync()).HasFieldError("templateId"));
    }

    [Fact]
    public async Task Active_player_adding_an_item_creates_an_AddItem_request_that_the_dm_approves()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);
        var ropeId = await s.Player.SrdItemIdAsync("Rope, hempen (50 feet)");

        var response = await s.Player.Client.PostAsJsonAsync(ItemTestHelpers.InventoryUrl(character.Id), new { templateId = ropeId, quantity = 2 });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var request = (await response.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        Assert.Equal(("AddItem", "Pending"), (request.Type, request.Status));
        Assert.Equal("Rope, hempen (50 feet)", request.Before!.Value.GetProperty("template").GetProperty("name").GetString());
        Assert.Empty((await s.Player.GetInventoryAsync(character.Id)).Items);

        var approved = await s.Dm.ApproveAsync(request.Id);

        Assert.Equal("Approved", approved.Status);
        var item = Assert.Single((await s.Player.GetInventoryAsync(character.Id)).Items);
        Assert.Equal((ropeId, 2), (item.TemplateId, item.Quantity));
    }

    [Fact]
    public async Task Active_player_custom_item_creates_a_CustomItem_request()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);

        var response = await s.Player.Client.PostAsJsonAsync(ItemTestHelpers.InventoryUrl(character.Id), new { quantity = 1, overrides = new { name = "Medallón de la abuela" } });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var request = (await response.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        Assert.Equal("CustomItem", request.Type);
        Assert.Null(request.Before);
        await s.Dm.ApproveAsync(request.Id);
        var item = Assert.Single((await s.Player.GetInventoryAsync(character.Id)).Items);
        Assert.Equal(("Medallón de la abuela", true, (Guid?)null), (item.Effective.Name, item.IsCustom, item.TemplateId));
    }

    [Fact]
    public async Task Dm_adds_items_directly_to_active_characters()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);

        await s.Dm.AddItemAsync(character.Id, new { quantity = 1, overrides = new { name = "Llave de la cripta" } });

        Assert.Single((await s.Player.GetInventoryAsync(character.Id)).Items);
    }

    [Fact]
    public async Task Other_players_cannot_see_or_change_the_inventory()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateSignedInUserAsync("Other Player");
        await s.Owner.AddMemberAsync(s.CampaignId, other, CampaignScenario.PlayerRole);
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var item = await s.Player.AddItemAsync(character.Id, new { quantity = 1, overrides = new { name = "Diario" } });

        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.GetAsync(ItemTestHelpers.InventoryUrl(character.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PatchItemAsync(character.Id, item.Id, new { notes = "mío" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.PostAsJsonAsync(ItemTestHelpers.InventoryUrl(character.Id), new { quantity = 1, overrides = new { name = "x" } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync(ItemTestHelpers.InventoryUrl(character.Id))).StatusCode);
    }

    [Fact]
    public async Task Chain_mail_and_shield_give_armor_class_18_on_the_sheet()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", level = 1 } },
            baseAbilities = new { str = 15, dex = 14, con = 14, @int = 10, wis = 10, cha = 10 },
            applyRacialBonuses = false,
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var chain = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Chain Mail"), quantity = 1 });
        var shield = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Shield"), quantity = 1 });
        Assert.Equal(12, (await s.Player.GetCharacterAsync(character.Id)).Sheet.ArmorClass);

        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, chain.Id, new { equipped = true })).StatusCode);
        Assert.Equal(16, (await s.Player.GetCharacterAsync(character.Id)).Sheet.ArmorClass);
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, shield.Id, new { equipped = true })).StatusCode);

        var detail = await s.Player.GetCharacterAsync(character.Id);
        Assert.Equal(18, detail.Sheet.ArmorClass);
        Assert.All(detail.Inventory.Items, i => Assert.True(i.Equipped));
        Assert.Equal(61m, detail.Inventory.TotalWeightLb);
        Assert.Equal(225, detail.Inventory.CarryCapacityLb);
    }

    [Fact]
    public async Task Equipping_a_second_armor_unequips_the_first()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var chain = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Chain Mail"), quantity = 1 });
        var leather = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Leather Armor"), quantity = 1 });

        await s.Player.PatchItemAsync(character.Id, chain.Id, new { equipped = true });
        await s.Player.PatchItemAsync(character.Id, leather.Id, new { equipped = true });

        var items = (await s.Player.GetInventoryAsync(character.Id)).Items.ToDictionary(i => i.Id);
        Assert.False(items[chain.Id].Equipped);
        Assert.True(items[leather.Id].Equipped);
        Assert.Equal(11, (await s.Player.GetCharacterAsync(character.Id)).Sheet.ArmorClass);
    }

    [Fact]
    public async Task Equipping_a_non_equippable_item_returns_400()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var rope = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Rope, hempen (50 feet)"), quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.PatchItemAsync(character.Id, rope.Id, new { equipped = true })).StatusCode);
    }

    [Fact]
    public async Task Attuning_a_fourth_item_returns_409_with_a_code_and_can_replace_one()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);
        var rings = new List<CharacterItemDto>();
        for (var i = 0; i < 4; i++)
        {
            rings.Add(await s.Dm.AddItemAsync(character.Id, new { quantity = 1, overrides = MagicRing }));
        }

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, rings[i].Id, new { attuned = true })).StatusCode);
        }

        var fourth = await s.Player.PatchItemAsync(character.Id, rings[3].Id, new { attuned = true });

        Assert.Equal(HttpStatusCode.Conflict, fourth.StatusCode);
        var problem = await fourth.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("attunement-limit", problem.GetProperty("code").GetString());
        Assert.Equal(3, (await s.Player.GetInventoryAsync(character.Id)).AttunedCount);

        var swap = await s.Player.PatchItemAsync(character.Id, rings[3].Id, new { attuned = true, replaceAttunedItemId = rings[0].Id });

        Assert.Equal(HttpStatusCode.OK, swap.StatusCode);
        var inventory = await s.Player.GetInventoryAsync(character.Id);
        Assert.Equal(3, inventory.AttunedCount);
        Assert.False(inventory.Items.Single(i => i.Id == rings[0].Id).Attuned);
        Assert.True(inventory.Items.Single(i => i.Id == rings[3].Id).Attuned);
    }

    [Fact]
    public async Task Patch_sets_notes_order_and_charges_without_approval()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);
        var wand = await s.Dm.AddItemAsync(character.Id, new { quantity = 1, overrides = new { name = "Varita de proyectiles", category = "MagicItem" } });

        var response = await s.Player.PatchItemAsync(character.Id, wand.Id, new { notes = "Recarga al amanecer", sortOrder = 5, charges = 7 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<CharacterItemDto>())!;
        Assert.Equal(("Recarga al amanecer", 5, 7, 7), (updated.Notes, updated.SortOrder, updated.Charges, updated.ChargesMax));

        var used = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.InventoryUrl(character.Id)}/{wand.Id}/use", new { amount = 2 });
        Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        Assert.Equal(5, Assert.Single((await used.Content.ReadFromJsonAsync<InventoryDto>())!.Items).Charges);

        var cleared = await s.Player.PatchItemAsync(character.Id, wand.Id, new { notes = (string?)null });
        Assert.Null((await cleared.Content.ReadFromJsonAsync<CharacterItemDto>())!.Notes);
    }

    [Fact]
    public async Task Using_a_consumable_subtracts_and_removes_it_at_zero()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var potion = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Potion of Healing"), quantity = 2 });
        var sword = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Longsword"), quantity = 1 });

        var first = await s.Player.Client.PostAsync($"{ItemTestHelpers.InventoryUrl(character.Id)}/{potion.Id}/use", null);
        Assert.Equal(1, (await first.Content.ReadFromJsonAsync<InventoryDto>())!.Items.Single(i => i.Id == potion.Id).Quantity);
        var second = await s.Player.Client.PostAsync($"{ItemTestHelpers.InventoryUrl(character.Id)}/{potion.Id}/use", null);
        Assert.DoesNotContain((await second.Content.ReadFromJsonAsync<InventoryDto>())!.Items, i => i.Id == potion.Id);
        var useSword = await s.Player.Client.PostAsync($"{ItemTestHelpers.InventoryUrl(character.Id)}/{sword.Id}/use", null);
        Assert.Equal(HttpStatusCode.BadRequest, useSword.StatusCode);
    }

    [Fact]
    public async Task Removing_items_is_direct_in_draft_and_needs_approval_when_active()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var draft = await s.Player.CreateCharacterAsync(s.CampaignId, "Borrador");
        var draftItem = await s.Player.AddItemAsync(draft.Id, new { templateId = await s.Player.SrdItemIdAsync("Arrow"), quantity = 20 });
        var active = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);
        var activeItem = await s.Dm.AddItemAsync(active.Id, new { quantity = 3, overrides = new { name = "Gema" } });

        var partial = await s.Player.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"{ItemTestHelpers.InventoryUrl(draft.Id)}/{draftItem.Id}")
        {
            Content = JsonContent.Create(new { quantity = 5 }),
        });
        Assert.Equal(HttpStatusCode.NoContent, partial.StatusCode);
        Assert.Equal(15, Assert.Single((await s.Player.GetInventoryAsync(draft.Id)).Items).Quantity);
        Assert.Equal(HttpStatusCode.NoContent, (await s.Player.Client.DeleteAsync($"{ItemTestHelpers.InventoryUrl(draft.Id)}/{draftItem.Id}")).StatusCode);
        Assert.Empty((await s.Player.GetInventoryAsync(draft.Id)).Items);

        var pending = await s.Player.Client.DeleteAsync($"{ItemTestHelpers.InventoryUrl(active.Id)}/{activeItem.Id}");
        Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
        var request = (await pending.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        Assert.Equal("RemoveItem", request.Type);
        Assert.Equal("Gema", request.Payload.GetProperty("itemName").GetString());
        Assert.Equal(3, request.Before!.Value.GetProperty("quantity").GetInt32());
        Assert.Single((await s.Player.GetInventoryAsync(active.Id)).Items);

        await s.Dm.ApproveAsync(request.Id);
        Assert.Empty((await s.Player.GetInventoryAsync(active.Id)).Items);
    }

    [Fact]
    public async Task Money_is_direct_for_the_dm_and_needs_approval_for_an_active_owner()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var draft = await s.Player.CreateCharacterAsync(s.CampaignId, "Borrador");
        var active = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);

        var draftMoney = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(draft.Id)}/money", new { deltaCp = 1500, reason = "Equipo inicial" });
        Assert.Equal(HttpStatusCode.OK, draftMoney.StatusCode);
        Assert.Equal(1500, (await draftMoney.Content.ReadFromJsonAsync<InventoryDto>())!.CopperPieces);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(draft.Id)}/money", new { deltaCp = -1501, reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(draft.Id)}/money", new { deltaCp = 0 })).StatusCode);

        await s.Dm.GiveMoneyAsync(active.Id, 100);
        var pending = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(active.Id)}/money", new { deltaCp = 250, reason = "Botín" });
        Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
        var request = (await pending.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        Assert.Equal(("AdjustMoney", 250), (request.Type, request.Payload.GetProperty("deltaCp").GetInt32()));
        Assert.Equal(100, request.Before!.Value.GetProperty("copperPieces").GetInt32());
        Assert.Equal(100, (await s.Player.GetInventoryAsync(active.Id)).CopperPieces);

        await s.Dm.ApproveAsync(request.Id);
        Assert.Equal(350, (await s.Player.GetInventoryAsync(active.Id)).CopperPieces);
    }
}
