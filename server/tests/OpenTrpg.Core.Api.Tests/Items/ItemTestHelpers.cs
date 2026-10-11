using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
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

    /// <summary>Prefix of the D&amp;D 5e routes of a character (phase 32B).</summary>
    public static string Dnd5eCharacterUrl(Guid id) => $"/api/v1/systems/dnd5e/characters/{id}";

    public static string InventoryUrl(Guid characterId) => $"{CharacterUrl(characterId)}/inventory";

    public static string ItemsUrl(Guid campaignId) => $"/api/v1/campaigns/{campaignId}/items";

    public static string ShopUrl(Guid shopId) => $"/api/v1/shops/{shopId}";

    /// <summary>Id of an SRD item by exact name (the first one when the dataset repeats the name).</summary>
    public static async Task<Guid> SrdItemIdAsync(this SignedInUser actor, string name)
    {
        var response = await actor.Client.GetAsync($"/api/v1/systems/dnd5e/catalog/items?search={Uri.EscapeDataString(name)}&pageSize=50");
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

    /// <summary>
    /// Answers the level-1 choices of the first class (<c>class.*</c> keys of <c>origin-choices</c>: fighting style,
    /// expertise...) that block the activation, with the first eligible options, preferring the ones without numeric
    /// effects so the sheet of the test stays as it was. Choices of an option are answered once it is picked.
    /// </summary>
    public static async Task AnswerClassChoicesAsync(this HttpClient client, Guid characterId)
    {
        var url = $"{Dnd5eCharacterUrl(characterId)}/origin-choices";
        var plan = JsonNode.Parse(await client.GetStringAsync(url))!;
        var choices = plan["choices"]!.AsArray().Select(c => c!.AsObject())
            .Where(c => c["key"]!.GetValue<string>().StartsWith("class.", StringComparison.Ordinal) && c["required"]!.GetValue<int>() > 0)
            .ToList();
        if (choices.Count == 0)
        {
            return;
        }

        var picked = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        bool Met(JsonNode? requires) => requires is null
            || (picked.TryGetValue(requires["choiceKey"]!.GetValue<string>(), out var parent) && parent.Contains(requires["index"]!.GetValue<string>()));

        bool progress;
        do
        {
            progress = false;
            foreach (var choice in choices.Where(c => !picked.ContainsKey(c["key"]!.GetValue<string>()) && Met(c["requires"])).ToList())
            {
                var key = choice["key"]!.GetValue<string>();
                var required = choice["required"]!.GetValue<int>();
                picked[key] = choice["freeText"]!.GetValue<bool>()
                    ? Enumerable.Range(1, required).Select(n => $"Valor {n}").ToList()
                    : choice["options"]!.AsArray()
                        .Where(o => o!["eligible"]!.GetValue<bool>() && Met(o["requires"]))
                        .OrderBy(o => o!["effectsPreview"]?.AsArray().Count ?? 0)
                        .Take(required)
                        .Select(o => o!["index"]!.GetValue<string>())
                        .ToList();
                progress = true;
            }
        }
        while (progress);

        var saved = await client.PutAsJsonAsync(url, new { choices = picked.Select(p => new { key = p.Key, selected = p.Value }) });
        Assert.True(saved.StatusCode == HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
    }

    /// <summary>Activation by <paramref name="dm"/> after answering the level-1 choices of the first class.</summary>
    public static async Task<HttpResponseMessage> ActivateAsync(this SignedInUser dm, Guid characterId)
    {
        await dm.Client.AnswerClassChoicesAsync(characterId);
        return await dm.Client.PostAsync($"{CharacterUrl(characterId)}/activate", null);
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
        var patch = await owner.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", level = 3 } },
            baseAbilities = new { str = 16, dex = 12, con = 14, @int = 10, wis = 10, cha = 10 },
            applyRacialBonuses = false,
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var activate = await dm.ActivateAsync(character.Id);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        return (await activate.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }
}
