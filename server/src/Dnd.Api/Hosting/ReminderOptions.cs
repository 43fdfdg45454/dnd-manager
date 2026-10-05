namespace Dnd.Api.Hosting;

public sealed class ReminderOptions
{
    public const string SectionName = "Reminders";

    /// <summary>Run the <see cref="ReminderDispatcher"/>. Turned off in tests, which call the processor directly.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Seconds between two checks for due reminders.</summary>
    public int PollSeconds { get; set; } = 60;
}
