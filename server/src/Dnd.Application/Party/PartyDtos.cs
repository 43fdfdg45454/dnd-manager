using Dnd.Application.Characters;

namespace Dnd.Application.Party;

/// <summary>What the DM sees of one active character at the table: vitals, defenses and spell slots.</summary>
/// <param name="SpellSlots">Regular slots, levels 1-9 with a maximum (or spent ones).</param>
/// <param name="PactSlots">Pact Magic slots (level = pact slot level), or null.</param>
/// <param name="PendingRest">Rest the player is asking for, or null.</param>
/// <param name="PendingLevelUpTo">Level granted by a DM and not completed yet, or null.</param>
public sealed record PartyMemberDto(
    Guid Id,
    string Name,
    Guid? OwnerUserId,
    string? OwnerDisplayName,
    string? PortraitUrl,
    IReadOnlyList<CharacterClassSummaryDto> Classes,
    int Level,
    int HitPointsCurrent,
    int HitPointsMax,
    int TemporaryHitPoints,
    int ArmorClass,
    int Initiative,
    int PassivePerception,
    int Speed,
    IReadOnlyList<CharacterConditionDto> Conditions,
    int ExhaustionLevel,
    int DeathSaveSuccesses,
    int DeathSaveFailures,
    string? ConcentratingOnSpellIndex,
    bool Inspiration,
    IReadOnlyList<SpellSlotDto> SpellSlots,
    SpellSlotDto? PactSlots,
    PendingRestDto? PendingRest,
    int? PendingLevelUpTo,
    bool SpellPreparationPending = false,
    string? SpellPreparationReason = null);

/// <summary>
/// The active characters of a campaign, sorted by name. After <c>POST /party/adjust</c>, <see cref="Damage"/> lists
/// the damage applied to each character and what it meant for its concentration (empty otherwise).
/// </summary>
public sealed record PartyDto(IReadOnlyList<PartyMemberDto> Characters)
{
    public IReadOnlyList<DamageOutcomeDto> Damage { get; init; } = [];
}
