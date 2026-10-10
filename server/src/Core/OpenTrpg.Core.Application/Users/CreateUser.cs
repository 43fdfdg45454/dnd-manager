using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Auth;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Users;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace OpenTrpg.Core.Application.Users;

public sealed record CreateUserRequest(string Email, string DisplayName, string Role);

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Introduce el correo electrónico.")
            .MaximumLength(User.EmailMaxLength).WithMessage("El correo electrónico es demasiado largo.")
            .EmailAddress().WithMessage("El correo electrónico no es válido.");
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Introduce el nombre.")
            .MaximumLength(User.DisplayNameMaxLength)
            .WithMessage($"El nombre no puede superar los {User.DisplayNameMaxLength} caracteres.");
        RuleFor(x => x.Role).Must(UserRoles.IsValid).WithMessage("El rol debe ser \"Admin\" o \"User\".");
    }
}

/// <summary>Creates a user without password and emails the setup link.</summary>
public sealed class CreateUserHandler(
    IUserRepository users,
    PasswordTokenIssuer passwordTokens,
    IAccountEmailService emails,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<CreateUserHandler> logger)
{
    public async Task<UserDto> HandleAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = User.NormalizeEmail(request.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
        {
            throw AppException.Conflict("Ya existe un usuario con ese correo electrónico.");
        }

        UserRoles.TryParse(request.Role, out var role);
        var user = User.Create(email, request.DisplayName, role, clock.UtcNow);
        users.Add(user);

        var token = await passwordTokens.IssueAsync(user, PasswordTokenPurpose.Setup, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await emails.SendSetupEmailAsync(user, token, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The user already exists; the admin can resend the setup email once SMTP works.
            logger.LogError(ex, "Could not send the setup email to user {UserId}", user.Id);
        }

        return UserDto.From(user);
    }
}
