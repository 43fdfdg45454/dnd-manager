using Dnd.Application.Abstractions;
using Dnd.Application.Abstractions.Persistence;
using Dnd.Application.ChangeRequests;
using Dnd.Application.Common;
using Dnd.Domain.Characters;

namespace Dnd.Application.Characters;

/// <summary>
/// Bridges characters with the rules catalog: calculates sheets, keeps derived state (automatic
/// resources, current hit points) in sync after edits, checks catalog references of edits and
/// builds the DTOs.
/// </summary>
public interface ICharacterSheetService
{
    /// <summary>Calculates the sheet of a character with all its child collections loaded.</summary>
    Task<CharacterSheet> CalculateAsync(Character character, CancellationToken cancellationToken = default);

    /// <summary>
    /// After a sheet edit (or on creation): calculates the sheet, regenerates the automatic class
    /// resources and refreshes the current hit points. Returns the new sheet.
    /// </summary>
    Task<CharacterSheet> RecalculateAsync(Character character, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks that the race, subrace, background, classes, subclasses and spells an edit would leave
    /// on the character exist in the catalog (and that subraces/subclasses belong to their parent).
    /// Throws a validation <see cref="AppException"/> otherwise.
    /// </summary>
    Task EnsureCatalogReferencesAsync(Character character, SheetEdit edit, CancellationToken cancellationToken = default);

    Task<CharacterDetailDto> BuildDetailAsync(Character character, CancellationToken cancellationToken = default);

    /// <summary>Summaries sorted by name. Hit points only for characters the viewer owns, or all when a DM.</summary>
    Task<IReadOnlyList<CharacterSummaryDto>> BuildSummariesAsync(
        IReadOnlyList<Character> characters,
        Guid viewerUserId,
        bool viewerIsDm,
        CancellationToken cancellationToken = default);
}

public sealed class CharacterSheetService(
    ICatalogRepository catalog,
    IEquippedGearProvider gearProvider,
    IUserRepository users,
    IChangeRequestRepository changeRequests) : ICharacterSheetService
{
    public async Task<CharacterSheet> CalculateAsync(Character character, CancellationToken cancellationToken = default)
    {
        var sheetCatalog = await SheetCatalog.LoadAsync(catalog, [character], includeSpells: false, cancellationToken);
        return await CalculateAsync(character, sheetCatalog, cancellationToken);
    }

    public async Task<CharacterSheet> RecalculateAsync(Character character, CancellationToken cancellationToken = default)
    {
        var sheet = await CalculateAsync(character, cancellationToken);
        character.SyncAutoResources(ClassResourceRules.ForClasses(character.Classes, sheet.AbilityModifiers));
        character.RefreshHitPoints(sheet.HitPointsMax);
        return sheet;
    }

    public async Task EnsureCatalogReferencesAsync(Character character, SheetEdit edit, CancellationToken cancellationToken = default)
    {
        static string? Effective(string? edited, string? current) =>
            edited is null ? current : edited.Trim() is { Length: > 0 } trimmed ? trimmed : null;

        var race = Effective(edit.RaceIndex, character.RaceIndex);
        if (race is not null && race != character.RaceIndex && await catalog.GetRaceAsync(race, cancellationToken) is null)
        {
            throw AppException.Validation("raceIndex", $"La raza '{race}' no existe en el catálogo.");
        }

        // Changing the race without a subrace clears the subrace (see Character.ApplySheetEdit).
        var subrace = edit.SubraceIndex is not null
            ? Effective(edit.SubraceIndex, null)
            : race == character.RaceIndex ? character.SubraceIndex : null;
        if (subrace is not null && (edit.SubraceIndex is not null || edit.RaceIndex is not null))
        {
            var subraces = await catalog.ListSubracesByIndexAsync([subrace], cancellationToken);
            if (subraces.Count == 0 || subraces[0].RaceIndex != race)
            {
                throw AppException.Validation("subraceIndex", $"La subraza '{subrace}' no existe para la raza elegida.");
            }
        }

        var background = Effective(edit.BackgroundIndex, character.BackgroundIndex);
        if (background is not null && background != character.BackgroundIndex
            && (await catalog.ListBackgroundsByIndexAsync([background], cancellationToken)).Count == 0)
        {
            throw AppException.Validation("backgroundIndex", $"El trasfondo '{background}' no existe en el catálogo.");
        }

        if (edit.Classes is { Count: > 0 } classes)
        {
            var classIndexes = classes.Select(c => c.ClassIndex.Trim()).Distinct(StringComparer.Ordinal).ToList();
            var known = (await catalog.ListClassesByIndexAsync(classIndexes, cancellationToken)).Select(c => c.Index).ToHashSet(StringComparer.Ordinal);
            if (classIndexes.FirstOrDefault(c => !known.Contains(c)) is { } unknownClass)
            {
                throw AppException.Validation("classes", $"La clase '{unknownClass}' no existe en el catálogo.");
            }

            var subclassIndexes = classes
                .Select(c => c.SubclassIndex?.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var subclasses = (await catalog.ListSubclassesByIndexAsync(subclassIndexes, cancellationToken)).ToDictionary(s => s.Index, StringComparer.Ordinal);
            foreach (var entry in classes)
            {
                var subclass = entry.SubclassIndex?.Trim();
                if (!string.IsNullOrEmpty(subclass)
                    && (!subclasses.TryGetValue(subclass, out var definition) || definition.ClassIndex != entry.ClassIndex.Trim()))
                {
                    throw AppException.Validation("classes", $"La subclase '{subclass}' no existe para la clase '{entry.ClassIndex.Trim()}'.");
                }
            }
        }

        if (edit.Spells is { Count: > 0 } spells)
        {
            var spellIndexes = spells.Select(s => s.SpellIndex.Trim()).Distinct(StringComparer.Ordinal).ToList();
            var known = (await catalog.ListSpellsByIndexAsync(spellIndexes, cancellationToken)).Select(s => s.Index).ToHashSet(StringComparer.Ordinal);
            if (spellIndexes.FirstOrDefault(s => !known.Contains(s)) is { } unknownSpell)
            {
                throw AppException.Validation("spells", $"El conjuro '{unknownSpell}' no existe en el catálogo.");
            }
        }
    }

    public async Task<CharacterDetailDto> BuildDetailAsync(Character character, CancellationToken cancellationToken = default)
    {
        var sheetCatalog = await SheetCatalog.LoadAsync(catalog, [character], includeSpells: true, cancellationToken);
        var sheet = await CalculateAsync(character, sheetCatalog, cancellationToken);
        var ownerName = character.OwnerUserId is { } ownerId
            ? (await users.GetDisplayNamesAsync([ownerId], cancellationToken)).GetValueOrDefault(ownerId)
            : null;
        var pending = await changeRequests.ListViewsAsync(
            new ChangeRequestQuery(CharacterId: character.Id, Status: ChangeRequestStatus.Pending),
            cancellationToken);

        return new CharacterDetailDto
        {
            Id = character.Id,
            CampaignId = character.CampaignId,
            OwnerUserId = character.OwnerUserId,
            OwnerDisplayName = ownerName,
            Name = character.Name,
            Status = character.Status.ToString(),
            RaceIndex = character.RaceIndex,
            RaceName = sheetCatalog.Race(character.RaceIndex)?.Name,
            SubraceIndex = character.SubraceIndex,
            SubraceName = sheetCatalog.Subrace(character.SubraceIndex)?.Name,
            BackgroundIndex = character.BackgroundIndex,
            BackgroundName = sheetCatalog.Background(character.BackgroundIndex)?.Name,
            Alignment = character.Alignment,
            ApplyRacialBonuses = character.ApplyRacialBonuses,
            HpMode = character.HpMode.ToString(),
            BaseStr = character.BaseStr,
            BaseDex = character.BaseDex,
            BaseCon = character.BaseCon,
            BaseInt = character.BaseInt,
            BaseWis = character.BaseWis,
            BaseCha = character.BaseCha,
            HitPointsCurrent = character.HitPointsCurrent,
            TemporaryHitPoints = character.TemporaryHitPoints,
            DeathSaveSuccesses = character.DeathSaveSuccesses,
            DeathSaveFailures = character.DeathSaveFailures,
            ExhaustionLevel = character.ExhaustionLevel,
            Conditions = character.Conditions.Select(c => new CharacterConditionDto(c.Index, c.Note)).ToList(),
            ConcentratingOnSpellIndex = character.ConcentratingOnSpellIndex,
            Inspiration = character.Inspiration,
            CopperPieces = character.CopperPieces,
            HitDiceUsed = character.HitDiceUsed,
            Notes = character.Notes,
            Backstory = character.Backstory,
            PortraitFileId = character.PortraitFileId,
            PortraitUrl = null,
            CreatedAt = character.CreatedAt,
            UpdatedAt = character.UpdatedAt,
            Classes = character.OrderedClasses
                .Select(c => new CharacterClassDto(
                    c.ClassIndex,
                    sheetCatalog.Class(c.ClassIndex)?.Name ?? c.ClassIndex,
                    c.SubclassIndex,
                    sheetCatalog.Subclass(c.SubclassIndex)?.Name,
                    c.Level,
                    c.Order))
                .ToList(),
            Proficiencies = character.Proficiencies
                .OrderBy(p => p.Type)
                .ThenBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new CharacterProficiencyDto(p.Id, p.Type.ToString(), p.Key, p.Expertise, p.Source.ToString()))
                .ToList(),
            Spells = character.Spells
                .Select(s => (Spell: s, Definition: sheetCatalog.Spell(s.SpellIndex)))
                .OrderBy(s => s.Definition?.Level ?? int.MaxValue)
                .ThenBy(s => s.Definition?.Name ?? s.Spell.SpellIndex, StringComparer.Ordinal)
                .ThenBy(s => s.Spell.ClassIndex, StringComparer.Ordinal)
                .Select(s => new CharacterSpellDto(
                    s.Spell.Id,
                    s.Spell.SpellIndex,
                    s.Spell.ClassIndex,
                    s.Spell.IsPrepared,
                    s.Spell.AlwaysPrepared,
                    s.Definition?.Name,
                    s.Definition?.Level))
                .ToList(),
            Overrides = character.Overrides
                .OrderBy(o => o.Field, StringComparer.Ordinal)
                .Select(o => new CharacterOverrideDto(o.Field, o.Value, o.Note))
                .ToList(),
            Resources = character.Resources
                .OrderBy(r => r.IsAuto ? 0 : 1)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .Select(ToDto)
                .ToList(),
            SpellSlots = SpellSlots(character, sheet),
            Sheet = ToDto(sheet),
            PendingChangeRequests = pending.Select(ChangeRequestDto.From).ToList(),
        };
    }

    public async Task<IReadOnlyList<CharacterSummaryDto>> BuildSummariesAsync(
        IReadOnlyList<Character> characters,
        Guid viewerUserId,
        bool viewerIsDm,
        CancellationToken cancellationToken = default)
    {
        var sheetCatalog = await SheetCatalog.LoadAsync(catalog, characters, includeSpells: false, cancellationToken);
        var visible = characters.Where(c => c.CanViewSheet(viewerUserId, viewerIsDm)).Select(c => c.Id).ToList();
        var gear = await gearProvider.GetAsync(visible, cancellationToken);
        var ownerIds = characters.Select(c => c.OwnerUserId).OfType<Guid>().Distinct().ToList();
        var owners = await users.GetDisplayNamesAsync(ownerIds, cancellationToken);

        return characters
            .OrderBy(c => c.Name, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(c => c.Id)
            .Select(c =>
            {
                var showHp = c.CanViewSheet(viewerUserId, viewerIsDm);
                var hitPointsMax = showHp
                    ? SheetCalculator.Calculate(sheetCatalog.InputFor(c, gear.GetValueOrDefault(c.Id) ?? EquippedGear.None)).HitPointsMax
                    : (int?)null;
                return new CharacterSummaryDto(
                    c.Id,
                    c.CampaignId,
                    c.OwnerUserId,
                    c.OwnerUserId is { } owner ? owners.GetValueOrDefault(owner) : null,
                    c.Name,
                    c.Status.ToString(),
                    sheetCatalog.Race(c.RaceIndex)?.Name,
                    c.OrderedClasses
                        .Select(k => new CharacterClassSummaryDto(
                            k.ClassIndex,
                            sheetCatalog.Class(k.ClassIndex)?.Name ?? k.ClassIndex,
                            sheetCatalog.Subclass(k.SubclassIndex)?.Name,
                            k.Level))
                        .ToList(),
                    c.TotalLevel,
                    showHp ? c.HitPointsCurrent : null,
                    hitPointsMax,
                    null);
            })
            .ToList();
    }

    private async Task<CharacterSheet> CalculateAsync(Character character, SheetCatalog sheetCatalog, CancellationToken cancellationToken)
    {
        var gear = await gearProvider.GetAsync([character.Id], cancellationToken);
        return SheetCalculator.Calculate(sheetCatalog.InputFor(character, gear.GetValueOrDefault(character.Id) ?? EquippedGear.None));
    }

    /// <summary>Levels with slots (or spent ones), pact slots first as level 0.</summary>
    private static List<SpellSlotDto> SpellSlots(Character character, CharacterSheet sheet) =>
        Enumerable.Range(SpellSlotState.PactLevel, 10)
            .Select(level => new SpellSlotDto(level, sheet.SpellSlotMax(level), character.SpellSlotsUsed(level)))
            .Where(s => s.Max > 0 || s.Used > 0)
            .ToList();

    private static CharacterResourceDto ToDto(CharacterResource r) =>
        new(r.Id, r.Key, r.Name, r.Max, r.Used, r.Recharge.ToString(), r.IsAuto);

    private static CharacterSheetDto ToDto(CharacterSheet sheet) => new(
        sheet.Abilities.ToDictionary(a => a.Key, a => new AbilityDto(a.Value.Score, a.Value.Modifier, a.Value.Overridden)),
        sheet.TotalLevel,
        sheet.ProficiencyBonus,
        sheet.SavingThrows.ToDictionary(s => s.Key, s => new SavingThrowDto(s.Value.Value, s.Value.Proficient, s.Value.Overridden)),
        sheet.Skills.Select(s => new SheetSkillDto(s.Index, s.Name, s.Ability, s.Value, s.Proficient, s.Expertise, s.Overridden)).ToList(),
        sheet.PassivePerception,
        sheet.Initiative,
        sheet.ArmorClass,
        sheet.Speed,
        sheet.HitPointsMax,
        sheet.HitDice.Select(h => new HitDiceDto(h.ClassIndex, h.Die, h.Total, h.Remaining)).ToList(),
        sheet.Spellcasting.Select(s => new SpellcastingDto(s.ClassIndex, s.Ability, s.SaveDc, s.AttackBonus, s.PreparedMax)).ToList(),
        sheet.PactMagic?.SlotLevel,
        sheet.OverriddenFields);
}
