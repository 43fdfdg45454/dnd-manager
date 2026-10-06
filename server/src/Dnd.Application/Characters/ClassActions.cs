using Dnd.Application.Abstractions;
using Dnd.Application.Common;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Dnd.Application.Characters;

// Class actions of the combat view: owner and DMs, without approval, in any status.

/// <param name="Amount">Points of the pool to spend.</param>
/// <param name="TargetSelf">True: the character heals itself; false: only the pool is spent (another creature is healed).</param>
/// <param name="Note">Free note of the action (e.g. "Curar a Thorin"); not stored, only logged with the action.</param>
public sealed record LayOnHandsRequest(int Amount, bool TargetSelf = true, string? Note = null);

public sealed class LayOnHandsRequestValidator : AbstractValidator<LayOnHandsRequest>
{
    public const int NoteMaxLength = 200;

    public LayOnHandsRequestValidator()
    {
        RuleFor(x => x.Amount).InclusiveBetween(1, AmountRequestValidator.MaxAmount)
            .WithMessage($"La cantidad debe estar entre 1 y {AmountRequestValidator.MaxAmount}.");
        RuleFor(x => x.Note).MaximumLength(NoteMaxLength)
            .WithMessage($"La nota no puede superar los {NoteMaxLength} caracteres.");
    }
}

public sealed record DivineSmiteRequest(int SlotLevel);

public sealed class DivineSmiteRequestValidator : AbstractValidator<DivineSmiteRequest>
{
    public DivineSmiteRequestValidator()
    {
        RuleFor(x => x.SlotLevel).InclusiveBetween(1, 9).WithMessage("El nivel del espacio de conjuro debe estar entre 1 y 9.");
    }
}

/// <param name="SlotLevels">One entry per slot to recover (e.g. [2, 1]).</param>
public sealed record ArcaneRecoveryRequest(IReadOnlyList<int>? SlotLevels);

public sealed class ArcaneRecoveryRequestValidator : AbstractValidator<ArcaneRecoveryRequest>
{
    /// <summary>More slots than this can never be valid (a wizard 20 recovers at most 10 slot levels).</summary>
    public const int MaxSlots = 10;

    public ArcaneRecoveryRequestValidator()
    {
        RuleFor(x => x.SlotLevels)
            .NotEmpty().WithMessage("Indica al menos un espacio de conjuro que recuperar.")
            .Must(l => l is null || l.Count <= MaxSlots).WithMessage($"Se pueden recuperar como máximo {MaxSlots} espacios.");
    }
}

/// <summary>Rage, Lay on Hands, Divine Smite and Arcane Recovery. 400 when the class does not apply or no uses remain.</summary>
public sealed class ClassActionHandler(CharacterTracker tracker, IDateTimeProvider clock, ILogger<ClassActionHandler> logger)
{
    public const string Rage = "rage";
    public const string LayOnHands = "lay-on-hands";
    public const string DivineSmite = "divine-smite";
    public const string ArcaneRecovery = "arcane-recovery";

    public static AppException UnknownAction(string action) => AppException.NotFound($"La acción de clase '{action}' no existe.");

    public async Task<CharacterDetailDto> RageAsync(Guid currentUserId, Guid characterId, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        character.Rage(clock.UtcNow);
        return await tracker.SaveAsync(character, cancellationToken);
    }

    public async Task<CharacterDetailDto> LayOnHandsAsync(Guid currentUserId, Guid characterId, LayOnHandsRequest request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        var sheet = await tracker.SheetAsync(character, cancellationToken);
        character.LayOnHands(request.Amount, request.TargetSelf, sheet.HitPointsMax, clock.UtcNow);
        var detail = await tracker.SaveAsync(character, cancellationToken);
        logger.LogInformation(
            "Lay on Hands of {CharacterId}: {Amount} points, self {TargetSelf}, note {Note}.",
            character.Id,
            request.Amount,
            request.TargetSelf,
            string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim());
        return detail;
    }

    public async Task<DivineSmiteResultDto> DivineSmiteAsync(Guid currentUserId, Guid characterId, DivineSmiteRequest request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        var sheet = await tracker.SheetAsync(character, cancellationToken);
        var damageDice = character.DivineSmite(request.SlotLevel, sheet.SpellSlotMax(request.SlotLevel), clock.UtcNow);
        return new DivineSmiteResultDto(await tracker.SaveAsync(character, cancellationToken), damageDice);
    }

    public async Task<CharacterDetailDto> ArcaneRecoveryAsync(Guid currentUserId, Guid characterId, ArcaneRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        character.ArcaneRecovery(request.SlotLevels ?? [], clock.UtcNow);
        return await tracker.SaveAsync(character, cancellationToken);
    }
}
