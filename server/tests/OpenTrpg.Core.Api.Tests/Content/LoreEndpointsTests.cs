using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Lore;
using static OpenTrpg.Core.Api.Tests.Content.ContentTestHelpers;

namespace OpenTrpg.Core.Api.Tests.Content;

public sealed class LoreEndpointsTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    [Fact]
    public async Task Slug_comes_from_the_title_and_gets_a_numeric_suffix_when_taken()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var first = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "La Ciudad de Phandalin");
        var second = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "la ciudad de phandalin!");
        var third = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "  La   Ciudad de Phandalin ");
        var accents = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Árbol del Ñandú");
        var symbols = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "???");

        Assert.Equal("la-ciudad-de-phandalin", first.Slug);
        Assert.Equal("la-ciudad-de-phandalin-2", second.Slug);
        Assert.Equal("la-ciudad-de-phandalin-3", third.Slug);
        Assert.Equal("arbol-del-nandu", accents.Slug);
        Assert.Equal("entrada", symbols.Slug);

        // Slugs are unique per campaign, not globally.
        var other = await factory.CreateCampaignScenarioAsync();
        Assert.Equal("la-ciudad-de-phandalin", (await other.Dm.CreateLoreAsync(other.CampaignId, "La Ciudad de Phandalin")).Slug);
    }

    [Fact]
    public async Task Create_returns_the_entry_and_validates_the_body()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var url = $"{scenario.Url}/lore";

        var created = await scenario.Dm.Client.PostAsJsonAsync(url, new { title = "Mundo", category = "World", contentMarkdown = "# Hola", visibility = "DmOnly" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var entry = (await created.Content.ReadFromJsonAsync<LoreEntryDto>())!;
        Assert.Equal("World", entry.Category);
        Assert.Equal("DmOnly", entry.Visibility);
        Assert.Equal("# Hola", entry.ContentMarkdown);
        Assert.Equal(scenario.Dm.Id, entry.CreatedByUserId);
        Assert.Empty(entry.Attachments);
        Assert.Equal($"/api/v1/lore/{entry.Id}", created.Headers.Location?.OriginalString);

        foreach (var body in new object[]
                 {
                     new { title = "", category = "World", contentMarkdown = "", visibility = "Players" },
                     new { title = "T", category = "Dragon", contentMarkdown = "", visibility = "Players" },
                     new { title = "T", category = "World", contentMarkdown = "", visibility = "Everyone" },
                     new { title = new string('x', 201), category = "World", contentMarkdown = "", visibility = "Players" },
                     new { title = "T", category = "World", contentMarkdown = "", visibility = "Players", parentId = Guid.NewGuid() },
                     new { title = "T", category = "World", contentMarkdown = "", visibility = "Players", coverFileId = Guid.NewGuid() },
                 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PostAsJsonAsync(url, body)).StatusCode);
        }
    }

    [Fact]
    public async Task Players_only_see_player_entries_and_get_404_for_dm_only_ones()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var open = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Abierta");
        var secret = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Secreta", visibility: "DmOnly");

        var playerList = await scenario.Player.Client.GetFromJsonAsync<List<LoreSummaryDto>>($"{scenario.Url}/lore");
        var dmList = await scenario.Dm.Client.GetFromJsonAsync<List<LoreSummaryDto>>($"{scenario.Url}/lore");

        Assert.Equal([open.Id], playerList!.Select(e => e.Id));
        Assert.Equal(2, dmList!.Count);
        Assert.Equal(HttpStatusCode.OK, (await scenario.Player.Client.GetAsync($"/api/v1/lore/{open.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Player.Client.GetAsync($"/api/v1/lore/{secret.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scenario.Dm.Client.GetAsync($"/api/v1/lore/{secret.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Player.Client.PatchAsJsonAsync($"/api/v1/lore/{secret.Id}", new { title = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Player.Client.DeleteAsync($"/api/v1/lore/{secret.Id}")).StatusCode);
    }

    [Fact]
    public async Task Search_does_not_leak_dm_only_content_to_players()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Pública", content: "nada especial");
        await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Oculta", visibility: "DmOnly", content: "el dragón duerme");

        var player = await scenario.Player.Client.GetFromJsonAsync<List<LoreSummaryDto>>($"{scenario.Url}/lore?search=DRAGÓN");
        var dm = await scenario.Dm.Client.GetFromJsonAsync<List<LoreSummaryDto>>($"{scenario.Url}/lore?search=DRAGÓN");

        Assert.Empty(player!);
        Assert.Equal(["Oculta"], dm!.Select(e => e.Title));
    }

    [Fact]
    public async Task Only_dms_manage_lore_and_outsiders_do_not_see_the_campaign()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var entry = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Entrada");
        var body = new { title = "Otra", category = "Note", contentMarkdown = "", visibility = "Players" };

        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PostAsJsonAsync($"{scenario.Url}/lore", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PatchAsJsonAsync($"/api/v1/lore/{entry.Id}", new { title = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.DeleteAsync($"/api/v1/lore/{entry.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.GetAsync($"{scenario.Url}/lore")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.GetAsync($"/api/v1/lore/{entry.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.PostAsJsonAsync($"{scenario.Url}/lore", body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scenario.Owner.Client.PatchAsJsonAsync($"/api/v1/lore/{entry.Id}", new { sortOrder = 3 })).StatusCode);
    }

    [Fact]
    public async Task Filters_by_category_search_and_parent()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var region = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Costa de la Espada", category: "Region");
        var town = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Neverwinter", category: "Place", parentId: region.Id);
        await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Gundren", category: "Npc", parentId: town.Id, content: "Busca la mina");

        async Task<string[]> TitlesAsync(string query) =>
            (await scenario.Player.Client.GetFromJsonAsync<List<LoreSummaryDto>>($"{scenario.Url}/lore{query}"))!.Select(e => e.Title).ToArray();

        Assert.Equal(3, (await TitlesAsync(string.Empty)).Length);
        Assert.Equal(["Neverwinter"], await TitlesAsync("?category=Place"));
        Assert.Equal(["Gundren"], await TitlesAsync("?search=mina"));
        Assert.Equal(["Neverwinter"], await TitlesAsync($"?parentId={region.Id}"));
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Player.Client.GetAsync($"{scenario.Url}/lore?category=Dragon")).StatusCode);
    }

    [Fact]
    public async Task Deleting_an_entry_moves_its_children_to_the_root()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var parent = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Padre");
        var child = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Hija", parentId: parent.Id);
        var grandChild = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Nieta", parentId: child.Id);
        Assert.Equal(parent.Id, child.ParentId);

        var delete = await scenario.Dm.Client.DeleteAsync($"/api/v1/lore/{parent.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Dm.Client.GetAsync($"/api/v1/lore/{parent.Id}")).StatusCode);
        var movedChild = await scenario.Dm.Client.GetFromJsonAsync<LoreEntryDto>($"/api/v1/lore/{child.Id}");
        var untouched = await scenario.Dm.Client.GetFromJsonAsync<LoreEntryDto>($"/api/v1/lore/{grandChild.Id}");
        Assert.Null(movedChild!.ParentId);
        Assert.Equal(child.Id, untouched!.ParentId);
    }

    [Fact]
    public async Task Update_changes_fields_keeps_the_slug_and_moves_entries_without_cycles()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var parent = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Padre");
        var child = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Hija", parentId: parent.Id);
        var url = $"/api/v1/lore/{child.Id}";

        var patch = await scenario.Dm.Client.PatchAsJsonAsync(url, new { title = "Nuevo título", category = "Quest", contentMarkdown = "otro", visibility = "DmOnly", sortOrder = 7 });

        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var updated = (await patch.Content.ReadFromJsonAsync<LoreEntryDto>())!;
        Assert.Equal("Nuevo título", updated.Title);
        Assert.Equal("hija", updated.Slug);
        Assert.Equal("Quest", updated.Category);
        Assert.Equal("DmOnly", updated.Visibility);
        Assert.Equal(7, updated.SortOrder);
        Assert.Equal(parent.Id, updated.ParentId);

        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PatchAsJsonAsync($"/api/v1/lore/{parent.Id}", new { parentId = child.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PatchAsJsonAsync(url, new { parentId = child.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PatchAsJsonAsync(url, new { parentId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PatchAsJsonAsync(url, new { category = "Dragon" })).StatusCode);

        var root = await scenario.Dm.Client.PatchAsJsonAsync(url, new { parentId = (Guid?)null });
        Assert.Null((await root.Content.ReadFromJsonAsync<LoreEntryDto>())!.ParentId);
    }

    [Fact]
    public async Task Attachments_reference_campaign_files_and_are_deleted_with_their_entry()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var entry = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Con adjuntos");
        var image = await scenario.Dm.UploadOkAsync(Png(), "LoreAttachment", scenario.CampaignId, fileName: "retrato.png");
        var pdf = await scenario.Dm.UploadOkAsync(Pdf(), "LoreAttachment", scenario.CampaignId, fileName: "nota.pdf");
        var url = $"/api/v1/lore/{entry.Id}/attachments";

        var added = await scenario.Dm.Client.PostAsJsonAsync(url, new { fileId = image.Id, caption = "  Su cara  " });
        await scenario.Dm.Client.PostAsJsonAsync(url, new { fileId = pdf.Id });

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var attachment = (await added.Content.ReadFromJsonAsync<LoreAttachmentDto>())!;
        Assert.Equal("Su cara", attachment.Caption);
        Assert.Equal(image.Url, attachment.Url);
        Assert.Equal("retrato.png", attachment.FileName);

        var read = await scenario.Player.Client.GetFromJsonAsync<LoreEntryDto>($"/api/v1/lore/{entry.Id}");
        Assert.Equal(["retrato.png", "nota.pdf"], read!.Attachments.Select(a => a.FileName));

        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Dm.Client.DeleteAsync($"{url}/{attachment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Dm.Client.DeleteAsync($"{url}/{attachment.Id}")).StatusCode);
        Assert.False(await scenario.Dm.FileExistsAsync(image.Id));
        Assert.True(await scenario.Dm.FileExistsAsync(pdf.Id));

        await scenario.Dm.Client.DeleteAsync($"/api/v1/lore/{entry.Id}");
        Assert.False(await scenario.Dm.FileExistsAsync(pdf.Id));
    }

    [Fact]
    public async Task Attachment_files_must_be_lore_attachments_of_the_same_campaign_and_players_cannot_add_them()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var foreign = await factory.CreateCampaignScenarioAsync();
        var entry = await scenario.Dm.CreateLoreAsync(scenario.CampaignId, "Entrada");
        var url = $"/api/v1/lore/{entry.Id}/attachments";
        var own = await scenario.Dm.UploadOkAsync(Png(), "LoreAttachment", scenario.CampaignId);
        var map = await scenario.Dm.UploadOkAsync(Png(), "MapImage", scenario.CampaignId);
        var foreignFile = await foreign.Dm.UploadOkAsync(Png(), "LoreAttachment", foreign.CampaignId);

        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PostAsJsonAsync(url, new { fileId = foreignFile.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PostAsJsonAsync(url, new { fileId = map.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PostAsJsonAsync(url, new { fileId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PostAsJsonAsync(url, new { fileId = own.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.PostAsJsonAsync(url, new { fileId = own.Id })).StatusCode);
    }

    [Fact]
    public async Task Cover_must_be_an_image_of_the_campaign_and_is_released_when_replaced()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var cover = await scenario.Dm.UploadOkAsync(Png(), "LoreAttachment", scenario.CampaignId);
        var pdf = await scenario.Dm.UploadOkAsync(Pdf(), "LoreAttachment", scenario.CampaignId);

        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PostAsJsonAsync(
            $"{scenario.Url}/lore", new { title = "T", category = "Place", contentMarkdown = "", visibility = "Players", coverFileId = pdf.Id })).StatusCode);

        var created = await scenario.Dm.Client.PostAsJsonAsync(
            $"{scenario.Url}/lore", new { title = "Con portada", category = "Place", contentMarkdown = "", visibility = "Players", coverFileId = cover.Id });
        var entry = (await created.Content.ReadFromJsonAsync<LoreEntryDto>())!;
        Assert.Equal(cover.Url, entry.CoverUrl);
        Assert.Equal(cover.Url, (await scenario.Player.Client.GetFromJsonAsync<List<LoreSummaryDto>>($"{scenario.Url}/lore"))!.Single().CoverUrl);

        var removed = await scenario.Dm.Client.PatchAsJsonAsync($"/api/v1/lore/{entry.Id}", new { coverFileId = (Guid?)null });
        Assert.Null((await removed.Content.ReadFromJsonAsync<LoreEntryDto>())!.CoverFileId);
        Assert.False(await scenario.Dm.FileExistsAsync(cover.Id));
    }
}
