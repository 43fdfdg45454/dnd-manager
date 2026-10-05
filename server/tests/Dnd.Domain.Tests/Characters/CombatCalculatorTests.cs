using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using Dnd.Domain.Common;
using Dnd.Domain.Items;
using Dnd.Domain.Tests.Items;
using static Dnd.Domain.Tests.Characters.TestCatalog;

namespace Dnd.Domain.Tests.Characters;

public class CombatCalculatorTests
{
    private static readonly ItemTemplate Rapier = Weapon("rapier", "Rapier", "Martial Melee", "1d8", "Piercing", properties: ["finesse"]);

    private static readonly ItemTemplate Shortbow = Weapon(
        "shortbow", "Shortbow", "Simple Ranged", "1d6", "Piercing", properties: ["ammunition", "range", "two-handed"], range: (80, 320));

    private static readonly ItemTemplate Handaxe = Weapon(
        "handaxe", "Handaxe", "Simple Melee", "1d6", "Slashing", properties: ["light", "thrown"], range: (20, 60));

    private static readonly ItemTemplate Quarterstaff = Weapon(
        "quarterstaff", "Quarterstaff", "Simple Melee", "1d6", "Bludgeoning", versatile: "1d8", properties: ["versatile"]);

    // ---- Attacks ---------------------------------------------------------------------------------

    [Fact]
    public void Fighter_1_with_Str_16_and_a_longsword_hits_at_plus_5_for_1d8_plus_3()
    {
        var character = NewCharacter(Scores(str: 16), [new ClassEntry("fighter", null, 1)], [WeaponProficiency("martial-weapons")]);

        var attacks = Attacks(character, TestItems.Longsword);

        var sword = attacks[0];
        Assert.Equal(("Longsword", 5, "1d8+3", "1d10+3", "Slashing"), (sword.Name, sword.AttackBonus, sword.Damage, sword.VersatileDamage, sword.DamageType));
        Assert.Equal(["versatile"], sword.Properties);
        Assert.Null(sword.Range);
        Assert.Null(sword.Notes);
        var unarmed = attacks[1];
        Assert.Equal((CombatCalculator.UnarmedStrikeName, 5, "1+3", "Bludgeoning"), (unarmed.Name, unarmed.AttackBonus, unarmed.Damage, unarmed.DamageType));
        Assert.Null(unarmed.ItemId);
    }

    [Fact]
    public void Rogue_with_a_rapier_uses_Dex_through_finesse()
    {
        var character = NewCharacter(Scores(str: 10, dex: 16), [new ClassEntry("rogue", null, 1)], [WeaponProficiency("rapiers")]);

        var rapier = Attacks(character, Rapier)[0];

        Assert.Equal((5, "1d8+3"), (rapier.AttackBonus, rapier.Damage));
    }

    [Fact]
    public void Finesse_keeps_Str_when_it_is_higher()
    {
        var character = NewCharacter(Scores(str: 16, dex: 12), [new ClassEntry("fighter", null, 1)], [WeaponProficiency("martial-weapons")]);

        var rapier = Attacks(character, Rapier)[0];

        Assert.Equal((5, "1d8+3"), (rapier.AttackBonus, rapier.Damage));
    }

    [Fact]
    public void Ranged_weapon_uses_Dex_even_when_Str_is_higher()
    {
        var character = NewCharacter(Scores(str: 16, dex: 12), [new ClassEntry("fighter", null, 1)], [WeaponProficiency("simple-weapons")]);

        var bow = Attacks(character, Shortbow)[0];

        Assert.Equal((3, "1d6+1", "80/320"), (bow.AttackBonus, bow.Damage, bow.Range));
    }

    [Fact]
    public void Thrown_melee_weapon_uses_Str()
    {
        var character = NewCharacter(Scores(str: 14, dex: 18), [new ClassEntry("fighter", null, 1)], [WeaponProficiency("simple-weapons")]);

        var axe = Attacks(character, Handaxe)[0];

        Assert.Equal((4, "1d6+2", "20/60"), (axe.AttackBonus, axe.Damage, axe.Range));
    }

    [Fact]
    public void Weapon_without_proficiency_does_not_add_the_proficiency_bonus()
    {
        var character = NewCharacter(Scores(str: 14), [new ClassEntry("wizard", null, 1)], [WeaponProficiency("daggers")]);

        var sword = Attacks(character, TestItems.Longsword)[0];

        Assert.Equal((2, "1d8+2"), (sword.AttackBonus, sword.Damage));
    }

    [Theory]
    [InlineData("longswords")]
    [InlineData("longsword")]
    [InlineData("Martial Weapons")]
    [InlineData("martial-weapons")]
    public void Proficiency_matches_the_weapon_or_its_category_in_any_accepted_form(string key)
    {
        var character = NewCharacter(Scores(str: 10), [new ClassEntry("rogue", null, 1)], [WeaponProficiency(key)]);

        Assert.Equal(2, Attacks(character, TestItems.Longsword)[0].AttackBonus);
    }

