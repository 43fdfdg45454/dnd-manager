using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.ChangeRequests;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.Files;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Application.Party;
using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Items;
using OpenTrpg.Systems.Dnd5e.Application.Party;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>
/// Bridges characters with the rules catalog: calculates sheets, keeps derived state (automatic
/// resources, current hit points) in sync after edits, checks catalog references of edits and
/// builds the DTOs.
/// </summary>
public interface ICharacterSheetService
{
    /// <summary>Calculates the sheet of a character with all its child collections (inventory included) loaded.</summary>
    Task<CharacterSheet> CalculateAsync(Dnd5eCharacter character, CancellationToken cancellationToken = default);

    /// <summary>
    /// Like <see cref="CalculateAsync"/> for several characters at once, loading the catalog and the item
    /// templates once for all of them. Sheets by character id.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, CharacterSheet>> CalculateManyAsync(IReadOnlyList<Dnd5eCharacter> characters, CancellationToken cancellationToken = default);

    /// <summary>
    /// After a sheet edit, an inventory change that can affect the sheet (equip, attune, remove), a level-up or on
    /// creation: calculates the sheet, regenerates the automatic resources (class and chosen options) and
    /// refreshes the current hit points (capped at the new maximum). Returns the new sheet.
    /// </summary>
    Task<CharacterSheet> RecalculateAsync(Dnd5eCharacter character, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks that the race, subrace, background, classes, subclasses and spells an edit would leave
    /// on the character exist in the catalog (and that subraces/subclasses belong to their parent).
    /// Throws a validation <see cref="AppException"/> otherwise.
    /// </summary>
    Task EnsureCatalogReferencesAsync(Dnd5eCharacter character, SheetEdit edit, CancellationToken cancellationToken = default);

    Task<CharacterDetailDto> BuildDetailAsync(Dnd5eCharacter character, CancellationToken cancellationToken = default);

    /// <summary>The DM's view of the given characters (loaded with every child collection), sorted by name.</summary>
    Task<IReadOnlyList<PartyMemberDto>> BuildPartyAsync(IReadOnlyList<Dnd5eCharacter> characters, CancellationToken cancellationToken = default);

    /// <summary>The D&amp;D 5e fields of the detail of a character (see <see cref="ISheetSystem.BuildDetailAsync"/>).</summary>
    Task<Dnd5eCharacterDetailDto> BuildSystemDetailAsync(Dnd5eCharacter character, CancellationToken cancellationToken = default);
}

public sealed class CharacterSheetService(
    ICatalogRepository catalog,
    IUserRepository users,
    IRestRequestRepository restRequests,
    IItemTemplateRepository itemTemplates,
    ICharacterCompanionRepository companions,
    CompanionPlanner companionPlanner,
    CharacterViews views,
    IDateTimeProvider clock) : ICharacterSheetService
{
    public async Task<CharacterSheet> CalculateAsync(Dnd5eCharacter character, CancellationToken cancellationToken = default)
    {
        var sheetCatalog = await SheetCatalog.LoadAsync(catalog, [character], includeSpells: false, cancellationToken);
        return await CalculateAsync(character, sheetCatalog, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, CharacterSheet>> CalculateManyAsync(IReadOnlyList<Dnd5eCharacter> characters, CancellationToken cancellationToken = default)
    {
        var sheetCatalog = await SheetCatalog.LoadAsync(catalog, characters, includeSpells: false, cancellationToken);
        return await CalculateManyAsync(characters, sheetCatalog, cancellationToken);
    }

    public async Task<IReadOnlyList<PartyMemberDto>> BuildPartyAsync(IReadOnlyList<Dnd5eCharacter> characters, CancellationToken cancellationToken = default)
    {
        var sheetCatalog = await SheetCatalog.LoadAsync(catalog, characters, includeSpells: false, cancellationToken);
        var sheetsById = await CalculateManyAsync(characters, sheetCatalog, cancellationToken);
        var owners = await users.GetDisplayNamesAsync(characters.Select(c => c.OwnerUserId).OfType<Guid>().Distinct().ToList(), cancellationToken);
        var pendingRests = (await restRequests.ListPendingAsync(characters.Select(c => c.Id).ToList(), cancellationToken))
            .ToDictionary(r => r.CharacterId);

        return characters
            .OrderBy(c => c.Name, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(c => c.Id)
            .Select(c =>
            {
                var sheet = sheetsById[c.Id];
                return new PartyMemberDto(
                    c.Id,
                    c.Name,
                    c.OwnerUserId,
                    c.OwnerUserId is { } owner ? owners.GetValueOrDefault(owner) : null,
                    FileUrls.For(c.Character.PortraitFileId),
                    c.OrderedClasses
                        .Select(k => new CharacterClassSummaryDto(
                            k.ClassIndex,
                            sheetCatalog.Class(k.ClassIndex)?.Name ?? k.ClassIndex,
                            sheetCatalog.Subclass(k.SubclassIndex)?.Name,
                            k.Level))
                        .ToList(),
                    c.TotalLevel,
                    c.HitPointsCurrent,
                    sheet.HitPointsMax,
                    c.TemporaryHitPoints,
                    sheet.ArmorClass,
                    sheet.Initiative,
                    sheet.PassivePerception,
                    sheet.Speed,
                    c.Conditions.Select(k => new CharacterConditionDto(k.Index, k.Note)).ToList(),
                    c.ExhaustionLevel,
                    c.DeathSaveSuccesses,
                    c.DeathSaveFailures,
                    c.ConcentratingOnSpellIndex,
                    c.Inspiration,
                    Enumerable.Range(1, 9)
                        .Select(level => new SpellSlotDto(level, sheet.SpellSlotMax(level), c.SpellSlotsUsed(level)))
                        .Where(slot => slot.Max > 0 || slot.Used > 0)
                        .ToList(),
                    sheet.PactMagic is { } pact
                        ? new SpellSlotDto(pact.SlotLevel, pact.Slots, c.SpellSlotsUsed(SpellSlotState.PactLevel))
                        : null,
                    pendingRests.TryGetValue(c.Id, out var rest) ? PendingRestDto.From(rest) : null,
                    c.PendingLevelUpTo,
                    c.SpellPreparationPending,
                    c.SpellPreparationReason?.ToString());
            })
            .ToList();
    }

    public async Task<CharacterSheet> RecalculateAsync(Dnd5eCharacter character, CancellationToken cancellationToken = default)
    {
        var sheetCatalog = await SheetCatalog.LoadAsync(catalog, [character], includeSpells: false, cancellationToken);

        // Fixed proficiencies and spells of the race and subrace (racial spells follow the total level).
        var subrace = sheetCatalog.Subrace(character.SubraceIndex);
        character.SyncRaceGrants(
            sheetCatalog.RaceGrants(character.RaceIndex),
            subrace is not null && subrace.RaceIndex == character.RaceIndex ? subrace.Grants : null,
            clock.UtcNow);

        var sheet = await CalculateAsync(character, sheetCatalog, cancellationToken);
        character.SyncAutoResources(AutoResourceTemplates(character, sheet));
        character.RefreshHitPoints(sheet.HitPointsMax);
        return sheet;
    }

    public async Task EnsureCatalogReferencesAsync(Dnd5eCharacter character, SheetEdit edit, CancellationToken cancellationToken = default)
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

    public Task<CharacterDetailDto> BuildDetailAsync(Dnd5eCharacter character, CancellationToken cancellationToken = default) =>
        views.BuildDetailAsync(new CharacterRef(character.Character, character), cancellationToken);

    public async Task<Dnd5eCharacterDetailDto> BuildSystemDetailAsync(Dnd5eCharacter character, CancellationToken cancellationToken = default)
    {
        var sheetCatalog = await SheetCatalog.LoadAsync(catalog, [character], includeSpells: true, cancellationToken);
        var sheet = await CalculateAsync(character, sheetCatalog, cancellationToken);
        var pendingRest = (await restRequests.ListPendingAsync([character.Id], cancellationToken)).FirstOrDefault();
        var templates = await InventoryView.LoadTemplatesAsync(itemTemplates, character.Items.Select(i => i.TemplateId), cancellationToken);
        var autoTemplates = AutoResourceTemplates(character, sheet)
            .GroupBy(t => t.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.MaxBy(t => t.Max)!, StringComparer.Ordinal);
        var optionCosts = OptionCosts.ForCharacter(character, sheetCatalog.Option);
        var resources = character.Resources
            .OrderBy(r => r.IsAuto ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .Select(r => CharacterResourceDto.From(r, r.IsAuto && r.Key is { } key ? autoTemplates.GetValueOrDefault(key) : null) with
            {
                Options = r.Key is null ? [] : optionCosts.Where(c => c.Resource == r.Key).ToList(),
            })
            .ToList();
        var classes = character.OrderedClasses
            .Select(c => new CharacterClassDto(
                c.ClassIndex,
                sheetCatalog.Class(c.ClassIndex)?.Name ?? c.ClassIndex,
                c.SubclassIndex,
                sheetCatalog.Subclass(c.SubclassIndex)?.Name,
                c.Level,
                c.Order,
                sheetCatalog.Class(c.ClassIndex) is null || (c.SubclassIndex is not null && sheetCatalog.Subclass(c.SubclassIndex) is null)))
            .ToList();
        var spells = character.Spells
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
                s.Definition?.Level,
                s.Definition is null,
                s.Definition?.Category.ToString()))
            .ToList();
        var raceMissing = (character.RaceIndex is not null && sheetCatalog.Race(character.RaceIndex) is null)
            || (character.SubraceIndex is not null && sheetCatalog.Subrace(character.SubraceIndex) is null);
        var backgroundMissing = character.BackgroundIndex is not null && sheetCatalog.Background(character.BackgroundIndex) is null;
        var companionGrant = sheetCatalog.Companion(character);
        var companion = await companions.GetByCharacterAsync(character.Id, cancellationToken);

        return new Dnd5eCharacterDetailDto
        {
            RaceIndex = character.RaceIndex,
            RaceName = sheetCatalog.Race(character.RaceIndex)?.Name,
            SubraceIndex = character.SubraceIndex,
            SubraceName = sheetCatalog.Subrace(character.SubraceIndex)?.Name,
            BackgroundIndex = character.BackgroundIndex,
            BackgroundName = sheetCatalog.Background(character.BackgroundIndex)?.Name,
            RaceCatalogMissing = raceMissing,
            BackgroundCatalogMissing = backgroundMissing,
            CatalogMissing = raceMissing || backgroundMissing || classes.Any(c => c.CatalogMissing) || spells.Any(s => s.CatalogMissing),
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
            HitDiceUsed = character.HitDiceUsed,
            BackgroundDetail = character.BackgroundDetail,
            Classes = classes,
            Proficiencies = character.Proficiencies
                .OrderBy(p => p.Type)
                .ThenBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new CharacterProficiencyDto(p.Id, p.Type.ToString(), p.Key, p.Expertise, p.Source.ToString()))
                .ToList(),
            Spells = spells,
            Overrides = character.Overrides
                .OrderBy(o => o.Field, StringComparer.Ordinal)
                .Select(o => new CharacterOverrideDto(o.Field, o.Value, o.Note))
                .ToList(),
            Resources = resources,
            SpellSlots = SpellSlots(character, sheet),
            Sheet = ToDto(sheet),
            Inventory = InventoryView.Build(character.Character, templates, Dnd5eInventory.CarryingCapacity(sheet.Abilities[Abilities.Str].Score)),
            Combat = CombatSummaryBuilder.Build(character, sheet, sheetCatalog, templates, resources),
            PendingRest = pendingRest is null ? null : PendingRestDto.From(pendingRest),
            PendingLevelUpTo = character.PendingLevelUpTo,
            SpellPreparationPending = character.SpellPreparationPending,
            SpellPreparationReason = character.SpellPreparationReason?.ToString(),
            RestRollsPending = character.RestRollsPending,
            InvalidChoices = ChoiceValidity.Find(character, sheet, sheetCatalog.Option).Select(InvalidChoicesPlanner.ToDto).ToList(),
            Choices = character.Choices
                .Where(c => c.Key != CharacterChoice.HitPointsKey && !(c.IsOrigin && OriginChoiceKeys.IsGrant(c.Key)))
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Level)
                .Select(CharacterChoiceDto.From)
                .ToList(),
            Feats = character.Choices
                .Where(c => c.Selection.Feat is not null)
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Level)
                .Select(c => CharacterFeatDto.From(c, sheetCatalog.Option(c.Selection.Feat!.Index)))
                .ToList(),
            OptionCosts = optionCosts,
            Companion = companion is null ? null : companionPlanner.BuildDto(companion, companionGrant, sheet.ProficiencyBonus),
            CompanionFeature = companionGrant is null ? null : CompanionPlanner.FeatureDto(companionGrant),
            CompanionPending = companionGrant is not null && companion is null,
        };
    }

