using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using Dnd.Domain.Common;
using Dnd.Domain.Items;
using static Dnd.Domain.Tests.Characters.TestCatalog;

namespace Dnd.Domain.Tests.Items;

public class ItemModifierTests
{
    // ---- Validation ------------------------------------------------------------------------------

    [Fact]
    public void Normalize_trims_and_lowercases_the_target()
    {
        Assert.Equal(new ItemModifier(ItemModifierKind.AbilityBonus, "dex", 3), new ItemModifier(ItemModifierKind.AbilityBonus, " DEX ", 3).Normalize());
        Assert.Equal(new ItemModifier(ItemModifierKind.SaveBonus, null, 1), new ItemModifier(ItemModifierKind.SaveBonus, "  ", 1).Normalize());
        Assert.Equal("sleight-of-hand", new ItemModifier(ItemModifierKind.SkillBonus, "Sleight-Of-Hand", 2).Normalize().Target);
    }

    [Theory]
    [InlineData(ItemModifierKind.AbilityBonus, null, 1)]
    [InlineData(ItemModifierKind.AbilityBonus, "luck", 1)]
    [InlineData(ItemModifierKind.AbilitySet, null, 19)]
    [InlineData(ItemModifierKind.AbilitySet, "str", 0)]
    [InlineData(ItemModifierKind.AbilitySet, "str", 31)]
    [InlineData(ItemModifierKind.SaveBonus, "stealth", 1)]
    [InlineData(ItemModifierKind.ArmorClassBonus, "dex", 1)]
    [InlineData(ItemModifierKind.ArmorClassBonus, null, -11)]
    [InlineData(ItemModifierKind.SpeedBonus, null, 31)]
    [InlineData((ItemModifierKind)99, null, 1)]
    public void Invalid_modifiers_are_rejected(ItemModifierKind kind, string? target, int value)
    {
        var modifier = new ItemModifier(kind, target, value);

        Assert.NotNull(modifier.Validate());
        Assert.Throws<DomainException>(() => modifier.Normalize());
    }

    [Theory]
    [InlineData(ItemModifierKind.AbilitySet, "str", 1)]
    [InlineData(ItemModifierKind.AbilitySet, "str", 30)]
    [InlineData(ItemModifierKind.SaveBonus, null, 1)]
    [InlineData(ItemModifierKind.SaveBonus, "wis", -10)]
    [InlineData(ItemModifierKind.SkillBonus, null, 2)]
    [InlineData(ItemModifierKind.SkillBonus, "stealth", 2)]
    [InlineData(ItemModifierKind.HitPointsMaxBonus, null, 30)]
    public void Valid_modifiers_pass(ItemModifierKind kind, string? target, int value) =>
        Assert.Null(new ItemModifier(kind, target, value).Validate());

    [Fact]
    public void An_item_has_at_most_ten_modifiers()
    {
        var eleven = Enumerable.Repeat(new ItemModifier(ItemModifierKind.SpeedBonus, null, 5), ItemLimits.MaxModifiers + 1).ToList();

        Assert.Throws<DomainException>(() => ItemModifier.NormalizeAll(eleven));
        Assert.Throws<DomainException>(() => new ItemOverrides { Modifiers = eleven }.Normalize());
        Assert.Throws<DomainException>(() => Template("Demasiado", ItemCategory.MagicItem, eleven));
    }

    // ---- Effective item --------------------------------------------------------------------------

    [Fact]
    public void Overrides_keep_replace_or_remove_the_template_modifiers()
    {
        var template = Template("Guantes", ItemCategory.MagicItem, [new ItemModifier(ItemModifierKind.AbilityBonus, "dex", 3)]);

        Assert.Equal(template.Modifiers, EffectiveItem.Resolve(template, ItemOverrides.None()).Modifiers);
        Assert.Empty(EffectiveItem.Resolve(template, new ItemOverrides { Modifiers = [] }).Modifiers);
        var replaced = new ItemOverrides { Modifiers = [new ItemModifier(ItemModifierKind.AbilityBonus, "str", 1)] };
        Assert.Equal(replaced.Modifiers, EffectiveItem.Resolve(template, replaced).Modifiers);
        Assert.True(EffectiveItem.Resolve(template, new ItemOverrides { Modifiers = [] }).IsCustom);
    }

    [Fact]
    public void An_empty_modifier_list_survives_normalization_and_copy()
    {
        var overrides = new ItemOverrides { Modifiers = [] };

        Assert.False(overrides.IsEmpty);
        Assert.NotNull(overrides.Normalize().Modifiers);
        Assert.Empty(overrides.Copy().Modifiers!);
        Assert.Null(ItemOverrides.None().Normalize().Modifiers);
    }

