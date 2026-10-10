using System.Net;
using System.Net.Http.Json;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Sessions;
using static OpenTrpg.Core.Api.Tests.Sessions.SessionTestHelpers;

namespace OpenTrpg.Core.Api.Tests.Sessions;

public sealed class JournalTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static async Task<SessionDto> SetSummaryAsync(SignedInUser user, Guid sessionId, string? summary)
    {
        var response = await user.Client.PutAsJsonAsync($"/api/v1/sessions/{sessionId}/summary", new { summaryMarkdown = summary });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionDto>())!;
    }

    private static async Task<PagedResult<SessionSummaryDto>> JournalAsync(SignedInUser user, Guid campaignId, string query = "")
    {
        var response = await user.Client.GetAsync($"/api/v1/campaigns/{campaignId}/journal{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PagedResult<SessionSummaryDto>>())!;
    }

    [Fact]
    public async Task A_dm_writes_the_summary_and_everybody_reads_it()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "La mina perdida");

        var saved = await SetSummaryAsync(scenario.Dm, session.Id, "# Crónica\nLlegaron a la mina.");

        Assert.Equal("# Crónica\nLlegaron a la mina.", saved.SummaryMarkdown);
        Assert.NotNull(saved.SummaryUpdatedAt);
        var seenByPlayer = await scenario.Player.GetSessionAsync(session.Id);
        Assert.Equal(saved.SummaryMarkdown, seenByPlayer.SummaryMarkdown);
        Assert.Equal(saved.SummaryUpdatedAt, seenByPlayer.SummaryUpdatedAt);

        // The owner is a DM too, and the summary can be written before or after the session.
        Assert.Equal("Otro", (await SetSummaryAsync(scenario.Owner, session.Id, "Otro")).SummaryMarkdown);
    }

    [Fact]
    public async Task Players_and_outsiders_cannot_edit_the_summary()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);
        await SetSummaryAsync(scenario.Dm, session.Id, "Original");
        var url = $"/api/v1/sessions/{session.Id}/summary";

        Assert.Equal(HttpStatusCode.Forbidden, (await scenario.Player.Client.PutAsJsonAsync(url, new { summaryMarkdown = "Cambiado" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.PutAsJsonAsync(url, new { summaryMarkdown = "Cambiado" })).StatusCode);
        Assert.Equal("Original", (await scenario.Player.GetSessionAsync(session.Id)).SummaryMarkdown);
    }

    [Fact]
    public async Task An_empty_summary_removes_the_text_and_its_timestamp()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId);

        foreach (var empty in new string?[] { "", "   \n ", null })
        {
            Assert.NotNull((await SetSummaryAsync(scenario.Dm, session.Id, "Algo ocurrió")).SummaryUpdatedAt);

            var cleared = await SetSummaryAsync(scenario.Dm, session.Id, empty);

            Assert.Null(cleared.SummaryMarkdown);
            Assert.Null(cleared.SummaryUpdatedAt);
            var stored = await scenario.Player.GetSessionAsync(session.Id);
            Assert.Null(stored.SummaryMarkdown);
            Assert.Null(stored.SummaryUpdatedAt);
        }

        Assert.Empty((await JournalAsync(scenario.Dm, scenario.CampaignId)).Items);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await scenario.Dm.Client.PutAsJsonAsync($"/api/v1/sessions/{session.Id}/summary", new { summaryMarkdown = new string('x', 100_001) })).StatusCode);
    }

    [Fact]
    public async Task The_journal_lists_summaries_chronologically_and_skips_sessions_without_one()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var third = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Tercera por fecha", FromNow(TimeSpan.FromDays(-1)));
        var first = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Primera por fecha", FromNow(TimeSpan.FromDays(-20)));
        var second = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Segunda por fecha", FromNow(TimeSpan.FromDays(-10)));
        await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Sin resumen", FromNow(TimeSpan.FromDays(-5)));
        await SetSummaryAsync(scenario.Dm, third.Id, "C");
        await SetSummaryAsync(scenario.Dm, first.Id, "A");
        await SetSummaryAsync(scenario.Dm, second.Id, "B");

        var journal = await JournalAsync(scenario.Player, scenario.CampaignId);

        Assert.Equal(["A", "B", "C"], journal.Items.Select(i => i.SummaryMarkdown));
        Assert.Equal([2, 3, 1], journal.Items.Select(i => i.Number));
        Assert.Equal((3, 1, 20), (journal.Total, journal.Page, journal.PageSize));
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d[+-]0[12]:00$", journal.Items[0].StartsAtLocal);
        Assert.NotNull(journal.Items[0].SummaryUpdatedAt);
    }

    [Fact]
    public async Task The_journal_hides_cancelled_sessions_from_players_but_not_from_dms()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var played = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Jugada", FromNow(TimeSpan.FromDays(-10)));
        var cancelled = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, "Cancelada", FromNow(TimeSpan.FromDays(-5)));
        await SetSummaryAsync(scenario.Dm, played.Id, "Se jugó");
        await SetSummaryAsync(scenario.Dm, cancelled.Id, "Nunca ocurrió");
        await scenario.Dm.PatchSessionAsync(cancelled.Id, new { status = "Cancelled" });

        var forPlayer = await JournalAsync(scenario.Player, scenario.CampaignId);
        var forDm = await JournalAsync(scenario.Dm, scenario.CampaignId);
        var forOwner = await JournalAsync(scenario.Owner, scenario.CampaignId);

        Assert.Equal([played.Id], forPlayer.Items.Select(i => i.Id));
        Assert.Equal(1, forPlayer.Total);
        Assert.Equal([played.Id, cancelled.Id], forDm.Items.Select(i => i.Id));
        Assert.Equal("Cancelled", forDm.Items[1].Status);
        Assert.Equal(2, forOwner.Total);
    }

    [Fact]
    public async Task The_journal_is_paged_and_only_for_members()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        for (var i = 0; i < 5; i++)
        {
            var session = await scenario.Dm.CreateSessionAsync(scenario.CampaignId, $"Sesión {i}", FromNow(TimeSpan.FromDays(-30 + i)));
            await SetSummaryAsync(scenario.Dm, session.Id, $"Resumen {i}");
        }

        var firstPage = await JournalAsync(scenario.Player, scenario.CampaignId, "?page=1&pageSize=2");
        var lastPage = await JournalAsync(scenario.Player, scenario.CampaignId, "?page=3&pageSize=2");
        var beyond = await JournalAsync(scenario.Player, scenario.CampaignId, "?page=9&pageSize=2");

        Assert.Equal(["Resumen 0", "Resumen 1"], firstPage.Items.Select(i => i.SummaryMarkdown));
        Assert.Equal(["Resumen 4"], lastPage.Items.Select(i => i.SummaryMarkdown));
        Assert.Equal((5, 3, 2), (firstPage.Total, lastPage.Page, lastPage.PageSize));
        Assert.Empty(beyond.Items);

        foreach (var query in new[] { "?page=0", "?pageSize=0", "?pageSize=101" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await scenario.Player.Client.GetAsync($"{scenario.Url}/journal{query}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await scenario.Outsider.Client.GetAsync($"{scenario.Url}/journal")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"{scenario.Url}/journal")).StatusCode);
    }
}
