using System.Globalization;
using System.Text;
using Dnd.Domain.Catalog;
using Dnd.Domain.Items;

namespace Dnd.Domain.Characters;

/// <summary>An equipped weapon as seen by <see cref="CombatCalculator"/>.</summary>
/// <param name="ItemId">Inventory entry id.</param>
/// <param name="TemplateIndex">Dataset index of the template ("longsword"); null for homebrew or hand-made items.</param>
/// <param name="Item">The effective item (overrides applied).</param>
/// <param name="Attuned">Whether the entry is attuned: the weapon's own modifiers need it when the item requires attunement.</param>
public sealed record EquippedWeapon(Guid? ItemId, string? TemplateIndex, EffectiveItem Item, bool Attuned = false);

/// <summary>
/// One attack of the combat view. <see cref="Damage"/> reads like "1d8+3". <see cref="AttackBreakdown"/>
/// explains <see cref="AttackBonus"/> (ability, proficiency, items); <see cref="DamageBreakdown"/> the flat
/// damage bonus added to the dice (ability, items).
/// </summary>
public sealed record AttackValue(
    Guid? ItemId,
    string Name,
    int AttackBonus,
    string Damage,
    string? DamageType,
    string? VersatileDamage,
    string? Range,
    IReadOnlyList<string> Properties,
    string? Notes,
    ValueBreakdown AttackBreakdown,
    ValueBreakdown DamageBreakdown);

/// <summary>How a weapon is used, for the conditions of <see cref="FeatureModifier"/>.</summary>
/// <param name="Ranged">Ranged weapon (<see cref="CombatCalculator.IsRanged"/>).</param>
/// <param name="TwoHanded">Melee weapon with the two-handed property.</param>
/// <param name="OtherWeaponEquipped">Another weapon is equipped too.</param>
public sealed record WeaponContext(bool Ranged, bool TwoHanded, bool OtherWeaponEquipped);

/// <summary>
/// Pure combat numbers (SRD 5.1): attacks of the equipped weapons plus the unarmed strike, and the
/// class feature values shown by the class panels and used by the class actions.
/// </summary>
public static class CombatCalculator
{
    public const string UnarmedStrikeName = "Ataque sin armas";
    public const string UnarmedDamageType = "Bludgeoning";

    public const string SimpleWeapons = "simple-weapons";
    public const string MartialWeapons = "martial-weapons";

    /// <summary>Highest slot level Arcane Recovery can recover.</summary>
    public const int ArcaneRecoveryMaxSlotLevel = 5;

    /// <summary>Divine Smite deals at most 5d8 (6d8 against undead and fiends, left to the player).</summary>
    public const int DivineSmiteMaxDice = 5;

    private const string Monk = "monk";
    private const string Finesse = "finesse";
    private const string Ammunition = "ammunition";
    private const string Heavy = "heavy";
    private const string TwoHanded = "two-handed";
    private const string Shortsword = "shortsword";

    /// <summary>
    /// Attacks of the equipped weapons (in the given order) followed by the unarmed strike.
    /// Ability: Dex for ranged weapons and for finesse weapons when Dex is higher, Str otherwise.
    /// Proficiency: a weapon proficiency with the weapon (index, its plural or its name) or with its
    /// category ("simple-weapons", "martial-weapons"). Attack and damage bonuses: those of the weapon
    /// itself (when active) plus those of the other active items that are not weapons
    /// (<see cref="CharacterSheet.ItemEffects"/>), which also apply to the unarmed strike.
    /// Monks use Martial Arts with the unarmed strike and monk weapons. Rage is not added.
    /// Attack and damage bonuses of chosen options (<see cref="CharacterSheet.FeatureModifiers"/>) apply when
    /// their condition holds: <c>rangedWeapon</c> for ranged weapons, <c>oneHandedMeleeNoOtherWeapon</c> for a
    /// melee weapon without the two-handed property when no other weapon is equipped (not to the two-handed
    /// damage of a versatile weapon), <c>twoHandedMelee</c> for two-handed melee weapons and the two-handed
    /// damage of versatile ones; <c>twoWeaponFighting</c> is not calculated. Unconditional ones apply to every
    /// attack, the unarmed strike included.
    /// </summary>
    public static IReadOnlyList<AttackValue> Attacks(Character character, CharacterSheet sheet, IEnumerable<EquippedWeapon> weapons)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(weapons);

