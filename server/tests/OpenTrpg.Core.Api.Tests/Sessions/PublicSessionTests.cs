using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Sessions;
using Microsoft.Extensions.DependencyInjection;
using static OpenTrpg.Core.Api.Tests.Sessions.SessionTestHelpers;

namespace OpenTrpg.Core.Api.Tests.Sessions;

public sealed class PublicSessionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private ISessionLinkTokens Tokens => factory.Services.GetRequiredService<ISessionLinkTokens>();

    private static string Url(Guid sessionId, string? token) =>
        $"/api/v1/public/sessions/{sessionId}{(token is null ? string.Empty : $"?token={Uri.EscapeDataString(token)}")}";

    private async Task<HttpResponseMessage> RsvpAsync(Guid sessionId, string? token, string status) =>
        await factory.CreateClient().PostAsJsonAsync(
            $"/api/v1/public/sessions/{sessionId}/rsvp{(token is null ? string.Empty : $"?token={Uri.EscapeDataString(token)}")}",
            new { status });

    [Fact]
    public async Task The_link_of_a_reminder_email_shows_the_session_and_answers_attendance_without_signing_in()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var startsAt = FromNow(TimeSpan.FromDays(3));
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "La mina perdida", startsAt, 120, "Taberna del Dragón", "Traed dados");
        await factory.ProcessRemindersAsync(startsAt.AddHours(-24).AddMinutes(1));
        var (sessionId, token, _) = FakeEmailSender.ExtractSessionLink(factory.Emails.LastSentTo(scenario.Player.Email));
        var anonymous = factory.CreateClient();

        var view = await anonymous.GetFromJsonAsync<PublicSessionDto>(Url(sessionId, token));

        Assert.Equal((session.Id, 1, "La mina perdida", "Taberna del Dragón", "Traed dados"), (view!.Id, view.Number, view.Title, view.Location, view.Notes));
        Assert.Equal((startsAt, session.StartsAtLocal, "Europe/Madrid", 120), (view.StartsAt, view.StartsAtLocal, view.TimeZoneId, view.DurationMinutes));
        Assert.Equal(("Player User", null, "Scheduled"), (view.UserDisplayName, view.MyRsvp, view.Status));

        var answered = await RsvpAsync(sessionId, token, "Maybe");

        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        Assert.Equal("Maybe", (await answered.Content.ReadFromJsonAsync<PublicSessionDto>())!.MyRsvp);
        var seenInApp = await scenario.Dm.GetSessionAsync(session.Id);
        Assert.Equal([("Player User", "Maybe")], seenInApp.Rsvps.Select(r => (r.DisplayName, r.Status)));
        Assert.Equal("Maybe", (await scenario.Player.GetSessionAsync(session.Id)).MyRsvp);
    }

    [Fact]
    public async Task A_valid_token_answers_on_behalf_of_its_user_and_keeps_the_comment()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);
        await scenario.Player.RespondAsync(session.Id, "Yes", "Llego tarde");

        var response = await RsvpAsync(session.Id, Tokens.Create(session.Id, scenario.Player.Id), "No");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = (await response.Content.ReadFromJsonAsync<PublicSessionDto>())!;
        Assert.Equal(("No", "Llego tarde"), (view.MyRsvp, view.MyComment));
        var other = await scenario.Dm.GetSessionAsync(session.Id);
        Assert.Equal(new RsvpCountsDto(0, 1, 0, 2), other.Counts);
    }

    [Fact]
    public async Task A_manipulated_or_foreign_token_gets_401()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);
        var other = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Otra");
        var valid = Tokens.Create(session.Id, scenario.Player.Id);
        var signature = valid[(valid.IndexOf('.') + 1)..];
        var flipped = signature[..^1] + (signature[^1] == 'A' ? 'B' : 'A');

        var invalid = new[]
        {
            $"{scenario.Player.Id:N}.{flipped}",                       // tampered signature
            $"{scenario.Dm.Id:N}.{signature}",                          // another user with the player's signature
            Tokens.Create(other.Id, scenario.Player.Id),                // token of another session
            valid + "x",
            valid[..^3],
            signature,
            $"{scenario.Player.Id:N}.",
            "abc",
            string.Empty,
        };

        foreach (var token in invalid)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Url(session.Id, token))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await RsvpAsync(session.Id, token, "Yes")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Url(session.Id, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await RsvpAsync(session.Id, null, "Yes")).StatusCode);
        Assert.Empty((await scenario.Dm.GetSessionAsync(session.Id)).Rsvps);
    }

    [Fact]
    public async Task The_token_stops_working_when_the_user_leaves_the_campaign_or_is_deactivated_or_the_session_is_gone()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);
        var player = Tokens.Create(session.Id, scenario.Player.Id);
        var dm = Tokens.Create(session.Id, scenario.Dm.Id);
        var outsider = Tokens.Create(session.Id, scenario.Outsider.Id);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Url(session.Id, player))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url(session.Id, outsider))).StatusCode);

        await scenario.Owner.Client.DeleteAsync($"{scenario.Url}/members/{scenario.Player.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url(session.Id, player))).StatusCode);

        await factory.SetUserActiveAsync(scenario.Dm.Id, false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url(session.Id, dm))).StatusCode);

        await scenario.Owner.Client.DeleteAsync($"/api/v1/sessions/{session.Id}");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(Url(session.Id, Tokens.Create(session.Id, scenario.Owner.Id)))).StatusCode);
    }

    [Fact]
    public async Task The_public_rsvp_validates_the_status_and_cancelled_sessions_reject_it()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);
        var token = Tokens.Create(session.Id, scenario.Player.Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await RsvpAsync(session.Id, token, "Perhaps")).StatusCode);

        await scenario.Dm.PatchSessionAsync(session.Id, new { status = "Cancelled" });
        var view = await factory.CreateClient().GetFromJsonAsync<PublicSessionDto>(Url(session.Id, token));
        Assert.Equal("Cancelled", view!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await RsvpAsync(session.Id, token, "Yes")).StatusCode);
    }

    [Fact]
    public async Task The_public_page_is_served_as_uncached_html_that_calls_the_public_api()
    {
        var response = await factory.CreateClient().GetAsync($"/sessions/{Guid.NewGuid()}?token=abc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("no-referrer", response.Headers.GetValues("Referrer-Policy"));
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/public/sessions/", html);
        Assert.Contains("lang=\"es\"", html);
        Assert.Contains("Sí", html);
        Assert.Contains("Quizá", html);

        Assert.Equal(HttpStatusCode.NotFound, (await factory.CreateClient().GetAsync("/sessions/not-a-guid")).StatusCode);
    }

    [Fact]
    public void Tokens_are_bound_to_the_session_and_the_user()
    {
        var session = Guid.NewGuid();
        var user = Guid.NewGuid();

        var token = Tokens.Create(session, user);

        Assert.True(Tokens.TryValidate(session, token, out var validated));
        Assert.Equal(user, validated);
        Assert.False(Tokens.TryValidate(Guid.NewGuid(), token, out _));
        Assert.Equal(token, Tokens.Create(session, user));
        Assert.NotEqual(token, Tokens.Create(session, Guid.NewGuid()));
        Assert.Matches(@"^[0-9a-f]{32}\.[A-Za-z0-9_-]{43}$", token);
    }
}
