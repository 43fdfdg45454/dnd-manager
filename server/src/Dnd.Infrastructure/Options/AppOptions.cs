namespace Dnd.Infrastructure.Options;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Optional fallback for the public base URL of the instance (<c>App:PublicUrl</c>). The API does not need
    /// it: the origin is taken from the requests that reach it through the reverse proxy (see
    /// <c>IPublicUrlProvider</c>). It is only used for links built before any request has been seen, and only
    /// when informed it must be an absolute http(s) URL.
    /// </summary>
    public string? PublicUrl { get; set; }

    /// <summary>Email of the Admin created on first boot when there are no users. Empty disables it.</summary>
    public string? InitialAdminEmail { get; set; }

    /// <summary>IANA time zone given to new campaigns (for example <c>Europe/Madrid</c>).</summary>
    public string DefaultTimeZone { get; set; } = "Europe/Madrid";

    /// <summary>Run the initial admin bootstrap at startup.</summary>
    public bool SeedInitialAdmin { get; set; } = true;
}
