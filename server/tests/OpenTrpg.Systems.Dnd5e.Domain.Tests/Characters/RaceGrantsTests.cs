using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using static OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters.TestCatalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Domain.Tests.Characters;

/// <summary>Phase 25, block 1: subrace speed, fixed race and subrace grants and racial spells by total level.</summary>
public sealed class RaceGrantsTests
{
    private static readonly RaceInfo MountainFolk = new(25, []) { Name = "Mountain Folk" };

    private static OptionGrants Grants(
        IReadOnlyList<string>? skills = null,
        IReadOnlyList<string>? weapons = null,
        IReadOnlyList<string>? savingThrows = null,
        IReadOnlyList<string>? cantrips = null,
        IReadOnlyList<GrantedSpell>? spells = null,
        string? ability = null) =>
        new(skills ?? [], cantrips ?? [], spells ?? [], [], weapons ?? [], [], [], savingThrows ?? []) { SpellcastingAbility = ability };

    private static Dnd5eCharacter Fighter(int level, string race = "mountain-folk", string? subrace = null, int cha = 10)
    {
        var character = NewCharacter(Scores(cha: cha), [new ClassEntry("fighter", null, level)]);
        character.ApplySheetEdit(new SheetEdit { RaceIndex = race, SubraceIndex = subrace }, Now);
        return character;
    }

    [Fact]
    public void Subrace_speed_replaces_the_race_speed_with_its_breakdown()
    {
        var subrace = new SubraceInfo([]) { Name = "Swift Folk", Speed = 35 };

        var sheet = Sheet(Fighter(1), MountainFolk, subrace);

        Assert.Equal(35, sheet.Speed);
        var parts = sheet.Breakdowns[OverrideFields.Speed].Parts;
        Assert.Equal(new BreakdownPart(BreakdownSources.Race, BreakdownLabels.Race, 25), parts[0]);
        Assert.Equal(new BreakdownPart(BreakdownSources.Subrace, "Swift Folk", 10), parts[1]);
    }

    [Fact]
    public void Subrace_without_speed_keeps_the_race_speed()
    {
        var sheet = Sheet(Fighter(1), MountainFolk, new SubraceInfo([]) { Name = "Plain Folk" });

        Assert.Equal(25, sheet.Speed);
        Assert.Single(sheet.Breakdowns[OverrideFields.Speed].Parts);
    }

    [Fact]
    public void Race_and_subrace_grants_become_race_proficiencies_and_are_removed_when_the_race_changes()
    {
        var character = Fighter(1, subrace: "mountain-folk-deep");

        var changed = character.SyncRaceGrants(
            Grants(skills: ["perception"], weapons: ["battleaxes"]),
            Grants(savingThrows: ["con"]),
            Now);

        Assert.True(changed);
        Assert.Equal(ProficiencySource.Race, character.FindProficiency(ProficiencyType.Skill, "perception")!.Source);
        Assert.Equal(ProficiencySource.Race, character.FindProficiency(ProficiencyType.Weapon, "battleaxes")!.Source);
        Assert.Equal(ProficiencySource.Race, character.FindProficiency(ProficiencyType.SavingThrow, "con")!.Source);
        Assert.NotNull(character.OriginChoice("race.grant.skills"));
        Assert.NotNull(character.OriginChoice("race.subrace.grant.saving-throws"));

        // Same grants again: nothing changes.
        Assert.False(character.SyncRaceGrants(Grants(skills: ["perception"], weapons: ["battleaxes"]), Grants(savingThrows: ["con"]), Now));

        character.ApplySheetEdit(new SheetEdit { RaceIndex = "other-folk" }, Now);

        Assert.Null(character.FindProficiency(ProficiencyType.Skill, "perception"));
        Assert.Null(character.FindProficiency(ProficiencyType.Weapon, "battleaxes"));
        Assert.Null(character.FindProficiency(ProficiencyType.SavingThrow, "con"));
        Assert.DoesNotContain(character.OriginChoices, c => OriginChoiceKeys.IsGrant(c.Key));
    }

    [Fact]
    public void A_grant_does_not_take_over_a_proficiency_the_class_already_gave()
    {
        var character = NewCharacter(
            classes: [new ClassEntry("fighter", null, 1)],
            proficiencies: [new ProficiencyEntry(ProficiencyType.Skill, "perception", false, ProficiencySource.Class)]);
        character.ApplySheetEdit(new SheetEdit { RaceIndex = "mountain-folk" }, Now);

        character.SyncRaceGrants(Grants(skills: ["perception"]), null, Now);
        character.SyncRaceGrants(null, null, Now);

        Assert.Equal(ProficiencySource.Class, character.FindProficiency(ProficiencyType.Skill, "perception")!.Source);
    }

    [Fact]
    public void Removed_subrace_grants_are_undone()
    {
        var character = Fighter(1, subrace: "mountain-folk-deep");
        character.SyncRaceGrants(null, Grants(weapons: ["warpicks"]), Now);
        Assert.NotNull(character.FindProficiency(ProficiencyType.Weapon, "warpicks"));

        // The pack that added the subrace was uninstalled: the subrace has no grants any more.
        Assert.True(character.SyncRaceGrants(null, null, Now));

        Assert.Null(character.FindProficiency(ProficiencyType.Weapon, "warpicks"));
    }

