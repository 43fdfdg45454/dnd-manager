using OpenTrpg.Core.Application.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenTrpg.Core.Api.Hosting;

/// <summary>
/// Background loop that sends the session reminders that are due. All the logic lives in
/// <see cref="ReminderProcessor"/>; this class only wakes it up every <c>Reminders:PollSeconds</c>.
/// </summary>
internal sealed class ReminderDispatcher(
    IServiceScopeFactory scopes,
    IOptions<ReminderOptions> options,
    ILogger<ReminderDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Reminder dispatcher is disabled (Reminders:Enabled=false)");
            return;
        }

        var period = TimeSpan.FromSeconds(Math.Max(1, settings.PollSeconds));
        logger.LogInformation("Reminder dispatcher started, polling every {Seconds} s", period.TotalSeconds);

        using var timer = new PeriodicTimer(period);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ReminderProcessor>().ProcessDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The loop must survive a failed tick (for example the database being unavailable).
                logger.LogError(ex, "Reminder dispatcher tick failed");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
