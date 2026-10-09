using System.Net;
using System.Net.Http.Json;
using Dnd.Application.Common;
using Dnd.Application.Items;
using Dnd.Domain.Items;
using Dnd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dnd.Api.Tests.Items;

[Collection(CatalogCollection.Name)]
public class ShopEndpointsTests(CatalogApiFactory factory)
{
    // ---- Shop management -------------------------------------------------------------------------

    [Fact]
    public async Task Players_only_see_open_shops()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var open = await s.Dm.CreateShopAsync(s.CampaignId, "Abierta");
        var closed = await s.Dm.CreateShopAsync(s.CampaignId, "Cerrada", open: false);

        var asPlayer = await s.Player.Client.GetFromJsonAsync<List<ShopSummaryDto>>($"/api/v1/campaigns/{s.CampaignId}/shops");
        var asDm = await s.Dm.Client.GetFromJsonAsync<List<ShopSummaryDto>>($"/api/v1/campaigns/{s.CampaignId}/shops");

        Assert.Equal(open.Id, Assert.Single(asPlayer!).Id);
        Assert.Equal(["Abierta", "Cerrada"], asDm!.Select(x => x.Name));
        Assert.Equal(HttpStatusCode.NotFound, (await s.Player.Client.GetAsync(ItemTestHelpers.ShopUrl(closed.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.GetAsync(ItemTestHelpers.ShopUrl(closed.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Player.Client.GetAsync(ItemTestHelpers.ShopUrl(open.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync(ItemTestHelpers.ShopUrl(open.Id))).StatusCode);
    }

    [Fact]
    public async Task New_shops_are_closed_and_only_dms_manage_them()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var shop = await s.Dm.CreateShopAsync(s.CampaignId, "Herrería", open: false);

        Assert.False(shop.IsOpen);
        Assert.Equal(Shop.DefaultBuybackPercent, shop.BuybackPercent);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/shops", new { name = "Mía" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.ShopUrl(shop.Id)}/items", new { overrides = new { name = "x" }, priceCp = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/shops", new { name = "x", buybackPercent = 150 })).StatusCode);
    }

    [Fact]
    public async Task Dm_edits_shop_items_and_deletes_the_shop()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var sword = await s.Dm.AddShopItemAsync(shop.Id, new { templateId = await s.Dm.SrdItemIdAsync("Longsword"), priceCp = 1500, stock = 2 });
        var custom = await s.Dm.AddShopItemAsync(shop.Id, new { overrides = new { name = "Mapa del tesoro", category = "Other" }, priceCp = 300 });

        Assert.Equal(("Longsword", 2), (sword.Effective.Name, sword.Stock));
        Assert.Null(custom.Stock);

        var patch = await s.Dm.Client.PatchAsJsonAsync($"{ItemTestHelpers.ShopUrl(shop.Id)}/items/{sword.Id}", new { priceCp = 1400, stock = (int?)null, overrides = new { name = "Espada larga" } });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = (await patch.Content.ReadFromJsonAsync<ShopItemDto>())!;
        Assert.Equal((1400, (int?)null, "Espada larga"), (updated.PriceCp, updated.Stock, updated.Effective.Name));

        Assert.Equal(HttpStatusCode.NoContent, (await s.Dm.Client.DeleteAsync($"{ItemTestHelpers.ShopUrl(shop.Id)}/items/{custom.Id}")).StatusCode);
        var detail = await s.Dm.Client.GetFromJsonAsync<ShopDto>(ItemTestHelpers.ShopUrl(shop.Id));
        Assert.Equal(sword.Id, Assert.Single(detail!.Items).Id);

        Assert.Equal(HttpStatusCode.NoContent, (await s.Dm.Client.DeleteAsync(ItemTestHelpers.ShopUrl(shop.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Dm.Client.GetAsync(ItemTestHelpers.ShopUrl(shop.Id))).StatusCode);
    }

    // ---- Bulk add --------------------------------------------------------------------------------

    [Fact]
    public async Task Bulk_add_uses_the_list_price_unless_a_price_is_given()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var shop = await s.Dm.CreateShopAsync(s.CampaignId, "Armería");
        var longsword = await s.Dm.SrdItemIdAsync("Longsword");
        var shield = await s.Dm.SrdItemIdAsync("Shield");
        var homebrew = await s.Dm.CreateHomebrewAsync(s.CampaignId, new { name = "Daga rúnica", category = "Weapon" });

        var response = await s.Dm.Client.PostAsJsonAsync($"{ItemTestHelpers.ShopUrl(shop.Id)}/items/bulk", new
        {
            items = new object[]
            {
                new { templateId = longsword },
                new { templateId = shield, priceCp = 900, stock = 3 },
                new { templateId = homebrew.Id },
            },
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = (await response.Content.ReadFromJsonAsync<ShopDto>())!;
        Assert.Equal(
            [("Longsword", 1500, (int?)null), ("Shield", 900, 3), ("Daga rúnica", 0, null)],
            detail.Items.Select(i => (i.Effective.Name, i.PriceCp, i.Stock)));
        Assert.All(detail.Items, i => Assert.NotNull(i.TemplateId));
    }

    [Fact]
    public async Task Bulk_add_is_all_or_nothing_and_only_for_dms()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var other = await factory.CreateCampaignScenarioAsync();
        var foreign = await other.Dm.CreateHomebrewAsync(other.CampaignId, new { name = "Ajeno", category = "Weapon" });
        var longsword = await s.Dm.SrdItemIdAsync("Longsword");
        var url = $"{ItemTestHelpers.ShopUrl(shop.Id)}/items/bulk";

        var unknown = await s.Dm.Client.PostAsJsonAsync(url, new { items = new[] { new { templateId = longsword }, new { templateId = foreign.Id } } });
        var empty = await s.Dm.Client.PostAsJsonAsync(url, new { items = Array.Empty<object>() });
        var badPrice = await s.Dm.Client.PostAsJsonAsync(url, new { items = new[] { new { templateId = longsword, priceCp = -1 } } });
        var asPlayer = await s.Player.Client.PostAsJsonAsync(url, new { items = new[] { new { templateId = longsword } } });

        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badPrice.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asPlayer.StatusCode);
        Assert.Empty((await s.Dm.Client.GetFromJsonAsync<ShopDto>(ItemTestHelpers.ShopUrl(shop.Id)))!.Items);
    }

    // ---- Purchases -------------------------------------------------------------------------------

    [Fact]
    public async Task Buying_subtracts_money_and_stock_and_records_the_transaction()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId);
        await s.Dm.GiveMoneyAsync(character.Id, 10000);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId, "Armería");
        var chain = await s.Dm.AddShopItemAsync(shop.Id, new { templateId = await s.Dm.SrdItemIdAsync("Chain Mail"), priceCp = 7500, stock = 2 });

        var result = await (await s.Player.BuyAsync(shop.Id, character.Id, chain.Id)).ReadTradeAsync();

        Assert.Equal(2500, result.Inventory.CopperPieces);
        var item = Assert.Single(result.Inventory.Items);
        Assert.Equal(("Chain Mail", 1), (item.Effective.Name, item.Quantity));
        Assert.Equal(("Purchase", "Chain Mail", 1, 7500, "Armería", "Activo"), (result.Transaction.Type, result.Transaction.ItemName, result.Transaction.Quantity, result.Transaction.TotalCp, result.Transaction.ShopName, result.Transaction.CharacterName));
        var shopAfter = await s.Player.Client.GetFromJsonAsync<ShopDto>(ItemTestHelpers.ShopUrl(shop.Id));
        Assert.Equal(1, Assert.Single(shopAfter!.Items).Stock);
    }

    [Fact]
    public async Task Buying_stackable_items_accumulates_them()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        await s.Dm.GiveMoneyAsync(character.Id, 1000);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var arrows = await s.Dm.AddShopItemAsync(shop.Id, new { templateId = await s.Dm.SrdItemIdAsync("Arrow"), priceCp = 5 });

        await (await s.Player.BuyAsync(shop.Id, character.Id, arrows.Id, 20)).ReadTradeAsync();
        var result = await (await s.Player.BuyAsync(shop.Id, character.Id, arrows.Id, 10)).ReadTradeAsync();

        Assert.Equal(30, Assert.Single(result.Inventory.Items).Quantity);
        Assert.Equal(850, result.Inventory.CopperPieces);
    }

    [Fact]
    public async Task Buying_without_enough_money_returns_400_and_changes_nothing()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        await s.Dm.GiveMoneyAsync(character.Id, 100);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var chain = await s.Dm.AddShopItemAsync(shop.Id, new { templateId = await s.Dm.SrdItemIdAsync("Chain Mail"), priceCp = 7500, stock = 1 });

        var response = await s.Player.BuyAsync(shop.Id, character.Id, chain.Id);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var inventory = await s.Player.GetInventoryAsync(character.Id);
        Assert.Equal(100, inventory.CopperPieces);
        Assert.Empty(inventory.Items);
        Assert.Equal(1, Assert.Single((await s.Dm.Client.GetFromJsonAsync<ShopDto>(ItemTestHelpers.ShopUrl(shop.Id)))!.Items).Stock);
    }