    [Fact]
    public void Plural_dataset_proficiency_of_a_two_word_weapon_matches()
    {
        var crossbow = Weapon("crossbow-light", "Crossbow, light", "Simple Ranged", "1d8", "Piercing", properties: ["ammunition", "loading"]);
        var character = NewCharacter(Scores(dex: 14), [new ClassEntry("wizard", null, 1)], [WeaponProficiency("crossbows-light")]);

        Assert.Equal(4, Attacks(character, crossbow)[0].AttackBonus);
    }

    [Fact]
    public void Item_overrides_add_attack_and_damage_bonuses_and_effects_become_notes()
    {
        var character = NewCharacter(Scores(str: 8), [new ClassEntry("fighter", null, 1)], [WeaponProficiency("martial-weapons")]);
        var magic = new ItemOverrides { AttackBonus = 1, DamageBonus = 0, Effects = ["Brilla", "+1 a ataque"] };

        var sword = CombatCalculator.Attacks(character, Sheet(character), [new EquippedWeapon(Guid.NewGuid(), "longsword", TestItems.Effective(TestItems.Longsword, magic))])[0];

        Assert.Equal((-1 + 2 + 1, "1d8-1", "1d10-1", "Brilla; +1 a ataque"), (sword.AttackBonus, sword.Damage, sword.VersatileDamage, sword.Notes));
    }

    [Fact]
    public void Non_weapons_are_ignored_and_the_unarmed_strike_is_always_last()
    {
        var character = NewCharacter(Scores(str: 10), [new ClassEntry("fighter", null, 1)]);

        var attacks = Attacks(character, TestItems.Shield, TestItems.ChainMail);

        var unarmed = Assert.Single(attacks);
        Assert.Equal((2, "1"), (unarmed.AttackBonus, unarmed.Damage));
    }

    [Theory]
    [InlineData(1, "1d4+3")]
    [InlineData(5, "1d6+3")]
    [InlineData(11, "1d8+3")]
    [InlineData(17, "1d10+3")]
    public void Monk_unarmed_strike_uses_martial_arts_and_Dex(int level, string damage)
    {
        var character = NewCharacter(Scores(str: 10, dex: 16), [new ClassEntry("monk", null, level)]);
        var sheet = Sheet(character);

        var unarmed = Assert.Single(CombatCalculator.Attacks(character, sheet, []));

        Assert.Equal((3 + sheet.ProficiencyBonus, damage), (unarmed.AttackBonus, unarmed.Damage));
    }

    [Fact]
    public void Monk_weapon_uses_Dex_and_the_martial_arts_die_when_larger()
    {
        var character = NewCharacter(Scores(str: 10, dex: 16), [new ClassEntry("monk", null, 11)], [WeaponProficiency("simple-weapons")]);

        var staff = Attacks(character, Quarterstaff)[0];

        Assert.Equal((3 + 4, "1d8+3", "1d8+3"), (staff.AttackBonus, staff.Damage, staff.VersatileDamage));
    }

    [Theory]
    [InlineData("1d8", 3, "1d8+3")]
    [InlineData("1d8", 0, "1d8")]
    [InlineData("1d8", -1, "1d8-1")]
    [InlineData("2d6", 4, "2d6+4")]
    public void Damage_format(string dice, int bonus, string expected) =>
        Assert.Equal(expected, CombatCalculator.FormatDamage(dice, bonus));

    [Theory]
    [InlineData(1, "2d8")]
    [InlineData(2, "3d8")]
    [InlineData(4, "5d8")]
    [InlineData(5, "5d8")]
    public void Divine_smite_dice(int slotLevel, string dice) => Assert.Equal(dice, CombatCalculator.DivineSmiteDice(slotLevel));

    // ---- Class actions ---------------------------------------------------------------------------

    [Fact]
    public void Rage_spends_a_use_and_fails_without_uses_or_without_the_class()
    {
        var barbarian = WithResources(NewCharacter(Scores(), [new ClassEntry("barbarian", null, 1)]));
        var fighter = WithResources(NewCharacter(Scores(), [new ClassEntry("fighter", null, 1)]));

        barbarian.Rage(Now);
        barbarian.Rage(Now);

        Assert.Equal(2, barbarian.Resources.Single(r => r.Key == ClassResourceRules.Rage).Used);
        Assert.Throws<DomainException>(() => barbarian.Rage(Now));
        Assert.Throws<DomainException>(() => fighter.Rage(Now));
    }

