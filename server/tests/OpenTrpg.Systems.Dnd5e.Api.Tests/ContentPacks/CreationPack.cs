using System.Text.Json.Nodes;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>
/// Fictitious format-3 pack for the creation and level-up gaps (phase 35B1): every class, option and item is
/// invented ("Cronista del vacío", "Guadaña doble"...). Each test changes a copy when it needs a broken variant.
/// </summary>
internal static class CreationPack
{
    public const string Id = "vacio-prueba";

    /// <summary>Index with the pack prefix.</summary>
    public static string I(string name) => $"{Id}-{name}";

    /// <summary>The whole pack, as the tests import it.</summary>
    public static JsonObject Build() => new()
    {
        ["formatVersion"] = 3,
        ["system"] = "dnd5e",
        ["id"] = Id,
        ["name"] = "Crónicas del vacío (prueba)",
        ["version"] = "1.0.0",
        ["classes"] = new JsonArray(Herald(), Keeper(), Penitent(), Scribe(), Smith(), Lookout(), Veil(), Tamer()),
        ["optionSets"] = new JsonArray(Signals(), Tactics(), Heritages()),
        ["feats"] = Feats(),
        ["races"] = Races(),
        ["items"] = Items(),
    };

    /// <summary>A copy of <paramref name="pack"/> with another id and prefix, for the error tests.</summary>
    public static JsonObject Renamed(JsonObject pack, string id)
    {
        var json = pack.ToJsonString().Replace($"\"{Id}\"", $"\"{id}\"", StringComparison.Ordinal)
            .Replace($"{Id}-", $"{id}-", StringComparison.Ordinal);
        return JsonNode.Parse(json)!.AsObject();
    }

    /// <summary>Twenty levels with no features; <paramref name="features"/> adds features by level.</summary>
    public static JsonArray Levels(Func<int, JsonObject?>? level = null)
    {
        var levels = new JsonArray();
        for (var number = 1; number <= 20; number++)
        {
            levels.Add(level?.Invoke(number) ?? new JsonObject { ["level"] = number });
        }

        return levels;
    }

    /// <summary>A class with the fields every class needs; <paramref name="extra"/> adds or replaces fields.</summary>
    public static JsonObject Class(string index, string name, int hitDie, JsonObject? extra = null)
    {
        var result = new JsonObject
        {
            ["index"] = I(index),
            ["name"] = name,
            ["hitDie"] = hitDie,
            ["savingThrows"] = new JsonArray("str", "cha"),
            ["proficiencies"] = new JsonObject { ["armor"] = new JsonArray("Light Armor"), ["weapons"] = new JsonArray("Simple Weapons") },
            ["skillChoices"] = new JsonObject { ["choose"] = 2, ["from"] = new JsonArray("athletics", "history", "insight", "persuasion") },
            ["description"] = new JsonArray("Clase ficticia de prueba."),
            ["levels"] = Levels(),
        };
        foreach (var (key, value) in extra ?? [])
        {
            result[key] = value?.DeepClone();
        }

        return result;
    }

    /// <summary>
    /// H05/H06: "Heraldo de bruma", whose multiclass prerequisites are (Str 13 or Dex 13) and Cha 13, and whose
    /// multiclass proficiencies grow with its levels (heavy armor at 2, exotic armor at 3).
    /// </summary>
    private static JsonObject Herald() => Class("heraldo", "Heraldo de bruma", 10, new JsonObject
    {
        ["multiclassing"] = new JsonObject
        {
            ["prerequisites"] = new JsonObject
            {
                ["anyOf"] = new JsonArray(new JsonObject { ["str"] = 13 }, new JsonObject { ["dex"] = 13 }),
                ["cha"] = 13,
            },
            ["proficiencies"] = new JsonObject { ["armor"] = new JsonArray("light", "medium") },
            ["proficienciesByLevel"] = new JsonObject
            {
                ["2"] = new JsonObject { ["armor"] = new JsonArray("heavy") },
                ["3"] = new JsonObject { ["armor"] = new JsonArray("exotic") },
            },
        },
        // H20: its tactics reuse an SRD fighting style and a signal of another set.
        ["levelChoices"] = new JsonArray(new JsonObject
        {
            ["level"] = 2,
            ["key"] = "tacticas",
            ["name"] = "Tácticas",
            ["kind"] = "OptionSet",
            ["setId"] = I("tacticas"),
            ["choose"] = 1,
            ["from"] = new JsonArray(I("tactica-niebla"), "fighting-style-defense", I("senal-basica")),
        }),
    });

