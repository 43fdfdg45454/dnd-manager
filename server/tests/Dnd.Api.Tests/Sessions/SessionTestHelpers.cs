using System.Net;
using System.Net.Http.Json;
using Dnd.Application.Campaigns;
using Dnd.Application.Sessions;
using Dnd.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dnd.Api.Tests.Sessions;

internal static class SessionTestHelpers
{
    /// <summary>An instant in the future, truncated to whole minutes so JSON and database round trips are exact.</summary>
    public static DateTimeOffset FromNow(TimeSpan offset)
    {
        var instant = DateTimeOffset.UtcNow + offset;
        return new DateTimeOffset(instant.Year, instant.Month, instant.Day, instant.Hour, instant.Minute, 0, TimeSpan.Zero);
    }

    public static async Task<HttpResponseMessage> PostSessionAsync(
        this SignedInUser user,
        Guid campaignId,
        string title,
        DateTimeOffset startsAt,
        int? durationMinutes = null,
        string? location = null,
        string? notes = null) =>
        await user.Client.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/sessions", new { title, startsAt, durationMinutes, location, notes });

    public static async Task<SessionDto> CreateSessionAsync(
        this SignedInUser user,
        Guid campaignId,
        string title = "La mina perdida",
        DateTimeOffset? startsAt = null,
        int? durationMinutes = null,
        string? location = null,
        string? notes = null)
    {
        var response = await user.PostSessionAsync(campaignId, title, startsAt ?? FromNow(TimeSpan.FromDays(3)), durationMinutes, location, notes);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionDto>())!;
    }

    public static async Task<SessionDto> GetSessionAsync(this SignedInUser user, Guid sessionId)
    {
        var response = await user.Client.GetAsync($"/api/v1/sessions/{sessionId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionDto>())!;
    }

    public static async Task<SessionDto> PatchSessionAsync(this SignedInUser user, Guid sessionId, object body)
    {
        var response = await user.Client.PatchAsJsonAsync($"/api/v1/sessions/{sessionId}", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionDto>())!;
    }

    public static async Task<SessionDto> RespondAsync(this SignedInUser user, Guid sessionId, string status, string? comment = null)
    {
        var response = await user.Client.PutAsJsonAsync($"/api/v1/sessions/{sessionId}/rsvp", new { status, comment });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessionDto>())!;
    }

    public static async Task<CampaignDto> PatchSettingsAsync(this SignedInUser user, Guid campaignId, object body)
    {
        var response = await user.Client.PatchAsJsonAsync($"/api/v1/campaigns/{campaignId}/settings", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CampaignDto>())!;
    }

    /// <summary>Reminders stored for the session (all, sent or not), ordered by send time.</summary>
    public static async Task<List<Reminder>> RemindersOfAsync(this ApiFactory factory, Guid sessionId)
    {
        var reminders = new List<Reminder>();
        await factory.WithDbAsync(async db => reminders = await db.Reminders.AsNoTracking().Where(r => r.SessionId == sessionId).ToListAsync());
        return reminders.OrderBy(r => r.SendAt).ToList();
    }

    /// <summary>Runs the reminder processor as the dispatcher would at <paramref name="now"/>.</summary>
    public static async Task<int> ProcessRemindersAsync(this ApiFactory factory, DateTimeOffset now)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReminderProcessor>().ProcessDueAsync(now);
    }

    public static async Task SetNotificationsAsync(this SignedInUser user, bool enabled)
    {
        var response = await user.Client.PatchAsJsonAsync("/api/v1/auth/me", new { notificationsEnabled = enabled });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static string Query(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("o"));
}
