using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenTrpg.Core.Domain.Characters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTrpg.Core.Domain.Rules;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Rules;

namespace OpenTrpg.Core.Api.Tests.Regression;

/// <summary>
/// API regression of the server split (phase 32): walks every route the installed app uses (the 5e routes of
/// phase 30 §4.6 and the core ones) with one test character and compares status and JSON body with the
/// capture taken before the split (<c>Fixtures/api-before-32.json</c>). Ids and timestamps are normalized.
/// Set <c>UPDATE_API_FIXTURES=1</c> to rewrite the fixture.
/// </summary>
public sealed class ApiRegressionTests(RegressionApiFactory factory) : IClassFixture<RegressionApiFactory>
{
    private const string FixtureName = "api-before-32.json";

    [Fact]
    public async Task Every_route_answers_as_before_the_split()
    {
        var run = new RegressionRun(factory);
        await run.ExecuteAsync();
        var actual = run.Render();

        var fixturePath = Path.GetFullPath(Path.Combine(SourceDirectory(), "..", "Fixtures", FixtureName));
        if (Environment.GetEnvironmentVariable("UPDATE_API_FIXTURES") == "1" || !File.Exists(fixturePath))
        {
            await File.WriteAllTextAsync(fixturePath, actual);
            return;
        }

        var expected = await File.ReadAllTextAsync(fixturePath);
        if (Canonical(expected) != Canonical(actual))
        {
            var actualPath = Path.ChangeExtension(fixturePath, ".actual.json");
            await File.WriteAllTextAsync(actualPath, actual);
            Assert.Fail($"The API answers differ from {FixtureName} (first difference at step '{FirstDifference(expected, actual)}'); actual written to {actualPath}.");
        }
    }

    /// <summary>
    /// The answers with the keys of every object sorted: since the split the fields of the game system are written
    /// after the core ones, so only the order of the keys may change.
    /// </summary>
    private static string Canonical(string json) => Sorted(JsonNode.Parse(json))?.ToJsonString() ?? "null";

    private static JsonNode? Sorted(JsonNode? node) => node switch
    {
        JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Sorted(p.Value)))),
        JsonArray a => new JsonArray(a.Select(Sorted).ToArray()),
        null => null,
        _ => node.DeepClone(),
    };

    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    private static string FirstDifference(string expected, string actual)
    {
        var expectedSteps = JsonNode.Parse(expected)!.AsArray();
        var actualSteps = JsonNode.Parse(actual)!.AsArray();
        for (var i = 0; i < Math.Max(expectedSteps.Count, actualSteps.Count); i++)
        {
            var e = i < expectedSteps.Count ? Sorted(expectedSteps[i])?.ToJsonString() : null;
            var a = i < actualSteps.Count ? Sorted(actualSteps[i])?.ToJsonString() : null;
            if (e != a)
            {
                return ((i < actualSteps.Count ? actualSteps[i] : expectedSteps[i])!["step"]!).GetValue<string>();
            }
        }

        return "?";
    }
}

/// <summary>Runs the regression scenario step by step, keeping the normalized answers.</summary>
internal sealed partial class RegressionRun(RegressionApiFactory factory)
{
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly JsonArray _steps = [];
    private readonly Dictionary<string, string> _ids = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, string> _names = [];

    private SignedInUser _owner = null!;
    private SignedInUser _dm = null!;
    private SignedInUser _player = null!;

    public string Render() => _steps.ToJsonString(Indented) + "\n";

