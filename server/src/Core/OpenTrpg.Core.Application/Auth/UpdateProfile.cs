using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Users;
using OpenTrpg.Core.Domain.Users;
using FluentValidation;

namespace OpenTrpg.Core.Application.Auth;

/// <summary>Partial update of the own profile: null fields are left unchanged.</summary>
public sealed record UpdateProfileRequest(string? DisplayName, bool? NotificationsEnabled);

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("El nombre no puede estar vacío.")
            .MaximumLength(User.DisplayNameMaxLength)
            .WithMessage($"El nombre no puede superar los {User.DisplayNameMaxLength} caracteres.")
            .When(x => x.DisplayName is not null);
    }
}

/// <summary>The authenticated user edits their display name and whether they receive emails.</summary>
public sealed class UpdateProfileHandler(IUserRepository users, IUnitOfWork unitOfWork)
{
    public async Task<UserDto> HandleAsync(Guid currentUserId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(currentUserId, cancellationToken)
            ?? throw AppException.Unauthorized("La sesión no es válida.");

        if (request.DisplayName is not null)
        {
            user.Rename(request.DisplayName);
        }

        if (request.NotificationsEnabled is { } enabled)
        {
            user.SetNotificationsEnabled(enabled);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UserDto.From(user);
    }
}
