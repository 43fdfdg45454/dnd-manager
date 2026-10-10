using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Items;

namespace OpenTrpg.Core.Api.Tests.Items;

/// <summary>HTTP helpers shared by the item, inventory and shop tests.</summary>
internal static class ItemTestHelpers
{
    public static string CharacterUrl(Guid id) => $"/api/v1/characters/{id}";

    public static string InventoryUrl(Guid characterId) => $"{CharacterUrl(characterId)}/inventory";

    public static string ItemsUrl(Guid campaignId) => $"/api/v1/campaigns/{campaignId}/items";

    public static string ShopUrl(Guid shopId) => $"/api/v1/shops/{shopId}";

    /// <summary>Id of an SRD item by exact name (the first one when the dataset repeats the name).</summary>
    public static async Task<Guid> SrdItemIdAsync(this SignedInUser actor, string name)
    {
        var response = await actor.Client.GetAsync($"/api/v1/catalog/items?search={Uri.EscapeDataString(name)}&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content.ReadFromJsonAsync<PagedResult<ItemSummaryDto>>())!;
        return page.Items.First(i => i.Name == name).Id;
    }

    public static async Task<CharacterDetailDto> CreateCharacterAsync(this SignedInUser actor, Guid campaignId, string name = "Héroe")
    {
        var response = await actor.Client.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/characters", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    /// <summary>Character of <paramref name="owner"/> activated by <paramref name="dm"/>.</summary>
    public static async Task<CharacterDetailDto> CreateActiveCharacterAsync(this SignedInUser owner, SignedInUser dm, Guid campaignId, string name = "Activo")
    {
        var character = await owner.CreateCharacterAsync(campaignId, name);
        var response = await dm.Client.PostAsync($"{CharacterUrl(character.Id)}/activate", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    public static async Task<InventoryDto> GiveMoneyAsync(this SignedInUser dm, Guid characterId, int deltaCp)
    {
        var response = await dm.Client.PostAsJsonAsync($"{CharacterUrl(characterId)}/money", new { deltaCp, reason = "Prueba" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InventoryDto>())!;
    }

    public static async Task<CharacterItemDto> AddItemAsync(this SignedInUser actor, Guid characterId, object body)
    {
        var response = await actor.Client.PostAsJsonAsync(InventoryUrl(characterId), body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterItemDto>())!;
    }

    public static async Task<InventoryDto> GetInventoryAsync(this SignedInUser actor, Guid characterId)
    {
        var response = await actor.Client.GetAsync(InventoryUrl(characterId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InventoryDto>())!;
    }

    public static async Task<CharacterDetailDto> GetCharacterAsync(this SignedInUser actor, Guid characterId)
    {
        var response = await actor.Client.GetAsync(CharacterUrl(characterId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    public static async Task<HttpResponseMessage> PatchItemAsync(this SignedInUser actor, Guid characterId, Guid itemId, object body) =>
        await actor.Client.PatchAsJsonAsync($"{InventoryUrl(characterId)}/{itemId}", body);

    public static async Task<ChangeRequestDto> ApproveAsync(this SignedInUser dm, Guid requestId)
    {
        var response = await dm.Client.PostAsync($"/api/v1/change-requests/{requestId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
    }

    public static async Task<ItemDetailDto> CreateHomebrewAsync(this SignedInUser dm, Guid campaignId, object body)
    {
        var response = await dm.Client.PostAsJsonAsync(ItemsUrl(campaignId), body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ItemDetailDto>())!;
    }

    /// <summary>Creates a shop as DM, opened unless <paramref name="open"/> is false.</summary>
    public static async Task<ShopDto> CreateShopAsync(this SignedInUser dm, Guid campaignId, string name = "Tienda", int? buybackPercent = null, bool open = true)
    {
        var response = await dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/shops", new { name, buybackPercent });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var shop = (await response.Content.ReadFromJsonAsync<ShopDto>())!;
        if (!open)
        {
            return shop;
        }

        var patch = await dm.Client.PatchAsJsonAsync(ShopUrl(shop.Id), new { isOpen = true });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        return (await patch.Content.ReadFromJsonAsync<ShopDto>())!;
    }

    public static async Task<ShopItemDto> AddShopItemAsync(this SignedInUser dm, Guid shopId, object body)
    {
        var response = await dm.Client.PostAsJsonAsync($"{ShopUrl(shopId)}/items", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ShopItemDto>())!;
    }

    public static Task<HttpResponseMessage> BuyAsync(this SignedInUser actor, Guid shopId, Guid characterId, Guid shopItemId, int quantity = 1) =>
        actor.Client.PostAsJsonAsync($"{ShopUrl(shopId)}/buy", new { characterId, shopItemId, quantity });

    public static Task<HttpResponseMessage> SellAsync(this SignedInUser actor, Guid shopId, Guid characterId, Guid itemId, int quantity = 1) =>
        actor.Client.PostAsJsonAsync($"{ShopUrl(shopId)}/sell", new { characterId, itemId, quantity });

    public static async Task<TradeResultDto> ReadTradeAsync(this HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TradeResultDto>())!;
    }

    /// <summary>An active level 3 fighter of <paramref name="owner"/>, activated by <paramref name="dm"/>.</summary>
    public static async Task<CharacterDetailDto> ActiveFighterAsync(SignedInUser owner, SignedInUser dm, Guid campaignId, string name)
    {
        var character = await owner.CreateCharacterAsync(campaignId, name);
        var patch = await owner.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", level = 3 } },
            baseAbilities = new { str = 16, dex = 12, con = 14, @int = 10, wis = 10, cha = 10 },
            applyRacialBonuses = false,
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var activate = await dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        return (await activate.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }
}
