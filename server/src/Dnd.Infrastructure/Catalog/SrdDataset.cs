using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dnd.Domain.Catalog;

namespace Dnd.Infrastructure.Catalog;

/// <summary>Every catalog row produced from the SRD dataset, ready to be inserted.</summary>
internal sealed record SrdCatalog(
    IReadOnlyList<ClassDefinition> Classes,
    IReadOnlyList<ClassLevel> ClassLevels,
    IReadOnlyList<SubclassDefinition> Subclasses,
    IReadOnlyList<SubclassLevel> SubclassLevels,
    IReadOnlyList<FeatureDefinition> Features,
    IReadOnlyList<RaceDefinition> Races,
    IReadOnlyList<SubraceDefinition> Subraces,
    IReadOnlyList<TraitDefinition> Traits,
    IReadOnlyList<SpellDefinition> Spells,
    IReadOnlyList<(string Index, ItemTemplateData Data)> Items,
    IReadOnlyList<ConditionDefinition> Conditions,
    IReadOnlyList<SkillDefinition> Skills,
    IReadOnlyList<BackgroundDefinition> Backgrounds,
    IReadOnlyList<EquipmentCategory> EquipmentCategories);

/// <summary>
/// Reads the SRD 5.1 JSON files of the 5e-database project (embedded in this assembly from
/// <c>server/seed/srd</c>) and maps them to catalog entities. The read models are private and
/// tolerant: missing fields become null/empty instead of failing.
/// </summary>
internal static class SrdDataset
{
    /// <summary>Commit and date of the 5e-database snapshot in <c>server/seed/srd</c>.</summary>
    public const string Version = "5e-database@a6212beb (2026-10-02); 2026-10-06: consumables, modifiers, skill choices, level choices, starting equipment";

