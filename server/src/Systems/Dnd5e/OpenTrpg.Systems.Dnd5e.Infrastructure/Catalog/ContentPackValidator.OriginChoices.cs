using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// Phase 19: decisions of races, subraces and backgrounds (ability bonuses, skills, languages, tools, a cantrip,
// feats and trait options such as a draconic ancestry), normalized to RaceChoices.
internal sealed partial class ContentPackValidator
{
    private const int MaxOriginChoose = 10;

    /// <summary>Damage types of the SRD (resistances and trait options).</summary>
    private static readonly string[] DamageTypeIndexes =
        ["acid", "bludgeoning", "cold", "fire", "force", "lightning", "necrotic", "piercing", "poison", "psychic", "radiant", "slashing", "thunder"];

    /// <summary>Damage types of the pack's own vocabulary (<c>reference.damageTypes</c>).</summary>
    private readonly HashSet<string> _packDamageTypes = new(StringComparer.Ordinal);

    /// <summary>A damage type of the SRD, of a required pack or of this pack.</summary>
    private bool KnownDamageType(string type) =>
        DamageTypeIndexes.Contains(type, StringComparer.Ordinal)
        || _packDamageTypes.Contains(type)
        || (_context.Vocabularies?.TryGetValue(ReferenceEntry.DamageTypes, out var known) == true && known.Contains(type));

    private List<string> DamageTypes(string path, List<string?>? values)
    {
        var result = new List<string>();
        ForEachText(path, values, (itemPath, value) =>
        {
            var type = value.ToLowerInvariant();
            if (KnownDamageType(type))
            {
                result.Add(type);
            }
            else
            {
                AddError(itemPath, $"Tipo de daño desconocido. Valores admitidos: {string.Join(", ", DamageTypeIndexes)}.");
            }
        });
        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Calls <paramref name="action"/> with the index and (when given as an object) the name of every option of a choice.</summary>
    private void ForEachOption(string path, List<PackOptionRefJson?>? options, Action<string, string, string?> action)
    {
        if (options is null)
        {
            return;
        }

        if (options.Count > MaxListEntries)
        {
            AddError(path, $"No puede tener más de {MaxListEntries} entradas.");
            return;
        }

        for (var i = 0; i < options.Count; i++)
        {
            var index = options[i]?.Index?.Trim();
            if (string.IsNullOrEmpty(index))
            {
                AddError($"{path}[{i}]", "El valor no puede estar vacío.");
                continue;
            }

            action($"{path}[{i}]", index, NullableText($"{path}[{i}].name", options[i]!.Name, NameMaxLength));
        }
    }

    /// <summary>The options of a free choice (languages, tools, cantrips): the name is the index unless given.</summary>
    private List<OriginOption> Options(string path, List<PackOptionRefJson?>? options, int maxLength)
    {
        var result = new List<OriginOption>();
        ForEachOption(path, options, (itemPath, index, name) =>
        {
            if (index.Length > maxLength)
            {
                AddError(itemPath, $"No puede superar los {maxLength} caracteres.");
            }

            result.Add(new OriginOption(index, name ?? index));
        });
        return result;
    }

    /// <summary>The breath weapon of a trait option (draconic ancestry); null when invalid.</summary>
    private BreathWeaponInfo? BreathWeapon(string path, PackBreathWeaponJson breath)
    {
        var name = RequiredText($"{path}.name", breath.Name, NameMaxLength);
        var area = RequiredText($"{path}.area", breath.Area, ShortTextMaxLength);
        var save = breath.Save?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(save) || !Abilities.IsValid(save))
        {
            AddError($"{path}.save", "Característica desconocida (str, dex, con, int, wis o cha).");
            save = null;
        }

        var damage = LevelMap($"{path}.damageAtCharacterLevel", breath.DamageAtCharacterLevel, 1, 20);
        if (damage is null)
        {
            AddError($"{path}.damageAtCharacterLevel", "Indica el daño por nivel de personaje ({ \"1\": \"2d6\" }).");
        }

        return save is null || damage is null || name.Length == 0 || area.Length == 0 ? null : new BreathWeaponInfo(name, area, save, damage);
    }

