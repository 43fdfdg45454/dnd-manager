using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Library;
using OpenTrpg.Core.Infrastructure.Files;
using OpenTrpg.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static OpenTrpg.Core.Api.Tests.Content.ContentTestHelpers;

namespace OpenTrpg.Core.Api.Tests.Content;

/// <summary>Factory whose storage already holds the operator-provided SRD PDF when the host starts.</summary>
public sealed class SrdApiFactory : ApiFactory
{
    public SrdApiFactory()
    {
        Directory.CreateDirectory(Path.Combine(FilesRoot, "system"));
        File.WriteAllBytes(Path.Combine(FilesRoot, "system", SystemDocumentSeeder.SrdFileName), Pdf());
    }
}

public sealed class LibraryEndpointsTests(SrdApiFactory factory) : IClassFixture<SrdApiFactory>
{
    [Fact]
    public async Task The_srd_pdf_placed_by_the_operator_is_registered_as_a_system_document_once()
    {
        var user = await factory.CreateSignedInUserAsync();

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<SystemDocumentSeeder>().SeedAsync();
        }

        var documents = await user.Client.GetFromJsonAsync<List<LibraryDocumentDto>>("/api/v1/library?search=srd");
        var srd = Assert.Single(documents!);
        Assert.True(srd.IsSystem);
        Assert.Equal("Rules", srd.Category);
        Assert.Equal(SystemDocumentSeeder.SrdFileName, srd.FileName);
        Assert.Contains("CC-BY 4.0", srd.Description);

