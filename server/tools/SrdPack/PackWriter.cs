using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Tools.SrdPack;

/// <summary>
/// Writes the SRD catalog (the rows <see cref="SrdDataset"/>, <see cref="SrdBeasts"/> and the hand-written level choice
/// files produce) as a content pack of format 3 (<c>docs/content-packs.md</c>). Every value the rows carry is written,
/// so that importing the pack gives back the same rows; the conversion fails instead of dropping data it cannot express.
/// </summary>
internal sealed class PackWriter(SrdCatalog catalog, IReadOnlyList<BeastDto> beasts, JsonObject optionSetsFile, JsonObject levelChoicesFile)
{
    /// <summary>Mandatory CC-BY 4.0 attribution of the SRD 5.1 (as in <c>Dnd5eSystem.SrdAttributionText</c>).</summary>
    public const string Attribution =
        "This work includes material taken from the System Reference Document 5.1 (“SRD 5.1”) by Wizards of the Coast LLC "
        + "and available at https://dnd.wizards.com/resources/systems-reference-document. The SRD 5.1 is licensed under the "
        + "Creative Commons Attribution 4.0 International License available at https://creativecommons.org/licenses/by/4.0/legalcode.";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>The pack without its version (<see cref="Program"/> sets it).</summary>
    public JsonObject Write()
    {
        var pack = new JsonObject
        {
            ["formatVersion"] = 3,
            ["system"] = Dnd5eCatalogSources.SystemId,
            ["id"] = Dnd5eCatalogSources.Srd,
            ["name"] = Dnd5eCatalogSources.SrdName,
            ["version"] = string.Empty,
            ["attribution"] = Attribution,
        };
        pack["skills"] = Array(catalog.Skills.Select(Skill));
        pack["reference"] = Reference();
        pack["conditions"] = Array(catalog.Conditions.Select(c => Obj(("index", c.Index), ("name", c.Name), ("description", Strings(c.Description)))));
        pack["optionSets"] = OptionSets();
        pack["classes"] = Array(catalog.Classes.Select(Class));
        pack["items"] = Array(catalog.Items.Select(i => Item(i.Index, i.Data)));
        pack["spells"] = Array(catalog.Spells.Select(Spell));
        pack["traits"] = Array(catalog.Traits.Select(Trait));
        pack["races"] = Array(catalog.Races.Select(Race));
        pack["backgrounds"] = Array(catalog.Backgrounds.Select(Background));
        pack["creatures"] = Array(beasts.Select(Creature));
        return pack;
    }

    // ---- Vocabularies, skills and option sets ----------------------------------------------------

    private static JsonObject Skill(SkillDefinition s) =>
        Obj(("index", s.Index), ("name", s.Name), ("ability", s.AbilityIndex), ("description", Strings(s.Description)));

    private JsonObject Reference()
    {
        JsonArray Kind(string kind) => Array(catalog.ReferenceEntries.Where(e => e.Kind == kind).Select(e =>
        {
            var entry = Obj(("index", e.Index), ("name", e.Name), ("description", Strings(JsonSerializer.Deserialize<List<string>>(e.DescriptionJson) ?? [])));
            if (kind == ReferenceEntry.EquipmentCategories)
            {
                var category = catalog.EquipmentCategories.SingleOrDefault(c => c.Index == e.Index)
                    ?? throw new InvalidOperationException($"Equipment category '{e.Index}' has no row.");
                if (category.Name != e.Name)
                {
                    throw new InvalidOperationException($"Equipment category '{e.Index}': the name of the row and of the vocabulary differ.");
                }

                entry["items"] = Strings(category.ItemIndexes);
            }

            return entry;
        }));

        if (catalog.EquipmentCategories.Count != catalog.ReferenceEntries.Count(e => e.Kind == ReferenceEntry.EquipmentCategories))
        {
            throw new InvalidOperationException("Every equipment category must also be a vocabulary entry.");
        }

        return new JsonObject
        {
            ["languages"] = Kind(ReferenceEntry.Languages),
            ["weaponProperties"] = Kind(ReferenceEntry.WeaponProperties),
            ["equipmentCategories"] = Kind(ReferenceEntry.EquipmentCategories),
            ["damageTypes"] = Kind(ReferenceEntry.DamageTypes),
            ["magicSchools"] = Kind(ReferenceEntry.MagicSchools),
            ["tools"] = Kind(ReferenceEntry.Tools),
        };
    }

