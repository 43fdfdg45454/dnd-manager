using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database for the format extensions the SRD needed as a base pack.</summary>
public sealed class BasePackFormatExtensionsApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// The format-3 fields added when the SRD became a content pack (phase 34C) work in any pack: attribution, shared
/// traits cited by index, per-level spellcasting tables, features outside the levels, item contents, equipment
/// categories with items, multi-part spell damage, healing, breath weapons, option names and hit point rolls. A fictitious
/// pack ("Molde de ejemplo") covers them; the skills stay reserved to the base pack.
/// </summary>
public class BasePackFormatExtensionsTests(BasePackFormatExtensionsApiFactory factory) : IClassFixture<BasePackFormatExtensionsApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string PackId = "molde-ejemplo";

    [Fact]
    public async Task A_pack_with_the_base_pack_extensions_imports_them()
    {
        var admin = await factory.CreateAdminClientAsync();
        var response = await admin.PostAsync(PacksUrl, Json(Pack()));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        await factory.WithDbAsync(async db =>
        {
            var trait = await db.Set<TraitDefinition>().SingleAsync(x => x.Index == "molde-ejemplo-piel-de-roca");
            Assert.Equal(["molde-ejemplo-gente-de-roca"], trait.RaceIndexes);
            Assert.Equal(["molde-ejemplo-roca-alta"], trait.SubraceIndexes);
            Assert.Equal(PackId, trait.Source);
            var race = await db.Set<RaceDefinition>().SingleAsync(x => x.Index == "molde-ejemplo-gente-de-roca");
            Assert.Equal(["molde-ejemplo-piel-de-roca", "molde-ejemplo-mirada"], race.TraitIndexes);
            Assert.Equal(["molde-ejemplo-piel-de-roca"], (await db.Set<SubraceDefinition>().SingleAsync(x => x.Index == "molde-ejemplo-roca-alta")).TraitIndexes);
            Assert.Equal(new OriginOption("str", "FUE"), race.Choices.AbilityBonuses!.From[0]);
            Assert.Equal("con", race.Choices.AbilityBonuses.From[1].Index);
            var vein = race.Choices.TraitOptions.Single().Options.Single();
            Assert.Equal(("fire", "dex", "3d6"), (vein.DamageType, vein.BreathWeapon!.SaveAbility, vein.BreathWeapon.DiceAt(7)));

            var category = await db.Set<EquipmentCategory>().SingleAsync(x => x.Index == "molde-ejemplo-cinceles");
            Assert.Equal(["molde-ejemplo-cincel", "hammer"], category.ItemIndexes);
            Assert.Equal(PackId, category.Source);

            var twin = await db.Set<SpellDefinition>().SingleAsync(x => x.Index == "molde-ejemplo-doble-golpe");
            Assert.Equal(2, JsonNode.Parse(twin.DamageJson!)!.AsArray().Count);
            Assert.Equal(SpellCategory.Damage, twin.Category);
            var relief = await db.Set<SpellDefinition>().SingleAsync(x => x.Index == "molde-ejemplo-alivio");
            Assert.Equal("2d4 + MOD", JsonNode.Parse(relief.HealJson!)!["2"]!.GetValue<string>());
            Assert.Equal(SpellCategory.Healing, relief.Category);

            var mason = await db.Set<ClassDefinition>().SingleAsync(x => x.Index == "molde-ejemplo-cantero");
            Assert.Equal(("wis", true, null), (mason.SpellcastingAbility, mason.IsSpellcaster, mason.SpellcastingJson));
            var third = await db.Set<ClassLevel>().SingleAsync(x => x.Index == "molde-ejemplo-cantero-3");
            Assert.Equal([4, 0, 0, 0, 0, 0, 0, 0, 0], third.SpellSlots);
            Assert.Equal((2, 4), (third.CantripsKnown, third.SpellsKnown));
            var hidden = await db.Set<FeatureDefinition>().SingleAsync(x => x.Index == "molde-ejemplo-golpe-firme");
            Assert.Equal(("molde-ejemplo-cantero", 5), (hidden.ClassIndex, hidden.Level));
            Assert.DoesNotContain(await db.Set<ClassLevel>().Where(x => x.ClassIndex == "molde-ejemplo-cantero").ToListAsync(), l => l.FeatureIndexes.Contains("molde-ejemplo-golpe-firme"));
        });

        var beast = await admin.GetFromJsonAsync<BeastDto>("/api/v1/systems/dnd5e/catalog/beasts/molde-ejemplo-topo");
        Assert.Equal((5, "2d4"), (beast!.HitPoints, beast.HitPointsRoll));
    }

    [Fact]
    public async Task Skills_are_reserved_to_the_base_pack_and_cited_traits_must_exist()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Pack("molde-erroneo");
        pack["skills"] = JsonNode.Parse("""[{ "index": "molde-erroneo-tallar", "name": "Tallar", "ability": "dex" }]""");
        pack["races"]![0]!["traits"]!.AsArray().Add("molde-erroneo-no-existe");
        pack["classes"]![0]!["levels"]![0]!["features"] = JsonNode.Parse("""[{ "index": "molde-erroneo-x", "name": "X", "level": 1 }]""");
        pack["classes"]![0]!.AsObject().Remove("spellcastingAbility");
        pack["classes"]![0]!.AsObject().Remove("multiclassSpellcasting");

        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
        Assert.Contains(errors, e => e.StartsWith("skills:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("races[0].traits[2]:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("classes[0].levels[0].features[0].level:", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("classes[0].levels[0]:", StringComparison.Ordinal) && e.Contains("spellcastingAbility", StringComparison.Ordinal));
    }

    private static StringContent Json(JsonNode pack) => new(pack.ToJsonString(), Encoding.UTF8, "application/json");

    /// <summary>The fictitious pack, with every index prefixed by <paramref name="id"/>.</summary>
    private static JsonObject Pack(string id = PackId)
    {
        var levels = new JsonArray();
        for (var level = 1; level <= 20; level++)
        {
            levels.Add(new JsonObject
            {
                ["level"] = level,
                ["features"] = level == 1 ? JsonNode.Parse($$"""[{ "index": "{{id}}-tallado", "name": "Tallado" }]""") : new JsonArray(),
                ["spellSlots"] = new JsonArray(Math.Min(level + 1, 4), level >= 5 ? 2 : 0),
                ["cantripsKnown"] = 2,
                ["spellsKnown"] = level + 1,
            });
        }

        var json = $$"""
        {
          "formatVersion": 3,
          "id": "{{id}}",
          "name": "Molde de ejemplo",
          "version": "1.0.0",
          "attribution": "Contenido ficticio de ejemplo.",
          "traits": [
            { "index": "{{id}}-piel-de-roca", "name": "Piel de roca", "description": ["Tu piel es dura."],
              "races": ["{{id}}-gente-de-roca"], "subraces": ["{{id}}-roca-alta"] }
          ],
          "races": [
            {
              "index": "{{id}}-gente-de-roca", "name": "Gente de roca", "speed": 25, "size": "Medium",
              "traits": ["{{id}}-piel-de-roca", { "index": "{{id}}-mirada", "name": "Mirada pétrea" }],
              "subraces": [{ "index": "{{id}}-roca-alta", "name": "Roca alta", "traits": ["{{id}}-piel-de-roca"] }],
              "choices": {
                "abilityBonuses": { "choose": 1, "amount": 1, "from": [{ "index": "str", "name": "FUE" }, "con"] },
                "traitOptions": [{
                  "key": "{{id}}-veta", "name": "Veta", "choose": 1,
                  "options": [{
                    "index": "{{id}}-veta-ardiente", "name": "Veta ardiente", "damageType": "fire",
                    "breathWeapon": { "name": "Aliento de veta", "area": "15 ft. cone", "save": "dex", "damageAtCharacterLevel": { "1": "2d6", "6": "3d6" } }
                  }]
                }]
              }
            }
          ],
          "reference": {
            "equipmentCategories": [{ "index": "{{id}}-cinceles", "name": "Cinceles", "items": ["{{id}}-cincel", "hammer"] }]
          },
          "items": [
            { "index": "{{id}}-cincel", "name": "Cincel", "category": "Tool" },
            { "index": "{{id}}-kit", "name": "Kit de cantero", "category": "AdventuringGear",
              "contents": [{ "item": "{{id}}-cincel", "quantity": 2 }, { "item": "hammer", "name": "Martillo" }] }
          ],
          "spells": [
            { "index": "{{id}}-doble-golpe", "name": "Doble golpe", "level": 1, "school": "evocation", "castingTime": "1 action",
              "range": "60 feet", "duration": "Instantaneous",
              "damage": [{ "type": "Fire", "atSlotLevel": { "1": "1d6" } }, { "type": "Radiant", "atSlotLevel": { "1": "1d6" } }] },
            { "index": "{{id}}-alivio", "name": "Alivio", "level": 1, "school": "evocation", "castingTime": "1 action",
              "range": "Touch", "duration": "Instantaneous", "healAtSlotLevel": { "1": "1d4 + MOD", "2": "2d4 + MOD" } }
          ],
          "classes": [
            {
              "index": "{{id}}-cantero", "name": "Cantero", "hitDie": 8,
              "spellcastingAbility": "wis", "multiclassSpellcasting": "half",
              "features": [{ "index": "{{id}}-golpe-firme", "name": "Golpe firme", "level": 5 }]
            }
          ],
          "creatures": [
            { "index": "{{id}}-topo", "name": "Topo de ejemplo", "size": "Tiny", "type": "beast", "armorClass": 10,
              "hitPoints": 5, "hitDice": "2d4", "hitPointsRoll": "2d4", "challengeRating": 0 }
          ]
        }
        """;
        var pack = JsonNode.Parse(json)!.AsObject();
        pack["classes"]![0]!["levels"] = levels;
        return pack;
    }
}