    public async Task ExecuteAsync()
    {
        _owner = await factory.CreateSignedInUserAsync("Owner User", "owner-regression@example.com");
        _dm = await factory.CreateSignedInUserAsync("Dm User", "dm-regression@example.com");
        _player = await factory.CreateSignedInUserAsync("Player User", "player-regression@example.com");
        Remember(_owner.Id, "owner");
        Remember(_dm.Id, "dm");
        Remember(_player.Id, "player");

        var campaign = await Step("campaign.create", _owner, HttpMethod.Post, "/api/v1/campaigns", new { name = "Regresión", description = "Campaña de regresión" });
        var c = Id(campaign, "id");
        Remember(Guid.Parse(c), "campaign");
        await AddMemberAsync(c, _dm, "DM");
        await AddMemberAsync(c, _player, "Player");

        await Step("systems.list", _player, HttpMethod.Get, "/api/v1/systems");
        await Step("campaign.get", _player, HttpMethod.Get, $"/api/v1/campaigns/{c}");
        await Step("campaign.list", _player, HttpMethod.Get, "/api/v1/campaigns");

        await CatalogAsync();
        var id = await CharacterLifecycleAsync(c);
        await TrackingAsync(id);
        await PartyAndLevelUpAsync(c, id);
        await InventoryAsync(c, id);
        await RestRequestsAsync(c, id);
        await HomebrewAndShopsAsync(c, id);
        await StashAndMessagesAsync(c, id);
        await OwnershipAsync(c, id);
    }

    private async Task CatalogAsync()
    {
        const string catalog = "/api/v1/systems/dnd5e/catalog";
        await Step("catalog.attribution", _player, HttpMethod.Get, $"{catalog}/attribution");
        await Step("catalog.classes", _player, HttpMethod.Get, $"{catalog}/classes");
        await Step("catalog.class", _player, HttpMethod.Get, $"{catalog}/classes/wizard");
        await Step("catalog.races", _player, HttpMethod.Get, $"{catalog}/races");
        await Step("catalog.race", _player, HttpMethod.Get, $"{catalog}/races/dwarf");
        await Step("catalog.spells", _player, HttpMethod.Get, $"{catalog}/spells?search=fire&pageSize=10");
        await Step("catalog.spell", _player, HttpMethod.Get, $"{catalog}/spells/fire-bolt");
        await Step("catalog.beasts", _player, HttpMethod.Get, $"{catalog}/beasts?q=wolf");
        await Step("catalog.beast", _player, HttpMethod.Get, $"{catalog}/beasts/wolf");
        var items = await Step("catalog.items", _player, HttpMethod.Get, $"{catalog}/items?search=Longsword&pageSize=5");
        _ids["longsword"] = items!["items"]!.AsArray().First(i => i!["name"]!.GetValue<string>() == "Longsword")!["id"]!.GetValue<string>();
        await Step("catalog.item", _player, HttpMethod.Get, $"{catalog}/items/{_ids["longsword"]}");
        var potions = await Step("catalog.items.potion", _player, HttpMethod.Get, $"{catalog}/items?search=Potion%20of%20Healing&pageSize=5");
        _ids["potion"] = potions!["items"]!.AsArray().First(i => i!["name"]!.GetValue<string>() == "Potion of Healing")!["id"]!.GetValue<string>();
        await Step("catalog.trinkets", _player, HttpMethod.Get, $"{catalog}/trinkets");
        await Step("catalog.roll-tables", _player, HttpMethod.Get, $"{catalog}/roll-tables");
        await Step("catalog.conditions", _player, HttpMethod.Get, $"{catalog}/conditions");
        await Step("catalog.skills", _player, HttpMethod.Get, $"{catalog}/skills");
        await Step("catalog.backgrounds", _player, HttpMethod.Get, $"{catalog}/backgrounds");
        await Step("catalog.equipment-category", _player, HttpMethod.Get, $"{catalog}/equipment-categories/arcane-foci");
        await Step("catalog.feature", _player, HttpMethod.Get, $"{catalog}/features/arcane-recovery");
        await Step("catalog.sources", _dm, HttpMethod.Get, $"{catalog}/sources");
    }