    /// <summary><c>option-sets.json</c> is already in the pack shape; null values are left out.</summary>
    private JsonArray OptionSets() =>
        Array(optionSetsFile["sets"]!.AsArray().Select(set => Compact(set!.DeepClone())!.AsObject()));

    // ---- Classes ---------------------------------------------------------------------------------

    private JsonObject Class(ClassDefinition c)
    {
        if (c.SpellcastingJson is not null || c.MulticlassJson is not null || c.ResourcesJson is not null || c.SpellListJson is not null
            || c.SubclassLevel != 0 || c.Description.Count > 0)
        {
            throw new InvalidOperationException($"Class '{c.Index}': the SRD classes have no pack-only fields.");
        }

        var armor = new List<string>();
        var weapons = new List<string>();
        var tools = new List<string>();
        foreach (var name in c.ProficiencyNames)
        {
            (name.Contains("armor", StringComparison.OrdinalIgnoreCase) || name == "Shields" ? armor
                : name.Contains("Kit", StringComparison.Ordinal) || name.Contains("Tools", StringComparison.Ordinal) || name.Contains("Supplies", StringComparison.Ordinal) ? tools
                : weapons).Add(name);
        }

        if (!armor.Concat(weapons).Concat(tools).SequenceEqual(c.ProficiencyNames))
        {
            throw new InvalidOperationException($"Class '{c.Index}': the proficiencies are not in the order armor, weapons, tools.");
        }

        var json = Obj(("index", c.Index), ("name", c.Name), ("hitDie", c.HitDie), ("savingThrows", Strings(c.SavingThrows)));
        var proficiencies = new JsonObject();
        Put(proficiencies, "armor", Strings(armor));
        Put(proficiencies, "weapons", Strings(weapons));
        Put(proficiencies, "tools", Strings(tools));
        Put(json, "proficiencies", proficiencies);
        var skillChoices = JsonNode.Parse(c.SkillChoicesJson)!.AsObject();
        if (skillChoices["choose"]!.GetValue<int>() > 0)
        {
            json["skillChoices"] = skillChoices;
        }

        Put(json, "startingEquipment", c.StartingEquipment is { } equipment ? StartingEquipment(equipment) : null);
        Put(json, "startingEquipmentText", c.StartingEquipmentText);
        Put(json, "subclassFlavor", c.SubclassFlavor);
        if (c.SpellcastingAbility is not null)
        {
            json["spellcastingAbility"] = c.SpellcastingAbility;
            json["multiclassSpellcasting"] = c.IsPactCaster ? "pact" : c.SpellcastingLevel switch
            {
                0 => "none",
                1 => "full",
                2 => "half",
                3 => "third",
                _ => throw new InvalidOperationException($"Class '{c.Index}': unknown spellcasting level {c.SpellcastingLevel}."),
            };
        }
        else if (c.IsSpellcaster || c.SpellcastingLevel != 0 || c.IsPactCaster)
        {
            throw new InvalidOperationException($"Class '{c.Index}': a caster without spellcasting ability.");
        }

        var levels = catalog.ClassLevels.Where(l => l.ClassIndex == c.Index).OrderBy(l => l.Level).ToList();
        if (levels.Select(l => l.Level).SequenceEqual(Enumerable.Range(1, 20)) is false)
        {
            throw new InvalidOperationException($"Class '{c.Index}': the levels are not 1 to 20.");
        }

        var listed = new HashSet<string>(StringComparer.Ordinal);
        var asiSoFar = 0;
        var levelArray = new JsonArray();
        foreach (var level in levels)
        {
            if (level.Index != $"{c.Index}-{level.Level}")
            {
                throw new InvalidOperationException($"Class level '{level.Index}' does not follow the pattern <class>-<level>.");
            }

            var entry = Obj(("level", level.Level), ("profBonus", level.ProfBonus));

            // The levels with an Ability Score Improvement are those of the "asi" level choices; the dataset's running count
            // is wrong for the rogue (11th level: 2 after 3 at 10th...), so it is only checked.
            var asi = AsiLevels(c.Index).Contains(level.Level);
            asiSoFar += asi ? 1 : 0;
            if (level.AbilityScoreBonuses != asiSoFar)
            {
                Console.Error.WriteLine($"warning: class level '{level.Index}': the dataset counts {level.AbilityScoreBonuses} ability score improvements; the pack, {asiSoFar}.");
            }

            entry["abilityScoreImprovement"] = asi;
            entry["features"] = Array(level.FeatureIndexes.Select(index =>
            {
                listed.Add(index);
                return Feature(index, c.Index, null, level.Level, withLevel: false);
            }));
            if (level.SpellSlots.Any(s => s != 0))
            {
                entry["spellSlots"] = new JsonArray(level.SpellSlots.Select(s => (JsonNode)s).ToArray());
            }

            Put(entry, "cantripsKnown", level.CantripsKnown);
            Put(entry, "spellsKnown", level.SpellsKnown);
            if (JsonNode.Parse(level.ClassSpecificJson) is JsonObject { Count: > 0 } specific)
            {
                entry["classSpecific"] = specific;
            }

            levelArray.Add(entry);
        }

        json["levels"] = levelArray;
        var subclasses = catalog.Subclasses.Where(s => s.ClassIndex == c.Index).Select(s => Subclass(s, listed)).ToList();

        // Features no level lists (options and parts of other features: "Fighting Style: Archery"...).
        Put(json, "features", Array(catalog.Features
            .Where(f => f.ClassIndex == c.Index && f.SubclassIndex is null && !listed.Contains(f.Index))
            .Select(f => Feature(f.Index, c.Index, null, f.Level, withLevel: true))));
        Put(json, "levelChoices", LevelChoices(c.Index, null));
        json["subclasses"] = Array(subclasses);
        return json;
    }

