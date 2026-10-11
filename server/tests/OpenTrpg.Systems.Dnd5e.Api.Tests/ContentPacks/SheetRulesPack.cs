using System.Text.Json.Nodes;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>
/// Fictitious pack of phase 35B2 (sheet rules): a class with formula modifiers, its own armor class, critical range,
/// passive, ability maximum, speeds, death saves and hit dice; a sorcerer subclass with spell DC and attack bonuses; a
/// small race with defenses, a natural weapon and a lost saving throw; feats that take a saving throw away or ask for a
/// resistance; and firearms, double weapons, bombs and an item restricted to the class.
/// </summary>
public static class SheetRulesPack
{
    public const string Id = "reglas-ejemplo";

    public static string I(string suffix) => $"{Id}-{suffix}";

    public static JsonObject Build() => new()
    {
        ["formatVersion"] = 3,
        ["id"] = Id,
        ["name"] = "Reglas de ejemplo",
        ["version"] = "1.0.0",
        ["classes"] = new JsonArray(Lookout(), SorcererExtension()),
        ["races"] = new JsonArray(ScaledRace()),
        ["feats"] = new JsonArray(Renouncer(), Elemental()),
        ["items"] = new JsonArray(Pistol(), DoubleMace(), Grenade(), Amulet()),
    };

    private static JsonObject Feature(string index, string name, JsonArray modifiers) => new()
    {
        ["index"] = I(index),
        ["name"] = name,
        ["description"] = new JsonArray("Texto de ejemplo."),
        ["modifiers"] = modifiers,
    };

    private static JsonObject Modifier(string kind, JsonNode? value, string? target = null, string? condition = null)
    {
        var modifier = new JsonObject { ["kind"] = kind };
        if (target is not null)
        {
            modifier["target"] = target;
        }

        if (value is not null)
        {
            modifier["value"] = value;
        }

        if (condition is not null)
        {
            modifier["condition"] = condition;
        }

        return modifier;
    }

    /// <summary>H21, H22, H24, H25 (class DC), H26, H27, H29, H30b and H32 on level 1 features.</summary>
    private static JsonObject Lookout()
    {
        var bark = Modifier("ArmorClassFormula", 10, condition: "noArmor");
        bark["abilities"] = new JsonArray("dex", "wis");
        return new JsonObject
        {
            ["index"] = I("vigia"),
            ["name"] = "Vigía de ejemplo",
            ["hitDie"] = 10,
            ["savingThrows"] = new JsonArray("str", "con"),
            ["proficiencies"] = new JsonObject { ["armor"] = new JsonArray(), ["weapons"] = new JsonArray("Simple Weapons"), ["tools"] = new JsonArray() },
            ["skillChoices"] = new JsonObject { ["choose"] = 2, ["from"] = new JsonArray("athletics", "perception", "insight") },
            ["startingEquipmentText"] = "Texto de ejemplo.",
            ["saveDc"] = new JsonObject { ["name"] = "CD de ejemplo", ["ability"] = "wis" },
            ["subclassFlavor"] = "Senda",
            ["subclassLevel"] = 3,
            ["description"] = new JsonArray("Clase ficticia."),
            ["levels"] = new JsonArray([new JsonObject
            {
                ["level"] = 1,
                ["features"] = new JsonArray(
                    Feature("piel", "Piel de corteza", new JsonArray(bark)),
                    Feature("aguante", "Aguante", new JsonArray(Modifier("HitPointsMaxBonus", "characterLevel"))),
                    Feature("ojo", "Ojo certero", new JsonArray(Modifier("CriticalRange", 19, "weapon"), Modifier("PassiveBonus", 5, "investigation"))),
                    Feature("forma", "Forma anfibia", new JsonArray(Modifier("AbilityMax", 22, "con"), Modifier("Speed", "walk", "swim"), Modifier("Speed", 20, "fly"))),
                    Feature("terquedad", "Terquedad", new JsonArray(Modifier("DeathSaveFailures", 4), Modifier("HitDiceRecovery", null, "all")))),
            }, .. Enumerable.Range(2, 19).Select(level => new JsonObject { ["level"] = level })]),
        };
    }

    /// <summary>H24 (spells) and H25 (spell DC and attack, cantrips only).</summary>
    private static JsonObject SorcererExtension() => new()
    {
        ["extends"] = "sorcerer",
        ["subclasses"] = new JsonArray(new JsonObject
        {
            ["index"] = I("chispa"),
            ["name"] = "Chispa de ejemplo",
            ["description"] = new JsonArray("Texto de ejemplo."),
            ["levels"] = new JsonArray(new JsonObject
            {
                ["level"] = 1,
                ["features"] = new JsonArray(Feature("chispa-afilada", "Chispa afilada", new JsonArray(
                    Modifier("SpellSaveDcBonus", 1, condition: "cantrip"),
                    Modifier("SpellAttackBonus", 2, "sorcerer"),
                    Modifier("CriticalRange", 19, "spell")))),
            }),
        }),
    };