    /// <summary>
    /// H01: "Penitente de ceniza" casts without slots: each spell spends a use of "Plegarias" and the hit points of
    /// its level, up to a maximum spell level by class level; it prepares Wis + half its level.
    /// </summary>
    private static JsonObject Penitent() => Class("penitente", "Penitente de ceniza", 8, new JsonObject
    {
        ["savingThrows"] = new JsonArray("wis", "con"),
        ["multiclassing"] = new JsonObject { ["prerequisites"] = new JsonObject { ["wis"] = 13 } },
        ["spellcasting"] = new JsonObject
        {
            ["ability"] = "wis",
            ["progression"] = "hitPoints",
            ["preparation"] = "prepared",
            ["preparedFormula"] = "mod:wis+halfClassLevel",
            ["maxSpellLevel"] = new JsonObject { ["1"] = 1, ["5"] = 2, ["9"] = 3, ["13"] = 4, ["17"] = 5 },
            ["usesResource"] = I("plegarias"),
            ["hitPointCost"] = new JsonArray(5, 10, 20, 30, 45),
            ["cantripsKnown"] = new JsonObject { ["1"] = 2, ["10"] = 3 },
        },
        ["spellList"] = new JsonArray(new JsonObject { ["class"] = "cleric" }),
        ["resources"] = new JsonArray(new JsonObject
        {
            ["key"] = I("plegarias"),
            ["name"] = "Plegarias",
            ["max"] = new JsonObject { ["byLevel"] = new JsonObject { ["1"] = 2, ["5"] = 5, ["10"] = 8, ["20"] = 15 } },
            ["recharge"] = "LongRest",
        }),
    });

    /// <summary>
    /// H02: "Escriba de rituales" casts only rituals of its book: four rituals of any list at level 1 and one wizard
    /// ritual per level, treating one non-ritual spell as a ritual.
    /// </summary>
    private static JsonObject Scribe() => Class("escriba", "Escriba de rituales", 8, new JsonObject
    {
        ["savingThrows"] = new JsonArray("int", "wis"),
        ["spellcasting"] = new JsonObject
        {
            ["ability"] = "int",
            ["progression"] = "ritualsOnly",
            ["ritual"] = true,
            ["maxSpellLevel"] = new JsonObject { ["1"] = 1, ["5"] = 2, ["9"] = 3 },
            ["treatAsRitual"] = new JsonArray("mage-armor"),
        },
        ["levelChoices"] = new JsonArray(
            new JsonObject
            {
                ["level"] = 1,
                ["key"] = "rituales",
                ["name"] = "Rituales",
                ["kind"] = "SpellbookSpells",
                ["choose"] = 4,
                ["filter"] = new JsonObject { ["spellList"] = "any", ["ritualOnly"] = true, ["spellLevels"] = new JsonArray(1) },
            },
            new JsonObject
            {
                ["level"] = 2,
                ["key"] = "rituales-2",
                ["name"] = "Rituales",
                ["kind"] = "SpellbookSpells",
                ["choose"] = 1,
                ["filter"] = new JsonObject { ["spellList"] = "wizard", ["ritualOnly"] = true, ["maxSpellLevelBySlots"] = true },
            }),
    });

    /// <summary>H03: "Forjador", a non-caster with a Pact Magic order and an order with its own slot table.</summary>
    private static JsonObject Smith() => Class("forjador", "Forjador", 10, new JsonObject
    {
        ["subclassFlavor"] = "Taller",
        ["subclassLevel"] = 3,
        ["subclasses"] = new JsonArray(
            new JsonObject
            {
                ["index"] = I("taller-pacto"),
                ["name"] = "Taller del pacto",
                ["description"] = new JsonArray("Subclase ficticia."),
                ["spellcasting"] = new JsonObject { ["progression"] = "pact", ["ability"] = "int", ["fromLevel"] = 3, ["spellList"] = "warlock", ["spellsKnown"] = new JsonObject { ["3"] = 2 } },
            },
            new JsonObject
            {
                ["index"] = I("taller-tabla"),
                ["name"] = "Taller de la tabla",
                ["description"] = new JsonArray("Subclase ficticia."),
                ["spellcasting"] = new JsonObject
                {
                    ["progression"] = "table",
                    ["ability"] = "int",
                    ["fromLevel"] = 3,
                    ["spellList"] = "wizard",
                    ["spellsKnown"] = new JsonObject { ["3"] = 2 },
                    ["slots"] = new JsonObject { ["3"] = new JsonArray(2), ["7"] = new JsonArray(0, 3) },
                },
            }),
    });

