using System.Net;
using System.Net.Http.Json;
using System.Text;
using Dnd.Api.Tests.Items;
using Dnd.Application.Catalog;
using Dnd.Application.Characters;
using Dnd.Application.ContentPacks;

namespace Dnd.Api.Tests.ContentPacks;

/// <summary>
/// Validates the operator's complete private PHB pack (<c>content-packs/phb-2014.json</c> at the repository root,
/// never committed: see <c>docs/content-packs.md</c>) when it exists on this machine: it must import without errors
/// and every content kind it carries (subclasses, level choices, subraces, backgrounds, spells, feats) must reach
/// the catalog and the level-up wizard. Without the file the test passes without checking anything.
/// </summary>
public class PrivatePhbPackTests(ContentPackApiFactory factory) : IClassFixture<ContentPackApiFactory>
{
    private const string PacksUrl = "/api/v1/admin/content-packs";
    private const string PackId = "phb-2014";

    private static readonly string[] Classes =
        ["barbarian", "bard", "cleric", "druid", "fighter", "monk", "paladin", "ranger", "rogue", "sorcerer", "warlock", "wizard"];

    [Fact]
    public async Task The_private_phb_pack_imports_with_every_content_kind_and_its_choices_reach_the_wizard()
    {
        if (FindPack() is not { } path)
        {
            return;
        }

        var admin = await factory.CreateAdminClientAsync();
        var response = await admin.PostAsync(PacksUrl, new StringContent(await File.ReadAllTextAsync(path), Encoding.UTF8, "application/json"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var result = (await response.Content.ReadFromJsonAsync<ContentPackImportResultDto>())!;
        Assert.Equal(PackId, result.Id);
        Assert.Equal(28, result.Counts["subclasses"]);
        Assert.Equal(42, result.Counts["spells"]);
        Assert.Equal(12, result.Counts["backgrounds"]);
        Assert.True(result.Counts["subraces"] >= 4, $"Subrazas: {result.Counts["subraces"]}.");
        Assert.True(result.Counts["options"] >= 41 + 16 + 17, $"Opciones: {result.Counts["options"]}.");
        Assert.True(result.Counts["levelChoices"] >= 40, $"Elecciones: {result.Counts["levelChoices"]}.");
        Assert.Equal(1, result.Counts["rollTables"]);

        // Every class gains at least one subclass of the pack.
        foreach (var classIndex in Classes)
        {
            var detail = await GetAsync<ClassDetailDto>(admin, $"/api/v1/catalog/classes/{classIndex}");
            Assert.Contains(detail.Subclasses, s => s.Source == PackId && s.Index.StartsWith($"{PackId}-", StringComparison.Ordinal));
        }

        // PHB subraces sit on the SRD races: the drow with its bonus, weapons and racial spells by level, the wood elf with its own speed.
        Assert.Equal(4, result.Counts["raceExtensions"]);
        var elf = await GetAsync<RaceDetailDto>(admin, "/api/v1/catalog/races/elf");
        var drow = Assert.Single(elf.Subraces, s => s.Index == $"{PackId}-dark-elf");
        Assert.Contains(drow.AbilityBonuses, b => b is { Ability: "cha", Bonus: 1 });
        Assert.Contains(drow.Traits, t => t.Name == "Sunlight Sensitivity");
        Assert.NotNull(drow.Grants);
        Assert.Contains("hand-crossbows", drow.Grants!.Weapons);
        Assert.Contains(drow.Grants.Spells, sp => sp is { Index: "darkness", MinLevel: 5 });
        var woodElf = Assert.Single(elf.Subraces, s => s.Index == $"{PackId}-wood-elf");
        Assert.Equal(35, woodElf.Speed);
        var dwarf = await GetAsync<RaceDetailDto>(admin, "/api/v1/catalog/races/dwarf");
        Assert.Contains(dwarf.Subraces, s => s.Index == $"{PackId}-mountain-dwarf");
        var variantHuman = await GetAsync<RaceDetailDto>(admin, $"/api/v1/catalog/races/{PackId}-variant-human");
        Assert.NotNull(variantHuman.Choices?.Feats);

        var backgrounds = await GetAsync<List<BackgroundDto>>(admin, "/api/v1/catalog/backgrounds");
        var sage = Assert.Single(backgrounds, b => b.Index == $"{PackId}-sage");
        Assert.Equal(["Arcana", "History"], sage.SkillProficiencies.Order());
        Assert.NotNull(sage.Personality);
        Assert.Equal((8, 6, 6, 6), (sage.Personality!.Traits.Count, sage.Personality.Ideals.Count, sage.Personality.Bonds.Count, sage.Personality.Flaws.Count));
        Assert.NotNull(sage.StartingEquipment);
        Assert.Equal(12, backgrounds.Count(b => b.Source == PackId));

        var hex = await GetAsync<SpellDetailDto>(admin, $"/api/v1/catalog/spells/{PackId}-hex");
        Assert.Equal((1, "Enchantment", true), (hex.Level, hex.School, hex.Concentration));
        Assert.Contains("warlock", hex.ClassIndexes);

        // A fighter reaching level 3 is offered the Battle Master and, with it, three maneuvers; the superiority dice come with the feature itself.
        var s = await factory.CreateCampaignScenarioAsync();
        var hero = await ActiveFighterAsync(s, level: 2);
        var plan = await PlanAsync(s.Player, hero.Id);
        var subclass = Assert.Single(plan.Choices, c => c.Kind == "Subclass");
        Assert.Contains(subclass.Options, o => o.Index == $"{PackId}-battle-master");
        Assert.Contains(subclass.Options, o => o.Index == $"{PackId}-eldritch-knight");
        var maneuvers = Assert.Single(plan.Choices, c => c.Key == "maniobras");
        Assert.Equal(($"{PackId}-battle-master", 3, 16), (maneuvers.SubclassIndex, maneuvers.Required, maneuvers.Options.Count));
        Assert.DoesNotContain(plan.Choices, c => c.Key.StartsWith("recurso-", StringComparison.Ordinal));
        var tool = Assert.Single(plan.Choices, c => c.Key == "herramienta-de-artesano");
        Assert.Contains(tool.Options, o => o.Index == "Smith's tools");

        // The Eldritch Knight (a third caster of the wizard list) asks for two cantrips and three level-1 wizard spells.
        var knightCantrips = Assert.Single(plan.Choices, c => c.Key == "trucos" && c.SubclassIndex == $"{PackId}-eldritch-knight");
        Assert.Equal(2, knightCantrips.Required);
        Assert.Contains(knightCantrips.Options, o => o.Index == "fire-bolt");
        var knightSpells = Assert.Single(plan.Choices, c => c.Key == "conjuros" && c.SubclassIndex == $"{PackId}-eldritch-knight");
        Assert.Equal(3, knightSpells.Required);
        Assert.NotEmpty(knightSpells.Options);
        Assert.All(knightSpells.Options, o => Assert.Equal(1, o.SpellLevel));
        Assert.Contains(knightSpells.Options, o => o.Index == "shield");
        Assert.Contains(knightSpells.Options, o => o.Index == $"{PackId}-chromatic-orb");

        var level3 = await ApplyAsync(s.Player, hero.Id, new
        {
            hitPointsRolled = 6,
            choices = new object[]
            {
                new { key = "subclass", selected = new[] { $"{PackId}-battle-master" } },
                new { key = "maniobras", selected = new[] { $"{PackId}-maneuver-parry", $"{PackId}-maneuver-riposte", $"{PackId}-maneuver-trip-attack" } },
                new { key = "herramienta-de-artesano", selected = new[] { "Smith's tools" } },
            },
        });
        Assert.Equal($"{PackId}-battle-master", level3.Classes.Single().SubclassIndex);
        var superiority = Assert.Single(level3.Resources, r => r.Key == $"{PackId}-superiority-dice");
        Assert.Equal((4, "ShortRest", true, "d8"), (superiority.Max, superiority.Recharge, superiority.IsAuto, superiority.Dice));
        Assert.Contains("Combat Superiority", superiority.Source);
        Assert.Contains(level3.Proficiencies, p => p is { Type: "Tool", Key: "Smith's tools" });

        // Level 4: the 41 feats are offered as before, with their prerequisites.
        await GrantAsync(s, hero.Id);
        var asi = Assert.Single((await PlanAsync(s.Player, hero.Id)).Choices, c => c.Kind == "AsiOrFeat");
        var feats = asi.Options.Where(o => o.Index.StartsWith($"{PackId}-feat-", StringComparison.Ordinal)).ToList();
        Assert.Equal(41, feats.Count);
        Assert.All(feats, f => Assert.NotEmpty(f.Description));
        Assert.Contains(feats, f => f.EffectsPreview.Any(e => e.Field == "initiative" && e.Value == 5));
        var rookie = Assert.Single(asi.Options, o => o.Index == $"{PackId}-feat-alert");
        Assert.True(rookie.Eligible);

        // A cleric of the Light domain gets the always-prepared domain spells and the bonus cantrip.
        var cleric = await s.Player.CreateCharacterAsync(s.CampaignId, "Clériga");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(cleric.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "cleric", subclassIndex = $"{PackId}-light-domain", level = 2 } },
            baseAbilities = new { str = 10, dex = 12, con = 14, @int = 10, wis = 16, cha = 10 },
            applyRacialBonuses = false,
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(cleric.Id)}/activate", null)).StatusCode);
        await GrantAsync(s, cleric.Id);
        var clericPlan = await PlanAsync(s.Player, cleric.Id);
        Assert.DoesNotContain(clericPlan.Choices, c => c.Key == "recurso-destello-protector");
        var level3Cleric = await ApplyAsync(s.Player, cleric.Id, new { hitPointsRolled = 5, choices = Array.Empty<object>() });
        var flare = Assert.Single(level3Cleric.Resources, r => r.Key == $"{PackId}-warding-flare");
        Assert.Equal((3, "LongRest", true), (flare.Max, flare.Recharge, flare.IsAuto));
        Assert.Contains(level3Cleric.Spells, sp => sp is { SpellIndex: "burning-hands", AlwaysPrepared: true });
        Assert.Contains(level3Cleric.Spells, sp => sp is { SpellIndex: "flaming-sphere", AlwaysPrepared: true });
        Assert.DoesNotContain(level3Cleric.Spells, sp => sp.SpellIndex == "daylight");
        Assert.Contains(level3Cleric.Spells, sp => sp is { SpellIndex: "light", AlwaysPrepared: true });
        var wardingFlare = Assert.Single(level3Cleric.Resources, r => r.Key == $"{PackId}-warding-flare");
        Assert.Equal(3, wardingFlare.Max);

        // From v2.3 the Eldritch Knight is a third caster with Intelligence and its spells are abjuration or evocation
        // except at levels 3, 8, 14 and 20.
        if (Version.Parse(result.Version) >= new Version(2, 3))
        {
            var knight = await s.Player.CreateCharacterAsync(s.CampaignId, "Caballera");
            var knightPatch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(knight.Id)}/sheet", new
            {
                classes = new[] { new { classIndex = "fighter", subclassIndex = $"{PackId}-eldritch-knight", level = 3 } },
                baseAbilities = new { str = 16, dex = 12, con = 14, @int = 14, wis = 10, cha = 8 },
                applyRacialBonuses = false,
            });
            Assert.Equal(HttpStatusCode.OK, knightPatch.StatusCode);
            var knightSheet = (await knightPatch.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
            var knightCasting = Assert.Single(knightSheet.Sheet.Spellcasting);
            Assert.Equal(("fighter", "int", 12, 3, 2), (knightCasting.ClassIndex, knightCasting.Ability, knightCasting.SaveDc, knightCasting.SpellsKnownMax, knightCasting.CantripsKnownMax));
            Assert.Equal(2, knightSheet.SpellSlots.Single(sl => sl.Level == 1).Max);

            Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(knight.Id)}/activate", null)).StatusCode);
            await GrantAsync(s, knight.Id);
            var knightPlan = await PlanAsync(s.Player, knight.Id);
            var level4Spells = Assert.Single(knightPlan.Choices, c => c.Key == "conjuros");
            Assert.Equal(1, level4Spells.Choose);
            Assert.True(level4Spells.Options.Single(o => o.Index == "shield").Eligible);
            var charm = level4Spells.Options.Single(o => o.Index == "charm-person");
            Assert.False(charm.Eligible);
            Assert.Equal("Solo abjuración o evocación salvo en los niveles 3, 8, 14 y 20", charm.Reason);
        }

        // From v2.4 the Archfey's expanded list reaches the catalog and a warlock with that patron.
        if (Version.Parse(result.Version) >= new Version(2, 4))
        {
            var faerieFire = await GetAsync<SpellDetailDto>(admin, "/api/v1/catalog/spells/faerie-fire");
            Assert.Contains(faerieFire.ExpandedBy, e => e.SubclassIndex == $"{PackId}-the-archfey" && e.ClassIndex == "warlock");
            var phantasmal = await GetAsync<SpellDetailDto>(admin, $"/api/v1/catalog/spells/{PackId}-phantasmal-force");
            Assert.Equal(2, phantasmal.ExpandedBy.Count);
        }
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {response.StatusCode}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<CharacterDetailDto> ActiveFighterAsync(CampaignScenario s, int level)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, "Maestra");
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex = "fighter", level } },
            baseAbilities = new { str = 16, dex = 12, con = 14, @int = 10, wis = 13, cha = 10 },
            applyRacialBonuses = false,
            proficiencies = new[] { new { type = "Armor", key = "light-armor", expertise = false } },
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null)).StatusCode);
        await GrantAsync(s, character.Id);
        return character;
    }

    private static async Task GrantAsync(CampaignScenario s, Guid characterId)
    {
        var granted = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { characterId } });
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
    }

    private static async Task<LevelUpPlanDto> PlanAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync($"{ItemTestHelpers.CharacterUrl(id)}/level-up");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LevelUpPlanDto>())!;
    }

    private static async Task<CharacterDetailDto> ApplyAsync(SignedInUser actor, Guid id, object body)
    {
        var response = await actor.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(id)}/level-up", body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    /// <summary>Walks up from the test binaries to the repository root and returns the pack path when the file exists.</summary>
    private static string? FindPack()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "content-packs", "phb-2014.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
