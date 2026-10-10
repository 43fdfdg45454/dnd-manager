using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Users;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace OpenTrpg.Core.Application.Auth;

public sealed record ForgotPasswordRequest(string Email);

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Introduce tu correo electrónico.")
            .EmailAddress().WithMessage("El correo electrónico no es válido.");
    }
}

/// <summary>Sends a reset link if the account exists and is active. Never reveals whether it exists.</summary>
public sealed class ForgotPasswordHandler(
    IUserRepository users,
    PasswordTokenIssuer passwordTokens,
    IAccountEmailService emails,
    IUnitOfWork unitOfWork,
    ILogger<ForgotPasswordHandler> logger)
{
    public async Task HandleAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByEmailAsync(User.NormalizeEmail(request.Email), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return;
        }

        var token = await passwordTokens.IssueAsync(user, PasswordTokenPurpose.Reset, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await emails.SendResetEmailAsync(user, token, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The response must not reveal anything, so a delivery failure is only logged.
            logger.LogError(ex, "Could not send the password reset email to user {UserId}", user.Id);
        }
    }
}
