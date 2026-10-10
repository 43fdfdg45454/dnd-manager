using System.Text.Json;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// Everything <see cref="SheetCalculator"/> needs. Classes, proficiencies and overrides are read from
/// <see cref="Character"/> (its child collections must be loaded); the catalog data comes alongside.
/// </summary>
/// <param name="Character">The character, with classes, proficiencies and overrides loaded.</param>
/// <param name="Classes">Catalog info of (at least) every class the character has.</param>
/// <param name="Race">Race of the character, or null when it has none.</param>
/// <param name="Subrace">Subrace of the character, or null.</param>
/// <param name="Skills">The skills to list in the sheet, in display order (usually every SRD skill).</param>
/// <param name="Gear">Equipped armor, shield and active item modifiers; null means nothing equipped.</param>
/// <param name="Choices">Effects of the level choices (<see cref="ChoiceEffects.Build"/>); null means none.</param>
public sealed record SheetInput(
    Dnd5eCharacter Character,
    IReadOnlyList<ClassInfo> Classes,
    RaceInfo? Race,
    SubraceInfo? Subrace,
    IReadOnlyList<SkillInfo> Skills,
    EquippedGear? Gear = null,
    ChoiceEffects? Choices = null);

/// <summary>Catalog data of a class used by the sheet.</summary>
public sealed record ClassInfo
{
    private static readonly IReadOnlyList<int> NoSlots = new int[ClassLevel.SpellSlotLevels];

    public required string Index { get; init; }

    public int HitDie { get; init; }

    /// <summary>"int", "wis" or "cha"; null for non-casters.</summary>
    public string? SpellcastingAbility { get; init; }

    /// <summary>1 full caster, 2 half, 3 third, 0 none (see <see cref="ClassDefinition.SpellcastingLevel"/>).</summary>
    public int SpellcastingLevel { get; init; }

    public bool IsPactCaster { get; init; }

    /// <summary>Spell slots (9 entries, spell levels 1-9) by class level.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<int>> SpellSlotsByLevel { get; init; } = new Dictionary<int, IReadOnlyList<int>>();

    /// <summary>Class level from which the class casts spells (1; a subclass caster starts at its <see cref="SubclassSpellcasting.FromLevel"/>).</summary>
    public int SpellcastingFromLevel { get; init; } = 1;

    /// <summary>Spellcasting given by the subclass (content packs), or null when the class casts on its own or not at all.</summary>
    public SubclassSpellcasting? SubclassSpellcasting { get; init; }

    /// <summary>Whether the class casts spells at a class level.</summary>
    public bool CastsAt(int classLevel) => SpellcastingAbility is not null && classLevel >= SpellcastingFromLevel;

    /// <summary>
    /// The class with the spellcasting of its subclass (<paramref name="spellcasting"/>) when the class does not cast on
    /// its own: ability, progression, slots of the progression table and the spells/cantrips known tables. Unchanged
    /// otherwise.
    /// </summary>
    public ClassInfo WithSubclassSpellcasting(SubclassSpellcasting? spellcasting)
    {
        if (spellcasting is null || SpellcastingAbility is not null || SpellcastingLevel > 0)
        {
            return this;
        }

        return this with
        {
            SpellcastingAbility = spellcasting.Ability,
            SpellcastingLevel = spellcasting.SpellcastingLevel,
            SpellcastingFromLevel = spellcasting.FromLevel,
            SubclassSpellcasting = spellcasting,
            SpellSlotsByLevel = Enumerable.Range(1, 20).ToDictionary(l => l, spellcasting.SlotsAt),
        };
    }

    /// <summary>Slots of the class table at a class level; nine zeros when unknown.</summary>
    public IReadOnlyList<int> SlotsByLevel(int level) =>
        SpellSlotsByLevel.TryGetValue(level, out var slots) ? slots : NoSlots;

    /// <summary>Builds the info from the catalog class and its levels (only the class's own levels are used).</summary>
    public static ClassInfo From(ClassDefinition definition, IEnumerable<ClassLevel> levels)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(levels);

