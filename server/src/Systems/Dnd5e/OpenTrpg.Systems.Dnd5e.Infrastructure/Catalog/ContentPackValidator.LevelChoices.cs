using System.Globalization;
using System.Text.Json;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// Format 2 of the content packs: option sets (new ones, or options added to sets of the SRD or of other packs),
// level choices of classes and subclasses, and grants (options and subclass levels). References to sets,
// options and spells are checked at the end, against the catalog and the pack itself.
internal sealed partial class ContentPackValidator
{
    public static readonly IReadOnlyList<string> FilterSources = [ChoiceFilter.ListSource, ChoiceFilter.SpellbookSource, ChoiceFilter.KnownSource];

    private readonly List<(string Path, string SetId)> _setReferences = [];
    private readonly List<(string Path, string? SetId, string Index)> _optionReferences = [];
    private readonly List<(string Path, string Index)> _spellReferences = [];
    private readonly List<(string Path, string Index)> _raceReferences = [];
    private readonly List<(string Path, string Index)> _classReferences = [];
    private readonly List<(string Path, string Index)> _subclassReferences = [];
    private readonly HashSet<string> _declaredSets = new(StringComparer.Ordinal);

    /// <summary>True when <paramref name="value"/> is given and the pack declares format 2; reports an error when it is given without it.</summary>
    private bool RequireLevelChoicesFormat(string path, object? value)
    {
        if (value is null)
        {
            return false;
        }

        if (_formatVersion < LevelChoicesFormatVersion)
        {
            AddError(path, $"Requiere \"formatVersion\": {LevelChoicesFormatVersion}.");
            return false;
        }

        return true;
    }

    private bool IsOtherSource(string source) => source != _id;

    private void OptionSet(string path, PackOptionSetJson set, ContentPackRows rows)
    {
        var setId = set.SetId?.Trim();
        if (string.IsNullOrEmpty(setId))
        {
            AddError($"{path}.setId", "Campo obligatorio.");
            return;
        }

        var existing = _context.OptionSets is { } sets && sets.TryGetValue(setId, out var source) && IsOtherSource(source);
        if (existing)
        {
            // Options added to a set of the SRD or of another pack; its name is not changed.
            if (!_declaredSets.Add(setId))
            {
                AddError($"{path}.setId", $"El conjunto '{setId}' aparece dos veces en el paquete.");
            }
        }
        else
        {
            var id = Index($"{path}.setId", "optionSets", setId);
            var name = RequiredText($"{path}.name", set.Name, NameMaxLength);
            if (id is null)
            {
                return;
            }

            _declaredSets.Add(id);
            rows.OptionSets.Add(new OptionSetDefinition { SetId = id, Name = name, Source = _id });
        }

        ForEach($"{path}.options", set.Options, (optionPath, option) => Option(optionPath, option, setId, rows));
    }

    private void Option(string path, PackOptionJson option, string setId, ContentPackRows rows, string? category = null, bool creationOnly = false, bool starter = false, bool repeatable = false)
    {
        var index = Index($"{path}.index", "options", option.Index);
        var definition = new OptionDefinition
        {
            Index = index ?? string.Empty,
            SetId = setId,
            Name = RequiredText($"{path}.name", option.Name, NameMaxLength),
            Description = Paragraphs($"{path}.description", option.Description),
            PrerequisitesText = NullableText($"{path}.prerequisitesText", option.PrerequisitesText, LevelChoiceRule.NoteMaxLength),
            PrerequisitesJson = Prerequisites($"{path}.prerequisites", option.Prerequisites),
            ModifiersJson = ChoiceModifiers($"{path}.modifiers", option.Modifiers),
            AbilityIncreaseJson = AbilityIncreaseOf($"{path}.abilityIncrease", option.AbilityIncrease),
            GrantsJson = option.Grants is null ? null : Grants($"{path}.grants", option.Grants),
            ResourceJson = Resource($"{path}.resource", option.Resource),
            CostJson = Cost($"{path}.cost", option.Cost, setId),
            ChoicesJson = OptionChoices($"{path}.choices", option.Choices, index, atLevels: false),
            LevelChoicesJson = OptionChoices($"{path}.levelChoices", option.LevelChoices, index, atLevels: true),
            Category = category,
            CreationOnly = creationOnly,
            Starter = starter,
            Repeatable = repeatable,
            ItemEffectsJson = ItemEffectsOf($"{path}.itemEffects", option.ItemEffects),
            Source = _id,
        };

        if (index is not null)
        {
            rows.Options.Add(definition);
        }
    }

    private string? Prerequisites(string path, PackPrerequisitesJson? prerequisites) =>
        prerequisites is null ? null : LevelChoiceJson.Serialize(PrerequisitesObject(path, prerequisites, nested: false));

