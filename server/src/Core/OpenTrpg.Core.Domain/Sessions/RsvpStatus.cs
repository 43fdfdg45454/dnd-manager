namespace OpenTrpg.Core.Domain.Sessions;

/// <summary>Answer of a member to a session. Persisted as its name. No answer is "pending" (no row).</summary>
public enum RsvpStatus
{
    Yes,
    No,
    Maybe,
}
