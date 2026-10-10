using System.Text.RegularExpressions;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>An option applied to an inventory entry, with its effects and its modifiers of item kinds.</summary>
public sealed record AppliedEnhancement(string Index, string Name, ItemEffects Effects, IReadOnlyList<ItemModifier> Modifiers);

/// <summary>A bonus of an enhancement as an item modifier with the label the breakdowns show ("Filo (Espada larga)").</summary>
public sealed record EnhancementBonus(string Label, ItemModifier Modifier);

/// <summary>An item with its enhancements applied: the changed item and the bonuses they add while it is active.</summary>
public sealed record EnhancedItem(EffectiveItem Item, IReadOnlyList<EnhancementBonus> Bonuses, IReadOnlyList<AppliedEnhancement> Enhancements)
{
    public static EnhancedItem Plain(EffectiveItem item) => new(item, [], []);
}

/// <summary>
/// Applies the enhancements of one inventory entry (phase 35B3): properties gained, damage die steps and type, and the
/// bonuses to attack, damage and armor class (plus the options' item modifiers), each labeled with the option.
/// </summary>
public static partial class ItemEnhancer
{
    /// <summary>The damage die ladder of the steps: 1 → 1d4 → 1d6 → 1d8 → 1d10 → 1d12 → 2d8 → 2d10 → 2d12 (2d6 counts as 1d12).</summary>
    public static IReadOnlyList<string> DamageSteps { get; } = ["1", "1d4", "1d6", "1d8", "1d10", "1d12", "2d8", "2d10", "2d12"];

    public static EnhancedItem Apply(EffectiveItem item, IReadOnlyList<AppliedEnhancement> enhancements)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(enhancements);
        var applicable = enhancements.Where(e => e.Effects.AppliesToItem(item)).ToList();
        if (applicable.Count == 0)
        {
            return EnhancedItem.Plain(item);
        }

        var properties = item.Properties.ToList();
        foreach (var property in applicable.SelectMany(e => e.Effects.AddProperties))
        {
            if (!properties.Contains(property, StringComparer.OrdinalIgnoreCase))
            {
                properties.Add(property);
            }
        }

        var steps = applicable.Sum(e => e.Effects.DamageStep);
        var damageType = applicable.Select(e => e.Effects.DamageType).LastOrDefault(t => t is not null);
        var changed = item with
        {
            Properties = properties,
            DamageDice = StepDamage(item.DamageDice, steps),
            VersatileDice = StepDamage(item.VersatileDice, steps),
            DamageType = damageType ?? item.DamageType,
        };

        var bonuses = new List<EnhancementBonus>();
        foreach (var enhancement in applicable)
        {
            var label = $"{enhancement.Name} ({item.Name})";
            void Add(ItemModifierKind kind, int value)
            {
                if (value != 0)
                {
                    bonuses.Add(new EnhancementBonus(label, new ItemModifier(kind, null, value)));
                }
            }

            Add(ItemModifierKind.AttackBonus, enhancement.Effects.AttackBonus);
            Add(ItemModifierKind.DamageBonus, enhancement.Effects.DamageBonus);
            Add(ItemModifierKind.ArmorClassBonus, enhancement.Effects.ArmorClassBonus);
            bonuses.AddRange(enhancement.Modifiers.Where(m => ItemModifier.IsItemKind(m.Kind)).Select(m => new EnhancementBonus(label, m)));
        }

        return new EnhancedItem(changed, bonuses, applicable);
    }

    /// <summary>
    /// The damage dice <paramref name="steps"/> steps up or down the ladder of <see cref="DamageSteps"/>, kept between its ends;
    /// dice off the ladder (and no dice) are returned unchanged.
    /// </summary>
    public static string? StepDamage(string? dice, int steps)
    {
        if (string.IsNullOrWhiteSpace(dice) || steps == 0)
        {
            return dice;
        }

        var normalized = DiceSpaces().Replace(dice, string.Empty).ToLowerInvariant();
        var index = normalized == "2d6" ? 5 : DamageSteps.ToList().IndexOf(normalized);
        return index < 0 ? dice : DamageSteps[Math.Clamp(index + steps, 0, DamageSteps.Count - 1)];
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex DiceSpaces();
}

/// <summary>The item enhancement feature a character has reached, with the level of its class.</summary>
public sealed record ItemEnhancementGrant(FeatureDefinition Feature, ItemEnhancementRule Rule, int ClassLevel)
{
    public int MaxPerItem => Rule.MaxPerItemAt(ClassLevel);
}

public static class ItemEnhancementGrants
{
    /// <summary>
    /// The item enhancement feature of the character's classes or subclasses whose level it has reached (the highest one
    /// when several apply); null when none.
    /// </summary>
    public static ItemEnhancementGrant? Find(Dnd5eCharacter character, IEnumerable<FeatureDefinition> features)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(features);
        return features
            .Select(f => (Feature: f, Rule: f.ItemEnhancement, Owner: character.Classes.FirstOrDefault(c => c.ClassIndex == f.ClassIndex && (f.SubclassIndex is null || c.SubclassIndex == f.SubclassIndex))))
            .Where(f => f.Rule is not null && f.Owner is not null && f.Feature.Level <= f.Owner.Level)
            .OrderByDescending(f => f.Feature.Level)
            .ThenBy(f => f.Feature.Index, StringComparer.Ordinal)
            .Select(f => new ItemEnhancementGrant(f.Feature, f.Rule!, f.Owner!.Level))
            .FirstOrDefault();
    }
}