    /// <summary>H23, H28, H31c (fixed) and H33.</summary>
    private static JsonObject ScaledRace() => new()
    {
        ["index"] = I("escamado"),
        ["name"] = "Escamado",
        ["speed"] = 30,
        ["size"] = "Small",
        ["languages"] = new JsonArray("Common"),
        ["grants"] = new JsonObject
        {
            ["resistances"] = new JsonArray("fire"),
            ["immunities"] = new JsonArray("poison"),
            ["conditionImmunities"] = new JsonArray("charmed"),
            ["removeSavingThrows"] = new JsonObject { ["choose"] = 1, ["from"] = new JsonArray("str") },
            ["naturalWeapons"] = new JsonArray(new JsonObject
            {
                ["name"] = "Garras de ejemplo",
                ["damage"] = "1d4",
                ["damageByCharacterLevel"] = new JsonObject { ["5"] = "1d6" },
                ["damageType"] = "slashing",
                ["finesse"] = true,
            }),
        },
    };

    /// <summary>H31c (to choose) and H23: a starter feat that takes a saving throw and a class skill away.</summary>
    private static JsonObject Renouncer() => new()
    {
        ["index"] = I("renuncia"),
        ["name"] = "Renuncia de ejemplo",
        ["starter"] = true,
        ["description"] = new JsonArray("Dote ficticia."),
        ["grants"] = new JsonObject
        {
            ["resistances"] = new JsonArray("cold"),
            ["removeSavingThrows"] = new JsonObject { ["choose"] = 1, ["from"] = new JsonArray("str", "con") },
            ["skillChoicesDelta"] = -1,
        },
    };

    /// <summary>H23: a feat that asks for a resistance.</summary>
    private static JsonObject Elemental() => new()
    {
        ["index"] = I("elemental"),
        ["name"] = "Aguante elemental",
        ["description"] = new JsonArray("Dote ficticia."),
        ["choices"] = new JsonArray(new JsonObject
        {
            ["key"] = "elemento",
            ["name"] = "Elemento",
            ["kind"] = "Resistance",
            ["choose"] = 1,
            ["from"] = new JsonArray("cold", "fire", "lightning"),
        }),
    };

    /// <summary>H40: a firearm without the ability in its damage and with a Dexterity requirement.</summary>
    private static JsonObject Pistol() => new()
    {
        ["index"] = I("pistola"),
        ["name"] = "Pistola de ejemplo",
        ["costCp"] = 10000,
        ["weightLb"] = 3,
        ["description"] = new JsonArray("Arma de fuego ficticia."),
        ["weapon"] = new JsonObject
        {
            ["category"] = "martial",
            ["range"] = "ranged",
            ["damage"] = "1d8",
            ["damageType"] = "piercing",
            ["rangeNormal"] = 30,
            ["rangeLong"] = 90,
            ["requires"] = new JsonObject { ["dex"] = 16 },
        },
        ["firearm"] = new JsonObject { ["misfire"] = 1, ["era"] = "Renaissance" },
    };

    /// <summary>H40 and H07: an exotic heavy weapon with a second profile.</summary>
    private static JsonObject DoubleMace() => new()
    {
        ["index"] = I("maza-doble"),
        ["name"] = "Maza doble de ejemplo",
        ["costCp"] = 3000,
        ["weightLb"] = 8,
        ["description"] = new JsonArray("Arma ficticia."),
        ["weapon"] = new JsonObject
        {
            ["category"] = "exotic",
            ["range"] = "melee",
            ["damage"] = "1d8",
            ["damageType"] = "bludgeoning",
            ["properties"] = new JsonArray("heavy"),
            ["alternate"] = new JsonObject { ["name"] = "Maza doble (mango)", ["damage"] = "1d4", ["properties"] = new JsonArray("light") },
        },
    };

    /// <summary>H07: a bomb.</summary>
    private static JsonObject Grenade() => new()
    {
        ["index"] = I("granada"),
        ["name"] = "Granada de ejemplo",
        ["costCp"] = 500,
        ["weightLb"] = 1,
        ["description"] = new JsonArray("Bomba ficticia."),
        ["weapon"] = new JsonObject
        {
            ["category"] = "simple",
            ["range"] = "ranged",
            ["damage"] = "2d6",
            ["damageType"] = "fire",
            ["properties"] = new JsonArray("bomb", "thrown"),
            ["rangeNormal"] = 20,
            ["rangeLong"] = 60,
        },
    };

    /// <summary>H40: attunement restricted to the class of the pack.</summary>
    private static JsonObject Amulet() => new()
    {
        ["index"] = I("amuleto"),
        ["name"] = "Amuleto de ejemplo",
        ["category"] = "MagicItem",
        ["rarity"] = "Uncommon",
        ["requiresAttunement"] = true,
        ["attunementClasses"] = new JsonArray(I("vigia")),
        ["description"] = new JsonArray("Objeto ficticio."),
        ["modifiers"] = new JsonArray(new JsonObject { ["kind"] = "ArmorClassBonus", ["value"] = 1 }),
    };
}