    [Fact]
    public void Legacy_attack_and_damage_bonuses_become_modifiers()
    {
        var item = EffectiveItem.Resolve(TestItems.Longsword, new ItemOverrides { AttackBonus = 1, DamageBonus = 2 });

        Assert.Equal(
            [new ItemModifier(ItemModifierKind.AttackBonus, null, 1), new ItemModifier(ItemModifierKind.DamageBonus, null, 2)],
            item.Modifiers);
        Assert.Equal((1, 2), (item.AttackBonus, item.DamageBonus));
    }

    [Fact]
    public void Magic_items_are_equippable_but_gear_and_consumables_are_not()
    {
        Assert.True(TestItems.Effective(TestItems.Attuned("Ring")).IsEquippable);
        Assert.False(TestItems.Effective(TestItems.Rope).IsEquippable);
        Assert.False(TestItems.Effective(Template("Poción", ItemCategory.Consumable, [])).IsEquippable);
    }

    [Fact]
    public void An_item_is_active_when_equipped_and_attuned_if_it_requires_attunement()
    {
        var character = NewCharacter();
        var ring = Template("Anillo", ItemCategory.MagicItem, [], requiresAttunement: true);
        var cloak = Template("Capa", ItemCategory.MagicItem, []);
        var ringEntry = character.AddItem(ring.Id, ItemOverrides.None(), 1, TestItems.Effective(ring), Now);
        var cloakEntry = character.AddItem(cloak.Id, ItemOverrides.None(), 1, TestItems.Effective(cloak), Now);
        EffectiveItem Resolve(CharacterItem i) => i.TemplateId == ring.Id ? TestItems.Effective(ring) : TestItems.Effective(cloak);

        Assert.False(TestItems.Effective(cloak).IsActiveFor(cloakEntry));
        character.UpdateItem(cloakEntry.Id, new ItemUpdate { Equipped = true }, Resolve, Now);
        character.UpdateItem(ringEntry.Id, new ItemUpdate { Equipped = true }, Resolve, Now);
        Assert.True(TestItems.Effective(cloak).IsActiveFor(cloakEntry));
        Assert.False(TestItems.Effective(ring).IsActiveFor(ringEntry));

        character.UpdateItem(ringEntry.Id, new ItemUpdate { Attuned = true }, Resolve, Now);
        Assert.True(TestItems.Effective(ring).IsActiveFor(ringEntry));
    }

    // ---- Sheet -----------------------------------------------------------------------------------

    [Fact]
    public void Ability_bonus_adds_to_the_score()
    {
        var character = NewCharacter(Scores(dex: 14));
        var gloves = Template("Guantes ágiles", ItemCategory.MagicItem, [new ItemModifier(ItemModifierKind.AbilityBonus, "dex", 3)]);

        var sheet = Sheet(character, gear: Gear((gloves, false)));

        Assert.Equal((17, 3), (sheet.Abilities["dex"].Score, sheet.Abilities["dex"].Modifier));
        Assert.False(sheet.Abilities["dex"].Overridden);
        Assert.Equal(3, sheet.Initiative);
        Assert.Equal([new AppliedItemEffect("Guantes ágiles", ItemModifierKind.AbilityBonus, "dex", 3)], sheet.ItemEffects);
    }

    [Fact]
    public void Ability_set_raises_a_lower_score_and_ignores_a_higher_one()
    {
        var gauntlets = Template("Gauntlets of Ogre Power", ItemCategory.MagicItem, [new ItemModifier(ItemModifierKind.AbilitySet, "str", 19)]);

        var weak = Sheet(NewCharacter(Scores(str: 12)), gear: Gear((gauntlets, false)));
        var strong = Sheet(NewCharacter(Scores(str: 20)), gear: Gear((gauntlets, false)));

        Assert.Equal(19, weak.Abilities["str"].Score);
        Assert.Single(weak.ItemEffects);
        Assert.Equal(20, strong.Abilities["str"].Score);
        Assert.Empty(strong.ItemEffects);
    }

    [Fact]
    public void Ability_override_still_wins_over_items()
    {
        var character = NewCharacter(Scores(dex: 14), overrides: [new OverrideEntry(OverrideFields.Ability("dex"), 10)]);
        var gloves = Template("Guantes", ItemCategory.MagicItem, [new ItemModifier(ItemModifierKind.AbilityBonus, "dex", 3)]);

        Assert.Equal(10, Sheet(character, gear: Gear((gloves, false))).Abilities["dex"].Score);
    }

