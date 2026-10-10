using System.Globalization;
using System.Text.Json;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Infrastructure.Catalog;

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

    private void Option(string path, PackOptionJson option, string setId, ContentPackRows rows)
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
            Source = _id,
        };

        if (index is not null)
        {
            rows.Options.Add(definition);
        }
    }

    private string? Prerequisites(string path, PackPrerequisitesJson? prerequisites)
    {
        if (prerequisites is null)
        {
            return null;
        }

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
                AddError(armorPath, "Armadura desconocida. Valores admitidos: light, medium, heavy, shields.");
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
        return LevelChoiceJson.Serialize(new { minLevel, pactBoon, cantrip, abilities, races, proficiency = new { armor, weapon }, spellcasting });
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

            if (modifier.Value is not { } value)
            {
                AddError($"{modifierPath}.value", "Campo obligatorio.");
                return;
            }

            var candidate = new ItemModifier(kind, modifier.Target, value);
            if (candidate.Validate() is { } error)
            {
                AddError(modifierPath, error);
                return;
            }

            var condition = NullableText($"{modifierPath}.condition", modifier.Condition, ShortTextMaxLength);
            if (!ModifierConditions.IsValid(condition))
            {
                AddError($"{modifierPath}.condition", $"Condición desconocida. Valores admitidos: {string.Join(", ", ModifierConditions.All)}.");
                return;
            }

            var normalized = candidate.Normalize();
            result.Add(new { kind = normalized.Kind.ToString(), target = normalized.Target, value = normalized.Value, condition });
        });

        return LevelChoiceJson.Serialize(result);
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
            if (_context.Skills.ContainsKey(key))
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
                if (origin)
                {
                    usesPerLongRest = OptionalInt($"{spellPath}.usesPerLongRest", spell.UsesPerLongRest, 1, 20);
                }
                else
                {
                    AddError($"{spellPath}.usesPerLongRest", "Solo se admite en las razas y subrazas (para opciones, usa resource).");
                }
            }

            if (index is null)
            {
                AddError($"{spellPath}.index", "Campo obligatorio.");
                return;
            }

            _spellReferences.Add(($"{spellPath}.index", index));
            spells.Add(new { index, minLevel, usesPerLongRest });
        });

        string? spellcastingAbility = null;
        var ability = grants.SpellcastingAbility?.Trim().ToLowerInvariant();
        if (!origin)
        {
            if (!string.IsNullOrEmpty(ability))
            {
                AddError($"{path}.spellcastingAbility", "Solo se admite en las razas y subrazas.");
            }
        }
        else if (string.IsNullOrEmpty(ability))
        {
            if (spells.Count + cantrips.Count > 0)
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

        return LevelChoiceJson.Serialize(new
        {
            skills,
            cantrips,
            spells,
            armor = TextList($"{path}.armor", grants.Armor, 50, ShortTextMaxLength),
            weapons = TextList($"{path}.weapons", grants.Weapons, 50, ShortTextMaxLength),
            tools = TextList($"{path}.tools", grants.Tools, 50, ShortTextMaxLength),
            languages = TextList($"{path}.languages", grants.Languages, 50, ShortTextMaxLength),
            savingThrows,
            spellcastingAbility,
        });
    }

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
        "classLevel, halfClassLevel o mod:<característica>), { \"formula\": \"...\", \"min\": 0 } o { \"byLevel\": { \"3\": 4 } }";

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
            AddError($"{path}.{unknown}", "Campo desconocido: usa formula y min, o byLevel.");
            return null;
        }

        if (properties.TryGetValue("byLevel", out var byLevel))
        {
            if (properties.Count > 1)
            {
                AddError(path, "byLevel no se combina con formula ni min.");
                return null;
            }

            if (byLevel.ValueKind != JsonValueKind.Object)
            {
                AddError($"{path}.byLevel", "Debe ser un objeto { \"nivel\": usos }.");
                return null;
            }

            var entries = byLevel.EnumerateObject().ToDictionary(
                p => p.Name,
                p => (string?)(p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetRawText() : null),
                StringComparer.Ordinal);
            var table = LevelTable($"{path}.byLevel", entries, (entryPath, text) =>
            {
                if (int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var uses)
                    && uses is >= 0 and <= CharacterResource.MaxUses)
                {
                    return uses;
                }

                AddError(entryPath, $"Debe ser un entero entre 0 y {CharacterResource.MaxUses}.");
                return (int?)null;
            });
            return table is null ? null : new { byLevel = table };
        }

        var min = 1;
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

        if (!OptionResource.IsValidMax(formula, min))
        {
            AddError($"{path}.formula", $"Fórmula no válida: debe ser {ResourceMaxHelp}.");
            return null;
        }

        return new { formula, min };
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
        if (_context.CasterClasses?.Contains(classIndex) == true)
        {
            AddError(path, $"La clase '{classIndex}' ya lanza conjuros: spellcasting solo vale en subclases de clases que no lanzan conjuros.");
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
        else if (!_context.Classes.ContainsKey(spellList))
        {
            AddError($"{path}.spellList", "Debe ser una clase del catálogo.");
            spellList = null;
        }

        var cantrips = KnownTable($"{path}.cantripsKnown", spellcasting.CantripsKnown, 10);
        var spells = KnownTable($"{path}.spellsKnown", spellcasting.SpellsKnown, 30);
        return valid && level is not null && !string.IsNullOrEmpty(ability) && !string.IsNullOrEmpty(spellList) && cantrips is not null && spells is not null
            && (spellcasting.FromLevel is null || fromLevel is not null)
            ? new SubclassSpellcasting(level.Value, ability, fromLevel ?? 1, spellList, cantrips, spells)
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

    private void LevelChoice(string path, PackLevelChoiceJson rule, string classIndex, string? subclassIndex, ContentPackRows rows, SubclassSpellcasting? spellcasting = null)
    {
        var level = RequiredInt($"{path}.level", rule.Level, 1, 20);
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
        else if (EnumNames.TryParse<LevelChoiceKind>(rule.Kind.Trim(), out var parsed))
        {
            kind = parsed;
        }
        else
        {
            AddError($"{path}.kind", $"Tipo de elección desconocido. Valores admitidos: {string.Join(", ", Enum.GetNames<LevelChoiceKind>())}.");
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
            if (setId is not null && kind is LevelChoiceKind.OptionSet or LevelChoiceKind.Custom)
            {
                _optionReferences.Add((fromPath, setId, value));
            }
        });

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

        if (level is null || key.Length == 0 || kind is null || choose is null)
        {
            return;
        }

        if (after is not null)
        {
            _afterReferences.Add(($"{path}.after", classIndex, subclassIndex, level.Value, key, after));
        }

        var id = LevelChoiceRule.IdFor(classIndex, subclassIndex, level.Value, key);
        if (!Record("levelChoiceRules", id, path))
        {
            return;
        }

        rows.LevelChoiceRules.Add(new LevelChoiceRule
        {
            Id = id,
            ClassIndex = classIndex,
            SubclassIndex = subclassIndex,
            Level = level.Value,
            Key = key,
            Name = name,
            Kind = kind.Value,
            SetId = kind == LevelChoiceKind.AsiOrFeat ? null : setId,
            Choose = choose.Value,
            FromJson = rule.From is null ? null : LevelChoiceJson.Serialize(from),
            FilterJson = filter,
            Replaces = rule.Replaces ?? false,
            Cumulative = rule.Cumulative ?? false,
            Note = note,
            After = after,
            Source = _id,
        });
    }

    private string? Filter(string path, PackChoiceFilterJson? filter)
    {
        if (filter is null)
        {
            return null;
        }

        var spellList = NullableText($"{path}.spellList", filter.SpellList, IndexMaxLength)?.ToLowerInvariant();
        if (spellList is not null && spellList != ChoiceFilter.AnyList && !_context.Classes.ContainsKey(spellList))
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

        return LevelChoiceJson.Serialize(new
        {
            spellList,
            spellLevels = levels,
            maxSpellLevelBySlots = filter.MaxSpellLevelBySlots ?? false,
            source,
            cantripsOnly = filter.CantripsOnly ?? false,
            schools = schools.Count > 0 ? schools : null,
            schoolsExceptAt = exceptAt.Count > 0 ? exceptAt.Order().ToList() : null,
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
    }
}