    private async Task<string> CharacterLifecycleAsync(string c)
    {
        var created = await Step("character.create", _player, HttpMethod.Post, $"/api/v1/campaigns/{c}/characters", new { name = "Regresión", heightInches = 52, weightPounds = 150 });
        var id = Id(created, "id");
        Remember(Guid.Parse(id), "character");
        var url = $"/api/v1/characters/{id}";

        await Step("character.sheet.draft", _player, HttpMethod.Patch, $"{Dnd5e(url)}/sheet", new
        {
            raceIndex = "dwarf",
            subraceIndex = "hill-dwarf",
            backgroundIndex = "acolyte",
            alignment = "Legal bueno",
            hpMode = "Average",
            baseAbilities = new { str = 8, dex = 14, con = 14, @int = 15, wis = 12, cha = 10 },
            classes = new[] { new { classIndex = "wizard", subclassIndex = (string?)null, level = 1 } },
            spells = new[]
            {
                new { spellIndex = "fire-bolt", classIndex = "wizard", isPrepared = true },
                new { spellIndex = "magic-missile", classIndex = "wizard", isPrepared = true },
                new { spellIndex = "shield", classIndex = "wizard", isPrepared = true },
                new { spellIndex = "mage-armor", classIndex = "wizard", isPrepared = false },
                new { spellIndex = "sleep", classIndex = "wizard", isPrepared = false },
            },
            notes = "Notas de prueba",
            backstory = "Historia",
        });

        var origin = await Step("character.origin-choices.get", _player, HttpMethod.Get, $"{Dnd5e(url)}/origin-choices");
        await Step("character.origin-choices.put", _player, HttpMethod.Put, $"{Dnd5e(url)}/origin-choices", new JsonObject { ["choices"] = AutoAnswers(origin?["choices"]?.AsArray()) });
        await Step("character.spell-preparation.get.draft", _player, HttpMethod.Get, $"{Dnd5e(url)}/spell-preparation");
        await Step("character.get.draft", _player, HttpMethod.Get, url);
        await Step("character.list.player", _player, HttpMethod.Get, $"/api/v1/campaigns/{c}/characters");
        await Step("character.list.dm", _dm, HttpMethod.Get, $"/api/v1/campaigns/{c}/characters");

        var submitted = await Step("character.submit", _player, HttpMethod.Post, $"{url}/submit");
        var activate = Id(submitted, "id");
        await Step("change-requests.list", _dm, HttpMethod.Get, $"/api/v1/campaigns/{c}/change-requests");
        await Step("change-request.get", _player, HttpMethod.Get, $"/api/v1/change-requests/{activate}");
        await Step("change-request.approve.activate", _dm, HttpMethod.Post, $"/api/v1/change-requests/{activate}/approve", new { comment = "Adelante" });
        await Step("character.get.active", _player, HttpMethod.Get, url);

        var edit = await Step("character.sheet.request", _player, HttpMethod.Patch, $"{Dnd5e(url)}/sheet", new { notes = "Notas nuevas", alignment = "Neutral" });
        await Step("change-request.reject", _dm, HttpMethod.Post, $"/api/v1/change-requests/{Id(edit, "id")}/reject", new { comment = "No" });
        var edit2 = await Step("character.sheet.request.2", _player, HttpMethod.Patch, $"{Dnd5e(url)}/sheet", new { backstory = "Otra historia" });
        await Step("change-request.cancel", _player, HttpMethod.Post, $"/api/v1/change-requests/{Id(edit2, "id")}/cancel");
        var edit3 = await Step("character.sheet.request.3", _player, HttpMethod.Patch, $"{Dnd5e(url)}/sheet", new { name = "Regresión II", overrides = new[] { new { field = "speed", value = 30, note = "Botas" } } });
        await Step("change-request.approve.sheet", _dm, HttpMethod.Post, $"/api/v1/change-requests/{Id(edit3, "id")}/approve");
        await Step("character.sheet.dm", _dm, HttpMethod.Patch, $"{Dnd5e(url)}/sheet", new { notes = "Notas del DM" });
        return id;
    }

