using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Application.Characters;

/// <summary>Builds the <see cref="CombatSummaryDto"/> of a character from its calculated sheet and inventory.</summary>
public static class CombatSummaryBuilder
{
    private const string Barbarian = "barbarian";
    private const string Wizard = "wizard";
    private const string Druid = "druid";
    private const string Paladin = "paladin";

    /// <summary>Reckless Attack is gained at barbarian level 2.</summary>
    private const int RecklessAttackLevel = 2;

    /// <param name="catalog">Catalog with the character's spells loaded (spell levels for the wizard panel).</param>
    /// <param name="templates">Templates of the character's inventory entries.</param>
    public static CombatSummaryDto Build(
        Dnd5eCharacter character,
        CharacterSheet sheet,
        SheetCatalog catalog,
        IReadOnlyDictionary<Guid, ItemTemplate> templates,
        IReadOnlyList<CharacterResourceDto> resources)
    {
        var items = character.Items
            .Select(i => (Entry: i, Template: InventoryView.TemplateOf(templates, i.TemplateId), Effective: InventoryView.Resolve(templates, i)))
            .OrderBy(i => i.Entry.SortOrder)
            .ThenBy(i => i.Effective.Name, StringComparer.InvariantCultureIgnoreCase)
            .ThenBy(i => i.Entry.Id)
            .ToList();
        var equipped = items.Where(i => i.Entry.Equipped).ToList();

        var attacks = CombatCalculator.Attacks(
                character,
                sheet,
                equipped.Select(i => new EquippedWeapon(i.Entry.Id, i.Template?.Index, i.Effective, i.Entry.Attuned)))
            .Select(a => new AttackDto(
                a.ItemId, a.Name, a.AttackBonus, a.Damage, a.DamageType, a.VersatileDamage, a.Range, a.Properties, a.Notes,
                ValueBreakdownDto.From(a.AttackBreakdown), ValueBreakdownDto.From(a.DamageBreakdown)))
            .ToList();

        var spellSlots = Enumerable.Range(1, 9)
            .Select(level => new SpellSlotDto(level, sheet.SpellSlotMax(level), character.SpellSlotsUsed(level)))
            .Where(s => s.Max > 0 || s.Used > 0)
            .ToList();
        var pactSlots = sheet.PactMagic is { } pact
            ? new SpellSlotDto(pact.SlotLevel, pact.Slots, character.SpellSlotsUsed(SpellSlotState.PactLevel))
            : null;

        var quickConsumables = items
            .Where(i => i.Effective.IsConsumable)
            .Select(i => new QuickConsumableDto(i.Entry.Id, i.Effective.Name, i.Entry.Quantity, i.Entry.Charges))
            .ToList();

        var gear = EquippedGear.FromEquipped(equipped.Select(i => (i.Effective, i.Entry.Attuned)));
        var classPanels = character.OrderedClasses
            .Select(c => Panel(character, sheet, catalog, c, spellSlots, gear))
            .OfType<ClassPanelDto>()
            .ToList();

        var onceSinceLongRest = character.Resources
            .Where(r => r.Recharge == ResourceRecharge.LongRest && r.Max == 1)
            .OrderBy(r => r.IsAuto ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .Select(r => new OnceSinceLongRestDto(r.Key, r.Name, r.Used >= r.Max))
            .ToList();

        return new CombatSummaryDto(attacks, spellSlots, pactSlots, resources, quickConsumables, classPanels, onceSinceLongRest);
    }

    private static ClassPanelDto? Panel(
        Dnd5eCharacter character,
        CharacterSheet sheet,
        SheetCatalog catalog,
        CharacterClassLevel characterClass,
        IReadOnlyList<SpellSlotDto> spellSlots,
        EquippedGear gear)
    {
        var level = characterClass.Level;
        object? data = characterClass.ClassIndex switch
        {
            Barbarian => new BarbarianPanelData(
                CombatCalculator.RageDamageBonus(level),
                Uses(character, ClassResourceRules.Rage),
                level >= RecklessAttackLevel,
                CombatCalculator.BrutalCriticalDice(level),
                10 + sheet.Modifier(Abilities.Dex) + sheet.Modifier(Abilities.Con) + (gear.HasShield ? gear.ShieldArmorClass : 0)
                    + gear.Modifiers.Where(m => m.Modifier.Kind == ItemModifierKind.ArmorClassBonus).Sum(m => m.Modifier.Value)),
            Wizard => WizardPanel(character, sheet, catalog, level),
            Paladin => PaladinPanel(character, spellSlots, level),
            Druid => DruidPanel(character, level),
            _ => null,
        };

        return data is null ? null : new ClassPanelDto(characterClass.ClassIndex, level, data);
    }

    private static WizardPanelData WizardPanel(Dnd5eCharacter character, CharacterSheet sheet, SheetCatalog catalog, int level)
    {
        // Cantrips are not part of the spellbook nor count as prepared spells.
        var spells = character.Spells
            .Where(s => s.ClassIndex == Wizard && catalog.Spell(s.SpellIndex)?.Level != 0)
            .OrderBy(s => catalog.Spell(s.SpellIndex)?.Level ?? int.MaxValue)
            .ThenBy(s => s.SpellIndex, StringComparer.Ordinal)
            .ToList();
        var arcaneRecovery = character.Resources.FirstOrDefault(r => r.IsAuto && r.Key == ClassResourceRules.ArcaneRecovery);

        return new WizardPanelData(
            spells.Select(s => s.SpellIndex).ToList(),
            spells.Where(s => s.IsPrepared).Select(s => s.SpellIndex).ToList(),
            sheet.Spellcasting.FirstOrDefault(s => s.ClassIndex == Wizard)?.PreparedMax ?? 0,
            new ArcaneRecoveryPanelDto(arcaneRecovery is { } r && r.Used >= r.Max, CombatCalculator.ArcaneRecoveryLevels(level)));
    }

    /// <summary>Circle of the Land druids: Natural Recovery (null for other druids).</summary>
    private static DruidPanelData? DruidPanel(Dnd5eCharacter character, int level)
    {
        var naturalRecovery = character.Resources.FirstOrDefault(r => r.IsAuto && r.Key == ClassResourceRules.NaturalRecovery);
        return naturalRecovery is null
            ? null
            : new DruidPanelData(new ArcaneRecoveryPanelDto(naturalRecovery.Used >= naturalRecovery.Max, CombatCalculator.ArcaneRecoveryLevels(level)));
    }

    private static PaladinPanelData PaladinPanel(Dnd5eCharacter character, IReadOnlyList<SpellSlotDto> spellSlots, int level)
    {
        var pool = Uses(character, ClassResourceRules.LayOnHands);
        var smiteSlots = level >= Dnd5eCharacter.DivineSmiteMinLevel
            ? spellSlots
                .Where(s => s.Max > 0)
                .Select(s => new SmiteSlotDto(s.Level, Math.Max(0, s.Max - s.Used), CombatCalculator.DivineSmiteDice(s.Level)))
                .ToList()
            : [];

        return new PaladinPanelData(
            new LayOnHandsPanelDto(pool.Max, pool.Used),
            new DivineSmitePanelDto(smiteSlots),
            Uses(character, ClassResourceRules.ChannelDivinity),
            CombatCalculator.AuraRange(level));
    }

    private static UsesDto Uses(Dnd5eCharacter character, string key) =>
        character.Resources.FirstOrDefault(r => r.IsAuto && r.Key == key) is { } resource
            ? new UsesDto(resource.Max, resource.Used)
            : new UsesDto(0, 0);
}
