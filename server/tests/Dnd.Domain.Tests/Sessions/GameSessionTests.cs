using Dnd.Domain.Common;
using Dnd.Domain.Sessions;

namespace Dnd.Domain.Tests.Sessions;

public class GameSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Start = new(2026, 10, 10, 18, 0, 0, TimeSpan.Zero);

    private static GameSession NewSession(DateTimeOffset? startsAt = null) =>
        GameSession.Create(Guid.NewGuid(), 1, " Título ", startsAt ?? Start, 180, " Taberna ", " Notas ", Guid.NewGuid(), Now);

    [Fact]
    public void Create_normalizes_the_fields_and_stores_the_start_in_utc()
    {
        var session = GameSession.Create(Guid.NewGuid(), 4, " Título ", new DateTimeOffset(2026, 10, 10, 20, 0, 0, TimeSpan.FromHours(2)), null, "  ", null, Guid.NewGuid(), Now);

        Assert.Equal((4, "Título", SessionStatus.Scheduled), (session.Number, session.Title, session.Status));
        Assert.Equal(Start, session.StartsAt);
        Assert.Equal(TimeSpan.Zero, session.StartsAt.Offset);
        Assert.Null(session.Location);
        Assert.Null(session.SummaryMarkdown);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_titles(string title) =>
        Assert.Equal(
            DomainErrorKind.RuleViolation,
            Assert.Throws<DomainException>(() => GameSession.Create(Guid.NewGuid(), 1, title, Start, null, null, null, Guid.NewGuid(), Now)).Kind);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(GameSession.MaxDurationMinutes + 1)]
    public void Duration_must_be_between_one_minute_and_a_day(int minutes) =>
        Assert.Throws<DomainException>(() => NewSession().SetDuration(minutes, Now));

    [Fact]
    public void SyncReminders_creates_one_reminder_per_future_offset()
    {
        var session = NewSession();

        var (removed, added) = session.SyncReminders([120, 1440], Now);

        Assert.Empty(removed);
        Assert.Equal([(1440, Start.AddHours(-24)), (120, Start.AddHours(-2))], added.Select(r => (r.OffsetMinutes, r.SendAt)));
        Assert.Equal(2, session.Reminders.Count);
        Assert.All(session.Reminders, r => Assert.True(r.IsPending));
    }

    [Fact]
    public void SyncReminders_skips_offsets_whose_send_time_has_passed()
    {
        var session = NewSession(startsAt: Now.AddHours(3));

        var (_, added) = session.SyncReminders([1440, 120], Now);

        Assert.Equal([120], added.Select(r => r.OffsetMinutes));
        Assert.Empty(NewSession(startsAt: Now.AddMinutes(30)).SyncReminders([1440, 120], Now).Added);
    }

    [Fact]
    public void SyncReminders_replaces_the_pending_ones_and_keeps_the_sent_ones()
    {
        var session = NewSession();
        session.SyncReminders([1440, 120], Now);
        var sent = session.Reminders.First(r => r.OffsetMinutes == 1440);
        sent.MarkSent(Start.AddHours(-24));
        var pending = session.Reminders.First(r => r.OffsetMinutes == 120);

        session.Reschedule(Start.AddDays(7), Now);
        var (removed, added) = session.SyncReminders([1440, 120], Now);

        Assert.Equal([pending], removed);
        Assert.Equal(2, added.Count);
        Assert.Equal(3, session.Reminders.Count);
        Assert.Contains(sent, session.Reminders);
        Assert.DoesNotContain(pending, session.Reminders);
    }

    [Fact]
    public void SyncReminders_creates_nothing_unless_the_session_is_scheduled()
    {
        var session = NewSession();
        session.SyncReminders([1440, 120], Now);

        session.SetStatus(SessionStatus.Cancelled, Now);
        var (removed, added) = session.SyncReminders([1440, 120], Now);

        Assert.Equal(2, removed.Count);
        Assert.Empty(added);
        Assert.Empty(session.Reminders);
    }

    [Fact]
    public void SetSummary_stores_the_text_and_clears_it_when_blank()
    {
        var session = NewSession();

        session.SetSummary("Relato", Now.AddDays(1));
        Assert.Equal(("Relato", Now.AddDays(1)), (session.SummaryMarkdown, session.SummaryUpdatedAt));

        session.SetSummary("  \n", Now.AddDays(2));
        Assert.Null(session.SummaryMarkdown);
        Assert.Null(session.SummaryUpdatedAt);

        Assert.Throws<DomainException>(() => session.SetSummary(new string('x', GameSession.SummaryMaxLength + 1), Now));
    }

    [Fact]
    public void Respond_creates_then_updates_the_single_answer_of_a_user()
    {
        var session = NewSession();
        var user = Guid.NewGuid();

        session.Respond(user, RsvpStatus.Yes, " Llego tarde ", Now);
        session.Respond(user, RsvpStatus.Maybe, null, Now.AddHours(1));

        var rsvp = Assert.Single(session.Rsvps);
        Assert.Equal((user, RsvpStatus.Maybe, null), (rsvp.UserId, rsvp.Status, rsvp.Comment));
        Assert.Equal(Now.AddHours(1), rsvp.UpdatedAt);
    }

    [Fact]
    public void Respond_is_a_conflict_on_cancelled_sessions()
    {
        var session = NewSession();
        session.SetStatus(SessionStatus.Cancelled, Now);

        Assert.Equal(DomainErrorKind.Conflict, Assert.Throws<DomainException>(() => session.Respond(Guid.NewGuid(), RsvpStatus.Yes, null, Now)).Kind);
    }

    [Theory]
    [InlineData(-60, true)]
    [InlineData(239, true)]
    [InlineData(241, false)]
    public void A_session_without_duration_counts_as_in_progress_for_four_hours(int minutesAfterStart, bool expected)
    {
        var session = GameSession.Create(Guid.NewGuid(), 1, "T", Start, null, null, null, Guid.NewGuid(), Now);

        Assert.Equal(expected, session.IsUpcomingOrInProgress(Start.AddMinutes(minutesAfterStart)));
    }
}