    /// <summary>H04: "Vigía arcano" only knows cantrips; its order adds third-caster slots from level 3.</summary>
    private static JsonObject Lookout() => Class("vigia", "Vigía arcano", 8, new JsonObject
    {
        ["spellcastingAbility"] = "int",
        ["multiclassSpellcasting"] = "none",
        ["spellList"] = new JsonArray(new JsonObject { ["class"] = "wizard" }),
        ["levels"] = Levels(n => new JsonObject { ["level"] = n, ["cantripsKnown"] = n < 10 ? 2 : 3 }),
        ["subclassFlavor"] = "Torre",
        ["subclassLevel"] = 3,
        ["subclasses"] = new JsonArray(new JsonObject
        {
            ["index"] = I("torre-tercio"),
            ["name"] = "Torre del tercio",
            ["description"] = new JsonArray("Subclase ficticia."),
            ["spellcasting"] = new JsonObject { ["progression"] = "third", ["ability"] = "int", ["fromLevel"] = 3, ["spellList"] = "wizard", ["spellsKnown"] = new JsonObject { ["3"] = 3, ["4"] = 4 } },
        }),
    });

    /// <summary>H14: "Iniciado del velo", an auxiliary level for casters that counts as a caster level for slots.</summary>
    private static JsonObject Veil() => Class("velo", "Iniciado del velo", 6, new JsonObject
    {
        ["auxiliary"] = true,
        ["prerequisites"] = new JsonObject { ["classFeature"] = "spellcasting" },
        ["countsAsCasterLevel"] = true,
        ["multiclassing"] = new JsonObject { ["proficiencies"] = new JsonObject { ["tools"] = new JsonArray("Kit de ejemplo") } },
        ["levels"] = new JsonArray(new JsonObject
        {
            ["level"] = 1,
            ["features"] = new JsonArray(new JsonObject { ["index"] = I("velo-tenue"), ["name"] = "Velo tenue", ["description"] = new JsonArray("Texto de ejemplo.") }),
        }),
    });

    /// <summary>H14: "Domador de ecos", an auxiliary level from total level 3 with a narrative prerequisite.</summary>
    private static JsonObject Tamer() => Class("domador", "Domador de ecos", 10, new JsonObject
    {
        ["auxiliary"] = true,
        ["prerequisites"] = new JsonObject { ["characterLevel"] = 3 },
        ["prerequisitesText"] = "Nivel 3 y haber escuchado un eco (texto ficticio).",
        ["levels"] = new JsonArray(new JsonObject { ["level"] = 1 }),
    });

    private static JsonObject Tactics() => new()
    {
        ["setId"] = I("tacticas"),
        ["name"] = "Tácticas",
        ["options"] = new JsonArray(Option("tactica-niebla", "Táctica de niebla", null), Option("tactica-oculta", "Táctica oculta", null)),
    };

    /// <summary>
    /// H08/H09: "Guardián de faros", with a "Señales" choice at levels 2, 5 and 9 and two orders at level 3, one of
    /// which needs Strength 15.
    /// </summary>
    private static JsonObject Keeper() => Class("guardian", "Guardián de faros", 10, new JsonObject
    {
        ["subclassFlavor"] = "Orden",
        ["subclassLevel"] = 3,
        ["levelChoices"] = new JsonArray(
            new JsonObject { ["level"] = 2, ["key"] = "senales", ["name"] = "Señales", ["kind"] = "OptionSet", ["setId"] = I("senales"), ["choose"] = 1 },
            new JsonObject { ["level"] = 5, ["key"] = "senales-5", ["name"] = "Señales", ["kind"] = "OptionSet", ["setId"] = I("senales"), ["choose"] = 1 },
            new JsonObject { ["level"] = 9, ["key"] = "senales-9", ["name"] = "Señales", ["kind"] = "OptionSet", ["setId"] = I("senales"), ["choose"] = 1 },
            // H10: the ability improvement of level 4 may be swapped for a signal.
            new JsonObject { ["level"] = 4, ["key"] = "asi", ["name"] = "Mejora o señal", ["kind"] = "AsiOrFeat", ["choose"] = 1, ["alsoFrom"] = new JsonArray(I("senales")) }),
        ["subclasses"] = new JsonArray(
            new JsonObject
            {
                ["index"] = I("orden-llama"),
                ["name"] = "Orden de la Llama",
                ["description"] = new JsonArray("Subclase ficticia."),
                // H11: the order grants a signal without spending a choice.
                ["levels"] = new JsonArray(new JsonObject
                {
                    ["level"] = 3,
                    ["features"] = new JsonArray(new JsonObject { ["index"] = I("llama-guia"), ["name"] = "Llama guía", ["description"] = new JsonArray("Texto de ejemplo.") }),
                    ["grants"] = new JsonObject { ["options"] = new JsonArray(I("farolillo")) },
                }),
            },
            new JsonObject
            {
                ["index"] = I("orden-yunque"),
                ["name"] = "Orden del Yunque",
                ["description"] = new JsonArray("Subclase ficticia."),
                ["prerequisites"] = new JsonObject { ["abilities"] = new JsonObject { ["str"] = 15 } },
            }),
    });