    [Fact]
    public void An_item_that_requires_attunement_applies_only_when_attuned()
    {
        var character = NewCharacter(Scores(str: 10));
        var belt = Template("Belt", ItemCategory.MagicItem, [new ItemModifier(ItemModifierKind.AbilitySet, "str", 21)], requiresAttunement: true);

        Assert.Equal(10, Sheet(character, gear: Gear((belt, false))).Abilities["str"].Score);
        Assert.Equal(21, Sheet(character, gear: Gear((belt, true))).Abilities["str"].Score);
    }

    [Fact]
    public void A_shield_adds_its_own_armor_class()
    {
        var character = NewCharacter(Scores(dex: 10));
        var shieldPlusOne = Template("Shield +1", ItemCategory.Shield, [], armorClassBase: 3);

        Assert.Equal(13, Sheet(character, gear: Gear((shieldPlusOne, false))).ArmorClass);
        Assert.Equal(12, Sheet(character, gear: Gear((Template("Escudo sin dato", ItemCategory.Shield, []), false))).ArmorClass);
        Assert.Equal(12, Sheet(character, gear: Gear((TestItems.Shield, false))).ArmorClass);
    }

    [Fact]
    public void Armor_class_bonus_applies_with_and_without_armor()
    {
        var character = NewCharacter(Scores(dex: 14));
        var ring = Template("Ring of Protection", ItemCategory.MagicItem, [new ItemModifier(ItemModifierKind.ArmorClassBonus, null, 1)]);

        Assert.Equal(10 + 2 + 1, Sheet(character, gear: Gear((ring, false))).ArmorClass);
        Assert.Equal(16 + 1, Sheet(character, gear: Gear((TestItems.ChainMail, false), (ring, false))).ArmorClass);
    }

    [Fact]
    public void Save_bonus_without_target_applies_to_the_six_saves()
    {
        var character = NewCharacter();
        var cloak = Template("Cloak of Protection", ItemCategory.MagicItem, [new ItemModifier(ItemModifierKind.SaveBonus, null, 1)]);
        var wisdom = Template("Amuleto", ItemCategory.MagicItem, [new ItemModifier(ItemModifierKind.SaveBonus, "wis", 2)]);

        var sheet = Sheet(character, gear: Gear((cloak, false), (wisdom, false)));

        Assert.All(Abilities.All.Where(a => a != "wis"), a => Assert.Equal(1, sheet.SavingThrows[a].Value));
        Assert.Equal(3, sheet.SavingThrows["wis"].Value);
    }

    [Fact]
    public void Skill_speed_and_initiative_bonuses_apply()
    {
        var character = NewCharacter();
        var boots = Template("Botas", ItemCategory.MagicItem,
        [
            new ItemModifier(ItemModifierKind.SkillBonus, "stealth", 5),
            new ItemModifier(ItemModifierKind.SkillBonus, null, 1),
            new ItemModifier(ItemModifierKind.SpeedBonus, null, 10),
            new ItemModifier(ItemModifierKind.InitiativeBonus, null, 2),
        ]);

        var sheet = Sheet(character, gear: Gear((boots, false)));

        Assert.Equal(6, sheet.Skills.Single(s => s.Index == "stealth").Value);
        Assert.Equal(1, sheet.Skills.Single(s => s.Index == "athletics").Value);
        Assert.Equal(11, sheet.PassivePerception);
        Assert.Equal(40, sheet.Speed);
        Assert.Equal(2, sheet.Initiative);
    }

    [Fact]
    public void Hit_points_max_bonus_applies_on_top_of_the_override()
    {
        var character = NewCharacter(classes: [new ClassEntry("fighter", null, 3)], overrides: [new OverrideEntry(OverrideFields.HitPointsMax, 31)]);
        var amulet = Template("Amuleto vital", ItemCategory.MagicItem, [new ItemModifier(ItemModifierKind.HitPointsMaxBonus, null, 5)]);

        Assert.Equal(36, Sheet(character, gear: Gear((amulet, false))).HitPointsMax);
        Assert.True(Sheet(character, gear: Gear((amulet, false))).IsOverridden(OverrideFields.HitPointsMax));
    }

    [Fact]
    public void Weapon_attack_bonuses_stay_out_of_the_sheet_effects()
    {
        var sword = Template("Espada +1", ItemCategory.Weapon,
            [new ItemModifier(ItemModifierKind.AttackBonus, null, 1), new ItemModifier(ItemModifierKind.DamageBonus, null, 1)]);
        var ring = Template("Anillo", ItemCategory.MagicItem, [new ItemModifier(ItemModifierKind.AttackBonus, null, 1)]);

        var sheet = Sheet(NewCharacter(), gear: Gear((sword, false), (ring, false)));

        Assert.Equal([new AppliedItemEffect("Anillo", ItemModifierKind.AttackBonus, null, 1)], sheet.ItemEffects);
    }

