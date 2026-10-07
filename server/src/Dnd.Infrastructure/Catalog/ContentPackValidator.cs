using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dnd.Application.Common;
using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using Dnd.Domain.Items;

namespace Dnd.Infrastructure.Catalog;

/// <summary>Catalog data a content pack can reference: SRD classes, SRD subclasses, skills and (format 2) option sets, options and spells.</summary>
/// <param name="Classes">Class index → subclass flavor (e.g. "Martial Archetype").</param>
/// <param name="SrdSubclasses">SRD subclass index → class index.</param>
/// <param name="Skills">Skill index ("perception") → name ("Perception").</param>
/// <param name="OptionSets">Option set id → source (the SRD or a pack).</param>
/// <param name="Options">Option index → (set id, source).</param>
/// <param name="Spells">Spell index → source.</param>
/// <param name="SrdItems">Indexes of the SRD item templates (starting equipment references).</param>
/// <param name="EquipmentCategories">Indexes of the equipment categories ("martial-weapons").</param>
/// <param name="PackSubclasses">Subclasses of the packs already imported: index → (class index, pack id).</param>
internal sealed record ContentPackContext(
    IReadOnlyDictionary<string, string> Classes,
    IReadOnlyDictionary<string, string> SrdSubclasses,
    IReadOnlyDictionary<string, string> Skills,
    IReadOnlyDictionary<string, string>? OptionSets = null,
    IReadOnlyDictionary<string, (string SetId, string Source)>? Options = null,
    IReadOnlyDictionary<string, string>? Spells = null,
    IReadOnlySet<string>? SrdItems = null,
    IReadOnlySet<string>? EquipmentCategories = null,
    IReadOnlyDictionary<string, (string ClassIndex, string Source)>? PackSubclasses = null);

/// <summary>Catalog rows of a valid content pack, every one with <c>Source</c> = the pack id.</summary>
internal sealed class ContentPackRows
{
    public List<SubclassDefinition> Subclasses { get; } = [];

    public List<SubclassLevel> SubclassLevels { get; } = [];

    public List<FeatureDefinition> Features { get; } = [];

    public List<RaceDefinition> Races { get; } = [];

    public List<SubraceDefinition> Subraces { get; } = [];

    public List<TraitDefinition> Traits { get; } = [];

    public List<SpellDefinition> Spells { get; } = [];

    public List<BackgroundDefinition> Backgrounds { get; } = [];

    public List<(string Index, ItemTemplateData Data)> Items { get; } = [];

    public List<OptionSetDefinition> OptionSets { get; } = [];

    public List<OptionDefinition> Options { get; } = [];

    public List<LevelChoiceRule> LevelChoiceRules { get; } = [];

    public List<TrinketEntry> Trinkets { get; } = [];

    public List<RollTable> RollTables { get; } = [];

    public Dictionary<string, int> Counts() => new()
    {
        ["optionSets"] = OptionSets.Count,
        ["options"] = Options.Count,
        ["levelChoices"] = LevelChoiceRules.Count,
        ["subclasses"] = Subclasses.Count,
        ["features"] = Features.Count,
        ["items"] = Items.Count,
        ["spells"] = Spells.Count,
        ["races"] = Races.Count,
        ["subraces"] = Subraces.Count,
        ["traits"] = Traits.Count,
        ["backgrounds"] = Backgrounds.Count,
        ["trinkets"] = Trinkets.Count,
        ["rollTables"] = RollTables.Count,
    };
}

/// <summary>
/// Validates a deserialized content pack and maps it to catalog rows. Every problem is collected as
/// "path: message" (Spanish), with the path in the JSON (<c>items[3].modifiers[0].kind</c>). The
/// indexes are recorded with their path (<see cref="IndexPaths"/>) so that the importer can report
/// collisions with other sources.
/// </summary>
internal sealed partial class ContentPackValidator
{
    public const int CurrentFormatVersion = 2;

