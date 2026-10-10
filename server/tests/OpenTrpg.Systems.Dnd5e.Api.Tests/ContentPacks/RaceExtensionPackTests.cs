using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions;
using OpenTrpg.Systems.Dnd5e.Application.Catalog;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Api.Tests.Items;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests.ContentPacks;

/// <summary>Own database: the pack adds a subrace to an SRD race and is uninstalled.</summary>
public sealed class RaceExtensionPackApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Phase 25, block 1: a fictitious pack that extends the SRD dwarf with a subrace (own speed, grants and racial spells by
/// total level) and defines a new race with fixed grants; the SRD races import their trait proficiencies.
/// </summary>
public class RaceExtensionPackTests(RaceExtensionPackApiFactory factory) : IClassFixture<RaceExtensionPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string PackId = "folk-ejemplo";

    private static readonly object Pack = new
    {
        formatVersion = 2,
        id = PackId,
        name = "Pueblos de ejemplo",
        version = "1.0.0",
        races = new object[]
        {
            new
            {
                extends = "dwarf",
                traits = new[] { new { index = "folk-ejemplo-stone-memory", name = "Stone Memory", description = new[] { "Texto de ejemplo." } } },
                grants = new { skills = new[] { "history" } },
                subraces = new[]
                {
                    new
                    {
                        index = "folk-ejemplo-deep-folk",
                        name = "Deep Folk",
                        description = "Texto de ejemplo.",
                        speed = 30,
                        abilityBonuses = new[] { new { ability = "cha", bonus = 1 } },
                        grants = new
                        {
                            weapons = new[] { "warpicks" },
                            cantrips = new[] { "dancing-lights" },
                            spells = new object[]
                            {
                                new { index = "faerie-fire", minLevel = 3, usesPerLongRest = 1 },
                                new { index = "darkness", minLevel = 5, usesPerLongRest = 1 },
                            },
                            spellcastingAbility = "cha",
                        },
                    },
                },
            },
            new
            {
                index = "folk-ejemplo-mountain-folk",
                name = "Mountain Folk",
                speed = 25,
                size = "Medium",
                grants = new { armor = new[] { "light-armor" }, savingThrows = new[] { "con" } },
            },
        },
    };

    [Fact]
    public async Task A_pack_adds_a_subrace_with_speed_grants_and_racial_spells_to_an_srd_race_and_removes_it_on_uninstall()
    {
        var admin = await factory.CreateAdminClientAsync();
        var import = await admin.PostAsync(PacksUrl, Json(Pack));
        Assert.True(import.StatusCode == HttpStatusCode.Created, await import.Content.ReadAsStringAsync());
        var result = (await import.Content.ReadFromJsonAsync<ContentPackImportResultDtoView>())!;
        Assert.Equal((1, 1, 1), (result.Counts["races"], result.Counts["subraces"], result.Counts["raceExtensions"]));

        var s = await factory.CreateCampaignScenarioAsync();
        var dwarf = (await s.Player.Client.GetFromJsonAsync<RaceDetailDto>("/api/v1/catalog/races/dwarf"))!;
        Assert.Equal("srd", dwarf.Source);
        var deepFolk = Assert.Single(dwarf.Subraces, r => r.Index == "folk-ejemplo-deep-folk");
        Assert.Equal((30, PackId), (deepFolk.Speed, deepFolk.Source));
        Assert.Contains(dwarf.Traits, t => t.Index == "folk-ejemplo-stone-memory");
        Assert.Contains("history", dwarf.Grants!.Skills);
        Assert.Contains("battleaxes", dwarf.Grants.Weapons);
        var races = (await s.Player.Client.GetFromJsonAsync<List<RaceSummaryDto>>("/api/v1/catalog/races"))!;
        Assert.Contains("folk-ejemplo-deep-folk", races.Single(r => r.Index == "dwarf").SubraceIndexes);

        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Enana");
        var url = $"{ItemTestHelpers.CharacterUrl(hero.Id)}/sheet";
        var patch = await s.Player.Client.PatchAsJsonAsync(url, new
        {
            raceIndex = "dwarf",
            subraceIndex = "folk-ejemplo-deep-folk",
            classes = new[] { new { classIndex = "fighter", level = 2 } },
            baseAbilities = new { str = 15, dex = 10, con = 14, @int = 8, wis = 10, cha = 15 },
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());

        var detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal(30, detail.Sheet.Speed);
        Assert.Equal(
            [("race", "Raza", 25), ("subrace", "Deep Folk", 5)],
            detail.Sheet.Breakdowns["speed"].Parts.Select(p => (p.Source, p.Label, p.Value)));
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Weapon", Key: "battleaxes", Source: "Race" });
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Weapon", Key: "warpicks", Source: "Race" });
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Skill", Key: "history", Source: "Race" });
        Assert.DoesNotContain(detail.Choices, c => c.Key.Contains(".grant.", StringComparison.Ordinal));

        // Level 2: only the cantrip; the "Raza" spellcasting uses Charisma 16 (+3).
        Assert.Equal(["dancing-lights"], detail.Spells.Where(sp => sp.ClassIndex == "race").Select(sp => sp.SpellIndex));
        var racial = Assert.Single(detail.Sheet.Spellcasting, c => c.ClassIndex == "race");
        Assert.Equal(("cha", 13, 5, (int?)null), (racial.Ability, racial.SaveDc, racial.AttackBonus, racial.PreparedMax));
        Assert.DoesNotContain(detail.Resources, r => r.Key == "race.faerie-fire");

        // Total level 3 brings faerie fire, always prepared, with its use per long rest.
        await s.Player.Client.PatchAsJsonAsync(url, new { classes = new[] { new { classIndex = "fighter", level = 3 } } });
        detail = await s.Player.GetCharacterAsync(hero.Id);
        var faerieFire = Assert.Single(detail.Spells, sp => sp.SpellIndex == "faerie-fire");
        Assert.Equal(("race", true), (faerieFire.ClassIndex, faerieFire.AlwaysPrepared));
        Assert.DoesNotContain(detail.Spells, sp => sp.SpellIndex == "darkness");
        var uses = Assert.Single(detail.Resources, r => r.Key == "race.faerie-fire");
        Assert.Equal(("Faerie Fire", 1, "LongRest", true), (uses.Name, uses.Max, uses.Recharge, uses.IsAuto));

        // Uninstalling the pack removes the subrace: the sheet warns and its grants are gone on the next recalculation.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{PacksUrl}/{PackId}")).StatusCode);
        dwarf = (await s.Player.Client.GetFromJsonAsync<RaceDetailDto>("/api/v1/catalog/races/dwarf"))!;
        Assert.DoesNotContain(dwarf.Subraces, r => r.Index == "folk-ejemplo-deep-folk");
        Assert.DoesNotContain(dwarf.Traits, t => t.Index == "folk-ejemplo-stone-memory");

        detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.True(detail.RaceCatalogMissing);
        await s.Player.Client.PatchAsJsonAsync(url, new { notes = "Tras desinstalar" });
        detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Equal(25, detail.Sheet.Speed);
        Assert.DoesNotContain(detail.Proficiencies, p => p.Key is "warpicks" or "history");
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Weapon", Key: "battleaxes", Source: "Race" });
        Assert.DoesNotContain(detail.Spells, sp => sp.ClassIndex == "race");
        Assert.DoesNotContain(detail.Sheet.Spellcasting, c => c.ClassIndex == "race");
        Assert.DoesNotContain(detail.Resources, r => r.Key == "race.faerie-fire");
    }

    [Fact]
    public async Task A_new_pack_race_applies_its_grants_and_changing_race_removes_them()
    {
        var admin = await factory.CreateAdminClientAsync();
        var import = await admin.PostAsync(PacksUrl, Json(Pack));
        Assert.True(import.StatusCode == HttpStatusCode.Created, await import.Content.ReadAsStringAsync());

        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await s.Player.CreateCharacterAsync(s.CampaignId, "Montañesa");
        var url = $"{ItemTestHelpers.CharacterUrl(hero.Id)}/sheet";
        await s.Player.Client.PatchAsJsonAsync(url, new { raceIndex = "folk-ejemplo-mountain-folk", classes = new[] { new { classIndex = "wizard", level = 1 } } });

        var detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Armor", Key: "light-armor", Source: "Race" });
        Assert.Contains(detail.Proficiencies, p => p is { Type: "SavingThrow", Key: "con", Source: "Race" });

        await s.Player.Client.PatchAsJsonAsync(url, new { raceIndex = "elf", subraceIndex = "high-elf" });
        detail = await s.Player.GetCharacterAsync(hero.Id);
        Assert.DoesNotContain(detail.Proficiencies, p => p.Key == "light-armor");
        Assert.DoesNotContain(detail.Proficiencies, p => p is { Type: "SavingThrow", Key: "con" });

        // SRD trait proficiencies: Keen Senses of the elf and Elf Weapon Training of the high elf.
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Skill", Key: "perception", Source: "Race" });
        Assert.Contains(detail.Proficiencies, p => p is { Type: "Weapon", Key: "longswords", Source: "Race" });
    }

    [Fact]
    public async Task Reimporting_the_srd_keeps_the_subraces_packs_add_to_its_races()
    {
        var admin = await factory.CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsync(PacksUrl, Json(Pack))).StatusCode);
        await factory.WithDbAsync(async db =>
            await db.CatalogImports.Where(x => x.Ruleset == Dnd5eCatalogSources.SrdRuleset).ExecuteDeleteAsync());

        using (var scope = factory.Services.CreateScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<ISrdSeeder>().SeedAsync());
        }

        await factory.WithDbAsync(async db =>
        {
            Assert.True(await db.Set<SubraceDefinition>().AnyAsync(x => x.Index == "folk-ejemplo-deep-folk" && x.RaceIndex == "dwarf"));
            Assert.True(await db.Set<RaceExtensionDefinition>().AnyAsync(x => x.Source == PackId && x.RaceIndex == "dwarf"));
            Assert.Contains("battleaxes", (await db.Set<RaceDefinition>().SingleAsync(x => x.Index == "dwarf")).GrantsJson);
        });
    }

    [Fact]
    public async Task Invalid_race_extensions_and_racial_grants_are_reported()
    {
        var admin = await factory.CreateAdminClientAsync();
        var invalid = new
        {
            formatVersion = 2,
            id = "folk-erroneo",
            name = "Pueblos erróneos",
            version = "1.0.0",
            races = new object[]
            {
                new { extends = "no-such-race", subraces = Array.Empty<object>() },
                new { extends = "elf", speed = 35, size = "Small", abilityBonuses = new[] { new { ability = "dex", bonus = 2 } }, languages = new[] { "Elvish" } },
                new
                {
                    index = "folk-erroneo-shadow-folk",
                    name = "Shadow Folk",
                    speed = 30,
                    size = "Medium",
                    grants = new { cantrips = new[] { "dancing-lights" } },
                    subraces = new[]
                    {
                        new
                        {
                            index = "folk-erroneo-dusk-folk",
                            name = "Dusk Folk",
                            speed = 250,
                            grants = new { spells = new[] { new { index = "darkness", minLevel = 5, usesPerLongRest = 0 } }, spellcastingAbility = "luck" },
                        },
                    },
                },
            },
        };

        var errors = await ReadErrorsAsync(await admin.PostAsync(PacksUrl, Json(invalid)));

        Assert.Contains(errors, e => e.StartsWith("races[0].extends: La raza 'no-such-race' no existe", StringComparison.Ordinal));
        foreach (var field in new[] { "speed", "size", "abilityBonuses", "languages" })
        {
            Assert.Contains($"races[1].{field}: No se admite en una raza con extends (la raza base ya lo define).", errors);
        }

        Assert.Contains(errors, e => e.StartsWith("races[2].grants.spellcastingAbility: Campo obligatorio", StringComparison.Ordinal));
        Assert.Contains("races[2].subraces[0].speed: Debe estar entre 0 y 200.", errors);
        Assert.Contains("races[2].subraces[0].grants.spells[0].usesPerLongRest: Debe estar entre 1 y 20.", errors);
        Assert.Contains(errors, e => e.StartsWith("races[2].subraces[0].grants.spellcastingAbility: Característica desconocida", StringComparison.Ordinal));

        // Outside races, the racial fields are rejected.
        var option = new
        {
            formatVersion = 2,
            id = "folk-opcion",
            name = "Opción",
            version = "1.0.0",
            optionSets = new[]
            {
                new
                {
                    setId = "feats",
                    options = new[]
                    {
                        new
                        {
                            index = "folk-opcion-dote",
                            name = "Dote de ejemplo",
                            grants = new { spells = new[] { new { index = "darkness", usesPerLongRest = 1 } }, spellcastingAbility = "cha" },
                        },
                    },
                },
            },
        };
        var optionErrors = await ReadErrorsAsync(await admin.PostAsync(PacksUrl, Json(option)));
        Assert.Contains(optionErrors, e => e.StartsWith("optionSets[0].options[0].grants.spells[0].usesPerLongRest: Solo se admite en las razas", StringComparison.Ordinal));
        Assert.Contains("optionSets[0].options[0].grants.spellcastingAbility: Solo se admite en las razas y subrazas.", optionErrors);
    }

    private static StringContent Json(object value) => new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static async Task<List<string>> ReadErrorsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        return problem.GetProperty("errors").EnumerateArray().Select(e => e.GetString()!).ToList();
    }

    private sealed record ContentPackImportResultDtoView(string Id, Dictionary<string, int> Counts);
}
