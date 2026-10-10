using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;
using static OpenTrpg.Core.Domain.Tests.Characters.TestCatalog;

namespace OpenTrpg.Core.Domain.Tests.Characters;

public class RestTests
{
    [Fact]
    public void Long_rest_restores_hp_slots_resources_and_half_the_hit_dice()
    {
        var character = ActiveCharacter([new ClassEntry("fighter", null, 3), new ClassEntry("wizard", null, 2)], out var sheet);
        var shortRestResource = character.Resources.Single(r => r.Key == "second-wind");
        var longRestResource = character.Resources.Single(r => r.Key == "arcane-recovery");
        var dawn = character.AddManualResource("Varita", 3, ResourceRecharge.Dawn, Now);
        var manual = character.AddManualResource("Pociones", 2, ResourceRecharge.Manual, Now);
        foreach (var resource in new[] { shortRestResource, longRestResource, dawn, manual })
        {
            character.SpendResource(resource.Id, 1, Now);
        }

        character.SpendSpellSlot(1, 2, sheet.SpellSlotMax(1), Now);
        character.ShortRest(new Dictionary<string, int> { ["fighter"] = 3, ["wizard"] = 2 }, sheet, new FixedDice(1), Now);
        character.ApplyCombatUpdate(
            new CombatUpdate { HitPointsCurrent = 3, TemporaryHitPoints = 5, DeathSaveFailures = 2, DeathSaveSuccesses = 1, ExhaustionLevel = 2 },
            sheet.HitPointsMax,
            Now);
        character.SetConcentration("bless", Now);

        character.LongRest(sheet.HitPointsMax, Now);

        Assert.Equal(sheet.HitPointsMax, character.HitPointsCurrent);
        Assert.Equal(5, character.TemporaryHitPoints);
        Assert.Equal(0, character.SpellSlotsUsed(1));
        Assert.Equal((0, 0, 1, 1), (shortRestResource.Used, longRestResource.Used, dawn.Used, manual.Used));
        // 5 levels → recovers 2 dice, main class first.
        Assert.Equal(new Dictionary<string, int> { ["fighter"] = 1, ["wizard"] = 2 }, character.HitDiceUsed);
        Assert.Equal(1, character.ExhaustionLevel);
        Assert.Equal((0, 0), (character.DeathSaveSuccesses, character.DeathSaveFailures));
        Assert.Null(character.ConcentratingOnSpellIndex);
    }

    [Fact]
    public void Long_rest_recovers_at_least_one_hit_die()
    {
        var character = ActiveCharacter([new ClassEntry("fighter", null, 1)], out var sheet);
        character.ShortRest(new Dictionary<string, int> { ["fighter"] = 1 }, sheet, new FixedDice(5), Now);
        Assert.Equal(0, character.HitDiceRemaining("fighter"));

        character.LongRest(sheet.HitPointsMax, Now);

        Assert.Equal(1, character.HitDiceRemaining("fighter"));
        Assert.Empty(character.HitDiceUsed);
    }

    [Fact]
    public void Long_rest_recovers_pact_slots_too()
    {
        var character = ActiveCharacter([new ClassEntry("warlock", null, 3)], out var sheet);
        character.SpendSpellSlot(SpellSlotState.PactLevel, 2, sheet.SpellSlotMax(SpellSlotState.PactLevel), Now);

        character.LongRest(sheet.HitPointsMax, Now);

        Assert.Equal(0, character.SpellSlotsUsed(SpellSlotState.PactLevel));
    }

