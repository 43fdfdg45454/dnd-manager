using Dnd.Application.ChangeRequests;
using Dnd.Application.Items;

namespace Dnd.Application.Characters;

/// <summary>One class of a character in the summary.</summary>
public sealed record CharacterClassSummaryDto(string ClassIndex, string ClassName, string? SubclassName, int Level);

/// <summary>What every member sees of a character. Hit points only for the owner and DMs (null otherwise).</summary>
public sealed record CharacterSummaryDto(
    Guid Id,
    Guid CampaignId,
    Guid? OwnerUserId,
    string? OwnerDisplayName,
    string Name,
    string Status,
    string? RaceName,
    IReadOnlyList<CharacterClassSummaryDto> Classes,
    int Level,
    int? HitPointsCurrent,
    int? HitPointsMax,
    string? PortraitUrl);

public sealed record CharacterClassDto(string ClassIndex, string ClassName, string? SubclassIndex, string? SubclassName, int Level, int Order);

public sealed record CharacterProficiencyDto(Guid Id, string Type, string Key, bool Expertise, string Source);

/// <param name="SpellName">Name from the catalog (null when the spell is not in it).</param>
/// <param name="SpellLevel">Spell level from the catalog (0 = cantrip; null when unknown).</param>
public sealed record CharacterSpellDto(Guid Id, string SpellIndex, string ClassIndex, bool IsPrepared, bool AlwaysPrepared, string? SpellName, int? SpellLevel);

public sealed record CharacterOverrideDto(string Field, int Value, string? Note);

public sealed record CharacterResourceDto(Guid Id, string? Key, string Name, int Max, int Used, string Recharge, bool IsAuto);

/// <summary>Spell slots of a level; level 0 = Pact Magic slots (all of the pact slot level).</summary>
public sealed record SpellSlotDto(int Level, int Max, int Used);

public sealed record CharacterConditionDto(string Index, string? Note);

public sealed record AbilityDto(int Score, int Modifier, bool Overridden);

public sealed record SavingThrowDto(int Value, bool Proficient, bool Overridden);

public sealed record SheetSkillDto(string Index, string Name, string Ability, int Value, bool Proficient, bool Expertise, bool Overridden);

public sealed record HitDiceDto(string ClassIndex, int Die, int Total, int Remaining);

public sealed record SpellcastingDto(string ClassIndex, string Ability, int SaveDc, int AttackBonus, int? PreparedMax);

/// <param name="PactSlotLevel">Spell level of the Pact Magic slots (null without them).</param>
public sealed record CharacterSheetDto(
    IReadOnlyDictionary<string, AbilityDto> Abilities,
    int TotalLevel,
    int ProficiencyBonus,
    IReadOnlyDictionary<string, SavingThrowDto> SavingThrows,
    IReadOnlyList<SheetSkillDto> Skills,
    int PassivePerception,
    int Initiative,
    int ArmorClass,
    int Speed,
    int HitPointsMax,
    IReadOnlyList<HitDiceDto> HitDice,
    IReadOnlyList<SpellcastingDto> Spellcasting,
    int? PactSlotLevel,
    IReadOnlyList<string> OverriddenFields);

/// <summary>Full character: stored fields, child collections, calculated sheet, inventory and pending change requests.</summary>
public sealed record CharacterDetailDto
{
    public required Guid Id { get; init; }

    public required Guid CampaignId { get; init; }

    public required Guid? OwnerUserId { get; init; }

    public required string? OwnerDisplayName { get; init; }

    public required string Name { get; init; }

    public required string Status { get; init; }

    public required string? RaceIndex { get; init; }

    public required string? RaceName { get; init; }

    public required string? SubraceIndex { get; init; }

    public required string? SubraceName { get; init; }

    public required string? BackgroundIndex { get; init; }

    public required string? BackgroundName { get; init; }

    public required string? Alignment { get; init; }

    public required bool ApplyRacialBonuses { get; init; }

    public required string HpMode { get; init; }

    public required int BaseStr { get; init; }

    public required int BaseDex { get; init; }

    public required int BaseCon { get; init; }

    public required int BaseInt { get; init; }

    public required int BaseWis { get; init; }

    public required int BaseCha { get; init; }

    public required int HitPointsCurrent { get; init; }

    public required int TemporaryHitPoints { get; init; }

    public required int DeathSaveSuccesses { get; init; }

    public required int DeathSaveFailures { get; init; }

    public required int ExhaustionLevel { get; init; }

    public required IReadOnlyList<CharacterConditionDto> Conditions { get; init; }

    public required string? ConcentratingOnSpellIndex { get; init; }

    public required bool Inspiration { get; init; }

    public required int CopperPieces { get; init; }

    /// <summary>Spent hit dice by class index.</summary>
    public required IReadOnlyDictionary<string, int> HitDiceUsed { get; init; }

    public required string Notes { get; init; }

    public required string Backstory { get; init; }

    public required Guid? PortraitFileId { get; init; }

    /// <summary>Always null until file storage arrives (phase 7).</summary>
    public required string? PortraitUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public required IReadOnlyList<CharacterClassDto> Classes { get; init; }

    public required IReadOnlyList<CharacterProficiencyDto> Proficiencies { get; init; }

    public required IReadOnlyList<CharacterSpellDto> Spells { get; init; }

    public required IReadOnlyList<CharacterOverrideDto> Overrides { get; init; }

    public required IReadOnlyList<CharacterResourceDto> Resources { get; init; }

    public required IReadOnlyList<SpellSlotDto> SpellSlots { get; init; }

    public required CharacterSheetDto Sheet { get; init; }

    public required IReadOnlyList<ChangeRequestDto> PendingChangeRequests { get; init; }

    public required InventoryDto Inventory { get; init; }
}
