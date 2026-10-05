namespace Dnd.Application.Abstractions.Persistence;

/// <summary>Row counts of the instance, for the admin dashboard.</summary>
/// <param name="ScheduledSessions">Sessions in status Scheduled that have not started yet.</param>
/// <param name="FilesBytes">Sum of the sizes of every stored file.</param>
public sealed record InstanceCounts(
    int UsersTotal,
    int UsersActive,
    int Campaigns,
    int Characters,
    int ScheduledSessions,
    int Files,
    long FilesBytes);

public interface IInstanceStatsRepository
{
    Task<InstanceCounts> GetCountsAsync(DateTimeOffset now, CancellationToken cancellationToken = default);
}