    /// <summary>First format with option sets, level choices and grants.</summary>
    public const int LevelChoicesFormatVersion = 2;
    public const int MaxListEntries = 500;
    public const int MaxParagraphs = 200;
    public const int ParagraphMaxLength = 10_000;
    public const int NameMaxLength = 200;
    public const int IndexMaxLength = 100;
    public const int VersionMaxLength = 40;
    public const int ShortTextMaxLength = 100;
    public const int LongTextMaxLength = 10_000;

    public static readonly IReadOnlyList<string> Schools =
        ["abjuration", "conjuration", "divination", "enchantment", "evocation", "illusion", "necromancy", "transmutation"];

    public static readonly IReadOnlyList<string> Sizes = ["Tiny", "Small", "Medium", "Large", "Huge", "Gargantuan"];

    private static readonly JsonSerializerOptions CamelCase = new(JsonSerializerDefaults.Web);

    private readonly ContentPackContext _context;
    private readonly List<string> _errors = [];
    private readonly Dictionary<string, HashSet<string>> _seen = new(StringComparer.Ordinal);
    private string _id = string.Empty;
    private int _formatVersion = 1;

    public ContentPackValidator(ContentPackContext context) => _context = context;

    public IReadOnlyList<string> Errors => _errors;

    /// <summary>Path of every index of the pack by table ("subclasses", "features", "items"...).</summary>
    public Dictionary<string, Dictionary<string, string>> IndexPaths { get; } = new(StringComparer.Ordinal);

    public string Id => _id;

    public string Name { get; private set; } = string.Empty;

    public string Version { get; private set; } = string.Empty;

    [GeneratedRegex("^[a-z0-9-]{3,40}$")]
    private static partial Regex PackIdPattern();

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex IndexPattern();

    public void AddError(string path, string message) => _errors.Add($"{path}: {message}");

    public ContentPackRows Validate(PackJson pack)
    {
        var rows = new ContentPackRows();

        if (pack.FormatVersion is { } format && format is < 1 or > CurrentFormatVersion)
        {
            AddError("formatVersion", $"Versión de formato no admitida; se admiten 1 y {CurrentFormatVersion}.");
        }

        _formatVersion = pack.FormatVersion ?? 1;

        var id = pack.Id?.Trim();
        if (string.IsNullOrEmpty(id))
        {
            AddError("id", "Campo obligatorio.");
        }
        else if (!PackIdPattern().IsMatch(id))
        {
            AddError("id", "Debe tener entre 3 y 40 caracteres: minúsculas, números y guiones.");
        }
        else if (CatalogSources.IsReserved(id))
        {
            AddError("id", $"\"{id}\" está reservado; elige otro identificador.");
        }
        else
        {
            _id = id;
        }

        Name = RequiredText("name", pack.Name, NameMaxLength);
        Version = RequiredText("version", pack.Version, VersionMaxLength);

        // Without a valid id the index prefixes cannot be checked: stop here.
        if (_id.Length == 0)
        {
            return rows;
        }

        var packSubclasses = new Dictionary<string, string>(StringComparer.Ordinal);
        if (RequireLevelChoicesFormat("optionSets", pack.OptionSets))
        {
            ForEach("optionSets", pack.OptionSets, (path, set) => OptionSet(path, set, rows));
        }

        ForEach("classesExtended", pack.ClassesExtended, (path, extension) => ClassExtension(path, extension, rows, packSubclasses));
        ForEach("items", pack.Items, (path, item) => Item(path, item, rows));
        ForEach("spells", pack.Spells, (path, spell) => Spell(path, spell, rows, packSubclasses));
        ForEach("races", pack.Races, (path, race) => Race(path, race, rows));
        ForEach("backgrounds", pack.Backgrounds, (path, background) => Background(path, background, rows));
        Trinkets(pack.Trinkets, rows);
        RollTables(pack.RollTables, rows, packSubclasses);
        CheckLevelChoiceReferences(rows);
        CheckStartingEquipmentReferences(rows);
        return rows;
    }

    // ---- Definitions -----------------------------------------------------------------------------

