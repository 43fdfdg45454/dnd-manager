using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Items;

namespace OpenTrpg.Core.Api.Tests.Items;

[Collection(CatalogCollection.Name)]
public class CampaignItemEndpointsTests(CatalogApiFactory factory)
{
    private static readonly object Homebrew = new
    {
        name = "Espada de la Luna",
        category = "Weapon",
        subcategory = "Martial Melee",
        rarity = "Rare",
        requiresAttunement = true,
        costCp = 50000,
        weightLb = 3,
        damageDice = "1d8",
        damageType = "Radiant",
        properties = new[] { "finesse" },
        stealthDisadvantage = false,
        description = new[] { "Brilla bajo la luna." },
        effects = new[] { "Luz 20 ft" },
    };

    [Fact]
    public async Task Homebrew_is_visible_to_members_when_searching_with_source_homebrew()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var item = await s.Dm.CreateHomebrewAsync(s.CampaignId, Homebrew);

        var homebrew = await SearchAsync(s.Player, s.CampaignId, "source=homebrew");
        var srdOnly = await SearchAsync(s.Player, s.CampaignId, "source=srd&search=espada");
        var all = await SearchAsync(s.Player, s.CampaignId, "search=espada de la");

        var summary = Assert.Single(homebrew.Items);
        Assert.Equal((item.Id, "homebrew", "Weapon", "Rare"), (summary.Id, summary.Source, summary.Category, summary.Rarity));
        Assert.Empty(srdOnly.Items);
        Assert.Contains(all.Items, i => i.Id == item.Id);
        Assert.Equal("srd", (await SearchAsync(s.Player, s.CampaignId, "search=chain mail&source=all")).Items.First().Source);
        Assert.Equal(["Luz 20 ft"], item.Effects);
        Assert.Equal("homebrew", item.Source);
    }

    [Fact]
    public async Task Homebrew_of_another_campaign_is_not_listed()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateCampaignScenarioAsync();
        await other.Dm.CreateHomebrewAsync(other.CampaignId, Homebrew);

        Assert.Empty((await SearchAsync(s.Player, s.CampaignId, "source=homebrew")).Items);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync(ItemTestHelpers.ItemsUrl(s.CampaignId))).StatusCode);
    }

    [Fact]
    public async Task Catalog_item_detail_returns_homebrew_only_to_members_of_its_campaign()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var item = await s.Dm.CreateHomebrewAsync(s.CampaignId, Homebrew);
        var srdId = await s.Player.SrdItemIdAsync("Chain Mail");

        var asMember = await s.Player.Client.GetAsync($"/api/v1/systems/dnd5e/catalog/items/{item.Id}");
        var asOutsider = await s.Outsider.Client.GetAsync($"/api/v1/systems/dnd5e/catalog/items/{item.Id}");
        var srdAsOutsider = await s.Outsider.Client.GetAsync($"/api/v1/systems/dnd5e/catalog/items/{srdId}");

        Assert.Equal(HttpStatusCode.OK, asMember.StatusCode);
        var detail = (await asMember.Content.ReadFromJsonAsync<ItemDetailDto>())!;
        Assert.Equal((item.Id, s.CampaignId, false), (detail.Id, detail.CampaignId, detail.IsSrd));
        Assert.Equal(HttpStatusCode.NotFound, asOutsider.StatusCode);
        Assert.Equal(HttpStatusCode.OK, srdAsOutsider.StatusCode);
    }

    [Fact]
    public async Task Campaign_item_detail_serves_srd_and_homebrew()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var item = await s.Dm.CreateHomebrewAsync(s.CampaignId, Homebrew);
        var srdId = await s.Player.SrdItemIdAsync("Chain Mail");

        var homebrew = await s.Player.Client.GetFromJsonAsync<ItemDetailDto>($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{item.Id}");
        var srd = await s.Player.Client.GetFromJsonAsync<ItemDetailDto>($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{srdId}");

        Assert.Equal("Espada de la Luna", homebrew!.Name);
        Assert.Equal((16, "srd"), (srd!.ArmorClassBase, srd.Source));
    }

    [Fact]
    public async Task Only_dms_manage_homebrew()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var item = await s.Dm.CreateHomebrewAsync(s.CampaignId, Homebrew);

        var create = await s.Player.Client.PostAsJsonAsync(ItemTestHelpers.ItemsUrl(s.CampaignId), Homebrew);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{item.Id}", new { name = "Mía" });
        var delete = await s.Player.Client.DeleteAsync($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{item.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, patch.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Create_validates_the_input()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var response = await s.Dm.Client.PostAsJsonAsync(ItemTestHelpers.ItemsUrl(s.CampaignId), new { name = " ", category = "Sword", armorClassBase = 99 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.True(problem.HasFieldError("name"));
        Assert.True(problem.HasFieldError("category"));
        Assert.True(problem.HasFieldError("armorClassBase"));
    }

    [Fact]
    public async Task Patch_changes_only_the_given_fields_and_null_clears()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var item = await s.Dm.CreateHomebrewAsync(s.CampaignId, Homebrew);

        var response = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{item.Id}", new { name = "Espada del Sol", rarity = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<ItemDetailDto>())!;
        Assert.Equal(("Espada del Sol", (string?)null, "1d8", 50000), (updated.Name, updated.Rarity, updated.DamageDice, updated.CostCp));
        Assert.Equal(["Luz 20 ft"], updated.Effects);

        var invalid = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{item.Id}", new { category = "Nope" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Srd_items_cannot_be_edited_or_deleted_through_the_campaign()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var srdId = await s.Dm.SrdItemIdAsync("Chain Mail");

        Assert.Equal(HttpStatusCode.NotFound, (await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{srdId}", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Dm.Client.DeleteAsync($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{srdId}")).StatusCode);
    }

    [Fact]
    public async Task Deleting_homebrew_in_use_returns_409()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var inInventory = await s.Dm.CreateHomebrewAsync(s.CampaignId, Homebrew);
        var inShop = await s.Dm.CreateHomebrewAsync(s.CampaignId, Homebrew);
        var unused = await s.Dm.CreateHomebrewAsync(s.CampaignId, Homebrew);
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        await s.Player.AddItemAsync(character.Id, new { templateId = inInventory.Id, quantity = 1 });
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        await s.Dm.AddShopItemAsync(shop.Id, new { templateId = inShop.Id, priceCp = 100 });

        var deleteUsed = await s.Dm.Client.DeleteAsync($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{inInventory.Id}");
        var deleteInShop = await s.Dm.Client.DeleteAsync($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{inShop.Id}");
        var deleteUnused = await s.Dm.Client.DeleteAsync($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}/{unused.Id}");

        Assert.Equal(HttpStatusCode.Conflict, deleteUsed.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deleteInShop.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteUnused.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_campaign_deletes_homebrew_shops_and_inventories()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var item = await s.Dm.CreateHomebrewAsync(s.CampaignId, Homebrew);
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        await s.Player.AddItemAsync(character.Id, new { templateId = item.Id, quantity = 1 });
        await s.Dm.GiveMoneyAsync(character.Id, 1000);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var shopItem = await s.Dm.AddShopItemAsync(shop.Id, new { templateId = item.Id, priceCp = 100, stock = 3 });
        (await s.Player.BuyAsync(shop.Id, character.Id, shopItem.Id)).EnsureSuccessStatusCode();

        var response = await s.Owner.Client.DeleteAsync(s.Url);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Owner.Client.GetAsync($"/api/v1/systems/dnd5e/catalog/items/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Owner.Client.GetAsync(ItemTestHelpers.ShopUrl(shop.Id))).StatusCode);
    }

    [Fact]
    public async Task Search_filters_by_several_categories_subcategory_prefix_and_indexes()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        var armor = await SearchAsync(s.Player, s.CampaignId, "category=Armor,Shield&pageSize=200");
        var simple = await SearchAsync(s.Player, s.CampaignId, "category=Weapon&subcategory=simple&pageSize=200");
        var potions = await SearchAsync(s.Player, s.CampaignId, "subcategory=Potion&search=healing&pageSize=200");
        var byIndex = await SearchAsync(s.Player, s.CampaignId, "indexes=longsword, shield ,unknown-item");
        var badCategory = await s.Player.Client.GetAsync($"{ItemTestHelpers.ItemsUrl(s.CampaignId)}?category=Armor,Nope");

        Assert.Contains(armor.Items, i => i.Name == "Chain Mail");
        Assert.Contains(armor.Items, i => i.Category == "Shield");
        Assert.All(armor.Items, i => Assert.Contains(i.Category, new[] { "Armor", "Shield" }));
        Assert.Equal(14, simple.Total);
        Assert.All(simple.Items, i => Assert.StartsWith("Simple", i.Subcategory));
        Assert.NotEmpty(potions.Items);
        Assert.All(potions.Items, i => Assert.Equal("Potion", i.Subcategory));
        Assert.Equal(["longsword", "shield"], byIndex.Items.Select(i => i.Index).Order());
        Assert.Equal(HttpStatusCode.BadRequest, badCategory.StatusCode);
    }

    private static async Task<PagedResult<ItemSummaryDto>> SearchAsync(SignedInUser actor, Guid campaignId, string query)
    {
        var response = await actor.Client.GetAsync($"{ItemTestHelpers.ItemsUrl(campaignId)}?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<ItemSummaryDto>>())!;
    }
}
