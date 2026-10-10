using System.Globalization;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// Phase 35B3 (H31, H31b): features that enhance concrete inventory entries, the effects of their options and grafts.
internal sealed partial class ContentPackValidator
{
    public const int GraftSlotMaxLength = 40;

    /// <summary><c>{ "optionSets": ["set"], "maxPerItemByLevel": { "2": 1, "6": 2 } }</c>, normalized (<see cref="ItemEnhancementRule.ToJson"/>).</summary>
    private string? ItemEnhancement(string path, PackItemEnhancementJson enhancement)
    {
        var valid = true;
        var sets = new List<string>();
        ForEachText($"{path}.optionSets", enhancement.OptionSets, (setPath, value) =>
        {
            if (Reference(setPath, value) is { } setId && !sets.Contains(setId))
            {
                sets.Add(setId);
                _setReferences.Add((setPath, setId));
            }
        });
        if (sets.Count == 0)
        {
            AddError($"{path}.optionSets", "Indica al menos un conjunto de opciones.");
            valid = false;
        }
        else if (sets.Count > ItemEnhancementRule.MaxOptionSets)
        {
            AddError($"{path}.optionSets", $"Como máximo {ItemEnhancementRule.MaxOptionSets} conjuntos.");
            valid = false;
        }

        var byLevel = new Dictionary<int, int>();
        if (enhancement.MaxPerItemByLevel is not { Count: > 0 })
        {
            AddError($"{path}.maxPerItemByLevel", "Campo obligatorio: opciones por objeto según el nivel de la clase.");
            valid = false;
        }

        foreach (var (levelText, value) in enhancement.MaxPerItemByLevel ?? [])
        {
            var entryPath = $"{path}.maxPerItemByLevel.{levelText}";
            if (!int.TryParse(levelText, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || level is < 1 or > 20)
            {
                AddError(entryPath, "La clave debe ser un nivel de 1 a 20.");
                valid = false;
            }
            else if (RequiredInt(entryPath, value, 1, ItemEnhancementRule.MaxPerItemLimit) is { } count)
            {
                byLevel[level] = count;
            }
            else
            {
                valid = false;
            }
        }

        return valid ? new ItemEnhancementRule(sets, byLevel).ToJson() : null;
    }

    /// <summary>
    /// <c>{ "appliesTo": ["weapon"], "attackBonus": 1, "addProperties": ["finesse"], "damageStep": 1, "damageType": "fire" }</c>;
    /// null when the option has none (or it is not valid).
    /// </summary>
    private string? ItemEffectsOf(string path, PackItemEffectsJson? effects)
    {
        if (effects is null)
        {
            return null;
        }

        var appliesTo = new List<string>();
        ForEachText($"{path}.appliesTo", effects.AppliesTo, (kindPath, value) =>
        {
            var kind = value.Trim().ToLowerInvariant();
            if (!ItemEffects.Kinds.Contains(kind, StringComparer.Ordinal))
            {
                AddError(kindPath, $"Valores admitidos: {string.Join(", ", ItemEffects.Kinds)}.");
            }
            else if (!appliesTo.Contains(kind))
            {
                appliesTo.Add(kind);
            }
        });

        string? damageType = null;
        if (NullableText($"{path}.damageType", effects.DamageType, ItemLimits.DamageTypeMaxLength)?.ToLowerInvariant() is { } type)
        {
            if (KnownDamageType(type))
            {
                damageType = type;
            }
            else
            {
                AddError($"{path}.damageType", "Tipo de daño desconocido (del SRD, del paquete o de sus paquetes requeridos).");
            }
        }

        var result = new ItemEffects
        {
            AppliesTo = appliesTo,
            AttackBonus = OptionalInt($"{path}.attackBonus", effects.AttackBonus, -ItemEffects.MaxBonus, ItemEffects.MaxBonus) ?? 0,
            DamageBonus = OptionalInt($"{path}.damageBonus", effects.DamageBonus, -ItemEffects.MaxBonus, ItemEffects.MaxBonus) ?? 0,
            ArmorClassBonus = OptionalInt($"{path}.armorClassBonus", effects.ArmorClassBonus, -ItemEffects.MaxBonus, ItemEffects.MaxBonus) ?? 0,
            AddProperties = TextList($"{path}.addProperties", effects.AddProperties, ItemLimits.MaxListEntries, ItemLimits.PropertyMaxLength)
                .Select(p => p.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            DamageStep = OptionalInt($"{path}.damageStep", effects.DamageStep, -ItemEffects.MaxDamageSteps, ItemEffects.MaxDamageSteps) ?? 0,
            DamageType = damageType,
        };
        return result.ToJson();
    }

    /// <summary><c>{ "slot": "brazos" }</c>: the body slot of a graft (free vocabulary, lowercase).</summary>
    private string? GraftSlot(string path, PackGraftJson graft) =>
        RequiredText($"{path}.slot", graft.Slot, GraftSlotMaxLength).ToLowerInvariant() is { Length: > 0 } slot ? slot : null;
}
