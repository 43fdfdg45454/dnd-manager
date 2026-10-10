using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Campaigns;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;

namespace OpenTrpg.Core.Api.Tests;

public class CampaignMemberEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Owner = CampaignScenario.OwnerRole;
    private const string Dm = CampaignScenario.DmRole;
    private const string Player = CampaignScenario.PlayerRole;
    private const string Outsider = CampaignScenario.OutsiderRole;

    [Theory]
    [InlineData(Owner, HttpStatusCode.OK)]
    [InlineData(Dm, HttpStatusCode.OK)]
    [InlineData(Player, HttpStatusCode.OK)]
    [InlineData(Outsider, HttpStatusCode.NotFound)]
    public async Task List_members_by_role(string actor, HttpStatusCode expected)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var response = await scenario.As(actor).Client.GetAsync($"{scenario.Url}/members");

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            var members = (await response.Content.ReadFromJsonAsync<List<MemberDto>>())!;
            Assert.Equal(["Owner", "DM", "Player"], members.Select(m => m.Role));
            Assert.Equal(
                [(scenario.Owner.Id, "Owner User", scenario.Owner.Email), (scenario.Dm.Id, "Dm User", scenario.Dm.Email), (scenario.Player.Id, "Player User", scenario.Player.Email)],
                members.Select(m => (m.UserId, m.DisplayName, m.Email)));
        }
    }

    [Theory]
    [InlineData(Owner, "Player", HttpStatusCode.Accepted)]
    [InlineData(Owner, "DM", HttpStatusCode.Accepted)]
    [InlineData(Dm, "Player", HttpStatusCode.Accepted)]
    [InlineData(Dm, "DM", HttpStatusCode.Forbidden)]
    [InlineData(Player, "Player", HttpStatusCode.Forbidden)]
    [InlineData(Player, "DM", HttpStatusCode.Forbidden)]
    [InlineData(Outsider, "Player", HttpStatusCode.NotFound)]
    public async Task Invite_member_by_role(string actor, string role, HttpStatusCode expected)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var newcomer = await factory.CreateSignedInUserAsync("Newcomer");

        var response = await scenario.As(actor).Client.PostAsJsonAsync($"{scenario.Url}/members", new { userId = newcomer.Id, role });

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Accepted)
        {
            var invitation = (await response.Content.ReadFromJsonAsync<CampaignInvitationDto>())!;
            Assert.Equal((newcomer.Id, "Newcomer", newcomer.Email, role), (invitation.UserId, invitation.DisplayName, invitation.Email, invitation.Role));

            // Invited, not yet a member: the campaign stays hidden until they accept.
            Assert.Equal(HttpStatusCode.NotFound, (await newcomer.Client.GetAsync(scenario.Url)).StatusCode);
            var mine = (await newcomer.Client.GetFromJsonAsync<List<MyInvitationDto>>("/api/v1/me/invitations"))!;
            Assert.Equal([invitation.Id], mine.Select(i => i.Id));

            var accept = await newcomer.Client.PostAsync($"/api/v1/invitations/{invitation.Id}/accept", null);
            Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
            var member = (await accept.Content.ReadFromJsonAsync<MemberDto>())!;
            Assert.Equal((newcomer.Id, role), (member.UserId, member.Role));

            var campaign = (await newcomer.Client.GetFromJsonAsync<CampaignDto>(scenario.Url))!;
            Assert.Equal(role, campaign.MyRole);
            Assert.Empty((await newcomer.Client.GetFromJsonAsync<List<MyInvitationDto>>("/api/v1/me/invitations"))!);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, (await newcomer.Client.GetAsync(scenario.Url)).StatusCode);
        }
    }

    [Fact]
    public async Task Add_existing_member_returns_409()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var asPlayer = await scenario.Owner.Client.PostAsJsonAsync($"{scenario.Url}/members", new { userId = scenario.Player.Id, role = "Player" });
        var ownerAgain = await scenario.Dm.Client.PostAsJsonAsync($"{scenario.Url}/members", new { userId = scenario.Owner.Id, role = "Player" });

        Assert.Equal(HttpStatusCode.Conflict, asPlayer.StatusCode);
        Assert.Equal(409, (await asPlayer.ReadProblemAsync()).GetProperty("status").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, ownerAgain.StatusCode);
    }

    [Fact]
    public async Task Add_unknown_or_inactive_user_returns_404()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var inactive = await factory.CreateSignedInUserAsync();
        await factory.SetUserActiveAsync(inactive.Id, false);

        var unknown = await scenario.Owner.Client.PostAsJsonAsync($"{scenario.Url}/members", new { userId = Guid.NewGuid(), role = "Player" });
        var deactivated = await scenario.Owner.Client.PostAsJsonAsync($"{scenario.Url}/members", new { userId = inactive.Id, role = "Player" });

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deactivated.StatusCode);
    }

    [Fact]
    public async Task Add_member_validates_user_and_role()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var newcomer = await factory.CreateSignedInUserAsync();

        var asOwner = await scenario.Owner.Client.PostAsJsonAsync($"{scenario.Url}/members", new { userId = newcomer.Id, role = "Owner" });
        var missing = await scenario.Owner.Client.PostAsJsonAsync($"{scenario.Url}/members", new { role = "dm" });

        Assert.Equal(HttpStatusCode.BadRequest, asOwner.StatusCode);
        Assert.True((await asOwner.ReadProblemAsync()).HasFieldError("role"));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var problem = await missing.ReadProblemAsync();
        Assert.True(problem.HasFieldError("userId"));
        Assert.True(problem.HasFieldError("role"));
    }

    [Theory]
    [InlineData(Owner, HttpStatusCode.OK)]
    [InlineData(Dm, HttpStatusCode.Forbidden)]
    [InlineData(Player, HttpStatusCode.Forbidden)]
    [InlineData(Outsider, HttpStatusCode.NotFound)]
    public async Task Change_member_role_by_role(string actor, HttpStatusCode expected)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var response = await scenario.As(actor).Client.PatchAsJsonAsync($"{scenario.Url}/members/{scenario.Player.Id}", new { role = "DM" });

        Assert.Equal(expected, response.StatusCode);
        var playerView = (await scenario.Player.Client.GetFromJsonAsync<CampaignDto>(scenario.Url))!;
        Assert.Equal(expected == HttpStatusCode.OK ? "DM" : "Player", playerView.MyRole);
        if (expected == HttpStatusCode.OK)
        {
            var member = (await response.Content.ReadFromJsonAsync<MemberDto>())!;
            Assert.Equal((scenario.Player.Id, "Player User", "DM"), (member.UserId, member.DisplayName, member.Role));
        }
    }

    [Fact]
    public async Task Change_role_rejects_the_owner_unknown_members_and_invalid_roles()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var outsiderUrl = $"{scenario.Url}/members/{scenario.Outsider.Id}";

        var ownerRole = await scenario.Owner.Client.PatchAsJsonAsync($"{scenario.Url}/members/{scenario.Owner.Id}", new { role = "Player" });
        var notMember = await scenario.Owner.Client.PatchAsJsonAsync(outsiderUrl, new { role = "Player" });
        var toOwner = await scenario.Owner.Client.PatchAsJsonAsync($"{scenario.Url}/members/{scenario.Dm.Id}", new { role = "Owner" });

        Assert.Equal(HttpStatusCode.BadRequest, ownerRole.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, notMember.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, toOwner.StatusCode);
        Assert.True((await toOwner.ReadProblemAsync()).HasFieldError("role"));

        var demoted = await scenario.Owner.Client.PatchAsJsonAsync($"{scenario.Url}/members/{scenario.Dm.Id}", new { role = "Player" });
        Assert.Equal(HttpStatusCode.OK, demoted.StatusCode);
        Assert.Equal("Player", (await demoted.Content.ReadFromJsonAsync<MemberDto>())!.Role);
    }

    [Theory]
    [InlineData(Owner, HttpStatusCode.NoContent)]
    [InlineData(Dm, HttpStatusCode.NoContent)]
    [InlineData(Player, HttpStatusCode.Forbidden)]
    [InlineData(Outsider, HttpStatusCode.NotFound)]
    public async Task Remove_player_by_role(string actor, HttpStatusCode expected)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var response = await scenario.As(actor).Client.DeleteAsync($"{scenario.Url}/members/{scenario.Player.Id}");

        Assert.Equal(expected, response.StatusCode);
        var playerAccess = await scenario.Player.Client.GetAsync(scenario.Url);
        Assert.Equal(expected == HttpStatusCode.NoContent ? HttpStatusCode.NotFound : HttpStatusCode.OK, playerAccess.StatusCode);
    }

    [Fact]
    public async Task Owner_can_remove_a_dm_but_a_dm_cannot()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var otherDm = await factory.CreateSignedInUserAsync();
        await scenario.Owner.AddMemberAsync(scenario.CampaignId, otherDm, "DM");

        var byDm = await scenario.Dm.Client.DeleteAsync($"{scenario.Url}/members/{otherDm.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, byDm.StatusCode);

        var byOwner = await scenario.Owner.Client.DeleteAsync($"{scenario.Url}/members/{otherDm.Id}");
        Assert.Equal(HttpStatusCode.NoContent, byOwner.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherDm.Client.GetAsync(scenario.Url)).StatusCode);
    }

    [Fact]
    public async Task Owner_cannot_be_removed_nor_leave()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var ownerUrl = $"{scenario.Url}/members/{scenario.Owner.Id}";

        var bySelf = await scenario.Owner.Client.DeleteAsync(ownerUrl);
        var byDm = await scenario.Dm.Client.DeleteAsync(ownerUrl);
        var leave = await scenario.Owner.Client.PostAsync($"{scenario.Url}/leave", null);

        Assert.Equal(HttpStatusCode.BadRequest, bySelf.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, byDm.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, leave.StatusCode);
        Assert.Equal(400, (await leave.ReadProblemAsync()).GetProperty("status").GetInt32());
        var campaign = (await scenario.Owner.Client.GetFromJsonAsync<CampaignDto>(scenario.Url))!;
        Assert.Equal("Owner", campaign.MyRole);
        Assert.Equal(3, campaign.Members.Count);
    }

    [Fact]
    public async Task Remove_unknown_member_returns_404()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var response = await scenario.Owner.Client.DeleteAsync($"{scenario.Url}/members/{scenario.Outsider.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(Owner, HttpStatusCode.BadRequest)]
    [InlineData(Dm, HttpStatusCode.NoContent)]
    [InlineData(Player, HttpStatusCode.NoContent)]
    [InlineData(Outsider, HttpStatusCode.NotFound)]
    public async Task Leave_by_role(string actor, HttpStatusCode expected)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var user = scenario.As(actor);

        var response = await user.Client.PostAsync($"{scenario.Url}/leave", null);

        Assert.Equal(expected, response.StatusCode);
        var campaigns = (await user.Client.GetFromJsonAsync<List<CampaignSummaryDto>>("/api/v1/campaigns"))!;
        Assert.Equal(actor == Owner, campaigns.Any(c => c.Id == scenario.CampaignId));
        if (expected == HttpStatusCode.NoContent)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await user.Client.GetAsync(scenario.Url)).StatusCode);
        }
    }

    [Theory]
    [InlineData(Owner, HttpStatusCode.OK)]
    [InlineData(Dm, HttpStatusCode.Forbidden)]
    [InlineData(Player, HttpStatusCode.Forbidden)]
    [InlineData(Outsider, HttpStatusCode.NotFound)]
    public async Task Transfer_ownership_by_role(string actor, HttpStatusCode expected)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var response = await scenario.As(actor).Client.PostAsJsonAsync(
            $"{scenario.Url}/transfer-ownership",
            new { toUserId = scenario.Player.Id, previousOwnerRole = "DM" });

        Assert.Equal(expected, response.StatusCode);
        var campaign = (await scenario.Player.Client.GetFromJsonAsync<CampaignDto>(scenario.Url))!;
        Assert.Equal(expected == HttpStatusCode.OK ? scenario.Player.Id : scenario.Owner.Id, campaign.OwnerId);
    }

    [Theory]
    [InlineData("DM")]
    [InlineData("Player")]
    public async Task Transfer_changes_owner_keeps_previous_owner_with_given_role_and_records_it(string previousOwnerRole)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var response = await scenario.Owner.Client.PostAsJsonAsync(
            $"{scenario.Url}/transfer-ownership",
            new { toUserId = scenario.Dm.Id, previousOwnerRole });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var campaign = (await response.Content.ReadFromJsonAsync<CampaignDto>())!;
        Assert.Equal(scenario.Dm.Id, campaign.OwnerId);
        Assert.Equal("Dm User", campaign.OwnerDisplayName);
        Assert.Equal(previousOwnerRole, campaign.MyRole);
        Assert.True(campaign.UpdatedAt >= campaign.CreatedAt);
        Assert.Equal("Owner", campaign.Members.Single(m => m.UserId == scenario.Dm.Id).Role);
        Assert.Equal(previousOwnerRole, campaign.Members.Single(m => m.UserId == scenario.Owner.Id).Role);
        Assert.Single(campaign.Members, m => m.Role == "Owner");

        var newOwnerView = (await scenario.Dm.Client.GetFromJsonAsync<CampaignDto>(scenario.Url))!;
        Assert.Equal("Owner", newOwnerView.MyRole);

        await factory.WithDbAsync(async db =>
        {
            var transfer = await db.OwnershipTransfers.SingleAsync(t => t.CampaignId == scenario.CampaignId);
            Assert.Equal(scenario.Owner.Id, transfer.FromUserId);
            Assert.Equal(scenario.Dm.Id, transfer.ToUserId);
            Assert.Equal(Enum.Parse<CampaignRole>(previousOwnerRole), transfer.PreviousOwnerNewRole);
        });

        // Permissions follow the new roles.
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PostAsync($"{scenario.Url}/leave", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Owner.Client.PostAsync($"{scenario.Url}/leave", null)).StatusCode);
    }

    [Fact]
    public async Task Transfer_rejects_self_non_members_and_invalid_roles()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var url = $"{scenario.Url}/transfer-ownership";

        var toSelf = await scenario.Owner.Client.PostAsJsonAsync(url, new { toUserId = scenario.Owner.Id, previousOwnerRole = "DM" });
        var toOutsider = await scenario.Owner.Client.PostAsJsonAsync(url, new { toUserId = scenario.Outsider.Id, previousOwnerRole = "DM" });
        var keepOwner = await scenario.Owner.Client.PostAsJsonAsync(url, new { toUserId = scenario.Dm.Id, previousOwnerRole = "Owner" });

        Assert.Equal(HttpStatusCode.BadRequest, toSelf.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, toOutsider.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, keepOwner.StatusCode);
        Assert.True((await keepOwner.ReadProblemAsync()).HasFieldError("previousOwnerRole"));

        var campaign = (await scenario.Owner.Client.GetFromJsonAsync<CampaignDto>(scenario.Url))!;
        Assert.Equal(scenario.Owner.Id, campaign.OwnerId);
        await factory.WithDbAsync(async db =>
            Assert.False(await db.OwnershipTransfers.AnyAsync(t => t.CampaignId == scenario.CampaignId)));
    }

    [Fact]
    public async Task Player_with_characters_cannot_become_dm_until_they_are_reassigned()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var url = $"{scenario.Url}/members/{scenario.Player.Id}";
        var first = await CreatePlayerCharacterAsync(scenario, "Brisa");
        await CreatePlayerCharacterAsync(scenario, "Albor");

        var blocked = await scenario.Owner.Client.PatchAsJsonAsync(url, new { role = "DM" });

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(
            "Player User tiene personajes en la campaña (Albor, Brisa). Reasígnalos o conviértelos en PNJ antes de nombrarlo DM.",
            await ReadDetailAsync(blocked));
        Assert.Equal("Player", await RoleOfAsync(scenario, scenario.Player.Id));

        var characters = (await scenario.Owner.Client.GetFromJsonAsync<List<CharacterSummaryDto>>($"{scenario.Url}/characters"))!;
        foreach (var character in characters)
        {
            (await scenario.Owner.Client.PutAsJsonAsync($"/api/v1/characters/{character.Id}/owner", new { ownerUserId = (Guid?)null })).EnsureSuccessStatusCode();
        }

        var promoted = await scenario.Owner.Client.PatchAsJsonAsync(url, new { role = "DM" });

        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        Assert.Equal("DM", await RoleOfAsync(scenario, scenario.Player.Id));
        Assert.Equal(HttpStatusCode.OK, (await scenario.Player.Client.GetAsync($"/api/v1/characters/{first}")).StatusCode);
    }

    [Fact]
    public async Task Player_without_characters_becomes_dm_and_a_dm_goes_back_to_player()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var promoted = await scenario.Owner.Client.PatchAsJsonAsync($"{scenario.Url}/members/{scenario.Player.Id}", new { role = "DM" });
        var demoted = await scenario.Owner.Client.PatchAsJsonAsync($"{scenario.Url}/members/{scenario.Dm.Id}", new { role = "Player" });

        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, demoted.StatusCode);
        Assert.Equal("DM", await RoleOfAsync(scenario, scenario.Player.Id));
        Assert.Equal("Player", await RoleOfAsync(scenario, scenario.Dm.Id));
    }

    [Fact]
    public async Task Ownership_cannot_go_to_a_player_with_characters()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        await CreatePlayerCharacterAsync(scenario, "Brisa");

        var response = await scenario.Owner.Client.PostAsJsonAsync(
            $"{scenario.Url}/transfer-ownership",
            new { toUserId = scenario.Player.Id, previousOwnerRole = "Player" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            "Player User tiene personajes en la campaña (Brisa). Reasígnalos o conviértelos en PNJ antes de nombrarlo DM.",
            await ReadDetailAsync(response));
        var campaign = (await scenario.Owner.Client.GetFromJsonAsync<CampaignDto>(scenario.Url))!;
        Assert.Equal(scenario.Owner.Id, campaign.OwnerId);
        await factory.WithDbAsync(async db =>
            Assert.False(await db.OwnershipTransfers.AnyAsync(t => t.CampaignId == scenario.CampaignId)));
    }

    private static async Task<Guid> CreatePlayerCharacterAsync(CampaignScenario scenario, string name)
    {
        var response = await scenario.Player.Client.PostAsJsonAsync($"{scenario.Url}/characters", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!.Id;
    }

    private static async Task<string?> ReadDetailAsync(HttpResponseMessage response) =>
        (await response.ReadProblemAsync()).GetProperty("detail").GetString();

    private static async Task<string> RoleOfAsync(CampaignScenario scenario, Guid userId)
    {
        var campaign = (await scenario.Owner.Client.GetFromJsonAsync<CampaignDto>(scenario.Url))!;
        return campaign.Members.Single(m => m.UserId == userId).Role;
    }

    [Fact]
    public async Task Promoted_dm_can_add_players_but_not_dms()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        (await scenario.Owner.Client.PatchAsJsonAsync($"{scenario.Url}/members/{scenario.Player.Id}", new { role = "DM" })).EnsureSuccessStatusCode();
        var newcomer = await factory.CreateSignedInUserAsync();

        var asDm = await scenario.Player.Client.PostAsJsonAsync($"{scenario.Url}/members", new { userId = newcomer.Id, role = "DM" });
        var asPlayer = await scenario.Player.Client.PostAsJsonAsync($"{scenario.Url}/members", new { userId = newcomer.Id, role = "Player" });

        Assert.Equal(HttpStatusCode.Forbidden, asDm.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, asPlayer.StatusCode);
    }

    [Fact]
    public async Task Invitations_can_be_declined_cancelled_and_are_not_duplicated()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var newcomer = await factory.CreateSignedInUserAsync("Newcomer");

        var invitation = await scenario.Owner.InviteAsync(scenario.CampaignId, newcomer, "Player");
        var again = await scenario.Owner.Client.PostAsJsonAsync($"{scenario.Url}/members", new { userId = newcomer.Id, role = "Player" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // DMs see the pending invitations; players do not.
        var pending = (await scenario.Dm.Client.GetFromJsonAsync<List<CampaignInvitationDto>>($"{scenario.Url}/invitations"))!;
        Assert.Equal([invitation.Id], pending.Select(i => i.Id));
        Assert.Equal("Owner User", pending[0].InvitedByDisplayName);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.GetAsync($"{scenario.Url}/invitations")).StatusCode);

        // Only the invited user can act on it.
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Player.Client.PostAsync($"/api/v1/invitations/{invitation.Id}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Player.Client.PostAsync($"/api/v1/invitations/{invitation.Id}/decline", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await newcomer.Client.PostAsync($"/api/v1/invitations/{invitation.Id}/decline", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await newcomer.Client.PostAsync($"/api/v1/invitations/{invitation.Id}/accept", null)).StatusCode);
        Assert.Empty((await scenario.Dm.Client.GetFromJsonAsync<List<CampaignInvitationDto>>($"{scenario.Url}/invitations"))!);

        // A DM cancels a new invitation; the user no longer sees it.
        var second = await scenario.Dm.InviteAsync(scenario.CampaignId, newcomer, "Player");
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.DeleteAsync($"{scenario.Url}/invitations/{second.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Dm.Client.DeleteAsync($"{scenario.Url}/invitations/{second.Id}")).StatusCode);
        Assert.Empty((await newcomer.Client.GetFromJsonAsync<List<MyInvitationDto>>("/api/v1/me/invitations"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await newcomer.Client.GetAsync(scenario.Url)).StatusCode);
    }
}