    private async Task TrackingAsync(string id)
    {
        var url = $"/api/v1/characters/{id}";
        await Step("combat.update", _player, HttpMethod.Patch, $"{Dnd5e(url)}/combat", new { temporaryHitPoints = 3, inspiration = true, conditions = new[] { new { index = "poisoned", note = "Veneno" } } });
        await Step("concentration.start", _player, HttpMethod.Post, $"{Dnd5e(url)}/concentration", new { spellIndex = "shield" });
        await Step("damage", _player, HttpMethod.Post, $"{Dnd5e(url)}/damage", new { amount = 5 });
        await Step("concentration.stop", _player, HttpMethod.Post, $"{Dnd5e(url)}/concentration", new { spellIndex = (string?)null });
        await Step("spell-slots.spend", _player, HttpMethod.Post, $"{Dnd5e(url)}/spell-slots/1/spend", new { amount = 1 });
        await Step("spell-slots.restore", _player, HttpMethod.Post, $"{Dnd5e(url)}/spell-slots/1/restore");
        await Step("spell-slots.spend.again", _player, HttpMethod.Post, $"{Dnd5e(url)}/spell-slots/1/spend");
        await Step("class-action.arcane-recovery", _player, HttpMethod.Post, $"{Dnd5e(url)}/class-actions/arcane-recovery", new { slotLevels = new[] { 1 } });
        await Step("class-action.rage", _player, HttpMethod.Post, $"{Dnd5e(url)}/class-actions/rage");
        await Step("class-action.unknown", _player, HttpMethod.Post, $"{Dnd5e(url)}/class-actions/fly");

        var withResource = await Step("resources.add", _player, HttpMethod.Post, $"{Dnd5e(url)}/resources", new { name = "Suerte", max = 3, recharge = "LongRest" });
        var resource = Id(withResource, "id");
        await Step("resources.spend", _player, HttpMethod.Post, $"{Dnd5e(url)}/resources/{resource}/spend", new { amount = 2 });
        await Step("resources.restore", _player, HttpMethod.Post, $"{Dnd5e(url)}/resources/{resource}/restore");
        await Step("resources.rolls", _player, HttpMethod.Post, $"{Dnd5e(url)}/resources/{resource}/rolls", new { values = new[] { 1 } });
        await Step("resources.delete", _player, HttpMethod.Delete, $"{Dnd5e(url)}/resources/{resource}");

        await Step("rest.short.player", _player, HttpMethod.Post, $"{Dnd5e(url)}/rest/short", new { hitDice = new Dictionary<string, int> { ["wizard"] = 1 } });
        await Step("rest.short", _dm, HttpMethod.Post, $"{Dnd5e(url)}/rest/short", new { hitDice = new Dictionary<string, int> { ["wizard"] = 1 } });
        await Step("rest.long", _dm, HttpMethod.Post, $"{Dnd5e(url)}/rest/long");

        await Step("invalid-choices.get", _player, HttpMethod.Get, $"{Dnd5e(url)}/invalid-choices");
        await Step("invalid-choices.post", _player, HttpMethod.Post, $"{Dnd5e(url)}/invalid-choices", new { choices = Array.Empty<object>() });

        await Step("companion.set", _player, HttpMethod.Put, $"{Dnd5e(url)}/companion", new { beastIndex = "wolf", name = "Lobo" });
        await Step("companion.hp", _player, HttpMethod.Post, $"{Dnd5e(url)}/companion/hp", new { delta = -1 });
        await Step("companion.delete", _dm, HttpMethod.Delete, $"{Dnd5e(url)}/companion");

        await Step("spell-preparation.get", _player, HttpMethod.Get, $"{Dnd5e(url)}/spell-preparation");
        await Step("spell-preparation.post.player", _player, HttpMethod.Post, $"{Dnd5e(url)}/spell-preparation", new { classes = new[] { new { classIndex = "wizard", spells = new[] { "magic-missile", "shield" } } } });
        await Step("spell-preparation.post.dm", _dm, HttpMethod.Post, $"{Dnd5e(url)}/spell-preparation", new { classes = new[] { new { classIndex = "wizard", spells = new[] { "magic-missile", "sleep" } } } });
        await Step("spell-preparation.keep", _dm, HttpMethod.Post, $"{Dnd5e(url)}/spell-preparation/keep");
    }

