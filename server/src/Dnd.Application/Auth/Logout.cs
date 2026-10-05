using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using FluentValidation;

namespace Dnd.Application.Auth;

public sealed record LogoutRequest(string RefreshToken);

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Falta el token de refresco.");
    }
}

public sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    /// <summary>Revokes the refresh token if it belongs to <paramref name="currentUserId"/>. Idempotent.</summary>
    public async Task HandleAsync(Guid currentUserId, LogoutRequest request, CancellationToken cancellationToken = default)
    {
        var stored = await refreshTokens.GetByHashAsync(tokens.HashToken(request.RefreshToken), cancellationToken);
        if (stored is null || stored.UserId != currentUserId || stored.IsRevoked)
        {
            return;
        }

        stored.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
