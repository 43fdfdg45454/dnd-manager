using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the pack adds subclasses with expanded spell lists.</summary>
public sealed class ExpandedSpellListPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Fictitious warlock patron and cleric domain whose expanded spell lists add SRD spells of other classes
/// (phase 25, block 4): they count as class spells only for the characters with the subclass and are not granted.
/// </summary>
public class ExpandedSpellListPackTests(ExpandedSpellListPackApiFactory factory) : IClassFixture<ExpandedSpellListPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string Patron = "pactos-ejemplo-hada";
    private const string PatronName = "Dama del Bosque de Ejemplo";
    private const string Domain = "pactos-ejemplo-dominio";

    private static JsonObject Pack() => JsonSerializer.SerializeToNode(new
    {
        formatVersion = 2,
        id = "pactos-ejemplo",
        name = "Pactos de Ejemplo",
        version = "1.0.0",
        classesExtended = new object[]
        {
            new
            {
                classIndex = "warlock",
                subclasses = new[]
                {
                    new
                    {
                        index = Patron,
                        name = PatronName,
                        description = new[] { "Texto de ejemplo." },
                        expandedSpellList = new[] { new { index = "faerie-fire", level = 1 }, new { index = "sleep", level = 1 } },
                    },
                },
            },
            new
            {
                classIndex = "cleric",
                subclasses = new[]
                {
                    new
                    {
                        index = Domain,
                        name = "Dominio de Ejemplo",
                        description = new[] { "Texto de ejemplo." },
                        expandedSpellList = new[] { new { index = "magic-missile", level = 1 } },
                    },
                },
            },
        },
    })!.AsObject();

    private async Task ImportAsync()
    {
        var admin = await factory.CreateAdminClientAsync();
        var list = await admin.GetStringAsync(PacksUrl);
        if (list.Contains("pactos-ejemplo", StringComparison.Ordinal))
        {
            return;
        }

        var import = await admin.PostAsync(PacksUrl, Json(Pack()));
        Assert.True(import.StatusCode == HttpStatusCode.Created, await import.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_warlock_with_the_patron_may_learn_the_expanded_spells_and_one_without_it_may_not()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();

        var withPatron = await LevelOneAsync(s, "warlock", Patron, "Brujo del bosque");
        var plan = await PlanAsync(s.Player, withPatron.Id);
        var spells = Assert.Single(plan.Choices, c => c.Kind == "SpellsKnown");
        Assert.True(spells.Options.Single(o => o.Index == "faerie-fire").Eligible);
        Assert.True(spells.Options.Single(o => o.Index == "sleep").Eligible);
        Assert.Contains(spells.Options, o => o.Index == "hellish-rebuke");
        Assert.DoesNotContain(spells.Options, o => o.Index == "magic-missile");

        // Not granted: the character does not know them until it picks one.
        var detail = await s.Player.GetCharacterAsync(withPatron.Id);
        Assert.DoesNotContain(detail.Spells, sp => sp.SpellIndex is "faerie-fire" or "sleep");

        var learned = await s.Player.Client.PostAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(withPatron.Id)}/level-up", new
        {
            hitPointsRolled = 5,
            choices = new object[]
            {
                new { key = "eldritch-invocations", selected = new[] { "eldritch-invocation-armor-of-shadows", "eldritch-invocation-beast-speech" } },
                new { key = "spells-known", selected = new[] { "faerie-fire" } },
            },
        });
        var body = await learned.Content.ReadAsStringAsync();
        Assert.True(learned.StatusCode == HttpStatusCode.OK, body);
        var after = JsonSerializer.Deserialize<CharacterDetailDto>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Contains(after.Spells, sp => sp.SpellIndex == "faerie-fire" && sp.ClassIndex == "warlock" && !sp.AlwaysPrepared);
        Assert.DoesNotContain(after.Spells, sp => sp.SpellIndex == "sleep");

        var fiend = await LevelOneAsync(s, "warlock", "fiend", "Brujo infernal");
        var fiendSpells = Assert.Single((await PlanAsync(s.Player, fiend.Id)).Choices, c => c.Kind == "SpellsKnown");
        Assert.DoesNotContain(fiendSpells.Options, o => o.Index is "faerie-fire" or "sleep");
    }

    [Fact]
    public async Task A_cleric_with_the_domain_may_prepare_the_expanded_spells()
    {
        await ImportAsync();
        var s = await factory.CreateCampaignScenarioAsync();

        var withDomain = await LevelOneAsync(s, "cleric", Domain, "Clériga de ejemplo");
        var candidates = Assert.Single((await PreparationAsync(s.Player, withDomain.Id)).Classes).Candidates;
        Assert.Contains(candidates, sp => sp.Index == "magic-missile");
        Assert.Contains(candidates, sp => sp.Index == "bless");
        Assert.DoesNotContain(candidates, sp => sp.Index is "faerie-fire" or "sleep");

        var prepared = await s.Player.Client.PostAsJsonAsync(
            $"{ItemTestHelpers.Dnd5eCharacterUrl(withDomain.Id)}/spell-preparation",
            new { classes = new[] { new { classIndex = "cleric", spells = new[] { "magic-missile", "bless" } } } });
        Assert.True(prepared.StatusCode == HttpStatusCode.OK, await prepared.Content.ReadAsStringAsync());

        var plain = await LevelOneAsync(s, "cleric", null, "Clérigo sin dominio");
        var plainCandidates = Assert.Single((await PreparationAsync(s.Player, plain.Id)).Classes).Candidates;
        Assert.DoesNotContain(plainCandidates, sp => sp.Index == "magic-missile");
    }

    [Fact]
    public async Task The_catalog_labels_the_spells_of_an_expanded_list()
    {
        await ImportAsync();
        var admin = await factory.CreateAdminClientAsync();

        var detail = (await admin.GetFromJsonAsync<SpellDetailDto>("/api/v1/systems/dnd5e/catalog/spells/faerie-fire"))!;
        var expansion = Assert.Single(detail.ExpandedBy);
        Assert.Equal((Patron, PatronName, "warlock", "pactos-ejemplo"), (expansion.SubclassIndex, expansion.SubclassName, expansion.ClassIndex, expansion.Source));
        Assert.DoesNotContain("warlock", detail.ClassIndexes);

        var page = (await admin.GetFromJsonAsync<PagedResult<SpellSummaryDto>>("/api/v1/systems/dnd5e/catalog/spells?level=1&pageSize=200"))!;
        Assert.Equal([Patron], page.Items.Single(sp => sp.Index == "sleep").ExpandedBy.Select(e => e.SubclassIndex));
        Assert.Equal([Domain], page.Items.Single(sp => sp.Index == "magic-missile").ExpandedBy.Select(e => e.SubclassIndex));
        Assert.Empty(page.Items.Single(sp => sp.Index == "bless").ExpandedBy);
    }

    [Fact]
    public async Task Wrong_levels_unknown_and_repeated_spells_are_reported()
    {
        var admin = await factory.CreateAdminClientAsync();
        var pack = Pack();
        pack["id"] = "pactos-malos";
        pack["classesExtended"]![0]!["subclasses"]![0]!["index"] = "pactos-malos-x";
        pack["classesExtended"]![0]!["subclasses"]![0]!["expandedSpellList"] = JsonNode.Parse("""
            [
              {"index":"faerie-fire","level":2},
              {"index":"conjuro-inexistente","level":1},
              {"index":"sleep","level":1},
              {"index":"sleep","level":1},
              {"index":"blur","level":10}
            ]
            """);
        pack["classesExtended"]![1]!["subclasses"]![0]!["index"] = "pactos-malos-y";

        var errors = await ImportErrorsAsync(admin, pack);

        var prefix = "classesExtended[0].subclasses[0].expandedSpellList";
        Assert.Contains(errors, e => e.StartsWith($"{prefix}[0].level:", StringComparison.Ordinal) && e.Contains("nivel 1", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"{prefix}[1].index:", StringComparison.Ordinal) && e.Contains("no existe", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"{prefix}[3].index:", StringComparison.Ordinal) && e.Contains("repetido", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith($"{prefix}[4].level:", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, e => e.StartsWith($"{prefix}[2]", StringComparison.Ordinal));

        // Format 1 packs cannot use it.
        var old = Pack();
        old["formatVersion"] = 1;
        old["id"] = "pactos-viejos";
        old["classesExtended"]![0]!["subclasses"]![0]!["index"] = "pactos-viejos-x";
        old["classesExtended"]![1]!["subclasses"]![0]!["index"] = "pactos-viejos-y";
        Assert.Contains(await ImportErrorsAsync(admin, old), e => e.StartsWith("classesExtended[0].subclasses[0].expandedSpellList:", StringComparison.Ordinal));
    }

    private static StringContent Json(JsonNode pack) => new(pack.ToJsonString(), Encoding.UTF8, "application/json");

    private static async Task<List<string>> ImportErrorsAsync(HttpClient admin, JsonNode pack)
    {
        var response = await admin.PostAsync(PacksUrl, Json(pack));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    /// <summary>An active level 1 character of the class and subclass (Cha and Wis 16) with a level granted by the DM.</summary>
    private static async Task<CharacterDetailDto> LevelOneAsync(CampaignScenario s, string classIndex, string? subclass, string name)
    {
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, name);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(hero.Id)}/sheet", new
        {
            classes = new[] { new { classIndex, subclassIndex = subclass, level = 1 } },
            baseAbilities = new { str = 8, dex = 14, con = 14, @int = 10, wis = 16, cha = 16 },
            applyRacialBonuses = false,
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(hero.Id)}/activate", null)).StatusCode);
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/systems/dnd5e/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { hero.Id } });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        return await s.Player.GetCharacterAsync(hero.Id);
    }

    private static async Task<LevelUpPlanDto> PlanAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/level-up");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LevelUpPlanDto>())!;
    }

    private static async Task<SpellPreparationDto> PreparationAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync($"{ItemTestHelpers.Dnd5eCharacterUrl(id)}/spell-preparation");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SpellPreparationDto>())!;
    }
}