    /// <summary>
    /// The gear comes from the loaded inventory (<see cref="Character.Items"/>), not from the database, so
    /// that unsaved changes (equip, attune, remove) are already reflected before saving.
    /// </summary>
    private async Task<CharacterSheet> CalculateAsync(Dnd5eCharacter character, SheetCatalog sheetCatalog, CancellationToken cancellationToken)
    {
        var equipped = character.Items.Where(i => i.Equipped).ToList();
        var templates = await InventoryView.LoadTemplatesAsync(itemTemplates, equipped.Select(i => i.TemplateId), cancellationToken);
        return SheetCalculator.Calculate(sheetCatalog.InputFor(character, Dnd5eInventory.Gear(equipped, templates)));
    }

    /// <summary>Sheets of several characters with their inventories loaded, with one template query for all.</summary>
    private async Task<IReadOnlyDictionary<Guid, CharacterSheet>> CalculateManyAsync(
        IReadOnlyList<Dnd5eCharacter> characters,
        SheetCatalog sheetCatalog,
        CancellationToken cancellationToken)
    {
        var templates = await InventoryView.LoadTemplatesAsync(
            itemTemplates,
            characters.SelectMany(c => c.Items).Where(i => i.Equipped).Select(i => i.TemplateId),
            cancellationToken);
        return characters.ToDictionary(
            c => c.Id,
            c => SheetCalculator.Calculate(sheetCatalog.InputFor(c, Dnd5eInventory.Gear(c.Items.Where(i => i.Equipped), templates))));
    }

