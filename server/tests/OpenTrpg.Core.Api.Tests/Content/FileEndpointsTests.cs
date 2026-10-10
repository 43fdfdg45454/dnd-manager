using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpenTrpg.Core.Api.Tests.Items;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Files;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static OpenTrpg.Core.Api.Tests.Content.ContentTestHelpers;
using OpenTrpg.Systems.Dnd5e.Application.Characters;

namespace OpenTrpg.Core.Api.Tests.Content;

public sealed class FileEndpointsTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    private const int Megabyte = 1024 * 1024;

    [Fact]
    public async Task Dm_uploads_an_image_and_every_member_downloads_it_with_cache_headers()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var bytes = Png(100, 50, padding: 200);

        var response = await scenario.Dm.Client.UploadAsync(bytes, "MapImage", scenario.CampaignId, fileName: "..\\secret/mapa.png", contentType: "image/png");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = (await response.Content.ReadFromJsonAsync<StoredFileDto>())!;
        Assert.Equal("mapa.png", stored.FileName);
        Assert.Equal("image/png", stored.ContentType);
        Assert.Equal(bytes.Length, stored.SizeBytes);
        Assert.Equal($"/api/v1/files/{stored.Id}", stored.Url);
        Assert.Equal(stored.Url, response.Headers.Location?.OriginalString);

        foreach (var member in new[] { scenario.Owner, scenario.Dm, scenario.Player })
        {
            var download = await member.Client.GetAsync(stored.Url);
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
            Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
            Assert.NotNull(download.Headers.ETag);
            Assert.Equal(TimeSpan.FromDays(1), download.Headers.CacheControl?.MaxAge);
            Assert.True(download.Headers.CacheControl?.Private);
        }
    }

    [Fact]
    public async Task Stored_bytes_live_under_year_and_month_folders_and_the_hash_matches()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var bytes = Png(8, 8, padding: 50);

        var stored = await scenario.Dm.UploadOkAsync(bytes, "MapImage", scenario.CampaignId);

        var today = DateTime.UtcNow;
        var folder = Path.Combine(factory.FilesRoot, today.ToString("yyyy"), today.ToString("MM"));
        var path = Directory.GetFiles(folder, $"{stored.Id}.png").Single();
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));

        var download = await scenario.Dm.Client.GetAsync(stored.Url);
        Assert.Equal($"\"{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))}\"", download.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task Download_with_range_answers_206_and_a_matching_etag_answers_304()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var bytes = Png(10, 10, padding: 500);
        var stored = await scenario.Dm.UploadOkAsync(bytes, "MapImage", scenario.CampaignId);

        var request = new HttpRequestMessage(HttpMethod.Get, stored.Url) { Headers = { Range = new RangeHeaderValue(0, 9) } };
        var partial = await scenario.Player.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(10, (await partial.Content.ReadAsByteArrayAsync()).Length);
        Assert.Equal(bytes.Length, partial.Content.Headers.ContentRange?.Length);
        Assert.Equal("bytes", partial.Headers.AcceptRanges.Single());

        var full = await scenario.Player.Client.GetAsync(stored.Url);
        var conditional = new HttpRequestMessage(HttpMethod.Get, stored.Url) { Headers = { IfNoneMatch = { full.Headers.ETag! } } };
        Assert.Equal(HttpStatusCode.NotModified, (await scenario.Player.Client.SendAsync(conditional)).StatusCode);
    }

    [Theory]
    [InlineData("MapImage")]
    [InlineData("Portrait")]
    [InlineData("LoreAttachment")]
    public async Task Upload_rejects_content_that_is_not_an_image_whatever_the_declared_type(string kind)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var character = await scenario.Player.CreateCharacterAsync(scenario.CampaignId);
        var characterId = kind == "Portrait" ? character.Id : (Guid?)null;
        var campaignId = kind == "Portrait" ? (Guid?)null : scenario.CampaignId;

        var text = await scenario.Dm.Client.UploadAsync("hola"u8.ToArray(), kind, campaignId, characterId, "nota.png", "image/png");
        var script = await scenario.Dm.Client.UploadAsync("<script>alert(1)</script>"u8.ToArray(), kind, campaignId, characterId, "x.html", "text/html");

        Assert.Equal(HttpStatusCode.BadRequest, text.StatusCode);
        Assert.True((await text.ReadProblemAsync()).HasFieldError("file"));
        Assert.Equal(HttpStatusCode.BadRequest, script.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_a_type_that_the_kind_does_not_allow()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var admin = await factory.CreateAdminClientAsync();

        var pdfAsMap = await scenario.Dm.Client.UploadAsync(Pdf(), "MapImage", scenario.CampaignId, contentType: "application/pdf");
        var imageAsLibrary = await admin.UploadAsync(Png(), "LibraryDocument", contentType: "image/png");
        var pdfAsRelease = await admin.UploadAsync(Pdf(), "AppRelease", contentType: "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, pdfAsMap.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, imageAsLibrary.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, pdfAsRelease.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_a_corrupt_image_an_empty_file_and_an_unknown_kind()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var corrupt = Png(0, 0);

        var zeroSize = await scenario.Dm.Client.UploadAsync(corrupt, "MapImage", scenario.CampaignId);
        var empty = await scenario.Dm.Client.UploadAsync([], "MapImage", scenario.CampaignId);
        var unknownKind = await scenario.Dm.Client.UploadAsync(Png(), "Avatar", scenario.CampaignId);
        var missingKind = await scenario.Dm.Client.UploadAsync(Png(), string.Empty, scenario.CampaignId);

        Assert.Equal(HttpStatusCode.BadRequest, zeroSize.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownKind.StatusCode);
        Assert.True((await unknownKind.ReadProblemAsync()).HasFieldError("kind"));
        Assert.Equal(HttpStatusCode.BadRequest, missingKind.StatusCode);
    }

    [Fact]
    public async Task Upload_without_a_file_or_with_a_bad_campaign_id_is_rejected()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        using var noFile = new MultipartFormDataContent { { new StringContent("MapImage"), "kind" } };
        using var badCampaign = new MultipartFormDataContent
        {
            { new ByteArrayContent(Png()), "file", "a.png" },
            { new StringContent("MapImage"), "kind" },
            { new StringContent("not-a-guid"), "campaignId" },
        };
        using var json = JsonContent.Create(new { kind = "MapImage" });

        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PostAsync("/api/v1/files", noFile)).StatusCode);
        var bad = await scenario.Dm.Client.PostAsync("/api/v1/files", badCampaign);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.True((await bad.ReadProblemAsync()).HasFieldError("campaignId"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await scenario.Dm.Client.PostAsync("/api/v1/files", json)).StatusCode);

        var withoutCampaign = await scenario.Dm.Client.UploadAsync(Png(), "MapImage");
        Assert.Equal(HttpStatusCode.BadRequest, withoutCampaign.StatusCode);
        Assert.True((await withoutCampaign.ReadProblemAsync()).HasFieldError("campaignId"));
    }

    [Fact]
    public async Task Upload_over_the_limit_is_a_413()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        // Slightly over the limit: the use case refuses it. Far over it: refused before reading the form.
        var over = await scenario.Dm.Client.UploadAsync(Png(padding: Megabyte), "MapImage", scenario.CampaignId);
        var wayOver = await scenario.Dm.Client.UploadAsync(Png(padding: 3 * Megabyte), "MapImage", scenario.CampaignId);
        var atLimit = await scenario.Dm.Client.UploadAsync(Png(padding: Megabyte - 200), "MapImage", scenario.CampaignId);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, over.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, wayOver.StatusCode);
        Assert.Equal("application/problem+json", wayOver.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Created, atLimit.StatusCode);
    }

    [Fact]
    public void Kestrel_and_multipart_limits_follow_the_configured_maximum_upload_size()
    {
        var expected = ContentApiFactory.LimitMegabytes * Megabyte + Api.Endpoints.FileEndpoints.MultipartOverheadBytes;

        Assert.Equal(expected, factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits.MaxRequestBodySize);
        Assert.Equal(expected, factory.Services.GetRequiredService<IOptions<FormOptions>>().Value.MultipartBodyLengthLimit);
    }

    [Fact]
    public async Task Players_and_outsiders_cannot_upload_campaign_files()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        foreach (var kind in new[] { "MapImage", "LoreAttachment" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.UploadAsync(Png(), kind, scenario.CampaignId)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.UploadAsync(Png(), kind, scenario.CampaignId)).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await scenario.Owner.Client.UploadAsync(Png(), kind, scenario.CampaignId)).StatusCode);
        }

        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.UploadAsync(Png(), "MapImage", scenario.CampaignId)).StatusCode);
    }

    [Fact]
    public async Task Only_admins_upload_library_documents_and_app_releases()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var admin = await factory.CreateAdminClientAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Owner.Client.UploadAsync(Pdf(), "LibraryDocument")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Dm.Client.UploadAsync(Apk(), "AppRelease")).StatusCode);

        var pdf = await admin.UploadOkAsync(Pdf(), "LibraryDocument");
        var apk = await admin.UploadOkAsync(Apk(), "AppRelease");
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.Equal("application/vnd.android.package-archive", apk.ContentType);
    }

    [Fact]
    public async Task Global_files_download_for_any_user_and_campaign_files_only_for_members()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var admin = await factory.CreateAdminClientAsync();
        var global = await admin.UploadOkAsync(Pdf(), "LibraryDocument");
        var campaignFile = await scenario.Dm.UploadOkAsync(Png(), "MapImage", scenario.CampaignId);

        Assert.Equal(HttpStatusCode.OK, (await scenario.Outsider.Client.GetAsync(global.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.GetAsync(campaignFile.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.GetAsync($"/api/v1/files/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(global.Url)).StatusCode);
    }

    [Fact]
    public async Task Portrait_upload_is_for_the_owner_of_the_character_or_a_dm()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateSignedInUserAsync();
        await scenario.Owner.AddMemberAsync(scenario.CampaignId, other, CampaignScenario.PlayerRole);
        var character = await scenario.Player.CreateCharacterAsync(scenario.CampaignId);

        Assert.Equal(HttpStatusCode.Created, (await scenario.Player.Client.UploadAsync(Png(), "Portrait", characterId: character.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await scenario.Dm.Client.UploadAsync(Png(), "Portrait", characterId: character.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.UploadAsync(Png(), "Portrait", characterId: character.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.UploadAsync(Png(), "Portrait", characterId: character.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Player.Client.UploadAsync(Png(), "Portrait", characterId: Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Player.Client.UploadAsync(Png(), "Portrait")).StatusCode);
    }

    [Fact]
    public async Task Portrait_shows_up_in_the_character_detail_and_summary_and_a_replaced_one_is_deleted()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var character = await scenario.Player.CreateCharacterAsync(scenario.CampaignId);
        Assert.Null(character.PortraitUrl);
        var first = await scenario.Player.UploadOkAsync(Png(), "Portrait", characterId: character.Id);
        var second = await scenario.Player.UploadOkAsync(Png(64, 64), "Portrait", characterId: character.Id);

        var set = await scenario.Player.Client.PatchAsJsonAsync($"/api/v1/characters/{character.Id}/portrait", new { fileId = first.Id });

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var detail = (await set.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal(first.Id, detail.PortraitFileId);
        Assert.Equal($"/api/v1/files/{first.Id}", detail.PortraitUrl);
        Assert.Equal($"/api/v1/files/{first.Id}", (await scenario.Dm.GetCharacterAsync(character.Id)).PortraitUrl);

        var list = await scenario.Dm.Client.GetFromJsonAsync<List<CharacterSummaryDto>>($"/api/v1/campaigns/{scenario.CampaignId}/characters");
        Assert.Equal($"/api/v1/files/{first.Id}", list!.Single().PortraitUrl);
        Assert.True(await scenario.Player.FileExistsAsync(first.Id));

        var replace = await scenario.Dm.Client.PatchAsJsonAsync($"/api/v1/characters/{character.Id}/portrait", new { fileId = second.Id });
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        Assert.False(await scenario.Player.FileExistsAsync(first.Id));
        Assert.True(await scenario.Player.FileExistsAsync(second.Id));

        var clear = await scenario.Player.Client.PatchAsJsonAsync($"/api/v1/characters/{character.Id}/portrait", new { fileId = (Guid?)null });
        Assert.Null((await clear.Content.ReadFromJsonAsync<CharacterDetailDto>())!.PortraitUrl);
        Assert.False(await scenario.Player.FileExistsAsync(second.Id));
    }

    [Fact]
    public async Task Setting_a_portrait_checks_permissions_and_the_file()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateSignedInUserAsync();
        await scenario.Owner.AddMemberAsync(scenario.CampaignId, other, CampaignScenario.PlayerRole);
        var character = await scenario.Player.CreateCharacterAsync(scenario.CampaignId);
        var portrait = await scenario.Dm.UploadOkAsync(Png(), "Portrait", characterId: character.Id);
        var mapImage = await scenario.Dm.UploadOkAsync(Png(), "MapImage", scenario.CampaignId);
        var otherCampaign = await factory.CreateCampaignScenarioAsync();
        var foreignCharacter = await otherCampaign.Player.CreateCharacterAsync(otherCampaign.CampaignId);
        var foreignPortrait = await otherCampaign.Dm.UploadOkAsync(Png(), "Portrait", characterId: foreignCharacter.Id);
        var url = $"/api/v1/characters/{character.Id}/portrait";

        Assert.Equal(HttpStatusCode.Forbidden, (await other.Client.PatchAsJsonAsync(url, new { fileId = portrait.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.PatchAsJsonAsync(url, new { fileId = portrait.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Player.Client.PatchAsJsonAsync(url, new { fileId = mapImage.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Player.Client.PatchAsJsonAsync(url, new { fileId = foreignPortrait.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Player.Client.PatchAsJsonAsync(url, new { fileId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await scenario.Player.Client.PatchAsJsonAsync(url, new { fileId = portrait.Id })).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_campaign_removes_its_files_from_disk()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var stored = await scenario.Dm.UploadOkAsync(Png(), "MapImage", scenario.CampaignId);
        var today = DateTime.UtcNow;
        var path = Path.Combine(factory.FilesRoot, today.ToString("yyyy"), today.ToString("MM"), $"{stored.Id}.png");
        Assert.True(File.Exists(path));

        var delete = await scenario.Owner.Client.DeleteAsync(scenario.Url);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.False(File.Exists(path));
    }
}