    /// <summary>Options with the prerequisites of H08: another option, a subclass, a class feature, alternatives, level, size...</summary>
    private static JsonObject Signals() => new()
    {
        ["setId"] = I("senales"),
        ["name"] = "Señales",
        ["options"] = new JsonArray(
            Option("senal-basica", "Señal básica", null),
            Option("senal-doble", "Señal doble", new JsonObject { ["options"] = new JsonArray(I("senal-basica")) }, new JsonObject { ["options"] = new JsonArray(I("destello")) }),
            Option("faro-del-yunque", "Faro del yunque", new JsonObject { ["subclasses"] = new JsonArray(I("orden-yunque")) }),
            Option("luz-arcana", "Luz arcana", new JsonObject { ["classFeature"] = "spellcasting" }),
            Option("destello", "Destello", new JsonObject
            {
                ["anyOf"] = new JsonArray(
                    new JsonObject { ["abilities"] = new JsonObject { ["int"] = 14 } },
                    new JsonObject { ["abilities"] = new JsonObject { ["wis"] = 14 } }),
            }),
            Option("veterano", "Veterano del faro", new JsonObject { ["characterLevel"] = 5 }),
            Option("foco-firme", "Foco firme", new JsonObject { ["spellcastingAbilityMin"] = 15 }),
            Option("farolillo", "Farolillo", new JsonObject { ["sizes"] = new JsonArray("small") }),
            Option("del-heraldo", "Eco del heraldo", new JsonObject { ["classes"] = new JsonArray(I("heraldo")) }),
            Choir()),
    };

    /// <summary>
    /// H12/H16: "Señal de coro" asks for a tactic and a skill when taken, and for another skill at class level 5.
    /// </summary>
    private static JsonObject Choir()
    {
        var option = Option("senal-coro", "Señal de coro", null);
        option["choices"] = new JsonArray(
            new JsonObject { ["key"] = "tactica", ["name"] = "Táctica del coro", ["kind"] = "OptionSet", ["setId"] = I("tacticas"), ["choose"] = 1 },
            new JsonObject { ["key"] = "habilidad", ["name"] = "Habilidad del coro", ["kind"] = "Skill", ["choose"] = 1, ["from"] = new JsonArray("arcana", "stealth") });
        option["levelChoices"] = new JsonArray(
            new JsonObject { ["level"] = 5, ["key"] = "coro-5", ["name"] = "Voz del coro", ["kind"] = "Skill", ["choose"] = 1, ["from"] = new JsonArray("religion", "nature") });
        return option;
    }

    /// <summary>H12: a feat that asks for a saving throw and an exotic melee weapon.</summary>
    private static JsonArray Feats() =>
    [
        new JsonObject
        {
            ["index"] = I("vigia-del-eco"),
            ["name"] = "Vigía del eco",
            ["description"] = new JsonArray("Dote ficticia."),
            ["choices"] = new JsonArray(
                new JsonObject { ["key"] = "salvacion", ["name"] = "Salvación", ["kind"] = "SavingThrow", ["choose"] = 1 },
                new JsonObject
                {
                    ["key"] = "arma",
                    ["name"] = "Arma exótica",
                    ["kind"] = "Weapon",
                    ["choose"] = 1,
                    ["filter"] = new JsonObject { ["weaponCategories"] = new JsonArray("exotic"), ["firearms"] = false },
                }),
        },
        // H13/H15: a feat only for origin choices and a starter feat.
        new JsonObject { ["index"] = I("lazo-de-sangre"), ["name"] = "Lazo de sangre", ["creationOnly"] = true, ["description"] = new JsonArray("Dote ficticia.") },
        new JsonObject
        {
            ["index"] = I("chispa-temprana"),
            ["name"] = "Chispa temprana",
            ["starter"] = true,
            ["description"] = new JsonArray("Dote ficticia."),
            ["grants"] = new JsonObject { ["skills"] = new JsonArray("perception") },
        },
    ];