    // ---- Combat ----------------------------------------------------------------------------------

    [Fact]
    public void A_plus_one_weapon_and_a_plus_one_ring_add_up_with_breakdowns()
    {
        var character = NewCharacter(
            Scores(dex: 16),
            [new ClassEntry("fighter", null, 1)],
            [new ProficiencyEntry(ProficiencyType.Weapon, "martial-weapons", false, ProficiencySource.Class)]);
        var sword = Template("Espada", ItemCategory.Weapon,
            [new ItemModifier(ItemModifierKind.AttackBonus, null, 1), new ItemModifier(ItemModifierKind.DamageBonus, null, 1)],
            damageDice: "1d8", properties: ["finesse"], subcategory: "Martial Melee");
        var ring = Template("Anillo", ItemCategory.MagicItem,
            [new ItemModifier(ItemModifierKind.AttackBonus, null, 1), new ItemModifier(ItemModifierKind.DamageBonus, null, 1)]);
        var sheet = Sheet(character, gear: Gear((sword, false), (ring, false)));

        var attacks = CombatCalculator.Attacks(
            character, sheet, [new EquippedWeapon(Guid.NewGuid(), null, TestItems.Effective(sword)), new EquippedWeapon(Guid.NewGuid(), null, TestItems.Effective(ring))]);

        var attack = attacks[0];
        Assert.Equal((3 + 2 + 1 + 1, "1d8+5"), (attack.AttackBonus, attack.Damage));
        Assert.Equal("7 = ability:Destreza 3, proficiency:Competencia 2, item:Espada 1, item:Anillo 1", Text(attack.AttackBreakdown));
        Assert.Equal("5 = ability:Destreza 3, item:Espada 1, item:Anillo 1", Text(attack.DamageBreakdown));
        var unarmed = attacks[1];
        Assert.Equal((0 + 2 + 1, "1+1"), (unarmed.AttackBonus, unarmed.Damage));
        Assert.Equal("3 = ability:Fuerza 0, proficiency:Competencia 2, item:Anillo 1", Text(unarmed.AttackBreakdown));
        Assert.Equal("1 = ability:Fuerza 0, item:Anillo 1", Text(unarmed.DamageBreakdown));
    }

    [Fact]
    public void A_weapon_that_requires_attunement_needs_it_for_its_bonus()
    {
        var character = NewCharacter(Scores(str: 14), [new ClassEntry("fighter", null, 1)]);
        var blade = Template("Hoja", ItemCategory.Weapon, [new ItemModifier(ItemModifierKind.AttackBonus, null, 3)], damageDice: "1d8", requiresAttunement: true);

        var unattuned = CombatCalculator.Attacks(character, Sheet(character), [new EquippedWeapon(null, null, TestItems.Effective(blade))])[0];
        var attuned = CombatCalculator.Attacks(character, Sheet(character), [new EquippedWeapon(null, null, TestItems.Effective(blade), Attuned: true)])[0];

        Assert.Equal((2, "2 = ability:Fuerza 2"), (unattuned.AttackBonus, Text(unattuned.AttackBreakdown)));
        Assert.Equal((5, "5 = ability:Fuerza 2, item:Hoja 3"), (attuned.AttackBonus, Text(attuned.AttackBreakdown)));
    }

    /// <summary>"7 = ability:Destreza 3, proficiency:Competencia 2, ..."; also checks that the parts add up.</summary>
    internal static string Text(ValueBreakdown breakdown)
    {
        Assert.Equal(breakdown.Total, breakdown.Parts.Sum(p => p.Value));
        return $"{breakdown.Total} = {string.Join(", ", breakdown.Parts.Select(p => $"{p.Source}:{p.Label} {p.Value}"))}";
    }

    private static EquippedGear Gear(params (ItemTemplate Template, bool Attuned)[] equipped) =>
        EquippedGear.FromEquipped(equipped.Select(e => (TestItems.Effective(e.Template), e.Attuned)));

    private static ItemTemplate Template(
        string name,
        ItemCategory category,
        IReadOnlyList<ItemModifier> modifiers,
        bool requiresAttunement = false,
        int? armorClassBase = null,
        string? damageDice = null,
        IReadOnlyList<string>? properties = null,
        string subcategory = "") =>
        ItemTemplate.CreateHomebrew(Guid.NewGuid(), new ItemTemplateData
        {
            Name = name,
            Category = category,
            Subcategory = subcategory,
            RequiresAttunement = requiresAttunement,
            ArmorClassBase = armorClassBase,
            DamageDice = damageDice,
            Properties = properties ?? [],
            Modifiers = modifiers,
        }, Now);
}