        var str = sheet.Modifier(Abilities.Str);
        var dex = sheet.Modifier(Abilities.Dex);
        var monkLevel = ClassLevel(character, Monk);
        var martialArtsDie = MartialArtsDie(monkLevel);
        var weaponKeys = character.Proficiencies
            .Where(p => p.Type == ProficiencyType.Weapon)
            .Select(p => Slug(p.Key))
            .ToHashSet(StringComparer.Ordinal);

        var globalAttack = ItemBonuses(sheet.ItemEffects.Where(e => e.Kind == ItemModifierKind.AttackBonus));
        var globalDamage = ItemBonuses(sheet.ItemEffects.Where(e => e.Kind == ItemModifierKind.DamageBonus));
        var featureAttack = sheet.FeatureModifiers.Where(m => m.Kind == ItemModifierKind.AttackBonus).ToList();
        var featureDamage = sheet.FeatureModifiers.Where(m => m.Kind == ItemModifierKind.DamageBonus).ToList();
        var equippedWeapons = weapons.Where(w => w.Item.Category == ItemCategory.Weapon).ToList();

        var attacks = new List<AttackValue>();
        foreach (var weapon in equippedWeapons)
        {
            var item = weapon.Item;
            var ranged = IsRanged(item);
            var context = new WeaponContext(ranged, !ranged && HasProperty(item, TwoHanded), equippedWeapons.Count > 1);
            var monkWeapon = martialArtsDie is not null && IsMonkWeapon(weapon);
            var useDex = IsRanged(item) || ((HasProperty(item, Finesse) || monkWeapon) && dex > str);
            var ability = useDex ? Abilities.Dex : Abilities.Str;
            var proficient = IsProficient(weaponKeys, weapon);
            var active = item.IsActive(equipped: true, weapon.Attuned);
            var ownAttack = active ? item.Modifiers.Where(m => m.Kind == ItemModifierKind.AttackBonus).Sum(m => m.Value) : 0;
            var ownDamage = active ? item.Modifiers.Where(m => m.Kind == ItemModifierKind.DamageBonus).Sum(m => m.Value) : 0;

            var attack = new BreakdownBuilder().Add(BreakdownSources.Ability, BreakdownLabels.Ability(ability), sheet.Modifier(ability));
            if (proficient)
            {
                attack.Add(BreakdownSources.Proficiency, BreakdownLabels.Proficiency, sheet.ProficiencyBonus);
            }

            var damageBonus = new BreakdownBuilder().Add(BreakdownSources.Ability, BreakdownLabels.Ability(ability), sheet.Modifier(ability));
            if (ownAttack != 0)
            {
                attack.Add(BreakdownSources.Item, item.Name, ownAttack);
            }

            if (ownDamage != 0)
            {
                damageBonus.Add(BreakdownSources.Item, item.Name, ownDamage);
            }

            attack.AddAll(globalAttack).AddAll(FeatureParts(featureAttack, context, versatileGrip: false));
            var versatileBonus = new BreakdownBuilder().AddAll(damageBonus.Build().Parts).AddAll(globalDamage)
                .AddAll(FeatureParts(featureDamage, context, versatileGrip: true));
            damageBonus.AddAll(globalDamage).AddAll(FeatureParts(featureDamage, context, versatileGrip: false));
            var dice = monkWeapon ? AtLeastDie(item.DamageDice, martialArtsDie!.Value) : item.DamageDice;

            attacks.Add(new AttackValue(
                weapon.ItemId,
                item.Name,
                attack.Total,
                string.IsNullOrWhiteSpace(dice) ? "0" : FormatDamage(dice, damageBonus.Total),
                item.DamageType,
                string.IsNullOrWhiteSpace(item.VersatileDice) ? null : FormatDamage(item.VersatileDice, versatileBonus.Total),
                FormatRange(item.RangeNormal, item.RangeLong),
                item.Properties,
                item.Effects.Count == 0 ? null : string.Join("; ", item.Effects),
                attack.Build(),
                damageBonus.Build()));
        }

