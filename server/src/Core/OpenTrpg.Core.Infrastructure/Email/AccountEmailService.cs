using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Domain.Users;
using OpenTrpg.Core.Infrastructure.Email.Templates;

namespace OpenTrpg.Core.Infrastructure.Email;

internal sealed class AccountEmailService(IEmailSender sender, PublicLinkBuilder links) : IAccountEmailService
{
    public Task<string> BuildSetPasswordLinkAsync(string token, CancellationToken cancellationToken = default) =>
        links.BuildAsync($"/set-password?token={Uri.EscapeDataString(token)}", cancellationToken);

    public async Task SendSetupEmailAsync(User user, string token, CancellationToken cancellationToken = default) =>
        await sender.SendAsync(
            AccountEmailTemplates.Setup(user.Email, user.DisplayName, await BuildSetPasswordLinkAsync(token, cancellationToken)),
            cancellationToken);

    public async Task SendResetEmailAsync(User user, string token, CancellationToken cancellationToken = default) =>
        await sender.SendAsync(
            AccountEmailTemplates.Reset(user.Email, user.DisplayName, await BuildSetPasswordLinkAsync(token, cancellationToken)),
            cancellationToken);
}
