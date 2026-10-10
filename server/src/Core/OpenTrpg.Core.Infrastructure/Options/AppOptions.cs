namespace OpenTrpg.Core.Infrastructure.Options;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// Public base URL of the instance (<c>App:PublicUrl</c>), the one users reach the API through (the reverse
    /// proxy of the operator). Required: an absolute http(s) URL without path, query or userinfo, for example
    /// <c>https://dnd.example.com</c>. It is the origin of every link that goes into an email.
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>IANA time zone given to new campaigns (for example <c>Europe/Madrid</c>).</summary>
    public string DefaultTimeZone { get; set; } = "Europe/Madrid";
}
