using System.Globalization;
using System.Text.Json;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// Format 3 of the content packs: full classes (20 levels, spellcasting, resources and subclasses), feats, creatures,
// conditions, rules documents, vocabularies and requires.
internal sealed partial class ContentPackValidator
{
    public const string DefaultSubclassFlavor = "Subclass";
    public const int MaxClassSpecificEntries = 30;
    public const int MaxLevels = 20;

    /// <summary>Levels with an Ability Score Improvement when a class does not mark them.</summary>
    public static readonly IReadOnlyList<int> StandardAsiLevels = [4, 8, 12, 16, 19];

    private static readonly IReadOnlyList<int> HitDice = [4, 6, 8, 10, 12];

    private readonly List<(string Path, string Index)> _classSpellListReferences = [];

    /// <summary>Proficiency bonus of the standard table at a character level.</summary>
    public static int StandardProficiencyBonus(int level) => 2 + ((Math.Clamp(level, 1, 20) - 1) / 4);

    /// <summary>Pact Magic table (warlock): slots and their level by class level, as a 9-entry slots row.</summary>
    public static int[] PactSlots(int classLevel)
    {
        var slots = new int[ClassLevel.SpellSlotLevels];
        var count = classLevel switch { 1 => 1, <= 10 => 2, <= 16 => 3, _ => 4 };
        var slotLevel = Math.Min(5, (classLevel + 1) / 2);
        slots[slotLevel - 1] = count;
        return slots;
    }

    private void RequiresOf(List<string?>? requires)
    {
        var result = new List<string>();
        ForEachText("requires", requires, (path, value) =>
        {
            if (value == _id)
            {
                AddError(path, "Un paquete no puede requerirse a sí mismo.");
            }
            else if (_context.Packs is not { } packs || !packs.TryGetValue(value, out var system))
            {
                AddError(path, $"El paquete '{value}' no está importado: impórtalo antes que este.");
            }
            else if (system != Dnd5eCatalogSources.SystemId)
            {
                AddError(path, $"El paquete '{value}' es de otro sistema.");
            }
            else if (!result.Contains(value))
            {
                result.Add(value);
            }
        });
        Requires = result;
    }

    // ---- Classes ---------------------------------------------------------------------------------