    /// <summary>Levels of the class with an Ability Score Improvement (its <c>AsiOrFeat</c> level choices).</summary>
    private HashSet<int> AsiLevels(string classIndex) =>
        levelChoicesFile["rules"]!.AsArray()
            .Select(r => r!.AsObject())
            .Where(r => r["classIndex"]!.GetValue<string>() == classIndex && r["subclassIndex"] is null && r["kind"]!.GetValue<string>() == "AsiOrFeat")
            .Select(r => r["level"]!.GetValue<int>())
            .ToHashSet();

    private JsonObject Subclass(SubclassDefinition s, HashSet<string> listed)
    {
        if (s.SpellcastingJson is not null || s.ExpandedSpellListJson is not null)
        {
            throw new InvalidOperationException($"Subclass '{s.Index}': the SRD subclasses have no pack-only fields.");
        }

        var json = Obj(("index", s.Index), ("name", s.Name), ("flavor", s.Flavor), ("description", Strings(s.Description)));
        var levels = new JsonArray();
        foreach (var level in catalog.SubclassLevels.Where(l => l.SubclassIndex == s.Index).OrderBy(l => l.Level))
        {
            if (level.Index != $"{s.Index}-{level.Level}" || level.GrantsJson is not null)
            {
                throw new InvalidOperationException($"Subclass level '{level.Index}' cannot be written as a pack level.");
            }

            levels.Add(Obj(("level", level.Level), ("features", Array(level.FeatureIndexes.Select(index =>
            {
                listed.Add(index);
                return Feature(index, s.ClassIndex, s.Index, level.Level, withLevel: false);
            })))));
        }

        json["levels"] = levels;
        Put(json, "features", Array(catalog.Features
            .Where(f => f.SubclassIndex == s.Index && !listed.Contains(f.Index))
            .Select(f =>
            {
                listed.Add(f.Index);
                return Feature(f.Index, s.ClassIndex, s.Index, f.Level, withLevel: true);
            })));
        Put(json, "levelChoices", LevelChoices(s.ClassIndex, s.Index));
        return json;
    }

    private JsonObject Feature(string index, string classIndex, string? subclassIndex, int level, bool withLevel)
    {
        var feature = catalog.Features.SingleOrDefault(f => f.Index == index)
            ?? throw new InvalidOperationException($"Feature '{index}' does not exist.");
        if (feature.ClassIndex != classIndex || feature.Level != level)
        {
            throw new InvalidOperationException($"Feature '{index}' is listed by a level of another class or level.");
        }

        if (feature.SubclassIndex != subclassIndex)
        {
            // The dataset files "supreme-healing" (Life Domain, 17) without its subclass although the subclass level lists it.
            Console.Error.WriteLine($"warning: feature '{index}' is listed by {subclassIndex ?? classIndex} {level} but the dataset gives it the subclass '{feature.SubclassIndex ?? "(none)"}'; the pack follows the level.");
        }

        if (feature.ResourceJson is not null || feature.CompanionJson is not null || feature.ModifiersJson is not null)
        {
            throw new InvalidOperationException($"Feature '{index}': the SRD features have no pack-only fields.");
        }

        var json = Obj(("index", feature.Index), ("name", feature.Name));
        if (withLevel)
        {
            json["level"] = feature.Level;
        }

        json["description"] = Strings(feature.Description);
        return json;
    }

