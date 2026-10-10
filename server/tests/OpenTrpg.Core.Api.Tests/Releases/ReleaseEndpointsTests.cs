using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OpenTrpg.Core.Application.Releases;
using OpenTrpg.Core.Domain.Files;
using OpenTrpg.Core.Domain.Sessions;
using OpenTrpg.Core.Api.Tests.Content;
using Microsoft.EntityFrameworkCore;
using static OpenTrpg.Core.Api.Tests.Content.ContentTestHelpers;
using static OpenTrpg.Core.Api.Tests.Sessions.SessionTestHelpers;

namespace OpenTrpg.Core.Api.Tests.Releases;

internal static class ReleaseTestHelpers
{
    /// <summary>An APK-looking file (ZIP signature) of <paramref name="size"/> bytes.</summary>
    public static byte[] ApkOfSize(int size, byte fill = 0x2A)
    {
        var bytes = new byte[size];
        Array.Fill(bytes, fill);
        Apk().CopyTo(bytes, 0);
        return bytes;
    }

    public static async Task<HttpResponseMessage> PublishAsync(
        this HttpClient client,
        byte[]? apk,
        string version,
        int buildNumber,
        string? notes = null,
        bool? isMandatory = null,
        string fileName = "app-release.apk")
    {
        using var content = new MultipartFormDataContent();
        if (apk is not null)
        {
            var file = new ByteArrayContent(apk);
            file.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            content.Add(file, "file", fileName);
        }

        content.Add(new StringContent(version), "version");
        content.Add(new StringContent(buildNumber.ToString()), "buildNumber");
        if (notes is not null)
        {
            content.Add(new StringContent(notes), "notes");
        }

        if (isMandatory is { } mandatory)
        {
            content.Add(new StringContent(mandatory ? "true" : "false"), "isMandatory");
        }

        return await client.PostAsync("/api/v1/admin/releases", content);
    }

    public static async Task<ReleaseDto> PublishOkAsync(
        this HttpClient client,
        string version,
        int buildNumber,
        string? notes = null,
        bool? isMandatory = null,
        byte[]? apk = null)
    {
        var response = await client.PublishAsync(apk ?? ApkOfSize(64), version, buildNumber, notes, isMandatory);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReleaseDto>())!;
    }

    public static Task<int> CountFilesAsync(this ApiFactory factory, FileKind kind)
    {
        var count = 0;
        return CountAsync();

        async Task<int> CountAsync()
        {
            await factory.WithDbAsync(async db => count = await db.StoredFiles.CountAsync(f => f.Kind == kind));
            return count;
        }
    }
}

