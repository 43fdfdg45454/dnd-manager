using Dnd.Domain.Campaigns;
using Dnd.Domain.Common;
using Dnd.Domain.Sessions;

namespace Dnd.Domain.Tests.Sessions;

public class CampaignScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Europe/Madrid", true)]
    [InlineData("America/New_York", true)]
    [InlineData("UTC", true)]
    [InlineData("Mars/Olympus", false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData(" Europe/Madrid", false)]
    [InlineData(null, false)]
    public void Time_zones_must_be_known_iana_ids(string? id, bool valid) =>
        Assert.Equal(valid, CampaignSchedule.IsValidTimeZone(id));

    [Fact]
    public void Local_time_follows_daylight_saving_time()
    {
        Assert.Equal("2026-10-10T20:00:00+02:00", CampaignSchedule.ToLocalIso(new DateTimeOffset(2026, 10, 10, 18, 0, 0, TimeSpan.Zero), "Europe/Madrid"));
        Assert.Equal("2027-01-15T19:00:00+01:00", CampaignSchedule.ToLocalIso(new DateTimeOffset(2027, 1, 15, 18, 0, 0, TimeSpan.Zero), "Europe/Madrid"));
        Assert.Equal("2026-10-10T14:00:00-04:00", CampaignSchedule.ToLocalIso(new DateTimeOffset(2026, 10, 10, 18, 0, 0, TimeSpan.Zero), "America/New_York"));
    }

    [Theory]
    [InlineData(new[] { 1 }, true)]
    [InlineData(new[] { 1440, 120 }, true)]
    [InlineData(new[] { 43200 }, true)]
    [InlineData(new int[0], true)]
    [InlineData(new[] { 0 }, false)]
    [InlineData(new[] { 43201 }, false)]
    [InlineData(new[] { 60, 60 }, false)]
    public void Offsets_are_unique_minutes_between_one_and_thirty_days(int[] offsets, bool valid) =>
        Assert.Equal(valid, CampaignSchedule.AreValidOffsets(offsets));

    [Fact]
    public void Offsets_round_trip_through_json_and_default_when_unreadable()
    {
        Assert.Equal([1440, 120], CampaignSchedule.ParseOffsets(CampaignSchedule.SerializeOffsets([1440, 120])));
        Assert.Equal(CampaignSchedule.DefaultOffsetsMinutes, CampaignSchedule.ParseOffsets(null));
        Assert.Equal(CampaignSchedule.DefaultOffsetsMinutes, CampaignSchedule.ParseOffsets("not json"));
        Assert.Equal([2880, 60], CampaignSchedule.RequireOffsets([60, 2880]));
    }

    [Fact]
    public void A_new_campaign_has_the_given_or_fallback_time_zone_and_default_reminders()
    {
        var withZone = Campaign.Create("Campaña", null, Guid.NewGuid(), Now, "America/New_York");
        var fallback = Campaign.Create("Campaña", null, Guid.NewGuid(), Now);

        Assert.Equal("America/New_York", withZone.TimeZoneId);
        Assert.Equal(CampaignSchedule.FallbackTimeZoneId, fallback.TimeZoneId);
        Assert.Equal([1440, 120], fallback.ReminderOffsetsMinutes);
        Assert.Throws<DomainException>(() => Campaign.Create("Campaña", null, Guid.NewGuid(), Now, "Mars/Olympus"));
    }

    [Fact]
    public void Only_dms_update_the_settings_and_the_result_tells_whether_offsets_changed()
    {
        var owner = Guid.NewGuid();
        var player = Guid.NewGuid();
        var campaign = Campaign.Create("Campaña", null, owner, Now);
        campaign.AddMember(owner, player, CampaignRole.Player, Now);

        Assert.Equal(DomainErrorKind.Forbidden, Assert.Throws<DomainException>(() => campaign.UpdateSettings(player, "UTC", null, Now)).Kind);

        Assert.False(campaign.UpdateSettings(owner, "UTC", null, Now));
        Assert.Equal("UTC", campaign.TimeZoneId);
        Assert.False(campaign.UpdateSettings(owner, null, [120, 1440], Now));
        Assert.True(campaign.UpdateSettings(owner, null, [60], Now));
        Assert.Equal([60], campaign.ReminderOffsetsMinutes);
        Assert.Equal(DomainErrorKind.RuleViolation, Assert.Throws<DomainException>(() => campaign.UpdateSettings(owner, "Mars/Olympus", null, Now)).Kind);
        Assert.Equal(DomainErrorKind.RuleViolation, Assert.Throws<DomainException>(() => campaign.UpdateSettings(owner, null, [0], Now)).Kind);
        Assert.Equal("UTC", campaign.TimeZoneId);
    }
}
