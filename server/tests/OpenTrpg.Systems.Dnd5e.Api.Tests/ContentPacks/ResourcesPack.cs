using System.Text.Json.Nodes;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>
/// Fictitious packs of phase 35B3 (resources, granted spells, tags and repeatable feats): a fighter subclass with a
/// resource whose maximum adds a table and a formula, a resource that recharges on initiative, one with a recharge die
/// and a bonus to the maximum of the first; a race with a trait resource that loses a skill and is immune to a condition
/// of the required pack; feats that grant spells with their own ability, can be taken again or ask which saving throw is
/// lost; a spell with a tag and a tag vocabulary that tags SRD spells; a wizard subclass that expands its list by tag.
/// </summary>
public static class ResourcesPack
{
    public const string Id = "recursos-ejemplo";

    /// <summary>The required pack: a condition and nothing else.</summary>
    public const string BaseId = "recursos-base";

    public static string I(string suffix) => $"{Id}-{suffix}";

    public static string Numb => $"{BaseId}-entumecido";

    public static JsonObject BuildBase() => new()
    {
        ["formatVersion"] = 3,
        ["id"] = BaseId,
        ["name"] = "Recursos base",
        ["version"] = "1.0.0",
        ["conditions"] = new JsonArray(new JsonObject { ["index"] = Numb, ["name"] = "Entumecido", ["description"] = new JsonArray("Estado ficticio.") }),
    };

    public static JsonObject Build() => new()
    {
        ["formatVersion"] = 3,
        ["id"] = Id,
        ["name"] = "Recursos de ejemplo",
        ["version"] = "1.0.0",
        ["requires"] = new JsonArray(BaseId),
        ["classes"] = new JsonArray(FighterExtension(), WizardExtension()),
        ["races"] = new JsonArray(StoneRace()),
        ["feats"] = new JsonArray(Spark(), Versatile(), Sacrifice()),
        ["spells"] = new JsonArray(Flash()),
        ["reference"] = new JsonObject
        {
            ["spellTags"] = new JsonArray(new JsonObject
            {
                ["index"] = I("luz"),
                ["name"] = "Luz",
                ["description"] = new JsonArray("Etiqueta ficticia."),
                ["spells"] = new JsonArray("light", "magic-missile"),
            }),
        },
    };

    private static JsonObject Feature(string index, string name, string key, JsonObject resourceOrModifiers)
    {
        var feature = new JsonObject
        {
            ["index"] = I(index),
            ["name"] = name,
            ["description"] = new JsonArray("Texto de ejemplo."),
        };
        feature[key] = resourceOrModifiers[key]!.DeepClone();
        return feature;
    }

    /// <summary>H34 and H35 on level 3 features of a fighter subclass; a level 4 spell choice filtered by tag (H38).</summary>
    private static JsonObject FighterExtension() => new()
    {
        ["extends"] = "fighter",
        ["subclasses"] = new JsonArray(new JsonObject
        {
            ["index"] = I("senda"),
            ["name"] = "Senda de ejemplo",
            ["description"] = new JsonArray("Subclase ficticia."),
            ["levels"] = new JsonArray(new JsonObject
            {
                ["level"] = 3,
                ["features"] = new JsonArray(
                    Feature("brio", "Brío", "resource", new JsonObject
                    {
                        ["resource"] = new JsonObject
                        {
                            ["key"] = I("brio"),
                            ["name"] = "Brío",
                            ["max"] = new JsonObject { ["formula"] = "mod:con", ["byLevel"] = new JsonObject { ["3"] = 2, ["6"] = 3 } },
                        },
                    }),
                    Feature("impulso", "Impulso", "resource", new JsonObject
                    {
                        ["resource"] = new JsonObject { ["key"] = I("impulso"), ["name"] = "Impulso", ["max"] = 1, ["recharge"] = "Initiative" },
                    }),
                    Feature("resuello", "Resuello", "resource", new JsonObject
                    {
                        ["resource"] = new JsonObject
                        {
                            ["key"] = I("resuello"),
                            ["name"] = "Resuello",
                            ["max"] = 1,
                            ["recharge"] = "Manual",
                            ["rechargeRoll"] = new JsonObject { ["dice"] = "d6", ["min"] = 5 },
                        },
                    }),
                    Feature("reserva", "Reserva", "modifiers", new JsonObject
                    {
                        ["modifiers"] = new JsonArray(new JsonObject { ["kind"] = "ResourceMaxBonus", ["target"] = I("brio"), ["value"] = 1 }),
                    })),
            }),
            ["levelChoices"] = new JsonArray(new JsonObject
            {
                ["level"] = 4,
                ["key"] = "chispazo",
                ["name"] = "Chispazo",
                ["kind"] = "SpellsKnown",
                ["choose"] = 1,
                ["filter"] = new JsonObject { ["spellList"] = "any", ["spellLevels"] = new JsonArray(1), ["tags"] = new JsonArray(I("luz")) },
            }),
        }),
    };

