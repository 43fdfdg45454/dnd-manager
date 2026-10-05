namespace Dnd.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinSecretLength = 32;

    /// <summary>HS256 signing key. At least <see cref="MinSecretLength"/> characters.</summary>
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "dnd-companion";

    public string Audience { get; set; } = "dnd-companion-app";

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 30;
}