    /// <summary>Levels with slots (or spent ones), pact slots first as level 0.</summary>
    private static List<SpellSlotDto> SpellSlots(Dnd5eCharacter character, CharacterSheet sheet) =>
        Enumerable.Range(SpellSlotState.PactLevel, 10)
            .Select(level => new SpellSlotDto(level, sheet.SpellSlotMax(level), character.SpellSlotsUsed(level)))
            .Where(s => s.Max > 0 || s.Used > 0)
            .ToList();

    /// <summary>Templates of the automatic resources: SRD class resources and those of chosen options and subclass features.</summary>
    private static List<ResourceTemplate> AutoResourceTemplates(Dnd5eCharacter character, CharacterSheet sheet) =>
        [.. ClassResourceRules.ForClasses(character.Classes, sheet.AbilityModifiers), .. sheet.ChoiceResources];

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
        sheet.Spellcasting.Select(s => new SpellcastingDto(s.ClassIndex, s.Ability, s.SaveDc, s.AttackBonus, s.PreparedMax) { SpellsKnownMax = s.SpellsKnownMax, CantripsKnownMax = s.CantripsKnownMax }).ToList(),
        sheet.PactMagic?.SlotLevel,
        sheet.OverriddenFields,
        sheet.ItemEffects.Select(e => new ItemEffectDto(e.ItemName, e.Kind.ToString(), e.Target, e.Value)).ToList(),
        sheet.Breakdowns.ToDictionary(b => b.Key, b => ValueBreakdownDto.From(b.Value), StringComparer.Ordinal))
    {
        Resistances = sheet.Resistances.Select(r => new ResistanceDto(r.DamageType, r.Source, r.Label)).ToList(),
        BreathWeapon = sheet.BreathWeapon is { } b ? new BreathWeaponValueDto(b.Name, b.Source, b.DamageType, b.Dice, b.SaveAbility, b.Area, b.Dc) : null,
    };
}
