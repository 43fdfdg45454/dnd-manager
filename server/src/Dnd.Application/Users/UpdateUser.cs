using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.Auth;
using Dnd.Application.Common;
using Dnd.Domain.Users;
using FluentValidation;

namespace Dnd.Application.Users;

/// <summary>Partial update: null fields are left unchanged.</summary>
public sealed record UpdateUserRequest(string? DisplayName, string? Role, bool? IsActive);

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("El nombre no puede estar vacío.")
            .MaximumLength(User.DisplayNameMaxLength)
            .WithMessage($"El nombre no puede superar los {User.DisplayNameMaxLength} caracteres.")
            .When(x => x.DisplayName is not null);
        RuleFor(x => x.Role)
            .Must(UserRoles.IsValid).WithMessage("El rol debe ser \"Admin\" o \"User\".")
            .When(x => x.Role is not null);
    }
}

public sealed class UpdateUserHandler(IUserRepository users, AuthSessionIssuer sessions, IUnitOfWork unitOfWork)
{
    public async Task<UserDto> HandleAsync(Guid currentUserId, Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw AppException.NotFound("Usuario no encontrado.");

        UserRole? newRole = UserRoles.TryParse(request.Role, out var parsed) ? parsed : null;

        if (user.Id == currentUserId)
        {
            if (newRole is not null && newRole != UserRole.Admin)
            {
                throw AppException.Validation("role", "No puedes quitarte a ti mismo el rol de administrador.");
            }

            if (request.IsActive == false)
            {
                throw AppException.Validation("isActive", "No puedes desactivar tu propia cuenta.");
            }
        }

        if (request.DisplayName is not null)
        {
            user.Rename(request.DisplayName);
        }

        if (newRole is not null)
        {
            user.ChangeRole(newRole.Value);
        }

        if (request.IsActive is { } isActive && isActive != user.IsActive)
        {
            user.SetActive(isActive);
            if (!isActive)
            {
                await sessions.RevokeAllAsync(user.Id, cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UserDto.From(user);
    }
}
