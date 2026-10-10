using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Api.Tests.Party;
using OpenTrpg.Core.Application.Campaigns;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Items;

namespace OpenTrpg.Core.Api.Tests.Items;

[Collection(CatalogCollection.Name)]
public class PartyStashEndpointsTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task Every_member_sees_the_stash_but_only_dms_manage_it()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var url = StashUrl(s.CampaignId);

        var empty = await GetStashAsync(s.Player, s.CampaignId);

        Assert.Equal((0L, true), (empty.CopperPieces, empty.PlayersCanTakeFromStash));
        Assert.Empty(empty.Items);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.PostAsJsonAsync($"{url}/items", new { overrides = new { name = "Gema" }, quantity = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.PostAsJsonAsync($"{url}/gold", new { deltaCp = 100 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Player.Client.PostAsJsonAsync($"{url}/gold/split", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Outsider.Client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Dm_adds_edits_and_removes_loot()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var gem = await AddAsync(s.Dm, s.CampaignId, new { overrides = new { name = "Gema roja", category = "Other" }, quantity = 3, notes = "Del dragón" });

        Assert.Equal(("Gema roja", 3, "Del dragón", "Dm User"), (gem.Item.Name, gem.Quantity, gem.Notes, gem.AddedByDisplayName));

        var patch = await s.Dm.Client.PatchAsJsonAsync($"{StashUrl(s.CampaignId)}/items/{gem.Id}", new { quantity = 2, notes = (string?)null });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var patched = (await patch.Content.ReadFromJsonAsync<PartyStashItemDto>())!;
        Assert.Equal((2, (string?)null), (patched.Quantity, patched.Notes));

        Assert.Equal(HttpStatusCode.NoContent, (await s.Dm.Client.DeleteAsync($"{StashUrl(s.CampaignId)}/items/{gem.Id}")).StatusCode);
        Assert.Empty((await GetStashAsync(s.Dm, s.CampaignId)).Items);
        var log = await TransactionsAsync(s.Dm, s.CampaignId);
        Assert.Equal(["StashRemove", "StashAdd"], log.Items.Select(t => t.Type));
        Assert.All(log.Items, t => Assert.Equal(((Guid?)null, (Guid?)null, "Dm User"), (t.ShopId, t.CharacterId, t.ActorDisplayName)));
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PostAsJsonAsync($"{StashUrl(s.CampaignId)}/items", new { quantity = 1 })).StatusCode);
    }

    [Fact]
    public async Task Taking_two_of_five_leaves_three_and_creates_the_item_on_the_character()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Héroe");
        var arrows = await AddAsync(s.Dm, s.CampaignId, new { templateId = await s.Dm.SrdItemIdAsync("Arrow"), quantity = 5 });

        var stash = await TakeAsync(s.Player, s.CampaignId, arrows.Id, hero.Id, 2);

        Assert.Equal(3, Assert.Single(stash.Items).Quantity);
        var item = Assert.Single((await s.Player.GetInventoryAsync(hero.Id)).Items);
        Assert.Equal(("Arrow", 2), (item.Effective.Name, item.Quantity));
        var response = await s.Player.Client.PostAsJsonAsync($"{StashUrl(s.CampaignId)}/items/{arrows.Id}/take", new { characterId = hero.Id, quantity = 4 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Taking_everything_removes_the_entry()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Héroe");
        var gem = await AddAsync(s.Dm, s.CampaignId, new { overrides = new { name = "Gema" }, quantity = 1 });

        var stash = await TakeAsync(s.Player, s.CampaignId, gem.Id, hero.Id, 1);

        Assert.Empty(stash.Items);
        Assert.Equal("Gema", Assert.Single((await s.Player.GetInventoryAsync(hero.Id)).Items).Effective.Name);
    }

    [Fact]
    public async Task When_players_cannot_take_only_the_dm_moves_loot()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Héroe");
        var gem = await AddAsync(s.Dm, s.CampaignId, new { overrides = new { name = "Gema" }, quantity = 2 });
        var settings = await s.Dm.Client.PatchAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/settings", new { playersCanTakeFromStash = false });
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        Assert.False((await settings.Content.ReadFromJsonAsync<CampaignDto>())!.PlayersCanTakeFromStash);

        var asPlayer = await s.Player.Client.PostAsJsonAsync($"{StashUrl(s.CampaignId)}/items/{gem.Id}/take", new { characterId = hero.Id, quantity = 1 });
        var stash = await TakeAsync(s.Dm, s.CampaignId, gem.Id, hero.Id, 1);
        var itemId = Assert.Single((await s.Player.GetInventoryAsync(hero.Id)).Items).Id;
        var giveBack = await s.Player.Client.PostAsJsonAsync($"{StashUrl(s.CampaignId)}/items/return", new { characterId = hero.Id, characterItemId = itemId, quantity = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, asPlayer.StatusCode);
        Assert.Equal(1, Assert.Single(stash.Items).Quantity);
        Assert.False(stash.PlayersCanTakeFromStash);
        Assert.Equal(HttpStatusCode.Forbidden, giveBack.StatusCode);
    }

    [Fact]
    public async Task Players_only_take_with_their_own_characters()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateSignedInUserAsync("Other Player");
        await s.Owner.AddMemberAsync(s.CampaignId, other, CampaignScenario.PlayerRole);
        var theirs = await other.CreateCharacterAsync(s.CampaignId, "Suyo");
        var gem = await AddAsync(s.Dm, s.CampaignId, new { overrides = new { name = "Gema" }, quantity = 2 });

        var response = await s.Player.Client.PostAsJsonAsync($"{StashUrl(s.CampaignId)}/items/{gem.Id}/take", new { characterId = theirs.Id, quantity = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Giving_back_stacks_on_the_stash_entry()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Héroe");
        var arrows = await AddAsync(s.Dm, s.CampaignId, new { templateId = await s.Dm.SrdItemIdAsync("Arrow"), quantity = 5 });
        await TakeAsync(s.Player, s.CampaignId, arrows.Id, hero.Id, 2);
        var taken = Assert.Single((await s.Player.GetInventoryAsync(hero.Id)).Items);

        var response = await s.Player.Client.PostAsJsonAsync($"{StashUrl(s.CampaignId)}/items/return", new { characterId = hero.Id, characterItemId = taken.Id, quantity = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stash = (await response.Content.ReadFromJsonAsync<PartyStashDto>())!;
        Assert.Equal((arrows.Id, 4), (Assert.Single(stash.Items).Id, stash.Items[0].Quantity));
        Assert.Equal(1, Assert.Single((await s.Player.GetInventoryAsync(hero.Id)).Items).Quantity);
    }

    [Fact]
    public async Task Gold_is_added_withdrawn_and_never_below_zero()
    {
        var s = await factory.CreateCampaignScenarioAsync();

        await GoldAsync(s.Dm, s.CampaignId, 500);
        var stash = await GoldAsync(s.Dm, s.CampaignId, -200);
        var tooMuch = await s.Dm.Client.PostAsJsonAsync($"{StashUrl(s.CampaignId)}/gold", new { deltaCp = -301 });

        Assert.Equal(300, stash.CopperPieces);
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Dm.Client.PostAsJsonAsync($"{StashUrl(s.CampaignId)}/gold", new { deltaCp = 0 })).StatusCode);
        var log = await TransactionsAsync(s.Dm, s.CampaignId);
        Assert.Equal([("StashGoldAdd", 200), ("StashGoldAdd", 500)], log.Items.Select(t => (t.Type, t.TotalCp)));
    }

    [Fact]
    public async Task Splitting_1000_cp_among_three_leaves_1_in_the_stash()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var characters = new[]
        {
            await PartyEndpointsTests.ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "A"),
            await PartyEndpointsTests.ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "B"),
            await PartyEndpointsTests.ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "C"),
        };
        await s.Player.CreateCharacterAsync(s.CampaignId, "Borrador sin parte");
        await GoldAsync(s.Dm, s.CampaignId, 1000);

        var response = await s.Dm.Client.PostAsJsonAsync($"{StashUrl(s.CampaignId)}/gold/split", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await response.Content.ReadFromJsonAsync<PartyStashDto>())!.CopperPieces);
        foreach (var character in characters)
        {
            Assert.Equal(333, (await s.Player.GetInventoryAsync(character.Id)).CopperPieces);
        }

        var splits = (await TransactionsAsync(s.Dm, s.CampaignId)).Items.Where(t => t.Type == "StashGoldSplit").ToList();
        Assert.Equal(3, splits.Count);
        Assert.All(splits, t => Assert.Equal(333, t.TotalCp));
    }

    [Fact]
    public async Task Splitting_among_some_characters_only_pays_them()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var a = await PartyEndpointsTests.ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "A");
        var b = await PartyEndpointsTests.ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "B");
        await GoldAsync(s.Dm, s.CampaignId, 101);

        var response = await s.Dm.Client.PostAsJsonAsync($"{StashUrl(s.CampaignId)}/gold/split", new { characterIds = new[] { a.Id } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, (await response.Content.ReadFromJsonAsync<PartyStashDto>())!.CopperPieces);
        Assert.Equal((101, 0), ((await s.Player.GetInventoryAsync(a.Id)).CopperPieces, (await s.Player.GetInventoryAsync(b.Id)).CopperPieces));
    }

    [Fact]
    public async Task Transactions_list_the_take_with_its_actor()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Héroe");
        var gem = await AddAsync(s.Dm, s.CampaignId, new { overrides = new { name = "Gema" }, quantity = 2 });
        await TakeAsync(s.Player, s.CampaignId, gem.Id, hero.Id, 1);

        var asPlayer = await TransactionsAsync(s.Player, s.CampaignId);

        var take = Assert.Single(asPlayer.Items);
        Assert.Equal(("StashTake", "Gema", 1, 0), (take.Type, take.ItemName, take.Quantity, take.TotalCp));
        Assert.Equal((hero.Id, "Héroe", s.Player.Id, "Player User"), (take.CharacterId, take.CharacterName, take.ActorUserId, take.ActorDisplayName));
        Assert.Null(take.ShopId);
        Assert.Null(take.ShopName);
        Assert.Equal(2, (await TransactionsAsync(s.Dm, s.CampaignId)).Total);
    }

    [Fact]
    public async Task Purchases_record_their_actor_too()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Héroe");
        await s.Dm.GiveMoneyAsync(hero.Id, 100);
        var shop = await s.Dm.CreateShopAsync(s.CampaignId);
        var bread = await s.Dm.AddShopItemAsync(shop.Id, new { overrides = new { name = "Pan" }, priceCp = 2 });

        var trade = await (await s.Player.BuyAsync(shop.Id, hero.Id, bread.Id)).ReadTradeAsync();

        Assert.Equal((shop.Id, "Tienda", "Player User"), (trade.Transaction.ShopId, trade.Transaction.ShopName, trade.Transaction.ActorDisplayName));
    }

    private static string StashUrl(Guid campaignId) => $"/api/v1/campaigns/{campaignId}/stash";

    private static async Task<PartyStashDto> GetStashAsync(SignedInUser actor, Guid campaignId)
    {
        var response = await actor.Client.GetAsync(StashUrl(campaignId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyStashDto>())!;
    }

    private static async Task<PartyStashItemDto> AddAsync(SignedInUser dm, Guid campaignId, object body)
    {
        var response = await dm.Client.PostAsJsonAsync($"{StashUrl(campaignId)}/items", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyStashItemDto>())!;
    }

    private static async Task<PartyStashDto> TakeAsync(SignedInUser actor, Guid campaignId, Guid itemId, Guid characterId, int quantity)
    {
        var response = await actor.Client.PostAsJsonAsync($"{StashUrl(campaignId)}/items/{itemId}/take", new { characterId, quantity });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyStashDto>())!;
    }

    private static async Task<PartyStashDto> GoldAsync(SignedInUser dm, Guid campaignId, int deltaCp)
    {
        var response = await dm.Client.PostAsJsonAsync($"{StashUrl(campaignId)}/gold", new { deltaCp });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PartyStashDto>())!;
    }

    private static async Task<PagedResult<TransactionDto>> TransactionsAsync(SignedInUser actor, Guid campaignId)
    {
        var response = await actor.Client.GetAsync($"/api/v1/campaigns/{campaignId}/transactions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<TransactionDto>>())!;
    }
}
