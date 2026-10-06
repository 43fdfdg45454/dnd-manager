using System.Text.Json;
using Dnd.Application.Common;
using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using Dnd.Domain.Items;

namespace Dnd.Infrastructure.Catalog;

// Format 2 of the content packs: option sets (new ones, or options added to sets of the SRD or of other packs),
// level choices of classes and subclasses, and grants (options and subclass levels). References to sets,
// options and spells are checked at the end, against the catalog and the pack itself.
internal sealed partial class ContentPackValidator
{
    public static readonly IReadOnlyList<string> FilterSources = [ChoiceFilter.ListSource, ChoiceFilter.SpellbookSource, ChoiceFilter.KnownSource];

    private readonly List<(string Path, string SetId)> _setReferences = [];
    private readonly List<(string Path, string? SetId, string Index)> _optionReferences = [];
    private readonly List<(string Path, string Index)> _spellReferences = [];
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

        return LevelChoiceJson.Serialize(new { minLevel, pactBoon, cantrip, abilities });
    }

    private string ChoiceModifiers(string path, List<PackChoiceModifierJson?>? modifiers)
    {
        var result = new List<object>();
        if (modifiers is { Count: > ItemLimits.MaxModifiers })
        {
            AddError(path, $"Una opción admite como máximo {ItemLimits.MaxModifiers} modificadores.");
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

    private string Grants(string path, PackGrantsJson grants)
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
            if (index is null)
            {
                AddError($"{spellPath}.index", "Campo obligatorio.");
                return;
            }

            _spellReferences.Add(($"{spellPath}.index", index));
            spells.Add(new { index, minLevel });
        });

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
        string? max = resource.Max switch
        {
            { ValueKind: JsonValueKind.Number } number when number.TryGetInt32(out var value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            { ValueKind: JsonValueKind.String } text => text.GetString()?.Trim(),
            _ => null,
        };
        if (max is null || !OptionResource.IsValidMax(max))
        {
            AddError($"{path}.max", $"Debe ser un entero entre 1 y {CharacterResource.MaxUses} o una fórmula: proficiencyBonus, classLevel, halfClassLevel o mod:<característica>.");
        }

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

        return rollOnRest is null
            ? LevelChoiceJson.Serialize(new { key, name, max, recharge = recharge.ToString() })
            : LevelChoiceJson.Serialize(new { key, name, max, recharge = recharge.ToString(), rollOnRest });
    }

    private void LevelChoice(string path, PackLevelChoiceJson rule, string classIndex, string? subclassIndex, ContentPackRows rows)
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

        var choose = RequiredInt($"{path}.choose", rule.Choose, 0, 20);
        var filter = Filter($"{path}.filter", rule.Filter);
        var note = OptionalText($"{path}.note", rule.Note, LevelChoiceRule.NoteMaxLength);
        if (level is null || key.Length == 0 || kind is null || choose is null)
        {
            return;
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

        return LevelChoiceJson.Serialize(new
        {
            spellList,
            spellLevels = levels,
            maxSpellLevelBySlots = filter.MaxSpellLevelBySlots ?? false,
            source,
            cantripsOnly = filter.CantripsOnly ?? false,
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
    }
}
