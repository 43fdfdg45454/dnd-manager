using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dnd.Api.Tests.Items;
using Dnd.Application.Catalog;
using Dnd.Application.Characters;
using Dnd.Application.ChangeRequests;
using Dnd.Application.Party;
using Dnd.Domain.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Dnd.Api.Tests.SpellPreparation;

/// <summary>Forced spell preparation and spell categories (phase 18).</summary>
[Collection(CatalogCollection.Name)]
public class SpellPreparationEndpointsTests(CatalogApiFactory factory, ITestOutputHelper output)
{
    private static readonly object ClericScores = new { str = 10, dex = 12, con = 14, @int = 10, wis = 16, cha = 10 };
    private static readonly object WizardScores = new { str = 8, dex = 14, con = 14, @int = 16, wis = 12, cha = 10 };
    private static readonly object PaladinScores = new { str = 16, dex = 10, con = 14, @int = 8, wis = 10, cha = 14 };

    [Fact]
    public async Task A_new_level_1_cleric_with_Wis_16_must_prepare_up_to_4_spells_of_its_list()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await ActiveAsync(s, "cleric", 1, ClericScores, [("sacred-flame", true)]);

        Assert.True(cleric.SpellPreparationPending);
        Assert.Equal("Creation", cleric.SpellPreparationReason);
        var party = await GetPartyAsync(s.Dm, s.CampaignId);
        Assert.Equal((true, "Creation"), (party.Characters.Single().SpellPreparationPending, party.Characters.Single().SpellPreparationReason));

        var preparation = await GetAsync(s.Player, cleric.Id);

        Assert.True(preparation.Pending);
        Assert.False(preparation.CanKeep);
        var c = Assert.Single(preparation.Classes);
        Assert.Equal(("cleric", "Cleric", 4, 1), (c.ClassIndex, c.ClassName, c.Max, c.MaxSpellLevel));
        Assert.Empty(c.Prepared);
        Assert.All(c.Candidates, spell => Assert.Equal(1, spell.Level));
        Assert.Contains(c.Candidates, spell => spell.Index == "cure-wounds" && spell.Category == "Healing" && spell.School == "Evocation");
        Assert.Contains(c.Candidates, spell => spell.Index == "bless" && spell.Category == "Buff" && spell.Concentration);
        Assert.DoesNotContain(c.Candidates, spell => spell.Index is "sacred-flame" or "spiritual-weapon" or "magic-missile");

        var tooMany = await PostAsync(s.Player, cleric.Id, Prepare("cleric", "bless", "cure-wounds", "guiding-bolt", "healing-word", "command"));
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Contains("como máximo 4", await DetailAsync(tooMany));

        var prepared = await PrepareAsync(s.Player, cleric.Id, Prepare("cleric", "bless", "cure-wounds", "guiding-bolt", "healing-word"));