    /// <summary>H38: a wizard subclass that adds every spell with the tag to its list.</summary>
    private static JsonObject WizardExtension() => new()
    {
        ["extends"] = "wizard",
        ["subclasses"] = new JsonArray(new JsonObject
        {
            ["index"] = I("lumen"),
            ["name"] = "Escuela de ejemplo",
            ["description"] = new JsonArray("Subclase ficticia."),
            ["expandedSpellList"] = new JsonArray(new JsonObject { ["tags"] = new JsonArray(I("luz")) }),
        }),
    };

    /// <summary>H36 (trait resource) and the 35B2 leftovers: a lost skill and a condition of the required pack.</summary>
    private static JsonObject StoneRace() => new()
    {
        ["index"] = I("petreo"),
        ["name"] = "Pétreo",
        ["speed"] = 30,
        ["size"] = "Medium",
        ["languages"] = new JsonArray("Common"),
        ["traits"] = new JsonArray(new JsonObject
        {
            ["index"] = I("tenacidad"),
            ["name"] = "Tenacidad",
            ["description"] = new JsonArray("Rasgo ficticio."),
            ["resource"] = new JsonObject { ["key"] = I("tenacidad"), ["name"] = "Tenacidad", ["max"] = "proficiencyBonus" },
        }),
        ["grants"] = new JsonObject
        {
            ["removeSkills"] = new JsonArray("athletics"),
            ["conditionImmunities"] = new JsonArray(Numb),
        },
    };

    /// <summary>H37: a feat that grants a spell cast with its own ability, once per long rest, and a cantrip.</summary>
    private static JsonObject Spark() => new()
    {
        ["index"] = I("chispa"),
        ["name"] = "Chispa arcana",
        ["description"] = new JsonArray("Dote ficticia."),
        ["grants"] = new JsonObject
        {
            ["cantrips"] = new JsonArray("light"),
            ["spells"] = new JsonArray(new JsonObject { ["index"] = "magic-missile", ["usesPerLongRest"] = 1 }),
            ["spellcastingAbility"] = "int",
        },
    };

    /// <summary>H39: a feat that can be taken more than once.</summary>
    private static JsonObject Versatile() => new()
    {
        ["index"] = I("versatil"),
        ["name"] = "Versátil",
        ["description"] = new JsonArray("Dote ficticia."),
        ["repeatable"] = true,
        ["modifiers"] = new JsonArray(new JsonObject { ["kind"] = "InitiativeBonus", ["value"] = 1 }),
    };

    /// <summary>35B2 leftover: a level-up feat that asks which saving throw is lost.</summary>
    private static JsonObject Sacrifice() => new()
    {
        ["index"] = I("sacrificio"),
        ["name"] = "Sacrificio",
        ["description"] = new JsonArray("Dote ficticia."),
        ["grants"] = new JsonObject
        {
            ["resistances"] = new JsonArray("fire"),
            ["removeSavingThrows"] = new JsonObject { ["choose"] = 1, ["from"] = new JsonArray("str", "con") },
        },
    };

    /// <summary>H38: a spell of the pack with the tag.</summary>
    private static JsonObject Flash() => new()
    {
        ["index"] = I("destello"),
        ["name"] = "Destello de ejemplo",
        ["level"] = 1,
        ["school"] = "evocation",
        ["castingTime"] = "1 action",
        ["range"] = "30 feet",
        ["components"] = new JsonArray("V"),
        ["duration"] = "Instantaneous",
        ["description"] = new JsonArray("Conjuro ficticio."),
        ["tags"] = new JsonArray(I("luz")),
    };
}