    private string? OriginChoices(string path, PackOriginChoicesJson? choices)
    {
        if (choices is null)
        {
            return null;
        }

        AbilityBonusChoice? abilities = null;
        if (choices.AbilityBonuses is { } bonuses)
        {
            var choose = RequiredInt($"{path}.abilityBonuses.choose", bonuses.Choose, 1, 6) ?? 1;
            var amount = bonuses.Amount is null ? 1 : RequiredInt($"{path}.abilityBonuses.amount", bonuses.Amount, 1, 2) ?? 1;
            var from = new List<OriginOption>();
            ForEachOption($"{path}.abilityBonuses.from", bonuses.From, (itemPath, value, label) =>
            {
                var ability = value.ToLowerInvariant();
                if (Abilities.IsValid(ability))
                {
                    from.Add(new OriginOption(ability, label ?? BreakdownLabels.Ability(ability)));
                }
                else
                {
                    AddError(itemPath, "Característica desconocida (str, dex, con, int, wis o cha).");
                }
            });
            if (from.Count == 0)
            {
                from = Abilities.All.Select(a => new OriginOption(a, BreakdownLabels.Ability(a))).ToList();
            }

            if (choose > from.Count)
            {
                AddError($"{path}.abilityBonuses.choose", "No puede superar el número de características posibles.");
            }

            abilities = new AbilityBonusChoice(choose, amount, from);
        }

        PickChoice? skills = null;
        if (choices.Skills is { } skillChoice)
        {
            var from = new List<OriginOption>();
            ForEachOption($"{path}.skills.from", skillChoice.From, (itemPath, value, label) =>
            {
                var key = value.ToLowerInvariant();
                if (_skills.TryGetValue(key, out var name))
                {
                    from.Add(new OriginOption(key, label ?? name));
                }
                else
                {
                    AddError(itemPath, $"La habilidad '{value}' no existe (usa índices como \"perception\").");
                }
            });
            skills = new PickChoice(RequiredInt($"{path}.skills.choose", skillChoice.Choose, 1, MaxOriginChoose) ?? 1, from);
        }

        PickChoice? Texts(string name, PackPickChoiceJson? pick)
        {
            if (pick is null)
            {
                return null;
            }

            var from = Options($"{path}.{name}.from", pick.From, ShortTextMaxLength);
            return new PickChoice(RequiredInt($"{path}.{name}.choose", pick.Choose, 1, MaxOriginChoose) ?? 1, from);
        }

        CantripChoice? cantrip = null;
        if (choices.Cantrip is { } cantripChoice)
        {
            var list = cantripChoice.SpellList?.Trim().ToLowerInvariant();
            cantrip = new CantripChoice(
                RequiredInt($"{path}.cantrip.choose", cantripChoice.Choose, 1, MaxOriginChoose) ?? 1,
                string.IsNullOrEmpty(list) ? ChoiceFilter.AnyList : list,
                Options($"{path}.cantrip.from", cantripChoice.From, IndexMaxLength));
        }

        FeatChoice? feats = null;
        if (choices.Feats is { } featChoice)
        {
            feats = new FeatChoice(RequiredInt($"{path}.feats.choose", featChoice.Choose, 1, 1) ?? 1);
        }

        var traitOptions = new List<TraitOptionChoice>();
        ForEach($"{path}.traitOptions", choices.TraitOptions, (traitPath, trait) =>
        {
            var key = RequiredText($"{traitPath}.key", trait.Key, IndexMaxLength);
            if (key.Length > 0 && !IndexPattern().IsMatch(key))
            {
                AddError($"{traitPath}.key", "Solo puede contener minúsculas, números y guiones.");
            }

            var name = RequiredText($"{traitPath}.name", trait.Name, NameMaxLength);
            var choose = RequiredInt($"{traitPath}.choose", trait.Choose, 1, MaxOriginChoose) ?? 1;
            var options = new List<TraitOption>();
            ForEach($"{traitPath}.options", trait.Options, (optionPath, option) =>
            {
                var index = RequiredText($"{optionPath}.index", option.Index, IndexMaxLength);
                if (index.Length > 0 && !IndexPattern().IsMatch(index))
                {
                    AddError($"{optionPath}.index", "Solo puede contener minúsculas, números y guiones.");
                }

                string? damageType = null;
                if (!string.IsNullOrWhiteSpace(option.DamageType))
                {
                    damageType = option.DamageType.Trim().ToLowerInvariant();
                    if (!KnownDamageType(damageType))
                    {
                        AddError($"{optionPath}.damageType", $"Tipo de daño desconocido. Valores admitidos: {string.Join(", ", DamageTypeIndexes)}.");
                    }
                }

                options.Add(new TraitOption(index, RequiredText($"{optionPath}.name", option.Name, NameMaxLength), Paragraphs($"{optionPath}.description", option.Description))
                {
                    DamageType = damageType,
                    BreathWeapon = option.BreathWeapon is { } breath ? BreathWeapon($"{optionPath}.breathWeapon", breath) : null,
                });
            });
            if (options.Count == 0)
            {
                AddError($"{traitPath}.options", "Necesita al menos una opción.");
            }

            traitOptions.Add(new TraitOptionChoice(key, name, choose, options));
        });

        return new RaceChoices
        {
            AbilityBonuses = abilities,
            Skills = skills,
            Languages = Texts("languages", choices.Languages),
            Tools = Texts("tools", choices.Tools),
            Cantrip = cantrip,
            Feats = feats,
            TraitOptions = traitOptions,
        }.ToJson();
    }
}
