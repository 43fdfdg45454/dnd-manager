using System.Net;
using System.Net.Http.Json;
using Dnd.Application.Maps;
using static Dnd.Api.Tests.Content.ContentTestHelpers;

namespace Dnd.Api.Tests.Content;

public sealed class MapEndpointsTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    [Fact]
    public async Task Create_reads_the_size_of_the_image_and_returns_the_map()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var file = await scenario.Dm.UploadOkAsync(Jpeg(1200, 900), "MapImage", scenario.CampaignId);

        var response = await scenario.Dm.Client.PostAsJsonAsync($"{scenario.Url}/maps", new { name = "  Costa  ", fileId = file.Id, visibility = "Players" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var map = (await response.Content.ReadFromJsonAsync<MapDto>())!;
        Assert.Equal("Costa", map.Name);
        Assert.Equal(1200, map.WidthPx);
        Assert.Equal(900, map.HeightPx);
        Assert.Equal(file.Url, map.Url);
        Assert.Equal(file.Id, map.FileId);
        Assert.Empty(map.Pins);
        Assert.Equal($"/api/v1/maps/{map.Id}", response.Headers.Location?.OriginalString);

        var webp = await scenario.Dm.UploadOkAsync(WebpLossless(320, 200), "MapImage", scenario.CampaignId);
        var second = await scenario.Dm.Client.PostAsJsonAsync($"{scenario.Url}/maps", new { name = "Webp", fileId = webp.Id, visibility = "DmOnly" });
        var webpMap = (await second.Content.ReadFromJsonAsync<MapDto>())!;
        Assert.Equal((320, 200), (webpMap.WidthPx, webpMap.HeightPx));
        Assert.True(webpMap.SortOrder > map.SortOrder);
    }

    [Fact]
    public async Task Players_cannot_upload_a_map_nor_create_one()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var file = await scenario.Dm.UploadOkAsync(Png(), "MapImage", scenario.CampaignId);

        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.UploadAsync(Png(), "MapImage", scenario.CampaignId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PostAsJsonAsync(
            $"{scenario.Url}/maps", new { name = "Mapa", fileId = file.Id, visibility = "Players" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.PostAsJsonAsync(
            $"{scenario.Url}/maps", new { name = "Mapa", fileId = file.Id, visibility = "Players" })).StatusCode);
    }

    [Fact]
    public async Task Map_must_be_an_image_of_the_same_campaign()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var foreign = await factory.CreateCampaignScenarioAsync();
        var pdf = await scenario.Dm.UploadOkAsync(Pdf(), "LoreAttachment", scenario.CampaignId);
        var attachmentImage = await scenario.Dm.UploadOkAsync(Png(), "LoreAttachment", scenario.CampaignId);
        var foreignMap = await foreign.Dm.UploadOkAsync(Png(), "MapImage", foreign.CampaignId);
        var admin = await factory.CreateAdminClientAsync();
        var library = await admin.UploadOkAsync(Pdf(), "LibraryDocument");

        foreach (var fileId in new[] { pdf.Id, attachmentImage.Id, foreignMap.Id, library.Id, Guid.NewGuid() })
        {
            var response = await scenario.Dm.Client.PostAsJsonAsync($"{scenario.Url}/maps", new { name = "Mapa", fileId, visibility = "Players" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True((await response.ReadProblemAsync()).HasFieldError("fileId"));
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PostAsJsonAsync(
            $"{scenario.Url}/maps", new { name = "", fileId = foreignMap.Id, visibility = "Players" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PostAsJsonAsync(
            $"{scenario.Url}/maps", new { name = "Mapa", fileId = pdf.Id, visibility = "Nobody" })).StatusCode);
    }

    [Fact]
    public async Task Players_do_not_receive_dm_only_pins_or_maps()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var visible = await scenario.Dm.CreateMapAsync(scenario.CampaignId, "Visible");
        var hidden = await scenario.Dm.CreateMapAsync(scenario.CampaignId, "Oculto", "DmOnly");
        await scenario.Dm.CreatePinAsync(visible.Id, "Público");
        await scenario.Dm.CreatePinAsync(visible.Id, "Secreto", "DmOnly");

        var playerMap = await scenario.Player.Client.GetFromJsonAsync<MapDto>($"/api/v1/maps/{visible.Id}");
        var dmMap = await scenario.Dm.Client.GetFromJsonAsync<MapDto>($"/api/v1/maps/{visible.Id}");
        var playerList = await scenario.Player.Client.GetFromJsonAsync<List<MapSummaryDto>>($"{scenario.Url}/maps");
        var dmList = await scenario.Dm.Client.GetFromJsonAsync<List<MapSummaryDto>>($"{scenario.Url}/maps");

        Assert.Equal(["Público"], playerMap!.Pins.Select(p => p.Title));
        Assert.Equal(["Público", "Secreto"], dmMap!.Pins.Select(p => p.Title));
        Assert.Equal(["Visible"], playerList!.Select(m => m.Name));
        Assert.Equal(["Visible", "Oculto"], dmList!.Select(m => m.Name));
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Player.Client.GetAsync($"/api/v1/maps/{hidden.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Player.Client.PostAsJsonAsync(
            $"/api/v1/maps/{hidden.Id}/pins", new { x = 0.5, y = 0.5, title = "x", icon = "place", visibility = "Players" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.GetAsync($"/api/v1/maps/{visible.Id}")).StatusCode);
    }

    [Fact]
    public async Task Only_dms_edit_maps_and_pins()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var map = await scenario.Dm.CreateMapAsync(scenario.CampaignId);
        var pin = await scenario.Dm.CreatePinAsync(map.Id);
        var pinBody = new { x = 0.5, y = 0.5, title = "x", icon = "place", visibility = "Players" };

        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PostAsJsonAsync($"/api/v1/maps/{map.Id}/pins", pinBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PatchAsJsonAsync($"/api/v1/maps/{map.Id}/pins/{pin.Id}", new { x = 0.1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.DeleteAsync($"/api/v1/maps/{map.Id}/pins/{pin.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PatchAsJsonAsync($"/api/v1/maps/{map.Id}", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.DeleteAsync($"/api/v1/maps/{map.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scenario.Owner.Client.PatchAsJsonAsync($"/api/v1/maps/{map.Id}/pins/{pin.Id}", new { x = 0.1 })).StatusCode);
    }

    [Fact]
    public async Task Pins_are_validated_moved_edited_and_deleted()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var map = await scenario.Dm.CreateMapAsync(scenario.CampaignId);
        var pinsUrl = $"/api/v1/maps/{map.Id}/pins";

        foreach (var body in new object[]
                 {
                     new { x = 1.5, y = 0.5, title = "x", icon = "place", visibility = "Players" },
                     new { x = 0.5, y = -0.1, title = "x", icon = "place", visibility = "Players" },
                     new { y = 0.5, title = "x", icon = "place", visibility = "Players" },
                     new { x = 0.5, y = 0.5, title = "", icon = "place", visibility = "Players" },
                     new { x = 0.5, y = 0.5, title = "x", icon = "castle", visibility = "Players" },
                     new { x = 0.5, y = 0.5, title = "x", icon = "place", color = "red", visibility = "Players" },
                     new { x = 0.5, y = 0.5, title = "x", icon = "place", visibility = "Nobody" },
                     new { x = 0.5, y = 0.5, title = "x", icon = "place", visibility = "Players", loreEntryId = Guid.NewGuid() },
                 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PostAsJsonAsync(pinsUrl, body)).StatusCode);
        }

        var edges = await scenario.Dm.Client.PostAsJsonAsync(pinsUrl, new { x = 0, y = 1, title = "Borde", icon = "custom", visibility = "DmOnly" });
        Assert.Equal(HttpStatusCode.Created, edges.StatusCode);
        var pin = await scenario.Dm.CreatePinAsync(map.Id, "Ciudad");
        Assert.Equal("#FF8800", pin.Color);
        Assert.Equal("Nota", pin.Note);

        var moved = await scenario.Dm.Client.PatchAsJsonAsync($"{pinsUrl}/{pin.Id}", new { x = 0.9, y = 0.1, title = "Capital", icon = "city", color = (string?)null, note = "", visibility = "DmOnly" });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var updated = (await moved.Content.ReadFromJsonAsync<MapPinDto>())!;
        Assert.Equal((0.9, 0.1), (updated.X, updated.Y));
        Assert.Equal("Capital", updated.Title);
        Assert.Equal("city", updated.Icon);
        Assert.Null(updated.Color);
        Assert.Null(updated.Note);
        Assert.Equal("DmOnly", updated.Visibility);

        var untouched = await scenario.Dm.Client.PatchAsJsonAsync($"{pinsUrl}/{pin.Id}", new { title = "Solo título" });
        Assert.Equal(0.9, (await untouched.Content.ReadFromJsonAsync<MapPinDto>())!.X);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PatchAsJsonAsync($"{pinsUrl}/{pin.Id}", new { x = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Dm.Client.PatchAsJsonAsync($"{pinsUrl}/{Guid.NewGuid()}", new { x = 0.1 })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Dm.Client.DeleteAsync($"{pinsUrl}/{pin.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Dm.Client.DeleteAsync($"{pinsUrl}/{pin.Id}")).StatusCode);
        var remaining = await scenario.Dm.Client.GetFromJsonAsync<MapDto>($"/api/v1/maps/{map.Id}");
        Assert.Equal(["Borde"], remaining!.Pins.Select(p => p.Title));
    }

    [Fact]
    public async Task Pins_link_to_lore_entries_of_the_campaign_and_are_unlinked_when_the_entry_is_deleted()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var foreign = await factory.CreateCampaignScenarioAsync();
        var map = await scenario.Dm.CreateMapAsync(scenario.CampaignId);
        var lore = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Ciudad");
        var foreignLore = await foreign.Dm.CreateLoreAsync(foreign.CampaignId, "Ajena");

        var pin = await scenario.Dm.CreatePinAsync(map.Id, loreEntryId: lore.Id);
        Assert.Equal(lore.Id, pin.LoreEntryId);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PatchAsJsonAsync($"/api/v1/maps/{map.Id}/pins/{pin.Id}", new { loreEntryId = foreignLore.Id })).StatusCode);

        await scenario.Dm.Client.DeleteAsync($"/api/v1/lore/{lore.Id}");

        var reloaded = await scenario.Dm.Client.GetFromJsonAsync<MapDto>($"/api/v1/maps/{map.Id}");
        Assert.Null(reloaded!.Pins.Single().LoreEntryId);
    }

    [Fact]
    public async Task Update_changes_the_map_and_replacing_the_image_releases_the_old_one()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var map = await scenario.Dm.CreateMapAsync(scenario.CampaignId, width: 100, height: 100);
        var pin = await scenario.Dm.CreatePinAsync(map.Id);
        var replacement = await scenario.Dm.UploadOkAsync(Png(300, 200), "MapImage", scenario.CampaignId);
        var url = $"/api/v1/maps/{map.Id}";

        var patch = await scenario.Dm.Client.PatchAsJsonAsync(url, new { name = "Renombrado", visibility = "DmOnly", sortOrder = 5, fileId = replacement.Id });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = (await patch.Content.ReadFromJsonAsync<MapDto>())!;
        Assert.Equal("Renombrado", updated.Name);
        Assert.Equal("DmOnly", updated.Visibility);
        Assert.Equal(5, updated.SortOrder);
        Assert.Equal((300, 200), (updated.WidthPx, updated.HeightPx));
        Assert.Equal(pin.Id, updated.Pins.Single().Id);
        Assert.False(await scenario.Dm.FileExistsAsync(map.FileId));
        Assert.True(await scenario.Dm.FileExistsAsync(replacement.Id));

        var pdf = await scenario.Dm.UploadOkAsync(Pdf(), "LoreAttachment", scenario.CampaignId);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PatchAsJsonAsync(url, new { fileId = pdf.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PatchAsJsonAsync(url, new { name = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PatchAsJsonAsync(url, new { sortOrder = -1 })).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_map_deletes_its_pins_and_image()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var map = await scenario.Dm.CreateMapAsync(scenario.CampaignId);
        await scenario.Dm.CreatePinAsync(map.Id);

        var delete = await scenario.Dm.Client.DeleteAsync($"/api/v1/maps/{map.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Dm.Client.GetAsync($"/api/v1/maps/{map.Id}")).StatusCode);
        Assert.False(await scenario.Dm.FileExistsAsync(map.FileId));
        Assert.Empty((await scenario.Dm.Client.GetFromJsonAsync<List<MapSummaryDto>>($"{scenario.Url}/maps"))!);
    }
}