    /// <summary>The rules of <c>level-choices.json</c> of a class (or of one of its subclasses), without the class fields.</summary>
    private JsonArray LevelChoices(string classIndex, string? subclassIndex) =>
        Array(levelChoicesFile["rules"]!.AsArray()
            .Select(r => r!.AsObject())
            .Where(r => r["classIndex"]!.GetValue<string>() == classIndex && r["subclassIndex"]?.GetValue<string>() == subclassIndex)
            .Select(r =>
            {
                var rule = Compact(r.DeepClone())!.AsObject();
                rule.Remove("classIndex");
                rule.Remove("subclassIndex");
                return rule;
            }));

    private static JsonObject StartingEquipment(StartingEquipment equipment)
    {
        var json = new JsonObject();
        Put(json, "fixed", Array(equipment.Fixed.Select(StartingItem)));
        Put(json, "choices", Array(equipment.Choices.Select(choice => Obj(
            ("description", choice.Description),
            ("choose", choice.Choose),
            ("options", Array(choice.Options.Select(option =>
            {
                var o = Obj(("label", option.Label));
                Put(o, "items", Array(option.Items.Select(StartingItem)));
                Put(o, "categories", Array(option.Categories.Select(p => Obj(("category", p.Category), ("choose", p.Choose)))));
                return o;
            })))))));
        if (equipment.Gold is { } gold)
        {
            json["gold"] = Obj(("dice", gold.Dice), ("multiplier", gold.Multiplier));
        }

        Put(json, "fixedGoldCp", equipment.FixedGoldCp);
        return json;
    }

    /// <summary>A starting item; the contents of equipment packs come from the item (<c>items[].contents</c>).</summary>
    private static JsonObject StartingItem(StartingItem item)
    {
        if (item.Contents is { Count: > 0 } contents
            && !contents.SequenceEqual(SrdDataset.PackContents.GetValueOrDefault(item.Item) ?? []))
        {
            throw new InvalidOperationException($"Starting item '{item.Item}': its contents are not those of the item.");
        }

        var json = Obj(("item", item.Item), ("quantity", item.Quantity));
        Put(json, "name", item.Name);
        return json;
    }

    // ---- Items, spells -----------------------------------------------------------------------------

    private static JsonObject Item(string index, ItemTemplateData data)
    {
        if (data.Effects.Count > 0 || data.SystemDataJson is not null)
        {
            throw new InvalidOperationException($"Item '{index}': the SRD items have no effects nor system data.");
        }

        var json = Obj(("index", index), ("name", data.Name), ("category", data.Category.ToString()));
        Put(json, "subcategory", data.Subcategory);
        Put(json, "rarity", data.Rarity?.ToString());
        Put(json, "requiresAttunement", data.RequiresAttunement ? true : null);
        Put(json, "costCp", data.Cost);
        Put(json, "weightLb", data.WeightLb);
        Put(json, "damageDice", data.DamageDice);
        Put(json, "damageType", data.DamageType);
        Put(json, "versatileDice", data.VersatileDice);
        Put(json, "properties", Strings(data.Properties));
        Put(json, "rangeNormal", data.RangeNormal);
        Put(json, "rangeLong", data.RangeLong);
        Put(json, "armorClassBase", data.ArmorClassBase);
        Put(json, "addDexModifier", data.AddDexModifier);
        Put(json, "maxDexBonus", data.MaxDexBonus);
        Put(json, "strengthMinimum", data.StrengthMinimum);
        Put(json, "stealthDisadvantage", data.StealthDisadvantage ? true : null);
        Put(json, "description", Strings(data.Description));
        Put(json, "modifiers", Array(data.Modifiers.Select(m =>
        {
            var modifier = Obj(("kind", m.Kind.ToString()));
            Put(modifier, "target", m.Target);
            modifier["value"] = m.Value;
            return modifier;
        })));
        Put(json, "contents", Array((SrdDataset.PackContents.GetValueOrDefault(index) ?? []).Select(StartingItem)));
        return json;
    }

