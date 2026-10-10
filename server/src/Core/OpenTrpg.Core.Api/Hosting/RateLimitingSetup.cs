using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using OpenTrpg.Core.Domain.Users;
using OpenTrpg.Core.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace OpenTrpg.Core.Api.Hosting;

/// <summary>A fixed window: at most <see cref="PermitLimit"/> requests every <see cref="WindowSeconds"/>.</summary>
public sealed class FixedWindowLimit
{
    public int PermitLimit { get; set; }

    public int WindowSeconds { get; set; } = 60;

    public FixedWindowRateLimiterOptions ToLimiterOptions() => new()
    {
        PermitLimit = PermitLimit,
        Window = TimeSpan.FromSeconds(WindowSeconds),
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}

/// <summary>Rate limits (section <c>RateLimits</c>); every limit is a fixed window without queue.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    /// <summary>Connection attempts to the hubs (negotiate and connect) per user, or per IP when anonymous.</summary>
    public FixedWindowLimit Hub { get; set; } = new() { PermitLimit = 30 };

    /// <summary><c>POST /api/v1/auth/login</c> per client IP (forwarded by the operator's proxy).</summary>
    public FixedWindowLimit Login { get; set; } = new() { PermitLimit = 10 };

    /// <summary><c>POST /api/v1/auth/login</c> per account (normalized email): cannot be dodged by changing IP.</summary>
    public FixedWindowLimit LoginAccount { get; set; } = new() { PermitLimit = 10 };

    public IEnumerable<FixedWindowLimit> All => [Hub, Login, LoginAccount];
}

/// <summary>
/// Rate limiting with <c>Microsoft.AspNetCore.RateLimiting</c>: policy <see cref="HubPolicy"/> on the
/// hubs, <see cref="LoginPolicy"/> and the per-account <see cref="LoginAccountRateLimiter"/> on login.
/// Rejections are <c>429</c> ProblemDetails with <c>Retry-After</c>.
/// </summary>
public static class RateLimitingSetup
{
    public const string HubPolicy = "hub";

    public const string LoginPolicy = "login";

    public const string TooManyRequestsTitle = "Demasiadas peticiones.";

    public const string TooManyRequestsDetail = "Has hecho demasiadas peticiones seguidas. Espera un momento y vuelve a intentarlo.";

    /// <summary>Query parameter that identifies an established SignalR connection (polls, sends, close).</summary>
    private const string ConnectionIdQueryParameter = "id";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName))
            .Validate(o => o.All.All(l => l.PermitLimit >= 1 && l.WindowSeconds >= 1), "RateLimits: every PermitLimit and WindowSeconds must be at least 1.")
            .ValidateOnStart();
        services.AddSingleton<LoginAccountRateLimiter>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) => WriteTooManyRequestsAsync(context.HttpContext, context.Lease);

            options.AddPolicy(HubPolicy, context =>
            {
                // Only new connections count: a long-polling client polls again after every event and
                // would otherwise be throttled in normal use. Negotiate and the WebSocket/SSE connect
                // request are counted; the polls and sends of an established connection are not.
                if (IsEstablishedConnectionTraffic(context))
                {
                    return RateLimitPartition.GetNoLimiter("hub:established");
                }

                var limit = Limits(context).Hub;
                return RateLimitPartition.GetFixedWindowLimiter($"hub:{UserOrIp(context)}", _ => limit.ToLimiterOptions());
            });

            options.AddPolicy(LoginPolicy, context =>
            {
                var limit = Limits(context).Login;
                return RateLimitPartition.GetFixedWindowLimiter($"login:{Ip(context)}", _ => limit.ToLimiterOptions());
            });
        });

        return services;
    }

    /// <summary>Writes the 429 ProblemDetails (with <c>Retry-After</c> when the lease knows it).</summary>
    public static async ValueTask WriteTooManyRequestsAsync(HttpContext httpContext, RateLimitLease lease)
    {
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problems = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = TooManyRequestsTitle,
                Detail = TooManyRequestsDetail,
            },
        });
    }

    private static RateLimitOptions Limits(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;

    private static bool IsEstablishedConnectionTraffic(HttpContext context)
    {
        var request = context.Request;
        if (!request.Query.ContainsKey(ConnectionIdQueryParameter) || context.WebSockets.IsWebSocketRequest)
        {
            return false;
        }

        var isEventStream = HttpMethods.IsGet(request.Method)
            && request.Headers.Accept.Any(a => a?.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase) == true);
        return !isEventStream;
    }

    private static string UserOrIp(HttpContext context) =>
        context.User.FindFirstValue(JwtClaimTypes.Subject) is { Length: > 0 } userId ? $"user:{userId}" : $"ip:{Ip(context)}";

    /// <summary>Client address after the forwarded headers (behind the proxy the connection is always the proxy's).</summary>
    private static string Ip(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

/// <summary>
/// Per-account login limiter (<see cref="RateLimitOptions.LoginAccount"/>), keyed by the normalized
/// email of the request. Unlike the per-IP policy it cannot be dodged with forged forwarded headers.
/// </summary>
public sealed class LoginAccountRateLimiter : IDisposable
{
    /// <summary>Longer keys are cut: no real address is that long and the key stays small.</summary>
    private const int MaxKeyLength = 320;

    private readonly PartitionedRateLimiter<string> _limiter;

    public LoginAccountRateLimiter(IOptions<RateLimitOptions> options)
    {
        var limit = options.Value.LoginAccount;
        _limiter = PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => limit.ToLimiterOptions()));
    }

    /// <summary>Takes one attempt for the account; the lease says whether it was allowed.</summary>
    public RateLimitLease Acquire(string? email)
    {
        var key = User.NormalizeEmail(email ?? string.Empty);
        return _limiter.AttemptAcquire(key.Length > MaxKeyLength ? key[..MaxKeyLength] : key);
    }

    public void Dispose() => _limiter.Dispose();
}

/// <summary>Applies <see cref="LoginAccountRateLimiter"/> to the login endpoint (after validation).</summary>
internal sealed class LoginAccountRateLimitFilter(LoginAccountRateLimiter limiter) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<OpenTrpg.Core.Application.Auth.LoginRequest>().FirstOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        using var lease = limiter.Acquire(request.Email);
        if (!lease.IsAcquired)
        {
            await RateLimitingSetup.WriteTooManyRequestsAsync(context.HttpContext, lease);
            return Results.Empty;
        }

        return await next(context);
    }
}
