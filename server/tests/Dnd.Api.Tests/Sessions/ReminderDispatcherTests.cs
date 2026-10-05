using Dnd.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using static Dnd.Api.Tests.Sessions.SessionTestHelpers;

namespace Dnd.Api.Tests.Sessions;

/// <summary>Factory whose background <c>ReminderDispatcher</c> runs, polling every second.</summary>
public sealed class DispatcherApiFactory : ApiFactory
{
    protected override bool RunReminderDispatcher => true;
}

public sealed class ReminderDispatcherTests(DispatcherApiFactory factory) : IClassFixture<DispatcherApiFactory>
{
    [Fact]
    public async Task The_background_dispatcher_sends_due_reminders_on_its_own()
    {
        var scenario = await factory.CreateCampaignScenarioAsync();
        var now = DateTimeOffset.UtcNow;
        var session = GameSession.Create(scenario.CampaignId, 1, "La mina perdida", now.AddHours(1), null, null, null, scenario.Dm.Id, now);

        // A reminder that became due thirty minutes ago (generated "in the past").
        session.SyncReminders([90], now.AddHours(-2));
        Assert.Single(session.Reminders);
        await factory.WithDbAsync(async db =>
        {
            db.GameSessions.Add(session);
            await db.SaveChangesAsync();
        });

        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (DateTimeOffset.UtcNow < deadline && factory.Emails.SentTo(scenario.Player.Email).Count == 0)
        {
            await Task.Delay(200);
        }

        var email = Assert.Single(factory.Emails.SentTo(scenario.Player.Email));
        Assert.StartsWith("Recordatorio: La mina perdida — ", email.Subject);
        Assert.Single(factory.Emails.SentTo(scenario.Dm.Email));
        Assert.Single(factory.Emails.SentTo(scenario.Owner.Email));

        var stored = await factory.RemindersOfAsync(session.Id);
        Assert.NotNull(Assert.Single(stored).SentAt);

        // It does not send the same reminder again on later ticks.
        await Task.Delay(2500);
        Assert.Single(factory.Emails.SentTo(scenario.Player.Email));
        await factory.WithDbAsync(async db => Assert.Equal(1, await db.Reminders.CountAsync(r => r.SessionId == session.Id)));
    }
}
