using System.Net;
using System.Net.Http.Json;
using Dnd.Application.Campaigns;
using Dnd.Application.Systems;
using Dnd.Domain.Campaigns;
using Dnd.Domain.Users;
using Dnd.Infrastructure.Persistence;
using Dnd.Infrastructure.Persistence.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Dnd.Api.Tests;

/// <summary>Game system of each campaign: <c>GET /api/v1/systems</c>, campaign creation with a system and the migration.</summary>
public class GameSystemEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>
    /// Body of <c>GET /api/v1/catalog/attribution</c> before the game systems existed. The attribution now
    /// comes from the D&amp;D 5e system and must not change by a single byte.
    /// </summary>
    private const string AttributionJson =
        """{"ruleset":"srd-5.1","license":"CC-BY-4.0","text":"This work includes material taken from the System Reference Document 5.1 (“SRD 5.1”) by Wizards of the Coast LLC and available at https://dnd.wizards.com/resources/systems-reference-document. The SRD 5.1 is licensed under the Creative Commons Attribution 4.0 International License available at https://creativecommons.org/licenses/by/4.0/legalcode."}""";

    [Fact]
    public async Task Systems_require_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/systems");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Systems_list_the_single_default_system()
    {
        var user = await factory.CreateSignedInUserAsync();

        var response = await user.Client.GetAsync("/api/v1/systems");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var system = Assert.Single((await response.Content.ReadFromJsonAsync<List<GameSystemDto>>())!);
        Assert.Equal(new GameSystemDto("dnd5e", "Dungeons & Dragons 5e (SRD 5.1)", "5.1", true), system);
        Assert.Contains("\"isDefault\":true", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Attribution_is_byte_identical_to_the_previous_response()
    {
        var user = await factory.CreateSignedInUserAsync();

        var response = await user.Client.GetAsync("/api/v1/catalog/attribution");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AttributionJson, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Campaign_without_system_uses_dnd5e()
    {
        var owner = await factory.CreateSignedInUserAsync();

        var response = await owner.Client.PostAsJsonAsync("/api/v1/campaigns", new { name = "Sin sistema" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var campaign = (await response.Content.ReadFromJsonAsync<CampaignDto>())!;
        Assert.Equal("dnd5e", campaign.SystemId);
        Assert.Equal("dnd5e", (await owner.Client.GetFromJsonAsync<CampaignDto>($"/api/v1/campaigns/{campaign.Id}"))!.SystemId);
        var summary = Assert.Single((await owner.Client.GetFromJsonAsync<List<CampaignSummaryDto>>("/api/v1/campaigns"))!);
        Assert.Equal("dnd5e", summary.SystemId);
    }

    [Theory]
    [InlineData("dnd5e")]
    [InlineData(" DND5E ")]
    public async Task Campaign_with_dnd5e_uses_dnd5e(string systemId)
    {
        var owner = await factory.CreateSignedInUserAsync();

        var response = await owner.Client.PostAsJsonAsync("/api/v1/campaigns", new { name = "Con sistema", systemId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("dnd5e", (await response.Content.ReadFromJsonAsync<CampaignDto>())!.SystemId);
    }

    [Fact]
    public async Task Campaign_with_an_unknown_system_is_rejected()
    {
        var owner = await factory.CreateSignedInUserAsync();

        var response = await owner.Client.PostAsJsonAsync("/api/v1/campaigns", new { name = "GURPS", systemId = "gurps" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal("unknown_system", problem.GetProperty("code").GetString());
        Assert.True(problem.HasFieldError("systemId"));
        Assert.Contains("Sistema de juego desconocido: «gurps».", problem.ToString());
        Assert.Empty((await owner.Client.GetFromJsonAsync<List<CampaignSummaryDto>>("/api/v1/campaigns"))!);
    }

    [Fact]
    public async Task Campaign_with_a_too_long_system_is_rejected()
    {
        var owner = await factory.CreateSignedInUserAsync();

        var response = await owner.Client.PostAsJsonAsync(
            "/api/v1/campaigns", new { name = "Largo", systemId = new string('x', Campaign.SystemIdMaxLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.ReadProblemAsync()).HasFieldError("systemId"));
    }

    [Fact]
    public async Task Migration_moves_existing_campaigns_to_dnd5e()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;

        // A campaign saved, then the schema taken back to before the migration (Campaigns without SystemId).
        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var now = DateTimeOffset.UtcNow;
            var owner = User.Create("old@example.com", "Old", UserRole.User, now);
            db.Users.Add(owner);
            db.Campaigns.Add(Campaign.Create("Antigua", null, owner.Id, now, "UTC", "other-system"));
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Campaigns\" DROP COLUMN \"SystemId\"");

            // Up operations of the migration, as SQL for this provider.
            var operations = new AddCampaignSystemId().UpOperations;
            var commands = db.GetService<IMigrationsSqlGenerator>().Generate(operations, db.Model);
            foreach (var command in commands)
            {
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            }
        }

        await using (var db = new AppDbContext(options))
        {
            var campaign = await db.Campaigns.SingleAsync();
            Assert.Equal("Antigua", campaign.Name);
            Assert.Equal(Campaign.DefaultSystemId, campaign.SystemId);
        }
    }
}
