using System.Net;
using System.Net.Http.Json;
using Dnd.Application.Campaigns;
using Dnd.Application.Users;
using static Dnd.Api.Tests.Sessions.SessionTestHelpers;

namespace Dnd.Api.Tests.Sessions;

public sealed class CampaignSettingsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task New_campaigns_get_the_server_time_zone_and_the_default_reminders()
    {
        var owner = await factory.CreateSignedInUserAsync();

        var created = await owner.CreateCampaignAsync();

        Assert.Equal("Europe/Madrid", created.TimeZoneId);
        Assert.Equal([1440, 120], created.ReminderOffsetsMinutes);
        var fetched = (await owner.Client.GetFromJsonAsync<CampaignDto>($"/api/v1/campaigns/{created.Id}"))!;
        Assert.Equal("Europe/Madrid", fetched.TimeZoneId);
        Assert.Equal([1440, 120], fetched.ReminderOffsetsMinutes);
    }

    [Fact]
    public async Task A_dm_changes_the_time_zone_and_the_offsets_independently()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var zone = await scenario.Dm.PatchSettingsAsync(scenario.CampaignId, new { timeZoneId = "Atlantic/Canary" });
        Assert.Equal("Atlantic/Canary", zone.TimeZoneId);
        Assert.Equal([1440, 120], zone.ReminderOffsetsMinutes);

        var offsets = await scenario.Owner.PatchSettingsAsync(scenario.CampaignId, new { reminderOffsetsMinutes = new[] { 120, 43200, 10 } });
        Assert.Equal("Atlantic/Canary", offsets.TimeZoneId);
        Assert.Equal([43200, 120, 10], offsets.ReminderOffsetsMinutes);

        var unchanged = await scenario.Dm.PatchSettingsAsync(scenario.CampaignId, new { });
        Assert.Equal("Atlantic/Canary", unchanged.TimeZoneId);
        Assert.Equal([43200, 120, 10], unchanged.ReminderOffsetsMinutes);
        Assert.Equal(3, unchanged.Members.Count);
        Assert.Equal("DM", unchanged.MyRole);
    }

    [Fact]
    public async Task Settings_are_validated_and_restricted_to_dms()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var url = $"{scenario.Url}/settings";

        foreach (var body in new object[]
                 {
                     new { timeZoneId = "Mars/Olympus" },
                     new { timeZoneId = "" },
                     new { timeZoneId = " Europe/Madrid" },
                     new { reminderOffsetsMinutes = new[] { 0 } },
                     new { reminderOffsetsMinutes = new[] { 43_201 } },
                     new { reminderOffsetsMinutes = new[] { -5 } },
                     new { reminderOffsetsMinutes = new[] { 60, 60 } },
                     new { reminderOffsetsMinutes = Enumerable.Range(1, 11).ToArray() },
                 })
        {
            var response = await scenario.Dm.Client.PatchAsJsonAsync(url, body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PatchAsJsonAsync(url, new { timeZoneId = "Europe/Paris" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.PatchAsJsonAsync(url, new { timeZoneId = "Europe/Paris" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PatchAsJsonAsync(url, new { timeZoneId = "Europe/Paris" })).StatusCode);

        var campaign = (await scenario.Dm.Client.GetFromJsonAsync<CampaignDto>(scenario.Url))!;
        Assert.Equal("Europe/Madrid", campaign.TimeZoneId);
        Assert.Equal([1440, 120], campaign.ReminderOffsetsMinutes);
    }

    [Fact]
    public async Task Users_edit_their_display_name_and_whether_they_receive_emails()
    {
        var user = await factory.CreateSignedInUserAsync("Nombre inicial");
        var me = (await user.Client.GetFromJsonAsync<UserDto>("/api/v1/auth/me"))!;
        Assert.True(me.NotificationsEnabled);

        var off = await user.Client.PatchAsJsonAsync("/api/v1/auth/me", new { notificationsEnabled = false });
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        var afterOff = (await off.Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal((false, "Nombre inicial", user.Id), (afterOff.NotificationsEnabled, afterOff.DisplayName, afterOff.Id));

        var renamed = (await (await user.Client.PatchAsJsonAsync("/api/v1/auth/me", new { displayName = "  Nombre nuevo " })).Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal(("Nombre nuevo", false), (renamed.DisplayName, renamed.NotificationsEnabled));

        var on = (await (await user.Client.PatchAsJsonAsync("/api/v1/auth/me", new { notificationsEnabled = true, displayName = "Otro" })).Content.ReadFromJsonAsync<UserDto>())!;
        Assert.Equal(("Otro", true), (on.DisplayName, on.NotificationsEnabled));
        Assert.Equal("Otro", (await user.Client.GetFromJsonAsync<UserDto>("/api/v1/auth/me"))!.DisplayName);

        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.PatchAsJsonAsync("/api/v1/auth/me", new { displayName = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await user.Client.PatchAsJsonAsync("/api/v1/auth/me", new { displayName = new string('x', 101) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await user.Client.PatchAsJsonAsync("/api/v1/auth/me", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PatchAsJsonAsync("/api/v1/auth/me", new { notificationsEnabled = false })).StatusCode);
    }
}