    private void ClassExtension(string path, PackClassExtensionJson extension, ContentPackRows rows, Dictionary<string, string> packSubclasses)
    {
        var classIndex = extension.ClassIndex?.Trim();
        string? flavor = null;
        if (string.IsNullOrEmpty(classIndex))
        {
            AddError($"{path}.classIndex", "Campo obligatorio.");
        }
        else if (!_context.Classes.TryGetValue(classIndex, out flavor))
        {
            AddError($"{path}.classIndex", $"La clase '{classIndex}' no existe en el catálogo (los paquetes no añaden clases).");
        }

        if (RequireLevelChoicesFormat($"{path}.levelChoices", extension.LevelChoices) && classIndex is not null && flavor is not null)
        {
            ForEach($"{path}.levelChoices", extension.LevelChoices, (rulePath, rule) => LevelChoice(rulePath, rule, classIndex, null, rows));
        }

        ForEach($"{path}.subclasses", extension.Subclasses, (subclassPath, subclass) =>
        {
            var index = Index($"{subclassPath}.index", "subclasses", subclass.Index);
            var name = RequiredText($"{subclassPath}.name", subclass.Name, NameMaxLength);
            var subclassFlavor = OptionalText($"{subclassPath}.flavor", subclass.Flavor, NameMaxLength);
            var description = Paragraphs($"{subclassPath}.description", subclass.Description);
            if (index is null || classIndex is null || flavor is null)
            {
                return;
            }

            packSubclasses[index] = classIndex;
            if (RequireLevelChoicesFormat($"{subclassPath}.levelChoices", subclass.LevelChoices))
            {
                ForEach($"{subclassPath}.levelChoices", subclass.LevelChoices, (rulePath, rule) => LevelChoice(rulePath, rule, classIndex, index, rows));
            }

            rows.Subclasses.Add(new SubclassDefinition
            {
                Index = index,
                ClassIndex = classIndex,
                Name = name,
                Flavor = subclassFlavor.Length > 0 ? subclassFlavor : flavor,
                Description = description,
                Source = _id,
            });

            var levels = new HashSet<int>();
            ForEach($"{subclassPath}.levels", subclass.Levels, (levelPath, level) =>
            {
                var number = RequiredInt($"{levelPath}.level", level.Level, 1, 20);
                if (number is { } n && !levels.Add(n))
                {
                    AddError($"{levelPath}.level", "Nivel duplicado en la subclase.");
                }

                var featureIndexes = new List<string>();
                ForEach($"{levelPath}.features", level.Features, (featurePath, feature) =>
                {
                    var featureIndex = Index($"{featurePath}.index", "features", feature.Index);
                    var featureName = RequiredText($"{featurePath}.name", feature.Name, NameMaxLength);
                    var featureDescription = Paragraphs($"{featurePath}.description", feature.Description);
                    if (featureIndex is null || number is null)
                    {
                        return;
                    }

                    featureIndexes.Add(featureIndex);
                    rows.Features.Add(new FeatureDefinition
                    {
                        Index = featureIndex,
                        Name = featureName,
                        ClassIndex = classIndex,
                        SubclassIndex = index,
                        Level = number.Value,
                        Description = featureDescription,
                        Source = _id,
                    });
                });

                var grants = level.Grants is not null && RequireLevelChoicesFormat($"{levelPath}.grants", level.Grants)
                    ? Grants($"{levelPath}.grants", level.Grants)
                    : null;
                if (number is { } levelNumber)
                {
                    var levelIndex = $"{index}-{levelNumber}";
                    Record("subclassLevels", levelIndex, levelPath);
                    rows.SubclassLevels.Add(new SubclassLevel
                    {
                        Index = levelIndex,
                        SubclassIndex = index,
                        Level = levelNumber,
                        FeatureIndexes = featureIndexes,
                        GrantsJson = grants,
                        Source = _id,
                    });
                }
            });
        });
    }

