using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Users;
using FluentValidation;

namespace Dnd.Application.Auth;

public sealed record SetPasswordRequest(string Token, string Password);

public sealed class SetPasswordRequestValidator : AbstractValidator<SetPasswordRequest>
{
    public SetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Falta el token del enlace.");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Introduce una contraseña.")
            .MinimumLength(PasswordPolicy.MinLength)
            .WithMessage($"La contraseña debe tener al menos {PasswordPolicy.MinLength} caracteres.")
            .MaximumLength(PasswordPolicy.MaxLength)
            .WithMessage($"La contraseña no puede superar los {PasswordPolicy.MaxLength} caracteres.");
    }
}

/// <summary>Consumes a setup/reset token, sets the password and closes every open session of the user.</summary>
public sealed class SetPasswordHandler(
    IPasswordTokenRepository passwordTokens,
    IUserRepository users,
    ITokenService tokens,
    IPasswordHasher passwordHasher,
    AuthSessionIssuer sessions,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task HandleAsync(SetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var stored = await passwordTokens.GetByHashAsync(tokens.HashToken(request.Token), cancellationToken);
        if (stored is null || !stored.IsUsable(now))
        {
            throw InvalidToken();
        }

        var user = await users.GetByIdAsync(stored.UserId, cancellationToken) ?? throw InvalidToken();

        user.SetPasswordHash(passwordHasher.Hash(user, request.Password));

        // Single use: this token and any other outstanding link of the user stop working.
        foreach (var token in await passwordTokens.ListUnusedByUserAsync(user.Id, purpose: null, cancellationToken))
        {
            token.MarkUsed(now);
        }

        await sessions.RevokeAllAsync(user.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static AppException InvalidToken() =>
        AppException.Validation("token", "El enlace no es válido, ha caducado o ya se ha usado. Solicita uno nuevo.");
}