    [Fact]
    public void Short_rest_heals_die_plus_con_without_exceeding_the_maximum()
    {
        var character = ActiveCharacter([new ClassEntry("fighter", null, 3)], out var sheet, Scores(con: 14));
        Assert.Equal(28, sheet.HitPointsMax);
        character.ApplyCombatUpdate(new CombatUpdate { HitPointsCurrent = 10 }, sheet.HitPointsMax, Now);
        var dice = new FixedDice(4);

        var first = character.ShortRest(new Dictionary<string, int> { ["fighter"] = 1 }, new Dictionary<string, int> { ["fighter"] = 10 }, 2, sheet.HitPointsMax, dice, Now);

        Assert.Equal(16, character.HitPointsCurrent);
        Assert.Equal(new HitDieRoll("fighter", 10, 4, 6), Assert.Single(first.Rolls));
        Assert.Equal(6, first.HitPointsRestored);

        var second = character.ShortRest(new Dictionary<string, int> { ["fighter"] = 2 }, sheet, new FixedDice(10), Now);

        Assert.Equal(28, character.HitPointsCurrent);
        Assert.Equal(12, second.HitPointsRestored);
        Assert.Equal([10], dice.Sides);
        Assert.Equal(0, character.HitDiceRemaining("fighter"));
    }

    [Fact]
    public void Short_rest_healing_per_die_is_never_negative()
    {
        var character = ActiveCharacter([new ClassEntry("wizard", null, 2)], out var sheet, Scores(con: 6));
        character.ApplyCombatUpdate(new CombatUpdate { HitPointsCurrent = 1 }, sheet.HitPointsMax, Now);

        var result = character.ShortRest(new Dictionary<string, int> { ["wizard"] = 1 }, sheet, new FixedDice(1), Now);

        Assert.Equal(0, result.Rolls.Single().Healing);
        Assert.Equal(1, character.HitPointsCurrent);
    }

    [Fact]
    public void Short_rest_resets_short_rest_resources_and_pact_slots_only()
    {
        var character = ActiveCharacter([new ClassEntry("warlock", null, 3), new ClassEntry("fighter", null, 1), new ClassEntry("wizard", null, 1)], out var sheet);
        var secondWind = character.Resources.Single(r => r.Key == "second-wind");
        var arcaneRecovery = character.Resources.Single(r => r.Key == "arcane-recovery");
        character.SpendResource(secondWind.Id, 1, Now);
        character.SpendResource(arcaneRecovery.Id, 1, Now);
        character.SpendSpellSlot(SpellSlotState.PactLevel, 1, sheet.SpellSlotMax(SpellSlotState.PactLevel), Now);
        character.SpendSpellSlot(1, 1, sheet.SpellSlotMax(1), Now);

        character.ShortRest(new Dictionary<string, int>(), sheet, new FixedDice(1), Now);

        Assert.Equal((0, 1), (secondWind.Used, arcaneRecovery.Used));
        Assert.Equal(0, character.SpellSlotsUsed(SpellSlotState.PactLevel));
        Assert.Equal(1, character.SpellSlotsUsed(1));
    }

    [Fact]
    public void Short_rest_rejects_unavailable_hit_dice_without_changing_anything()
    {
        var character = ActiveCharacter([new ClassEntry("fighter", null, 2)], out var sheet);
        character.ApplyCombatUpdate(new CombatUpdate { HitPointsCurrent = 1 }, sheet.HitPointsMax, Now);

        Assert.Throws<DomainException>(() => character.ShortRest(new Dictionary<string, int> { ["fighter"] = 3 }, sheet, new FixedDice(5), Now));
        Assert.Throws<DomainException>(() => character.ShortRest(new Dictionary<string, int> { ["wizard"] = 1 }, sheet, new FixedDice(5), Now));
        Assert.Throws<DomainException>(() => character.ShortRest(new Dictionary<string, int> { ["fighter"] = -1 }, sheet, new FixedDice(5), Now));
        Assert.Equal(1, character.HitPointsCurrent);
        Assert.Equal(2, character.HitDiceRemaining("fighter"));
    }

    private static Dnd5eCharacter ActiveCharacter(IReadOnlyList<ClassEntry> classes, out CharacterSheet sheet, AbilityScores? abilities = null)
    {
        var character = NewCharacter(abilities ?? Scores(con: 12), classes);
        sheet = Sheet(character);
        character.SyncAutoResources(ClassResourceRules.ForClasses(character.Classes, sheet.AbilityModifiers));
        character.Activate(sheet.HitPointsMax, Now);
        return character;
    }
}