    private Dictionary<string, object?> PrerequisitesObject(string path, PackPrerequisitesJson prerequisites, bool nested)
    {

        var minLevel = OptionalInt($"{path}.minLevel", prerequisites.MinLevel, 1, 20);
        var pactBoon = Reference($"{path}.pactBoon", prerequisites.PactBoon);
        if (pactBoon is not null)
        {
            _optionReferences.Add(($"{path}.pactBoon", null, pactBoon));
        }

        var cantrip = Reference($"{path}.cantrip", prerequisites.Cantrip);
        if (cantrip is not null)
        {
            _spellReferences.Add(($"{path}.cantrip", cantrip));
        }

        var abilities = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, value) in prerequisites.Abilities ?? [])
        {
            var ability = key.Trim().ToLowerInvariant();
            if (!Abilities.IsValid(ability))
            {
                AddError($"{path}.abilities.{key}", "Característica desconocida (str, dex, con, int, wis o cha).");
            }
            else if (RequiredInt($"{path}.abilities.{key}", value, 1, 30) is { } minimum)
            {
                abilities[ability] = minimum;
            }
        }

        var races = new List<string>();
        ForEachText($"{path}.races", prerequisites.Races, (racePath, race) =>
        {
            if (Reference(racePath, race) is { } index && !races.Contains(index))
            {
                races.Add(index);
                _raceReferences.Add((racePath, index));
            }
        });

        var armor = new List<string>();
        ForEachText($"{path}.proficiency.armor", prerequisites.Proficiency?.Armor, (armorPath, value) =>
        {
            if (ProficiencyKeys.NormalizeArmor(value) is { } key)
            {
                if (!armor.Contains(key))
                {
                    armor.Add(key);
                }
            }
            else
            {
                AddError(armorPath, "Armadura desconocida. Valores admitidos: light, medium, heavy, shields, exotic.");
            }
        });

        var weapon = new List<string>();
        ForEachText($"{path}.proficiency.weapon", prerequisites.Proficiency?.Weapon, (weaponPath, value) =>
        {
            if (Reference(weaponPath, ProficiencyKeys.NormalizeWeapon(value)) is { } key && !weapon.Contains(key))
            {
                weapon.Add(key);
            }
        });

        var spellcasting = prerequisites.Spellcasting == true;
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["minLevel"] = minLevel,
            ["pactBoon"] = pactBoon,
            ["cantrip"] = cantrip,
            ["abilities"] = abilities,
            ["races"] = races,
            ["proficiency"] = new { armor, weapon },
            ["spellcasting"] = spellcasting,
        };

        var subclasses = new List<string>();
        ForEachText($"{path}.subclasses", prerequisites.Subclasses, (subclassPath, value) =>
        {
            if (Reference(subclassPath, value) is { } index && !subclasses.Contains(index))
            {
                subclasses.Add(index);
                _subclassReferences.Add((subclassPath, index));
            }
        });

        var options = new List<string>();
        ForEachText($"{path}.options", prerequisites.Options, (optionPath, value) =>
        {
            if (Reference(optionPath, value) is { } index && !options.Contains(index))
            {
                options.Add(index);
                _optionReferences.Add((optionPath, null, index));
            }
        });

        var classes = new List<string>();
        ForEachText($"{path}.classes", prerequisites.Classes, (classPath, value) =>
        {
            if (Reference(classPath, value) is { } index && !classes.Contains(index))
            {
                classes.Add(index);
                _classReferences.Add((classPath, index));
            }
        });

        string? classFeature = null;
        if (prerequisites.ClassFeature?.Trim() is { Length: > 0 } feature)
        {
            if (ClassFeatures.All.Contains(feature))
            {
                classFeature = feature;
            }
            else
            {
                AddError($"{path}.classFeature", "Valores admitidos: spellcasting, pactMagic.");
            }
        }

        var sizes = new List<string>();
        ForEachText($"{path}.sizes", prerequisites.Sizes, (sizePath, value) =>
        {
            var size = value.ToLowerInvariant();
            if (!Sizes.Contains(size, StringComparer.OrdinalIgnoreCase))
            {
                AddError(sizePath, $"Tamaño desconocido. Valores admitidos: {string.Join(", ", Sizes.Select(z => z.ToLowerInvariant()))}.");
            }
            else if (!sizes.Contains(size))
            {
                sizes.Add(size);
            }
        });

        var anyOf = new List<object>();
        if (prerequisites.AnyOf is { } alternatives)
        {
            if (nested)
            {
                AddError($"{path}.anyOf", "Las alternativas no pueden tener a su vez \"anyOf\".");
            }
            else if (alternatives.Count < 2)
            {
                AddError($"{path}.anyOf", "Necesita al menos dos alternativas.");
            }
            else
            {
                ForEach($"{path}.anyOf", alternatives, (alternativePath, alternative) => anyOf.Add(PrerequisitesObject(alternativePath, alternative, nested: true)));
            }
        }

        result["subclasses"] = subclasses;
        result["options"] = options;
        result["classes"] = classes;
        result["classFeature"] = classFeature;
        result["characterLevel"] = OptionalInt($"{path}.characterLevel", prerequisites.CharacterLevel, 1, 20);
        result["spellcastingAbilityMin"] = OptionalInt($"{path}.spellcastingAbilityMin", prerequisites.SpellcastingAbilityMin, 1, 30);
        result["sizes"] = sizes;
        result["anyOf"] = anyOf;
        return result;
    }

    private string ChoiceModifiers(string path, List<PackChoiceModifierJson?>? modifiers, string owner = "Una opción")
    {
        var result = new List<object>();
        if (modifiers is { Count: > ItemLimits.MaxModifiers })
        {
            AddError(path, $"{owner} admite como máximo {ItemLimits.MaxModifiers} modificadores.");
            return "[]";
        }

        ForEach(path, modifiers, (modifierPath, modifier) =>
        {
            if (string.IsNullOrWhiteSpace(modifier.Kind))
            {
                AddError($"{modifierPath}.kind", "Campo obligatorio.");
                return;
            }

            if (!EnumNames.TryParse<ItemModifierKind>(modifier.Kind.Trim(), out var kind))
            {
                AddError($"{modifierPath}.kind", $"Tipo de modificador desconocido. Valores admitidos: {string.Join(", ", Enum.GetNames<ItemModifierKind>())}.");
                return;
            }

            if (!ModifierValue($"{modifierPath}.value", modifier.Value, kind, out var value, out var formula))
            {
                return;
            }

            string? target;
            if (ItemModifier.IsItemKind(kind))
            {
                var candidate = new ItemModifier(kind, modifier.Target, value ?? 0);
                if (candidate.Validate() is { } error)
                {
                    AddError(modifierPath, error);
                    return;
                }

                target = candidate.Normalize().Target;
            }
            else if (!CharacterRuleModifier(modifierPath, kind, modifier.Target, value, out target))
            {
                return;
            }

            var condition = NullableText($"{modifierPath}.condition", modifier.Condition, ShortTextMaxLength);
            if (!ModifierConditions.IsValid(condition))
            {
                AddError($"{modifierPath}.condition", $"Condición desconocida. Valores admitidos: {string.Join(", ", ModifierConditions.All)}.");
                return;
            }

            if (condition is not null && ConditionsOf(kind, target) is { } allowed && !allowed.Contains(condition, StringComparer.Ordinal))
            {
                AddError($"{modifierPath}.condition", allowed.Count == 0
                    ? "Este tipo de modificador no admite condición."
                    : $"Condición no admitida en este tipo de modificador. Valores admitidos: {string.Join(", ", allowed)}.");
                return;
            }

            var entry = new Dictionary<string, object?>
            {
                ["kind"] = kind.ToString(),
                ["target"] = target,
                ["value"] = value,
                ["condition"] = condition,
            };
            if (formula is not null)
            {
                entry["formula"] = formula;
            }

            if (kind == ItemModifierKind.ArmorClassFormula)
            {
                entry["abilities"] = ArmorClassAbilities($"{modifierPath}.abilities", modifier.Abilities);
                entry["allowShield"] = modifier.AllowShield ?? true;
            }
            else if (modifier.Abilities is not null || modifier.AllowShield is not null)
            {
                AddError(modifierPath, "\"abilities\" y \"allowShield\" solo se admiten en ArmorClassFormula.");
                return;
            }

            result.Add(entry);
        });

        return LevelChoiceJson.Serialize(result);
    }

    /// <summary>
    /// Reads the value of a modifier: a number (<paramref name="value"/>) or a formula text (<paramref name="formula"/>,
    /// <see cref="ModifierFormula"/>). False (with the error added) when it is missing or not valid.
    /// </summary>
    private bool ModifierValue(string path, JsonElement? element, ItemModifierKind kind, out int? value, out string? formula)
    {
        value = null;
        formula = null;
        switch (element)
        {
            case null or { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined }:
                if (kind == ItemModifierKind.HitDiceRecovery)
                {
                    return true;
                }

                AddError(path, "Campo obligatorio.");
                return false;
            case { ValueKind: JsonValueKind.Number } number when number.TryGetInt32(out var integer):
                value = integer;
                return true;
            case { ValueKind: JsonValueKind.String } text:
                var trimmed = text.GetString()?.Trim();
                if (kind is ItemModifierKind.AbilityBonus or ItemModifierKind.AbilitySet or ItemModifierKind.AbilityMax
                    or ItemModifierKind.ArmorClassFormula or ItemModifierKind.CriticalRange or ItemModifierKind.DeathSaveFailures
                    or ItemModifierKind.HitDiceRecovery)
                {
                    AddError(path, "Este tipo de modificador no admite fórmulas: usa un número entero.");
                    return false;
                }

                if (string.IsNullOrEmpty(trimmed) || trimmed.Length > ShortTextMaxLength
                    || ModifierFormula.Parse(trimmed, allowWalk: kind == ItemModifierKind.Speed) is null)
                {
                    AddError(path, kind == ItemModifierKind.Speed
                        ? "Fórmula no válida: usa \"walk\" (igual a la velocidad caminando), un número o términos como \"characterLevel\", \"classLevel\", \"proficiencyBonus\" o \"mod:dex\" unidos con +."
                        : "Fórmula no válida: usa términos como \"characterLevel\", \"halfCharacterLevel\", \"classLevel\", \"halfClassLevel\", \"proficiencyBonus\", \"mod:wis\" o números unidos con + (\"2*classLevel\").");
                    return false;
                }

                formula = trimmed;
                return true;
            default:
                AddError(path, "Debe ser un número entero o una fórmula.");
                return false;
        }
    }

    /// <summary>Checks the target and the value of a character rule modifier (<see cref="ItemModifierKind.ArmorClassFormula"/> on).</summary>
    private bool CharacterRuleModifier(string path, ItemModifierKind kind, string? rawTarget, int? value, out string? target)
    {
        target = string.IsNullOrWhiteSpace(rawTarget) ? null : rawTarget.Trim().ToLowerInvariant();
        string? error = kind switch
        {
            ItemModifierKind.ArmorClassFormula => target is not null ? "Este tipo de modificador no admite objetivo."
                : value is < 0 or > 30 ? "La base de la CA debe estar entre 0 y 30." : null,
            ItemModifierKind.CriticalRange => target is not null && !ModifierTargets.CriticalRange.Contains(target, StringComparer.Ordinal)
                ? "El objetivo debe ser weapon, spell o ninguno (ambos)."
                : value is < 2 or > 19 ? "El crítico debe empezar en una tirada entre 2 y 19." : null,
            ItemModifierKind.SpellSaveDcBonus or ItemModifierKind.SpellAttackBonus =>
                target is not null && (target.Length > IndexMaxLength || !IndexPattern().IsMatch(target))
                    ? "El objetivo debe ser el índice de una clase o ninguno (todas)."
                    : value is < ItemLimits.MinModifier or > ItemLimits.MaxModifier ? $"El valor debe estar entre {ItemLimits.MinModifier} y {ItemLimits.MaxModifier}." : null,
            ItemModifierKind.PassiveBonus => target is null || !ModifierTargets.Passives.Contains(target, StringComparer.Ordinal)
                ? $"El objetivo debe ser {string.Join(", ", ModifierTargets.Passives)}."
                : value is < ItemLimits.MinModifier or > ItemLimits.MaxModifier ? $"El valor debe estar entre {ItemLimits.MinModifier} y {ItemLimits.MaxModifier}." : null,
            ItemModifierKind.AbilityMax => target is null || !Abilities.IsValid(target)
                ? "El modificador necesita una característica: str, dex, con, int, wis o cha."
                : value is < 1 or > 30 ? "El máximo debe estar entre 1 y 30." : null,
            ItemModifierKind.Speed => target is null || !ModifierTargets.Speeds.Contains(target, StringComparer.Ordinal)
                ? $"El objetivo debe ser {string.Join(", ", ModifierTargets.Speeds)}."
                : value is < 0 or > 200 ? "La velocidad debe estar entre 0 y 200 pies." : null,
            ItemModifierKind.DeathSaveFailures => target is not null ? "Este tipo de modificador no admite objetivo."
                : value is < 3 or > 10 ? "Los fallos para morir deben estar entre 3 y 10." : null,
            ItemModifierKind.HitDiceRecovery => target != ModifierTargets.All ? "El objetivo debe ser \"all\" (todos los dados de golpe)." : null,
            ItemModifierKind.ResourceMaxBonus => target is null || target.Length > IndexMaxLength || !IndexPattern().IsMatch(target)
                ? "El objetivo debe ser la clave de un recurso (\"ki\", \"mipaquete-dados\")."
                : value is < ItemLimits.MinModifier or > ItemLimits.MaxModifier ? $"El valor debe estar entre {ItemLimits.MinModifier} y {ItemLimits.MaxModifier}." : null,
            _ => "El tipo de modificador no es válido.",
        };

        if (error is not null)
        {
            AddError(path, error);
            return false;
        }

        return true;
    }

    /// <summary>The conditions a kind admits (empty: none), or null when it admits any.</summary>
    private static IReadOnlyList<string>? ConditionsOf(ItemModifierKind kind, string? target) => kind switch
    {
        ItemModifierKind.ArmorClassFormula => [ModifierConditions.NoArmor],
        ItemModifierKind.CriticalRange when target == ModifierTargets.Spell => [],
        ItemModifierKind.CriticalRange =>
        [
            ModifierConditions.RangedWeapon, ModifierConditions.Firearm, ModifierConditions.OneHandedMeleeNoOtherWeapon,
            ModifierConditions.TwoHandedMelee,
        ],
        ItemModifierKind.SpellSaveDcBonus or ItemModifierKind.SpellAttackBonus => [ModifierConditions.Cantrip],
        ItemModifierKind.PassiveBonus or ItemModifierKind.AbilityMax or ItemModifierKind.Speed
            or ItemModifierKind.DeathSaveFailures or ItemModifierKind.HitDiceRecovery or ItemModifierKind.ResourceMaxBonus => [],
        _ => ModifierConditions.All.Where(c => c != ModifierConditions.Cantrip).ToList(),
    };

    private List<string> ArmorClassAbilities(string path, List<string?>? values)
    {
        var abilities = new List<string>();
        ForEachText(path, values, (abilityPath, ability) =>
        {
            var key = ability.ToLowerInvariant();
            if (!Abilities.IsValid(key))
            {
                AddError(abilityPath, "Característica desconocida (str, dex, con, int, wis o cha).");
            }
            else if (!abilities.Contains(key))
            {
                abilities.Add(key);
            }
        });

        if (abilities.Count > 3)
        {
            AddError(path, "Como máximo 3 características.");
        }

        return abilities;
    }

    private string? AbilityIncreaseOf(string path, PackAbilityIncreaseJson? increase)
    {
        if (increase is null)
        {
            return null;
        }

        var amount = RequiredInt($"{path}.amount", increase.Amount, 1, 2);
        var from = new List<string>();
        ForEachText($"{path}.from", increase.From, (abilityPath, ability) =>
        {
            var key = ability.ToLowerInvariant();
            if (!Abilities.IsValid(key))
            {
                AddError(abilityPath, "Característica desconocida (str, dex, con, int, wis o cha).");
            }
            else if (!from.Contains(key))
            {
                from.Add(key);
            }
        });

        return amount is null ? null : LevelChoiceJson.Serialize(new { amount, from });
    }

    /// <param name="origin">
    /// Grants of a race or subrace: <c>spells[].minLevel</c> is the total level, and <c>spellcastingAbility</c> (required
    /// with spells or cantrips) and <c>spells[].usesPerLongRest</c> are allowed.
    /// </param>
    private string Grants(string path, PackGrantsJson grants, bool origin = false)
    {
        var skills = new List<string>();
        ForEachText($"{path}.skills", grants.Skills, (skillPath, skill) =>
        {
            var key = skill.ToLowerInvariant();
            if (_skills.ContainsKey(key))
            {
                skills.Add(key);
            }
            else
            {
                AddError(skillPath, $"La habilidad '{skill}' no existe (usa índices como \"perception\" o \"animal-handling\").");
            }
        });

        var cantrips = new List<string>();
        ForEachText($"{path}.cantrips", grants.Cantrips, (cantripPath, cantrip) =>
        {
            if (Reference(cantripPath, cantrip) is { } index)
            {
                cantrips.Add(index);
                _spellReferences.Add((cantripPath, index));
            }
        });

        var spells = new List<object>();
        ForEach($"{path}.spells", grants.Spells, (spellPath, spell) =>
        {
            var index = Reference($"{spellPath}.index", spell.Index);
            var minLevel = OptionalInt($"{spellPath}.minLevel", spell.MinLevel, 1, 20);
            int? usesPerLongRest = null;
            if (spell.UsesPerLongRest is not null)
            {
                usesPerLongRest = OptionalInt($"{spellPath}.usesPerLongRest", spell.UsesPerLongRest, 1, 20);
            }

            if (index is null)
            {
                AddError($"{spellPath}.index", "Campo obligatorio.");
                return;
            }

            _spellReferences.Add(($"{spellPath}.index", index));
            spells.Add(new { index, minLevel, usesPerLongRest });
        });

        // Races and subraces must say how they cast what they grant; options, feats and subclass levels may (else they use
        // the ability of the class of the pick).
        string? spellcastingAbility = null;
        var ability = grants.SpellcastingAbility?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(ability))
        {
            if (origin && spells.Count + cantrips.Count > 0)
            {
                AddError($"{path}.spellcastingAbility", "Campo obligatorio cuando se conceden conjuros o trucos (int, wis o cha...).");
            }
        }
        else if (!Abilities.IsValid(ability))
        {
            AddError($"{path}.spellcastingAbility", "Característica desconocida (str, dex, con, int, wis o cha).");
        }
        else
        {
            spellcastingAbility = ability;
        }

        var savingThrows = new List<string>();
        ForEachText($"{path}.savingThrows", grants.SavingThrows, (savePath, save) =>
        {
            var key = save.ToLowerInvariant();
            if (Abilities.IsValid(key))
            {
                savingThrows.Add(key);
            }
            else
            {
                AddError(savePath, "Característica desconocida (str, dex, con, int, wis o cha).");
            }
        });

        var options = new List<string>();
        ForEachText($"{path}.options", grants.Options, (optionPath, value) =>
        {
            if (origin)
            {
                AddError(optionPath, "Las razas, subrazas y trasfondos no conceden opciones.");
            }
            else if (Reference(optionPath, value) is { } index && !options.Contains(index))
            {
                options.Add(index);
                _optionReferences.Add((optionPath, null, index));
            }
        });

        var result = new Dictionary<string, object?>
        {
            ["skills"] = skills,
            ["cantrips"] = cantrips,
            ["spells"] = spells,
            ["options"] = options,
            ["armor"] = TextList($"{path}.armor", grants.Armor, 50, ShortTextMaxLength).Select(a => ProficiencyKeys.NormalizeArmor(a) ?? a).ToList(),
            ["weapons"] = TextList($"{path}.weapons", grants.Weapons, 50, ShortTextMaxLength).Select(ProficiencyKeys.NormalizeWeapon).ToList(),
            ["tools"] = TextList($"{path}.tools", grants.Tools, 50, ShortTextMaxLength),
            ["languages"] = TextList($"{path}.languages", grants.Languages, 50, ShortTextMaxLength),
            ["savingThrows"] = savingThrows,
            ["spellcastingAbility"] = spellcastingAbility,
        };
        SheetGrants(path, grants, result);
        return LevelChoiceJson.Serialize(result);
    }

    /// <summary>
    /// Validates the grants that change the sheet (defenses, size, saving throws lost, skill choices, natural weapons)
    /// and adds the present ones to <paramref name="result"/>, so grants without them serialize as before.
    /// </summary>
    private void SheetGrants(string path, PackGrantsJson grants, Dictionary<string, object?> result)
    {
        if (DamageTypes($"{path}.resistances", grants.Resistances) is { Count: > 0 } resistances)
        {
            result["resistances"] = resistances;
        }

        if (DamageTypes($"{path}.immunities", grants.Immunities) is { Count: > 0 } immunities)
        {
            result["immunities"] = immunities;
        }

        var conditions = new List<string>();
        ForEachText($"{path}.conditionImmunities", grants.ConditionImmunities, (conditionPath, value) =>
        {
            var condition = value.ToLowerInvariant();
            if (Domain.Catalog.DamageTypes.Conditions.Contains(condition, StringComparer.Ordinal) || _packConditions.Contains(condition)
                || _context.Conditions?.Contains(condition) == true)
            {
                if (!conditions.Contains(condition))
                {
                    conditions.Add(condition);
                }
            }
            else
            {
                AddError(conditionPath, $"Estado desconocido. Valores admitidos: {string.Join(", ", Domain.Catalog.DamageTypes.Conditions)} o los estados del paquete y de los paquetes requeridos.");
            }
        });
        if (conditions.Count > 0)
        {
            result["conditionImmunities"] = conditions;
        }

        if (grants.Size?.Trim().ToLowerInvariant() is { Length: > 0 } size)
        {
            if (Domain.Catalog.DamageTypes.Sizes.Contains(size, StringComparer.Ordinal))
            {
                result["size"] = size;
            }
            else
            {
                AddError($"{path}.size", $"Tamaño desconocido. Valores admitidos: {string.Join(", ", Domain.Catalog.DamageTypes.Sizes)}.");
            }
        }

        if (grants.RemoveSavingThrows is { } remove)
        {
            var from = new List<string>();
            ForEachText($"{path}.removeSavingThrows.from", remove.From, (abilityPath, ability) =>
            {
                var key = ability.ToLowerInvariant();
                if (!Abilities.IsValid(key))
                {
                    AddError(abilityPath, "Característica desconocida (str, dex, con, int, wis o cha).");
                }
                else if (!from.Contains(key))
                {
                    from.Add(key);
                }
            });

            var choose = RequiredInt($"{path}.removeSavingThrows.choose", remove.Choose, 1, 6);
            if (choose is { } count && from.Count > 0 && count > from.Count)
            {
                AddError($"{path}.removeSavingThrows.choose", "No puede superar el número de salvaciones de \"from\".");
            }
            else if (choose is { } valid)
            {
                result["removeSavingThrows"] = new { choose = valid, from };
            }
        }

        if (OptionalInt($"{path}.skillChoicesDelta", grants.SkillChoicesDelta, -10, 10) is { } delta and not 0)
        {
            result["skillChoicesDelta"] = delta;
        }

        var lostSkills = new List<string>();
        ForEachText($"{path}.removeSkills", grants.RemoveSkills, (skillPath, skill) =>
        {
            var key = skill.ToLowerInvariant();
            if (!_skills.ContainsKey(key))
            {
                AddError(skillPath, $"La habilidad '{skill}' no existe (usa índices como \"perception\" o \"animal-handling\").");
            }
            else if (!lostSkills.Contains(key))
            {
                lostSkills.Add(key);
            }
        });
        if (lostSkills.Count > 0)
        {
            result["removeSkills"] = lostSkills;
        }

        var weapons = new List<object>();
        ForEach($"{path}.naturalWeapons", grants.NaturalWeapons, (weaponPath, weapon) =>
        {
            var name = RequiredText($"{weaponPath}.name", weapon.Name, NaturalWeapon.NameMaxLength);
            var damage = NaturalWeaponDice($"{weaponPath}.damage", weapon.Damage);
            var byLevel = new SortedDictionary<int, string>();
            foreach (var (levelText, dice) in weapon.DamageByCharacterLevel ?? [])
            {
                var levelPath = $"{weaponPath}.damageByCharacterLevel.{levelText}";
                if (!int.TryParse(levelText, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || level is < 1 or > 20)
                {
                    AddError(levelPath, "El nivel debe ser un número entre 1 y 20.");
                }
                else if (NaturalWeaponDice(levelPath, dice) is { } levelDice)
                {
                    byLevel[level] = levelDice;
                }
                else if (string.IsNullOrWhiteSpace(dice))
                {
                    AddError(levelPath, "Campo obligatorio.");
                }
            }

            if (damage is null && byLevel.Count == 0)
            {
                AddError($"{weaponPath}.damage", "Indica \"damage\" o \"damageByCharacterLevel\".");
            }

            var damageType = weapon.DamageType?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(damageType))
            {
                AddError($"{weaponPath}.damageType", "Campo obligatorio.");
            }
            else if (!KnownDamageType(damageType))
            {
                AddError($"{weaponPath}.damageType", $"Tipo de daño desconocido. Valores admitidos: {string.Join(", ", DamageTypeIndexes)}.");
            }

            var reach = OptionalInt($"{weaponPath}.reach", weapon.Reach, 5, 30);
            if (name is null)
            {
                return;
            }

            var entry = new Dictionary<string, object?> { ["name"] = name, ["damageType"] = damageType };
            if (damage is not null)
            {
                entry["damage"] = damage;
            }

            if (byLevel.Count > 0)
            {
                entry["damageByCharacterLevel"] = byLevel.ToDictionary(p => p.Key.ToString(CultureInfo.InvariantCulture), p => p.Value);
            }

            if (weapon.Finesse == true)
            {
                entry["finesse"] = true;
            }

            if (reach is { } feet and not 5)
            {
                entry["reach"] = feet;
            }

            weapons.Add(entry);
        });
        if (weapons.Count > 0)
        {
            result["naturalWeapons"] = weapons;
        }
    }

    /// <summary>Damage dice of a natural weapon ("1d6", "2d4", "1"), without spaces; null when absent or not valid.</summary>
    private string? NaturalWeaponDice(string path, string? value)
    {
        var dice = value?.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (string.IsNullOrEmpty(dice))
        {
            return null;
        }

        if (dice.Length > ItemLimits.DiceMaxLength || !NaturalDicePattern().IsMatch(dice))
        {
            AddError(path, "Dados no válidos: usa \"1d6\", \"2d4\" o un número fijo.");
            return null;
        }

        return dice;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^(\d{1,2}d(4|6|8|10|12|20)|\d{1,2})$")]
    private static partial System.Text.RegularExpressions.Regex NaturalDicePattern();

    private string? Resource(string path, PackResourceJson? resource)
    {
        if (resource is null)
        {
            return null;
        }

        var key = Reference($"{path}.key", resource.Key);
        if (key is null)
        {
            AddError($"{path}.key", "Campo obligatorio.");
        }

        var name = RequiredText($"{path}.name", resource.Name, CharacterResource.NameMaxLength);
        var max = ResourceMax($"{path}.max", resource.Max);

        var recharge = ResourceRecharge.LongRest;
        if (!string.IsNullOrWhiteSpace(resource.Recharge) && !EnumNames.TryParse(resource.Recharge.Trim(), out recharge))
        {
            AddError($"{path}.recharge", $"Recarga desconocida. Valores admitidos: {string.Join(", ", Enum.GetNames<ResourceRecharge>())}.");
        }

        object? rollOnRest = null;
        if (resource.RollOnRest is { } roll)
        {
            var die = RollOnRest.ParseDie(roll.Dice);
            if (die is null)
            {
                AddError($"{path}.rollOnRest.dice", $"Dado no válido: usa {string.Join(", ", RollOnRest.AllowedDice.Select(d => $"d{d}"))}.");
            }

            var count = RequiredInt($"{path}.rollOnRest.count", roll.Count, 1, RollOnRest.MaxCount);
            var rest = RollOnRest.ParseRest(roll.Rest);
            if (rest is null)
            {
                AddError($"{path}.rollOnRest.rest", "Debe ser short o long.");
            }

            rollOnRest = new { dice = die is null ? null : $"d{die}", count, rest = rest == RestKind.Short ? "short" : "long" };
        }

        object? rechargeRoll = null;
        if (resource.RechargeRoll is { } rechargeJson)
        {
            var die = RollOnRest.ParseDie(rechargeJson.Dice);
            if (die is null)
            {
                AddError($"{path}.rechargeRoll.dice", $"Dado no válido: usa {string.Join(", ", RollOnRest.AllowedDice.Select(d => $"d{d}"))}.");
            }

            var minimum = RequiredInt($"{path}.rechargeRoll.min", rechargeJson.Min, 1, die ?? 100);
            if (die is not null && minimum is not null)
            {
                rechargeRoll = new { dice = $"d{die}", min = minimum };
            }
        }

        string? dice = null;
        if (resource.Dice is not null)
        {
            dice = RollOnRest.ParseDie(resource.Dice) is { } die ? $"d{die}" : null;
            if (dice is null)
            {
                AddError($"{path}.dice", $"Dado no válido: usa {string.Join(", ", RollOnRest.AllowedDice.Select(d => $"d{d}"))}.");
            }
        }

        SortedDictionary<string, string>? diceByLevel = null;
        if (resource.DiceByLevel is { } diceTable)
        {
            diceByLevel = LevelTable($"{path}.diceByLevel", diceTable, (entryPath, value) =>
            {
                if (RollOnRest.ParseDie(value) is { } die)
                {
                    return $"d{die}";
                }

                AddError(entryPath, $"Dado no válido: usa {string.Join(", ", RollOnRest.AllowedDice.Select(d => $"d{d}"))}.");
                return null;
            });
        }

        var result = new Dictionary<string, object?>
        {
            ["key"] = key,
            ["name"] = name,
            ["max"] = max,
            ["recharge"] = recharge.ToString(),
        };
        if (rollOnRest is not null)
        {
            result["rollOnRest"] = rollOnRest;
        }

        if (rechargeRoll is not null)
        {
            result["rechargeRoll"] = rechargeRoll;
        }

        if (dice is not null)
        {
            result["dice"] = dice;
        }

        if (diceByLevel is not null)
        {
            result["diceByLevel"] = diceByLevel;
        }

        return LevelChoiceJson.Serialize(result);
    }

    private const string ResourceMaxHelp =
        "un entero entre 1 y 999, una fórmula (términos separados por \"+\", cada uno un entero o [n*]símbolo con proficiencyBonus, " +
        "classLevel, halfClassLevel o mod:<característica>), { \"formula\": \"...\", \"min\": 0 }, { \"byLevel\": { \"3\": 4 } } " +
        "o ambos ({ \"formula\": \"mod:int\", \"byLevel\": { \"3\": 1 } }, que se suman)";

    /// <summary>
    /// Normalizes the <c>max</c> of a resource: the formula text (integers and simple formulas included),
    /// <c>{ formula, min }</c> or <c>{ byLevel }</c>; null (with an error) when invalid.
    /// </summary>
    private object? ResourceMax(string path, JsonElement? max)
    {
        switch (max)
        {
            case { ValueKind: JsonValueKind.Number } number:
                if (number.TryGetInt32(out var value) && value is >= 1 and <= CharacterResource.MaxUses)
                {
                    return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                break;
            case { ValueKind: JsonValueKind.String } text:
                if (text.GetString()?.Trim() is { } formula && OptionResource.IsValidMax(formula))
                {
                    return formula;
                }

                break;
            case { ValueKind: JsonValueKind.Object } maxObject:
                return ResourceMaxObject(path, maxObject);
        }

        AddError(path, $"Debe ser {ResourceMaxHelp}.");
        return null;
    }

    private object? ResourceMaxObject(string path, JsonElement maxObject)
    {
        var properties = maxObject.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
        if (properties.Keys.FirstOrDefault(k => !k.Equals("formula", StringComparison.OrdinalIgnoreCase) && !k.Equals("min", StringComparison.OrdinalIgnoreCase) && !k.Equals("byLevel", StringComparison.OrdinalIgnoreCase)) is { } unknown)
        {
            AddError($"{path}.{unknown}", "Campo desconocido: usa formula, min y byLevel.");
            return null;
        }

        SortedDictionary<string, int?>? table = null;
        if (properties.TryGetValue("byLevel", out var byLevel))
        {
            if (byLevel.ValueKind != JsonValueKind.Object)
            {
                AddError($"{path}.byLevel", "Debe ser un objeto { \"nivel\": usos }.");
                return null;
            }

            var entries = byLevel.EnumerateObject().ToDictionary(
                p => p.Name,
                p => (string?)(p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetRawText() : null),
                StringComparer.Ordinal);
            table = LevelTable($"{path}.byLevel", entries, (entryPath, text) =>
            {
                if (int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var uses)
                    && uses is >= 0 and <= CharacterResource.MaxUses)
                {
                    return uses;
                }

                AddError(entryPath, $"Debe ser un entero entre 0 y {CharacterResource.MaxUses}.");
                return (int?)null;
            });
            if (table is null)
            {
                return null;
            }

            // A table alone; with a formula, both are added up (the formula's minimum is 0 by default).
            if (!properties.ContainsKey("formula"))
            {
                if (properties.ContainsKey("min"))
                {
                    AddError($"{path}.min", "Con byLevel, min solo se admite junto a formula.");
                    return null;
                }

                return new { byLevel = table };
            }
        }

        var min = table is null ? 1 : 0;
        if (properties.TryGetValue("min", out var minElement))
        {
            if (minElement.ValueKind != JsonValueKind.Number || !minElement.TryGetInt32(out min) || min is < 0 or > CharacterResource.MaxUses)
            {
                AddError($"{path}.min", $"Debe ser un entero entre 0 y {CharacterResource.MaxUses}.");
                return null;
            }
        }

        if (!properties.TryGetValue("formula", out var formulaElement) || formulaElement.ValueKind != JsonValueKind.String
            || formulaElement.GetString()?.Trim() is not { Length: > 0 } formula)
        {
            AddError($"{path}.formula", "Campo obligatorio.");
            return null;
        }

        if (!OptionResource.IsValidMax(formula, table is null ? min : 0))
        {
            AddError($"{path}.formula", $"Fórmula no válida: debe ser {ResourceMaxHelp}.");
            return null;
        }

        return table is null ? new { formula, min } : new { formula, min, byLevel = table };
    }

    /// <summary>
    /// Validates a <c>{ "3": value, "10": value }</c> table: keys are class levels 1-20, at least one entry; each value
    /// is parsed with <paramref name="parse"/> (which reports its own errors). Null when anything is invalid.
    /// </summary>
    private SortedDictionary<string, T>? LevelTable<T>(string path, Dictionary<string, string?> table, Func<string, string?, T?> parse)
    {
        if (table.Count == 0)
        {
            AddError(path, "Debe tener al menos una entrada.");
            return null;
        }

        var result = new SortedDictionary<string, T>(Comparer<string>.Create((a, b) => int.Parse(a, System.Globalization.CultureInfo.InvariantCulture).CompareTo(int.Parse(b, System.Globalization.CultureInfo.InvariantCulture))));
        var valid = true;
        foreach (var (levelText, value) in table)
        {
            var entryPath = $"{path}.{levelText}";
            if (!int.TryParse(levelText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var level) || level is < 1 or > 20)
            {
                AddError(entryPath, "La clave debe ser un nivel de clase entre 1 y 20.");
                valid = false;
                continue;
            }

            if (parse(entryPath, value) is { } parsed)
            {
                result[level.ToString(System.Globalization.CultureInfo.InvariantCulture)] = parsed;
            }
            else
            {
                valid = false;
            }
        }

        return valid ? result : null;
    }

    /// <summary>Validates <c>subclasses[].spellcasting</c>; null when it has errors.</summary>
    private SubclassSpellcasting? SubclassSpellcasting(string path, PackSubclassSpellcastingJson spellcasting, string classIndex)
    {
        var valid = true;
        if (_casterClasses.Contains(classIndex) && !_cantripOnlyClasses.Contains(classIndex))
        {
            AddError(path, $"La clase '{classIndex}' ya lanza conjuros con espacios: spellcasting solo vale en subclases de clases que no lanzan conjuros o solo conocen trucos.");
            valid = false;
        }

        int? level = null;
        if (string.IsNullOrWhiteSpace(spellcasting.Progression))
        {
            AddError($"{path}.progression", "Campo obligatorio.");
        }
        else if ((level = Domain.Catalog.SubclassSpellcasting.LevelOf(spellcasting.Progression)) is null)
        {
            AddError($"{path}.progression", $"Valores admitidos: {string.Join(", ", Domain.Catalog.SubclassSpellcasting.Progressions)}.");
        }

        var ability = spellcasting.Ability?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(ability))
        {
            AddError($"{path}.ability", "Campo obligatorio.");
        }
        else if (!Abilities.IsValid(ability))
        {
            AddError($"{path}.ability", $"Característica desconocida. Valores admitidos: {string.Join(", ", Abilities.All)}.");
            ability = null;
        }

        var fromLevel = OptionalInt($"{path}.fromLevel", spellcasting.FromLevel, 1, 20);
        var spellList = spellcasting.SpellList?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(spellList))
        {
            AddError($"{path}.spellList", "Campo obligatorio.");
        }
        else if (!_classes.ContainsKey(spellList))
        {
            AddError($"{path}.spellList", "Debe ser una clase del catálogo.");
            spellList = null;
        }

        var cantrips = KnownTable($"{path}.cantripsKnown", spellcasting.CantripsKnown, 10);
        var spells = KnownTable($"{path}.spellsKnown", spellcasting.SpellsKnown, 30);
        var progression = spellcasting.Progression?.Trim().ToLowerInvariant();
        IReadOnlyDictionary<int, IReadOnlyList<int>> slots = new Dictionary<int, IReadOnlyList<int>>();
        if (spellcasting.Slots is { Count: > 0 } table)
        {
            if (progression is not (Domain.Catalog.SubclassSpellcasting.Table or Domain.Catalog.SubclassSpellcasting.Pact))
            {
                AddError($"{path}.slots", "Solo se admite con \"progression\": \"table\" o \"pact\"; las demás progresiones calculan los espacios.");
            }

            slots = SlotTable($"{path}.slots", table);
        }
        else if (progression == Domain.Catalog.SubclassSpellcasting.Table)
        {
            AddError($"{path}.slots", "Obligatorio con \"progression\": \"table\".");
            valid = false;
        }

        return valid && level is not null && !string.IsNullOrEmpty(ability) && !string.IsNullOrEmpty(spellList) && cantrips is not null && spells is not null
            && (spellcasting.FromLevel is null || fromLevel is not null)
            ? new SubclassSpellcasting(level.Value, ability, fromLevel ?? 1, spellList, cantrips, spells)
            {
                IsPact = progression == Domain.Catalog.SubclassSpellcasting.Pact,
                Slots = slots,
            }
            : null;
    }

    /// <summary>A <c>{"3": 2, "10": 3}</c> table of known spells or cantrips by class level; null when it has errors.</summary>
    private Dictionary<int, int>? KnownTable(string path, Dictionary<string, int?>? table, int max)
    {
        var result = new Dictionary<int, int>();
        var valid = true;
        foreach (var (key, value) in table ?? [])
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || level is < 1 or > 20)
            {
                AddError($"{path}.{key}", "La clave debe ser un nivel de clase entre 1 y 20.");
                valid = false;
            }
            else if (RequiredInt($"{path}.{key}", value, 0, max) is { } count)
            {
                result[level] = count;
            }
            else
            {
                valid = false;
            }
        }

        return valid ? result : null;
    }

    /// <summary>A level choice after validation (null fields: invalid).</summary>
    private sealed record ParsedLevelChoice(int? Level, string Key, string Name, LevelChoiceKind? Kind, string? SetId, int? Choose, string? FromJson, string? Filter, string Note, string? After, string? AlsoFromJson, bool Replaces, bool Cumulative);

    private ParsedLevelChoice ParseLevelChoice(string path, PackLevelChoiceJson rule, bool levelRequired, SubclassSpellcasting? spellcasting = null, IReadOnlyCollection<LevelChoiceKind>? allowedKinds = null)
    {
        int? level = null;
        if (levelRequired)
        {
            level = RequiredInt($"{path}.level", rule.Level, 1, 20);
        }
        else if (rule.Level is not null)
        {
            AddError($"{path}.level", "Las elecciones de una opción se hacen al tomarla: sin level (usa levelChoices para niveles posteriores).");
        }

        var key = RequiredText($"{path}.key", rule.Key, LevelChoiceRule.KeyMaxLength);
        if (key.Length > 0 && !IndexPattern().IsMatch(key))
        {
            AddError($"{path}.key", "Solo puede contener minúsculas, números y guiones.");
            key = string.Empty;
        }

        var name = RequiredText($"{path}.name", rule.Name, NameMaxLength);
        LevelChoiceKind? kind = null;
        if (string.IsNullOrWhiteSpace(rule.Kind))
        {
            AddError($"{path}.kind", "Campo obligatorio.");
        }
        else if (EnumNames.TryParse<LevelChoiceKind>(rule.Kind.Trim(), out var parsed)
                 && (parsed is not (LevelChoiceKind.SavingThrow or LevelChoiceKind.Weapon or LevelChoiceKind.Resistance) || allowedKinds is not null))
        {
            kind = parsed;
            if (allowedKinds is not null && !allowedKinds.Contains(parsed))
            {
                AddError($"{path}.kind", $"Aquí se admiten: {string.Join(", ", allowedKinds)}.");
                kind = null;
            }
        }
        else
        {
            var names = Enum.GetNames<LevelChoiceKind>().Where(n => allowedKinds is not null || n is not (nameof(LevelChoiceKind.SavingThrow) or nameof(LevelChoiceKind.Weapon) or nameof(LevelChoiceKind.Resistance)));
            AddError($"{path}.kind", $"Tipo de elección desconocido. Valores admitidos: {string.Join(", ", names)}.");
        }

        var setId = NullableText($"{path}.setId", rule.SetId, IndexMaxLength);
        if (kind == LevelChoiceKind.OptionSet && setId is null)
        {
            AddError($"{path}.setId", "Obligatorio en las elecciones OptionSet.");
        }

        if (setId is not null && kind is not LevelChoiceKind.AsiOrFeat)
        {
            _setReferences.Add(($"{path}.setId", setId));
        }

        var from = new List<string>();
        ForEachText($"{path}.from", rule.From, (fromPath, value) =>
        {
            from.Add(value);

            // Any option of the catalog or the pack may be named: a set can reuse options of other sets.
            if (setId is not null && kind is LevelChoiceKind.OptionSet or LevelChoiceKind.Custom)
            {
                _optionReferences.Add((fromPath, null, value));
            }
            else if (kind == LevelChoiceKind.SavingThrow && !Abilities.IsValid(value.ToLowerInvariant()))
            {
                AddError(fromPath, "Característica desconocida (str, dex, con, int, wis o cha).");
            }
            else if (kind == LevelChoiceKind.Resistance && !KnownDamageType(value.ToLowerInvariant()))
            {
                AddError(fromPath, $"Tipo de daño desconocido. Valores admitidos: {string.Join(", ", DamageTypeIndexes)}.");
            }
            else if (kind == LevelChoiceKind.Tool)
            {
                ToolReference(fromPath, value);
            }
        });

        var alsoFrom = new List<string>();
        if (rule.AlsoFrom is not null && kind is not (LevelChoiceKind.AsiOrFeat or LevelChoiceKind.OptionSet))
        {
            AddError($"{path}.alsoFrom", "Solo se admite en las elecciones AsiOrFeat y OptionSet.");
        }
        else
        {
            ForEachText($"{path}.alsoFrom", rule.AlsoFrom, (alsoPath, value) =>
            {
                if (value == setId || (kind == LevelChoiceKind.AsiOrFeat && value == OptionSets.Feats))
                {
                    AddError(alsoPath, "Ya es el conjunto de la elección.");
                }
                else if (!alsoFrom.Contains(value))
                {
                    alsoFrom.Add(value);
                    _setReferences.Add((alsoPath, value));
                }
            });
        }

        // Spell choices of a subclass with spellcasting may leave "choose" out: the increase of its known table is used.
        var knownTable = kind switch
        {
            LevelChoiceKind.SpellsKnown => spellcasting?.SpellsKnown,
            LevelChoiceKind.CantripsKnown => spellcasting?.CantripsKnown,
            _ => null,
        };
        var choose = rule.Choose is null && knownTable is { Count: > 0 }
            ? 0
            : RequiredInt($"{path}.choose", rule.Choose, 0, 20);
        var filter = Filter($"{path}.filter", rule.Filter);
        var note = OptionalText($"{path}.note", rule.Note, LevelChoiceRule.NoteMaxLength);
        string? after = null;
        if (rule.After is not null)
        {
            after = rule.After.Trim();
            if (after.Length == 0 || after.Length > LevelChoiceRule.KeyMaxLength || !IndexPattern().IsMatch(after))
            {
                AddError($"{path}.after", "Debe ser la key de otra elección del mismo nivel (minúsculas, números y guiones).");
                after = null;
            }
        }

        return new ParsedLevelChoice(
            level,
            key,
            name,
            kind,
            kind == LevelChoiceKind.AsiOrFeat ? null : setId,
            choose,
            rule.From is null ? null : LevelChoiceJson.Serialize(from),
            filter,
            note,
            after,
            alsoFrom.Count == 0 ? null : LevelChoiceJson.Serialize(alsoFrom),
            rule.Replaces ?? false,
            rule.Cumulative ?? false);
    }

    private void LevelChoice(string path, PackLevelChoiceJson rule, string classIndex, string? subclassIndex, ContentPackRows rows, SubclassSpellcasting? spellcasting = null)
    {
        var parsed = ParseLevelChoice(path, rule, levelRequired: true, spellcasting);
        if (parsed is not { Level: { } level, Kind: { } kind, Choose: { } choose } || parsed.Key.Length == 0)
        {
            return;
        }

        var key = parsed.Key;
        if (parsed.After is { } after)
        {
            _afterReferences.Add(($"{path}.after", classIndex, subclassIndex, level, key, after));
        }

        var id = LevelChoiceRule.IdFor(classIndex, subclassIndex, level, key);
        if (!Record("levelChoiceRules", id, path))
        {
            return;
        }

        rows.LevelChoiceRules.Add(new LevelChoiceRule
        {
            Id = id,
            ClassIndex = classIndex,
            SubclassIndex = subclassIndex,
            Level = level,
            Key = key,
            Name = parsed.Name,
            Kind = kind,
            SetId = parsed.SetId,
            Choose = choose,
            FromJson = parsed.FromJson,
            FilterJson = parsed.Filter,
            Replaces = parsed.Replaces,
            Cumulative = parsed.Cumulative,
            Note = parsed.Note,
            After = parsed.After,
            AlsoFromJson = parsed.AlsoFromJson,
            Source = _id,
        });
    }

    /// <summary>Kinds of the choices an option carries.</summary>
    private static readonly LevelChoiceKind[] OptionChoiceKinds =
    [
        LevelChoiceKind.OptionSet, LevelChoiceKind.Skill, LevelChoiceKind.Expertise, LevelChoiceKind.Tool, LevelChoiceKind.Language,
        LevelChoiceKind.CantripsKnown, LevelChoiceKind.SpellsKnown, LevelChoiceKind.SavingThrow, LevelChoiceKind.Weapon, LevelChoiceKind.Resistance,
        LevelChoiceKind.Custom,
    ];

    /// <summary>
    /// <c>choices</c> (made when the option is taken) or <c>levelChoices</c> (at later class levels) of an option or a
    /// feat, as the JSON of <see cref="OptionChoiceRule"/>; null when there are none.
    /// </summary>
    private string? OptionChoices(string path, List<PackLevelChoiceJson?>? rules, string? optionIndex, bool atLevels)
    {
        if (rules is null)
        {
            return null;
        }

        var result = new List<OptionChoiceRule>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        ForEach(path, rules, (rulePath, rule) =>
        {
            if (rule.After is not null)
            {
                AddError($"{rulePath}.after", "No se admite en las elecciones de una opción.");
            }

            var parsed = ParseLevelChoice(rulePath, rule, levelRequired: atLevels, allowedKinds: OptionChoiceKinds);
            if (parsed is not { Kind: { } kind, Choose: { } choose } || parsed.Key.Length == 0)
            {
                return;
            }

            if (!keys.Add(atLevels ? $"{parsed.Key}@{parsed.Level}" : parsed.Key))
            {
                AddError($"{rulePath}.key", "Key repetida en la opción.");
                return;
            }

            if (optionIndex is not null && OptionChoiceRule.KeyFor(optionIndex, parsed.Key).Length > Dnd5eCharacter.IndexMaxLength)
            {
                AddError($"{rulePath}.key", $"El índice de la opción y la key juntos superan {Dnd5eCharacter.IndexMaxLength} caracteres.");
                return;
            }

            result.Add(new OptionChoiceRule(parsed.Level, parsed.Key, parsed.Name, kind, parsed.SetId, choose, parsed.FromJson, parsed.Filter, parsed.Note, parsed.Replaces, parsed.Cumulative, parsed.AlsoFromJson));
        });

        return result.Count == 0 ? null : OptionChoiceRule.Serialize(result);
    }

    private string? Filter(string path, PackChoiceFilterJson? filter)
    {
        if (filter is null)
        {
            return null;
        }

        var spellList = NullableText($"{path}.spellList", filter.SpellList, IndexMaxLength)?.ToLowerInvariant();
        if (spellList is not null && spellList != ChoiceFilter.AnyList && !_classes.ContainsKey(spellList))
        {
            AddError($"{path}.spellList", "Debe ser una clase del catálogo o \"any\".");
        }

        var levels = new List<int>();
        for (var i = 0; i < (filter.SpellLevels?.Count ?? 0); i++)
        {
            if (RequiredInt($"{path}.spellLevels[{i}]", filter.SpellLevels![i], 0, 9) is { } spellLevel && !levels.Contains(spellLevel))
            {
                levels.Add(spellLevel);
            }
        }

        var source = NullableText($"{path}.source", filter.Source, ShortTextMaxLength)?.ToLowerInvariant();
        if (source is not null && !FilterSources.Contains(source))
        {
            AddError($"{path}.source", $"Valores admitidos: {string.Join(", ", FilterSources)}.");
        }

        var schools = new List<string>();
        ForEachText($"{path}.schools", filter.Schools, (schoolPath, value) =>
        {
            var school = value.ToLowerInvariant();
            if (!SpellSchools.All.Contains(school, StringComparer.Ordinal))
            {
                AddError(schoolPath, $"Escuela desconocida. Valores admitidos: {string.Join(", ", SpellSchools.All)}.");
            }
            else if (!schools.Contains(school))
            {
                schools.Add(school);
            }
        });

        var exceptAt = new List<int>();
        for (var i = 0; i < (filter.SchoolsExceptAt?.Count ?? 0); i++)
        {
            if (RequiredInt($"{path}.schoolsExceptAt[{i}]", filter.SchoolsExceptAt![i], 1, 20) is { } exceptLevel && !exceptAt.Contains(exceptLevel))
            {
                exceptAt.Add(exceptLevel);
            }
        }

        if (exceptAt.Count > 0 && schools.Count == 0)
        {
            AddError($"{path}.schoolsExceptAt", "Solo tiene sentido junto con schools.");
        }

        var weaponCategories = new List<string>();
        ForEachText($"{path}.weaponCategories", filter.WeaponCategories, (categoryPath, value) =>
        {
            var category = value.ToLowerInvariant();
            if (!WeaponCategories.Contains(category))
            {
                AddError(categoryPath, $"Valores admitidos: {string.Join(", ", WeaponCategories)}.");
            }
            else if (!weaponCategories.Contains(category))
            {
                weaponCategories.Add(category);
            }
        });

        var tags = SpellTagList($"{path}.tags", filter.Tags);
        return LevelChoiceJson.Serialize(new
        {
            tags = tags.Count > 0 ? tags : null,
            spellList,
            spellLevels = levels,
            maxSpellLevelBySlots = filter.MaxSpellLevelBySlots ?? false,
            source,
            cantripsOnly = filter.CantripsOnly ?? false,
            schools = schools.Count > 0 ? schools : null,
            schoolsExceptAt = exceptAt.Count > 0 ? exceptAt.Order().ToList() : null,
            weaponCategories = weaponCategories.Count > 0 ? weaponCategories : null,
            firearms = filter.Firearms,
            ritualOnly = filter.RitualOnly == true ? true : (bool?)null,
        });
    }

    /// <summary>An index of the catalog (no pack prefix required): trimmed, lowercase letters, digits and dashes.</summary>
    private string? Reference(string path, string? value)
    {
        var index = value?.Trim();
        if (string.IsNullOrEmpty(index))
        {
            return null;
        }

        if (index.Length > IndexMaxLength || !IndexPattern().IsMatch(index))
        {
            AddError(path, $"Índice no válido: minúsculas, números y guiones, como máximo {IndexMaxLength} caracteres.");
            return null;
        }

        return index;
    }

    /// <summary>Checks the sets, options and spells referenced by the pack against the catalog (other sources) and the pack.</summary>
    private void CheckLevelChoiceReferences(ContentPackRows rows)
    {
        var sets = (_context.OptionSets ?? new Dictionary<string, string>())
            .Where(s => IsOtherSource(s.Value))
            .Select(s => s.Key)
            .Concat(rows.OptionSets.Select(s => s.SetId))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var (path, setId) in _setReferences.Where(r => !sets.Contains(r.SetId)))
        {
            AddError(path, $"El conjunto '{setId}' no existe en el catálogo ni en el paquete.");
        }

        var options = (_context.Options ?? new Dictionary<string, (string SetId, string Source)>())
            .Where(o => IsOtherSource(o.Value.Source))
            .Select(o => (Index: o.Key, o.Value.SetId))
            .Concat(rows.Options.Select(o => (o.Index, o.SetId)))
            .ToLookup(o => o.Index, o => o.SetId, StringComparer.Ordinal);
        foreach (var (path, setId, index) in _optionReferences)
        {
            if (!options.Contains(index) || (setId is not null && !options[index].Contains(setId, StringComparer.Ordinal)))
            {
                AddError(path, setId is null
                    ? $"La opción '{index}' no existe en el catálogo ni en el paquete."
                    : $"La opción '{index}' no existe en el conjunto '{setId}'.");
            }
        }

        var spells = (_context.Spells ?? new Dictionary<string, string>())
            .Where(s => IsOtherSource(s.Value))
            .Select(s => s.Key)
            .Concat(rows.Spells.Select(s => s.Index))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var (path, index) in _spellReferences.Where(r => !spells.Contains(r.Index)))
        {
            AddError(path, $"El conjuro '{index}' no existe en el catálogo ni en el paquete.");
        }

        var races = (_context.Races ?? new Dictionary<string, string>())
            .Where(r => IsOtherSource(r.Value))
            .Select(r => r.Key)
            .Concat(rows.Races.Select(r => r.Index))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var (path, index) in _raceReferences.Where(r => !races.Contains(r.Index)))
        {
            AddError(path, $"La raza '{index}' no existe en el catálogo ni en el paquete.");
        }

        var classes = _context.Classes.Keys.Concat(rows.Classes.Select(c => c.Index)).ToHashSet(StringComparer.Ordinal);
        foreach (var (path, index) in _classReferences.Where(r => !classes.Contains(r.Index)))
        {
            AddError(path, $"La clase '{index}' no existe en el catálogo ni en el paquete.");
        }

        var subclasses = _context.SrdSubclasses.Keys.Concat(rows.Subclasses.Select(c => c.Index)).ToHashSet(StringComparer.Ordinal);
        foreach (var (path, index) in _subclassReferences.Where(r => !subclasses.Contains(r.Index)))
        {
            AddError(path, $"La subclase '{index}' no existe en el catálogo ni en el paquete.");
        }
    }
}
