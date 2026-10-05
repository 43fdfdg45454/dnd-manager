using System.Net;
using System.Net.Http.Json;
using Dnd.Application.Sessions;
using Dnd.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using static Dnd.Api.Tests.Sessions.SessionTestHelpers;

namespace Dnd.Api.Tests.Sessions;

public sealed class SessionEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_dm_creates_a_session_and_gets_the_dto()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var startsAt = FromNow(TimeSpan.FromDays(3));

        var response = await scenario.Dm.PostSessionAsync(scenario.CampaignId, "  La mina perdida ", startsAt, 180, "Taberna del Dragón", "Traed dados");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = (await response.Content.ReadFromJsonAsync<SessionDto>())!;
        Assert.Equal($"/api/v1/sessions/{session.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal((1, scenario.CampaignId, "La mina perdida"), (session.Number, session.CampaignId, session.Title));
        Assert.Equal(startsAt, session.StartsAt);
        Assert.Equal((180, "Taberna del Dragón", "Traed dados", "Scheduled"), (session.DurationMinutes, session.Location, session.Notes, session.Status));
        Assert.Null(session.SummaryMarkdown);
        Assert.Null(session.MyRsvp);
        Assert.Empty(session.Rsvps);
        Assert.Equal(new RsvpCountsDto(0, 0, 0, 3), session.Counts);

        // The owner can create sessions too.
        Assert.Equal(2, (await scenario.Owner.CreateSessionAsync(scenario.CampaignId)).Number);
    }

    [Fact]
    public async Task Players_and_outsiders_cannot_create_edit_or_delete_sessions()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);
        var url = $"/api/v1/sessions/{session.Id}";

        var startsAt = FromNow(TimeSpan.FromDays(2));
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.PostSessionAsync(scenario.CampaignId, "Nueva", startsAt)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PatchAsJsonAsync(url, new { title = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PostAsJsonAsync($"{url}/notify", new { subject = "a", message = "b" })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.PostSessionAsync(scenario.CampaignId, "Nueva", startsAt)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.PatchAsJsonAsync(url, new { title = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.GetAsync($"{scenario.Url}/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.RespondRawAsync(session.Id, "Yes")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(url)).StatusCode);
        Assert.Equal("La mina perdida", (await scenario.Dm.GetSessionAsync(session.Id)).Title);
    }

    [Fact]
    public async Task Number_is_correlative_per_campaign_and_stable_when_dates_change()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateCampaignScenarioAsync();

        var late = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Tarde", FromNow(TimeSpan.FromDays(30)));
        var early = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Temprano", FromNow(TimeSpan.FromDays(5)));
        var third = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Tercera", FromNow(TimeSpan.FromDays(10)));
        var otherFirst = await other.Dm.CreateSessionAsync(other.CampaignId);

        Assert.Equal([1, 2, 3], [late.Number, early.Number, third.Number]);
        Assert.Equal(1, otherFirst.Number);

        // Moving the first session after the others keeps its number; the list is ordered by date.
        var moved = await scenario.Dm.PatchSessionAsync(late.Id, new { startsAt = FromNow(TimeSpan.FromDays(60)) });
        Assert.Equal(1, moved.Number);
        var list = await scenario.Dm.Client.GetFromJsonAsync<List<SessionDto>>($"{scenario.Url}/sessions");
        Assert.Equal([2, 3, 1], list!.Select(s => s.Number));
    }

    [Fact]
    public async Task The_same_number_cannot_be_stored_twice_in_a_campaign()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var first = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);

        var duplicate = GameSession.Create(scenario.CampaignId, first.Number, "Duplicada", DateTimeOffset.UtcNow, null, null, null, scenario.Dm.Id, DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<DbUpdateException>(() => factory.WithDbAsync(async db =>
        {
            db.GameSessions.Add(duplicate);
            await db.SaveChangesAsync();
        }));
    }

    [Fact]
    public async Task StartsAtLocal_uses_the_time_zone_of_the_campaign()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var summer = new DateTimeOffset(2026, 10, 10, 18, 0, 0, TimeSpan.Zero);
        var winter = new DateTimeOffset(2027, 1, 15, 18, 0, 0, TimeSpan.Zero);

        var inSummer = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Verano", summer);
        var inWinter = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Invierno", winter);

        Assert.Equal("Europe/Madrid", inSummer.TimeZoneId);
        Assert.Equal("2026-10-10T20:00:00+02:00", inSummer.StartsAtLocal);
        Assert.Equal("2027-01-15T19:00:00+01:00", inWinter.StartsAtLocal);
        Assert.Equal(summer, inSummer.StartsAt);

        // The offset sent by the client does not matter: the instant is stored in UTC.
        var withOffset = await scenario.Dm.PostSessionAsync(scenario.CampaignId, "Con offset", new DateTimeOffset(2026, 10, 10, 20, 0, 0, TimeSpan.FromHours(2)));
        Assert.Equal(summer, (await withOffset.Content.ReadFromJsonAsync<SessionDto>())!.StartsAt);

        await scenario.Dm.PatchSettingsAsync(scenario.CampaignId, new { timeZoneId = "America/New_York" });
        var inNewYork = await scenario.Player.GetSessionAsync(inSummer.Id);
        Assert.Equal("America/New_York", inNewYork.TimeZoneId);
        Assert.Equal("2026-10-10T14:00:00-04:00", inNewYork.StartsAtLocal);
    }

    [Fact]
    public async Task Create_validates_the_body()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var url = $"{scenario.Url}/sessions";
        var startsAt = FromNow(TimeSpan.FromDays(1));

        foreach (var body in new object[]
                 {
                     new { title = "", startsAt },
                     new { title = "   ", startsAt },
                     new { title = new string('x', 201), startsAt },
                     new { title = "T" },
                     new { title = "T", startsAt, durationMinutes = 0 },
                     new { title = "T", startsAt, durationMinutes = 24 * 60 + 1 },
                     new { title = "T", startsAt, location = new string('x', 201) },
                     new { title = "T", startsAt, notes = new string('x', 10_001) },
                 })
        {
            var response = await scenario.Dm.Client.PostAsJsonAsync(url, body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Patch_changes_only_the_given_fields_and_null_clears_the_optional_ones()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Original", durationMinutes: 120, location: "Taberna", notes: "Notas");

        var renamed = await scenario.Dm.PatchSessionAsync(session.Id, new { title = "Nuevo título" });
        Assert.Equal(("Nuevo título", 120, "Taberna", "Notas"), (renamed.Title, renamed.DurationMinutes, renamed.Location, renamed.Notes));

        var cleared = await scenario.Dm.PatchSessionAsync(session.Id, new { location = (string?)null, durationMinutes = (int?)null });
        Assert.Null(cleared.Location);
        Assert.Null(cleared.DurationMinutes);
        Assert.Equal("Notas", cleared.Notes);
        Assert.Equal(session.StartsAt, cleared.StartsAt);

        foreach (var body in new object[]
                 {
                     new { title = "" },
                     new { status = "Postponed" },
                     new { durationMinutes = 0 },
                     new { startsAt = default(DateTimeOffset) },
                 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Dm.Client.PatchAsJsonAsync($"/api/v1/sessions/{session.Id}", body)).StatusCode);
        }
    }

    [Fact]
    public async Task Rsvp_updates_the_answer_counts_and_comment()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);

        var asPlayer = await scenario.Player.RespondAsync(session.Id, "Yes", "  Llevo yo la comida ");
        Assert.Equal("Yes", asPlayer.MyRsvp);
        Assert.Equal(new RsvpCountsDto(1, 0, 0, 2), asPlayer.Counts);

        await scenario.Dm.RespondAsync(session.Id, "Maybe");
        var changed = await scenario.Player.RespondAsync(session.Id, "No");
        Assert.Equal("No", changed.MyRsvp);
        Assert.Equal(new RsvpCountsDto(0, 1, 1, 1), changed.Counts);

        // Every member sees every answer, with names; reminders are only for DMs.
        var seenByOwner = await scenario.Owner.GetSessionAsync(session.Id);
        Assert.Null(seenByOwner.MyRsvp);
        Assert.Equal(
            [("Dm User", "Maybe"), ("Player User", "No")],
            seenByOwner.Rsvps.Select(r => (r.DisplayName, r.Status)));
        Assert.NotNull(seenByOwner.Reminders);
        Assert.Null((await scenario.Player.GetSessionAsync(session.Id)).Reminders);

        // The comment survives only while sent: the last answer replaces it.
        Assert.Null(changed.Rsvps.Single(r => r.UserId == scenario.Player.Id).Comment);
        var commented = await scenario.Player.RespondAsync(session.Id, "Yes", "Llego tarde");
        Assert.Equal("Llego tarde", commented.Rsvps.Single(r => r.UserId == scenario.Player.Id).Comment);

        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Player.RespondRawAsync(session.Id, "Perhaps")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Player.RespondRawAsync(session.Id, "Yes", new string('x', 501))).StatusCode);
    }

    [Fact]
    public async Task Rsvp_is_rejected_on_cancelled_sessions_and_ignores_members_who_left()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);
        await scenario.Player.RespondAsync(session.Id, "Yes");

        await scenario.Owner.Client.DeleteAsync($"{scenario.Url}/members/{scenario.Player.Id}");
        var afterLeaving = await scenario.Dm.GetSessionAsync(session.Id);
        Assert.Empty(afterLeaving.Rsvps);
        Assert.Equal(new RsvpCountsDto(0, 0, 0, 2), afterLeaving.Counts);

        await scenario.Dm.PatchSessionAsync(session.Id, new { status = "Cancelled" });
        Assert.Equal(HttpStatusCode.Conflict, (await scenario.Dm.RespondRawAsync(session.Id, "Yes")).StatusCode);
    }

    [Fact]
    public async Task List_returns_upcoming_sessions_by_date_and_honors_includePast_from_and_to()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var past = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Pasada", FromNow(TimeSpan.FromDays(-10)));
        var soon = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Pronto", FromNow(TimeSpan.FromDays(2)));
        var later = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Después", FromNow(TimeSpan.FromDays(20)));
        var url = $"{scenario.Url}/sessions";

        async Task<List<Guid>> Ids(string query) =>
            (await scenario.Player.Client.GetFromJsonAsync<List<SessionDto>>($"{url}{query}"))!.Select(s => s.Id).ToList();

        Assert.Equal([soon.Id, later.Id], await Ids(""));
        Assert.Equal([soon.Id, later.Id], await Ids("?includePast=false"));
        Assert.Equal([past.Id, soon.Id, later.Id], await Ids("?includePast=true"));
        Assert.Equal([past.Id, soon.Id], await Ids($"?from={Query(FromNow(TimeSpan.FromDays(-30)))}&to={Query(FromNow(TimeSpan.FromDays(10)))}"));
        Assert.Equal([soon.Id, later.Id], await Ids($"?from={Query(FromNow(TimeSpan.FromDays(1)))}"));
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await scenario.Player.Client.GetAsync($"{url}?from={Query(FromNow(TimeSpan.FromDays(5)))}&to={Query(FromNow(TimeSpan.FromDays(1)))}")).StatusCode);
    }

    [Fact]
    public async Task Delete_removes_the_session_with_its_answers_and_reminders()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);
        await scenario.Player.RespondAsync(session.Id, "Yes");
        Assert.Equal(2, (await factory.RemindersOfAsync(session.Id)).Count);

        Assert.Equal(HttpStatusCode.NoContent, (await scenario.Dm.Client.DeleteAsync($"/api/v1/sessions/{session.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Dm.Client.GetAsync($"/api/v1/sessions/{session.Id}")).StatusCode);
        Assert.Empty(await factory.RemindersOfAsync(session.Id));
        await factory.WithDbAsync(async db => Assert.False(await db.SessionRsvps.AnyAsync(r => r.SessionId == session.Id)));
    }

    [Fact]
    public async Task Me_sessions_lists_upcoming_scheduled_sessions_of_my_campaigns_only()
    {
        var owner = await factory.CreateSignedInUserAsync("Owner User");
        var member = await factory.CreateSignedInUserAsync("Member User");
        var first = await owner.CreateCampaignAsync("Primera");
        await owner.AddMemberAsync(first.Id, member, "Player");
        var second = await member.CreateCampaignAsync("Segunda");
        var foreignOwner = await factory.CreateSignedInUserAsync();
        var foreignCampaign = await foreignOwner.CreateCampaignAsync("Otra ajena");

        var inFirst = await owner.CreateSessionAsync(first.Id, "En la primera", FromNow(TimeSpan.FromDays(4)));
        var inSecond = await member.CreateSessionAsync(second.Id, "En la segunda", FromNow(TimeSpan.FromDays(2)));
        await owner.CreateSessionAsync(first.Id, "Pasada", FromNow(TimeSpan.FromDays(-3)));
        var cancelled = await owner.CreateSessionAsync(first.Id, "Cancelada", FromNow(TimeSpan.FromDays(6)));
        await owner.PatchSessionAsync(cancelled.Id, new { status = "Cancelled" });
        await foreignOwner.CreateSessionAsync(foreignCampaign.Id, "De otra campaña", FromNow(TimeSpan.FromDays(1)));

        var mine = await member.Client.GetFromJsonAsync<List<SessionDto>>("/api/v1/me/sessions");

        Assert.Equal([inSecond.Id, inFirst.Id], mine!.Select(s => s.Id));
        Assert.Equal(["Segunda", "Primera"], mine!.Select(s => s.CampaignName));
        Assert.DoesNotContain(mine!, s => s.Title == "De otra campaña");

        var ranged = await member.Client.GetFromJsonAsync<List<SessionDto>>(
            $"/api/v1/me/sessions?from={Query(FromNow(TimeSpan.FromDays(3)))}&to={Query(FromNow(TimeSpan.FromDays(5)))}");
        Assert.Equal([inFirst.Id], ranged!.Select(s => s.Id));

        var stranger = await factory.CreateSignedInUserAsync();
        Assert.Empty((await stranger.Client.GetFromJsonAsync<List<SessionDto>>("/api/v1/me/sessions"))!);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/me/sessions")).StatusCode);
    }
}

internal static class RsvpTestExtensions
{
    public static Task<HttpResponseMessage> RespondRawAsync(this SignedInUser user, Guid sessionId, string status, string? comment = null) =>
        user.Client.PutAsJsonAsync($"/api/v1/sessions/{sessionId}/rsvp", new { status, comment });
}