    [Fact]
    public void Racial_spells_are_granted_by_total_level_always_prepared_without_class()
    {
        var grants = Grants(
            cantrips: ["glimmer"],
            spells: [new GrantedSpell("veil-of-dusk", 3), new GrantedSpell("deep-shadow", 5)],
            ability: "cha");
        var character = NewCharacter(classes: [new ClassEntry("fighter", null, 2), new ClassEntry("rogue", null, 1)]);
        character.ApplySheetEdit(new SheetEdit { RaceIndex = "mountain-folk" }, Now);

        character.SyncRaceGrants(grants, null, Now);

        var spells = character.Spells.Where(s => s.ClassIndex == CharacterSpell.OriginClassIndex).ToList();
        Assert.Equal(["glimmer", "veil-of-dusk"], spells.Select(s => s.SpellIndex).Order());
        Assert.All(spells, s => Assert.True(s.AlwaysPrepared));

        character.ApplySheetEdit(new SheetEdit { Classes = [new ClassEntry("fighter", null, 4), new ClassEntry("rogue", null, 1)] }, Now);
        character.SyncRaceGrants(grants, null, Now);

        Assert.Contains(character.Spells, s => s.SpellIndex == "deep-shadow" && s.ClassIndex == CharacterSpell.OriginClassIndex);

        character.ApplySheetEdit(new SheetEdit { RaceIndex = "other-folk" }, Now);

        Assert.DoesNotContain(character.Spells, s => s.ClassIndex == CharacterSpell.OriginClassIndex);
    }

    [Fact]
    public void Racial_spellcasting_uses_the_granted_ability()
    {
        var race = MountainFolk with { Grants = Grants(cantrips: ["glimmer"], ability: "cha") };

        var sheet = Sheet(Fighter(5, cha: 16), race);

        var racial = Assert.Single(sheet.Spellcasting, s => s.ClassIndex == CharacterSpell.OriginClassIndex);
        Assert.Equal("cha", racial.Ability);
        Assert.Equal(8 + 3 + 3, racial.SaveDc);
        Assert.Equal(3 + 3, racial.AttackBonus);
        Assert.Null(racial.PreparedMax);
        Assert.Contains(sheet.Breakdowns["spellSaveDc.race"].Parts, p => p.Source == BreakdownSources.Ability && p.Value == 3);
    }

    [Fact]
    public void Subrace_ability_wins_and_no_spells_means_no_racial_spellcasting()
    {
        var race = MountainFolk with { Grants = Grants(weapons: ["battleaxes"], ability: "int") };
        Assert.DoesNotContain(Sheet(Fighter(1), race).Spellcasting, s => s.ClassIndex == CharacterSpell.OriginClassIndex);

        var subrace = new SubraceInfo([]) { Name = "Deep Folk", Grants = Grants(cantrips: ["glimmer"], ability: "wis") };
        var racial = Assert.Single(Sheet(Fighter(1), race, subrace).Spellcasting);
        Assert.Equal("wis", racial.Ability);
    }

    [Fact]
    public void Racial_spells_with_uses_per_long_rest_create_a_resource_from_their_level()
    {
        var subrace = new SubraceInfo([])
        {
            Name = "Deep Folk",
            Grants = Grants(spells: [new GrantedSpell("veil-of-dusk", 3) { UsesPerLongRest = 1 }, new GrantedSpell("deep-shadow", 5) { UsesPerLongRest = 2 }], ability: "cha"),
            SpellNames = new Dictionary<string, string> { ["veil-of-dusk"] = "Veil of Dusk" },
        };

        Assert.Empty(Sheet(Fighter(2), MountainFolk, subrace).ChoiceResources);

        var resources = Sheet(Fighter(5), MountainFolk, subrace).ChoiceResources;
        Assert.Equal(2, resources.Count);
        Assert.Contains(new ResourceTemplate("race.veil-of-dusk", "Veil of Dusk", 1, ResourceRecharge.LongRest), resources);
        Assert.Contains(new ResourceTemplate("race.deep-shadow", "deep-shadow", 2, ResourceRecharge.LongRest), resources);
    }

    [Fact]
    public void Grants_json_reads_the_spellcasting_ability_and_uses_per_long_rest()
    {
        var grants = LevelChoiceJson.ParseGrants("""{"spells":[{"index":"veil-of-dusk","minLevel":3,"usesPerLongRest":1}],"spellcastingAbility":"cha"}""");

        Assert.Equal("cha", grants.SpellcastingAbility);
        var spell = Assert.Single(grants.Spells);
        Assert.Equal(3, spell.MinLevel);
        Assert.Equal(1, spell.UsesPerLongRest);
    }

    [Fact]
    public void Race_info_merges_the_grants_of_the_packs_that_extend_it()
    {
        var race = new RaceDefinition { Index = "mountain-folk", Name = "Mountain Folk", Speed = 25, GrantsJson = """{"weapons":["battleaxes"]}""" };
        var extension = new RaceExtensionDefinition
        {
            Id = RaceExtensionDefinition.IdFor("tales", "mountain-folk"),
            RaceIndex = "mountain-folk",
            GrantsJson = """{"weapons":["battleaxes","warpicks"],"skills":["history"]}""",
            Source = "tales",
        };

        var info = RaceInfo.From(race, [extension]);

        Assert.Equal(["battleaxes", "warpicks"], info.Grants.Weapons);
        Assert.Equal(["history"], info.Grants.Skills);
    }
}