    /// <summary>H13: heritage feats of the "Medio vacío" race (an option set of their own).</summary>
    private static JsonObject Heritages() => new()
    {
        ["setId"] = I("herencias"),
        ["name"] = "Herencias",
        ["options"] = new JsonArray(
            Option("herencia-niebla", "Herencia de niebla", null, new JsonObject { ["skills"] = new JsonArray("stealth") }),
            Option("herencia-ceniza", "Herencia de ceniza", null)),
    };

    /// <summary>
    /// H13: "Medio vacío" picks one heritage feat; H13b: "Eco" grants a spell at level 3 and its subrace "Eco temprano"
    /// the same spell at level 1.
    /// </summary>
    private static JsonArray Races() =>
    [
        new JsonObject
        {
            ["index"] = I("medio-vacio"),
            ["name"] = "Medio vacío",
            ["speed"] = 30,
            ["size"] = "Medium",
            ["languages"] = new JsonArray("Common"),
            ["choices"] = new JsonObject
            {
                ["abilityBonuses"] = new JsonObject { ["choose"] = 2, ["amount"] = 1 },
                ["feats"] = new JsonObject { ["choose"] = 1, ["setId"] = I("herencias") },
            },
        },
        new JsonObject
        {
            ["index"] = I("eco"),
            ["name"] = "Eco",
            ["speed"] = 30,
            ["size"] = "Medium",
            ["languages"] = new JsonArray("Common"),
            ["grants"] = new JsonObject
            {
                ["spells"] = new JsonArray(new JsonObject { ["index"] = "cure-wounds", ["minLevel"] = 3 }),
                ["spellcastingAbility"] = "wis",
            },
            ["subraces"] = new JsonArray(new JsonObject
            {
                ["index"] = I("eco-temprano"),
                ["name"] = "Eco temprano",
                ["grants"] = new JsonObject
                {
                    ["spells"] = new JsonArray(new JsonObject { ["index"] = "cure-wounds", ["minLevel"] = 1 }),
                    ["spellcastingAbility"] = "wis",
                },
            }),
        },
    ];

    private static JsonObject Option(string index, string name, JsonObject? prerequisites, JsonObject? grants = null)
    {
        var option = new JsonObject { ["index"] = I(index), ["name"] = name, ["description"] = new JsonArray("Opción ficticia.") };
        if (prerequisites is not null)
        {
            option["prerequisites"] = prerequisites;
        }

        if (grants is not null)
        {
            option["grants"] = grants;
        }

        return option;
    }

    private static JsonArray Items() =>
    [
        new JsonObject
        {
            ["index"] = I("guadana-doble"),
            ["name"] = "Guadaña doble",
            ["costCp"] = 3000,
            ["weightLb"] = 8,
            ["weapon"] = new JsonObject { ["category"] = "exotic", ["range"] = "melee", ["damage"] = "1d10", ["damageType"] = "slashing", ["properties"] = new JsonArray("two-handed") },
        },
        new JsonObject
        {
            ["index"] = I("petardo"),
            ["name"] = "Petardo del vacío",
            ["costCp"] = 500,
            ["weightLb"] = 1,
            ["weapon"] = new JsonObject { ["category"] = "simple", ["range"] = "ranged", ["damage"] = "2d6", ["damageType"] = "fire", ["properties"] = new JsonArray("thrown", "bomb"), ["rangeNormal"] = 20, ["rangeLong"] = 60 },
        },
        new JsonObject
        {
            ["index"] = I("trabuco"),
            ["name"] = "Trabuco de bruma",
            ["costCp"] = 20000,
            ["weightLb"] = 6,
            ["weapon"] = new JsonObject { ["category"] = "exotic", ["range"] = "ranged", ["damage"] = "1d12", ["damageType"] = "piercing", ["properties"] = new JsonArray("loading"), ["rangeNormal"] = 40, ["rangeLong"] = 120 },
            ["firearm"] = new JsonObject { ["reload"] = 1, ["misfire"] = 3 },
        },
        new JsonObject
        {
            ["index"] = I("coraza-de-runas"),
            ["name"] = "Coraza de runas",
            ["costCp"] = 40000,
            ["weightLb"] = 30,
            ["armor"] = new JsonObject { ["category"] = "medium", ["baseAc"] = 15, ["exotic"] = true },
        },
    ];
}