    private const string ResourcePrefix = "5e-SRD-";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new StringListConverter() },
    };

    private static readonly JsonSerializerOptions CamelCase = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Multiclass spellcaster contribution per class (the dataset has no such field). The SRD has no
    /// third-caster subclasses (Eldritch Knight, Arcane Trickster), so those classes are 0.
    /// </summary>
    private static readonly Dictionary<string, int> SpellcastingLevels = new(StringComparer.Ordinal)
    {
        ["bard"] = 1,
        ["cleric"] = 1,
        ["druid"] = 1,
        ["sorcerer"] = 1,
        ["wizard"] = 1,
        ["warlock"] = 1,
        ["paladin"] = 2,
        ["ranger"] = 2,
    };

    private static readonly HashSet<string> PactCasters = new(StringComparer.Ordinal) { "warlock" };

    /// <summary>Alternative starting wealth per class (PHB/SRD "Starting Wealth by Class"; the dataset has no such field).</summary>
    private static readonly Dictionary<string, StartingGold> StartingWealth = new(StringComparer.Ordinal)
    {
        ["barbarian"] = new("2d4", 10),
        ["bard"] = new("5d4", 10),
        ["cleric"] = new("5d4", 10),
        ["druid"] = new("2d4", 10),
        ["fighter"] = new("5d4", 10),
        ["monk"] = new("5d4", 1),
        ["paladin"] = new("5d4", 10),
        ["ranger"] = new("5d4", 10),
        ["rogue"] = new("4d4", 10),
        ["sorcerer"] = new("3d4", 10),
        ["warlock"] = new("4d4", 10),
        ["wizard"] = new("4d4", 10),
    };

    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<StartingItem>>> PackContentsCache =
        new(() => PackContentsOf(Read<EquipmentJson>("Equipment")));

    /// <summary>Contents of the SRD equipment packs by item index ("explorers-pack" → backpack, bedroll...).</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<StartingItem>> PackContents => PackContentsCache.Value;

    public static SrdCatalog Load()
    {
        var subclassFiles = Read<SubclassJson>("Subclasses");
        var levelFiles = Read<LevelJson>("Levels");
        var equipment = Read<EquipmentJson>("Equipment");
        var contents = PackContentsOf(equipment);

        return new SrdCatalog(
            Read<ClassJson>("Classes").Select(c => MapClass(c, subclassFiles, contents)).ToList(),
            levelFiles.Where(l => l.Subclass is null).Select(MapClassLevel).ToList(),
            subclassFiles.Select(MapSubclass).ToList(),
            levelFiles.Where(l => l.Subclass is not null).Select(MapSubclassLevel).ToList(),
            Read<FeatureJson>("Features").Select(MapFeature).ToList(),
            Read<RaceJson>("Races").Select(MapRace).ToList(),
            Read<SubraceJson>("Subraces").Select(MapSubrace).ToList(),
            Read<TraitJson>("Traits").Select(MapTrait).ToList(),
            Read<SpellJson>("Spells").Select(MapSpell).ToList(),
            [
                .. equipment.Select(e => (e.Index, MapEquipment(e))),
                .. Read<MagicItemJson>("Magic-Items").Select(m => (m.Index, MapMagicItem(m))),
            ],
            Read<ConditionJson>("Conditions").Select(MapCondition).ToList(),
            Read<SkillJson>("Skills").Select(MapSkill).ToList(),
            Read<BackgroundJson>("Backgrounds").Select(b => MapBackground(b, contents)).ToList(),
            Read<EquipmentCategoryJson>("Equipment-Categories").Select(MapEquipmentCategory).ToList());
    }

    /// <summary>Converts a dataset cost to copper pieces (cp=1, sp=10, ep=50, gp=100, pp=1000).</summary>
    public static int? ToCopper(int? quantity, string? unit)
    {
        if (quantity is null)
        {
            return null;
        }

        int? factor = unit?.Trim().ToLowerInvariant() switch
        {
            "cp" => 1,
            "sp" => 10,
            "ep" => 50,
            "gp" => 100,
            "pp" => 1000,
            _ => null,
        };
        return factor is null ? null : quantity.Value * factor.Value;
    }

    private static List<T> Read<T>(string name)
    {
        var assembly = typeof(SrdDataset).Assembly;
        var suffix = $".{ResourcePrefix}{name}.json";
        var resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"SRD dataset file '{ResourcePrefix}{name}.json' is not embedded in {assembly.GetName().Name}.");

        using var stream = assembly.GetManifestResourceStream(resource)!;
        return JsonSerializer.Deserialize<List<T>>(stream, JsonOptions) ?? [];
    }

    // ---- Mapping -------------------------------------------------------------------------------

    private static ClassDefinition MapClass(ClassJson c, IReadOnlyList<SubclassJson> subclasses, IReadOnlyDictionary<string, IReadOnlyList<StartingItem>> contents)
    {
        var ability = c.Spellcasting?.SpellcastingAbility?.Index;
        return new ClassDefinition
        {
            Index = c.Index,
            Name = c.Name ?? c.Index,
            HitDie = c.HitDie ?? 0,
            SavingThrows = Indexes(c.SavingThrows),
            ProficiencyNames = (c.Proficiencies ?? [])
                .Where(p => p.Index is null || !p.Index.StartsWith("saving-throw-", StringComparison.Ordinal))
                .Select(p => p.Name ?? p.Index ?? string.Empty)
                .Where(n => n.Length > 0)
                .ToList(),
            SpellcastingAbility = ability,
            IsSpellcaster = ability is not null,
            SpellcastingLevel = ability is null ? 0 : SpellcastingLevels.GetValueOrDefault(c.Index),
            IsPactCaster = PactCasters.Contains(c.Index),
            SubclassFlavor = subclasses.FirstOrDefault(s => s.Class?.Index == c.Index)?.SubclassFlavor ?? string.Empty,
            StartingEquipmentText = StartingEquipment(c.StartingEquipment, c.StartingEquipmentOptions, null),
            StartingEquipmentJson = StructuredStartingEquipment(
                c.StartingEquipment, c.StartingEquipmentOptions, StartingWealth.GetValueOrDefault(c.Index), null, contents).ToJson(),
            SkillChoicesJson = SkillChoices(c.ProficiencyChoices),
        };
    }

    /// <summary>Picks the proficiency choice whose options are all skills and serializes it as <c>{"choose":N,"from":[...]}</c>.</summary>
    private static string SkillChoices(List<ProficiencyChoiceJson>? choices)
    {
        const string prefix = "skill-";
        foreach (var choice in choices ?? [])
        {
            var indexes = (choice.From?.Options ?? [])
                .Select(o => o.Item?.Index)
                .ToList();
            if (indexes.Count == 0 || indexes.Any(i => i is null || !i.StartsWith(prefix, StringComparison.Ordinal)))
            {
                continue;
            }

            return JsonSerializer.Serialize(new
            {
                choose = choice.Choose ?? 0,
                from = indexes.Select(i => i![prefix.Length..]).ToList(),
            });
        }

        return "{\"choose\":0,\"from\":[]}";
    }

    private static ClassLevel MapClassLevel(LevelJson l)
    {
        var slots = new int[ClassLevel.SpellSlotLevels];
        if (l.Spellcasting is { } spellcasting)
        {
            for (var level = 1; level <= ClassLevel.SpellSlotLevels; level++)
            {
                if (spellcasting.TryGetProperty($"spell_slots_level_{level}", out var value) && value.ValueKind == JsonValueKind.Number)
                {
                    slots[level - 1] = value.GetInt32();
                }
            }
        }

        return new ClassLevel
        {
            Index = l.Index,
            ClassIndex = l.Class?.Index ?? string.Empty,
            Level = l.Level ?? 0,
            ProfBonus = l.ProfBonus ?? 0,
            AbilityScoreBonuses = l.AbilityScoreBonuses ?? 0,
            FeatureIndexes = Indexes(l.Features),
            ClassSpecificJson = l.ClassSpecific is { ValueKind: JsonValueKind.Object } specific ? specific.GetRawText() : "{}",
            CantripsKnown = OptionalInt(l.Spellcasting, "cantrips_known"),
            SpellsKnown = OptionalInt(l.Spellcasting, "spells_known"),
            SpellSlots = slots,
        };
    }

    private static SubclassDefinition MapSubclass(SubclassJson s) => new()
    {
        Index = s.Index,
        ClassIndex = s.Class?.Index ?? string.Empty,
        Name = s.Name ?? s.Index,
        Flavor = s.SubclassFlavor ?? string.Empty,
        Description = s.Desc ?? [],
    };

    private static SubclassLevel MapSubclassLevel(LevelJson l) => new()
    {
        Index = l.Index,
        SubclassIndex = l.Subclass!.Index!,
        Level = l.Level ?? 0,
        FeatureIndexes = Indexes(l.Features),
    };

    private static FeatureDefinition MapFeature(FeatureJson f) => new()
    {
        Index = f.Index,
        Name = f.Name ?? f.Index,
        ClassIndex = f.Class?.Index ?? string.Empty,
        SubclassIndex = f.Subclass?.Index,
        Level = f.Level ?? 0,
        Description = f.Desc ?? [],
    };

    private static RaceDefinition MapRace(RaceJson r) => new()
    {
        Index = r.Index,
        Name = r.Name ?? r.Index,
        Speed = r.Speed ?? 0,
        Size = r.Size ?? string.Empty,
        AbilityBonusesJson = AbilityBonusesJson(r.AbilityBonuses),
        TraitIndexes = Indexes(r.Traits),
        Languages = (r.Languages ?? []).Select(x => x.Name ?? x.Index ?? string.Empty).Where(n => n.Length > 0).ToList(),
        Age = r.Age ?? string.Empty,
        Alignment = r.Alignment ?? string.Empty,
        SizeDescription = r.SizeDescription ?? string.Empty,
        SubraceIndexes = Indexes(r.Subraces),
    };

    private static SubraceDefinition MapSubrace(SubraceJson s) => new()
    {
        Index = s.Index,
        RaceIndex = s.Race?.Index ?? string.Empty,
        Name = s.Name ?? s.Index,
        Description = string.Join("\n", s.Desc ?? []),
        AbilityBonusesJson = AbilityBonusesJson(s.AbilityBonuses),
        TraitIndexes = Indexes(s.RacialTraits),
    };

    private static TraitDefinition MapTrait(TraitJson t) => new()
    {
        Index = t.Index,
        Name = t.Name ?? t.Index,
        Description = t.Desc ?? [],
        RaceIndexes = Indexes(t.Races),
        SubraceIndexes = Indexes(t.Subraces),
    };

    private static SpellDefinition MapSpell(SpellJson s) => new()
    {
        Index = s.Index,
        Name = s.Name ?? s.Index,
        Level = s.Level ?? 0,
        School = s.School?.Name ?? string.Empty,
        CastingTime = s.CastingTime ?? string.Empty,
        Range = s.Range ?? string.Empty,
        Components = s.Components ?? [],
        Material = string.IsNullOrWhiteSpace(s.Material) ? null : s.Material,
        Duration = s.Duration ?? string.Empty,
        Concentration = s.Concentration ?? false,
        Ritual = s.Ritual ?? false,
        Description = s.Desc ?? [],
        HigherLevel = s.HigherLevel ?? [],
        ClassIndexes = Indexes(s.Classes),
        SubclassIndexes = Indexes(s.Subclasses),
        AttackType = s.AttackType,
        DamageJson = SpellDamageJson(s.Damage),
        DcAbility = s.Dc?.DcType?.Index,
    };

    private static ItemTemplateData MapEquipment(EquipmentJson e)
    {
        var categoryIndex = e.EquipmentCategory?.Index;
        var category = categoryIndex switch
        {
            "weapon" => ItemCategory.Weapon,
            "armor" when string.Equals(e.ArmorCategory, "Shield", StringComparison.OrdinalIgnoreCase) => ItemCategory.Shield,
            "armor" => ItemCategory.Armor,
            "adventuring-gear" when IsAmmunition(e) => ItemCategory.Consumable,
            "adventuring-gear" => ItemCategory.AdventuringGear,
            "tools" => ItemCategory.Tool,
            "mounts-and-vehicles" => ItemCategory.Mount,
            _ => ItemCategory.Other,
        };

        var subcategory = category switch
        {
            ItemCategory.Weapon => e.CategoryRange ?? Join(e.WeaponCategory, e.WeaponRange),
            ItemCategory.Shield => "Shield",
            ItemCategory.Armor => e.ArmorCategory is null ? null : $"{e.ArmorCategory} Armor",
            ItemCategory.AdventuringGear or ItemCategory.Consumable => e.GearCategory?.Name,
            ItemCategory.Tool => e.ToolCategory,
            ItemCategory.Mount => e.VehicleCategory,
            _ => e.EquipmentCategory?.Name,
        };

        // Thrown weapons use their throw range; ranged weapons their range; melee reach is implicit.
        var range = e.ThrowRange ?? (string.Equals(e.WeaponRange, "Ranged", StringComparison.OrdinalIgnoreCase) ? e.Range : null);

        var description = new List<string>(e.Desc ?? []);
        description.AddRange(e.Special ?? []);
        if (e.Quantity is > 1)
        {
            description.Add($"Sold in bundles of {e.Quantity}.");
        }

        if (e.Contents is { Count: > 0 })
        {
            description.Add("Includes: " + string.Join(", ", e.Contents.Select(c => Counted(c.Item?.Name, c.Quantity))) + ".");
        }

        if (e.Speed?.Quantity is { } speed)
        {
            description.Add($"Speed: {speed.ToString(CultureInfo.InvariantCulture)} {e.Speed.Unit}.");
        }

        if (!string.IsNullOrWhiteSpace(e.Capacity))
        {
            description.Add($"Carrying capacity: {e.Capacity}.");
        }

        return new ItemTemplateData
        {
            Name = e.Name ?? e.Index,
            Category = category,
            Subcategory = subcategory ?? string.Empty,
            CostCp = ToCopper(e.Cost?.Quantity, e.Cost?.Unit),
            WeightLb = e.Weight,
            DamageDice = e.Damage?.DamageDice,
            DamageType = e.Damage?.DamageType?.Name,
            VersatileDice = e.TwoHandedDamage?.DamageDice,
            Properties = Indexes(e.Properties),
            RangeNormal = range?.Normal,
            RangeLong = range?.Long,
            ArmorClassBase = e.ArmorClass?.Base,
            AddDexModifier = e.ArmorClass?.DexBonus,
            MaxDexBonus = e.ArmorClass?.MaxBonus,
            StrengthMinimum = e.StrMinimum is > 0 ? e.StrMinimum : null,
            StealthDisadvantage = e.StealthDisadvantage ?? false,
            Description = description,
            Modifiers = SrdItemModifiers.For(e.Index),
        };
    }

    /// <summary>Plain arrows, bolts, bullets and needles: the adventuring gear of the "Ammunition" gear category.</summary>
    private static bool IsAmmunition(EquipmentJson e) =>
        string.Equals(e.GearCategory?.Index, "ammunition", StringComparison.OrdinalIgnoreCase);

    private static readonly string[] ConsumableNamePrefixes = ["Potion of", "Oil of", "Elixir of", "Philter of", "Spell Scroll", "Scroll of"];

    /// <summary>
    /// Magic items that are used up: potions (oils and philters are filed as potions in the SRD), scrolls and magic
    /// ammunition, by equipment category or, for items filed elsewhere, by name.
    /// </summary>
    private static bool IsMagicConsumable(MagicItemJson m) =>
        m.EquipmentCategory?.Index is "potion" or "scroll" or "ammunition"
        || (m.Name is { } name && ConsumableNamePrefixes.Any(p => name.StartsWith(p, StringComparison.OrdinalIgnoreCase)));

    private static ItemTemplateData MapMagicItem(MagicItemJson m)
    {
        var category = IsMagicConsumable(m)
            ? ItemCategory.Consumable
            : m.EquipmentCategory?.Index switch
            {
                "weapon" => ItemCategory.Weapon,
                "armor" => ItemCategory.Armor,
                _ => ItemCategory.MagicItem,
            };

        var description = m.Desc ?? [];
        return new ItemTemplateData
        {
            Name = m.Name ?? m.Index,
            Category = category,
            Subcategory = m.EquipmentCategory?.Name ?? string.Empty,
            Rarity = ParseRarity(m.Rarity?.Name),
            // The first line of the description reads like "Ring, rare (requires attunement)".
            RequiresAttunement = description.Count > 0 && description[0].Contains("requires attunement", StringComparison.OrdinalIgnoreCase),
            Description = description,
            Modifiers = SrdItemModifiers.For(m.Index),
        };
    }

    private static ConditionDefinition MapCondition(ConditionJson c) => new()
    {
        Index = c.Index,
        Name = c.Name ?? c.Index,
        Description = c.Desc ?? [],
    };

    private static SkillDefinition MapSkill(SkillJson s) => new()
    {
        Index = s.Index,
        Name = s.Name ?? s.Index,
        AbilityIndex = s.AbilityScore?.Index ?? string.Empty,
        Description = s.Desc ?? [],
    };

    private static BackgroundDefinition MapBackground(BackgroundJson b, IReadOnlyDictionary<string, IReadOnlyList<StartingItem>> contents) => new()
    {
        Index = b.Index,
        Name = b.Name ?? b.Index,
        FeatureName = b.Feature?.Name ?? string.Empty,
        FeatureDescription = b.Feature?.Desc ?? [],
        SkillProficiencies = (b.StartingProficiencies ?? [])
            .Where(p => p.Index?.StartsWith("skill-", StringComparison.Ordinal) == true)
            .Select(p => p.Name is { } name && name.StartsWith("Skill: ", StringComparison.Ordinal) ? name["Skill: ".Length..] : p.Name ?? p.Index!)
            .ToList(),
        StartingEquipmentText = StartingEquipment(b.StartingEquipment, b.StartingEquipmentOptions, b.StartingGold),
        StartingEquipmentJson = StructuredStartingEquipment(
            b.StartingEquipment, b.StartingEquipmentOptions, null, ToCopper(b.StartingGold?.Quantity, b.StartingGold?.Unit), contents).ToJson(),
    };

    private static EquipmentCategory MapEquipmentCategory(EquipmentCategoryJson c) => new()
    {
        Index = c.Index,
        Name = c.Name ?? c.Index,
        ItemIndexes = Indexes(c.Equipment).Distinct(StringComparer.Ordinal).ToList(),
    };

    // ---- Starting equipment ------------------------------------------------------------------------

    private static Dictionary<string, IReadOnlyList<StartingItem>> PackContentsOf(IEnumerable<EquipmentJson> equipment) =>
        equipment
            .Where(e => e.Contents is { Count: > 0 })
            .ToDictionary(
                e => e.Index,
                e => (IReadOnlyList<StartingItem>)e.Contents!
                    .Where(c => c.Item?.Index is not null)
                    .Select(c => new StartingItem(c.Item!.Index!, Math.Max(1, c.Quantity ?? 1), c.Item.Name))
                    .ToList(),
                StringComparer.Ordinal);

    /// <summary>
    /// Normalizes <c>starting_equipment</c> and <c>starting_equipment_options</c> (see <see cref="Domain.Catalog.StartingEquipment"/>):
    /// counted references become items, <c>multiple</c> options gather items and category picks, and choices
    /// from an equipment category become category picks.
    /// </summary>
    private static StartingEquipment StructuredStartingEquipment(
        List<StartingEquipmentJson>? fixedItems,
        List<EquipmentOptionJson>? options,
        StartingGold? gold,
        int? fixedGoldCp,
        IReadOnlyDictionary<string, IReadOnlyList<StartingItem>> contents)
    {
        StartingItem Item(ReferenceJson reference, int? quantity) =>
            new(reference.Index!, Math.Max(1, quantity ?? 1), reference.Name, contents.GetValueOrDefault(reference.Index!));

        var fixedList = (fixedItems ?? [])
            .Where(e => e.Equipment?.Index is not null)
            .Select(e => Item(e.Equipment!, e.Quantity))
            .ToList();

        var choices = new List<StartingEquipmentChoice>();
        foreach (var choice in options ?? [])
        {
            var mapped = new List<StartingEquipmentOption>();
            if (choice.From?.EquipmentCategory is { Index: not null } category)
            {
                // "holy symbol": one option, pick from the category.
                var label = Label(choice.Desc) ?? category.Name ?? category.Index;
                mapped.Add(new StartingEquipmentOption(label, [], [new StartingCategoryPick(category.Index, Math.Max(1, choice.Choose ?? 1))]));
                choices.Add(new StartingEquipmentChoice(choice.Desc ?? label, 1, mapped));
                continue;
            }

            foreach (var option in choice.From?.Options ?? [])
            {
                var items = new List<StartingItem>();
                var categories = new List<StartingCategoryPick>();
                var labels = new List<string>();
                Collect(option, items, categories, labels, Item);
                if (items.Count + categories.Count == 0)
                {
                    continue;
                }

                var optionLabel = string.Join(", ", labels);
                if (option.Prerequisites is { Count: > 0 })
                {
                    optionLabel += " (if proficient)";
                }

                mapped.Add(new StartingEquipmentOption(optionLabel, items, categories));
            }

            if (mapped.Count > 0)
            {
                choices.Add(new StartingEquipmentChoice(
                    choice.Desc ?? string.Join(" or ", mapped.Select(o => o.Label)),
                    Math.Clamp(choice.Choose ?? 1, 1, mapped.Count),
                    mapped));
            }
        }

        return new StartingEquipment(fixedList, choices, gold, fixedGoldCp is > 0 ? fixedGoldCp : null);
    }

    private static void Collect(
        EquipmentOptionItemJson option,
        List<StartingItem> items,
        List<StartingCategoryPick> categories,
        List<string> labels,
        Func<ReferenceJson, int?, StartingItem> item)
    {
        switch (option.OptionType)
        {
            case "counted_reference" when option.Of?.Index is not null:
                var counted = item(option.Of, option.Count);
                items.Add(counted);
                labels.Add(counted.Quantity > 1 ? $"{counted.Quantity} {Plural(counted.Name ?? counted.Item)}" : counted.Name ?? counted.Item);
                break;
            case "multiple":
                foreach (var part in option.Items ?? [])
                {
                    Collect(part, items, categories, labels, item);
                }

                break;
            case "choice" when option.Choice?.From?.EquipmentCategory is { Index: not null } category:
                categories.Add(new StartingCategoryPick(category.Index, Math.Max(1, option.Choice.Choose ?? 1)));
                labels.Add(Label(option.Choice.Desc) ?? category.Name ?? category.Index);
                break;
        }
    }

    /// <summary>"a martial weapon" → "Martial weapon"; "any simple weapon" → "Any simple weapon".</summary>
    private static string? Label(string? description)
    {
        var text = description?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        foreach (var article in (string[])["a ", "an "])
        {
            if (text.StartsWith(article, StringComparison.OrdinalIgnoreCase) && text.Length > article.Length)
            {
                text = text[article.Length..];
                break;
            }
        }

        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string Plural(string name) => name.EndsWith('s') ? name : name + "s";

    // ---- Helpers -------------------------------------------------------------------------------

    private static ItemRarity? ParseRarity(string? name) =>
        name is not null && Enum.TryParse<ItemRarity>(name.Replace(" ", string.Empty, StringComparison.Ordinal), ignoreCase: true, out var rarity)
            ? rarity
            : null;

    private static List<string> Indexes(IEnumerable<ReferenceJson>? references) =>
        (references ?? []).Select(r => r.Index).OfType<string>().ToList();

    private static int? OptionalInt(JsonElement? element, string property) =>
        element is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static string AbilityBonusesJson(IEnumerable<AbilityBonusJson>? bonuses) =>
        JsonSerializer.Serialize(
            (bonuses ?? [])
                .Where(b => b.AbilityScore?.Index is not null)
                .Select(b => new AbilityBonus(b.AbilityScore!.Index!, b.Bonus ?? 0))
                .ToList(),
            CamelCase);

    /// <summary>
    /// Normalizes the spell damage (an array of parts in this dataset, a single object in older
    /// versions) to <c>[{"type", "atSlotLevel", "atCharacterLevel"}]</c>.
    /// </summary>
    private static string? SpellDamageJson(JsonElement? damage)
    {
        var parts = damage switch
        {
            { ValueKind: JsonValueKind.Array } array => array.EnumerateArray().ToList(),
            { ValueKind: JsonValueKind.Object } single => [single],
            _ => [],
        };

        var mapped = parts
            .Select(p => p.Deserialize<SpellDamagePartJson>(JsonOptions))
            .OfType<SpellDamagePartJson>()
            .Select(p => new StoredSpellDamage(p.DamageType?.Name, ToLevelMap(p.DamageAtSlotLevel), ToLevelMap(p.DamageAtCharacterLevel)))
            .Where(p => p.AtSlotLevel is not null || p.AtCharacterLevel is not null)
            .ToList();

        return mapped.Count == 0 ? null : JsonSerializer.Serialize(mapped, CamelCase);
    }

    private static SortedDictionary<int, string>? ToLevelMap(Dictionary<string, string>? source)
    {
        if (source is null || source.Count == 0)
        {
            return null;
        }

        var map = new SortedDictionary<int, string>();
        foreach (var (key, value) in source)
        {
            if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
            {
                map[level] = value;
            }
        }

        return map.Count == 0 ? null : map;
    }

    private static string StartingEquipment(List<StartingEquipmentJson>? fixedItems, List<EquipmentOptionJson>? options, CostJson? gold)
    {
        var lines = new List<string>();
        lines.AddRange((fixedItems ?? []).Select(e => Counted(e.Equipment?.Name, e.Quantity)));
        foreach (var option in options ?? [])
        {
            if (!string.IsNullOrWhiteSpace(option.Desc))
            {
                lines.Add(option.Desc);
            }
            else if (option.From?.EquipmentCategory?.Name is { } categoryName)
            {
                lines.Add(option.Choose is > 1 ? $"{option.Choose} of: {categoryName}" : $"One of: {categoryName}");
            }
        }

        if (gold?.Quantity is { } quantity)
        {
            lines.Add($"{quantity} {gold.Unit}");
        }

        return string.Join("\n", lines.Where(l => l.Length > 0));
    }

    private static string Counted(string? name, int? quantity) =>
        name is null ? string.Empty : quantity is > 1 ? $"{name} (x{quantity})" : name;

    private static string? Join(string? a, string? b) =>
        a is null && b is null ? null : string.Join(' ', new[] { a, b }.OfType<string>());

    // ---- Stored shapes -------------------------------------------------------------------------

    private sealed record StoredSpellDamage(
        string? Type,
        SortedDictionary<int, string>? AtSlotLevel,
        SortedDictionary<int, string>? AtCharacterLevel);

    // ---- Read models (only the fields the catalog needs) ---------------------------------------

    private sealed class ReferenceJson
    {
        public string? Index { get; set; }

        public string? Name { get; set; }
    }

    private sealed class CostJson
    {
        public int? Quantity { get; set; }

        public string? Unit { get; set; }
    }

    private sealed class ClassJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public int? HitDie { get; set; }

        public List<ReferenceJson>? Proficiencies { get; set; }

        public List<ReferenceJson>? SavingThrows { get; set; }

        public List<ProficiencyChoiceJson>? ProficiencyChoices { get; set; }

        public List<StartingEquipmentJson>? StartingEquipment { get; set; }

        public List<EquipmentOptionJson>? StartingEquipmentOptions { get; set; }

        public ClassSpellcastingJson? Spellcasting { get; set; }
    }

    private sealed class ProficiencyChoiceJson
    {
        public int? Choose { get; set; }

        public ProficiencyChoiceSourceJson? From { get; set; }
    }

    private sealed class ProficiencyChoiceSourceJson
    {
        public List<ProficiencyChoiceOptionJson>? Options { get; set; }
    }

    private sealed class ProficiencyChoiceOptionJson
    {
        public ReferenceJson? Item { get; set; }
    }

    private sealed class ClassSpellcastingJson
    {
        public ReferenceJson? SpellcastingAbility { get; set; }
    }

    private sealed class StartingEquipmentJson
    {
        public ReferenceJson? Equipment { get; set; }

        public int? Quantity { get; set; }
    }

    private sealed class EquipmentOptionJson
    {
        public string? Desc { get; set; }

        public int? Choose { get; set; }

        public EquipmentOptionSourceJson? From { get; set; }
    }

    private sealed class EquipmentOptionSourceJson
    {
        public string? OptionSetType { get; set; }

        public ReferenceJson? EquipmentCategory { get; set; }

        public List<EquipmentOptionItemJson>? Options { get; set; }
    }

    private sealed class EquipmentOptionItemJson
    {
        public string? OptionType { get; set; }

        public int? Count { get; set; }

        public ReferenceJson? Of { get; set; }

        public List<EquipmentOptionItemJson>? Items { get; set; }

        public EquipmentOptionJson? Choice { get; set; }

        public List<JsonElement>? Prerequisites { get; set; }
    }

    private sealed class EquipmentCategoryJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public List<ReferenceJson>? Equipment { get; set; }
    }

    private sealed class LevelJson
    {
        public string Index { get; set; } = string.Empty;

        public int? Level { get; set; }

        public int? AbilityScoreBonuses { get; set; }

        public int? ProfBonus { get; set; }

        public List<ReferenceJson>? Features { get; set; }

        public JsonElement? Spellcasting { get; set; }

        public JsonElement? ClassSpecific { get; set; }

        public ReferenceJson? Class { get; set; }

        public ReferenceJson? Subclass { get; set; }
    }

    private sealed class SubclassJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public ReferenceJson? Class { get; set; }

        public string? SubclassFlavor { get; set; }

        public List<string>? Desc { get; set; }
    }

    private sealed class FeatureJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public int? Level { get; set; }

        public ReferenceJson? Class { get; set; }

        public ReferenceJson? Subclass { get; set; }

        public List<string>? Desc { get; set; }
    }

    private sealed class AbilityBonusJson
    {
        public ReferenceJson? AbilityScore { get; set; }

        public int? Bonus { get; set; }
    }

    private sealed class RaceJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public int? Speed { get; set; }

        public List<AbilityBonusJson>? AbilityBonuses { get; set; }

        public string? Alignment { get; set; }

        public string? Age { get; set; }

        public string? Size { get; set; }

        public string? SizeDescription { get; set; }

        public List<ReferenceJson>? Languages { get; set; }

        public List<ReferenceJson>? Traits { get; set; }

        public List<ReferenceJson>? Subraces { get; set; }
    }

    private sealed class SubraceJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public ReferenceJson? Race { get; set; }

        public List<string>? Desc { get; set; }

        public List<AbilityBonusJson>? AbilityBonuses { get; set; }

        public List<ReferenceJson>? RacialTraits { get; set; }
    }

    private sealed class TraitJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public List<string>? Desc { get; set; }

        public List<ReferenceJson>? Races { get; set; }

        public List<ReferenceJson>? Subraces { get; set; }
    }

    private sealed class SpellJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public List<string>? Desc { get; set; }

        public List<string>? HigherLevel { get; set; }

        public string? Range { get; set; }

        public List<string>? Components { get; set; }

        public string? Material { get; set; }

        public bool? Ritual { get; set; }

        public string? Duration { get; set; }

        public bool? Concentration { get; set; }

        public string? CastingTime { get; set; }

        public int? Level { get; set; }

        public string? AttackType { get; set; }

        public JsonElement? Damage { get; set; }

        public SpellDcJson? Dc { get; set; }

        public ReferenceJson? School { get; set; }

        public List<ReferenceJson>? Classes { get; set; }

        public List<ReferenceJson>? Subclasses { get; set; }
    }

    private sealed class SpellDcJson
    {
        public ReferenceJson? DcType { get; set; }
    }

    private sealed class SpellDamagePartJson
    {
        public ReferenceJson? DamageType { get; set; }

        public Dictionary<string, string>? DamageAtSlotLevel { get; set; }

        public Dictionary<string, string>? DamageAtCharacterLevel { get; set; }
    }

    private sealed class DamageJson
    {
        public string? DamageDice { get; set; }

        public ReferenceJson? DamageType { get; set; }
    }

    private sealed class RangeJson
    {
        public int? Normal { get; set; }

        public int? Long { get; set; }
    }

    private sealed class ArmorClassJson
    {
        public int? Base { get; set; }

        public bool? DexBonus { get; set; }

        public int? MaxBonus { get; set; }
    }

    private sealed class ContentJson
    {
        public ReferenceJson? Item { get; set; }

        public int? Quantity { get; set; }
    }

    private sealed class SpeedJson
    {
        public decimal? Quantity { get; set; }

        public string? Unit { get; set; }
    }

    private sealed class EquipmentJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public ReferenceJson? EquipmentCategory { get; set; }

        public string? WeaponCategory { get; set; }

        public string? WeaponRange { get; set; }

        public string? CategoryRange { get; set; }

        public string? ArmorCategory { get; set; }

        public ReferenceJson? GearCategory { get; set; }

        public string? ToolCategory { get; set; }

        public string? VehicleCategory { get; set; }

        public CostJson? Cost { get; set; }

        public decimal? Weight { get; set; }

        public DamageJson? Damage { get; set; }

        public DamageJson? TwoHandedDamage { get; set; }

        public RangeJson? Range { get; set; }

        public RangeJson? ThrowRange { get; set; }

        public List<ReferenceJson>? Properties { get; set; }

        public ArmorClassJson? ArmorClass { get; set; }

        public int? StrMinimum { get; set; }

        public bool? StealthDisadvantage { get; set; }

        public List<string>? Desc { get; set; }

        public List<string>? Special { get; set; }

        public int? Quantity { get; set; }

        public List<ContentJson>? Contents { get; set; }

        public SpeedJson? Speed { get; set; }

        public string? Capacity { get; set; }
    }

    private sealed class MagicItemJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public ReferenceJson? EquipmentCategory { get; set; }

        public ReferenceJson? Rarity { get; set; }

        public List<string>? Desc { get; set; }
    }

    private sealed class ConditionJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public List<string>? Desc { get; set; }
    }

    private sealed class SkillJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public ReferenceJson? AbilityScore { get; set; }

        public List<string>? Desc { get; set; }
    }

    private sealed class BackgroundFeatureJson
    {
        public string? Name { get; set; }

        public List<string>? Desc { get; set; }
    }

    private sealed class BackgroundJson
    {
        public string Index { get; set; } = string.Empty;

        public string? Name { get; set; }

        public List<ReferenceJson>? StartingProficiencies { get; set; }

        public List<StartingEquipmentJson>? StartingEquipment { get; set; }

        public List<EquipmentOptionJson>? StartingEquipmentOptions { get; set; }

        public CostJson? StartingGold { get; set; }

        public BackgroundFeatureJson? Feature { get; set; }
    }

    /// <summary>Reads a list of strings that may also come as a single string (e.g. subrace <c>desc</c>).</summary>
    private sealed class StringListConverter : JsonConverter<List<string>>
    {
        public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.String:
                    return [reader.GetString()!];
                case JsonTokenType.StartArray:
                    var list = new List<string>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            list.Add(reader.GetString()!);
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }

                    return list;
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.ToArray(), options);
    }
}
