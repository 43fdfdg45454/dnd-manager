using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;
using static OpenTrpg.Core.Domain.Tests.Characters.TestCatalog;

namespace OpenTrpg.Core.Domain.Tests.Characters;

public class DamageAndHealingTests
{
    [Fact]
    public void Damage_spends_temporary_hit_points_first()
    {
        var character = ActiveFighter(out var sheet);
        character.ApplyCombatUpdate(new CombatUpdate { TemporaryHitPoints = 5 }, sheet.HitPointsMax, Now);

        character.ApplyDamage(8, Now);

        Assert.Equal((0, sheet.HitPointsMax - 3), (character.TemporaryHitPoints, character.HitPointsCurrent));
    }

    [Fact]
    public void Damage_only_lowers_temporary_hit_points_when_they_cover_it()
    {
        var character = ActiveFighter(out var sheet);
        character.ApplyCombatUpdate(new CombatUpdate { TemporaryHitPoints = 10 }, sheet.HitPointsMax, Now);

        character.ApplyDamage(4, Now);

        Assert.Equal((6, sheet.HitPointsMax), (character.TemporaryHitPoints, character.HitPointsCurrent));
    }

    [Fact]
    public void Damage_never_goes_below_zero_and_cannot_be_negative()
    {
        var character = ActiveFighter(out _);

        character.ApplyDamage(500, Now);

        Assert.Equal(0, character.HitPointsCurrent);
        Assert.Equal(DomainErrorKind.RuleViolation, Assert.Throws<DomainException>(() => character.ApplyDamage(-1, Now)).Kind);
    }

    [Fact]
    public void Healing_is_capped_at_the_maximum()
    {
        var character = ActiveFighter(out var sheet);
        character.ApplyDamage(5, Now);

        character.Heal(100, sheet.HitPointsMax, Now);

        Assert.Equal(sheet.HitPointsMax, character.HitPointsCurrent);
        Assert.Throws<DomainException>(() => character.Heal(-1, sheet.HitPointsMax, Now));
    }

    [Fact]
    public void Healing_from_zero_resets_the_death_saves()
    {
        var character = ActiveFighter(out var sheet);
        character.ApplyDamage(sheet.HitPointsMax, Now);
        character.ApplyCombatUpdate(new CombatUpdate { DeathSaveSuccesses = 1, DeathSaveFailures = 2 }, sheet.HitPointsMax, Now);

        character.Heal(3, sheet.HitPointsMax, Now);

        Assert.Equal((3, 0, 0), (character.HitPointsCurrent, character.DeathSaveSuccesses, character.DeathSaveFailures));
    }

    [Fact]
    public void Healing_above_zero_keeps_the_death_saves()
    {
        var character = ActiveFighter(out var sheet);
        character.ApplyCombatUpdate(new CombatUpdate { HitPointsCurrent = 1, DeathSaveFailures = 1 }, sheet.HitPointsMax, Now);

        character.Heal(2, sheet.HitPointsMax, Now);

        Assert.Equal((3, 1), (character.HitPointsCurrent, character.DeathSaveFailures));
    }

    private static Dnd5eCharacter ActiveFighter(out CharacterSheet sheet)
    {
        var character = NewCharacter(Scores(con: 14), [new ClassEntry("fighter", null, 3)]);
        sheet = Sheet(character);
        character.Activate(sheet.HitPointsMax, Now);
        return character;
    }
}
