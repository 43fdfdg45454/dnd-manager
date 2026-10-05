using Dnd.Domain.Users;

namespace Dnd.Application.Abstractions;

/// <summary>Builds and sends the account emails (setup and password reset).</summary>
public interface IAccountEmailService
{
    string BuildSetPasswordLink(string token);

    Task SendSetupEmailAsync(User user, string token, CancellationToken cancellationToken = default);

    Task SendResetEmailAsync(User user, string token, CancellationToken cancellationToken = default);
}