        Assert.False(prepared.SpellPreparationPending);
        Assert.Null(prepared.SpellPreparationReason);
        Assert.Equal(
            ["bless", "cure-wounds", "guiding-bolt", "healing-word", "sacred-flame"],
            prepared.Spells.Where(sp => sp.IsPrepared).Select(sp => sp.SpellIndex).Order(StringComparer.Ordinal));
        Assert.Equal("Healing", prepared.Spells.Single(sp => sp.SpellIndex == "cure-wounds").Category);
        Assert.Equal("Damage", prepared.Spells.Single(sp => sp.SpellIndex == "sacred-flame").Category);
        Assert.True((await GetAsync(s.Player, cleric.Id)).CanKeep);
    }

    [Fact]
    public async Task Cantrips_always_prepared_spells_and_spells_of_other_lists_are_rejected()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await ActiveAsync(s, "cleric", 1, ClericScores, [("sacred-flame", true)]);
        var domain = await s.Dm.Client.PatchAsJsonAsync(
            $"{ItemTestHelpers.CharacterUrl(cleric.Id)}/sheet",
            new
            {
                spells = new[]
                {
                    new { spellIndex = "sacred-flame", classIndex = "cleric", isPrepared = true, alwaysPrepared = false },
                    new { spellIndex = "bless", classIndex = "cleric", isPrepared = true, alwaysPrepared = true },
                },
            });
        Assert.Equal(HttpStatusCode.OK, domain.StatusCode);
        var c = Assert.Single((await GetAsync(s.Player, cleric.Id)).Classes);
        Assert.Equal(["bless"], c.AlwaysPrepared.Select(sp => sp.Index));
        Assert.DoesNotContain(c.Candidates, sp => sp.Index == "bless");

        var always = await PostAsync(s.Player, cleric.Id, Prepare("cleric", "bless"));
        var cantrip = await PostAsync(s.Player, cleric.Id, Prepare("cleric", "sacred-flame"));
        var otherList = await PostAsync(s.Player, cleric.Id, Prepare("cleric", "magic-missile"));
        var tooHigh = await PostAsync(s.Player, cleric.Id, Prepare("cleric", "spiritual-weapon"));
        var wrongClass = await PostAsync(s.Player, cleric.Id, Prepare("wizard", "magic-missile"));

        Assert.Equal(HttpStatusCode.BadRequest, cantrip.StatusCode);
        Assert.Contains("Los trucos no se preparan", await DetailAsync(cantrip));
        Assert.Contains("ya está siempre preparado", await DetailAsync(always));
        Assert.Contains("no está en la lista de conjuros de Cleric", await DetailAsync(otherList));
        Assert.Contains("aún no tienes espacios de ese nivel", await DetailAsync(tooHigh));
        Assert.Equal(HttpStatusCode.BadRequest, wrongClass.StatusCode);
        Assert.True((await s.Player.GetCharacterAsync(cleric.Id)).SpellPreparationPending);
    }

    [Fact]
    public async Task The_owner_cannot_prepare_without_a_pending_preparation_but_the_dm_always_can()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await ActiveAsync(s, "cleric", 1, ClericScores, [("bless", true), ("cure-wounds", true)]);
        Assert.False(cleric.SpellPreparationPending);

        var owner = await PostAsync(s.Player, cleric.Id, Prepare("cleric", "guiding-bolt"));
        var keep = await s.Player.Client.PostAsync($"{Url(cleric.Id)}/keep", null);
        var outsider = await s.Outsider.Client.GetAsync(Url(cleric.Id));

        Assert.Equal(HttpStatusCode.Conflict, owner.StatusCode);
        Assert.Equal("No es momento de preparar conjuros.", await DetailAsync(owner));
        Assert.Equal(HttpStatusCode.Conflict, keep.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outsider.StatusCode);

        var byDm = await PrepareAsync(s.Dm, cleric.Id, Prepare("cleric", "guiding-bolt"));

        Assert.Equal(["guiding-bolt"], byDm.Spells.Where(sp => sp.IsPrepared).Select(sp => sp.SpellIndex));
    }

    [Fact]
    public async Task An_approved_long_rest_asks_for_the_preparation_and_keeping_it_clears_the_pending()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await ActiveAsync(s, "cleric", 1, ClericScores, [("bless", true), ("cure-wounds", true)]);
        var rest = await AskRestAsync(s.Player, cleric.Id);

        await ApproveRestAsync(s.Dm, rest.Id);

        var rested = await s.Player.GetCharacterAsync(cleric.Id);
        Assert.Equal((true, "LongRest"), (rested.SpellPreparationPending, rested.SpellPreparationReason));
        var preparation = await GetAsync(s.Player, cleric.Id);
        Assert.Equal(["bless", "cure-wounds"], Assert.Single(preparation.Classes).Prepared);
        Assert.True(preparation.CanKeep);

        var kept = await KeepAsync(s.Player, cleric.Id);

        Assert.False(kept.SpellPreparationPending);
        Assert.Equal(["bless", "cure-wounds"], kept.Spells.Where(sp => sp.IsPrepared).Select(sp => sp.SpellIndex).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Keeping_fails_when_the_maximum_went_down()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await ActiveAsync(s, "cleric", 1, ClericScores, [("bless", true), ("cure-wounds", true), ("guiding-bolt", true)]);
        var lowered = await s.Dm.Client.PatchAsJsonAsync(
            $"{ItemTestHelpers.CharacterUrl(cleric.Id)}/sheet",
            new { baseAbilities = new { str = 10, dex = 12, con = 14, @int = 10, wis = 10, cha = 10 } });
        Assert.Equal(HttpStatusCode.OK, lowered.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(cleric.Id)}/rest/long", null)).StatusCode);

        var preparation = await GetAsync(s.Player, cleric.Id);
        var keep = await s.Player.Client.PostAsync($"{Url(cleric.Id)}/keep", null);

        Assert.Equal((1, false), (preparation.Classes.Single().Max, preparation.CanKeep));
        Assert.Contains("como máximo 1", preparation.KeepProblem);
        Assert.Equal(HttpStatusCode.BadRequest, keep.StatusCode);
        Assert.Contains("como máximo 1 conjuros de Cleric y tienes 3", await DetailAsync(keep));
        Assert.True((await s.Player.GetCharacterAsync(cleric.Id)).SpellPreparationPending);

        var prepared = await PrepareAsync(s.Player, cleric.Id, Prepare("cleric", "cure-wounds"));
        Assert.False(prepared.SpellPreparationPending);
    }

    [Fact]
    public async Task A_wizard_prepares_only_from_its_spellbook_and_keeps_the_rest_of_the_book()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        string[] book = ["burning-hands", "detect-magic", "mage-armor", "magic-missile", "shield", "sleep"];
        var wizard = await ActiveAsync(s, "wizard", 1, WizardScores, [("fire-bolt", true), .. book.Select(b => (b, false))]);
        Assert.Equal((true, "Creation"), (wizard.SpellPreparationPending, wizard.SpellPreparationReason));

        var preparation = await GetAsync(s.Player, wizard.Id);

        var c = Assert.Single(preparation.Classes);
        Assert.Equal(4, c.Max);
        Assert.Equal(book, c.Candidates.Select(sp => sp.Index).Order(StringComparer.Ordinal));
        Assert.Equal("Defense", c.Candidates.Single(sp => sp.Index == "shield").Category);
        var notInBook = await PostAsync(s.Player, wizard.Id, Prepare("wizard", "charm-person"));
        Assert.Contains("no está en tu libro de conjuros", await DetailAsync(notInBook));

        var prepared = await PrepareAsync(s.Player, wizard.Id, Prepare("wizard", "magic-missile", "shield"));

        Assert.Equal(7, prepared.Spells.Count);
        Assert.Equal(["fire-bolt", "magic-missile", "shield"], prepared.Spells.Where(sp => sp.IsPrepared).Select(sp => sp.SpellIndex).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_paladin_reaching_level_2_prepares_for_the_first_time()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var paladin = await ActiveAsync(s, "paladin", 1, PaladinScores, []);
        Assert.False(paladin.SpellPreparationPending);
        Assert.Empty((await GetAsync(s.Player, paladin.Id)).Classes);
        Assert.Equal(HttpStatusCode.OK, (await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/grant-level", new { characterIds = new[] { paladin.Id } })).StatusCode);

        var levelUpUrl = $"{ItemTestHelpers.CharacterUrl(paladin.Id)}/level-up";
        var plan = (await s.Player.Client.GetFromJsonAsync<LevelUpPlanDto>(levelUpUrl))!;
        var choices = plan.Choices
            .Select(ch => new { key = ch.Key, selected = ch.Options.Where(o => o.Eligible).Take(ch.Required).Select(o => o.Index).ToArray() })
            .ToArray();
        var response = await s.Player.Client.PostAsJsonAsync(levelUpUrl, new { classIndex = "paladin", hitPointsRolled = 6, choices });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var leveled = (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;

        Assert.Equal((true, "LevelUp"), (leveled.SpellPreparationPending, leveled.SpellPreparationReason));
        var c = Assert.Single((await GetAsync(s.Player, paladin.Id)).Classes);
        Assert.Equal((3, 1), (c.Max, c.MaxSpellLevel));
        Assert.Contains(c.Candidates, sp => sp.Index == "bless");
    }

    [Fact]
    public async Task A_forced_long_rest_asks_the_preparing_characters_only()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await ActiveAsync(s, "cleric", 1, ClericScores, [("bless", true)]);
        var fighter = await s.Player.CreateActiveCharacterAsync(s.Dm, s.CampaignId, "Guerrera");

        var response = await s.Dm.Client.PostAsJsonAsync($"/api/v1/campaigns/{s.CampaignId}/party/rest", new { kind = "long" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var party = (await response.Content.ReadFromJsonAsync<PartyDto>())!;
        Assert.Equal((true, "LongRest"), party.Characters.Where(c => c.Id == cleric.Id).Select(c => (c.SpellPreparationPending, c.SpellPreparationReason)).Single());
        Assert.False(party.Characters.Single(c => c.Id == fighter.Id).SpellPreparationPending);
    }

    [Fact]
    public async Task A_sheet_edit_request_of_the_owner_does_not_change_the_preparation()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var cleric = await ActiveAsync(s, "cleric", 1, ClericScores, [("bless", true), ("cure-wounds", false)]);

        var patch = await s.Player.Client.PatchAsJsonAsync(
            $"{ItemTestHelpers.CharacterUrl(cleric.Id)}/sheet",
            new { spells = new[] { new { spellIndex = "bless", classIndex = "cleric", isPrepared = false }, new { spellIndex = "cure-wounds", classIndex = "cleric", isPrepared = true } } });
        Assert.Equal(HttpStatusCode.Accepted, patch.StatusCode);
        var request = (await patch.Content.ReadFromJsonAsync<ChangeRequestDto>())!;
        await s.Dm.ApproveAsync(request.Id);

        var after = await s.Player.GetCharacterAsync(cleric.Id);
        Assert.Equal([("bless", true), ("cure-wounds", false)], after.Spells.Select(sp => (sp.SpellIndex, sp.IsPrepared)).OrderBy(sp => sp.SpellIndex));
    }

    [Fact]
    public async Task Spells_carry_their_category_in_the_catalog()
    {
        var client = (await factory.CreateSignedInUserAsync("Lectora")).Client;
        var expected = new Dictionary<string, string>
        {
            ["cure-wounds"] = "Healing",
            ["fireball"] = "Damage",
            ["hold-person"] = "Control",
            ["bless"] = "Buff",
            ["shield"] = "Defense",
            ["find-familiar"] = "Summoning",
            ["detect-magic"] = "Utility",
        };

        foreach (var (index, category) in expected)
        {
            var detail = await client.GetFromJsonAsync<SpellDetailDto>($"/api/v1/catalog/spells/{index}");
            Assert.Equal((index, category), (index, detail!.Category));
        }

        using var page = await client.GetFromJsonAsync<JsonDocument>("/api/v1/catalog/spells?search=fireball");
        Assert.Contains(page!.RootElement.GetProperty("items").EnumerateArray(), i => i.GetProperty("category").GetString() == "Damage");
    }

    [Fact]
    public async Task Every_srd_spell_has_a_category()
    {
        factory.CreateClient().Dispose();

        await factory.WithDbAsync(async db =>
        {
            var spells = await db.CatalogSpells.AsNoTracking().Where(x => x.Source == CatalogSources.Srd).ToListAsync();
            Assert.Equal(319, spells.Count);
            Assert.All(spells, sp => Assert.True(Enum.IsDefined(sp.Category), sp.Index));

            var counts = spells.GroupBy(sp => sp.Category).ToDictionary(g => g.Key, g => g.Count());
            output.WriteLine(string.Join(", ", counts.OrderBy(c => c.Key).Select(c => $"{c.Key}: {c.Value}")));
            Assert.Equal(
                new Dictionary<SpellCategory, int>
                {
                    [SpellCategory.Utility] = 116,
                    [SpellCategory.Healing] = 17,
                    [SpellCategory.Damage] = 58,
                    [SpellCategory.Control] = 54,
                    [SpellCategory.Buff] = 29,
                    [SpellCategory.Defense] = 26,
                    [SpellCategory.Summoning] = 19,
                },
                counts);
            Assert.Equal(SpellCategory.Healing, spells.Single(sp => sp.Index == "cure-wounds").Category);
            Assert.Equal(SpellCategory.Damage, spells.Single(sp => sp.Index == "fireball").Category);
            Assert.Equal(SpellCategory.Control, spells.Single(sp => sp.Index == "hold-person").Category);
            Assert.Equal(SpellCategory.Buff, spells.Single(sp => sp.Index == "bless").Category);
            Assert.Equal(SpellCategory.Defense, spells.Single(sp => sp.Index == "shield").Category);
            Assert.Equal(SpellCategory.Summoning, spells.Single(sp => sp.Index == "find-familiar").Category);
        });
    }

    private static string Url(Guid id) => $"{ItemTestHelpers.CharacterUrl(id)}/spell-preparation";

    private static object Prepare(string classIndex, params string[] spells) => new { classes = new[] { new { classIndex, spells } } };

    /// <summary>Draft of the player with the given class, scores and spells (set while a draft), activated by the DM.</summary>
    private static async Task<CharacterDetailDto> ActiveAsync(CampaignScenario s, string classIndex, int level, object scores, (string Index, bool Prepared)[] spells)
    {
        var character = await s.Player.CreateCharacterAsync(s.CampaignId, classIndex);
        var patch = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/sheet", new
        {
            classes = new[] { new { classIndex, subclassIndex = (string?)null, level } },
            baseAbilities = scores,
            applyRacialBonuses = false,
            spells = spells.Select(sp => new { spellIndex = sp.Index, classIndex, isPrepared = sp.Prepared }).ToArray(),
        });
        Assert.True(patch.StatusCode == HttpStatusCode.OK, await patch.Content.ReadAsStringAsync());
        var activate = await s.Dm.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/activate", null);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        return await s.Player.GetCharacterAsync(character.Id);
    }

    private static async Task<SpellPreparationDto> GetAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.GetAsync(Url(id));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SpellPreparationDto>())!;
    }

    private static Task<HttpResponseMessage> PostAsync(SignedInUser actor, Guid id, object body) => actor.Client.PostAsJsonAsync(Url(id), body);

    private static async Task<CharacterDetailDto> PrepareAsync(SignedInUser actor, Guid id, object body)
    {
        var response = await PostAsync(actor, id, body);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static async Task<CharacterDetailDto> KeepAsync(SignedInUser actor, Guid id)
    {
        var response = await actor.Client.PostAsync($"{Url(id)}/keep", null);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static async Task<string> DetailAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail ?? string.Empty;

    private static async Task<RestRequestDto> AskRestAsync(SignedInUser player, Guid characterId)
    {
        var response = await player.Client.PostAsJsonAsync($"{ItemTestHelpers.CharacterUrl(characterId)}/rest-requests", new { kind = "long" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RestRequestDto>())!;
    }

    private static async Task ApproveRestAsync(SignedInUser dm, Guid requestId)
    {
        var response = await dm.Client.PostAsync($"/api/v1/rest-requests/{requestId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<PartyDto> GetPartyAsync(SignedInUser dm, Guid campaignId) =>
        (await dm.Client.GetFromJsonAsync<PartyDto>($"/api/v1/campaigns/{campaignId}/party"))!;
}
