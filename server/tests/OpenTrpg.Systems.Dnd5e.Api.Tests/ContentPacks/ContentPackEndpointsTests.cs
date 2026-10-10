using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Application.ContentPacks;
using OpenTrpg.Core.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application.Items;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Factory with the SRD imported and its own database, so that packs do not change the counts of the shared catalog tests.</summary>
public sealed class ContentPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

public class ContentPackEndpointsTests(ContentPackApiFactory factory) : IClassFixture<ContentPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string ExampleId = "reinos-ejemplo";

    [Fact]
    public async Task Importing_the_example_adds_its_subclass_item_spell_race_and_background()
    {
        var admin = await factory.CreateAdminClientAsync();

        var result = await ImportAsync(admin, Example(ExampleId));

        Assert.Equal(ExampleId, result.Id);
        Assert.Equal("Reinos de Ejemplo", result.Name);
        Assert.Equal("1.0.0", result.Version);
        Assert.Equal(1, result.Counts["subclasses"]);
        Assert.Equal(1, result.Counts["features"]);
        Assert.Equal(1, result.Counts["items"]);
        Assert.Equal(1, result.Counts["spells"]);
        Assert.Equal(1, result.Counts["races"]);
        Assert.Equal(1, result.Counts["subraces"]);
        Assert.Equal(2, result.Counts["traits"]);
        Assert.Equal(1, result.Counts["backgrounds"]);

        var fighter = await GetAsync<ClassDetailDto>(admin, "/api/v1/catalog/classes/fighter");
        var sentinel = Assert.Single(fighter.Subclasses, s => s.Index == "reinos-ejemplo-centinela");
        Assert.Equal(ExampleId, sentinel.Source);
        Assert.Equal("Centinela", sentinel.Name);
        Assert.Equal("Arquetipo marcial", sentinel.Flavor);
        var level3 = Assert.Single(sentinel.Levels);
        Assert.Equal(3, level3.Level);
        Assert.Equal("Vigilia", Assert.Single(level3.Features).Name);
        Assert.Equal("srd", Assert.Single(fighter.Subclasses, s => s.Index == "champion").Source);

        var items = await GetAsync<PagedResult<ItemSummaryDto>>(admin, "/api/v1/catalog/items?search=espada%20del%20alba");
        var sword = Assert.Single(items.Items);
        Assert.Equal(ExampleId, sword.Source);
        Assert.Equal("Rare", sword.Rarity);
        var swordDetail = await GetAsync<ItemDetailDto>(admin, $"/api/v1/catalog/items/{sword.Id}");
        Assert.Equal(["AttackBonus", "DamageBonus"], swordDetail.Modifiers.Select(m => m.Kind));
        Assert.Equal("1d10", swordDetail.VersatileDice);

        var spells = await GetAsync<PagedResult<SpellSummaryDto>>(admin, "/api/v1/catalog/spells?search=luz%20del%20alba");
        var spell = Assert.Single(spells.Items);
        Assert.Equal(ExampleId, spell.Source);
        Assert.Equal("Evocation", spell.School);
        Assert.Equal("Damage", spell.Category);
        var spellDetail = await GetAsync<SpellDetailDto>(admin, $"/api/v1/catalog/spells/{spell.Index}");
        Assert.Equal("2d8", spellDetail.Damage?.AtSlotLevel?[1]);
        Assert.Equal("dex", spellDetail.DcAbility);
        Assert.Contains((await GetAsync<PagedResult<SpellSummaryDto>>(admin, "/api/v1/catalog/spells?classIndex=paladin&level=1&pageSize=100")).Items, x => x.Index == spell.Index);

        var races = await GetAsync<List<RaceSummaryDto>>(admin, "/api/v1/catalog/races");
        Assert.Equal(ExampleId, Assert.Single(races, r => r.Index == "reinos-ejemplo-aurano").Source);
        Assert.All(races.Where(r => r.Index == "elf"), r => Assert.Equal("srd", r.Source));
        var race = await GetAsync<RaceDetailDto>(admin, "/api/v1/catalog/races/reinos-ejemplo-aurano");
        Assert.Equal("Brillo", Assert.Single(race.Traits).Name);
        var subrace = Assert.Single(race.Subraces);
        Assert.Equal("Mirada clara", Assert.Single(subrace.Traits).Name);
        Assert.Equal(new AbilityBonusDto("wis", 1), Assert.Single(subrace.AbilityBonuses));

        var backgrounds = await GetAsync<List<BackgroundDto>>(admin, "/api/v1/catalog/backgrounds");
        var lighthouse = Assert.Single(backgrounds, b => b.Index == "reinos-ejemplo-farero");
        Assert.Equal(ExampleId, lighthouse.Source);
        Assert.Equal(["Perception", "Survival"], lighthouse.SkillProficiencies);

        var packs = await GetAsync<List<ContentPackDto>>(admin, PacksUrl);
        var pack = Assert.Single(packs, p => p.Id == ExampleId);
        Assert.Equal("Reinos de Ejemplo", pack.Name);
        Assert.Equal(2, pack.Counts["traits"]);

        var sources = await GetAsync<List<CatalogSourceDto>>(admin, "/api/v1/catalog/sources");
        Assert.Equal("srd", sources[0].Id);
        Assert.Contains(new CatalogSourceDto(ExampleId, "Reinos de Ejemplo", "1.0.0"), sources);
    }

    [Fact]
    public async Task Reimporting_with_a_new_name_updates_the_item_and_keeps_its_id()
    {
        const string id = "reinos-ejemplo-re";
        var admin = await factory.CreateAdminClientAsync();
        await ImportAsync(admin, Example(id));
        var before = Assert.Single((await GetAsync<PagedResult<ItemSummaryDto>>(admin, "/api/v1/catalog/items?search=espada%20del%20alba&pageSize=100")).Items, i => i.Source == id);

        var renamed = Example(id).Replace("Espada del Alba", "Espada del Ocaso", StringComparison.Ordinal).Replace("\"1.0.0\"", "\"1.1.0\"", StringComparison.Ordinal);
        var response = await admin.PostAsync(PacksUrl, new StringContent(renamed, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var after = Assert.Single((await GetAsync<PagedResult<ItemSummaryDto>>(admin, "/api/v1/catalog/items?search=espada%20del%20ocaso&pageSize=100")).Items, i => i.Source == id);
        Assert.Equal(before.Id, after.Id);
        Assert.DoesNotContain((await GetAsync<PagedResult<ItemSummaryDto>>(admin, "/api/v1/catalog/items?search=espada%20del%20alba&pageSize=100")).Items, i => i.Source == id);

        var pack = Assert.Single(await GetAsync<List<ContentPackDto>>(admin, PacksUrl), p => p.Id == id);
        Assert.Equal("1.1.0", pack.Version);

        // Re-importing the same version replaces the content again.
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsync(PacksUrl, new StringContent(renamed, Encoding.UTF8, "application/json"))).StatusCode);
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.CatalogImports.CountAsync(x => x.Ruleset == CatalogSources.PackRuleset(id)));
            Assert.Equal(1, await db.Set<SubclassDefinition>().CountAsync(x => x.Source == id));
            Assert.Equal(1, await db.ItemTemplates.CountAsync(x => x.Source == id));
        });
    }

    [Fact]
    public async Task Deleting_a_pack_removes_its_content_and_characters_still_load_marked_as_missing()
    {
        const string id = "reinos-ejemplo-del";
        var admin = await factory.CreateAdminClientAsync();
        await ImportAsync(admin, Example(id));
        var s = await factory.CreateCampaignScenarioAsync();
        var created = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/characters", new { name = "Vigía", ownerUserId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var characterId = (await created.Content.ReadFromJsonAsync<CharacterDetailDto>())!.Id;

        var patch = await s.Dm.Client.PatchAsJsonAsync($"/api/v1/characters/{characterId}/sheet", new
        {
            raceIndex = $"{id}-aurano",
            subraceIndex = $"{id}-aurano-del-alba",
            backgroundIndex = $"{id}-farero",
            classes = new[] { new { classIndex = "fighter", subclassIndex = $"{id}-centinela", level = 3 } },
            spells = new[] { new { spellIndex = $"{id}-luz-del-alba", classIndex = "fighter", isPrepared = true } },
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var withPack = (await patch.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
        Assert.Equal("Centinela", withPack.Classes.Single().SubclassName);
        Assert.Equal("Aurano", withPack.RaceName);
        Assert.False(withPack.CatalogMissing);
        Assert.False(withPack.Classes.Single().CatalogMissing);

        // An inventory entry keeps using the pack item after the deletion.
        var sword = Assert.Single((await GetAsync<PagedResult<ItemSummaryDto>>(admin, "/api/v1/catalog/items?search=espada%20del%20alba&pageSize=100")).Items, i => i.Source == id);
        var added = await s.Dm.Client.PostAsJsonAsync($"/api/v1/characters/{characterId}/inventory", new { templateId = sword.Id, quantity = 1 });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{PacksUrl}/{id}")).StatusCode);

        var fighter = await GetAsync<ClassDetailDto>(admin, "/api/v1/catalog/classes/fighter");
        Assert.DoesNotContain(fighter.Subclasses, x => x.Source == id);
        Assert.DoesNotContain((await GetAsync<PagedResult<ItemSummaryDto>>(admin, "/api/v1/catalog/items?search=espada%20del%20alba&pageSize=100")).Items, i => i.Source == id);
        Assert.DoesNotContain((await GetAsync<PagedResult<SpellSummaryDto>>(admin, "/api/v1/catalog/spells?search=luz%20del%20alba&pageSize=100")).Items, x => x.Source == id);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/catalog/races/{id}-aurano")).StatusCode);
        Assert.DoesNotContain(await GetAsync<List<ContentPackDto>>(admin, PacksUrl), p => p.Id == id);

        var detail = await GetAsync<CharacterDetailDto>(s.Dm.Client, $"/api/v1/characters/{characterId}");
        Assert.True(detail.CatalogMissing);
        Assert.True(detail.RaceCatalogMissing);
        Assert.True(detail.BackgroundCatalogMissing);
        var fighterClass = Assert.Single(detail.Classes);
        Assert.True(fighterClass.CatalogMissing);
        Assert.Equal($"{id}-centinela", fighterClass.SubclassIndex);
        Assert.Null(fighterClass.SubclassName);
        var spell = Assert.Single(detail.Spells);
        Assert.True(spell.CatalogMissing);
        Assert.Null(spell.SpellName);
        Assert.Null(detail.RaceName);
        Assert.Equal(sword.Id, Assert.Single(detail.Inventory.Items).TemplateId);
        Assert.True(detail.Sheet.HitPointsMax > 0);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"{PacksUrl}/{id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_packs_are_rejected_with_every_error_and_its_path()
    {
        var admin = await factory.CreateAdminClientAsync();
        var invalid = """
            {
              "id": "reinos-erroneos",
              "name": "Reinos Erróneos",
              "version": "1.0.0",
              "classesExtended": [ { "classIndex": "artificer", "subclasses": [] } ],
              "items": [
                { "index": "reinos-erroneos-anillo", "name": "Anillo", "category": "Ring", "modifiers": [ { "kind": "Bogus", "value": 1 } ] }
              ],
              "spells": [
                { "index": "luz", "name": "", "level": 12, "school": "pyromancy", "castingTime": "1 action", "range": "Self", "duration": "1 minute", "classes": ["fighter"], "subclasses": ["no-such-subclass"], "category": "magic" }
              ],
              "backgrounds": [ { "index": "reinos-erroneos-a", "name": "A", "skillProficiencies": ["cooking"] }, { "index": "reinos-erroneos-a", "name": "B" } ]
            }
            """;

        var response = await admin.PostAsync(PacksUrl, new StringContent(invalid, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ReadErrorsAsync(response);
        Assert.Contains(errors, e => e.StartsWith("classesExtended[0].classIndex: La clase 'artificer' no existe", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("items[0].category: Categoría desconocida", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("items[0].modifiers[0].kind: Tipo de modificador desconocido", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("spells[0].index: Debe empezar por \"reinos-erroneos-\"", StringComparison.Ordinal));
        Assert.Contains("spells[0].name: Campo obligatorio.", errors);
        Assert.Contains("spells[0].level: Debe estar entre 0 y 9.", errors);
        Assert.Contains(errors, e => e.StartsWith("spells[0].school: Escuela desconocida", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("spells[0].subclasses[0]: La subclase 'no-such-subclass'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("spells[0].category: Categoría desconocida", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("backgrounds[0].skillProficiencies[0]: La habilidad 'cooking'", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.StartsWith("backgrounds[1].index: Índice 'reinos-erroneos-a' duplicado", StringComparison.Ordinal));
        Assert.DoesNotContain(await GetAsync<List<ContentPackDto>>(admin, PacksUrl), p => p.Id == "reinos-erroneos");

        // Unknown properties (typos) are reported with their path.
        var typo = """{ "id": "reinos-erratas", "name": "Erratas", "version": "1", "items": [ { "index": "reinos-erratas-x", "name": "X", "category": "Other", "damgeDice": "1d4" } ] }""";
        var typoErrors = await ReadErrorsAsync(await admin.PostAsync(PacksUrl, new StringContent(typo, Encoding.UTF8, "application/json")));
        Assert.Equal(["items[0].damgeDice: Propiedad desconocida (revisa el nombre)."], typoErrors);

        // Indexes already used by another source (here the SRD) are rejected.
        var collision = """{ "id": "potion", "name": "Pociones", "version": "1", "items": [ { "index": "potion-of-healing", "name": "Copia", "category": "Consumable" } ] }""";
        var collisionErrors = await ReadErrorsAsync(await admin.PostAsync(PacksUrl, new StringContent(collision, Encoding.UTF8, "application/json")));
        Assert.Equal(["items[0].index: El índice 'potion-of-healing' ya existe en otra fuente (srd)."], collisionErrors);

        // Reserved ids and invalid JSON.
        var reserved = await ReadErrorsAsync(await admin.PostAsync(PacksUrl, new StringContent("""{ "id": "srd", "name": "X", "version": "1" }""", Encoding.UTF8, "application/json")));
        Assert.Contains(reserved, e => e.StartsWith("id: ", StringComparison.Ordinal));
        var broken = await ReadErrorsAsync(await admin.PostAsync(PacksUrl, new StringContent("""{ "id": """, Encoding.UTF8, "application/json")));
        Assert.Contains("JSON no válido", Assert.Single(broken));

        // A multipart form without the file field.
        using var form = new MultipartFormDataContent { { new StringContent("x"), "other" } };
        Assert.Equal(["file: Falta el fichero del paquete."], await ReadErrorsAsync(await admin.PostAsync(PacksUrl, form)));
    }

    [Fact]
    public async Task Only_admins_manage_content_packs()
    {
        var user = await factory.CreateSignedInUserAsync("Jugador");

        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.GetAsync(PacksUrl)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.PostAsync(PacksUrl, new StringContent(Example("reinos-ejemplo-x"), Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.Client.DeleteAsync($"{PacksUrl}/{ExampleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(PacksUrl)).StatusCode);

        // Every user can see the sources, to label the pack content.
        Assert.Equal(HttpStatusCode.OK, (await user.Client.GetAsync("/api/v1/catalog/sources")).StatusCode);
    }

    [Fact]
    public async Task Reimporting_the_srd_keeps_the_packs()
    {
        const string id = "reinos-ejemplo-srd";
        var admin = await factory.CreateAdminClientAsync();
        await ImportAsync(admin, Example(id));
        Guid itemId = default;
        await factory.WithDbAsync(async db =>
        {
            itemId = (await db.ItemTemplates.SingleAsync(x => x.Source == id)).Id;

            // Simulates a new SRD dataset version.
            await db.CatalogImports.Where(x => x.Ruleset == Dnd5eCatalogSources.SrdRuleset).ExecuteDeleteAsync();
        });

        using (var scope = factory.Services.CreateScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<ISrdSeeder>().SeedAsync());
        }

        var fighter = await GetAsync<ClassDetailDto>(admin, "/api/v1/catalog/classes/fighter");
        Assert.Contains(fighter.Subclasses, x => x.Index == $"{id}-centinela" && x.Levels.Single().Features.Single().Name == "Vigilia");
        Assert.Contains(fighter.Subclasses, x => x.Index == "champion");
        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(12, await db.Set<ClassDefinition>().CountAsync());
            Assert.Equal(12, await db.Set<SubclassDefinition>().CountAsync(x => x.Source == Dnd5eCatalogSources.Srd));
            Assert.Equal(319, await db.Set<SpellDefinition>().CountAsync(x => x.Source == Dnd5eCatalogSources.Srd));
            Assert.Equal(itemId, (await db.ItemTemplates.SingleAsync(x => x.Source == id)).Id);
            Assert.True(await db.Set<SpellDefinition>().AnyAsync(x => x.Source == id));
            Assert.True(await db.Set<RaceDefinition>().AnyAsync(x => x.Source == id));
            Assert.True(await db.Set<SubraceDefinition>().AnyAsync(x => x.Source == id));
            Assert.Equal(2, await db.Set<TraitDefinition>().CountAsync(x => x.Source == id));
            Assert.True(await db.Set<BackgroundDefinition>().AnyAsync(x => x.Source == id));
            Assert.True(await db.CatalogImports.AnyAsync(x => x.Ruleset == CatalogSources.PackRuleset(id)));
        });
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    [Fact]
    public async Task Pack_spells_take_their_category_or_derive_it()
    {
        var admin = await factory.CreateAdminClientAsync();
        const string pack = """
            {
              "id": "reinos-categorias", "name": "Categorías", "version": "1",
              "spells": [
                { "index": "reinos-categorias-lobo", "name": "Lobo de bruma", "level": 2, "school": "conjuration", "castingTime": "1 action", "range": "30 feet", "duration": "1 hour", "classes": ["druid"], "category": "summoning" },
                { "index": "reinos-categorias-red", "name": "Red de raíces", "level": 1, "school": "conjuration", "castingTime": "1 action", "range": "60 feet", "duration": "1 minute", "classes": ["druid"], "dcAbility": "str" },
                { "index": "reinos-categorias-mapa", "name": "Mapa estelar", "level": 1, "school": "divination", "castingTime": "1 minute", "range": "Self", "duration": "1 hour", "classes": ["druid"] }
              ]
            }
            """;

        await ImportAsync(admin, pack);

        foreach (var (index, category) in new[] { ("lobo", "Summoning"), ("red", "Control"), ("mapa", "Utility") })
        {
            var spell = await GetAsync<SpellDetailDto>(admin, $"/api/v1/catalog/spells/reinos-categorias-{index}");
            Assert.Equal((index, category), (index, spell.Category));
        }
    }

    /// <summary>The fictitious example pack with its id (and therefore every index prefix) replaced by <paramref name="id"/>.</summary>
    private static string Example(string id) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "content-pack-example.json"))
            .Replace(ExampleId, id, StringComparison.Ordinal);

    private static async Task<ContentPackImportResultDto> ImportAsync(HttpClient admin, string json)
    {
        using var form = new MultipartFormDataContent();
        var file = new StringContent(json, Encoding.UTF8);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(file, "file", "pack.json");

        var response = await admin.PostAsync(PacksUrl, form);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ContentPackImportResultDto>())!;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<List<string>> ReadErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal("El paquete de contenido no es válido.", problem.GetProperty("title").GetString());
        return problem.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
    }
}
