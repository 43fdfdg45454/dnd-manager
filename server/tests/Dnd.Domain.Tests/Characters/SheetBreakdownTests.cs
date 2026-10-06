using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;
using Dnd.Domain.Items;
using Dnd.Domain.Tests.Items;
using static Dnd.Domain.Tests.Characters.TestCatalog;

namespace Dnd.Domain.Tests.Characters;

public class SheetBreakdownTests
{
    private static readonly ItemTemplate CloakOfProtection = ItemTemplate.CreateSrd("cloak-of-protection", new ItemTemplateData
    {
        Name = "Cloak of Protection",
        Category = ItemCategory.MagicItem,
        RequiresAttunement = true,
        Modifiers = [new ItemModifier(ItemModifierKind.ArmorClassBonus, null, 1), new ItemModifier(ItemModifierKind.SaveBonus, null, 1)],
    }, Now);

    private static readonly ItemTemplate AmuletOfHealth = ItemTemplate.CreateSrd("amulet-of-health", new ItemTemplateData
    {
        Name = "Amulet of Health",
        Category = ItemCategory.MagicItem,
        RequiresAttunement = true,
        Modifiers = [new ItemModifier(ItemModifierKind.AbilitySet, "con", 19)],
    }, Now);

    [Fact]
    public void Armor_class_with_armor_shield_cloak_and_override_is_explained_point_by_point()
    {
        var character = NewCharacter(Scores(dex: 14), overrides: [new OverrideEntry(OverrideFields.ArmorClass, 20, "Bendición")]);
        var gear = Gear((TestItems.ChainMail, false), (TestItems.Shield, false), (CloakOfProtection, true));

        var sheet = Sheet(character, gear: gear);

        Assert.Equal(20, sheet.ArmorClass);
        Assert.Equal(
            "20 = armor:Chain Mail 16, shield:Shield 2, item:Cloak of Protection 1, override:Ajuste manual: Bendición 1",
            ItemModifierTests.Text(sheet.Breakdowns[OverrideFields.ArmorClass]));
        Assert.Equal("3 = ability:Destreza 2, item:Cloak of Protection 1", ItemModifierTests.Text(sheet.Breakdowns["save.dex"]));
    }

    [Fact]
    public void Armor_class_without_override_has_no_override_part()
    {
        var sheet = Sheet(NewCharacter(Scores(dex: 14)), gear: Gear((TestItems.Leather, false), (CloakOfProtection, true)));

        Assert.Equal("14 = armor:Leather Armor 11, ability:Destreza 2, item:Cloak of Protection 1", ItemModifierTests.Text(sheet.Breakdowns["armorClass"]));
    }

    [Fact]
    public void Ability_score_with_race_subrace_and_an_item_set()
    {
        var character = NewCharacter(Scores(con: 12, wis: 10));

        var sheet = Sheet(character, Dwarf, HillDwarf, Gear((AmuletOfHealth, true)));

        Assert.Equal(19, sheet.Abilities["con"].Score);
        Assert.Equal("19 = base:Puntuación base 12, race:Raza 2, item:Amulet of Health 5", ItemModifierTests.Text(sheet.Breakdowns["ability.con"]));
        Assert.Equal("11 = base:Puntuación base 10, subrace:Subraza 1", ItemModifierTests.Text(sheet.Breakdowns["ability.wis"]));
        Assert.Equal("25 = race:Raza 25", ItemModifierTests.Text(sheet.Breakdowns["speed"]));
    }

    [Fact]
    public void Skill_with_expertise_counts_proficiency_twice()
    {
        var character = NewCharacter(
            Scores(dex: 16),
            [new ClassEntry("rogue", null, 1)],
            [new ProficiencyEntry(ProficiencyType.Skill, "stealth", Expertise: true)]);

        var sheet = Sheet(character);

        Assert.Equal(7, sheet.Skills.Single(s => s.Index == "stealth").Value);
        Assert.Equal("7 = ability:Destreza 3, proficiency:Competencia 2, expertise:Pericia 2", ItemModifierTests.Text(sheet.Breakdowns["skill.stealth"]));
    }

    [Fact]
    public void Override_of_an_ability_is_the_last_part()
    {
        var character = NewCharacter(Scores(str: 12), overrides: [new OverrideEntry(OverrideFields.Ability("str"), 15)]);

        Assert.Equal("15 = base:Puntuación base 12, override:Ajuste manual 3", ItemModifierTests.Text(Sheet(character).Breakdowns["ability.str"]));
    }

    [Fact]
    public void Every_breakdown_adds_up_and_matches_the_sheet()
    {
        var character = NewCharacter(
            Scores(str: 14, dex: 14, con: 16, @int: 16, wis: 12, cha: 8),
            [new ClassEntry("barbarian", null, 3), new ClassEntry("wizard", null, 2)],
            [new ProficiencyEntry(ProficiencyType.Skill, "perception")],
            [new OverrideEntry(OverrideFields.SpellSaveDc, 15, "Objeto")]);

        var sheet = Sheet(character, Dwarf, HillDwarf, Gear((CloakOfProtection, true)));

        Assert.All(sheet.Breakdowns.Values, b => Assert.Equal(b.Total, b.Parts.Sum(p => p.Value)));
        Assert.Equal(sheet.ArmorClass, sheet.Breakdowns["armorClass"].Total);
        Assert.Contains(sheet.Breakdowns["armorClass"].Parts, p => p.Source == BreakdownSources.Class && p.Value == 4);
        Assert.Equal(sheet.HitPointsMax, sheet.Breakdowns["hitPointsMax"].Total);
        Assert.Equal(sheet.Initiative, sheet.Breakdowns["initiative"].Total);
        Assert.Equal(sheet.PassivePerception, sheet.Breakdowns["passivePerception"].Total);
        Assert.Equal(sheet.ProficiencyBonus, sheet.Breakdowns["proficiencyBonus"].Total);
        Assert.Equal(sheet.Speed, sheet.Breakdowns["speed"].Total);
        Assert.All(Abilities.All, a =>
        {
            Assert.Equal(sheet.Abilities[a].Score, sheet.Breakdowns[$"ability.{a}"].Total);
            Assert.Equal(sheet.SavingThrows[a].Value, sheet.Breakdowns[$"save.{a}"].Total);
        });
        Assert.All(sheet.Skills, s => Assert.Equal(s.Value, sheet.Breakdowns[$"skill.{s.Index}"].Total));
        var wizard = sheet.Spellcasting.Single();
        Assert.Equal((15, wizard.AttackBonus), (sheet.Breakdowns["spellSaveDc.wizard"].Total, sheet.Breakdowns["spellAttackBonus.wizard"].Total));
        Assert.Equal(BreakdownSources.Override, sheet.Breakdowns["spellSaveDc.wizard"].Parts[^1].Source);
        Assert.Equal(
            ["class", "class", "ability"],
            sheet.Breakdowns["hitPointsMax"].Parts.Select(p => p.Source));
    }

    private static EquippedGear Gear(params (ItemTemplate Template, bool Attuned)[] equipped) =>
        EquippedGear.FromEquipped(equipped.Select(e => (TestItems.Effective(e.Template), e.Attuned)));
}
