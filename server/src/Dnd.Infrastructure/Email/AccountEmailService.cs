using Dnd.Application.Abstractions;
using Dnd.Domain.Users;
using Dnd.Infrastructure.Email.Templates;
using Dnd.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Dnd.Infrastructure.Email;

internal sealed class AccountEmailService(IEmailSender sender, IOptions<AppOptions> options) : IAccountEmailService
{
    public string BuildSetPasswordLink(string token) =>
        $"{options.Value.PublicUrl.TrimEnd('/')}/set-password?token={Uri.EscapeDataString(token)}";

    public Task SendSetupEmailAsync(User user, string token, CancellationToken cancellationToken = default) =>
        sender.SendAsync(AccountEmailTemplates.Setup(user.Email, user.DisplayName, BuildSetPasswordLink(token)), cancellationToken);

    public Task SendResetEmailAsync(User user, string token, CancellationToken cancellationToken = default) =>
        sender.SendAsync(AccountEmailTemplates.Reset(user.Email, user.DisplayName, BuildSetPasswordLink(token)), cancellationToken);
}