    private async Task PartyAndLevelUpAsync(string c, string id)
    {
        var party = $"/api/v1/systems/dnd5e/campaigns/{c}/party";
        await Step("party.get", _dm, HttpMethod.Get, party);
        await Step("party.get.player", _player, HttpMethod.Get, party);
        await Step("party.adjust", _dm, HttpMethod.Post, $"{party}/adjust", new object[]
        {
            new { characterId = id, hitPointsDelta = -2, temporaryHitPoints = 1, addConditions = new[] { new { index = "prone" } }, removeConditions = new[] { "poisoned" } },
        });
        await Step("party.rest.short", _dm, HttpMethod.Post, $"{party}/rest", new { kind = "short" });
        await Step("party.rest.long", _dm, HttpMethod.Post, $"{party}/rest", new { kind = "long", characterIds = new[] { id } });
        await Step("party.grant-level", _dm, HttpMethod.Post, $"{party}/grant-level", new { characterIds = new[] { id } });
        await Step("party.revoke-level", _dm, HttpMethod.Delete, $"{party}/grant-level", new { characterIds = new[] { id } });
        await Step("party.grant-level.again", _dm, HttpMethod.Post, $"{party}/grant-level");

        var url = $"/api/v1/characters/{id}";
        var plan = await Step("level-up.plan", _player, HttpMethod.Get, $"{Dnd5e(url)}/level-up");
        await Step("level-up.plan.class", _player, HttpMethod.Get, $"{Dnd5e(url)}/level-up?classIndex=wizard");
        await Step("level-up.apply", _player, HttpMethod.Post, $"{Dnd5e(url)}/level-up", new JsonObject
        {
            ["classIndex"] = "wizard",
            ["hitPointsRolled"] = 4,
            ["choices"] = AutoAnswers(plan?["choices"]?.AsArray()),
        });
        await Step("character.get.after-level-up", _player, HttpMethod.Get, url);
    }

    private async Task InventoryAsync(string c, string id)
    {
        var url = $"/api/v1/characters/{id}";
        await Step("inventory.get", _player, HttpMethod.Get, $"{url}/inventory");
        var sword = await Step("inventory.add.dm", _dm, HttpMethod.Post, $"{url}/inventory", new { templateId = _ids["longsword"], quantity = 1 });
        var swordId = Id(sword, "id");
        await Step("inventory.equip", _player, HttpMethod.Patch, $"{url}/inventory/{swordId}", new { equipped = true, notes = "Heredada" });
        var request = await Step("inventory.add.player", _player, HttpMethod.Post, $"{url}/inventory", new { templateId = _ids["potion"], quantity = 2 });
        await Step("change-request.approve.add-item", _dm, HttpMethod.Post, $"/api/v1/change-requests/{Id(request, "id")}/approve");
        var inventory = await Step("inventory.get.after", _player, HttpMethod.Get, $"{url}/inventory");
        var potion = FindItem(inventory, _ids["potion"], last: false);
        await Step("inventory.use", _player, HttpMethod.Post, $"{url}/inventory/{potion}/use");
        await Step("inventory.remove.player", _player, HttpMethod.Delete, $"{url}/inventory/{swordId}");
        await Step("money.dm", _dm, HttpMethod.Post, $"{url}/money", new { deltaCp = 5000, reason = "Botín" });
        var money = await Step("money.player", _player, HttpMethod.Post, $"{url}/money", new { deltaCp = -100, reason = "Gasto" });
        await Step("change-request.approve.money", _dm, HttpMethod.Post, $"/api/v1/change-requests/{Id(money, "id")}/approve");
        await Step("change-requests.list.after", _dm, HttpMethod.Get, $"/api/v1/campaigns/{c}/change-requests");
        await Step("character.get.after-inventory", _player, HttpMethod.Get, url);
    }

    private async Task RestRequestsAsync(string c, string id)
    {
        var url = $"/api/v1/characters/{id}/rest-requests";
        var shortRest = await Step("rest-request.create.short", _player, HttpMethod.Post, url, new { kind = "short", hitDice = new Dictionary<string, int> { ["wizard"] = 1 } });
        await Step("rest-request.list", _dm, HttpMethod.Get, $"/api/v1/campaigns/{c}/rest-requests");
        await Step("rest-request.get", _player, HttpMethod.Get, $"/api/v1/rest-requests/{Id(shortRest, "id")}");
        await Step("rest-request.approve.short", _dm, HttpMethod.Post, $"/api/v1/rest-requests/{Id(shortRest, "id")}/approve", new { comment = "Vale" });
        var longRest = await Step("rest-request.create.long", _player, HttpMethod.Post, url, new { kind = "long" });
        await Step("rest-request.reject", _dm, HttpMethod.Post, $"/api/v1/rest-requests/{Id(longRest, "id")}/reject", new { comment = "Aún no" });
        await Step("rest-request.create.long.2", _player, HttpMethod.Post, url, new { kind = "long" });
        await Step("rest-request.cancel", _player, HttpMethod.Delete, url);
        var longRest2 = await Step("rest-request.create.long.3", _player, HttpMethod.Post, url, new { kind = "long" });
        await Step("rest-request.approve.long", _dm, HttpMethod.Post, $"/api/v1/rest-requests/{Id(longRest2, "id")}/approve");
        await Step("character.get.after-rests", _player, HttpMethod.Get, $"/api/v1/characters/{id}");
    }

