using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Campaigns;
using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Api.Tests;

public class CampaignEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Owner = CampaignScenario.OwnerRole;
    private const string Dm = CampaignScenario.DmRole;
    private const string Player = CampaignScenario.PlayerRole;
    private const string Outsider = CampaignScenario.OutsiderRole;

    [Fact]
    public async Task Create_makes_the_creator_owner_and_only_member()
    {
        var owner = await factory.CreateSignedInUserAsync("Creator");

        var response = await owner.Client.PostAsJsonAsync("/api/v1/campaigns", new { name = "  La Mina Perdida ", description = "# Intro\nTexto" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var campaign = (await response.Content.ReadFromJsonAsync<CampaignDto>())!;
        Assert.Equal($"/api/v1/campaigns/{campaign.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("La Mina Perdida", campaign.Name);
        Assert.Equal("# Intro\nTexto", campaign.Description);
        Assert.Equal(owner.Id, campaign.OwnerId);
        Assert.Equal("Creator", campaign.OwnerDisplayName);
        Assert.Equal("Owner", campaign.MyRole);
        Assert.Equal(campaign.CreatedAt, campaign.UpdatedAt);
        var member = Assert.Single(campaign.Members);
        Assert.Equal(new MemberDto(owner.Id, "Creator", owner.Email, "Owner", campaign.CreatedAt), member);
    }

    [Fact]
    public async Task Create_accepts_a_missing_description_and_validates_lengths()
    {
        var owner = await factory.CreateSignedInUserAsync();

        var withoutDescription = await owner.Client.PostAsJsonAsync("/api/v1/campaigns", new { name = "Sin descripción" });
        Assert.Equal(HttpStatusCode.Created, withoutDescription.StatusCode);
        Assert.Equal(string.Empty, (await withoutDescription.Content.ReadFromJsonAsync<CampaignDto>())!.Description);

        var invalid = await owner.Client.PostAsJsonAsync("/api/v1/campaigns", new { name = " ", description = new string('x', 2001) });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.ReadProblemAsync();
        Assert.True(problem.HasFieldError("name"));
        Assert.True(problem.HasFieldError("description"));

        var tooLong = await owner.Client.PostAsJsonAsync("/api/v1/campaigns", new { name = new string('n', 101), description = "" });
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.True((await tooLong.ReadProblemAsync()).HasFieldError("name"));
    }

    [Fact]
    public async Task Inactive_user_cannot_create_campaigns()
    {
        var user = await factory.CreateSignedInUserAsync();
        await factory.SetUserActiveAsync(user.Id, false);

        var response = await user.Client.PostAsJsonAsync("/api/v1/campaigns", new { name = "Nope", description = "" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_returns_only_my_campaigns_ordered_by_name_with_my_role()
    {
        var me = await factory.CreateSignedInUserAsync("Me");
        var other = await factory.CreateSignedInUserAsync("Other");
        var zeta = await me.CreateCampaignAsync("zeta");
        var alpha = await other.CreateCampaignAsync("Alpha");
        await other.AddMemberAsync(alpha.Id, me, "Player");
        var beta = await other.CreateCampaignAsync("beta");
        await other.AddMemberAsync(beta.Id, me, "DM");
        await other.CreateCampaignAsync("Not mine");

        var campaigns = (await me.Client.GetFromJsonAsync<List<CampaignSummaryDto>>("/api/v1/campaigns"))!;

        Assert.Equal([alpha.Id, beta.Id, zeta.Id], campaigns.Select(c => c.Id));
        Assert.Equal(["Player", "DM", "Owner"], campaigns.Select(c => c.MyRole));
        Assert.Equal([2, 2, 1], campaigns.Select(c => c.MemberCount));
        var first = campaigns[0];
        Assert.Equal("Alpha", first.Name);
        Assert.Equal(other.Id, first.OwnerId);
        Assert.Equal("Other", first.OwnerDisplayName);
        Assert.Equal(alpha.CreatedAt, first.CreatedAt);
    }

    [Fact]
    public async Task Anonymous_requests_get_401()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/campaigns")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/campaigns", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/campaigns/{Guid.NewGuid()}")).StatusCode);
    }

    [Theory]
    [InlineData(Owner, HttpStatusCode.OK)]
    [InlineData(Dm, HttpStatusCode.OK)]
    [InlineData(Player, HttpStatusCode.OK)]
    [InlineData(Outsider, HttpStatusCode.NotFound)]
    public async Task Get_campaign_by_role(string actor, HttpStatusCode expected)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var user = scenario.As(actor);

        var response = await user.Client.GetAsync(scenario.Url);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var campaign = (await response.Content.ReadFromJsonAsync<CampaignDto>())!;
            Assert.Equal(actor, campaign.MyRole);
            Assert.Equal(scenario.Owner.Id, campaign.OwnerId);
            Assert.Equal("Owner User", campaign.OwnerDisplayName);
            Assert.Equal(
                [(scenario.Owner.Id, "Owner"), (scenario.Dm.Id, "DM"), (scenario.Player.Id, "Player")],
                campaign.Members.Select(m => (m.UserId, m.Role)));
        }
        else
        {
            Assert.Equal(404, (await response.ReadProblemAsync()).GetProperty("status").GetInt32());
        }
    }

    [Fact]
    public async Task Get_unknown_campaign_returns_404()
    {
        var user = await factory.CreateSignedInUserAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync($"/api/v1/campaigns/{Guid.NewGuid()}")).StatusCode);
    }

    [Theory]
    [InlineData(Owner, HttpStatusCode.OK)]
    [InlineData(Dm, HttpStatusCode.OK)]
    [InlineData(Player, HttpStatusCode.Forbidden)]
    [InlineData(Outsider, HttpStatusCode.NotFound)]
    public async Task Update_campaign_by_role(string actor, HttpStatusCode expected)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var response = await scenario.As(actor).Client.PatchAsJsonAsync(scenario.Url, new { name = "Renombrada" });

        Assert.Equal(expected, response.StatusCode);
        var current = (await scenario.Owner.Client.GetFromJsonAsync<CampaignDto>(scenario.Url))!;
        Assert.Equal(expected == HttpStatusCode.OK, current.Name == "Renombrada");
    }

    [Fact]
    public async Task Update_is_partial_and_touches_updated_at()
    {
        var owner = await factory.CreateSignedInUserAsync();
        var created = await owner.CreateCampaignAsync("Original", "Descripción original");
        var url = $"/api/v1/campaigns/{created.Id}";

        var renamed = await owner.Client.PatchAsJsonAsync(url, new { name = "Nueva" });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var afterRename = (await renamed.Content.ReadFromJsonAsync<CampaignDto>())!;
        Assert.Equal("Nueva", afterRename.Name);
        Assert.Equal("Descripción original", afterRename.Description);
        Assert.True(afterRename.UpdatedAt >= created.UpdatedAt);
        Assert.Equal("Owner", afterRename.MyRole);
        Assert.Single(afterRename.Members);

        var described = (await (await owner.Client.PatchAsJsonAsync(url, new { description = "" })).Content.ReadFromJsonAsync<CampaignDto>())!;
        Assert.Equal("Nueva", described.Name);
        Assert.Equal(string.Empty, described.Description);

        var invalid = await owner.Client.PatchAsJsonAsync(url, new { name = "", description = new string('x', 2001) });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var problem = await invalid.ReadProblemAsync();
        Assert.True(problem.HasFieldError("name"));
        Assert.True(problem.HasFieldError("description"));
    }

    [Theory]
    [InlineData(Owner, HttpStatusCode.NoContent)]
    [InlineData(Dm, HttpStatusCode.Forbidden)]
    [InlineData(Player, HttpStatusCode.Forbidden)]
    [InlineData(Outsider, HttpStatusCode.NotFound)]
    public async Task Delete_campaign_by_role(string actor, HttpStatusCode expected)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var response = await scenario.As(actor).Client.DeleteAsync(scenario.Url);

        Assert.Equal(expected, response.StatusCode);
        var stillThere = await scenario.Owner.Client.GetAsync(scenario.Url);
        Assert.Equal(expected == HttpStatusCode.NoContent ? HttpStatusCode.NotFound : HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_members_and_transfers_in_cascade()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        (await scenario.Owner.Client.PostAsJsonAsync(
            $"{scenario.Url}/transfer-ownership",
            new { toUserId = scenario.Dm.Id, previousOwnerRole = "DM" })).EnsureSuccessStatusCode();

        var response = await scenario.Dm.Client.DeleteAsync(scenario.Url);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var playerCampaigns = (await scenario.Player.Client.GetFromJsonAsync<List<CampaignSummaryDto>>("/api/v1/campaigns"))!;
        Assert.DoesNotContain(playerCampaigns, c => c.Id == scenario.CampaignId);
        await factory.WithDbAsync(async db =>
        {
            Assert.False(await db.Campaigns.AnyAsync(c => c.Id == scenario.CampaignId));
            Assert.False(await db.CampaignMembers.AnyAsync(m => m.CampaignId == scenario.CampaignId));
            Assert.False(await db.OwnershipTransfers.AnyAsync(t => t.CampaignId == scenario.CampaignId));
        });
    }
}
