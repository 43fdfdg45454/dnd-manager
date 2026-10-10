using System.Net;
using System.Net.Http.Json;
using System.Text;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Api.Tests;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>
/// Validates the operator's private third-party pack <c>content-packs/valdas-spire.json</c> (repository root, never
/// committed: see <c>docs/content-packs.md</c>) when it exists on this machine: it must import without validation
/// errors. Without the file the test passes without checking anything. Phase 35C extends it with the creation and
/// level-up walkthrough of every new class and race.
/// </summary>
public class PrivateValdasPackTests(ContentPackApiFactory factory) : IClassFixture<ContentPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string PackId = "valdas-spire";

    [Fact]
    public async Task The_private_valdas_pack_imports_without_errors()
    {
        if (FindPack() is not { } path)
        {
            return;
        }

        var admin = await factory.CreateAdminClientAsync();
        var response = await admin.PostAsync(PacksUrl, new StringContent(await File.ReadAllTextAsync(path), Encoding.UTF8, "application/json"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<ContentPackImportResultDto>())!;
        Assert.Equal((PackId, 3, "dnd5e"), (result.Id, result.FormatVersion, result.SystemId));
    }

    private static string? FindPack()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "content-packs", $"{PackId}.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
