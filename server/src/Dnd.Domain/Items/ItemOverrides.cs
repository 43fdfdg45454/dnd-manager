using Dnd.Domain.Catalog;
using Dnd.Domain.Common;

namespace Dnd.Domain.Items;

/// <summary>
/// Optional fields that replace those of the item template when the effective item is resolved
/// (<see cref="EffectiveItem.Resolve"/>). A null field keeps the template's value; an item without
/// template takes every field from here. Stored as an owned type of <see cref="CharacterItem"/> and
/// <see cref="ShopItem"/>: every owner needs its own instance (use <see cref="Copy"/>), never a shared one.
/// </summary>
public sealed class ItemOverrides
{
    public string? Name { get; init; }

    public IReadOnlyList<string>? Description { get; init; }

    public ItemCategory? Category { get; init; }

    public string? DamageDice { get; init; }

    public string? DamageType { get; init; }

    public string? VersatileDice { get; init; }

    public IReadOnlyList<string>? Properties { get; init; }

    public int? RangeNormal { get; init; }

    public int? RangeLong { get; init; }

    public int? ArmorClassBase { get; init; }

    public bool? AddDexModifier { get; init; }

    public int? MaxDexBonus { get; init; }

    public int? StrengthMinimum { get; init; }

    public bool? StealthDisadvantage { get; init; }

    public decimal? WeightLb { get; init; }

    public ItemRarity? Rarity { get; init; }

    public bool? RequiresAttunement { get; init; }

    public int? AttackBonus { get; init; }

    public int? DamageBonus { get; init; }

    /// <summary>Free-text effects, e.g. "+1 a ataque y daño", "Luz 20 ft".</summary>
    public IReadOnlyList<string>? Effects { get; init; }

    /// <summary>
    /// Structured modifiers that replace the template's: null keeps the template's, an empty list
    /// removes them all.
    /// </summary>
    public IReadOnlyList<ItemModifier>? Modifiers { get; init; }

    /// <summary>True when no field is overridden.</summary>
    public bool IsEmpty =>
        Name is null && Description is null && Category is null && DamageDice is null && DamageType is null
        && VersatileDice is null && Properties is null && RangeNormal is null && RangeLong is null
        && ArmorClassBase is null && AddDexModifier is null && MaxDexBonus is null && StrengthMinimum is null
        && StealthDisadvantage is null && WeightLb is null && Rarity is null && RequiresAttunement is null
        && AttackBonus is null && DamageBonus is null && Effects is null && Modifiers is null;

    /// <summary>A new instance without overridden fields.</summary>
    public static ItemOverrides None() => new();

    /// <summary>An independent copy (owned instances cannot be shared between owners).</summary>
    public ItemOverrides Copy() => new()
    {
        Name = Name,
        Description = Description?.ToArray(),
        Category = Category,
        DamageDice = DamageDice,
        DamageType = DamageType,
        VersatileDice = VersatileDice,
        Properties = Properties?.ToArray(),
        RangeNormal = RangeNormal,
        RangeLong = RangeLong,
        ArmorClassBase = ArmorClassBase,
        AddDexModifier = AddDexModifier,
        MaxDexBonus = MaxDexBonus,
        StrengthMinimum = StrengthMinimum,
        StealthDisadvantage = StealthDisadvantage,
        WeightLb = WeightLb,
        Rarity = Rarity,
        RequiresAttunement = RequiresAttunement,
        AttackBonus = AttackBonus,
        DamageBonus = DamageBonus,
        Effects = Effects?.ToArray(),
        Modifiers = Modifiers?.ToArray(),
    };

