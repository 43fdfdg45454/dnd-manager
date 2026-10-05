namespace Dnd.Infrastructure.Options;

public sealed class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Public base URL of the instance, used to build links in emails.</summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>Email of the Admin created on first boot when there are no users. Empty disables it.</summary>
    public string? InitialAdminEmail { get; set; }

    /// <summary>IANA time zone given to new campaigns (for example <c>Europe/Madrid</c>).</summary>
    public string DefaultTimeZone { get; set; } = "Europe/Madrid";

    /// <summary>Run the initial admin bootstrap at startup.</summary>
    public bool SeedInitialAdmin { get; set; } = true;
}
