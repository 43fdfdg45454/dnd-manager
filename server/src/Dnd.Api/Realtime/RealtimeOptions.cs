namespace Dnd.Api.Realtime;

/// <summary>Limits of the realtime hub (section <c>Realtime</c>).</summary>
public sealed class RealtimeOptions
{
    public const string SectionName = "Realtime";

    /// <summary>Simultaneous hub connections per user; the next one is refused (<c>Realtime:MaxConnectionsPerUser</c>).</summary>
    public int MaxConnectionsPerUser { get; set; } = 5;
}