    private static JsonObject Spell(SpellDefinition s)
    {
        var json = Obj(
            ("index", s.Index),
            ("name", s.Name),
            ("level", s.Level),
            ("school", s.School.ToLowerInvariant()),
            ("castingTime", s.CastingTime),
            ("range", s.Range),
            ("components", Strings(s.Components)));
        Put(json, "material", s.Material);
        json["duration"] = s.Duration;
        Put(json, "concentration", s.Concentration ? true : null);
        Put(json, "ritual", s.Ritual ? true : null);
        json["description"] = Strings(s.Description);
        Put(json, "higherLevel", Strings(s.HigherLevel));
        Put(json, "classes", Strings(s.ClassIndexes));
        Put(json, "subclasses", Strings(s.SubclassIndexes));
        Put(json, "attackType", s.AttackType);
        if (s.DamageJson is not null)
        {
            // One part as an object, several (Flame Strike: fire and radiant) as an array.
            var parts = JsonNode.Parse(s.DamageJson)!.AsArray();
            json["damage"] = parts.Count == 1 ? Compact(parts[0]!.DeepClone()) : Compact(parts.DeepClone());
        }

        if (s.HealJson is not null)
        {
            json["healAtSlotLevel"] = JsonNode.Parse(s.HealJson);
        }

        Put(json, "dcAbility", s.DcAbility);
        json["category"] = s.Category.ToString();
        return json;
    }

    // ---- Races, traits, backgrounds ----------------------------------------------------------------

    private static JsonObject Trait(TraitDefinition t)
    {
        var json = Obj(("index", t.Index), ("name", t.Name), ("description", Strings(t.Description)));
        Put(json, "races", Strings(t.RaceIndexes));
        Put(json, "subraces", Strings(t.SubraceIndexes));
        return json;
    }

    private JsonObject Race(RaceDefinition r)
    {
        if (r.HeightWeightJson is not null)
        {
            throw new InvalidOperationException($"Race '{r.Index}': the SRD races have no height and weight table.");
        }

        var json = Obj(("index", r.Index), ("name", r.Name), ("speed", r.Speed), ("size", r.Size));
        Put(json, "sizeDescription", r.SizeDescription);
        json["abilityBonuses"] = Array(JsonSerializer.Deserialize<List<AbilityBonus>>(r.AbilityBonusesJson, Web)!.Select(b => Obj(("ability", b.Ability), ("bonus", b.Bonus))));
        Put(json, "languages", Strings(r.Languages));
        Put(json, "age", r.Age);
        Put(json, "alignment", r.Alignment);
        Put(json, "traits", Strings(r.TraitIndexes));
        var subraces = catalog.Subraces.Where(s => s.RaceIndex == r.Index).ToList();
        if (!subraces.Select(s => s.Index).SequenceEqual(r.SubraceIndexes))
        {
            throw new InvalidOperationException($"Race '{r.Index}': its subraces are not those that point to it.");
        }

        Put(json, "subraces", Array(subraces.Select(Subrace)));
        Put(json, "choices", Choices(r.ChoicesJson));
        Put(json, "resistances", Strings(r.Resistances));
        Put(json, "grants", r.GrantsJson is null ? null : Compact(JsonNode.Parse(r.GrantsJson)));
        return json;
    }

    private static JsonObject Subrace(SubraceDefinition s)
    {
        if (s.Speed is not null || s.HeightWeightJson is not null)
        {
            throw new InvalidOperationException($"Subrace '{s.Index}': the SRD subraces do not change speed nor height.");
        }

        var json = Obj(("index", s.Index), ("name", s.Name));
        Put(json, "description", s.Description);
        json["abilityBonuses"] = Array(JsonSerializer.Deserialize<List<AbilityBonus>>(s.AbilityBonusesJson, Web)!.Select(b => Obj(("ability", b.Ability), ("bonus", b.Bonus))));
        Put(json, "traits", Strings(s.TraitIndexes));
        Put(json, "choices", Choices(s.ChoicesJson));
        Put(json, "resistances", Strings(s.Resistances));
        Put(json, "grants", s.GrantsJson is null ? null : Compact(JsonNode.Parse(s.GrantsJson)));
        return json;
    }

