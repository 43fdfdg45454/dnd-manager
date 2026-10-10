using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Auth;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Users;
using OpenTrpg.Core.Domain.Users;
using FluentValidation;

namespace OpenTrpg.Core.Application.Setup;

public sealed record CreateInitialAdminRequest(string Email, string DisplayName, string Password);

public sealed class CreateInitialAdminRequestValidator : AbstractValidator<CreateInitialAdminRequest>
{
    public CreateInitialAdminRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Introduce el correo electrónico.")
            .MaximumLength(User.EmailMaxLength).WithMessage("El correo electrónico es demasiado largo.")
            .EmailAddress().WithMessage("El correo electrónico no es válido.");
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Introduce el nombre.")
            .MaximumLength(User.DisplayNameMaxLength)
            .WithMessage($"El nombre no puede superar los {User.DisplayNameMaxLength} caracteres.");
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Introduce una contraseña.")
            .MinimumLength(PasswordPolicy.MinLength)
            .WithMessage($"La contraseña debe tener al menos {PasswordPolicy.MinLength} caracteres.")
            .MaximumLength(PasswordPolicy.MaxLength)
            .WithMessage($"La contraseña no puede superar los {PasswordPolicy.MaxLength} caracteres.");
    }
}

/// <summary>
/// First-boot bootstrap: creates the first Admin, with its password, while the instance has no users.
/// Anonymous by design (there is nobody to authenticate yet): once a user exists it answers 409.
/// </summary>
public sealed class CreateInitialAdminHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    // Check + save must not interleave between two simultaneous first requests.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<UserDto> HandleAsync(CreateInitialAdminRequest request, CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (await users.AnyAsync(cancellationToken))
            {
                throw AppException.Conflict("La instancia ya está configurada.");
            }

            var admin = User.Create(request.Email, request.DisplayName, UserRole.Admin, clock.UtcNow);
            admin.SetPasswordHash(passwordHasher.Hash(admin, request.Password));
            users.Add(admin);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return UserDto.From(admin);
        }
        finally
        {
            Gate.Release();
        }
    }
}
