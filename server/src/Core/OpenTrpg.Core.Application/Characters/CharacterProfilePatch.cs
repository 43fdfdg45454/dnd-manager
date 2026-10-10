using System.Text.Json.Serialization;
using FluentValidation;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Characters;

/// <summary>
/// The core part of a sheet edit: name, free texts, money, height and weight. A game system's sheet patch extends it
/// with its own fields. Absent fields do not change.
/// </summary>
public record CharacterProfilePatch
{
    public string? Name { get; init; }

    public string? Notes { get; init; }

    public string? Backstory { get; init; }

    /// <summary>Personality traits (free text; the wizard writes one per line).</summary>
    public string? PersonalityTraits { get; init; }

    public string? Ideals { get; init; }

    public string? Bonds { get; init; }

    public string? Flaws { get; init; }

    /// <summary>Money in the minor unit of the system's currency (copper pieces in D&amp;D 5e).</summary>
    public int? CopperPieces { get; init; }

    /// <summary>
    /// Height in inches (1–200); explicit <c>null</c> clears it. No mechanical effect: the owner of an
    /// active character changes it without approval.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<int?> HeightInches { get; init; }

    /// <summary>Weight in pounds (1–2000); explicit <c>null</c> clears it. Same rules as <see cref="HeightInches"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<int?> WeightPounds { get; init; }

    /// <summary>JSON names of the height and weight fields.</summary>
    public static IReadOnlyList<string> HeightAndWeightFields { get; } = ["heightInches", "weightPounds"];

    /// <summary>True when the patch changes height or weight.</summary>
    [JsonIgnore]
    public bool HasHeightOrWeight => HeightInches.IsSet || WeightPounds.IsSet;

    /// <summary>Maps to the domain edit of the core character. Call only on a patch accepted by the validator.</summary>
    public CharacterProfileEdit ToProfileEdit() => new()
    {
        Name = Name,
        Notes = Notes,
        Backstory = Backstory,
        PersonalityTraits = PersonalityTraits,
        Ideals = Ideals,
        Bonds = Bonds,
        Flaws = Flaws,
        Money = CopperPieces,
        HeightInches = ClearableNumber(HeightInches),
        WeightPounds = ClearableNumber(WeightPounds),
    };

    /// <summary>Absent → null (unchanged); explicit null → 0 (cleared, as the domain edit expects).</summary>
    protected static int? ClearableNumber(Optional<int?> value) => value.IsSet ? value.Value ?? 0 : null;
}

/// <summary>Rules of the core fields of a sheet edit (a system's patch validator includes it).</summary>
public sealed class CharacterProfilePatchValidator : AbstractValidator<CharacterProfilePatch>
{
    public CharacterProfilePatchValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre no puede estar vacío.")
            .MaximumLength(Character.NameMaxLength).WithMessage($"El nombre no puede superar los {Character.NameMaxLength} caracteres.")
            .When(x => x.Name is not null);
        RuleFor(x => x.Notes).MaximumLength(Character.TextMaxLength)
            .WithMessage($"Las notas no pueden superar los {Character.TextMaxLength} caracteres.");
        RuleFor(x => x.Backstory).MaximumLength(Character.TextMaxLength)
            .WithMessage($"La historia no puede superar los {Character.TextMaxLength} caracteres.");
        RuleFor(x => x.PersonalityTraits).MaximumLength(Character.PersonalityMaxLength)
            .WithMessage($"Los rasgos de personalidad no pueden superar los {Character.PersonalityMaxLength} caracteres.");
        RuleFor(x => x.Ideals).MaximumLength(Character.PersonalityMaxLength)
            .WithMessage($"Los ideales no pueden superar los {Character.PersonalityMaxLength} caracteres.");
        RuleFor(x => x.Bonds).MaximumLength(Character.PersonalityMaxLength)
            .WithMessage($"Los vínculos no pueden superar los {Character.PersonalityMaxLength} caracteres.");
        RuleFor(x => x.Flaws).MaximumLength(Character.PersonalityMaxLength)
            .WithMessage($"Los defectos no pueden superar los {Character.PersonalityMaxLength} caracteres.");
        RuleFor(x => x.CopperPieces).InclusiveBetween(0, Character.MaxMoney)
            .WithMessage($"El dinero debe estar entre 0 y {Character.MaxMoney} pc.")
            .When(x => x.CopperPieces is not null);
        RuleFor(x => x.HeightInches)
            .Must(v => !v.IsSet || v.Value is null || v.Value is >= Character.MinHeightInches and <= Character.MaxHeightInches)
            .WithMessage($"La altura debe estar entre {Character.MinHeightInches} y {Character.MaxHeightInches} pulgadas.")
            .OverridePropertyName("heightInches");
        RuleFor(x => x.WeightPounds)
            .Must(v => !v.IsSet || v.Value is null || v.Value is >= Character.MinWeightPounds and <= Character.MaxWeightPounds)
            .WithMessage($"El peso debe estar entre {Character.MinWeightPounds} y {Character.MaxWeightPounds} libras.")
            .OverridePropertyName("weightPounds");
    }
}
