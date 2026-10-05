using Dnd.Domain.Users;

namespace Dnd.Application.Abstractions;

/// <summary>Builds and sends the account emails (setup and password reset).</summary>
public interface IAccountEmailService
{
    /// <summary>Absolute link when the public origin is known, otherwise the relative <c>/set-password?token=...</c>.</summary>
    Task<string> BuildSetPasswordLinkAsync(string token, CancellationToken cancellationToken = default);

    Task SendSetupEmailAsync(User user, string token, CancellationToken cancellationToken = default);

    Task SendResetEmailAsync(User user, string token, CancellationToken cancellationToken = default);
}
