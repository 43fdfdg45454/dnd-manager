using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Auth;
using Dnd.Domain.Users;
using Microsoft.Extensions.Logging;

namespace Dnd.Application.Users;

/// <summary>
/// First-boot bootstrap: when the database has no users, creates an Admin with the configured email
/// and sends the setup email. The link is also logged as a warning so the first boot works without SMTP.
/// </summary>
public sealed class InitialAdminSeeder(
    IUserRepository users,
    PasswordTokenIssuer passwordTokens,
    IAccountEmailService emails,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<InitialAdminSeeder> logger)
{
    public const string DefaultDisplayName = "Administrador";

    public async Task SeedAsync(string? adminEmail, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adminEmail) || await users.AnyAsync(cancellationToken))
        {
            return;
        }

        var admin = User.Create(adminEmail, DefaultDisplayName, UserRole.Admin, clock.UtcNow);
        users.Add(admin);
        var token = await passwordTokens.IssueAsync(admin, PasswordTokenPurpose.Setup, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Initial admin {Email} created. Set its password with this link (valid for {Hours} h): {Link}",
            admin.Email,
            PasswordToken.SetupLifetime.TotalHours,
            emails.BuildSetPasswordLink(token));

        try
        {
            await emails.SendSetupEmailAsync(admin, token, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not send the setup email to the initial admin. Use the link from the log instead.");
        }
    }
}