    private async Task HomebrewAndShopsAsync(string c, string id)
    {
        var items = $"/api/v1/campaigns/{c}/items";
        var homebrew = await Step("homebrew.create", _dm, HttpMethod.Post, items, new
        {
            name = "Espada de prueba",
            category = "Weapon",
            subcategory = "Martial Melee",
            rarity = "Uncommon",
            requiresAttunement = true,
            costCp = 1500,
            weightLb = 3,
            damageDice = "1d8",
            damageType = "slashing",
            properties = new[] { "versatile" },
            versatileDice = "1d10",
            description = new[] { "Una espada." },
            modifiers = new[] { new { kind = "AttackBonus", target = (string?)null, value = 1 } },
        });
        var template = Id(homebrew, "id");
        await Step("homebrew.list", _dm, HttpMethod.Get, items);
        await Step("homebrew.get", _player, HttpMethod.Get, $"{items}/{template}");
        await Step("homebrew.patch", _dm, HttpMethod.Patch, $"{items}/{template}", new { costCp = 2000, description = new[] { "Una espada mejor." } });
        await Step("catalog.item.homebrew", _player, HttpMethod.Get, $"/api/v1/systems/dnd5e/catalog/items/{template}");

        var shop = await Step("shop.create", _dm, HttpMethod.Post, $"/api/v1/campaigns/{c}/shops", new { name = "Herrería", description = "Armas", buybackPercent = 50 });
        var shopUrl = $"/api/v1/shops/{Id(shop, "id")}";
        await Step("shop.open", _dm, HttpMethod.Patch, shopUrl, new { isOpen = true });
        var shopItem = await Step("shop.item.add", _dm, HttpMethod.Post, $"{shopUrl}/items", new { templateId = template, priceCp = 2000, stock = 2 });
        await Step("shop.item.bulk", _dm, HttpMethod.Post, $"{shopUrl}/items/bulk", new { items = new[] { new { templateId = _ids["longsword"], priceCp = 1500 } } });
        await Step("shop.item.patch", _dm, HttpMethod.Patch, $"{shopUrl}/items/{Id(shopItem, "id")}", new { priceCp = 1800 });
        await Step("shop.get", _player, HttpMethod.Get, shopUrl);
        await Step("shops.list", _player, HttpMethod.Get, $"/api/v1/campaigns/{c}/shops");
        var bought = await Step("shop.buy", _player, HttpMethod.Post, $"{shopUrl}/buy", new { characterId = id, shopItemId = Id(shopItem, "id"), quantity = 1 });
        var boughtItem = FindItem(bought?["inventory"], template, last: true);
        await Step("inventory.attune", _player, HttpMethod.Patch, $"/api/v1/characters/{id}/inventory/{boughtItem}", new { equipped = true, attuned = true });
        await Step("character.get.after-buy", _player, HttpMethod.Get, $"/api/v1/characters/{id}");
        await Step("shop.sell", _player, HttpMethod.Post, $"{shopUrl}/sell", new { characterId = id, itemId = boughtItem, quantity = 1 });
        await Step("transactions.list", _dm, HttpMethod.Get, $"/api/v1/campaigns/{c}/transactions");
        await Step("shop.item.delete", _dm, HttpMethod.Delete, $"{shopUrl}/items/{Id(shopItem, "id")}");
        await Step("shop.delete", _dm, HttpMethod.Delete, shopUrl);
        await Step("homebrew.delete", _dm, HttpMethod.Delete, $"{items}/{template}");
    }

