using System.Net;
using System.Net.Http.Json;
using System.Text;
using OpenTrpg.Core.Api.Tests.Items;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.ContentPacks;

namespace OpenTrpg.Core.Api.Tests.ContentPacks;

/// <summary>
/// Validates the operator's private feats pack (<c>content-packs/phb-feats.json</c> at the repository root, never
/// committed: see <c>docs/content-packs.md</c>) when it exists on this machine. The file is outside the test
/// assets on purpose; without it the test passes without checking anything.
/// </summary>
public class PrivateFeatsPackTests(ContentPackApiFactory factory) : IClassFixture<ContentPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";

    [Fact]
    public async Task The_private_feats_pack_imports_and_its_feats_are_offered_with_their_prerequisites()
    {
        if (FindPack() is not { } path)
        {
            return;
        }

        var admin = await factory.CreateAdminClientAsync();
        var response = await admin.PostAsync(PacksUrl, new StringContent(await File.ReadAllTextAsync(path), Encoding.UTF8, "application/json"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<ContentPackImportResultDto>())!;
        Assert.True(result.Counts["options"] >= 40, $"El paquete trae {result.Counts["options"]} dotes.");

        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, "Aprendiz");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", subclassIndex = "champion", level = 3 } },
            baseAbilities = new { str = 16, dex = 12, con = 14, @int = 10, wis = 10, cha = 10 },
            applyRacialBonuses = false,
            proficiencies = new[] { new { type = "Armor", key = "light-armor", expertise = false } },
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null)).StatusCode);
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { character.Id } });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);

        var plan = (await s.Player.Client.GetFromJsonAsync<LevelUpPlanDto>($"{ItemTestHelpers.CharacterUrl(character.Id)}/level-up"))!;
        var asi = Assert.Single(plan.Choices, c => c.Kind == "AsiOrFeat");
        var feats = asi.Options.Where(o => o.Index.StartsWith($"{result.Id}-", StringComparison.Ordinal)).ToList();
        Assert.True(feats.Count >= 40);
        Assert.All(feats, f => Assert.NotEmpty(f.Description));

        // Every ineligible feat says what is missing and what the character has.
        foreach (var feat in feats.Where(f => !f.Eligible))
        {
            Assert.NotNull(feat.PrerequisitesText);
            Assert.Contains("Requiere", feat.Reason);
            Assert.Contains(";", feat.Reason);
        }

        // A fighter with light armor only: medium-armor feats are out, light-armor ones are in, casters' ones are out.
        Assert.Contains(feats, f => f.PrerequisitesText is not null && f.PrerequisitesText.Contains("intermedia", StringComparison.Ordinal) && !f.Eligible);
        Assert.Contains(feats, f => f.PrerequisitesText is not null && f.PrerequisitesText.Contains("ligera", StringComparison.Ordinal) && f.Eligible);
        Assert.Contains(feats, f => f.Reason is not null && f.Reason.Contains("no lanzas conjuros", StringComparison.Ordinal));
        Assert.Contains(feats, f => f.AbilityIncrease is { From.Count: 2 });
        Assert.Contains(feats, f => f.AbilityIncrease is { From.Count: 0 });
        Assert.Contains(feats, f => f.EffectsPreview.Any(e => e.Field == "initiative" && e.Value == 5));
    }

    /// <summary>Walks up from the test binaries to the repository root and returns the pack path when the file exists.</summary>
    private static string? FindPack()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "content-packs", "phb-feats.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