    private void Class(string path, PackClassJson definition, ContentPackRows rows, Dictionary<string, string> packSubclasses)
    {
        var index = Index($"{path}.index", "classes", definition.Index);
        var name = RequiredText($"{path}.name", definition.Name, NameMaxLength);
        var hitDie = RequiredInt($"{path}.hitDie", definition.HitDie, 4, 12);
        if (hitDie is { } die && !HitDice.Contains(die))
        {
            AddError($"{path}.hitDie", "Debe ser 4, 6, 8, 10 o 12.");
        }

        var savingThrows = new List<string>();
        ForEachText($"{path}.savingThrows", definition.SavingThrows, (savePath, value) =>
        {
            var ability = value.ToLowerInvariant();
            if (!Abilities.IsValid(ability))
            {
                AddError(savePath, "Característica desconocida (str, dex, con, int, wis o cha).");
            }
            else if (!savingThrows.Contains(ability))
            {
                savingThrows.Add(ability);
            }
        });

        var proficiencies = new List<string>();
        if (definition.Proficiencies is { } classProficiencies)
        {
            proficiencies.AddRange(TextList($"{path}.proficiencies.armor", classProficiencies.Armor, 50, ShortTextMaxLength));
            proficiencies.AddRange(TextList($"{path}.proficiencies.weapons", classProficiencies.Weapons, 50, ShortTextMaxLength));
            proficiencies.AddRange(TextList($"{path}.proficiencies.tools", classProficiencies.Tools, 50, ShortTextMaxLength));
        }

        var skillChoices = ClassSkillChoices($"{path}.skillChoices", definition.SkillChoices);
        var startingEquipment = definition.StartingEquipment is { } equipment
            ? ParseStartingEquipment($"{path}.startingEquipment", equipment, allowGold: true)
            : null;
        var multiclassing = Multiclassing($"{path}.multiclassing", definition.Multiclassing);
        var description = Paragraphs($"{path}.description", definition.Description);
        var flavor = OptionalText($"{path}.subclassFlavor", definition.SubclassFlavor, NameMaxLength);
        var subclassLevel = OptionalInt($"{path}.subclassLevel", definition.SubclassLevel, 1, MaxLevels);
        var spellcasting = definition.Spellcasting is { } casting ? ClassSpellcasting($"{path}.spellcasting", casting) : null;
        var table = SpellcastingTable(path, definition);

        if (index is null)
        {
            return;
        }

        // Levels: 1-20 without gaps, each once.
        var levels = new Dictionary<int, (string Path, PackClassLevelJson Level)>();
        ForEach($"{path}.levels", definition.Levels, (levelPath, level) =>
        {
            if (RequiredInt($"{levelPath}.level", level.Level, 1, MaxLevels) is not { } number)
            {
                return;
            }

            if (!levels.TryAdd(number, (levelPath, level)))
            {
                AddError($"{levelPath}.level", "Nivel duplicado en la clase.");
            }
        });
        var missing = Enumerable.Range(1, MaxLevels).Where(l => !levels.ContainsKey(l)).ToList();
        if (missing.Count > 0)
        {
            AddError($"{path}.levels", $"Faltan niveles: {string.Join(", ", missing)} (una clase cubre del 1 al 20).");
        }

        var asiMarked = levels.Values.Any(l => l.Level.AbilityScoreImprovement is not null);
        var asiLevels = asiMarked
            ? levels.Where(l => l.Value.Level.AbilityScoreImprovement == true).Select(l => l.Key).ToHashSet()
            : StandardAsiLevels.ToHashSet();
        var classSpecific = new Dictionary<int, Dictionary<string, JsonElement>>();
        var asiSoFar = 0;
        foreach (var number in Enumerable.Range(1, MaxLevels))
        {
            if (!levels.TryGetValue(number, out var entry))
            {
                continue;
            }

            var (levelPath, level) = entry;
            var profBonus = OptionalInt($"{levelPath}.profBonus", level.ProfBonus, 2, 6) ?? StandardProficiencyBonus(number);
            var features = Features($"{levelPath}.features", level.Features, index, null, number, rows);
            var specific = ClassSpecific($"{levelPath}.classSpecific", level.ClassSpecific);
            classSpecific[number] = specific;
            if (asiLevels.Contains(number))
            {
                asiSoFar++;
            }

            var (tableSlots, tableCantrips, tableSpells) = LevelSpellcasting(levelPath, level, table is not null);
            rows.ClassLevels.Add(new ClassLevel
            {
                Index = $"{index}-{number}",
                ClassIndex = index,
                Level = number,
                ProfBonus = profBonus,
                AbilityScoreBonuses = asiSoFar,
                FeatureIndexes = features,
                ClassSpecificJson = JsonSerializer.Serialize(specific),
                CantripsKnown = spellcasting is null ? tableCantrips : Domain.Catalog.SubclassSpellcasting.At(spellcasting.Value.Cantrips, number),
                SpellsKnown = spellcasting is null ? tableSpells : Domain.Catalog.SubclassSpellcasting.At(spellcasting.Value.Spells, number),
                SpellSlots = spellcasting is null ? tableSlots : SlotsAt(spellcasting.Value, number),
                Source = _id,
            });
        }

        FeaturesWithLevel($"{path}.features", definition.Features, index, null, rows);

        var resources = new List<string>();
        ForEach($"{path}.resources", definition.Resources, (resourcePath, resource) =>
        {
            var max = resource.Max;
            if (max is { ValueKind: JsonValueKind.String } text && text.GetString() is { } formula
                && formula.StartsWith("classSpecific:", StringComparison.Ordinal))
            {
                var key = formula["classSpecific:".Length..];
                var byLevel = classSpecific
                    .Where(l => l.Value.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
                    .ToDictionary(l => l.Key.ToString(CultureInfo.InvariantCulture), l => l.Value[key].GetInt32());
                if (byLevel.Count == 0)
                {
                    AddError($"{resourcePath}.max", $"Ningún nivel de la clase tiene el valor numérico classSpecific \"{key}\".");
                    return;
                }

                max = JsonSerializer.SerializeToElement(new { byLevel });
            }

            if (Resource(resourcePath, resource.ToResource(max)) is { } json)
            {
                resources.Add(json);
            }
        });

        var spellList = ClassSpellListOf($"{path}.spellList", definition.SpellList);

        // Explicit level choices first: a generated one with the same level and key is left out.
        ForEach($"{path}.levelChoices", definition.LevelChoices, (rulePath, rule) => LevelChoice(rulePath, rule, index, null, rows));
        var hasSubclasses = definition.Subclasses is { Count: > 0 };
        GenerateLevelChoices(path, index, flavor.Length > 0 ? flavor : DefaultSubclassFlavor, subclassLevel ?? (hasSubclasses ? 3 : null), asiLevels, spellcasting, rows);

        rows.Classes.Add(new ClassDefinition
        {
            Index = index,
            Name = name,
            HitDie = hitDie ?? 8,
            SavingThrows = savingThrows,
            ProficiencyNames = proficiencies,
            SpellcastingAbility = spellcasting?.Ability ?? table?.Ability,
            IsSpellcaster = spellcasting is not null || table is not null,
            SpellcastingLevel = spellcasting is not null ? ClassSpellcastingInfo.SpellcastingLevelOf(spellcasting.Value.Info.Progression)
                : table is not null ? ClassSpellcastingInfo.SpellcastingLevelOf(table.Value.Multiclass) : 0,
            IsPactCaster = (spellcasting?.Info.Progression ?? table?.Multiclass) == ClassSpellcastingInfo.Pact,
            SubclassFlavor = flavor.Length > 0 ? flavor : DefaultSubclassFlavor,
            SkillChoicesJson = skillChoices,
            StartingEquipmentText = OptionalText($"{path}.startingEquipmentText", definition.StartingEquipmentText, LongTextMaxLength),
            StartingEquipmentJson = startingEquipment,
            Source = _id,
            Description = description,
            SubclassLevel = subclassLevel ?? 0,
            SpellcastingJson = spellcasting?.Info.ToJson(),
            MulticlassJson = multiclassing?.ToJson(),
            ResourcesJson = resources.Count == 0 ? null : $"[{string.Join(",", resources)}]",
            SpellListJson = spellList?.ToJson(),
        });

        Subclasses($"{path}.subclasses", definition.Subclasses, index, flavor.Length > 0 ? flavor : DefaultSubclassFlavor, rows, packSubclasses);
    }

    /// <summary>Spellcasting given as tables in the levels (<c>spellcastingAbility</c>, as the SRD classes).</summary>
    private readonly record struct SpellcastingTableInfo(string Ability, string Multiclass);

    private static readonly IReadOnlyList<string> MulticlassSpellcastingValues =
        [ClassSpellcastingInfo.Full, ClassSpellcastingInfo.Half, ClassSpellcastingInfo.Third, ClassSpellcastingInfo.Pact, "none"];

    /// <summary><c>spellcastingAbility</c> and <c>multiclassSpellcasting</c> of a class; null when the class has none.</summary>
    private SpellcastingTableInfo? SpellcastingTable(string path, PackClassJson definition)
    {
        if (definition.SpellcastingAbility is null)
        {
            if (definition.MulticlassSpellcasting is not null)
            {
                AddError($"{path}.multiclassSpellcasting", "Solo se admite con spellcastingAbility.");
            }

            return null;
        }

        if (definition.Spellcasting is not null)
        {
            AddError($"{path}.spellcastingAbility", "Usa spellcasting o spellcastingAbility con las tablas de los niveles, no ambos.");
            return null;
        }

        var ability = definition.SpellcastingAbility.Trim().ToLowerInvariant();
        if (!Abilities.IsValid(ability))
        {
            AddError($"{path}.spellcastingAbility", $"Característica desconocida. Valores admitidos: {string.Join(", ", Abilities.All)}.");
            return null;
        }

        var multiclass = definition.MulticlassSpellcasting?.Trim().ToLowerInvariant() ?? ClassSpellcastingInfo.Full;
        if (!MulticlassSpellcastingValues.Contains(multiclass))
        {
            AddError($"{path}.multiclassSpellcasting", $"Valores admitidos: {string.Join(", ", MulticlassSpellcastingValues)}.");
            return null;
        }

        return new SpellcastingTableInfo(ability, multiclass);
    }

    /// <summary>The spellcasting tables of a class level (<c>spellSlots</c>, <c>cantripsKnown</c>, <c>spellsKnown</c>).</summary>
    private (int[] Slots, int? Cantrips, int? Spells) LevelSpellcasting(string path, PackClassLevelJson level, bool allowed)
    {
        var slots = new int[ClassLevel.SpellSlotLevels];
        if (level.SpellSlots is null && level.CantripsKnown is null && level.SpellsKnown is null)
        {
            return (slots, null, null);
        }

        if (!allowed)
        {
            AddError(path, "spellSlots, cantripsKnown y spellsKnown solo se admiten en las clases con spellcastingAbility.");
            return (slots, null, null);
        }

        if (level.SpellSlots is { } row)
        {
            if (row.Count is < 1 or > ClassLevel.SpellSlotLevels)
            {
                AddError($"{path}.spellSlots", "Indica de 1 a 9 cantidades (espacios de nivel 1 a 9).");
            }
            else
            {
                for (var i = 0; i < row.Count; i++)
                {
                    slots[i] = RequiredInt($"{path}.spellSlots[{i}]", row[i], 0, 9) ?? 0;
                }
            }
        }

        return (slots, OptionalInt($"{path}.cantripsKnown", level.CantripsKnown, 0, 10), OptionalInt($"{path}.spellsKnown", level.SpellsKnown, 0, 30));
    }

    private readonly record struct ClassCasting(
        string Ability,
        ClassSpellcastingInfo Info,
        IReadOnlyDictionary<int, IReadOnlyList<int>> Slots,
        IReadOnlyDictionary<int, int> Cantrips,
        IReadOnlyDictionary<int, int> Spells);

    private static IReadOnlyList<int> SlotsAt(ClassCasting casting, int level) => casting.Info.Progression switch
    {
        ClassSpellcastingInfo.Full => SpellSlotTables.ProgressionSlots(1, level),
        ClassSpellcastingInfo.Half => SpellSlotTables.ProgressionSlots(2, level),
        ClassSpellcastingInfo.Third => SpellSlotTables.ProgressionSlots(3, level),
        ClassSpellcastingInfo.Pact when casting.Slots.Count == 0 => PactSlots(level),
        _ => casting.Slots.Where(s => s.Key <= level).OrderByDescending(s => s.Key).Select(s => s.Value).FirstOrDefault()
            ?? new int[ClassLevel.SpellSlotLevels],
    };

    private ClassCasting? ClassSpellcasting(string path, PackClassSpellcastingJson spellcasting)
    {
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

        var progression = spellcasting.Progression?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(progression))
        {
            AddError($"{path}.progression", "Campo obligatorio.");
            progression = null;
        }
        else if (!ClassSpellcastingInfo.Progressions.Contains(progression))
        {
            AddError($"{path}.progression", $"Valores admitidos: {string.Join(", ", ClassSpellcastingInfo.Progressions)}.");
            progression = null;
        }

        var slots = new Dictionary<int, IReadOnlyList<int>>();
        if (spellcasting.Slots is { Count: > 0 } table)
        {
            if (progression is not (ClassSpellcastingInfo.Table or ClassSpellcastingInfo.Pact))
            {
                AddError($"{path}.slots", "Solo se admite con \"progression\": \"table\" (o \"pact\"); las demás progresiones calculan los espacios.");
            }

            foreach (var (key, row) in table)
            {
                if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || level is < 1 or > MaxLevels)
                {
                    AddError($"{path}.slots.{key}", "La clave debe ser un nivel de clase entre 1 y 20.");
                    continue;
                }

                if (row is not { Count: >= 1 and <= ClassLevel.SpellSlotLevels })
                {
                    AddError($"{path}.slots.{key}", "Indica de 1 a 9 cantidades (espacios de nivel 1 a 9).");
                    continue;
                }

                var values = new int[ClassLevel.SpellSlotLevels];
                for (var i = 0; i < row.Count; i++)
                {
                    if (RequiredInt($"{path}.slots.{key}[{i}]", row[i], 0, 9) is { } count)
                    {
                        values[i] = count;
                    }
                }

                slots[level] = values;
            }
        }
        else if (progression == ClassSpellcastingInfo.Table)
        {
            AddError($"{path}.slots", "Obligatorio con \"progression\": \"table\".");
        }

