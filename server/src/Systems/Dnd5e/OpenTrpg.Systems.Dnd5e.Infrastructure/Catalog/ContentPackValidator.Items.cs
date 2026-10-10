using System.Text.Json;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// Format 3 of the items: weapon, armor, tool, ammunition and firearm sections mapped onto the 5e columns of the item
// template (the same ones the SRD items use) and, for what has no column, its system data.
internal sealed partial class ContentPackValidator
{
    public static readonly IReadOnlyList<string> WeaponCategories = ["simple", "martial", "exotic"];
    public static readonly IReadOnlyList<string> WeaponRanges = ["melee", "ranged"];
    public static readonly IReadOnlyList<string> ArmorCategories = ["light", "medium", "heavy", "shield"];

    private static readonly JsonSerializerOptions SystemDataOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The category the sections imply when <c>category</c> is left out.</summary>
    private static ItemCategory? ImpliedCategory(PackItemJson item) =>
        item.Weapon is not null ? ItemCategory.Weapon
        : item.Armor is { Category: { } armor } ? (string.Equals(armor.Trim(), "shield", StringComparison.OrdinalIgnoreCase) ? ItemCategory.Shield : ItemCategory.Armor)
        : item.Tool == true ? ItemCategory.Tool
        : item.Ammunition == true ? ItemCategory.Consumable
        : null;

    private Dictionary<string, object?> AlternateProfile(string path, PackAlternateWeaponJson alternate)
    {
        var profile = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (NullableText($"{path}.name", alternate.Name, ItemLimits.NameMaxLength) is { } name)
        {
            profile["name"] = name;
        }

        if (alternate.Range?.Trim().ToLowerInvariant() is { Length: > 0 } range)
        {
            if (WeaponRanges.Contains(range))
            {
                profile["range"] = range;
            }
            else
            {
                AddError($"{path}.range", "Debe ser \"melee\" o \"ranged\".");
            }
        }

        profile["damage"] = RequiredText($"{path}.damage", alternate.Damage, ItemLimits.DiceMaxLength);
        if (NullableText($"{path}.damageType", alternate.DamageType, ItemLimits.DamageTypeMaxLength) is { } type)
        {
            profile["damageType"] = type.ToLowerInvariant();
        }

        if (NullableText($"{path}.versatileDamage", alternate.VersatileDamage, ItemLimits.DiceMaxLength) is { } versatile)
        {
            profile["versatileDamage"] = versatile;
        }

        var properties = TextList($"{path}.properties", alternate.Properties, ItemLimits.MaxListEntries, ItemLimits.PropertyMaxLength)
            .Select(p => p.ToLowerInvariant())
            .ToList();
        if (properties.Count > 0)
        {
            profile["properties"] = properties;
        }

        if (OptionalInt($"{path}.rangeNormal", alternate.RangeNormal, 0, ItemLimits.MaxRange) is { } normal)
        {
            profile["rangeNormal"] = normal;
        }

        if (OptionalInt($"{path}.rangeLong", alternate.RangeLong, 0, ItemLimits.MaxRange) is { } far)
        {
            profile["rangeLong"] = far;
        }

        return profile;
    }

    private ItemTemplateData WithItemSections(string path, PackItemJson item, ItemTemplateData data)
    {
        var flatWeapon = item.DamageDice is not null || item.DamageType is not null || item.VersatileDice is not null
            || item.Properties is not null || item.RangeNormal is not null || item.RangeLong is not null;
        var flatArmor = item.ArmorClassBase is not null || item.AddDexModifier is not null || item.MaxDexBonus is not null
            || item.StrengthMinimum is not null || item.StealthDisadvantage is not null;
        var systemData = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (item.Weapon is { } weapon)
        {
            if (flatWeapon)
            {
                AddError($"{path}.weapon", "Usa \"weapon\" o los campos sueltos de arma (damageDice, properties...), no ambos.");
            }

            var category = weapon.Category?.Trim().ToLowerInvariant();
            if (category is null || !WeaponCategories.Contains(category))
            {
                AddError($"{path}.weapon.category", "Debe ser \"simple\", \"martial\" o \"exotic\".");
                category = "simple";
            }

            var range = weapon.Range?.Trim().ToLowerInvariant();
            if (range is null || !WeaponRanges.Contains(range))
            {
                AddError($"{path}.weapon.range", "Debe ser \"melee\" o \"ranged\".");
                range = "melee";
            }

            var damage = RequiredText($"{path}.weapon.damage", weapon.Damage, ItemLimits.DiceMaxLength);
            var properties = TextList($"{path}.weapon.properties", weapon.Properties, ItemLimits.MaxListEntries, ItemLimits.PropertyMaxLength)
                .Select(p => p.ToLowerInvariant())
                .ToList();
            if (item.Firearm is not null && !properties.Contains("firearm"))
            {
                properties.Add("firearm");
            }

            if (NullableText($"{path}.weapon.special", weapon.Special, LongTextMaxLength) is { } special)
            {
                systemData["special"] = special;
            }

            if (weapon.AddAbilityToDamage is { } addAbility)
            {
                systemData["addAbilityToDamage"] = addAbility;
            }

            if (weapon.Requires is { Count: > 0 } requires)
            {
                var minimums = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var (ability, minimum) in requires)
                {
                    var key = ability.Trim().ToLowerInvariant();
                    if (!Abilities.IsValid(key))
                    {
                        AddError($"{path}.weapon.requires.{ability}", "Característica desconocida (str, dex, con, int, wis o cha).");
                    }
                    else if (RequiredInt($"{path}.weapon.requires.{ability}", minimum, 1, 30) is { } value)
                    {
                        minimums[key] = value;
                    }
                }

                systemData["requires"] = minimums;
            }

            if (weapon.Alternate is { } alternate)
            {
                systemData["alternate"] = AlternateProfile($"{path}.weapon.alternate", alternate);
            }

            data = data with
            {
                Subcategory = data.Subcategory.Length > 0 ? data.Subcategory : $"{char.ToUpperInvariant(category[0])}{category[1..]} {char.ToUpperInvariant(range[0])}{range[1..]}",
                DamageDice = damage.Length == 0 ? null : damage,
                DamageType = RequiredText($"{path}.weapon.damageType", weapon.DamageType, ItemLimits.DamageTypeMaxLength).ToLowerInvariant() is { Length: > 0 } type ? type : null,
                VersatileDice = NullableText($"{path}.weapon.versatileDamage", weapon.VersatileDamage, ItemLimits.DiceMaxLength),
                Properties = properties,
                RangeNormal = OptionalInt($"{path}.weapon.rangeNormal", weapon.RangeNormal, 0, ItemLimits.MaxRange),
                RangeLong = OptionalInt($"{path}.weapon.rangeLong", weapon.RangeLong, 0, ItemLimits.MaxRange),
            };
        }

