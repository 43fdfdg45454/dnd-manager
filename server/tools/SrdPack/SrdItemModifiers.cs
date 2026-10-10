using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Tools.SrdPack;

/// <summary>
/// Structured modifiers of the SRD items whose effect is a plain number (the dataset only has text).
/// Applied by <see cref="SrdDataset"/> by item index; indexes missing from the dataset are ignored.
/// Conditional effects (e.g. Bracers of Defense only without armor or shield) are simplified to the
/// bonus; the description keeps the full rule.
/// </summary>
internal static class SrdItemModifiers
{
    private static readonly Dictionary<string, IReadOnlyList<ItemModifier>> ByIndex = new(StringComparer.Ordinal)
    {
        ["gauntlets-of-ogre-power"] = [Set("str", 19)],
        ["headband-of-intellect"] = [Set("int", 19)],
        ["amulet-of-health"] = [Set("con", 19)],
        ["belt-of-giant-strength-hill"] = [Set("str", 21)],
        ["belt-of-giant-strength-stone"] = [Set("str", 23)],
        ["belt-of-giant-strength-frost"] = [Set("str", 23)],
        ["belt-of-giant-strength-fire"] = [Set("str", 25)],
        ["belt-of-giant-strength-cloud"] = [Set("str", 27)],
        ["belt-of-giant-strength-storm"] = [Set("str", 29)],
        ["cloak-of-protection"] = [ArmorClass(1), AllSaves(1)],
        ["ring-of-protection"] = [ArmorClass(1), AllSaves(1)],
        ["bracers-of-defense"] = [ArmorClass(2)],
        ["weapon-1"] = Weapon(1),
        ["weapon-2"] = Weapon(2),
        ["weapon-3"] = Weapon(3),
        ["armor-1"] = [ArmorClass(1)],
        ["armor-2"] = [ArmorClass(2)],
        ["armor-3"] = [ArmorClass(3)],
    };

    /// <summary>Indexes with modifiers (for tests and diagnostics).</summary>
    public static IReadOnlyCollection<string> Indexes => ByIndex.Keys;

    /// <summary>Modifiers of an SRD item; empty when it has none.</summary>
    public static IReadOnlyList<ItemModifier> For(string? index) =>
        index is not null && ByIndex.TryGetValue(index, out var modifiers) ? modifiers : [];

    private static ItemModifier Set(string ability, int score) => new(ItemModifierKind.AbilitySet, ability, score);

    private static ItemModifier ArmorClass(int bonus) => new(ItemModifierKind.ArmorClassBonus, null, bonus);

    private static ItemModifier AllSaves(int bonus) => new(ItemModifierKind.SaveBonus, null, bonus);

    private static ItemModifier[] Weapon(int bonus) =>
        [new(ItemModifierKind.AttackBonus, null, bonus), new(ItemModifierKind.DamageBonus, null, bonus)];
}