        var unarmedAbility = martialArtsDie is not null && dex > str ? Abilities.Dex : Abilities.Str;
        var unarmedAttack = new BreakdownBuilder()
            .Add(BreakdownSources.Ability, BreakdownLabels.Ability(unarmedAbility), sheet.Modifier(unarmedAbility))
            .Add(BreakdownSources.Proficiency, BreakdownLabels.Proficiency, sheet.ProficiencyBonus)
            .AddAll(globalAttack)
            .AddAll(FeatureParts(featureAttack, null, versatileGrip: false));
        var unarmedDamage = new BreakdownBuilder()
            .Add(BreakdownSources.Ability, BreakdownLabels.Ability(unarmedAbility), sheet.Modifier(unarmedAbility))
            .AddAll(globalDamage)
            .AddAll(FeatureParts(featureDamage, null, versatileGrip: false));
        attacks.Add(new AttackValue(
            null,
            UnarmedStrikeName,
            unarmedAttack.Total,
            FormatDamage(martialArtsDie is { } die ? $"1d{die}" : "1", unarmedDamage.Total),
            UnarmedDamageType,
            null,
            null,
            [],
            null,
            unarmedAttack.Build(),
            unarmedDamage.Build()));

        return attacks;
    }

    /// <summary>"1d8" + 3 → "1d8+3"; 0 → "1d8"; −1 → "1d8-1".</summary>
    public static string FormatDamage(string dice, int bonus) => bonus switch
    {
        0 => dice,
        > 0 => $"{dice}+{bonus.ToString(CultureInfo.InvariantCulture)}",
        _ => $"{dice}{bonus.ToString(CultureInfo.InvariantCulture)}",
    };

    /// <summary>Martial Arts die by monk level: d4 (1), d6 (5), d8 (11), d10 (17); null without monk levels.</summary>
    public static int? MartialArtsDie(int monkLevel) => monkLevel switch
    {
        <= 0 => null,
        >= 17 => 10,
        >= 11 => 8,
        >= 5 => 6,
        _ => 4,
    };

    /// <summary>Rage damage bonus by barbarian level: +2, +3 (9), +4 (16).</summary>
    public static int RageDamageBonus(int barbarianLevel) => barbarianLevel >= 16 ? 4 : barbarianLevel >= 9 ? 3 : 2;

    /// <summary>Extra weapon dice of Brutal Critical: 1 (9), 2 (13), 3 (17); 0 before.</summary>
    public static int BrutalCriticalDice(int barbarianLevel) => barbarianLevel >= 17 ? 3 : barbarianLevel >= 13 ? 2 : barbarianLevel >= 9 ? 1 : 0;

    /// <summary>Paladin aura range in feet: 10 from level 6, 30 from level 18, 0 before.</summary>
    public static int AuraRange(int paladinLevel) => paladinLevel >= 18 ? 30 : paladinLevel >= 6 ? 10 : 0;

    /// <summary>Sum of slot levels Arcane Recovery restores: half the wizard level, rounded up.</summary>
    public static int ArcaneRecoveryLevels(int wizardLevel) => Math.Max(0, (wizardLevel + 1) / 2);

    /// <summary>Divine Smite extra damage for a slot level: 2d8 at 1st, +1d8 per level above, at most 5d8.</summary>
    public static string DivineSmiteDice(int slotLevel) =>
        $"{Math.Clamp(slotLevel + 1, 2, DivineSmiteMaxDice).ToString(CultureInfo.InvariantCulture)}d8";

    /// <summary>Level of the character in a class (0 without it).</summary>
    public static int ClassLevel(Character character, string classIndex)
    {
        ArgumentNullException.ThrowIfNull(character);
        return character.Classes.FirstOrDefault(c => c.ClassIndex == classIndex)?.Level ?? 0;
    }

    /// <summary>Ranged weapons: those with ammunition or a "Ranged" subcategory (thrown melee weapons are melee).</summary>
    public static bool IsRanged(EffectiveItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return HasProperty(item, Ammunition)
            || (item.Subcategory?.Contains("Ranged", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool IsProficient(HashSet<string> weaponKeys, EquippedWeapon weapon)
    {
        if (weaponKeys.Count == 0)
        {
            return false;
        }

        var subcategory = weapon.Item.Subcategory ?? string.Empty;
        if ((subcategory.StartsWith("Simple", StringComparison.OrdinalIgnoreCase) && weaponKeys.Contains(SimpleWeapons))
            || (subcategory.StartsWith("Martial", StringComparison.OrdinalIgnoreCase) && weaponKeys.Contains(MartialWeapons)))
        {
            return true;
        }

        return WeaponKeys(weapon).Any(weaponKeys.Contains);
    }

    /// <summary>
    /// Keys that name the weapon itself: the index and the name, singular and plural; the dataset uses
    /// plural proficiency indexes ("longswords", "crossbows-light" for "crossbow-light").
    /// </summary>
    private static IEnumerable<string> WeaponKeys(EquippedWeapon weapon)
    {
        foreach (var key in new[] { weapon.TemplateIndex, weapon.Item.Name }.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => Slug(k!)))
        {
            yield return key;
            yield return key + "s";
            var dash = key.IndexOf('-', StringComparison.Ordinal);
            if (dash > 0)
            {
                yield return key[..dash] + "s" + key[dash..];
            }
        }
    }

    /// <summary>Shortswords and simple melee weapons without the heavy or two-handed property.</summary>
    private static bool IsMonkWeapon(EquippedWeapon weapon)
    {
        var item = weapon.Item;
        if (weapon.TemplateIndex == Shortsword || Slug(item.Name) == Shortsword)
        {
            return true;
        }

        return string.Equals(item.Subcategory, "Simple Melee", StringComparison.OrdinalIgnoreCase)
            && !HasProperty(item, Heavy)
            && !HasProperty(item, TwoHanded);
    }

    /// <summary>Replaces a single die ("1d4") by the Martial Arts die when that is larger.</summary>
    private static string? AtLeastDie(string? dice, int die)
    {
        if (string.IsNullOrWhiteSpace(dice))
        {
            return $"1d{die}";
        }

        var parts = dice.Trim().Split('d');
        return parts.Length == 2 && parts[0] == "1" && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var sides) && sides < die
            ? $"1d{die}"
            : dice;
    }

    /// <summary>
    /// Whether a condition of a chosen option holds for an attack. <paramref name="weapon"/> null is the unarmed
    /// strike (only unconditional bonuses); <paramref name="versatileGrip"/> is the two-handed damage of a versatile weapon.
    /// </summary>
    public static bool ConditionHolds(string? condition, WeaponContext? weapon, bool versatileGrip) => condition switch
    {
        null => true,
        _ when weapon is null => false,
        ModifierConditions.RangedWeapon => weapon.Ranged,
        ModifierConditions.OneHandedMeleeNoOtherWeapon => !weapon.Ranged && !weapon.TwoHanded && !weapon.OtherWeaponEquipped && !versatileGrip,
        ModifierConditions.TwoHandedMelee => !weapon.Ranged && (weapon.TwoHanded || versatileGrip),
        _ => false,
    };

    /// <summary>Parts of the chosen options' bonuses whose condition holds.</summary>
    private static List<BreakdownPart> FeatureParts(IEnumerable<FeatureModifier> modifiers, WeaponContext? weapon, bool versatileGrip) =>
        modifiers
            .Where(m => ConditionHolds(m.Condition, weapon, versatileGrip))
            .Select(m => new BreakdownPart(BreakdownSources.Feature, m.Label, m.Value))
            .ToList();

    /// <summary>Attack or damage bonuses of non-weapon items, one part per item name (non-zero sums only).</summary>
    private static List<BreakdownPart> ItemBonuses(IEnumerable<AppliedItemEffect> effects) =>
        effects
            .GroupBy(e => e.ItemName, StringComparer.Ordinal)
            .Select(g => new BreakdownPart(BreakdownSources.Item, g.Key, g.Sum(e => e.Value)))
            .Where(p => p.Value != 0)
            .ToList();

    private static string? FormatRange(int? normal, int? @long) => (normal, @long) switch
    {
        ({ } n, { } l) => $"{n}/{l}",
        ({ } n, null) => n.ToString(CultureInfo.InvariantCulture),
        _ => null,
    };

    private static bool HasProperty(EffectiveItem item, string property) =>
        item.Properties.Any(p => Slug(p) == property);

    /// <summary>"Martial Weapons" → "martial-weapons"; "Crossbow, light" → "crossbow-light".</summary>
    private static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
