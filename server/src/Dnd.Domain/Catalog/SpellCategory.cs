namespace Dnd.Domain.Catalog;

/// <summary>What a spell is mainly for; the app shows it as an icon next to the spell name.</summary>
public enum SpellCategory
{
    Utility,
    Healing,
    Damage,
    Control,
    Buff,
    Defense,
    Summoning,
}

public static class SpellCategories
{
    /// <summary>Longest category name, for the database column.</summary>
    public const int MaxLength = 16;

    /// <summary>
    /// Default category from the spell data: healing at slot level → <see cref="SpellCategory.Healing"/>; damage →
    /// <see cref="SpellCategory.Damage"/>; a saving throw without damage → <see cref="SpellCategory.Control"/>;
    /// anything else → <see cref="SpellCategory.Utility"/>.
    /// </summary>
    public static SpellCategory Derive(bool heals, bool dealsDamage, bool hasSavingThrow) =>
        heals ? SpellCategory.Healing
        : dealsDamage ? SpellCategory.Damage
        : hasSavingThrow ? SpellCategory.Control
        : SpellCategory.Utility;

    /// <summary>Parses a category name (case-insensitive); null when it is not one.</summary>
    public static SpellCategory? TryParse(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !int.TryParse(value, out _)
        && Enum.TryParse<SpellCategory>(value.Trim(), ignoreCase: true, out var category)
        && Enum.IsDefined(category)
            ? category
            : null;
}
