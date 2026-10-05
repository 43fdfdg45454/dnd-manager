using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Common;
using Dnd.Domain.Users;
using FluentValidation;

namespace Dnd.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Introduce tu correo electrónico.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Introduce tu contraseña.");
    }
}

public sealed class LoginHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    AuthSessionIssuer sessions,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<AuthResponse> HandleAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByEmailAsync(User.NormalizeEmail(request.Email), cancellationToken);
        if (user is null || !user.IsActive || user.PasswordHash is null)
        {
            throw InvalidCredentials();
        }

        var outcome = passwordHasher.Verify(user, user.PasswordHash, request.Password);
        if (outcome == PasswordVerificationOutcome.Failed)
        {
            throw InvalidCredentials();
        }

        if (outcome == PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordHasher.Hash(user, request.Password));
        }

        user.RecordLogin(clock.UtcNow);
        var (response, _) = sessions.Issue(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    private static AppException InvalidCredentials() => AppException.Unauthorized("Correo o contraseña incorrectos.");
}
