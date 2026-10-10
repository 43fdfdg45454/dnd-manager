using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Auth;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Users;

namespace OpenTrpg.Core.Application.Users;

/// <summary>Issues a new setup token (invalidating the previous one) and emails it.</summary>
public sealed class ResendSetupEmailHandler(
    IUserRepository users,
    PasswordTokenIssuer passwordTokens,
    IAccountEmailService emails,
    IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw AppException.NotFound("Usuario no encontrado.");

        var token = await passwordTokens.IssueAsync(user, PasswordTokenPurpose.Setup, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await emails.SendSetupEmailAsync(user, token, cancellationToken);
    }
}
