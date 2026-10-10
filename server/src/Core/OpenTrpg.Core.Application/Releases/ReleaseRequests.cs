using OpenTrpg.Core.Domain.Releases;
using FluentValidation;

namespace OpenTrpg.Core.Application.Releases;

/// <summary>
/// Metadata of a new release. Sent as form fields next to the <c>file</c> part (multipart) or as a JSON
/// body with <see cref="FileId"/> pointing at an APK already uploaded with kind AppRelease.
/// </summary>
/// <param name="Version">Semantic version <c>major.minor.patch</c>.</param>
/// <param name="BuildNumber">Positive integer, higher than the previous builds.</param>
/// <param name="IsMandatory">Defaults to false.</param>
public sealed record PublishReleaseRequest(string? Version, int? BuildNumber, string? Notes, bool? IsMandatory, Guid? FileId);

public sealed class PublishReleaseRequestValidator : AbstractValidator<PublishReleaseRequest>
{
    public PublishReleaseRequestValidator()
    {
        RuleFor(x => x.Version)
            .Must(version => AppRelease.IsValidVersion(version?.Trim()))
            .WithMessage("La versión debe tener el formato mayor.menor.parche, por ejemplo 1.2.0.");
        RuleFor(x => x.BuildNumber)
            .NotNull().WithMessage("Indica el número de compilación.")
            .GreaterThanOrEqualTo(1).WithMessage("El número de compilación debe ser un entero positivo.");
        RuleFor(x => x.Notes)
            .MaximumLength(AppRelease.NotesMaxLength)
            .WithMessage($"Las notas no pueden superar los {AppRelease.NotesMaxLength} caracteres.");
    }
}
