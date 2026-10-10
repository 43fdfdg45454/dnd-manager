using OpenTrpg.Core.Domain.Sessions;

namespace OpenTrpg.Core.Domain.Tests.Sessions;

public class ReminderTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 10, 18, 0, 0, TimeSpan.Zero);

    private static Reminder NewReminder()
    {
        var session = GameSession.Create(Guid.NewGuid(), 1, "T", Start, null, null, null, Guid.NewGuid(), Start.AddDays(-5));
        session.SyncReminders([120], Start.AddDays(-5));
        return session.Reminders.Single();
    }

    [Fact]
    public void A_reminder_is_due_from_its_send_time()
    {
        var reminder = NewReminder();

        Assert.False(reminder.IsDue(Start.AddHours(-2).AddSeconds(-1)));
        Assert.True(reminder.IsDue(Start.AddHours(-2)));
        Assert.True(reminder.IsDue(Start));
    }

    [Fact]
    public void A_sent_reminder_is_never_due_again()
    {
        var reminder = NewReminder();

        reminder.MarkSent(Start.AddHours(-2));

        Assert.False(reminder.IsPending);
        Assert.False(reminder.IsDue(Start));
    }

    [Fact]
    public void Failures_delay_the_retry_by_five_minutes_and_the_third_one_abandons_the_reminder()
    {
        var reminder = NewReminder();
        var first = Start.AddHours(-2);

        reminder.RegisterFailure(first, "SMTP caído");
        Assert.Equal((1, "SMTP caído", null), (reminder.Attempts, reminder.LastError, reminder.FailedAt));
        Assert.False(reminder.IsDue(first.AddMinutes(4).AddSeconds(59)));
        Assert.True(reminder.IsDue(first.AddMinutes(5)));

        reminder.RegisterFailure(first.AddMinutes(5), "otra vez");
        Assert.Equal((2, null), (reminder.Attempts, reminder.FailedAt));
        Assert.True(reminder.IsDue(first.AddMinutes(10)));

        reminder.RegisterFailure(first.AddMinutes(10), "tercera");
        Assert.Equal((Reminder.MaxAttempts, first.AddMinutes(10)), (reminder.Attempts, reminder.FailedAt));
        Assert.False(reminder.IsPending);
        Assert.False(reminder.IsDue(first.AddDays(1)));
    }

    [Fact]
    public void The_error_text_is_truncated()
    {
        var reminder = NewReminder();

        reminder.RegisterFailure(Start, new string('x', Reminder.LastErrorMaxLength + 100));

        Assert.Equal(Reminder.LastErrorMaxLength, reminder.LastError!.Length);
    }
}