        return new ClassInfo
        {
            Index = definition.Index,
            HitDie = definition.HitDie,
            SpellcastingAbility = definition.SpellcastingAbility,
            SpellcastingLevel = definition.SpellcastingLevel,
            IsPactCaster = definition.IsPactCaster,
            SpellSlotsByLevel = levels
                .Where(l => l.ClassIndex == definition.Index)
                .GroupBy(l => l.Level)
                .ToDictionary(g => g.Key, g => g.First().SpellSlots),
        };
    }
}

/// <summary>Catalog data of a race used by the sheet.</summary>
public sealed record RaceInfo(int Speed, IReadOnlyList<AbilityBonus> AbilityBonuses)
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Decisions of the race (trait options give resistances and the breath weapon).</summary>
    public RaceChoices Choices { get; init; } = RaceChoices.None;

    /// <summary>Damage types always resisted.</summary>
    public IReadOnlyList<string> Resistances { get; init; } = [];

    /// <summary>Fixed grants of the race and of the packs that extend it (spells give the "Raza" spellcasting).</summary>
    public OptionGrants Grants { get; init; } = OptionGrants.None;

    /// <summary>Names of the granted spells (index → name) for their resources; the index is used when missing.</summary>
    public IReadOnlyDictionary<string, string> SpellNames { get; init; } = new Dictionary<string, string>();

    public static RaceInfo From(RaceDefinition definition, IEnumerable<RaceExtensionDefinition>? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new RaceInfo(definition.Speed, AbilityBonusJson.Parse(definition.AbilityBonusesJson))
        {
            Name = definition.Name,
            Choices = definition.Choices,
            Resistances = definition.Resistances,
            Grants = (extensions ?? []).Aggregate(definition.Grants, (grants, extension) => grants.Merge(extension.Grants)),
        };
    }
}

/// <summary>Catalog data of a subrace used by the sheet.</summary>
public sealed record SubraceInfo(IReadOnlyList<AbilityBonus> AbilityBonuses)
{
    public string Name { get; init; } = string.Empty;

    public RaceChoices Choices { get; init; } = RaceChoices.None;

    public IReadOnlyList<string> Resistances { get; init; } = [];

    /// <summary>Walking speed that replaces the race's, or null (most subraces do not change it).</summary>
    public int? Speed { get; init; }

    /// <summary>Fixed grants of the subrace.</summary>
    public OptionGrants Grants { get; init; } = OptionGrants.None;

    /// <summary>Names of the granted spells (index → name) for their resources.</summary>
    public IReadOnlyDictionary<string, string> SpellNames { get; init; } = new Dictionary<string, string>();

    public static SubraceInfo From(SubraceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new SubraceInfo(AbilityBonusJson.Parse(definition.AbilityBonusesJson))
        {
            Name = definition.Name,
            Choices = definition.Choices,
            Resistances = definition.Resistances,
            Speed = definition.Speed,
            Grants = definition.Grants,
        };
    }
}

/// <summary>A skill and the ability it uses ("dex" for "stealth").</summary>
public sealed record SkillInfo(string Index, string Name, string Ability)
{
    public static SkillInfo From(SkillDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new SkillInfo(definition.Index, definition.Name, definition.AbilityIndex);
    }
}

/// <summary>A modifier of an active item (equipped and, when required, attuned) as seen by the sheet.</summary>
/// <param name="ItemName">Name of the effective item, shown next to the effect.</param>
/// <param name="Modifier">The modifier.</param>
/// <param name="FromWeapon">
/// The item is a weapon: its <see cref="ItemModifierKind.AttackBonus"/>/<see cref="ItemModifierKind.DamageBonus"/>
/// apply only to its own attack (see <see cref="CombatCalculator"/>), not to the sheet.
/// </param>
public sealed record ActiveModifier(string ItemName, ItemModifier Modifier, bool FromWeapon);

