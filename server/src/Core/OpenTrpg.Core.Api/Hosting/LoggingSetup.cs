using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace OpenTrpg.Core.Api.Hosting;

/// <summary>
/// Serilog configuration. Production writes compact JSON (one event per line) to the console so the
/// container log driver can collect it; Development keeps a readable text output.
/// </summary>
/// <remarks>
/// The minimum level follows <c>Serilog:MinimumLevel:Default</c> when set and the standard
/// <c>Logging:LogLevel</c> section otherwise (so <c>Logging__LogLevel__Default</c> in docker compose
/// keeps working). Per-category levels come from <c>Logging:LogLevel:&lt;Category&gt;</c> and can be
/// overridden with <c>Serilog:MinimumLevel:Override</c>.
/// </remarks>
public static class LoggingSetup
{
    public const string ApplicationName = "dnd-companion-api";

    private const string DevelopmentTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    public static void Configure(HostBuilderContext context, LoggerConfiguration logger)
    {
        var configuration = context.Configuration;

        logger.MinimumLevel.Is(ResolveDefaultLevel(configuration));
        foreach (var (category, level) in ResolveCategoryLevels(configuration))
        {
            logger.MinimumLevel.Override(category, level);
        }

        // Explicit Serilog:MinimumLevel:Override entries win over the Logging section.
        logger.ReadFrom.Configuration(configuration);

        logger.Enrich.FromLogContext().Enrich.WithProperty("Application", ApplicationName);

        if (context.HostingEnvironment.IsDevelopment())
        {
            logger.WriteTo.Console(outputTemplate: DevelopmentTemplate);
        }
        else
        {
            logger.WriteTo.Console(new RenderedCompactJsonFormatter());
        }
    }

    /// <summary><c>Serilog:MinimumLevel:Default</c>, else <c>Logging:LogLevel:Default</c>, else Information.</summary>
    public static LogEventLevel ResolveDefaultLevel(IConfiguration configuration) =>
        ParseLevel(configuration["Serilog:MinimumLevel:Default"])
        ?? ParseLevel(configuration["Logging:LogLevel:Default"])
        ?? LogEventLevel.Information;

    /// <summary>Levels of the categories listed in <c>Logging:LogLevel</c> (everything but <c>Default</c>).</summary>
    public static IReadOnlyDictionary<string, LogEventLevel> ResolveCategoryLevels(IConfiguration configuration)
    {
        var levels = new Dictionary<string, LogEventLevel>(StringComparer.Ordinal);
        foreach (var entry in configuration.GetSection("Logging:LogLevel").GetChildren())
        {
            if (entry.Key != "Default" && ParseLevel(entry.Value) is { } level)
            {
                levels[entry.Key] = level;
            }
        }

        return levels;
    }

    /// <summary>
    /// Maps a Microsoft.Extensions.Logging or Serilog level name (case-insensitive) to a Serilog level.
    /// "None" has no equivalent and maps to Fatal, the least verbose one. Null for unknown names.
    /// </summary>
    public static LogEventLevel? ParseLevel(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "trace" or "verbose" => LogEventLevel.Verbose,
        "debug" => LogEventLevel.Debug,
        "information" => LogEventLevel.Information,
        "warning" => LogEventLevel.Warning,
        "error" => LogEventLevel.Error,
        "critical" or "fatal" or "none" => LogEventLevel.Fatal,
        _ => null,
    };

    /// <summary>
    /// Adds <c>RequestId</c> and <c>TraceId</c> to every event written while a request is handled, so
    /// the lines of one request (and the error body returned to the client) can be correlated.
    /// </summary>
    public static IApplicationBuilder UseRequestCorrelation(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            using (Serilog.Context.LogContext.PushProperty("RequestId", context.TraceIdentifier))
            using (Serilog.Context.LogContext.PushProperty("TraceId", System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier))
            {
                await next(context);
            }
        });

    /// <summary>Health probes run every few seconds: keep them out of the log unless the level is Verbose.</summary>
    public static LogEventLevel RequestLevel(HttpContext context, double elapsedMs, Exception? exception)
    {
        if (exception is not null || context.Response.StatusCode >= 500)
        {
            return LogEventLevel.Error;
        }

        return context.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Verbose : LogEventLevel.Information;
    }
}