    /// <summary>Origin choices; the options keep their names (<c>{ "index", "name" }</c>) when they differ from the index.</summary>
    private static JsonObject? Choices(string? choicesJson)
    {
        if (choicesJson is null)
        {
            return null;
        }

        var choices = RaceChoices.Parse(choicesJson);
        static JsonArray Options(IEnumerable<OriginOption> options) =>
            Array(options.Select(o => o.Index == o.Name ? (JsonNode)o.Index : Obj(("index", o.Index), ("name", o.Name))));

        static JsonObject Pick(PickChoice pick)
        {
            var json = Obj(("choose", pick.Choose));
            Put(json, "from", Options(pick.From));
            return json;
        }

        var result = new JsonObject();
        if (choices.AbilityBonuses is { } abilities)
        {
            result["abilityBonuses"] = Obj(("choose", abilities.Choose), ("amount", abilities.Amount), ("from", Options(abilities.From)));
        }

        Put(result, "skills", choices.Skills is { } skills ? Pick(skills) : null);
        Put(result, "languages", choices.Languages is { } languages ? Pick(languages) : null);
        Put(result, "tools", choices.Tools is { } tools ? Pick(tools) : null);
        if (choices.Cantrip is { } cantrip)
        {
            var json = Obj(("choose", cantrip.Choose), ("spellList", cantrip.SpellList));
            Put(json, "from", Options(cantrip.From));
            result["cantrip"] = json;
        }

        if (choices.Feats is { } feats)
        {
            result["feats"] = Obj(("choose", feats.Choose));
        }

        Put(result, "traitOptions", Array(choices.TraitOptions.Select(t => Obj(
            ("key", t.Key),
            ("name", t.Name),
            ("choose", t.Choose),
            ("options", Array(t.Options.Select(o =>
            {
                var option = Obj(("index", o.Index), ("name", o.Name), ("description", Strings(o.Description)));
                Put(option, "damageType", o.DamageType);
                if (o.BreathWeapon is { } breath)
                {
                    option["breathWeapon"] = Obj(
                        ("name", breath.Name),
                        ("area", breath.Area),
                        ("save", breath.SaveAbility),
                        ("damageAtCharacterLevel", new JsonObject(breath.DamageAtCharacterLevel
                            .OrderBy(d => d.Key)
                            .Select(d => KeyValuePair.Create(d.Key.ToString(CultureInfo.InvariantCulture), (JsonNode?)d.Value)))));
                }

                return option;
            })))))));
        return result;
    }

    private JsonObject Background(BackgroundDefinition b)
    {
        if (b.OptionalTablesJson is not null)
        {
            throw new InvalidOperationException($"Background '{b.Index}': the SRD backgrounds have no optional tables.");
        }

        var json = Obj(("index", b.Index), ("name", b.Name));
        Put(json, "featureName", b.FeatureName);
        Put(json, "featureDescription", Strings(b.FeatureDescription));
        Put(json, "skillProficiencies", Strings(b.SkillProficiencies.Select(name =>
            catalog.Skills.SingleOrDefault(s => s.Name == name)?.Index ?? throw new InvalidOperationException($"Background '{b.Index}': unknown skill '{name}'."))));
        Put(json, "startingEquipmentText", b.StartingEquipmentText);
        Put(json, "startingEquipment", b.StartingEquipment is { } equipment ? StartingEquipment(equipment) : null);
        Put(json, "choices", Choices(b.ChoicesJson));
        if (b.Personality is { } personality)
        {
            json["personality"] = Obj(
                ("traits", Strings(personality.Traits)),
                ("ideals", Array(personality.Ideals.Select(i =>
                {
                    var ideal = Obj(("text", i.Text));
                    Put(ideal, "alignment", i.Alignment);
                    return ideal;
                }))),
                ("bonds", Strings(personality.Bonds)),
                ("flaws", Strings(personality.Flaws)));
        }

        return json;
    }

    // ---- Creatures ---------------------------------------------------------------------------------