    private void Item(string path, PackItemJson item, ContentPackRows rows)
    {
        var index = Index($"{path}.index", "items", item.Index);
        var name = RequiredText($"{path}.name", item.Name, NameMaxLength);

        ItemCategory category = default;
        if (string.IsNullOrWhiteSpace(item.Category))
        {
            AddError($"{path}.category", "Campo obligatorio.");
        }
        else if (!EnumNames.TryParse(item.Category.Trim(), out category))
        {
            AddError($"{path}.category", $"Categoría desconocida. Valores admitidos: {string.Join(", ", Enum.GetNames<ItemCategory>())}.");
        }

        ItemRarity? rarity = null;
        if (!string.IsNullOrWhiteSpace(item.Rarity))
        {
            if (EnumNames.TryParse<ItemRarity>(item.Rarity.Trim(), out var parsed))
            {
                rarity = parsed;
            }
            else
            {
                AddError($"{path}.rarity", $"Rareza desconocida. Valores admitidos: {string.Join(", ", Enum.GetNames<ItemRarity>())}.");
            }
        }

        var modifiers = new List<ItemModifier>();
        if (item.Modifiers is { Count: > ItemLimits.MaxModifiers })
        {
            AddError($"{path}.modifiers", $"Un objeto admite como máximo {ItemLimits.MaxModifiers} modificadores.");
        }
        else
        {
            ForEach($"{path}.modifiers", item.Modifiers, (modifierPath, modifier) =>
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

                modifiers.Add(candidate.Normalize());
            });
        }

        var data = new ItemTemplateData
        {
            Name = name,
            Category = category,
            Subcategory = OptionalText($"{path}.subcategory", item.Subcategory, ItemLimits.SubcategoryMaxLength),
            Rarity = rarity,
            RequiresAttunement = item.RequiresAttunement ?? false,
            CostCp = OptionalInt($"{path}.costCp", item.CostCp, 0, ItemLimits.MaxCostCp),
            WeightLb = OptionalDecimal($"{path}.weightLb", item.WeightLb, 0, ItemLimits.MaxWeightLb),
            DamageDice = NullableText($"{path}.damageDice", item.DamageDice, ItemLimits.DiceMaxLength),
            DamageType = NullableText($"{path}.damageType", item.DamageType, ItemLimits.DamageTypeMaxLength),
            VersatileDice = NullableText($"{path}.versatileDice", item.VersatileDice, ItemLimits.DiceMaxLength),
            Properties = TextList($"{path}.properties", item.Properties, ItemLimits.MaxListEntries, ItemLimits.PropertyMaxLength),
            RangeNormal = OptionalInt($"{path}.rangeNormal", item.RangeNormal, 0, ItemLimits.MaxRange),
            RangeLong = OptionalInt($"{path}.rangeLong", item.RangeLong, 0, ItemLimits.MaxRange),
            ArmorClassBase = OptionalInt($"{path}.armorClassBase", item.ArmorClassBase, 0, ItemLimits.MaxArmorClass),
            AddDexModifier = item.AddDexModifier,
            MaxDexBonus = OptionalInt($"{path}.maxDexBonus", item.MaxDexBonus, 0, ItemLimits.MaxDexBonus),
            StrengthMinimum = OptionalInt($"{path}.strengthMinimum", item.StrengthMinimum, 0, ItemLimits.MaxStrengthMinimum),
            StealthDisadvantage = item.StealthDisadvantage ?? false,
            Description = Paragraphs($"{path}.description", item.Description),
            Effects = TextList($"{path}.effects", item.Effects, ItemLimits.MaxListEntries, ItemLimits.EffectMaxLength),
            Modifiers = modifiers,
        };

