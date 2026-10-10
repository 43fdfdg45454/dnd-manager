using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;
using OpenTrpg.Core.Domain.Campaigns;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests;

/// <summary>
/// Phase 32B: the D&amp;D 5e routes of phase 30 §4.6 live under <c>/api/v1/systems/dnd5e</c>. Every new route
/// answers, every old route is gone (404, no aliases) and the character and campaign routes answer 404 when the
/// campaign belongs to another game system.
/// </summary>
[Collection(CatalogCollection.Name)]
public class SystemRoutesTests(CatalogApiFactory factory)
{
    private const string NewPrefix = "/api/v1/systems/dnd5e";
    private const string OldPrefix = "/api/v1";

    /// <summary>One row of the table: method, path after the prefix and body.</summary>
    private sealed record Route(HttpMethod Method, string Path, object? Body = null)
    {
        public override string ToString() => $"{Method} {Path}";
    }

    [Fact]
    public async Task Every_route_of_the_table_moved_under_the_system_prefix()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var (characterId, resourceId) = await CharacterWithResourceAsync(s);
        var itemId = await s.Dm.SrdItemIdAsync("Longsword");

        var routes = CatalogRoutes(itemId)
            .Concat(CharacterRoutes(characterId, resourceId))
            .Concat(PartyRoutes(s.CampaignId, characterId));