        var download = await user.Client.GetAsync(srd.Url);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Pdf(), await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task System_documents_cannot_be_deleted_and_stay_available()
    {
        var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateSignedInUserAsync();
        var srd = (await user.Client.GetFromJsonAsync<List<LibraryDocumentDto>>("/api/v1/library"))!.Single(d => d.IsSystem);

        var delete = await admin.DeleteAsync($"/api/v1/library/{srd.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync(srd.Url)).StatusCode);
        Assert.Contains((await user.Client.GetFromJsonAsync<List<LibraryDocumentDto>>("/api/v1/library"))!, d => d.Id == srd.Id);

        // The title can be edited like any other.
        var patch = await admin.PatchAsJsonAsync($"/api/v1/library/{srd.Id}", new { description = "Otra" });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        // Leave it as the seeder wrote it: the other tests of the class share the factory (and run in any order).
        var restore = await admin.PatchAsJsonAsync($"/api/v1/library/{srd.Id}", new { description = srd.Description });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
    }

    [Fact]
    public async Task Admin_uploads_a_pdf_and_every_user_lists_and_downloads_it()
    {
        var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateSignedInUserAsync();
        var title = $"Aventura {Guid.NewGuid():N}";
        var pdf = await admin.UploadOkAsync(Pdf(), "LibraryDocument", fileName: "aventura.pdf");

        var created = await admin.PostAsJsonAsync("/api/v1/library", new { title, description = "  Para nivel 1  ", category = "Adventure", fileId = pdf.Id });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var document = (await created.Content.ReadFromJsonAsync<LibraryDocumentDto>())!;
        Assert.Equal(title, document.Title);
        Assert.Equal("Para nivel 1", document.Description);
        Assert.Equal("Adventure", document.Category);
        Assert.False(document.IsSystem);
        Assert.Equal(pdf.Url, document.Url);
        Assert.Equal("aventura.pdf", document.FileName);
        Assert.Equal(pdf.SizeBytes, document.SizeBytes);

        var all = await user.Client.GetFromJsonAsync<List<LibraryDocumentDto>>("/api/v1/library");
        Assert.Contains(all!, d => d.Id == document.Id);
        var byCategory = await user.Client.GetFromJsonAsync<List<LibraryDocumentDto>>("/api/v1/library?category=Adventure");
        Assert.Contains(byCategory!, d => d.Id == document.Id);
        Assert.DoesNotContain(byCategory!, d => d.IsSystem);
        var bySearch = await user.Client.GetFromJsonAsync<List<LibraryDocumentDto>>($"/api/v1/library?search={title.ToUpperInvariant()}");
        Assert.Equal([document.Id], bySearch!.Select(d => d.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.GetAsync("/api/v1/library?category=Dragon")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync(document.Url)).StatusCode);
    }

    [Fact]
    public async Task Only_admins_manage_the_library()
    {
        var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateSignedInUserAsync();
        var pdf = await admin.UploadOkAsync(Pdf(), "LibraryDocument");
        var document = (await (await admin.PostAsJsonAsync("/api/v1/library", new { title = "Reglas", category = "Rules", fileId = pdf.Id })).Content.ReadFromJsonAsync<LibraryDocumentDto>())!;

        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostAsJsonAsync("/api/v1/library", new { title = "x", category = "Rules", fileId = pdf.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PatchAsJsonAsync($"/api/v1/library/{document.Id}", new { title = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.DeleteAsync($"/api/v1/library/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/library")).StatusCode);
    }

    [Fact]
    public async Task Create_requires_a_library_pdf_and_valid_fields()
    {
        var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateSignedInUserAsync();
        var campaign = await user.CreateCampaignAsync();
        var image = await user.UploadOkAsync(Png(), "MapImage", campaign.Id);
        var apk = await admin.UploadOkAsync(Apk(), "AppRelease");
        var pdf = await admin.UploadOkAsync(Pdf(), "LibraryDocument");

        foreach (var body in new object[]
                 {
                     new { title = "T", category = "Rules", fileId = image.Id },
                     new { title = "T", category = "Rules", fileId = apk.Id },
                     new { title = "T", category = "Rules", fileId = Guid.NewGuid() },
                     new { title = "", category = "Rules", fileId = pdf.Id },
                     new { title = "T", category = "Dragon", fileId = pdf.Id },
                     new { title = "T", category = "Rules", fileId = Guid.Empty },
                 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/library", body)).StatusCode);
        }
    }

    [Fact]
    public async Task Admin_edits_and_deletes_a_document_together_with_its_file()
    {
        var admin = await factory.CreateAdminClientAsync();
        var user = await factory.CreateSignedInUserAsync();
        var pdf = await admin.UploadOkAsync(Pdf(), "LibraryDocument");
        var document = (await (await admin.PostAsJsonAsync("/api/v1/library", new { title = "Viejo", description = "d", category = "Rules", fileId = pdf.Id })).Content.ReadFromJsonAsync<LibraryDocumentDto>())!;

        var patch = await admin.PatchAsJsonAsync($"/api/v1/library/{document.Id}", new { title = "Nuevo", description = "", category = "Homebrew" });

        var updated = (await patch.Content.ReadFromJsonAsync<LibraryDocumentDto>())!;
        Assert.Equal("Nuevo", updated.Title);
        Assert.Null(updated.Description);
        Assert.Equal("Homebrew", updated.Category);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PatchAsJsonAsync($"/api/v1/library/{Guid.NewGuid()}", new { title = "x" })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/library/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/v1/library/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync(pdf.Url)).StatusCode);
    }

    [Fact]
    public async Task Dms_recommend_documents_in_the_campaign_and_members_read_them()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var admin = await factory.CreateAdminClientAsync();
        var pdf = await admin.UploadOkAsync(Pdf(), "LibraryDocument");
        var document = (await (await admin.PostAsJsonAsync("/api/v1/library", new { title = "Bestiario", category = "Supplement", fileId = pdf.Id })).Content.ReadFromJsonAsync<LibraryDocumentDto>())!;
        var url = $"{scenario.Url}/library/{document.Id}";

        Assert.Empty((await scenario.Player.Client.GetFromJsonAsync<List<LibraryDocumentDto>>($"{scenario.Url}/library"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Dm.Client.PutAsJsonAsync(url, new { note = "Leed el capítulo 1" })).StatusCode);

        var recommended = await scenario.Player.Client.GetFromJsonAsync<List<LibraryDocumentDto>>($"{scenario.Url}/library");
        var item = Assert.Single(recommended!);
        Assert.Equal(document.Id, item.Id);
        Assert.Equal("Leed el capítulo 1", item.Note);
        Assert.Equal(pdf.Url, item.Url);

        // Recommending again changes the note, it does not duplicate it; the body is optional.
        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Owner.Client.PutAsJsonAsync(url, new { note = "Otra nota" })).StatusCode);
        Assert.Equal("Otra nota", (await scenario.Player.Client.GetFromJsonAsync<List<LibraryDocumentDto>>($"{scenario.Url}/library"))!.Single().Note);
        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Dm.Client.PutAsync(url, null)).StatusCode);
        Assert.Null((await scenario.Player.Client.GetFromJsonAsync<List<LibraryDocumentDto>>($"{scenario.Url}/library"))!.Single().Note);

        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PutAsJsonAsync(url, new { note = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.GetAsync($"{scenario.Url}/library")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.PutAsJsonAsync(url, new { note = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Dm.Client.PutAsJsonAsync($"{scenario.Url}/library/{Guid.NewGuid()}", new { note = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PutAsJsonAsync(url, new { note = new string('x', 501) })).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Dm.Client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Dm.Client.DeleteAsync(url)).StatusCode);
        Assert.Empty((await scenario.Player.Client.GetFromJsonAsync<List<LibraryDocumentDto>>($"{scenario.Url}/library"))!);
    }

    [Fact]
    public async Task Deleting_a_document_removes_its_recommendations()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var admin = await factory.CreateAdminClientAsync();
        var pdf = await admin.UploadOkAsync(Pdf(), "LibraryDocument");
        var document = (await (await admin.PostAsJsonAsync("/api/v1/library", new { title = "Efímero", category = "Other", fileId = pdf.Id })).Content.ReadFromJsonAsync<LibraryDocumentDto>())!;
        await scenario.Dm.Client.PutAsJsonAsync($"{scenario.Url}/library/{document.Id}", new { note = "n" });

        await admin.DeleteAsync($"/api/v1/library/{document.Id}");

        Assert.Empty((await scenario.Player.Client.GetFromJsonAsync<List<LibraryDocumentDto>>($"{scenario.Url}/library"))!);
        await factory.WithDbAsync(async db => Assert.Equal(0, await db.CampaignDocuments.CountAsync(c => c.DocumentId == document.Id)));
    }
}
