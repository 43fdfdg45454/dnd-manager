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
        ["classes"] = new JsonArray(Herald(), Keeper()),
        ["optionSets"] = new JsonArray(Signals()),
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
    });

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
            new JsonObject { ["level"] = 9, ["key"] = "senales-9", ["name"] = "Señales", ["kind"] = "OptionSet", ["setId"] = I("senales"), ["choose"] = 1 }),
        ["subclasses"] = new JsonArray(
            new JsonObject
            {
                ["index"] = I("orden-llama"),
                ["name"] = "Orden de la Llama",
                ["description"] = new JsonArray("Subclase ficticia."),
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
            Option("senal-doble", "Señal doble", new JsonObject { ["options"] = new JsonArray(I("senal-basica")) }),
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
            Option("del-heraldo", "Eco del heraldo", new JsonObject { ["classes"] = new JsonArray(I("heraldo")) })),
    };

    private static JsonObject Option(string index, string name, JsonObject? prerequisites)
    {
        var option = new JsonObject { ["index"] = I(index), ["name"] = name, ["description"] = new JsonArray("Opción ficticia.") };
        if (prerequisites is not null)
        {
            option["prerequisites"] = prerequisites;
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
