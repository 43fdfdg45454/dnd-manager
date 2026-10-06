using Dnd.Domain.Catalog;

namespace Dnd.Domain.Items;

/// <summary>Limits shared by item templates, overrides, inventories and shops.</summary>
public static class ItemLimits
{
    public const int NameMaxLength = ItemTemplate.NameMaxLength;
    public const int SubcategoryMaxLength = 100;
    public const int DiceMaxLength = 32;
    public const int DamageTypeMaxLength = 32;
    public const int MaxListEntries = 50;
    public const int PropertyMaxLength = 100;
    public const int DescriptionEntryMaxLength = 5000;
    public const int EffectMaxLength = 500;
    public const int MaxRange = 10_000;
    public const int MaxArmorClass = 30;
    public const int MaxDexBonus = 10;
    public const int MaxStrengthMinimum = 30;
    public const decimal MaxWeightLb = 100_000m;
    public const int MinBonus = -20;
    public const int MaxBonus = 20;
    public const int MaxCostCp = 1_000_000_000;

    /// <summary>Range of the value of an <see cref="ItemModifier"/> (except <see cref="ItemModifierKind.AbilitySet"/>).</summary>
    public const int MinModifier = -10;

    public const int MaxModifier = 30;

    /// <summary>Lowest score an <see cref="ItemModifierKind.AbilitySet"/> modifier can set (the highest is <see cref="MaxModifier"/>).</summary>
    public const int MinAbilitySet = 1;

    /// <summary>Modifiers of one item.</summary>
    public const int MaxModifiers = 10;

    public const int ModifierTargetMaxLength = 64;

    /// <summary>Maximum quantity of one inventory entry or one shop operation.</summary>
    public const int MaxQuantity = 100_000;

    public const int MaxCharges = 999;
    public const int MaxStock = 100_000;
    public const int NotesMaxLength = 2000;

    /// <summary>Attuned items a character can have at the same time (SRD rule).</summary>
    public const int MaxAttunedItems = 3;
}