        foreach (var route in routes)
        {
            var fresh = await SendAsync(s.Dm, route, NewPrefix);
            Assert.True(fresh.Routed, $"{route} answered {(int)fresh.Status} without a handler under {NewPrefix}.");

            var old = await SendAsync(s.Dm, route, OldPrefix);
            Assert.True(old.Status == HttpStatusCode.NotFound && !old.Routed, $"{route} still answers {(int)old.Status} under {OldPrefix}.");
        }
    }

    [Fact]
    public async Task Character_and_campaign_routes_answer_404_for_a_campaign_of_another_system()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var (characterId, resourceId) = await CharacterWithResourceAsync(s);

        // Control: before the change the routes answer.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(s.Dm, new Route(HttpMethod.Get, $"/campaigns/{s.CampaignId}/party"), NewPrefix)).Status);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(s.Dm, new Route(HttpMethod.Get, $"/characters/{characterId}/origin-choices"), NewPrefix)).Status);

        await factory.WithDbAsync(async db =>
        {
            var campaign = await db.Campaigns.FindAsync(s.CampaignId);
            db.Entry(campaign!).Property(nameof(Campaign.SystemId)).CurrentValue = "other-system";
            await db.SaveChangesAsync();
        });

        foreach (var route in CharacterRoutes(characterId, resourceId).Concat(PartyRoutes(s.CampaignId, characterId)))
        {
            foreach (var actor in new[] { s.Dm, s.Player })
            {
                var (status, _) = await SendAsync(actor, route, NewPrefix);
                Assert.True(status == HttpStatusCode.NotFound, $"{route} answered {(int)status} for a campaign of another system.");
            }
        }
    }

    private static IEnumerable<Route> CatalogRoutes(Guid itemId) =>
    [
        new(HttpMethod.Get, "/catalog/attribution"),
        new(HttpMethod.Get, "/catalog/classes"),
        new(HttpMethod.Get, "/catalog/classes/fighter"),
        new(HttpMethod.Get, "/catalog/races"),
        new(HttpMethod.Get, "/catalog/races/dwarf"),
        new(HttpMethod.Get, "/catalog/spells?search=fire"),
        new(HttpMethod.Get, "/catalog/spells/fire-bolt"),
        new(HttpMethod.Get, "/catalog/beasts"),
        new(HttpMethod.Get, "/catalog/beasts/wolf"),
        new(HttpMethod.Get, "/catalog/items?search=Longsword"),
        new(HttpMethod.Get, $"/catalog/items/{itemId}"),
        new(HttpMethod.Get, "/catalog/trinkets"),
        new(HttpMethod.Get, "/catalog/roll-tables"),
        new(HttpMethod.Get, "/catalog/conditions"),
        new(HttpMethod.Get, "/catalog/skills"),
        new(HttpMethod.Get, "/catalog/backgrounds"),
        new(HttpMethod.Get, "/catalog/equipment-categories/arcane-foci"),
        new(HttpMethod.Get, "/catalog/features/second-wind"),
        new(HttpMethod.Get, "/catalog/sources"),
    ];

    private static IEnumerable<Route> CharacterRoutes(Guid id, Guid resourceId)
    {
        var c = $"/characters/{id}";
        return
        [
            new(HttpMethod.Patch, $"{c}/sheet", new { notes = "Notas" }),
            new(HttpMethod.Get, $"{c}/origin-choices"),
            new(HttpMethod.Put, $"{c}/origin-choices", new { choices = Array.Empty<object>() }),
            new(HttpMethod.Patch, $"{c}/combat", new { inspiration = true }),
            new(HttpMethod.Post, $"{c}/damage", new { amount = 1 }),
            new(HttpMethod.Put, $"{c}/companion", new { beastIndex = "wolf", name = "Lobo" }),
            new(HttpMethod.Post, $"{c}/companion/hp", new { delta = -1 }),
            new(HttpMethod.Delete, $"{c}/companion"),
            new(HttpMethod.Post, $"{c}/concentration", new { spellIndex = (string?)null }),
            new(HttpMethod.Post, $"{c}/spell-slots/1/spend", new { amount = 1 }),
            new(HttpMethod.Post, $"{c}/spell-slots/1/restore", new { amount = 1 }),
            new(HttpMethod.Post, $"{c}/resources", new { name = "Suerte", max = 3, recharge = "LongRest" }),
            new(HttpMethod.Post, $"{c}/resources/{resourceId}/spend", new { amount = 1 }),
            new(HttpMethod.Post, $"{c}/resources/{resourceId}/restore", new { amount = 1 }),
            new(HttpMethod.Post, $"{c}/resources/{resourceId}/rolls", new { values = new[] { 1 } }),
            new(HttpMethod.Post, $"{c}/rest/short", new { hitDice = new Dictionary<string, int>() }),
            new(HttpMethod.Post, $"{c}/rest/long"),
            new(HttpMethod.Get, $"{c}/spell-preparation"),
            new(HttpMethod.Post, $"{c}/spell-preparation", new { classes = Array.Empty<object>() }),
            new(HttpMethod.Post, $"{c}/spell-preparation/keep"),
            new(HttpMethod.Get, $"{c}/invalid-choices"),
            new(HttpMethod.Post, $"{c}/invalid-choices", new { choices = Array.Empty<object>() }),
            new(HttpMethod.Post, $"{c}/class-actions/rage"),
            new(HttpMethod.Get, $"{c}/level-up"),
            new(HttpMethod.Post, $"{c}/level-up", new { classIndex = "fighter", hitPointsRolled = 5, choices = Array.Empty<object>() }),
            new(HttpMethod.Delete, $"{c}/resources/{resourceId}"),
        ];
    }

    private static IEnumerable<Route> PartyRoutes(Guid campaignId, Guid characterId)
    {
        var p = $"/campaigns/{campaignId}/party";
        return
        [
            new(HttpMethod.Get, p),
            new(HttpMethod.Post, $"{p}/rest", new { kind = "long" }),
            new(HttpMethod.Post, $"{p}/adjust", new[] { new { characterId, hitPointsDelta = -1 } }),
            new(HttpMethod.Post, $"{p}/grant-level", new { characterIds = new[] { characterId } }),
            new(HttpMethod.Delete, $"{p}/grant-level", new { characterIds = new[] { characterId } }),
        ];
    }

    /// <summary>An active fighter of the player with one custom resource, created through the new routes.</summary>
    private static async Task<(Guid CharacterId, Guid ResourceId)> CharacterWithResourceAsync(CampaignScenario s)
    {
        var character = await ItemTestHelpers.ActiveFighterAsync(s.Player, s.Dm, s.CampaignId, "Ruta");
        var created = await s.Dm.Client.PostAsJsonAsync(
            $"{ItemTestHelpers.Dnd5eCharacterUrl(character.Id)}/resources", new { name = "Varita", max = 3, recharge = "LongRest" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var resource = await created.Content.ReadFromJsonAsync<JsonElement>();
        return (character.Id, resource.GetProperty("id").GetGuid());
    }

    /// <summary>
    /// Sends the request. <c>Routed</c> is false only for the 404 of a path without endpoint (a ProblemDetails
    /// without detail) or a 405; a handler's own 404 (for example, a character without companion) carries a detail.
    /// </summary>
    private static async Task<(HttpStatusCode Status, bool Routed)> SendAsync(SignedInUser actor, Route route, string prefix)
    {
        using var request = new HttpRequestMessage(route.Method, prefix + route.Path);
        if (route.Body is not null)
        {
            request.Content = JsonContent.Create(route.Body, route.Body.GetType());
        }

        using var response = await actor.Client.SendAsync(request);
        var status = response.StatusCode;
        if (status == HttpStatusCode.MethodNotAllowed)
        {
            return (status, false);
        }

        if (status != HttpStatusCode.NotFound)
        {
            return (status, true);
        }

        var body = await response.Content.ReadAsStringAsync();
        var hasDetail = body.Length > 0
            && JsonDocument.Parse(body).RootElement is { ValueKind: JsonValueKind.Object } problem
            && problem.TryGetProperty("detail", out var detail)
            && detail.ValueKind == JsonValueKind.String;
        return (status, hasDetail);
    }
}
