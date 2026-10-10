using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using FluentValidation;

namespace OpenTrpg.Core.Application.Auth;

public sealed record RefreshRequest(string RefreshToken);

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Falta el token de refresco.");
    }
}

/// <summary>
/// Rotates a refresh token: the presented one is revoked and a new one is issued. Presenting a
/// token that was already revoked is treated as theft and revokes every token of the user.
/// </summary>
public sealed class RefreshHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    ITokenService tokens,
    AuthSessionIssuer sessions,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    public async Task<AuthResponse> HandleAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var stored = await refreshTokens.GetByHashAsync(tokens.HashToken(request.RefreshToken), cancellationToken)
            ?? throw InvalidSession();

        if (stored.IsRevoked)
        {
            await sessions.RevokeAllAsync(stored.UserId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw InvalidSession();
        }

        if (stored.IsExpired(now))
        {
            throw InvalidSession();
        }

        var user = await users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            stored.Revoke(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw InvalidSession();
        }

        var (response, newTokenHash) = sessions.Issue(user);
        stored.Revoke(now, newTokenHash);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    private static AppException InvalidSession() => AppException.Unauthorized("La sesión no es válida o ha caducado.");
}
