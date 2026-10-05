using Dnd.Application.Abstractions;
using Dnd.Application.Common;
using Dnd.Domain.Campaigns;
using Microsoft.Extensions.DependencyInjection;

namespace Dnd.Api.Tests;

public class CampaignAccessTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData(CampaignScenario.OwnerRole, CampaignRole.Owner)]
    [InlineData(CampaignScenario.DmRole, CampaignRole.DM)]
    [InlineData(CampaignScenario.PlayerRole, CampaignRole.Player)]
    public async Task GetRole_returns_the_role_of_each_member(string actor, CampaignRole expected)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var role = await WithAccessAsync(access => access.GetRoleAsync(scenario.CampaignId, scenario.As(actor).Id));

        Assert.Equal(expected, role);
    }

    [Fact]
    public async Task GetRole_returns_null_for_non_members_and_unknown_campaigns()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        Assert.Null(await WithAccessAsync(access => access.GetRoleAsync(scenario.CampaignId, scenario.Outsider.Id)));
        Assert.Null(await WithAccessAsync(access => access.GetRoleAsync(Guid.NewGuid(), scenario.Owner.Id)));
    }

    [Theory]
    [InlineData(CampaignScenario.OwnerRole, CampaignRole.Player, null)]
    [InlineData(CampaignScenario.OwnerRole, CampaignRole.DM, null)]
    [InlineData(CampaignScenario.OwnerRole, CampaignRole.Owner, null)]
    [InlineData(CampaignScenario.DmRole, CampaignRole.Player, null)]
    [InlineData(CampaignScenario.DmRole, CampaignRole.DM, null)]
    [InlineData(CampaignScenario.DmRole, CampaignRole.Owner, AppErrorKind.Forbidden)]
    [InlineData(CampaignScenario.PlayerRole, CampaignRole.Player, null)]
    [InlineData(CampaignScenario.PlayerRole, CampaignRole.DM, AppErrorKind.Forbidden)]
    [InlineData(CampaignScenario.PlayerRole, CampaignRole.Owner, AppErrorKind.Forbidden)]
    [InlineData(CampaignScenario.OutsiderRole, CampaignRole.Player, AppErrorKind.NotFound)]
    [InlineData(CampaignScenario.OutsiderRole, CampaignRole.Owner, AppErrorKind.NotFound)]
    public async Task Require_grants_or_rejects_according_to_the_role_rank(string actor, CampaignRole minimum, AppErrorKind? expectedError)
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var userId = scenario.As(actor).Id;

        if (expectedError is null)
        {
            var role = await WithAccessAsync(access => access.RequireAsync(scenario.CampaignId, userId, minimum));
            Assert.True(role.IsAtLeast(minimum));
        }
        else
        {
            var error = await Assert.ThrowsAsync<AppException>(
                () => WithAccessAsync(access => access.RequireAsync(scenario.CampaignId, userId, minimum)));
            Assert.Equal(expectedError, error.Kind);
        }
    }

    [Fact]
    public async Task Require_on_an_unknown_campaign_is_not_found()
    {
        var user = await factory.CreateSignedInUserAsync();

        var error = await Assert.ThrowsAsync<AppException>(
            () => WithAccessAsync(access => access.RequireAsync(Guid.NewGuid(), user.Id, CampaignRole.Player)));

        Assert.Equal(AppErrorKind.NotFound, error.Kind);
    }

    private async Task<T> WithAccessAsync<T>(Func<ICampaignAccess, Task<T>> action)
    {
        using var scope = factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ICampaignAccess>());
    }
}