    private static JsonObject Creature(BeastDto b)
    {
        if (b.Reactions.Count > 0 || b.LegendaryActions.Count > 0 || b.Subtype is not null)
        {
            throw new InvalidOperationException($"Creature '{b.Index}': the SRD beasts have no reactions, legendary actions nor subtype.");
        }

        static JsonObject Numbers(IReadOnlyDictionary<string, int> values) =>
            new(values.Select(v => KeyValuePair.Create(v.Key, (JsonNode?)v.Value)));

        static JsonObject? Save(BeastSaveDto? save) => save is null ? null : Obj(("dc", save.Dc), ("ability", save.Ability));

        var json = Obj(("index", b.Index), ("name", b.Name), ("size", b.Size), ("type", b.Type));
        Put(json, "alignment", b.Alignment);
        json["armorClass"] = b.ArmorClass;
        Put(json, "armorClassType", b.ArmorClassType);
        json["hitPoints"] = b.HitPoints;
        Put(json, "hitDice", b.HitDice);
        Put(json, "hitPointsRoll", b.HitPointsRoll);
        json["speed"] = Numbers(b.Speeds);
        json["abilities"] = Numbers(b.Abilities);
        Put(json, "savingThrows", b.SavingThrows.Count == 0 ? null : Numbers(b.SavingThrows));
        Put(json, "skills", b.Skills.Count == 0 ? null : Numbers(b.Skills));
        Put(json, "damageVulnerabilities", Strings(b.DamageVulnerabilities));
        Put(json, "damageResistances", Strings(b.DamageResistances));
        Put(json, "damageImmunities", Strings(b.DamageImmunities));
        Put(json, "conditionImmunities", Strings(b.ConditionImmunities));
        Put(json, "senses", b.Senses.Count == 0 ? null : new JsonObject(b.Senses.Select(s => KeyValuePair.Create(s.Key, (JsonNode?)s.Value))));
        json["passivePerception"] = b.PassivePerception;
        Put(json, "languages", b.Languages);
        json["challengeRating"] = b.ChallengeRating;
        json["xp"] = b.Xp;
        json["proficiencyBonus"] = b.ProficiencyBonus;
        Put(json, "traits", Array(b.Traits.Select(t =>
        {
            var trait = Obj(("name", t.Name), ("description", t.Description));
            Put(trait, "save", Save(t.Save));
            return trait;
        })));
        Put(json, "actions", Array(b.Actions.Select(a =>
        {
            var action = Obj(("name", a.Name), ("description", a.Description));
            Put(action, "attackBonus", a.AttackBonus);
            Put(action, "damage", Array(a.Damage.Select(d =>
            {
                var damage = Obj(("dice", d.Dice));
                Put(damage, "type", d.Type);
                return damage;
            })));
            Put(action, "save", Save(a.Save));
            Put(action, "multiattack", a.IsMultiattack ? true : null);
            return action;
        })));
        Put(json, "description", b.Description is null ? null : Strings([b.Description]));
        return json;
    }

    // ---- JSON helpers ------------------------------------------------------------------------------

    private static JsonObject Obj(params (string Key, object? Value)[] properties)
    {
        var json = new JsonObject();
        foreach (var (key, value) in properties)
        {
            json[key] = ToNode(value);
        }

        return json;
    }

    /// <summary>Sets the property unless the value is null, an empty text, an empty array or an empty object.</summary>
    private static void Put(JsonObject json, string key, object? value)
    {
        var node = ToNode(value);
        if (node is null || node is JsonArray { Count: 0 } || node is JsonObject { Count: 0 }
            || (node is JsonValue v && v.GetValueKind() == JsonValueKind.String && v.GetValue<string>().Length == 0))
        {
            return;
        }

        json[key] = node;
    }

    private static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        JsonNode node => node,
        _ => JsonSerializer.SerializeToNode(value),
    };

    private static JsonArray Array(IEnumerable<JsonNode> nodes) => new(nodes.ToArray());

    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(v => (JsonNode)JsonValue.Create(v)).ToArray());

    /// <summary>A copy without the null properties.</summary>
    private static JsonNode? Compact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                var result = new JsonObject();
                foreach (var (key, value) in o)
                {
                    if (value is not null)
                    {
                        result[key] = Compact(value.DeepClone());
                    }
                }

                return result;
            case JsonArray a:
                return new JsonArray(a.Select(e => Compact(e?.DeepClone())).ToArray());
            default:
                return node?.DeepClone();
        }
    }
}