    private async Task StashAndMessagesAsync(string c, string id)
    {
        var stash = $"/api/v1/campaigns/{c}/stash";
        await Step("stash.get", _player, HttpMethod.Get, stash);
        var added = await Step("stash.item.add", _dm, HttpMethod.Post, $"{stash}/items", new { templateId = _ids["longsword"], quantity = 2, notes = "Del dragón" });
        var stashItem = Id(added, "id");
        await Step("stash.item.patch", _dm, HttpMethod.Patch, $"{stash}/items/{stashItem}", new { quantity = 3 });
        await Step("stash.item.take", _player, HttpMethod.Post, $"{stash}/items/{stashItem}/take", new { characterId = id, quantity = 1 });
        var inventory = await Step("inventory.get.after-stash", _player, HttpMethod.Get, $"/api/v1/characters/{id}/inventory");
        var characterItem = FindItem(inventory, _ids["longsword"], last: true);
        await Step("stash.item.return", _player, HttpMethod.Post, $"{stash}/items/return", new { characterId = id, characterItemId = characterItem, quantity = 1 });
        await Step("stash.gold", _dm, HttpMethod.Post, $"{stash}/gold", new { deltaCp = 1000 });
        await Step("stash.gold.split", _dm, HttpMethod.Post, $"{stash}/gold/split");
        await Step("stash.item.delete", _dm, HttpMethod.Delete, $"{stash}/items/{stashItem}");

        var messages = $"/api/v1/campaigns/{c}/messages";
        await Step("message.send", _dm, HttpMethod.Post, messages, new { characterIds = new[] { id }, body = "Mensaje secreto" });
        var inbox = await Step("message.list", _player, HttpMethod.Get, messages);
        await Step("message.unread", _player, HttpMethod.Get, $"{messages}/unread-count");
        var first = inbox is JsonArray list ? list.FirstOrDefault() : inbox?["items"]?.AsArray().FirstOrDefault();
        await Step("message.read", _player, HttpMethod.Post, $"/api/v1/messages/{Id(first, "id")}/read");
    }

    private async Task OwnershipAsync(string c, string id)
    {
        var url = $"/api/v1/characters/{id}";
        await Step("character.portrait.clear", _player, HttpMethod.Patch, $"{url}/portrait", new { fileId = (Guid?)null });
        await Step("character.owner.npc", _dm, HttpMethod.Put, $"{url}/owner", new { ownerUserId = (Guid?)null });
        await Step("character.owner.player", _dm, HttpMethod.Put, $"{url}/owner", new { ownerUserId = _player.Id });
        await Step("character.list.final", _dm, HttpMethod.Get, $"/api/v1/campaigns/{c}/characters");
        await Step("character.get.final", _dm, HttpMethod.Get, url);

        var npc = await Step("character.create.npc", _dm, HttpMethod.Post, $"/api/v1/campaigns/{c}/characters", new JsonObject { ["name"] = "PNJ", ["ownerUserId"] = null });
        var npcId = Id(npc, "id");
        Remember(Guid.Parse(npcId), "npc");
        await Step("character.activate.npc", _dm, HttpMethod.Post, $"/api/v1/characters/{npcId}/activate");
        await Step("character.get.npc", _dm, HttpMethod.Get, $"/api/v1/characters/{npcId}");
        await Step("character.delete.npc", _dm, HttpMethod.Delete, $"/api/v1/characters/{npcId}");
        await Step("character.get.npc.deleted", _dm, HttpMethod.Get, $"/api/v1/characters/{npcId}");
    }

    private async Task AddMemberAsync(string c, SignedInUser user, string role)
    {
        var invitation = await Step($"campaign.invite.{role}", _owner, HttpMethod.Post, $"/api/v1/campaigns/{c}/members", new { userId = user.Id, role });
        await Step($"campaign.accept.{role}", user, HttpMethod.Post, $"/api/v1/invitations/{Id(invitation, "id")}/accept");
    }