        if (index is not null)
        {
            rows.Items.Add((index, data));
        }
    }

    private void Spell(string path, PackSpellJson spell, ContentPackRows rows, Dictionary<string, string> packSubclasses)
    {
        var index = Index($"{path}.index", "spells", spell.Index);
        var name = RequiredText($"{path}.name", spell.Name, NameMaxLength);
        var level = RequiredInt($"{path}.level", spell.Level, 0, 9);

        var school = RequiredText($"{path}.school", spell.School, ShortTextMaxLength).ToLowerInvariant();
        if (school.Length > 0 && !Schools.Contains(school))
        {
            AddError($"{path}.school", $"Escuela desconocida. Valores admitidos: {string.Join(", ", Schools)}.");
        }

        var components = new List<string>();
        ForEachText($"{path}.components", spell.Components, (componentPath, component) =>
        {
            var upper = component.ToUpperInvariant();
            if (upper is not ("V" or "S" or "M"))
            {
                AddError(componentPath, "Componente desconocido (V, S o M).");
            }
            else if (!components.Contains(upper))
            {
                components.Add(upper);
            }
        });

        var classes = new List<string>();
        ForEachText($"{path}.classes", spell.Classes, (classPath, classIndex) =>
        {
            if (!_context.Classes.ContainsKey(classIndex))
            {
                AddError(classPath, $"La clase '{classIndex}' no existe en el catálogo.");
            }
            else if (!classes.Contains(classIndex))
            {
                classes.Add(classIndex);
            }
        });

        var subclasses = new List<string>();
        ForEachText($"{path}.subclasses", spell.Subclasses, (subclassPath, subclassIndex) =>
        {
            if (!_context.SrdSubclasses.ContainsKey(subclassIndex) && !packSubclasses.ContainsKey(subclassIndex))
            {
                AddError(subclassPath, $"La subclase '{subclassIndex}' no existe en el SRD ni en el paquete.");
            }
            else if (!subclasses.Contains(subclassIndex))
            {
                subclasses.Add(subclassIndex);
            }
        });

        var attackType = NullableText($"{path}.attackType", spell.AttackType, 16)?.ToLowerInvariant();
        if (attackType is not (null or "melee" or "ranged"))
        {
            AddError($"{path}.attackType", "Debe ser \"melee\", \"ranged\" o null.");
        }

        var dcAbility = NullableText($"{path}.dcAbility", spell.DcAbility, 8)?.ToLowerInvariant();
        if (dcAbility is not null && !Abilities.IsValid(dcAbility))
        {
            AddError($"{path}.dcAbility", "Característica desconocida (str, dex, con, int, wis o cha).");
        }

        var damageJson = SpellDamage($"{path}.damage", spell.Damage);
        var category = SpellCategories.Derive(heals: false, dealsDamage: damageJson is not null, hasSavingThrow: dcAbility is not null);
        if (spell.Category is not null)
        {
            if (SpellCategories.TryParse(spell.Category) is { } parsed)
            {
                category = parsed;
            }
            else
            {
                AddError($"{path}.category", $"Categoría desconocida. Valores: {string.Join(", ", Enum.GetNames<SpellCategory>())}.");
            }
        }

        var definition = new SpellDefinition
        {
            Index = index ?? string.Empty,
            Name = name,
            Level = level ?? 0,
            School = school.Length == 0 ? string.Empty : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(school),
            CastingTime = RequiredText($"{path}.castingTime", spell.CastingTime, ShortTextMaxLength),
            Range = RequiredText($"{path}.range", spell.Range, ShortTextMaxLength),
            Components = components,
            Material = NullableText($"{path}.material", spell.Material, LongTextMaxLength),
            Duration = RequiredText($"{path}.duration", spell.Duration, ShortTextMaxLength),
            Concentration = spell.Concentration ?? false,
            Ritual = spell.Ritual ?? false,
            Description = Paragraphs($"{path}.description", spell.Description),
            HigherLevel = Paragraphs($"{path}.higherLevel", spell.HigherLevel),
            ClassIndexes = classes,
            SubclassIndexes = subclasses,
            AttackType = attackType,
            DamageJson = damageJson,
            DcAbility = dcAbility,
            Category = category,
            Source = _id,
        };

        if (index is not null)
        {
            rows.Spells.Add(definition);
        }
    }

    private string? SpellDamage(string path, PackSpellDamageJson? damage)
    {
        if (damage is null)
        {
            return null;
        }

        var type = NullableText($"{path}.type", damage.Type, 32);
        var atSlotLevel = LevelMap($"{path}.atSlotLevel", damage.AtSlotLevel, 1, 9);
        var atCharacterLevel = LevelMap($"{path}.atCharacterLevel", damage.AtCharacterLevel, 1, 20);
        if (atSlotLevel is null && atCharacterLevel is null)
        {
            AddError(path, "Indica atSlotLevel o atCharacterLevel (o usa null si el conjuro no hace daño).");
            return null;
        }

        return JsonSerializer.Serialize(new[] { new { type, atSlotLevel, atCharacterLevel } }, CamelCase);
    }

    private SortedDictionary<int, string>? LevelMap(string path, Dictionary<string, string?>? source, int min, int max)
    {
        if (source is null || source.Count == 0)
        {
            return null;
        }

        var map = new SortedDictionary<int, string>();
        foreach (var (key, value) in source)
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || level < min || level > max)
            {
                AddError($"{path}.{key}", $"Nivel no válido; las claves van de {min} a {max}.");
                continue;
            }

            var dice = RequiredText($"{path}.{key}", value, ItemLimits.DiceMaxLength);
            if (dice.Length > 0)
            {
                map[level] = dice;
            }
        }

        return map.Count == 0 ? null : map;
    }

    private void Race(string path, PackRaceJson race, ContentPackRows rows)
    {
        var index = Index($"{path}.index", "races", race.Index);
        var name = RequiredText($"{path}.name", race.Name, NameMaxLength);
        var speed = RequiredInt($"{path}.speed", race.Speed, 0, 200);

        var size = RequiredText($"{path}.size", race.Size, ShortTextMaxLength);
        if (size.Length > 0)
        {
            var known = Sizes.FirstOrDefault(s => string.Equals(s, size, StringComparison.OrdinalIgnoreCase));
            if (known is null)
            {
                AddError($"{path}.size", $"Tamaño desconocido. Valores admitidos: {string.Join(", ", Sizes)}.");
            }
            else
            {
                size = known;
            }
        }

        var traitIndexes = Traits($"{path}.traits", race.Traits, rows, index, subraceIndex: null);
        var subraceIndexes = new List<string>();
        ForEach($"{path}.subraces", race.Subraces, (subracePath, subrace) =>
        {
            var subraceIndex = Index($"{subracePath}.index", "subraces", subrace.Index);
            var subraceName = RequiredText($"{subracePath}.name", subrace.Name, NameMaxLength);
            var description = OptionalText($"{subracePath}.description", subrace.Description, LongTextMaxLength);
            var bonuses = AbilityBonuses($"{subracePath}.abilityBonuses", subrace.AbilityBonuses);
            var subraceTraits = Traits($"{subracePath}.traits", subrace.Traits, rows, raceIndex: null, subraceIndex);
            if (subraceIndex is null || index is null)
            {
                return;
            }

            subraceIndexes.Add(subraceIndex);
            rows.Subraces.Add(new SubraceDefinition
            {
                Index = subraceIndex,
                RaceIndex = index,
                Name = subraceName,
                Description = description,
                AbilityBonusesJson = bonuses,
                TraitIndexes = subraceTraits,
                ChoicesJson = OriginChoices($"{subracePath}.choices", subrace.Choices),
                Resistances = DamageTypes($"{subracePath}.resistances", subrace.Resistances),
                Source = _id,
            });
        });

        var definition = new RaceDefinition
        {
            Index = index ?? string.Empty,
            Name = name,
            Speed = speed ?? 0,
            Size = size,
            AbilityBonusesJson = AbilityBonuses($"{path}.abilityBonuses", race.AbilityBonuses),
            TraitIndexes = traitIndexes,
            Languages = TextList($"{path}.languages", race.Languages, 50, ShortTextMaxLength),
            Age = OptionalText($"{path}.age", race.Age, LongTextMaxLength),
            Alignment = OptionalText($"{path}.alignment", race.Alignment, LongTextMaxLength),
            SizeDescription = OptionalText($"{path}.sizeDescription", race.SizeDescription, LongTextMaxLength),
            SubraceIndexes = subraceIndexes,
            ChoicesJson = OriginChoices($"{path}.choices", race.Choices),
            Resistances = DamageTypes($"{path}.resistances", race.Resistances),
            Source = _id,
        };

        if (index is not null)
        {
            rows.Races.Add(definition);
        }
    }

    private List<string> Traits(string path, List<PackTraitJson?>? traits, ContentPackRows rows, string? raceIndex, string? subraceIndex)
    {
        var indexes = new List<string>();
        ForEach(path, traits, (traitPath, trait) =>
        {
            var index = Index($"{traitPath}.index", "traits", trait.Index);
            var name = RequiredText($"{traitPath}.name", trait.Name, NameMaxLength);
            var description = Paragraphs($"{traitPath}.description", trait.Description);
            if (index is null)
            {
                return;
            }

            indexes.Add(index);
            rows.Traits.Add(new TraitDefinition
            {
                Index = index,
                Name = name,
                Description = description,
                RaceIndexes = raceIndex is null ? [] : [raceIndex],
                SubraceIndexes = subraceIndex is null ? [] : [subraceIndex],
                Source = _id,
            });
        });
        return indexes;
    }

    private string AbilityBonuses(string path, List<PackAbilityBonusJson?>? bonuses)
    {
        var result = new List<AbilityBonus>();
        ForEach(path, bonuses, (bonusPath, bonus) =>
        {
            var ability = bonus.Ability?.Trim().ToLowerInvariant();
            var value = RequiredInt($"{bonusPath}.bonus", bonus.Bonus, -10, 10);
            if (string.IsNullOrEmpty(ability))
            {
                AddError($"{bonusPath}.ability", "Campo obligatorio.");
            }
            else if (!Abilities.IsValid(ability))
            {
                AddError($"{bonusPath}.ability", "Característica desconocida (str, dex, con, int, wis o cha).");
            }
            else if (value is { } v)
            {
                result.Add(new AbilityBonus(ability, v));
            }
        });
        return JsonSerializer.Serialize(result, CamelCase);
    }

    private void Background(string path, PackBackgroundJson background, ContentPackRows rows)
    {
        var index = Index($"{path}.index", "backgrounds", background.Index);
        var skills = new List<string>();
        ForEachText($"{path}.skillProficiencies", background.SkillProficiencies, (skillPath, skill) =>
        {
            var key = skill.ToLowerInvariant();
            if (_context.Skills.TryGetValue(key, out var skillName))
            {
                skills.Add(skillName);
            }
            else
            {
                AddError(skillPath, $"La habilidad '{skill}' no existe (usa índices como \"perception\" o \"animal-handling\").");
            }
        });

        var definition = new BackgroundDefinition
        {
            Index = index ?? string.Empty,
            Name = RequiredText($"{path}.name", background.Name, NameMaxLength),
            FeatureName = OptionalText($"{path}.featureName", background.FeatureName, NameMaxLength),
            FeatureDescription = Paragraphs($"{path}.featureDescription", background.FeatureDescription),
            SkillProficiencies = skills,
            StartingEquipmentText = OptionalText($"{path}.startingEquipmentText", background.StartingEquipmentText, LongTextMaxLength),
            StartingEquipmentJson = background.StartingEquipment is { } equipment ? ParseStartingEquipment($"{path}.startingEquipment", equipment) : null,
            ChoicesJson = OriginChoices($"{path}.choices", background.Choices),
            PersonalityJson = background.Personality is { } personality ? Personality($"{path}.personality", personality)?.ToJson() : null,
            OptionalTablesJson = BackgroundTables($"{path}.optionalTables", background.OptionalTables) is { Count: > 0 } tables
                ? BackgroundTable.ToJson(tables)
                : null,
            Source = _id,
        };

        if (index is not null)
        {
            rows.Backgrounds.Add(definition);
        }
    }

    // ---- Field helpers ---------------------------------------------------------------------------

    /// <summary>Calls <paramref name="action"/> for every non-null entry of a list of at most <see cref="MaxListEntries"/>.</summary>
    private void ForEach<T>(string path, List<T?>? list, Action<string, T> action)
        where T : class
    {
        if (list is null)
        {
            return;
        }

        if (list.Count > MaxListEntries)
        {
            AddError(path, $"No puede tener más de {MaxListEntries} entradas.");
            return;
        }

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is { } entry)
            {
                action($"{path}[{i}]", entry);
            }
            else
            {
                AddError($"{path}[{i}]", "La entrada no puede ser null.");
            }
        }
    }

    /// <summary>Calls <paramref name="action"/> with every trimmed, non-blank text of a list of references.</summary>
    private void ForEachText(string path, List<string?>? list, Action<string, string> action)
    {
        if (list is null)
        {
            return;
        }

        if (list.Count > MaxListEntries)
        {
            AddError(path, $"No puede tener más de {MaxListEntries} entradas.");
            return;
        }

        for (var i = 0; i < list.Count; i++)
        {
            var value = list[i]?.Trim();
            if (string.IsNullOrEmpty(value))
            {
                AddError($"{path}[{i}]", "El valor no puede estar vacío.");
            }
            else
            {
                action($"{path}[{i}]", value);
            }
        }
    }

    /// <summary>Required index with the pack prefix, unique in its table within the pack. Null when invalid.</summary>
    private string? Index(string path, string table, string? value)
    {
        var index = value?.Trim();
        if (string.IsNullOrEmpty(index))
        {
            AddError(path, "Campo obligatorio.");
            return null;
        }

        if (index.Length > IndexMaxLength)
        {
            AddError(path, $"No puede superar los {IndexMaxLength} caracteres.");
            return null;
        }

        if (!IndexPattern().IsMatch(index))
        {
            AddError(path, "Solo puede contener minúsculas, números y guiones.");
            return null;
        }

        if (!index.StartsWith($"{_id}-", StringComparison.Ordinal) || index.Length == _id.Length + 1)
        {
            AddError(path, $"Debe empezar por \"{_id}-\" (el id del paquete).");
            return null;
        }

        return Record(table, index, path) ? index : null;
    }

    private bool Record(string table, string index, string path)
    {
        if (!_seen.TryGetValue(table, out var seen))
        {
            seen = new HashSet<string>(StringComparer.Ordinal);
            _seen[table] = seen;
            IndexPaths[table] = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        if (!seen.Add(index))
        {
            AddError(path, $"Índice '{index}' duplicado en el paquete.");
            return false;
        }

        IndexPaths[table][index] = path;
        return true;
    }

    private string RequiredText(string path, string? value, int maxLength)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            AddError(path, "Campo obligatorio.");
        }
        else if (text.Length > maxLength)
        {
            AddError(path, $"No puede superar los {maxLength} caracteres.");
        }

        return text;
    }

    private string OptionalText(string path, string? value, int maxLength) =>
        NullableText(path, value, maxLength) ?? string.Empty;

    private string? NullableText(string path, string? value, int maxLength)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (text.Length > maxLength)
        {
            AddError(path, $"No puede superar los {maxLength} caracteres.");
        }

        return text;
    }

    private List<string> Paragraphs(string path, List<string?>? value) =>
        TextList(path, value, MaxParagraphs, ParagraphMaxLength);

    private List<string> TextList(string path, List<string?>? value, int maxEntries, int maxLength)
    {
        if (value is null)
        {
            return [];
        }

        if (value.Count > maxEntries)
        {
            AddError(path, $"No puede tener más de {maxEntries} entradas.");
            return [];
        }

        var result = new List<string>();
        for (var i = 0; i < value.Count; i++)
        {
            var text = value[i]?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            if (text.Length > maxLength)
            {
                AddError($"{path}[{i}]", $"No puede superar los {maxLength} caracteres.");
            }

            result.Add(text);
        }

        return result;
    }

    private int? RequiredInt(string path, int? value, int min, int max)
    {
        if (value is null)
        {
            AddError(path, "Campo obligatorio.");
            return null;
        }

        return OptionalInt(path, value, min, max);
    }

    private int? OptionalInt(string path, int? value, int min, int max)
    {
        if (value is { } v && (v < min || v > max))
        {
            AddError(path, $"Debe estar entre {min} y {max}.");
            return null;
        }

        return value;
    }

    private decimal? OptionalDecimal(string path, decimal? value, decimal min, decimal max)
    {
        if (value is { } v && (v < min || v > max))
        {
            AddError(path, $"Debe estar entre {min.ToString(CultureInfo.InvariantCulture)} y {max.ToString(CultureInfo.InvariantCulture)}.");
            return null;
        }

        return value;
    }
}
