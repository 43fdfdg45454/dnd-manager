using System.Net;
using System.Net.Http.Json;
using Dnd.Application.Sessions;
using Dnd.Domain.Sessions;
using static Dnd.Api.Tests.Sessions.SessionTestHelpers;

namespace Dnd.Api.Tests.Sessions;

public sealed class ReminderTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Creating_a_session_in_three_days_generates_the_two_default_reminders()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var startsAt = FromNow(TimeSpan.FromDays(3));

        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: startsAt);

        var stored = await factory.RemindersOfAsync(session.Id);
        Assert.Equal([(1440, startsAt.AddHours(-24)), (120, startsAt.AddHours(-2))], stored.Select(r => (r.OffsetMinutes, r.SendAt)));
        Assert.All(stored, r => Assert.Equal((0, null, null), (r.Attempts, r.SentAt, r.FailedAt)));

        // DMs see them in the DTO; players do not.
        Assert.Equal([1440, 120], session.Reminders!.Select(r => r.OffsetMinutes));
        Assert.Equal(startsAt.AddHours(-24), session.Reminders![0].SendAt);
        Assert.Null((await scenario.Player.GetSessionAsync(session.Id)).Reminders);
    }

    [Fact]
    public async Task Only_offsets_whose_send_time_is_still_in_the_future_get_a_reminder()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();

        var inThreeHours = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: FromNow(TimeSpan.FromHours(3)));
        var inOneHour = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: FromNow(TimeSpan.FromHours(1)));
        var past = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: FromNow(TimeSpan.FromDays(-1)));

        Assert.Equal([120], (await factory.RemindersOfAsync(inThreeHours.Id)).Select(r => r.OffsetMinutes));
        Assert.Empty(await factory.RemindersOfAsync(inOneHour.Id));
        Assert.Empty(await factory.RemindersOfAsync(past.Id));
    }

    [Fact]
    public async Task Moving_the_date_regenerates_the_pending_reminders()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: FromNow(TimeSpan.FromDays(3)));
        var oldIds = (await factory.RemindersOfAsync(session.Id)).Select(r => r.Id).ToHashSet();

        var newStart = FromNow(TimeSpan.FromDays(10));
        var moved = await scenario.Dm.PatchSessionAsync(session.Id, new { startsAt = newStart });

        var stored = await factory.RemindersOfAsync(session.Id);
        Assert.Equal([(1440, newStart.AddHours(-24)), (120, newStart.AddHours(-2))], stored.Select(r => (r.OffsetMinutes, r.SendAt)));
        Assert.DoesNotContain(stored, r => oldIds.Contains(r.Id));
        Assert.Equal(newStart.AddHours(-24), moved.Reminders![0].SendAt);

        // Editing something else leaves the reminders alone.
        await scenario.Dm.PatchSessionAsync(session.Id, new { title = "Otro título" });
        Assert.Equal(stored.Select(r => r.Id), (await factory.RemindersOfAsync(session.Id)).Select(r => r.Id));
    }

    [Fact]
    public async Task Cancelling_removes_the_pending_reminders_and_scheduling_again_restores_them()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);

        var cancelled = await scenario.Dm.PatchSessionAsync(session.Id, new { status = "Cancelled" });
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Empty(cancelled.Reminders!);
        Assert.Empty(await factory.RemindersOfAsync(session.Id));

        // Moving a cancelled session does not schedule anything either.
        await scenario.Dm.PatchSessionAsync(session.Id, new { startsAt = FromNow(TimeSpan.FromDays(8)) });
        Assert.Empty(await factory.RemindersOfAsync(session.Id));

        var restored = await scenario.Dm.PatchSessionAsync(session.Id, new { status = "Scheduled" });
        Assert.Equal(2, restored.Reminders!.Count);
        Assert.Equal(2, (await factory.RemindersOfAsync(session.Id)).Count);

        var done = await scenario.Dm.PatchSessionAsync(session.Id, new { status = "Done" });
        Assert.Empty(done.Reminders!);
    }

    [Fact]
    public async Task The_processor_emails_every_member_and_marks_the_reminder_as_sent()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var startsAt = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(3).AddHours(18), TimeSpan.Zero);
        var session = await scenario.Dm.CreateSessionAsync(
            scenario.CampaignId, "La mina perdida", startsAt, durationMinutes: 210, location: "Taberna del Dragón", notes: "Traed dados\nY algo de picar");
        await scenario.Player.RespondAsync(session.Id, "Yes");
        var users = new[] { scenario.Owner, scenario.Dm, scenario.Player };

        // Nothing is due yet.
        await factory.ProcessRemindersAsync(startsAt.AddHours(-25));
        Assert.All(users, u => Assert.Empty(factory.Emails.SentTo(u.Email)));

        await factory.ProcessRemindersAsync(startsAt.AddHours(-24).AddMinutes(1));

        var reminders = await factory.RemindersOfAsync(session.Id);
        Assert.NotNull(reminders[0].SentAt);
        Assert.Equal(startsAt.AddHours(-24).AddMinutes(1), reminders[0].SentAt);
        Assert.Null(reminders[1].SentAt);
        Assert.All(users, u => Assert.Single(factory.Emails.SentTo(u.Email)));

        var toPlayer = factory.Emails.LastSentTo(scenario.Player.Email);
        var expectedDate = SessionLocal(startsAt);
        Assert.Equal($"Recordatorio: La mina perdida — {expectedDate}", toPlayer.Subject);
        Assert.Contains("Taberna del Dragón", toPlayer.TextBody);
        Assert.Contains("Traed dados", toPlayer.TextBody);
        Assert.Contains("3 h 30 min", toPlayer.TextBody);
        Assert.Contains("Europe/Madrid", toPlayer.TextBody);
        Assert.Contains("Vas a asistir", toPlayer.TextBody);
        Assert.Contains("Taberna del Dragón", WebUtility.HtmlDecode(toPlayer.HtmlBody));
        Assert.Contains("Sin responder todavía", factory.Emails.LastSentTo(scenario.Dm.Email).TextBody);

        var (sessionId, token, url) = FakeEmailSender.ExtractSessionLink(toPlayer);
        Assert.Equal(session.Id, sessionId);
        Assert.StartsWith($"{ApiFactory.TestOrigin}/sessions/{session.Id}?token=", url);
        Assert.NotEmpty(token);

        // A reminder that was sent is not sent again.
        await factory.ProcessRemindersAsync(startsAt.AddHours(-23));
        Assert.All(users, u => Assert.Single(factory.Emails.SentTo(u.Email)));

        await factory.ProcessRemindersAsync(startsAt.AddHours(-2));
        Assert.All(users, u => Assert.Equal(2, factory.Emails.SentTo(u.Email).Count));
        Assert.All(await factory.RemindersOfAsync(session.Id), r => Assert.NotNull(r.SentAt));
    }

    [Fact]
    public async Task Users_with_notifications_disabled_do_not_receive_reminders()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var startsAt = FromNow(TimeSpan.FromDays(3));
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: startsAt);
        await scenario.Player.SetNotificationsAsync(false);

        await factory.ProcessRemindersAsync(startsAt.AddHours(-24).AddMinutes(1));

        Assert.Empty(factory.Emails.SentTo(scenario.Player.Email));
        Assert.Single(factory.Emails.SentTo(scenario.Dm.Email));
        Assert.Single(factory.Emails.SentTo(scenario.Owner.Email));
        Assert.NotNull((await factory.RemindersOfAsync(session.Id))[0].SentAt);

        // Deactivated accounts are skipped too.
        await factory.SetUserActiveAsync(scenario.Dm.Id, false);
        await factory.ProcessRemindersAsync(startsAt.AddHours(-2));
        Assert.Single(factory.Emails.SentTo(scenario.Dm.Email));
        Assert.Equal(2, factory.Emails.SentTo(scenario.Owner.Email).Count);
    }

    [Fact]
    public async Task A_failing_sender_increments_attempts_retries_after_five_minutes_and_gives_up_at_the_third_failure()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var startsAt = FromNow(TimeSpan.FromDays(3));
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: startsAt);
        var due = startsAt.AddHours(-24).AddMinutes(1);
        var users = new[] { scenario.Owner.Email, scenario.Dm.Email, scenario.Player.Email };
        factory.Emails.FailWhen = m => users.Contains(m.To) ? new InvalidOperationException("SMTP caído") : null;
        try
        {
            await factory.ProcessRemindersAsync(due);
            var first = (await factory.RemindersOfAsync(session.Id))[0];
            Assert.Equal((1, null, null), (first.Attempts, first.SentAt, first.FailedAt));
            Assert.Equal("SMTP caído", first.LastError);

            // Not retried before five minutes have passed.
            await factory.ProcessRemindersAsync(due.AddMinutes(4));
            Assert.Equal(1, (await factory.RemindersOfAsync(session.Id))[0].Attempts);

            await factory.ProcessRemindersAsync(due.AddMinutes(5));
            Assert.Equal(2, (await factory.RemindersOfAsync(session.Id))[0].Attempts);

            await factory.ProcessRemindersAsync(due.AddMinutes(10));
            var abandoned = (await factory.RemindersOfAsync(session.Id))[0];
            Assert.Equal(3, abandoned.Attempts);
            Assert.Equal(due.AddMinutes(10), abandoned.FailedAt);
            Assert.Null(abandoned.SentAt);

            // The abandoned reminder is not attempted again and the DM can see it failed.
            await factory.ProcessRemindersAsync(due.AddMinutes(30));
            Assert.Equal(3, (await factory.RemindersOfAsync(session.Id))[0].Attempts);
            Assert.NotNull((await scenario.Dm.GetSessionAsync(session.Id)).Reminders![0].FailedAt);
            Assert.All(users, email => Assert.Empty(factory.Emails.SentTo(email)));
        }
        finally
        {
            factory.Emails.FailWhen = null;
        }
    }

    [Fact]
    public async Task A_reminder_that_failed_once_is_sent_when_the_retry_succeeds()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var startsAt = FromNow(TimeSpan.FromDays(3));
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: startsAt);
        var due = startsAt.AddHours(-24).AddMinutes(1);
        var users = new[] { scenario.Owner.Email, scenario.Dm.Email, scenario.Player.Email };

        factory.Emails.FailWhen = m => users.Contains(m.To) ? new InvalidOperationException("SMTP caído") : null;
        try
        {
            await factory.ProcessRemindersAsync(due);
        }
        finally
        {
            factory.Emails.FailWhen = null;
        }

        await factory.ProcessRemindersAsync(due.AddMinutes(5));

        var reminder = (await factory.RemindersOfAsync(session.Id))[0];
        Assert.Equal((1, due.AddMinutes(5)), (reminder.Attempts, reminder.SentAt));
        Assert.All(users, email => Assert.Single(factory.Emails.SentTo(email)));
    }

    [Fact]
    public async Task One_unreachable_recipient_does_not_block_or_duplicate_the_others()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var startsAt = FromNow(TimeSpan.FromDays(3));
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: startsAt);
        factory.Emails.FailWhen = m => m.To == scenario.Dm.Email ? new InvalidOperationException("Buzón inexistente") : null;
        try
        {
            await factory.ProcessRemindersAsync(startsAt.AddHours(-24).AddMinutes(1));
            await factory.ProcessRemindersAsync(startsAt.AddHours(-24).AddMinutes(10));
        }
        finally
        {
            factory.Emails.FailWhen = null;
        }

        var reminder = (await factory.RemindersOfAsync(session.Id))[0];
        Assert.NotNull(reminder.SentAt);
        Assert.Contains("Buzón inexistente", reminder.LastError);
        Assert.Single(factory.Emails.SentTo(scenario.Owner.Email));
        Assert.Single(factory.Emails.SentTo(scenario.Player.Email));
        Assert.Empty(factory.Emails.SentTo(scenario.Dm.Email));
    }

    [Fact]
    public async Task Changing_the_campaign_offsets_regenerates_the_reminders_of_upcoming_sessions()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var startsAt = FromNow(TimeSpan.FromDays(3));
        var upcoming = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: startsAt);
        var past = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: FromNow(TimeSpan.FromDays(-3)));

        var campaign = await scenario.Dm.PatchSettingsAsync(scenario.CampaignId, new { reminderOffsetsMinutes = new[] { 60, 2880 } });

        Assert.Equal([2880, 60], campaign.ReminderOffsetsMinutes);
        Assert.Equal(
            [(2880, startsAt.AddDays(-2)), (60, startsAt.AddHours(-1))],
            (await factory.RemindersOfAsync(upcoming.Id)).Select(r => (r.OffsetMinutes, r.SendAt)));
        Assert.Empty(await factory.RemindersOfAsync(past.Id));

        // New sessions use the new offsets; an empty list means no reminders.
        var another = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, startsAt: FromNow(TimeSpan.FromDays(9)));
        Assert.Equal([2880, 60], another.Reminders!.Select(r => r.OffsetMinutes));
        await scenario.Dm.PatchSettingsAsync(scenario.CampaignId, new { reminderOffsetsMinutes = Array.Empty<int>() });
        Assert.Empty(await factory.RemindersOfAsync(another.Id));
    }

    [Fact]
    public async Task Notify_emails_members_with_notifications_enabled_and_only_dms_can_send_it()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "La mina perdida");
        var campaignName = (await scenario.Dm.Client.GetFromJsonAsync<Dnd.Application.Campaigns.CampaignDto>(scenario.Url))!.Name;
        await scenario.Player.SetNotificationsAsync(false);

        var response = await scenario.Dm.Client.PostAsJsonAsync($"/api/v1/sessions/{session.Id}/notify", new { subject = "Cambio de sitio", message = "Nos vemos en la biblioteca.\nTraed café." });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(factory.Emails.SentTo(scenario.Player.Email));
        foreach (var user in new[] { scenario.Owner, scenario.Dm })
        {
            var email = Assert.Single(factory.Emails.SentTo(user.Email));
            Assert.Equal($"[{campaignName}] Cambio de sitio", email.Subject);
            Assert.Contains("Nos vemos en la biblioteca.", email.TextBody);
            Assert.Contains("Traed café.", WebUtility.HtmlDecode(email.HtmlBody));
            Assert.Equal(session.Id, FakeEmailSender.ExtractSessionLink(email).SessionId);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PostAsJsonAsync($"/api/v1/sessions/{session.Id}/notify", new { subject = "a", message = "b" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.PostAsJsonAsync($"/api/v1/sessions/{session.Id}/notify", new { subject = "a", message = "b" })).StatusCode);

        foreach (var body in new object[]
                 {
                     new { subject = "", message = "b" },
                     new { subject = "a", message = "" },
                     new { subject = "una\nlínea", message = "b" },
                     new { subject = new string('x', 151), message = "b" },
                 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PostAsJsonAsync($"/api/v1/sessions/{session.Id}/notify", body)).StatusCode);
        }
    }

    [Fact]
    public async Task Notify_fails_when_no_email_could_be_delivered()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);
        var emails = new[] { scenario.Owner.Email, scenario.Dm.Email, scenario.Player.Email };
        factory.Emails.FailWhen = m => emails.Contains(m.To) ? new InvalidOperationException("SMTP caído") : null;
        try
        {
            var response = await scenario.Dm.Client.PostAsJsonAsync($"/api/v1/sessions/{session.Id}/notify", new { subject = "a", message = "b" });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        finally
        {
            factory.Emails.FailWhen = null;
        }
    }

    private static string SessionLocal(DateTimeOffset startsAt)
    {
        var local = TimeZoneInfo.ConvertTime(startsAt, TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid"));
        string[] days = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];
        string[] months = ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];
        return $"{days[(int)local.DayOfWeek]} {local.Day} de {months[local.Month - 1]} de {local.Year}, {local:HH:mm}";
    }
}