    [Fact]
    public async Task Buying_without_stock_returns_400()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        await s.Dm.GiveMoneyAsync(character.Id, 100000);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var chain = await s.Dm.AddShopItemAsync(shop.Id, new { templateId = await s.Dm.SrdItemIdAsync("Chain Mail"), priceCp = 7500, stock = 1 });

        var tooMany = await s.Player.BuyAsync(shop.Id, character.Id, chain.Id, 2);
        await (await s.Player.BuyAsync(shop.Id, character.Id, chain.Id)).ReadTradeAsync();
        var soldOut = await s.Player.BuyAsync(shop.Id, character.Id, chain.Id);

        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, soldOut.StatusCode);
        Assert.Equal(100000 - 7500, (await s.Player.GetInventoryAsync(character.Id)).CopperPieces);
    }

    [Fact]
    public async Task Buying_from_a_closed_shop_returns_409()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        await s.Dm.GiveMoneyAsync(character.Id, 1000);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId, open: false);
        var item = await s.Dm.AddShopItemAsync(shop.Id, new { overrides = new { name = "Pan" }, priceCp = 2 });

        var response = await s.Player.BuyAsync(shop.Id, character.Id, item.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Buying_for_another_players_character_returns_403()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateSignedInUserAsync("Other Player");
        await s.Owner.AddMemberAsync(s.CampaignId, other, CampaignScenario.PlayerRole);
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        await s.Dm.GiveMoneyAsync(character.Id, 1000);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var item = await s.Dm.AddShopItemAsync(shop.Id, new { overrides = new { name = "Pan" }, priceCp = 2 });

        var response = await other.BuyAsync(shop.Id, character.Id, item.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1000, (await s.Player.GetInventoryAsync(character.Id)).CopperPieces);
        // A DM can trade on behalf of any character of the campaign.
        await (await s.Dm.BuyAsync(shop.Id, character.Id, item.Id)).ReadTradeAsync();
    }

    [Fact]
    public async Task Buying_with_a_character_of_another_campaign_returns_400()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateCampaignScenarioAsync();
        await other.Owner.AddMemberAsync(other.CampaignId, s.Player, CampaignScenario.PlayerRole);
        var foreign = await s.Player.CreateCharacterAsync(other.CampaignId);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var item = await s.Dm.AddShopItemAsync(shop.Id, new { overrides = new { name = "Pan" }, priceCp = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.BuyAsync(shop.Id, foreign.Id, item.Id)).StatusCode);
    }

    // ---- Sales -----------------------------------------------------------------------------------

    [Fact]
    public async Task Selling_pays_the_buyback_percent_and_restocks()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        await s.Dm.GiveMoneyAsync(character.Id, 3000);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId, buybackPercent: 40);
        var sword = await s.Dm.AddShopItemAsync(shop.Id, new { templateId = await s.Dm.SrdItemIdAsync("Longsword"), priceCp = 1500, stock = 2 });
        var bought = await (await s.Player.BuyAsync(shop.Id, character.Id, sword.Id)).ReadTradeAsync();
        var itemId = Assert.Single(bought.Inventory.Items).Id;

        var sold = await (await s.Player.SellAsync(shop.Id, character.Id, itemId)).ReadTradeAsync();

        Assert.Equal(1500 + 600, sold.Inventory.CopperPieces);
        Assert.Empty(sold.Inventory.Items);
        Assert.Equal(("Sale", 600, "Longsword"), (sold.Transaction.Type, sold.Transaction.TotalCp, sold.Transaction.ItemName));
        Assert.Equal(2, Assert.Single((await s.Dm.Client.GetFromJsonAsync<ShopDto>(ItemTestHelpers.ShopUrl(shop.Id)))!.Items).Stock);
    }

    [Fact]
    public async Task Selling_uses_the_template_cost_when_the_shop_does_not_stock_the_item()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var chain = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Chain Mail"), quantity = 1 });
        var keepsake = await s.Player.AddItemAsync(character.Id, new { quantity = 1, overrides = new { name = "Recuerdo" } });
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);

        var sold = await (await s.Player.SellAsync(shop.Id, character.Id, chain.Id)).ReadTradeAsync();

        Assert.Equal(3750, sold.Inventory.CopperPieces); // 50% of 75 gp
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.SellAsync(shop.Id, character.Id, keepsake.Id)).StatusCode);
    }

    [Fact]
    public async Task Selling_attuned_items_or_more_than_owned_returns_400()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var ring = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Arrow"), quantity = 2, overrides = new { requiresAttunement = true } });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, ring.Id, new { attuned = true })).StatusCode);
        var arrows = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Arrow"), quantity = 2 });
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);

        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.SellAsync(shop.Id, character.Id, ring.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Player.SellAsync(shop.Id, character.Id, arrows.Id, 3)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Player.SellAsync(shop.Id, character.Id, arrows.Id, 2)).StatusCode);
    }

    [Fact]
    public async Task Selling_to_a_closed_shop_returns_409()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var arrows = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Arrow"), quantity = 2 });
        var shop = await s.Dm.CreateShopAsync(s.CampaignId, open: false);

        Assert.Equal(HttpStatusCode.Conflict, (await s.Player.SellAsync(shop.Id, character.Id, arrows.Id)).StatusCode);
    }

    // ---- Transactions ----------------------------------------------------------------------------

    [Fact]
    public async Task Transactions_are_scoped_by_role()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateSignedInUserAsync("Other Player");
        await s.Owner.AddMemberAsync(s.CampaignId, other, CampaignScenario.PlayerRole);
        var mine = await s.Player.CreateCharacterAsync(s.CampaignId, "Mío");
        var theirs = await other.CreateCharacterAsync(s.CampaignId, "Suyo");
        await s.Dm.GiveMoneyAsync(mine.Id, 100);
        await s.Dm.GiveMoneyAsync(theirs.Id, 100);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var bread = await s.Dm.AddShopItemAsync(shop.Id, new { overrides = new { name = "Pan" }, priceCp = 2 });
        await (await s.Player.BuyAsync(shop.Id, mine.Id, bread.Id)).ReadTradeAsync();
        await (await s.Player.BuyAsync(shop.Id, mine.Id, bread.Id, 2)).ReadTradeAsync();
        await (await other.BuyAsync(shop.Id, theirs.Id, bread.Id)).ReadTradeAsync();

        var asDm = await GetTransactionsAsync(s.Dm, s.CampaignId, string.Empty);
        var asPlayer = await GetTransactionsAsync(s.Player, s.CampaignId, string.Empty);
        var filtered = await GetTransactionsAsync(s.Dm, s.CampaignId, $"characterId={theirs.Id}");
        var spy = await GetTransactionsAsync(s.Player, s.CampaignId, $"characterId={theirs.Id}");
        var paged = await GetTransactionsAsync(s.Dm, s.CampaignId, "page=2&pageSize=2");

        Assert.Equal(3, asDm.Total);
        Assert.Equal(2, asPlayer.Total);
        Assert.All(asPlayer.Items, t => Assert.Equal("Mío", t.CharacterName));
        Assert.Equal(2, asPlayer.Items[0].Quantity); // newest first
        Assert.Equal("Suyo", Assert.Single(filtered.Items).CharacterName);
        Assert.Empty(spy.Items);
        Assert.Single(paged.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync($"/api/v1/campaigns/{s.CampaignId}/transactions")).StatusCode);
    }

    // ---- Concurrency -----------------------------------------------------------------------------

    [Fact]
    public async Task Concurrent_updates_of_the_last_stock_fail_on_the_stale_version()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var last = await s.Dm.AddShopItemAsync(shop.Id, new { overrides = new { name = "Última poción", category = "Consumable" }, priceCp = 50, stock = 1 });

        // Two requests load the shop at the same time; both see one unit left.
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
        var shopA = await dbA.Shops.Include(x => x.Items).SingleAsync(x => x.Id == shop.Id);
        var shopB = await dbB.Shops.Include(x => x.Items).SingleAsync(x => x.Id == shop.Id);

        shopA.SellToCharacter(last.Id, 1, DateTimeOffset.UtcNow);
        shopB.SellToCharacter(last.Id, 1, DateTimeOffset.UtcNow);
        await dbA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
        Assert.Equal(0, Assert.Single((await s.Dm.Client.GetFromJsonAsync<ShopDto>(ItemTestHelpers.ShopUrl(shop.Id)))!.Items).Stock);
    }

    [Fact]
    public async Task Concurrent_money_changes_of_a_character_fail_on_the_stale_version()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        await s.Dm.GiveMoneyAsync(character.Id, 100);

        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();
        var characterA = await dbA.Characters.SingleAsync(x => x.Id == character.Id);
        var characterB = await dbB.Characters.SingleAsync(x => x.Id == character.Id);

        characterA.AdjustMoney(-100, DateTimeOffset.UtcNow);
        characterB.AdjustMoney(-100, DateTimeOffset.UtcNow);
        await dbA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
        Assert.Equal(0, (await s.Player.GetInventoryAsync(character.Id)).CopperPieces);
    }

    private static async Task<PagedResult<TransactionDto>> GetTransactionsAsync(SignedInUser actor, Guid campaignId, string query)
    {
        var response = await actor.Client.GetAsync($"/api/v1/campaigns/{campaignId}/transactions?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<TransactionDto>>())!;
    }
}