    [Fact]
    public void Lay_on_hands_heals_up_to_the_maximum_and_never_exceeds_the_pool()
    {
        var paladin = WithResources(NewCharacter(Scores(con: 10), [new ClassEntry("paladin", null, 2)]));
        var maxHp = Sheet(paladin).HitPointsMax;
        paladin.ApplyCombatUpdate(new CombatUpdate { HitPointsCurrent = maxHp - 3 }, maxHp, Now);

        var healed = paladin.LayOnHands(5, targetSelf: true, maxHp, Now);
        paladin.LayOnHands(4, targetSelf: false, maxHp, Now);

        Assert.Equal(3, healed);
        Assert.Equal(maxHp, paladin.HitPointsCurrent);
        Assert.Equal(9, paladin.Resources.Single(r => r.Key == ClassResourceRules.LayOnHands).Used);
        Assert.Throws<DomainException>(() => paladin.LayOnHands(2, targetSelf: false, maxHp, Now));
        paladin.LayOnHands(1, targetSelf: false, maxHp, Now);
        Assert.Equal(10, paladin.Resources.Single(r => r.Key == ClassResourceRules.LayOnHands).Used);
    }

    [Fact]
    public void Divine_smite_spends_the_slot_and_requires_paladin_2()
    {
        var paladin = WithResources(NewCharacter(Scores(cha: 14), [new ClassEntry("paladin", null, 2)]));
        var novice = WithResources(NewCharacter(Scores(cha: 14), [new ClassEntry("paladin", null, 1), new ClassEntry("wizard", null, 1)]));

        var dice = paladin.DivineSmite(1, Sheet(paladin).SpellSlotMax(1), Now);

        Assert.Equal("2d8", dice);
        Assert.Equal(1, paladin.SpellSlotsUsed(1));
        Assert.Throws<DomainException>(() => novice.DivineSmite(1, Sheet(novice).SpellSlotMax(1), Now));
    }

    [Fact]
    public void Arcane_recovery_restores_slots_once_and_validates_the_levels()
    {
        var wizard = WithResources(NewCharacter(Scores(@int: 16), [new ClassEntry("wizard", null, 5)]));
        var sheet = Sheet(wizard);
        wizard.SpendSpellSlot(1, 2, sheet.SpellSlotMax(1), Now);
        wizard.SpendSpellSlot(2, 1, sheet.SpellSlotMax(2), Now);

        Assert.Throws<DomainException>(() => wizard.ArcaneRecovery([2, 2], Now)); // 4 > ceil(5 / 2)
        Assert.Throws<DomainException>(() => wizard.ArcaneRecovery([1, 1, 1], Now)); // only 2 spent
        Assert.Throws<DomainException>(() => wizard.ArcaneRecovery([], Now));
        wizard.ArcaneRecovery([2, 1], Now);

        Assert.Equal((1, 0), (wizard.SpellSlotsUsed(1), wizard.SpellSlotsUsed(2)));
        Assert.Equal(1, wizard.Resources.Single(r => r.Key == ClassResourceRules.ArcaneRecovery).Used);
        Assert.Throws<DomainException>(() => wizard.ArcaneRecovery([1], Now));
    }

    [Fact]
    public void Arcane_recovery_never_recovers_slots_above_5th_level()
    {
        var wizard = WithResources(NewCharacter(Scores(@int: 16), [new ClassEntry("wizard", null, 20)]));
        var sheet = Sheet(wizard);
        wizard.SpendSpellSlot(6, 1, sheet.SpellSlotMax(6), Now);

        var error = Assert.Throws<DomainException>(() => wizard.ArcaneRecovery([6], Now));

        Assert.Contains("5", error.Message, StringComparison.Ordinal);
    }

    // ---- Helpers -----------------------------------------------------------------------------------

    private static IReadOnlyList<AttackValue> Attacks(Character character, params ItemTemplate[] equipped) =>
        CombatCalculator.Attacks(character, Sheet(character), equipped.Select(t => new EquippedWeapon(Guid.NewGuid(), t.Index, TestItems.Effective(t))));

    private static ProficiencyEntry WeaponProficiency(string key) => new(ProficiencyType.Weapon, key, false, ProficiencySource.Class);

    private static Character WithResources(Character character)
    {
        character.SyncAutoResources(ClassResourceRules.ForClasses(character.Classes, Sheet(character).AbilityModifiers));
        return character;
    }

    private static ItemTemplate Weapon(
        string index,
        string name,
        string subcategory,
        string dice,
        string damageType,
        string? versatile = null,
        IReadOnlyList<string>? properties = null,
        (int Normal, int Long)? range = null) =>
        ItemTemplate.CreateSrd(index, new ItemTemplateData
        {
            Name = name,
            Category = ItemCategory.Weapon,
            Subcategory = subcategory,
            DamageDice = dice,
            DamageType = damageType,
            VersatileDice = versatile,
            Properties = properties ?? [],
            RangeNormal = range?.Normal,
            RangeLong = range?.Long,
        }, Now);
}