    /// <summary>
    /// Validated copy: strings trimmed (blank → not overridden), list entries trimmed and blank ones
    /// dropped (an empty list → not overridden), modifiers normalized (an empty list of modifiers stays:
    /// it removes the template's). Throws a <see cref="DomainException"/> when a value is out of range.
    /// </summary>
    public ItemOverrides Normalize()
    {
        var name = Text(Name, ItemLimits.NameMaxLength, "El nombre");
        var damageDice = Text(DamageDice, ItemLimits.DiceMaxLength, "El dado de daño");
        var damageType = Text(DamageType, ItemLimits.DamageTypeMaxLength, "El tipo de daño");
        var versatileDice = Text(VersatileDice, ItemLimits.DiceMaxLength, "El dado de daño a dos manos");
        if (Category is { } category && !Enum.IsDefined(category))
        {
            throw DomainException.RuleViolation("La categoría del objeto no es válida.");
        }

        if (Rarity is { } rarity && !Enum.IsDefined(rarity))
        {
            throw DomainException.RuleViolation("La rareza del objeto no es válida.");
        }

        Range(RangeNormal, 0, ItemLimits.MaxRange, "El alcance normal");
        Range(RangeLong, 0, ItemLimits.MaxRange, "El alcance largo");
        Range(ArmorClassBase, 0, ItemLimits.MaxArmorClass, "La CA base");
        Range(MaxDexBonus, 0, ItemLimits.MaxDexBonus, "El máximo de Destreza");
        Range(StrengthMinimum, 0, ItemLimits.MaxStrengthMinimum, "La Fuerza mínima");
        Range(AttackBonus, ItemLimits.MinBonus, ItemLimits.MaxBonus, "El bono de ataque");
        Range(DamageBonus, ItemLimits.MinBonus, ItemLimits.MaxBonus, "El bono de daño");
        if (WeightLb is < 0 or > ItemLimits.MaxWeightLb)
        {
            throw DomainException.RuleViolation($"El peso debe estar entre 0 y {ItemLimits.MaxWeightLb} lb.");
        }

        var modifiers = ItemModifier.NormalizeAll(Modifiers);

        return new ItemOverrides
        {
            Name = name,
            Description = List(Description, ItemLimits.DescriptionEntryMaxLength, "La descripción"),
            Category = Category,
            DamageDice = damageDice,
            DamageType = damageType,
            VersatileDice = versatileDice,
            Properties = List(Properties, ItemLimits.PropertyMaxLength, "Las propiedades"),
            RangeNormal = RangeNormal,
            RangeLong = RangeLong,
            ArmorClassBase = ArmorClassBase,
            AddDexModifier = AddDexModifier,
            MaxDexBonus = MaxDexBonus,
            StrengthMinimum = StrengthMinimum,
            StealthDisadvantage = StealthDisadvantage,
            WeightLb = WeightLb,
            Rarity = Rarity,
            RequiresAttunement = RequiresAttunement,
            AttackBonus = AttackBonus,
            DamageBonus = DamageBonus,
            Effects = List(Effects, ItemLimits.EffectMaxLength, "Los efectos"),
            Modifiers = modifiers,
        };
    }

    private static string? Text(string? value, int maxLength, string label)
    {
        var trimmed = value?.Trim();
        if (trimmed is { Length: > 0 } && trimmed.Length > maxLength)
        {
            throw DomainException.RuleViolation($"{label} no puede superar los {maxLength} caracteres.");
        }

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static IReadOnlyList<string>? List(IReadOnlyList<string>? values, int entryMaxLength, string label)
    {
        if (values is null)
        {
            return null;
        }

        var result = values.Select(v => v?.Trim()).Where(v => !string.IsNullOrEmpty(v)).Cast<string>().ToArray();
        if (result.Length > ItemLimits.MaxListEntries || result.Any(v => v.Length > entryMaxLength))
        {
            throw DomainException.RuleViolation(
                $"{label} admiten como máximo {ItemLimits.MaxListEntries} entradas de {entryMaxLength} caracteres.");
        }

        return result.Length == 0 ? null : result;
    }

    private static void Range(int? value, int min, int max, string label)
    {
        if (value is { } v && (v < min || v > max))
        {
            throw DomainException.RuleViolation($"{label} debe estar entre {min} y {max}.");
        }
    }
}
