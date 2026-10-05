using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dnd.Api.Tests.Items;
using Dnd.Application.Characters;

namespace Dnd.Api.Tests;

[Collection(CatalogCollection.Name)]
public class CombatEndpointsTests(CatalogApiFactory factory)
{
    // ---- Combat summary ----------------------------------------------------------------------------

    [Fact]
    public async Task Fighter_with_an_equipped_longsword_gets_its_attack_in_the_combat_summary()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await SetupAsync(s.Player, s.CampaignId, "fighter", 1, new { str = 16, dex = 10, con = 14, @int = 10, wis = 10, cha = 10 }, "martial-weapons");
        var sword = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Longsword"), quantity = 1 });
        Assert.Equal(HttpStatusCode.OK, (await s.Player.PatchItemAsync(character.Id, sword.Id, new { equipped = true })).StatusCode);

        var combat = (await s.Player.GetCharacterAsync(character.Id)).Combat;

        var attack = combat.Attacks[0];
        Assert.Equal((sword.Id, "Longsword", 5, "1d8+3", "1d10+3", "Slashing"),
            (attack.ItemId, attack.Name, attack.AttackBonus, attack.Damage, attack.VersatileDamage, attack.DamageType));
        Assert.Contains("versatile", attack.Properties);
        Assert.Equal("Ataque sin armas", combat.Attacks[^1].Name);
        Assert.Equal("1+3", combat.Attacks[^1].Damage);
        Assert.Empty(combat.ClassPanels);
        Assert.Empty(combat.SpellSlots);
        Assert.Null(combat.PactSlots);
        Assert.Contains(combat.Resources, r => r.Key == "second-wind");
    }

    [Fact]
    public async Task Unequipped_weapons_are_not_attacks()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await SetupAsync(s.Player, s.CampaignId, "fighter", 1, new { str = 16, dex = 10, con = 14, @int = 10, wis = 10, cha = 10 }, "martial-weapons");
        await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Longsword"), quantity = 1 });

        var combat = (await s.Player.GetCharacterAsync(character.Id)).Combat;

        Assert.Equal("Ataque sin armas", Assert.Single(combat.Attacks).Name);
    }

    [Fact]
    public async Task Quick_consumables_include_potions()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await s.Player.CreateCharacterAsync(s.CampaignId);
        var potion = await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Potion of Healing"), quantity = 2 });
        await s.Player.AddItemAsync(character.Id, new { templateId = await s.Player.SrdItemIdAsync("Longsword"), quantity = 1 });

        var combat = (await s.Player.GetCharacterAsync(character.Id)).Combat;

        var consumable = Assert.Single(combat.QuickConsumables);
        Assert.Equal((potion.Id, "Potion of Healing", 2, (int?)null), (consumable.ItemId, consumable.Name, consumable.Quantity, consumable.Charges));
    }

    [Fact]
    public async Task Barbarian_3_panel_has_rage_bonus_and_uses()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await SetupAsync(s.Player, s.CampaignId, "barbarian", 3, new { str = 16, dex = 14, con = 16, @int = 8, wis = 10, cha = 10 });

        var panel = Assert.Single((await s.Player.GetCharacterAsync(character.Id)).Combat.ClassPanels);

        Assert.Equal(("barbarian", 3), (panel.ClassIndex, panel.Level));
        var data = (JsonElement)panel.Data;
        Assert.Equal(2, data.GetProperty("rageDamageBonus").GetInt32());
        Assert.Equal(3, data.GetProperty("rageUses").GetProperty("max").GetInt32());
        Assert.Equal(0, data.GetProperty("rageUses").GetProperty("used").GetInt32());
        Assert.True(data.GetProperty("recklessAttack").GetBoolean());
        Assert.Equal(0, data.GetProperty("brutalCriticalDice").GetInt32());
        Assert.Equal(10 + 2 + 3, data.GetProperty("unarmoredDefenseAc").GetInt32());
    }

    [Fact]
    public async Task Wizard_summary_has_slots_panel_and_arcane_recovery_once_per_long_rest()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await SetupAsync(s.Player, s.CampaignId, "wizard", 5, new { str = 8, dex = 14, con = 12, @int = 18, wis = 10, cha = 10 });
        await PatchSheetAsync(s.Player, character.Id, new
        {
            spells = new[]
            {
                new { spellIndex = "fire-bolt", classIndex = "wizard", isPrepared = true },
                new { spellIndex = "magic-missile", classIndex = "wizard", isPrepared = true },
                new { spellIndex = "shield", classIndex = "wizard", isPrepared = false },
            },
        });

        var combat = (await s.Player.GetCharacterAsync(character.Id)).Combat;

        Assert.Equal([(1, 4), (2, 3), (3, 2)], combat.SpellSlots.Select(x => (x.Level, x.Max)));
        var once = Assert.Single(combat.OnceSinceLongRest);
        Assert.Equal(("arcane-recovery", false), (once.Key, once.Used));
        var data = (JsonElement)Assert.Single(combat.ClassPanels).Data;
        Assert.Equal(["magic-missile", "shield"], data.GetProperty("spellbook").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["magic-missile"], data.GetProperty("prepared").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(9, data.GetProperty("preparedMax").GetInt32());
        Assert.False(data.GetProperty("arcaneRecovery").GetProperty("used").GetBoolean());
        Assert.Equal(3, data.GetProperty("arcaneRecovery").GetProperty("slotLevelsRecoverable").GetInt32());
    }

    // ---- Class actions -------------------------------------------------------------------------------

    [Fact]
    public async Task Rage_spends_a_use_and_is_rejected_for_other_classes()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var barbarian = await SetupAsync(s.Player, s.CampaignId, "barbarian", 1, new { str = 16, dex = 14, con = 16, @int = 8, wis = 10, cha = 10 });
        var fighter = await SetupAsync(s.Player, s.CampaignId, "fighter", 1, new { str = 16, dex = 14, con = 16, @int = 8, wis = 10, cha = 10 });

        var detail = await ActionAsync<CharacterDetailDto>(s.Dm, barbarian.Id, "rage");
        var wrongClass = await s.Player.Client.PostAsync(ActionUrl(fighter.Id, "rage"), null);

        Assert.Equal(1, detail.Resources.Single(r => r.Key == "rage").Used);
        var data = (JsonElement)Assert.Single(detail.Combat.ClassPanels).Data;
        Assert.Equal(1, data.GetProperty("rageUses").GetProperty("used").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, wrongClass.StatusCode);
    }

    [Fact]
    public async Task Divine_smite_at_1st_level_returns_2d8_and_spends_the_slot()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await SetupAsync(s.Player, s.CampaignId, "paladin", 2, new { str = 16, dex = 10, con = 14, @int = 8, wis = 10, cha = 14 });

        var result = await ActionAsync<DivineSmiteResultDto>(s.Player, character.Id, "divine-smite", new { slotLevel = 1 });

        Assert.Equal("2d8", result.DamageDice);
        Assert.Equal((1, 2, 1), result.Character.Combat.SpellSlots.Select(x => (x.Level, x.Max, x.Used)).Single());
        var data = (JsonElement)Assert.Single(result.Character.Combat.ClassPanels).Data;
        var slot = Assert.Single(data.GetProperty("divineSmite").GetProperty("slotsByLevel").EnumerateArray());
        Assert.Equal((1, 1, "2d8"), (slot.GetProperty("level").GetInt32(), slot.GetProperty("available").GetInt32(), slot.GetProperty("extraDice").GetString()));
        Assert.Equal(10, data.GetProperty("layOnHands").GetProperty("pool").GetInt32());

        await ActionAsync<DivineSmiteResultDto>(s.Player, character.Id, "divine-smite", new { slotLevel = 1 });
        var noSlots = await s.Player.Client.PostAsJsonAsync(ActionUrl(character.Id, "divine-smite"), new { slotLevel = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, noSlots.StatusCode);
    }

    [Fact]
    public async Task Lay_on_hands_heals_the_character_and_never_exceeds_the_pool()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await SetupAsync(s.Player, s.CampaignId, "paladin", 1, new { str = 16, dex = 10, con = 10, @int = 8, wis = 10, cha = 14 });
        var hurt = await s.Player.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/combat", new { hitPointsCurrent = 4 });
        Assert.Equal(HttpStatusCode.OK, hurt.StatusCode);

        var tooMuch = await s.Player.Client.PostAsJsonAsync(ActionUrl(character.Id, "lay-on-hands"), new { amount = 6 });
        var healed = await ActionAsync<CharacterDetailDto>(s.Player, character.Id, "lay-on-hands", new { amount = 3 });
        var other = await ActionAsync<CharacterDetailDto>(s.Player, character.Id, "lay-on-hands", new { amount = 2, targetSelf = false });
        var exhausted = await s.Player.Client.PostAsJsonAsync(ActionUrl(character.Id, "lay-on-hands"), new { amount = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Equal(7, healed.HitPointsCurrent);
        Assert.Equal(7, other.HitPointsCurrent);
        Assert.Equal(5, other.Resources.Single(r => r.Key == "lay-on-hands").Used);
        Assert.Equal(HttpStatusCode.BadRequest, exhausted.StatusCode);
    }

    [Fact]
    public async Task Arcane_recovery_rejects_more_levels_than_allowed_and_works_once()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var character = await SetupAsync(s.Player, s.CampaignId, "wizard", 5, new { str = 8, dex = 14, con = 12, @int = 18, wis = 10, cha = 10 });
        foreach (var level in new[] { 1, 2, 3 })
        {
            Assert.Equal(HttpStatusCode.OK, (await s.Player.Client.PostAsync($"{ItemTestHelpers.CharacterUrl(character.Id)}/spell-slots/{level}/spend", null)).StatusCode);
        }

        var excess = await s.Player.Client.PostAsJsonAsync(ActionUrl(character.Id, "arcane-recovery"), new { slotLevels = new[] { 3, 1 } });
        var empty = await s.Player.Client.PostAsJsonAsync(ActionUrl(character.Id, "arcane-recovery"), new { slotLevels = Array.Empty<int>() });
        var recovered = await ActionAsync<CharacterDetailDto>(s.Player, character.Id, "arcane-recovery", new { slotLevels = new[] { 2, 1 } });
        var again = await s.Player.Client.PostAsJsonAsync(ActionUrl(character.Id, "arcane-recovery"), new { slotLevels = new[] { 3 } });

        Assert.Equal(HttpStatusCode.BadRequest, excess.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal([(1, 0), (2, 0), (3, 1)], recovered.Combat.SpellSlots.Select(x => (x.Level, x.Used)));
        Assert.True(Assert.Single(recovered.Combat.OnceSinceLongRest).Used);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Unknown_action_is_404_and_other_players_cannot_act()
    {
        var s = await factory.CreateCampaignScenarioAsync();
        var other = await factory.CreateSignedInUserAsync("Other Player");
        await s.Owner.AddMemberAsync(s.CampaignId, other, CampaignScenario.PlayerRole);
        var character = await SetupAsync(s.Player, s.CampaignId, "barbarian", 1, new { str = 16, dex = 14, con = 16, @int = 8, wis = 10, cha = 10 });

        var unknown = await s.Player.Client.PostAsync(ActionUrl(character.Id, "fireball"), null);
        var asOther = await other.Client.PostAsync(ActionUrl(character.Id, "rage"), null);
        var asOutsider = await s.Outsider.Client.PostAsync(ActionUrl(character.Id, "rage"), null);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, asOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asOutsider.StatusCode);
    }

    // ---- Helpers ---------------------------------------------------------------------------------------

    private static string ActionUrl(Guid id, string action) => $"{ItemTestHelpers.CharacterUrl(id)}/class-actions/{action}";

    /// <summary>Draft of <paramref name="owner"/> with one class, base scores (no racial bonuses) and optional weapon proficiencies.</summary>
    private static async Task<CharacterDetailDto> SetupAsync(SignedInUser owner, Guid campaignId, string classIndex, int level, object scores, params string[] weapons)
    {
        var character = await owner.CreateCharacterAsync(campaignId, classIndex);
        return await PatchSheetAsync(owner, character.Id, new
        {
            classes = new[] { new { classIndex, level } },
            baseAbilities = scores,
            applyRacialBonuses = false,
            proficiencies = weapons.Select(key => new { type = "Weapon", key, expertise = false }).ToArray(),
        });
    }

    private static async Task<CharacterDetailDto> PatchSheetAsync(SignedInUser actor, Guid id, object patch)
    {
        var response = await actor.Client.PatchAsJsonAsync($"{ItemTestHelpers.CharacterUrl(id)}/sheet", patch);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CharacterDetailDto>())!;
    }

    private static async Task<T> ActionAsync<T>(SignedInUser actor, Guid id, string action, object? body = null)
    {
        var response = body is null
            ? await actor.Client.PostAsync(ActionUrl(id, action), null)
            : await actor.Client.PostAsJsonAsync(ActionUrl(id, action), body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