        if (item.Armor is { } armor)
        {
            if (flatArmor)
            {
                AddError($"{path}.armor", "Usa \"armor\" o los campos sueltos de armadura (armorClassBase...), no ambos.");
            }

            if (item.Weapon is not null)
            {
                AddError($"{path}.armor", "Un objeto no puede ser arma y armadura a la vez.");
            }

            var category = armor.Category?.Trim().ToLowerInvariant();
            if (category is null || !ArmorCategories.Contains(category))
            {
                AddError($"{path}.armor.category", $"Valores admitidos: {string.Join(", ", ArmorCategories)}.");
                category = "light";
            }

            var shield = category == "shield";
            data = data with
            {
                Subcategory = data.Subcategory.Length > 0 ? data.Subcategory : shield ? "Shield" : $"{char.ToUpperInvariant(category[0])}{category[1..]} Armor",
                ArmorClassBase = RequiredInt($"{path}.armor.baseAc", armor.BaseAc, 0, ItemLimits.MaxArmorClass),
                AddDexModifier = shield ? null : armor.DexBonus ?? category != "heavy",
                MaxDexBonus = OptionalInt($"{path}.armor.maxDexBonus", armor.MaxDexBonus, 0, ItemLimits.MaxDexBonus) ?? (category == "medium" ? 2 : null),
                StrengthMinimum = OptionalInt($"{path}.armor.strMin", armor.StrMin, 0, ItemLimits.MaxStrengthMinimum),
                StealthDisadvantage = armor.StealthDisadvantage ?? false,
            };
            if (armor.Exotic == true)
            {
                if (shield)
                {
                    AddError($"{path}.armor.exotic", "Un escudo no puede ser exótico.");
                }
                else if (!data.Properties.Contains(ProficiencyKeys.ExoticProperty))
                {
                    data = data with { Properties = [.. data.Properties, ProficiencyKeys.ExoticProperty] };
                }
            }
        }

        if (item.Tool == true)
        {
            systemData["tool"] = true;
        }

        if (item.Ammunition == true)
        {
            systemData["ammunition"] = true;
            data = data with { Subcategory = data.Subcategory.Length > 0 ? data.Subcategory : "Ammunition" };
        }

        if (item.Firearm is { } firearm)
        {
            if (item.Weapon is null)
            {
                AddError($"{path}.firearm", "Un arma de fuego necesita también su sección \"weapon\".");
            }

            var firearmData = new Dictionary<string, object?>();
            if (OptionalInt($"{path}.firearm.reload", firearm.Reload, 1, 100) is { } reload)
            {
                firearmData["reload"] = reload;
            }

            firearmData["misfire"] = RequiredInt($"{path}.firearm.misfire", firearm.Misfire, 1, 20);
            if (NullableText($"{path}.firearm.era", firearm.Era, ShortTextMaxLength) is { } era)
            {
                firearmData["era"] = era.ToLowerInvariant();
            }

            systemData["firearm"] = firearmData;
        }

        if (item.AttunementClasses is not null)
        {
            if (!data.RequiresAttunement)
            {
                AddError($"{path}.attunementClasses", "Solo se admite en objetos con \"requiresAttunement\": true.");
            }

            var classes = new List<string>();
            ForEachText($"{path}.attunementClasses", item.AttunementClasses, (classPath, value) =>
            {
                if (Reference(classPath, value.ToLowerInvariant()) is { } index && !classes.Contains(index))
                {
                    classes.Add(index);
                }
            });
            if (classes.Count > 0)
            {
                systemData["attunementClasses"] = classes;
            }
        }

        return systemData.Count == 0 ? data : data with { SystemDataJson = JsonSerializer.Serialize(systemData, SystemDataOptions) };
    }
}