/// <summary>
/// Equipped gear as seen by the sheet: the armor (<see cref="ArmorClassBase"/> null means no armor),
/// whether a shield is worn and its armor class (<see cref="ShieldArmorClass"/>), and the modifiers of
/// every active item (<see cref="Modifiers"/>). Built from the equipped inventory entries
/// (<see cref="FromEquipped(IEnumerable{ValueTuple{EffectiveItem, bool}})"/>).
/// </summary>
public sealed record EquippedGear(int? ArmorClassBase, bool AddDexModifier, int? MaxDexBonus, bool HasShield)
{
    public static EquippedGear None { get; } = new(null, false, null, false);

    public bool WearsArmor => ArmorClassBase is not null;

    /// <summary>Armor class added by the shield when <see cref="HasShield"/>: its <see cref="EffectiveItem.ArmorClassBase"/>, 2 when it has none.</summary>
    public int ShieldArmorClass { get; init; } = SheetCalculator.ShieldBonus;

    /// <summary>Modifiers of the active items, in inventory order.</summary>
    public IReadOnlyList<ActiveModifier> Modifiers { get; init; } = [];

    /// <summary>Name of the worn armor, for the armor class breakdown (null: a generic label).</summary>
    public string? ArmorName { get; init; }

    /// <summary>Name of the worn shield, for the armor class breakdown (null: a generic label).</summary>
    public string? ShieldName { get; init; }

    /// <summary>Gear from an equipped armor template (null for none) and whether a shield is equipped.</summary>
    public static EquippedGear From(ItemTemplate? armor, bool hasShield) => armor?.ArmorClassBase is { } armorClass
        ? new EquippedGear(armorClass, armor.AddDexModifier ?? false, armor.MaxDexBonus, hasShield) { ArmorName = armor.Name }
        : None with { HasShield = hasShield };

    /// <summary>Gear from an equipped effective armor (overrides applied; null for none) and whether a shield is equipped.</summary>
    public static EquippedGear From(EffectiveItem? armor, bool hasShield) => armor?.ArmorClassBase is { } armorClass
        ? new EquippedGear(armorClass, armor.AddDexModifier, armor.MaxDexBonus, hasShield) { ArmorName = armor.Name }
        : None with { HasShield = hasShield };

    /// <summary>
    /// Gear from the effective items a character has equipped, none of them attuned (items that require
    /// attunement give no modifiers). See <see cref="FromEquipped(IEnumerable{ValueTuple{EffectiveItem, bool}})"/>.
    /// </summary>
    public static EquippedGear FromEquipped(IEnumerable<EffectiveItem> equipped)
    {
        ArgumentNullException.ThrowIfNull(equipped);
        return FromEquipped(equipped.Select(i => (i, false)));
    }

    /// <summary>
    /// Gear from the effective items a character has equipped and whether each one is attuned: the first
    /// armor, the first shield (its armor class, 2 when it has none) and the modifiers of the active items.
    /// </summary>
    public static EquippedGear FromEquipped(IEnumerable<(EffectiveItem Item, bool Attuned)> equipped)
    {
        ArgumentNullException.ThrowIfNull(equipped);
        var items = equipped.ToList();
        var shield = items.Select(i => i.Item).FirstOrDefault(i => i.Category == ItemCategory.Shield);
        return From(items.Select(i => i.Item).FirstOrDefault(i => i.Category == ItemCategory.Armor), shield is not null) with
        {
            ShieldArmorClass = shield?.ArmorClassBase ?? SheetCalculator.ShieldBonus,
            ShieldName = shield?.Name,
            Modifiers = items
                .Where(i => i.Item.IsActive(equipped: true, i.Attuned))
                .SelectMany(i => i.Item.Modifiers.Select(m => new ActiveModifier(i.Item.Name, m, i.Item.Category == ItemCategory.Weapon)))
                .ToList(),
        };
    }
}

internal static class AbilityBonusJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<AbilityBonus> Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<AbilityBonus>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
