namespace Dnd.Domain.Sessions;

/// <summary>Lifecycle of a game session. Persisted as its name.</summary>
public enum SessionStatus
{
    Scheduled,
    Cancelled,
    Done,
}
