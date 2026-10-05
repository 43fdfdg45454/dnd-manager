using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Dnd.Api.Hosting;

/// <summary>
/// Forwarded headers (<c>X-Forwarded-For/Proto/Host</c>) set by the reverse proxy of the operator, honoured only
/// when the connection comes from <c>App:TrustedProxies</c>.
/// </summary>
/// <remarks>
/// Why not trust the headers from any origin: the scheme and host they carry end up in the links of emails
/// (set password, reset password, session links). If anybody could send <c>X-Forwarded-Host: evil.example</c>,
/// an attacker would request a password reset for a victim and the victim would receive a genuine email whose
/// link points to the attacker's host (host header poisoning), handing over the reset token. Only proxies we
/// know (by default loopback and the private ranges, because the API runs in Docker behind a proxy on the same
/// machine or LAN) may say which host the client used.
/// </remarks>
public static class ForwardedHeadersSetup
{
    public const string TrustedProxiesKey = "App:TrustedProxies";

    public static readonly string[] DefaultTrustedProxies =
    [
        "127.0.0.1/32",
        "::1/128",
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
    ];

    public static IServiceCollection AddAppForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var networks = ParseTrustedProxies(ResolveTrustedProxies(configuration));

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // The built-in defaults (loopback) are replaced by the configured list. An empty list must mean
            // "trust nobody", but the middleware treats empty known lists as "trust everyone": turn it off.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            if (networks.Count == 0)
            {
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            foreach (var network in networks)
            {
                options.KnownIPNetworks.Add(network);
            }
        });

        return services;
    }

    /// <summary>
    /// <c>App:TrustedProxies</c> as a list (<c>App:TrustedProxies:0</c>...) or a comma/space separated string;
    /// the defaults when the key is absent; an empty list when it is present but empty.
    /// </summary>
    public static IReadOnlyList<string> ResolveTrustedProxies(IConfiguration configuration)
    {
        var section = configuration.GetSection(TrustedProxiesKey);
        var children = section.GetChildren().Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (children.Count > 0)
        {
            return children.SelectMany(v => Split(v!)).ToList();
        }

        return section.Value is { } scalar ? Split(scalar).ToList() : DefaultTrustedProxies;
    }

    /// <summary>Parses CIDR ranges and single IPs (taken as /32 or /128); throws with the key name on an invalid entry.</summary>
    public static IReadOnlyList<System.Net.IPNetwork> ParseTrustedProxies(IEnumerable<string> entries)
    {
        var networks = new List<System.Net.IPNetwork>();
        foreach (var entry in entries)
        {
            if (System.Net.IPNetwork.TryParse(entry, out var network))
            {
                networks.Add(network);
            }
            else if (IPAddress.TryParse(entry, out var address))
            {
                networks.Add(new System.Net.IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128));
            }
            else
            {
                throw new InvalidOperationException($"{TrustedProxiesKey} contains '{entry}', which is not an IP address or a CIDR range (for example 10.0.0.0/8).");
            }
        }

        return networks;
    }

    private static IEnumerable<string> Split(string value) =>
        value.Split([',', ';', ' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