public sealed class ReleaseEndpointsTests(ContentApiFactory factory) : IClassFixture<ContentApiFactory>
{
    [Fact]
    public async Task Latest_answers_204_when_no_release_was_published()
    {
        using var empty = new ApiFactory();

        var response = await empty.CreateClient().GetAsync("/api/v1/app/latest");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Latest_returns_the_highest_build_number_to_anonymous_clients()
    {
        using var isolated = new ApiFactory();
        var admin = await isolated.CreateAdminClientAsync();
        await admin.PublishOkAsync("1.0.0", 5, "Primera");
        var newest = await admin.PublishOkAsync("1.2.0", 12, "Mejoras\nde la hoja", isMandatory: true, apk: ReleaseTestHelpers.ApkOfSize(1500));
        await admin.PublishOkAsync("1.1.0", 8, "Intermedia");

        var response = await isolated.CreateClient().GetAsync("/api/v1/app/latest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var latest = (await response.Content.ReadFromJsonAsync<LatestReleaseDto>())!;
        Assert.Equal("1.2.0", latest.Version);
        Assert.Equal(12, latest.BuildNumber);
        Assert.Equal("Mejoras\nde la hoja", latest.Notes);
        Assert.True(latest.IsMandatory);
        Assert.Equal("/api/v1/app/download/12", latest.DownloadUrl);
        Assert.Equal(1500, latest.SizeBytes);
        Assert.Equal(newest.PublishedAt, latest.PublishedAt);

        // The wire format is camelCase with the fields the app reads.
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var field in new[] { "version", "buildNumber", "notes", "isMandatory", "downloadUrl", "sizeBytes", "publishedAt" })
        {
            Assert.True(json.RootElement.TryGetProperty(field, out _), field);
        }
    }

    [Fact]
    public async Task Latest_has_empty_notes_and_is_not_mandatory_by_default()
    {
        using var isolated = new ApiFactory();
        var admin = await isolated.CreateAdminClientAsync();
        await admin.PublishOkAsync("0.9.0", 1);

        var latest = (await isolated.CreateClient().GetFromJsonAsync<LatestReleaseDto>("/api/v1/app/latest"))!;

        Assert.Equal(string.Empty, latest.Notes);
        Assert.False(latest.IsMandatory);
    }

    [Fact]
    public async Task Repeated_version_or_build_number_answers_409_and_stores_nothing()
    {
        using var isolated = new ApiFactory();
        var admin = await isolated.CreateAdminClientAsync();
        await admin.PublishOkAsync("2.0.0", 20);

        var sameVersion = await admin.PublishAsync(ReleaseTestHelpers.ApkOfSize(64), "2.0.0", 21);
        var sameBuild = await admin.PublishAsync(ReleaseTestHelpers.ApkOfSize(64), "2.0.1", 20);

        Assert.Equal(HttpStatusCode.Conflict, sameVersion.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, sameBuild.StatusCode);
        var releases = (await admin.GetFromJsonAsync<List<ReleaseDto>>("/api/v1/admin/releases"))!;
        Assert.Single(releases);
        Assert.Equal(1, await isolated.CountFilesAsync(FileKind.AppRelease));
    }

    [Fact]
    public async Task Invalid_metadata_or_file_answers_400()
    {
        var admin = await factory.CreateAdminClientAsync();

        var badVersion = await admin.PublishAsync(ReleaseTestHelpers.ApkOfSize(64), "1.2", 31);
        var badBuild = await admin.PublishAsync(ReleaseTestHelpers.ApkOfSize(64), "3.1.0", 0);
        var noFile = await admin.PublishAsync(null, "3.2.0", 32);
        var notAnApk = await admin.PublishAsync(Pdf(), "3.3.0", 33);

        Assert.Equal(HttpStatusCode.BadRequest, badVersion.StatusCode);
        Assert.True((await badVersion.ReadProblemAsync()).HasFieldError("version"));
        Assert.Equal(HttpStatusCode.BadRequest, badBuild.StatusCode);
        Assert.True((await badBuild.ReadProblemAsync()).HasFieldError("buildNumber"));
        Assert.Equal(HttpStatusCode.BadRequest, noFile.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notAnApk.StatusCode);
        Assert.True((await notAnApk.ReadProblemAsync()).HasFieldError("file"));
    }

    [Fact]
    public async Task Oversized_apk_answers_413()
    {
        var admin = await factory.CreateAdminClientAsync();

        var response = await admin.PublishAsync(ReleaseTestHelpers.ApkOfSize((ContentApiFactory.LimitMegabytes * 1024 * 1024) + 10), "4.0.0", 40);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task A_file_uploaded_before_can_be_published_by_its_id_but_only_once()
    {
        using var isolated = new ApiFactory();
        var admin = await isolated.CreateAdminClientAsync();
        var apk = await admin.UploadOkAsync(ReleaseTestHelpers.ApkOfSize(200), "AppRelease", fileName: "dnd.apk");

        var first = await admin.PostAsJsonAsync("/api/v1/admin/releases", new { version = "5.0.0", buildNumber = 50, notes = "Por id", isMandatory = false, fileId = apk.Id });
        var again = await admin.PostAsJsonAsync("/api/v1/admin/releases", new { version = "5.0.1", buildNumber = 51, fileId = apk.Id });
        var missing = await admin.PostAsJsonAsync("/api/v1/admin/releases", new { version = "5.0.2", buildNumber = 52 });
        var pdf = await admin.UploadOkAsync(Pdf(), "LibraryDocument");
        var wrongKind = await admin.PostAsJsonAsync("/api/v1/admin/releases", new { version = "5.0.3", buildNumber = 53, fileId = pdf.Id });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(200, (await first.Content.ReadFromJsonAsync<ReleaseDto>())!.SizeBytes);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongKind.StatusCode);
        Assert.Equal("5.0.0", (await isolated.CreateClient().GetFromJsonAsync<LatestReleaseDto>("/api/v1/app/latest"))!.Version);
    }

    [Fact]
    public async Task Admin_endpoints_require_the_admin_role()
    {
        var admin = await factory.CreateAdminClientAsync();
        var release = await admin.PublishOkAsync("6.0.0", 60);
        var player = await factory.CreateSignedInUserAsync();
        var anonymous = factory.CreateClient();

        foreach (var (client, expected) in new[] { (player.Client, HttpStatusCode.Forbidden), (anonymous, HttpStatusCode.Unauthorized) })
        {
            Assert.Equal(expected, (await client.GetAsync("/api/v1/admin/releases")).StatusCode);
            Assert.Equal(expected, (await client.PublishAsync(ReleaseTestHelpers.ApkOfSize(64), "6.1.0", 61)).StatusCode);
            Assert.Equal(expected, (await client.DeleteAsync($"/api/v1/admin/releases/{release.Id}")).StatusCode);
            Assert.Equal(expected, (await client.GetAsync("/api/v1/admin/stats")).StatusCode);
        }

        Assert.Contains((await admin.GetFromJsonAsync<List<ReleaseDto>>("/api/v1/admin/releases"))!, r => r.Id == release.Id);
    }

    [Fact]
    public async Task The_apk_downloads_anonymously_with_the_apk_file_name_and_supports_ranges()
    {
        var admin = await factory.CreateAdminClientAsync();
        var apk = ReleaseTestHelpers.ApkOfSize(2048);
        var release = await admin.PublishOkAsync("7.1.0", 71, apk: apk);
        var anonymous = factory.CreateClient();

        var full = await anonymous.GetAsync(release.DownloadUrl);

        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        Assert.Equal("application/vnd.android.package-archive", full.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", full.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("dnd-companion-7.1.0.apk", full.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal(apk, await full.Content.ReadAsByteArrayAsync());

        var request = new HttpRequestMessage(HttpMethod.Get, release.DownloadUrl) { Headers = { Range = new RangeHeaderValue(10, 19) } };
        var partial = await anonymous.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
        Assert.Equal(new ContentRangeHeaderValue(10, 19, 2048), partial.Content.Headers.ContentRange);
        Assert.Equal(apk[10..20], await partial.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/v1/app/download/99999")).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_release_removes_it_and_its_apk()
    {
        using var isolated = new ApiFactory();
        var admin = await isolated.CreateAdminClientAsync();
        var old = await admin.PublishOkAsync("8.0.0", 80);
        var newest = await admin.PublishOkAsync("8.1.0", 81);
        var anonymous = isolated.CreateClient();

        var delete = await admin.DeleteAsync($"/api/v1/admin/releases/{newest.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(newest.DownloadUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(old.DownloadUrl)).StatusCode);
        Assert.Equal(1, await isolated.CountFilesAsync(FileKind.AppRelease));
        Assert.Equal("8.0.0", (await anonymous.GetFromJsonAsync<LatestReleaseDto>("/api/v1/app/latest"))!.Version);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/admin/releases/{newest.Id}")).StatusCode);

        await admin.DeleteAsync($"/api/v1/admin/releases/{old.Id}");
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.GetAsync("/api/v1/app/latest")).StatusCode);
    }

    [Fact]
    public async Task Admin_lists_releases_newest_build_first()
    {
        using var isolated = new ApiFactory();
        var admin = await isolated.CreateAdminClientAsync();
        await admin.PublishOkAsync("1.0.0", 1);
        await admin.PublishOkAsync("1.0.2", 3);
        await admin.PublishOkAsync("1.0.1", 2);

        var releases = (await admin.GetFromJsonAsync<List<ReleaseDto>>("/api/v1/admin/releases"))!;

        Assert.Equal([3, 2, 1], releases.Select(r => r.BuildNumber));
    }

    [Fact]
    public async Task Stats_count_users_campaigns_sessions_files_and_the_latest_release()
    {
        using var isolated = new ApiFactory();
        var admin = await isolated.CreateAdminClientAsync();
        var owner = await isolated.CreateSignedInUserAsync();
        var inactive = await isolated.CreateSignedInUserAsync();
        await isolated.SetUserActiveAsync(inactive.Id, false);
        var campaign = await owner.CreateCampaignAsync();

        await owner.CreateSessionAsync(campaign.Id, "Próxima", FromNow(TimeSpan.FromDays(2)));
        var cancelled = await owner.CreateSessionAsync(campaign.Id, "Cancelada", FromNow(TimeSpan.FromDays(4)));
        var forgotten = await owner.CreateSessionAsync(campaign.Id, "Olvidada", FromNow(TimeSpan.FromDays(5)));
        await isolated.WithDbAsync(async db =>
        {
            var now = DateTimeOffset.UtcNow;
            (await db.GameSessions.SingleAsync(s => s.Id == cancelled.Id)).SetStatus(SessionStatus.Cancelled, now);
            (await db.GameSessions.SingleAsync(s => s.Id == forgotten.Id)).Reschedule(now.AddDays(-1), now);
            await db.SaveChangesAsync();
        });

        var apk = ReleaseTestHelpers.ApkOfSize(300);
        await admin.PublishOkAsync("9.0.0", 90, apk: apk);
        var newest = await admin.PublishOkAsync("9.1.0", 91, apk: ReleaseTestHelpers.ApkOfSize(500));
        await admin.UploadOkAsync(Pdf(), "LibraryDocument");

        var response = await admin.GetAsync("/api/v1/admin/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stats = (await response.Content.ReadFromJsonAsync<AdminStatsDto>())!;
        Assert.Matches(@"^\d+\.\d+\.\d+$", stats.ApiVersion);
        Assert.Equal(new StatsUsersDto(Total: 3, Active: 2), stats.Users);
        Assert.Equal(1, stats.Campaigns);
        Assert.Equal(0, stats.Characters);
        Assert.Equal(1, stats.ScheduledSessions);
        Assert.Equal(new StatsFilesDto(Count: 3, TotalBytes: 300 + 500 + Pdf().Length), stats.Files);
        Assert.Equal(new StatsLatestReleaseDto("9.1.0", 91, newest.PublishedAt), stats.LatestRelease);
    }

    [Fact]
    public async Task Stats_of_an_empty_instance_have_no_latest_release()
    {
        using var isolated = new ApiFactory();
        var admin = await isolated.CreateAdminClientAsync();

        var stats = (await admin.GetFromJsonAsync<AdminStatsDto>("/api/v1/admin/stats"))!;

        Assert.Null(stats.LatestRelease);
        Assert.Equal(new StatsFilesDto(0, 0), stats.Files);
        Assert.Equal(1, stats.Users.Total);
    }
}