    /// <summary>The D&amp;D 5e route of a core character route (phase 32B: <c>/api/v1/systems/dnd5e/...</c>).</summary>
    private static string Dnd5e(string coreUrl) => coreUrl.Replace("/api/v1/", "/api/v1/systems/dnd5e/", StringComparison.Ordinal);

    private static string FindItem(JsonNode? inventory, string templateId, bool last)
    {
        var matches = inventory?["items"]?.AsArray()
            .Where(i => i?["templateId"]?.GetValue<string>() == templateId)
            .ToList() ?? [];
        var item = last ? matches.LastOrDefault() : matches.FirstOrDefault();
        return item?["id"]?.GetValue<string>() ?? Guid.Empty.ToString();
    }

    /// <summary>Answers every required choice with its first eligible options (in the plan's order).</summary>
    private static JsonArray AutoAnswers(JsonArray? choices)
    {
        var answers = new JsonArray();
        foreach (var choice in choices ?? [])
        {
            var required = choice?["required"]?.GetValue<int>() ?? 0;
            var kind = choice?["kind"]?.GetValue<string>();
            if (choice is null || required <= 0 || choice["freeText"]?.GetValue<bool>() == true || kind == "Feat")
            {
                continue;
            }

            var picks = (choice["options"]?.AsArray() ?? [])
                .Where(o => o?["eligible"]?.GetValue<bool>() != false && o?["requires"] is null)
                .Take(required)
                .Select(o => (JsonNode?)JsonValue.Create(o!["index"]!.GetValue<string>()))
                .ToArray();
            answers.Add(new JsonObject { ["key"] = choice["key"]!.GetValue<string>(), ["selected"] = new JsonArray(picks) });
        }

        return answers;
    }

    private static string Id(JsonNode? node, string property) =>
        node is JsonObject obj && obj[property] is JsonValue value && value.TryGetValue<string>(out var id)
            ? id
            : Guid.Empty.ToString();

    private void Remember(Guid id, string name) => _names[id] = name;

    private async Task<JsonNode?> Step(string name, SignedInUser actor, HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = body is JsonNode node
                ? new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json")
                : JsonContent.Create(body);
        }

        using var response = await actor.Client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        JsonNode? parsed = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                parsed = JsonNode.Parse(text);
            }
            catch (JsonException)
            {
                parsed = JsonValue.Create(text);
            }
        }

        _steps.Add(new JsonObject
        {
            ["step"] = name,
            ["request"] = $"{method.Method} {Normalize(url)}",
            ["status"] = (int)response.StatusCode,
            ["body"] = NormalizeNode(parsed?.DeepClone()),
        });
        return parsed;
    }

    private JsonNode? NormalizeNode(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                obj.Remove("traceId");
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    obj[key] = NormalizeNode(obj[key]?.DeepClone());
                }

                return obj;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    array[i] = NormalizeNode(array[i]?.DeepClone());
                }

                return array;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                return JsonValue.Create(Normalize(value.GetValue<string>()));
            default:
                return node;
        }
    }

    private string Normalize(string text)
    {
        text = GuidPattern().Replace(text, m => $"<{NameOf(Guid.Parse(m.Value))}>");
        text = HexPattern().Replace(text, "<hex>");
        return TimestampPattern().Replace(text, "<time>");
    }

    private string NameOf(Guid id)
    {
        if (id == Guid.Empty)
        {
            return "empty";
        }

        if (!_names.TryGetValue(id, out var name))
        {
            name = $"id{_names.Count}";
            _names[id] = name;
        }

        return name;
    }

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidPattern();

    [GeneratedRegex("(?<![0-9a-zA-Z])[0-9a-f]{32}(?![0-9a-zA-Z])")]
    private static partial Regex HexPattern();

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?")]
    private static partial Regex TimestampPattern();
}

/// <summary>SRD catalog seeded and fixed dice (every die rolls half its sides plus one), so the run is repeatable.</summary>
public sealed class RegressionApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDiceRoller>();
            services.AddSingleton<IDiceRoller>(new FixedDiceRoller());
        });
    }

    private sealed class FixedDiceRoller : IDiceRoller
    {
        public int Roll(int sides) => sides / 2 + 1;
    }
}