        var preparation = spellcasting.Preparation?.Trim().ToLowerInvariant();
        if (preparation is not (null or ClassSpellcastingInfo.Prepared or ClassSpellcastingInfo.Known))
        {
            AddError($"{path}.preparation", "Debe ser \"prepared\" o \"known\".");
            preparation = null;
        }

        var cantrips = KnownTable($"{path}.cantripsKnown", spellcasting.CantripsKnown, 10);
        var spells = KnownTable($"{path}.spellsKnown", spellcasting.SpellsKnown, 30);
        preparation ??= spells is { Count: > 0 } ? ClassSpellcastingInfo.Known : ClassSpellcastingInfo.Prepared;
        if (preparation == ClassSpellcastingInfo.Known && spells is { Count: 0 })
        {
            AddError($"{path}.spellsKnown", "Un lanzador \"known\" necesita su tabla de conjuros conocidos.");
        }

        var focus = NullableText($"{path}.focus", spellcasting.Focus, ShortTextMaxLength);
        return ability is null || progression is null || cantrips is null || spells is null
            ? null
            : new ClassCasting(ability, new ClassSpellcastingInfo(progression, preparation, spellcasting.Ritual ?? false, focus), slots, cantrips, spells);
    }

    /// <summary>Subclass, Ability Score Improvement, cantrips and spells known choices from the class definition.</summary>
    private void GenerateLevelChoices(
        string path,
        string classIndex,
        string flavor,
        int? subclassLevel,
        IReadOnlySet<int> asiLevels,
        ClassCasting? casting,
        ContentPackRows rows)
    {
        void Add(int level, string key, string name, LevelChoiceKind kind, int choose, string note, string? filter = null, bool replaces = false)
        {
            // An explicit choice of the class replaces the generated one: the same key or kind at that level, or (the
            // subclass) a subclass choice at any level.
            var id = LevelChoiceRule.IdFor(classIndex, null, level, key);
            if ((_seen.TryGetValue("levelChoiceRules", out var seen) && seen.Contains(id))
                || rows.LevelChoiceRules.Any(r => r.ClassIndex == classIndex && r.SubclassIndex is null && r.Kind == kind
                    && (r.Level == level || kind == LevelChoiceKind.Subclass)))
            {
                return;
            }

            Record("levelChoiceRules", id, $"{path}.levels");
            rows.LevelChoiceRules.Add(new LevelChoiceRule
            {
                Id = id,
                ClassIndex = classIndex,
                Level = level,
                Key = key,
                Name = name,
                Kind = kind,
                Choose = choose,
                FilterJson = filter,
                Replaces = replaces,
                Note = note,
                Source = _id,
            });
        }

        if (subclassLevel is { } subclass)
        {
            Add(subclass, "subclass", flavor, LevelChoiceKind.Subclass, 1, "Elige la subclase.");
        }

        foreach (var level in asiLevels.Order())
        {
            Add(level, "asi", "Ability Score Improvement", LevelChoiceKind.AsiOrFeat, 1, "+2 a una característica o +1 a dos (máximo 20), o una dote si el DM las permite.");
        }

        if (casting is not { } spellcasting)
        {
            return;
        }

        for (var level = 2; level <= MaxLevels; level++)
        {
            var cantrips = (Domain.Catalog.SubclassSpellcasting.At(spellcasting.Cantrips, level) ?? 0) - (Domain.Catalog.SubclassSpellcasting.At(spellcasting.Cantrips, level - 1) ?? 0);
            if (cantrips > 0)
            {
                Add(level, "cantrips", "Cantrips Known", LevelChoiceKind.CantripsKnown, cantrips, "Trucos nuevos de la lista de la clase.",
                    $$"""{"spellList":"{{classIndex}}","cantripsOnly":true}""");
            }

            var spells = (Domain.Catalog.SubclassSpellcasting.At(spellcasting.Spells, level) ?? 0) - (Domain.Catalog.SubclassSpellcasting.At(spellcasting.Spells, level - 1) ?? 0);
            if (spellcasting.Info.Preparation == ClassSpellcastingInfo.Known && spells > 0)
            {
                Add(level, "spells-known", "Spells Known", LevelChoiceKind.SpellsKnown, spells,
                    "Conjuros nuevos de la lista de la clase de un nivel con espacios; se puede sustituir uno conocido.",
                    $$"""{"spellList":"{{classIndex}}","maxSpellLevelBySlots":true}""", replaces: true);
            }
        }
    }

    private string ClassSkillChoices(string path, PackPickChoiceJson? choices)
    {
        if (choices is null)
        {
            return "{\"choose\":0,\"from\":[]}";
        }

        var from = new List<string>();
        ForEachText($"{path}.from", choices.From?.Select(o => o?.Index).ToList(), (itemPath, value) =>
        {
            var key = value.ToLowerInvariant();
            if (!_skills.ContainsKey(key))
            {
                AddError(itemPath, $"La habilidad '{value}' no existe (usa índices como \"perception\").");
            }
            else if (!from.Contains(key))
            {
                from.Add(key);
            }
        });
        if (from.Count == 0)
        {
            from.AddRange(_skills.Keys.Order(StringComparer.Ordinal));
        }

        var choose = RequiredInt($"{path}.choose", choices.Choose, 0, from.Count) ?? 0;
        return JsonSerializer.Serialize(new { choose, from });
    }

    private ClassMulticlassing? Multiclassing(string path, PackMulticlassingJson? multiclassing)
    {
        if (multiclassing is null)
        {
            return null;
        }

        var prerequisites = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, value) in multiclassing.Prerequisites ?? [])
        {
            var ability = key.Trim().ToLowerInvariant();
            if (!Abilities.IsValid(ability))
            {
                AddError($"{path}.prerequisites.{key}", "Característica desconocida (str, dex, con, int, wis o cha).");
            }
            else if (RequiredInt($"{path}.prerequisites.{key}", value, 1, 30) is { } score)
            {
                prerequisites[ability] = score;
            }
        }

        var proficiencies = multiclassing.Proficiencies;
        return new ClassMulticlassing(
            prerequisites,
            TextList($"{path}.proficiencies.armor", proficiencies?.Armor, 50, ShortTextMaxLength),
            TextList($"{path}.proficiencies.weapons", proficiencies?.Weapons, 50, ShortTextMaxLength),
            TextList($"{path}.proficiencies.tools", proficiencies?.Tools, 50, ShortTextMaxLength),
            OptionalInt($"{path}.proficiencies.skills", proficiencies?.Skills, 0, 4) ?? 0);
    }

    private Dictionary<string, JsonElement> ClassSpecific(string path, Dictionary<string, JsonElement>? values)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (values is null)
        {
            return result;
        }

        if (values.Count > MaxClassSpecificEntries)
        {
            AddError(path, $"No puede tener más de {MaxClassSpecificEntries} valores.");
            return result;
        }

        foreach (var (key, value) in values)
        {
            if (key.Length is 0 or > IndexMaxLength || !key.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
            {
                AddError($"{path}.{key}", "Clave no válida: letras, números, guiones y guiones bajos.");
                continue;
            }

            result[key] = value.Clone();
        }

        return result;
    }

    /// <summary><c>spellList</c>: spell indexes and <c>{"class": "wizard"}</c> entries (an array, or one object).</summary>
    private ClassSpellList? ClassSpellListOf(string path, JsonElement? value)
    {
        if (value is not { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } list)
        {
            return null;
        }

        var spells = new List<string>();
        var classes = new List<string>();
        void Entry(string entryPath, JsonElement entry)
        {
            if (entry.ValueKind == JsonValueKind.String && entry.GetString()?.Trim() is { Length: > 0 } spell)
            {
                spells.Add(spell);
                _classSpellListReferences.Add((entryPath, spell));
            }
            else if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("class", out var classValue)
                     && classValue.ValueKind == JsonValueKind.String && classValue.GetString()?.Trim() is { Length: > 0 } classIndex
                     && entry.EnumerateObject().Count() == 1)
            {
                if (_classes.ContainsKey(classIndex))
                {
                    classes.Add(classIndex);
                }
                else
                {
                    AddError($"{entryPath}.class", $"La clase '{classIndex}' no existe en el catálogo.");
                }
            }
            else
            {
                AddError(entryPath, "Cada entrada es el índice de un conjuro o { \"class\": \"<clase>\" }.");
            }
        }

        if (list.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var entry in list.EnumerateArray())
            {
                Entry($"{path}[{i++}]", entry);
            }
        }
        else
        {
            Entry(path, list);
        }

        return new ClassSpellList(spells.Distinct(StringComparer.Ordinal).ToList(), classes.Distinct(StringComparer.Ordinal).ToList());
    }

    private void CheckClassSpellListReferences(ContentPackRows rows)
    {
        if (_classSpellListReferences.Count == 0)
        {
            return;
        }

        var spells = (_context.Spells ?? new Dictionary<string, string>()).Where(s => IsOtherSource(s.Value)).Select(s => s.Key)
            .Concat(rows.Spells.Select(s => s.Index))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var (path, index) in _classSpellListReferences.Where(r => !spells.Contains(r.Index)))
        {
            AddError(path, $"El conjuro '{index}' no existe en el SRD, en los paquetes requeridos ni en el paquete.");
        }
    }

    // ---- Feats -----------------------------------------------------------------------------------

    /// <summary><c>feats[]</c>: options of the <c>feats</c> set (the one Ability Score Improvements offer).</summary>
    private void Feats(List<PackFeatJson?>? feats, ContentPackRows rows)
    {
        ForEach("feats", feats, (path, feat) =>
            Option(path, new PackOptionJson
            {
                Index = feat.Index,
                Name = feat.Name,
                Description = feat.Description,
                PrerequisitesText = feat.PrerequisitesText,
                Prerequisites = feat.Prerequisites,
                Modifiers = feat.Modifiers,
                AbilityIncrease = feat.AbilityIncrease,
                Grants = feat.Grants,
                Resource = feat.Resource,
            }, OptionSets.Feats, rows, NullableText($"{path}.category", feat.Category, OptionDefinition.CategoryMaxLength)));
        rows.Feats = rows.Options.Count(o => o.SetId == OptionSets.Feats);
    }

    // ---- Creatures, conditions, rules and vocabularies --------------------------------------------

    private void Creature(string path, PackCreatureJson creature, ContentPackRows rows)
    {
        var index = Index($"{path}.index", "creatures", creature.Index);
        var name = RequiredText($"{path}.name", creature.Name, NameMaxLength);
        var size = RequiredText($"{path}.size", creature.Size, ShortTextMaxLength);
        var knownSize = Sizes.FirstOrDefault(s => string.Equals(s, size, StringComparison.OrdinalIgnoreCase));
        if (size.Length > 0 && knownSize is null)
        {
            AddError($"{path}.size", $"Tamaño desconocido. Valores admitidos: {string.Join(", ", Sizes)}.");
        }

        var type = RequiredText($"{path}.type", creature.Type, CreatureDefinition.TypeMaxLength).ToLowerInvariant();
        var cr = creature.ChallengeRating;
        if (cr is null)
        {
            AddError($"{path}.challengeRating", "Campo obligatorio.");
        }
        else if (cr is < 0 or > 30)
        {
            AddError($"{path}.challengeRating", "Debe estar entre 0 y 30 (0.125, 0.25 y 0.5 para 1/8, 1/4 y 1/2).");
        }

        var abilities = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var ability in Abilities.All)
        {
            abilities[ability] = 10;
        }

        foreach (var (key, value) in creature.Abilities ?? [])
        {
            var ability = key.Trim().ToLowerInvariant();
            if (!Abilities.IsValid(ability))
            {
                AddError($"{path}.abilities.{key}", "Característica desconocida (str, dex, con, int, wis o cha).");
            }
            else if (RequiredInt($"{path}.abilities.{key}", value, 1, 30) is { } score)
            {
                abilities[ability] = score;
            }
        }

        Dictionary<string, int> Bonuses(string field, Dictionary<string, int?>? values, Func<string, bool> valid, string error)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (key, value) in values ?? [])
            {
                var normalized = key.Trim().ToLowerInvariant();
                if (!valid(normalized))
                {
                    AddError($"{path}.{field}.{key}", error);
                }
                else if (RequiredInt($"{path}.{field}.{key}", value, -10, 30) is { } bonus)
                {
                    result[normalized] = bonus;
                }
            }

            return result;
        }

        var speeds = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, value) in creature.Speed ?? [])
        {
            if (RequiredInt($"{path}.speed.{key}", value, 0, 1000) is { } feet)
            {
                speeds[key.Trim().ToLowerInvariant()] = feet;
            }
        }

        var senses = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in creature.Senses ?? [])
        {
            if (NullableText($"{path}.senses.{key}", value, ShortTextMaxLength) is { } sense)
            {
                senses[key.Trim()] = sense;
            }
        }

        var hitPoints = RequiredInt($"{path}.hitPoints", creature.HitPoints, 1, 10_000) ?? 1;
        var challenge = cr is >= 0 and <= 30 ? cr.Value : 0;
        var beast = new BeastDto(
            index ?? string.Empty,
            name,
            knownSize ?? size,
            OptionalText($"{path}.alignment", creature.Alignment, ShortTextMaxLength),
            challenge,
            BeastDto.FormatChallengeRating(challenge),
            OptionalInt($"{path}.xp", creature.Xp, 0, 1_000_000) ?? 0,
            OptionalInt($"{path}.proficiencyBonus", creature.ProficiencyBonus, 2, 9) ?? Math.Max(2, 2 + ((int)Math.Max(1, challenge) - 1) / 4),
            RequiredInt($"{path}.armorClass", creature.ArmorClass, 1, 40) ?? 10,
            NullableText($"{path}.armorClassType", creature.ArmorClassType, ShortTextMaxLength),
            hitPoints,
            OptionalText($"{path}.hitDice", creature.HitDice, ShortTextMaxLength),
            NullableText($"{path}.hitPointsRoll", creature.HitPointsRoll, ShortTextMaxLength),
            speeds,
            abilities,
            Bonuses("savingThrows", creature.SavingThrows, Abilities.IsValid, "Característica desconocida (str, dex, con, int, wis o cha)."),
            Bonuses("skills", creature.Skills, _skills.ContainsKey, "Habilidad desconocida (usa índices como \"perception\")."),
            senses,
            OptionalInt($"{path}.passivePerception", creature.PassivePerception, 0, 40) ?? 10,
            OptionalText($"{path}.languages", creature.Languages, LongTextMaxLength),
            TextList($"{path}.damageVulnerabilities", creature.DamageVulnerabilities, 30, ShortTextMaxLength),
            TextList($"{path}.damageResistances", creature.DamageResistances, 30, ShortTextMaxLength),
            TextList($"{path}.damageImmunities", creature.DamageImmunities, 30, ShortTextMaxLength),
            TextList($"{path}.conditionImmunities", creature.ConditionImmunities, 30, ShortTextMaxLength),
            CreatureActions($"{path}.traits", creature.Traits).Select(a => new BeastTraitDto(a.Name, a.Description, a.Save)).ToList(),
            CreatureActions($"{path}.actions", creature.Actions),
            Paragraphs($"{path}.description", creature.Description) is { Count: > 0 } paragraphs ? string.Join("\n\n", paragraphs) : null)
        {
            Type = type,
            Subtype = NullableText($"{path}.subtype", creature.Subtype, NameMaxLength),
            Reactions = CreatureActions($"{path}.reactions", creature.Reactions),
            LegendaryActions = CreatureActions($"{path}.legendaryActions", creature.LegendaryActions),
            Source = _id,
        };

        if (index is not null && type.Length > 0)
        {
            rows.Creatures.Add(SrdBeastCatalog.ToRow(beast, _id));
        }
    }

    private List<BeastActionDto> CreatureActions(string path, List<PackCreatureActionJson?>? actions)
    {
        var result = new List<BeastActionDto>();
        ForEach(path, actions, (actionPath, action) =>
        {
            var damage = new List<BeastDamageDto>();
            ForEach($"{actionPath}.damage", action.Damage, (damagePath, entry) =>
                damage.Add(new BeastDamageDto(RequiredText($"{damagePath}.dice", entry.Dice, 40).Replace(" ", string.Empty, StringComparison.Ordinal), NullableText($"{damagePath}.type", entry.Type, 40))));
            BeastSaveDto? save = null;
            if (action.Save is { } saveJson)
            {
                var ability = saveJson.Ability?.Trim().ToLowerInvariant();
                if (ability is null || !Abilities.IsValid(ability))
                {
                    AddError($"{actionPath}.save.ability", "Característica desconocida (str, dex, con, int, wis o cha).");
                }

                if (RequiredInt($"{actionPath}.save.dc", saveJson.Dc, 1, 40) is { } dc && ability is not null && Abilities.IsValid(ability))
                {
                    save = new BeastSaveDto(dc, ability);
                }
            }

            result.Add(new BeastActionDto(
                RequiredText($"{actionPath}.name", action.Name, NameMaxLength),
                OptionalText($"{actionPath}.description", action.Description, ParagraphMaxLength),
                OptionalInt($"{actionPath}.attackBonus", action.AttackBonus, -10, 30),
                damage,
                save,
                action.Multiattack ?? false));
        });
        return result;
    }

    private void Condition(string path, PackConditionJson condition, ContentPackRows rows)
    {
        var index = Index($"{path}.index", "conditions", condition.Index);
        var name = RequiredText($"{path}.name", condition.Name, NameMaxLength);
        var description = Paragraphs($"{path}.description", condition.Description);
        if (index is not null)
        {
            rows.Conditions.Add(new ConditionDefinition { Index = index, Name = name, Description = description, Source = _id });
        }
    }

    private void Rule(string path, PackRuleJson rule, ContentPackRows rows)
    {
        var index = Index($"{path}.index", "rules", rule.Index);
        var title = RequiredText($"{path}.title", rule.Title, NameMaxLength);
        var category = NullableText($"{path}.category", rule.Category, RuleDefinition.CategoryMaxLength)?.ToLowerInvariant() ?? "general";
        var body = Paragraphs($"{path}.body", rule.Body);
        if (body.Count == 0)
        {
            AddError($"{path}.body", "Campo obligatorio: al menos un párrafo.");
        }

        var tags = TextList($"{path}.tags", rule.Tags, RuleDefinition.MaxTags, ShortTextMaxLength);
        if (index is not null)
        {
            rows.Rules.Add(new RuleDefinition { Index = index, Title = title, Category = category, Body = body, Tags = tags, Source = _id });
        }
    }

    private void Reference(PackReferenceJson? reference, ContentPackRows rows)
    {
        if (reference is null)
        {
            return;
        }

        foreach (var (kind, entries) in ReferenceLists(reference))
        {
            ForEach($"reference.{kind}", entries, (path, entry) =>
            {
                var index = Index($"{path}.index", $"reference.{kind}", entry.Index);
                var name = RequiredText($"{path}.name", entry.Name, NameMaxLength);
                var description = Paragraphs($"{path}.description", entry.Description);
                var items = new List<string>();
                if (kind == ReferenceEntry.EquipmentCategories)
                {
                    ForEachText($"{path}.items", entry.Items, (itemPath, item) =>
                    {
                        if (Reference(itemPath, item) is { } itemIndex && !items.Contains(itemIndex))
                        {
                            items.Add(itemIndex);
                            _itemReferences.Add((itemPath, itemIndex));
                        }
                    });
                }
                else if (entry.Items is not null)
                {
                    AddError($"{path}.items", "Solo las categorías de equipo (equipmentCategories) tienen objetos.");
                }

                if (index is not null && kind == ReferenceEntry.EquipmentCategories)
                {
                    rows.EquipmentCategories.Add(new EquipmentCategory { Index = index, Name = name, ItemIndexes = items, Source = _id });
                }

                if (index is not null)
                {
                    rows.ReferenceEntries.Add(new ReferenceEntry
                    {
                        Kind = kind,
                        Index = index,
                        Name = name,
                        DescriptionJson = JsonSerializer.Serialize(description),
                        Source = _id,
                    });
                }
            });
        }
    }

    private static IEnumerable<(string Kind, List<PackReferenceEntryJson?>? Entries)> ReferenceLists(PackReferenceJson reference) =>
    [
        (ReferenceEntry.Languages, reference.Languages),
        (ReferenceEntry.WeaponProperties, reference.WeaponProperties),
        (ReferenceEntry.EquipmentCategories, reference.EquipmentCategories),
        (ReferenceEntry.DamageTypes, reference.DamageTypes),
        (ReferenceEntry.MagicSchools, reference.MagicSchools),
        (ReferenceEntry.Tools, reference.Tools),
    ];
}
