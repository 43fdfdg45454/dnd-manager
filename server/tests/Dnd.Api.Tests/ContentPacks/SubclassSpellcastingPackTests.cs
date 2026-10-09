using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dnd.Api.Tests.Items;
using Dnd.Application.Characters;

namespace Dnd.Api.Tests.ContentPacks;

/// <summary>Own database: the pack adds a fighter subclass that casts spells.</summary>
public sealed class SubclassSpellcastingPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// A fictitious fighter subclass with third-caster spellcasting, known spells by level and a school filter
/// (phase 25, block 3).
/// </summary>
public class SubclassSpellcastingPackTests(SubclassSpellcastingPackApiFactory factory) : IClassFixture<SubclassSpellcastingPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string Subclass = "runas-ejemplo-centinela";
    private const string SchoolsReason = "Solo abjuración o evocación salvo en los niveles 3 y 8";

    private static readonly object SchoolFilter = new
    {
        spellList = "wizard",
        maxSpellLevelBySlots = true,
        schools = new[] { "abjuration", "evocation" },
        schoolsExceptAt = new[] { 8, 3 },
    };

    private static JsonObject Pack() => JsonSerializer.SerializeToNode(new
    {
        formatVersion = 2,
        id = "runas-ejemplo",
        name = "Runas de Ejemplo",
        version = "1.0.0",
        classesExtended = new[]
        {
            new
            {
                classIndex = "fighter",
                subclasses = new[]
                {
                    new
                    {
                        index = Subclass,
                        name = "Centinela rúnico de ejemplo",
                        description = new[] { "Texto de ejemplo." },
                        spellcasting = new
                        {
                            progression = "third",
                            ability = "int",
                            fromLevel = 3,
                            spellList = "wizard",
                            cantripsKnown = new Dictionary<string, int> { ["3"] = 2, ["10"] = 3 },
                            spellsKnown = new Dictionary<string, int> { ["3"] = 3, ["4"] = 4, ["7"] = 5 },
                        },
                        levelChoices = new object[]
                        {
                            new { level = 3, key = "trucos", name = "Trucos rúnicos", kind = "CantripsKnown", filter = new { spellList = "wizard" } },
                            new { level = 3, key = "conjuros", name = "Conjuros rúnicos", kind = "SpellsKnown", filter = SchoolFilter },
                            new { level = 4, key = "conjuros", name = "Conjuros rúnicos", kind = "SpellsKnown", filter = SchoolFilter },
                        },
                    },
                },
            },
        },
    })!.AsObject();

    private async Task ImportAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        var list = await admin.GetStringAsync(PacksUrl);
        if (list.Contains("runas-ejemplo", StringComparison.Ordinal))
        {
            return;
        }

        var import = await admin.PostAsync(PacksUrl, Json(Pack()));
        Assert.True(import.StatusCode == HttpStatusCode.Created, await import.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_fighter_with_the_subclass_casts_as_a_third_caster_from_level_3()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Centinela");

        var second = await PatchAsync(s, hero.Id, [("fighter", null, 2)]);
        Assert.Empty(second.Sheet.Spellcasting);
        Assert.All(second.SpellSlots, slot => Assert.Equal(0, slot.Max));

        // Intelligence 14 (+2), proficiency +2.
        var third = await PatchAsync(s, hero.Id, [("fighter", Subclass, 3)]);
        var casting = Assert.Single(third.Sheet.Spellcasting);
        Assert.Equal(("fighter", "int", 12, 4, (int?)null), (casting.ClassIndex, casting.Ability, casting.SaveDc, casting.AttackBonus, casting.PreparedMax));
        Assert.Equal((3, 2), (casting.SpellsKnownMax, casting.CantripsKnownMax));
        Assert.Contains(third.Sheet.Breakdowns["spellSaveDc.fighter"].Parts, p => p is { Source: "ability", Label: "Inteligencia", Value: 2 });
        Assert.Contains(third.Sheet.Breakdowns["spellAttackBonus.fighter"].Parts, p => p is { Source: "ability", Label: "Inteligencia", Value: 2 });
        Assert.Equal(2, MaxSlots(third, 1));
        Assert.Equal(0, MaxSlots(third, 2));

        var tenth = await PatchAsync(s, hero.Id, [("fighter", Subclass, 10)]);
        Assert.Equal((4, 3), (MaxSlots(tenth, 1), MaxSlots(tenth, 2)));
        Assert.Equal((5, 3), (tenth.Sheet.Spellcasting[0].SpellsKnownMax, tenth.Sheet.Spellcasting[0].CantripsKnownMax));

        // Multiclass: fighter 3 contributes floor(3/3) = 1 to the caster level, wizard 1 another 1.
        var multiclass = await PatchAsync(s, hero.Id, [("fighter", Subclass, 3), ("wizard", null, 1)]);
        Assert.Equal(["fighter", "wizard"], multiclass.Sheet.Spellcasting.Select(c => c.ClassIndex));
        Assert.Equal(3, MaxSlots(multiclass, 1));

        // Another subclass is not a caster.
        var champion = await PatchAsync(s, hero.Id, [("fighter", "champion", 10)]);
        Assert.Empty(champion.Sheet.Spellcasting);
    }

    [Fact]
    public async Task The_planner_uses_the_known_tables_and_the_school_filter()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Aprendiz");
        await PatchAsync(s, hero.Id, [("fighter", null, 2)], player: true);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/activate", null)).StatusCode);
        await GrantAsync(s, hero.Id);

        // Level 3: the subclass is chosen now; its choices come without "choose" and take the table values. Level 3
        // is one of the exceptions, so any school is eligible.
        var plan3 = await PlanAsync(s.Player, hero.Id);
        var cantrips = Assert.Single(plan3.Choices, c => c.SubclassIndex == Subclass && c.Kind == "CantripsKnown");
        Assert.Equal(2, cantrips.Choose);
        Assert.Contains(cantrips.Options, o => o.Index == "fire-bolt" && o.Eligible);
        var spells3 = Assert.Single(plan3.Choices, c => c.SubclassIndex == Subclass && c.Kind == "SpellsKnown");
        Assert.Equal(3, spells3.Choose);
        Assert.All(spells3.Options, o => Assert.Equal(1, o.SpellLevel));
        Assert.True(spells3.Options.Single(o => o.Index == "charm-person").Eligible);

        // Level 4 with the subclass: one more spell, only abjuration or evocation.
        var veteran = await s.Player.CreateCharacterAsync(s.CampaignId, "Veterano");
        await PatchAsync(s, veteran.Id, [("fighter", Subclass, 3)], player: true);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(veteran.Id)}/activate", null)).StatusCode);
        await GrantAsync(s, veteran.Id);
        var plan4 = await PlanAsync(s.Player, veteran.Id);
        var spells4 = Assert.Single(plan4.Choices, c => c.Kind == "SpellsKnown");
        Assert.Equal((1, 1), (spells4.Choose, spells4.Required));
        Assert.True(spells4.Options.Single(o => o.Index == "magic-missile").Eligible);
        Assert.True(spells4.Options.Single(o => o.Index == "shield").Eligible);
        var charm = spells4.Options.Single(o => o.Index == "charm-person");
        Assert.False(charm.Eligible);
        Assert.Equal(SchoolsReason, charm.Reason);
        Assert.All(spells4.Options, o => Assert.Equal(1, o.SpellLevel));

        var casting = plan4.Spellcasting!;
        Assert.Equal(("fighter", "int", 4, 1), (casting.ClassIndex, casting.Ability, casting.SpellsKnown!.Value, casting.MaxSpellLevel));
        Assert.Equal(3, casting.SpellSlots[0]);

        // Choosing an enchantment spell is refused; an evocation is learned.
        object Body(string spell) => new
        {
            hitPointsRolled = 6,
            choices = new object[]
            {
                new { key = "asi", selected = new { asi = new { str = 2 } } },
                new { key = "conjuros", selected = new[] { spell } },

                // The level-3 cantrips of the subclass were never chosen (the sheet was edited to level 3): caught up now.
                new { key = "trucos", selected = new[] { "fire-bolt", "light" } },
            },
        };
        var refused = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(veteran.Id)}/level-up", Body("charm-person"));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.DoesNotContain("Falta", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var learned = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(veteran.Id)}/level-up", Body("magic-missile"));
        Assert.True(learned.StatusCode == HttpStatusCode.OK, await learned.Content.ReadAsStringAsync());
        var after = (await learned.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Contains(after.Spells, sp => sp.SpellIndex == "magic-missile" && sp.ClassIndex == "fighter");
        Assert.Equal(4, after.Sheet.Spellcasting.Single().SpellsKnownMax);
    }

    [Fact]
    public async Task Invalid_spellcasting_and_school_filters_are_reported()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Pack();
        pack["id"] = "runas-malas";
        var subclass = pack["classesExtended"]![0]!["subclasses"]![0]!.AsObject();
        subclass["index"] = "runas-malas-x";
        subclass["spellcasting"] = JsonNode.Parse("""{"progression":"quarter","ability":"luck","fromLevel":0,"spellList":"nadie","spellsKnown":{"0":2,"3":-1}}""");
        subclass["levelChoices"] = JsonNode.Parse("""
            [
              {"level":3,"key":"conjuros","name":"Conjuros","kind":"SpellsKnown","filter":{"schools":["fire"],"schoolsExceptAt":[21]}},
              {"level":3,"key":"estilos","name":"Estilos","kind":"Language"}
            ]
            """);

        var errors = await ImportErrorsAsync(admin, pack);

        var prefix = "classesExtended[0].subclasses[0]";
        foreach (var path in new[]
        {
            "spellcasting.progression", "spellcasting.ability", "spellcasting.fromLevel", "spellcasting.spellList",
            "spellcasting.spellsKnown.0", "spellcasting.spellsKnown.3",
            "levelChoices[0].choose", "levelChoices[0].filter.schools[0]", "levelChoices[0].filter.schoolsExceptAt[0]",
            "levelChoices[1].choose",
        })
        {
            Assert.Contains(errors, e => e.StartsWith($"{prefix}.{path}:", StringComparison.Ordinal));
        }

        // A class that already casts spells cannot take a subclass spellcasting.
        var wizard = Pack();
        wizard["id"] = "runas-mago";
        wizard["classesExtended"]![0]!["classIndex"] = "wizard";
        wizard["classesExtended"]![0]!["subclasses"]![0]!["index"] = "runas-mago-x";
        Assert.Contains(await ImportErrorsAsync(admin, wizard), e => e.StartsWith($"{prefix}.spellcasting:", StringComparison.Ordinal));
    }

    private static int MaxSlots(CharacterDetailDto detail, int level) => detail.SpellSlots.FirstOrDefault(s => s.Level == level)?.Max ?? 0;

    private static StringContent Json(JsonNode pack) => new(pack.ToJsonString(), Encoding.UTF8, "application/json");

    private static async Task<List<string>> ImportErrorsAsync(HttpClient admin, JsonNode pack)
    {
        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    private static async Task<CharacterDetailDto> PatchAsync(CampaignScenario s, Guid id, (string Class, string? Subclass, int Level)[] classes, bool player = false)
    {
        var client = player ? s.Player.Client : s.Dm.Client;
        var patch = await client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(id)}/sheet", new
        {
            classes = classes.Select(c => new { classIndex = c.Class, subclassIndex = c.Subclass, level = c.Level }).ToArray(),
            baseAbilities = new { str = 16, dex = 12, con = 14, @int = 14, wis = 10, cha = 8 },
            applyRacialBonuses = false,
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        return (await patch.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static async Task GrantAsync(CampaignScenario s, Guid characterId)
    {
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { characterId } });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
    }

    private static async Task<LevelUpPlanDto> PlanAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync($"{ItemTestHelpers.CharacterUrl(id)}/level-up");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LevelUpPlanDto>())!;
    }
}
