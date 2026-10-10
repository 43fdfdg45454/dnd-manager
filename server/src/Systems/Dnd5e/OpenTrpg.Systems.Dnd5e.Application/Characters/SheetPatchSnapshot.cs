using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Application;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Application.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>
/// The current values of the fields a <see cref="SheetPatch"/> changes, in the same shape, so a
/// change request can show "before → after" for each field.
/// </summary>
public static class SheetPatchSnapshot
{
    public static SheetPatch Before(Dnd5eCharacter character, SheetPatch patch) => new()
    {
        Name = patch.Name is null ? null : character.Character.Name,
        RaceIndex = patch.RaceIndex.IsSet ? new Optional<string?>(character.RaceIndex) : default,
        SubraceIndex = patch.SubraceIndex.IsSet ? new Optional<string?>(character.SubraceIndex) : default,
        BackgroundIndex = patch.BackgroundIndex.IsSet ? new Optional<string?>(character.BackgroundIndex) : default,
        Alignment = patch.Alignment.IsSet ? new Optional<string?>(character.Alignment) : default,
        ApplyRacialBonuses = patch.ApplyRacialBonuses is null ? null : character.ApplyRacialBonuses,
        HpMode = patch.HpMode is null ? null : character.HpMode.ToString(),
        BaseAbilities = patch.BaseAbilities is null
            ? null
            : new BaseAbilitiesPatch(character.BaseStr, character.BaseDex, character.BaseCon, character.BaseInt, character.BaseWis, character.BaseCha),
        Classes = patch.Classes is null
            ? null
            : character.OrderedClasses.Select(c => new ClassPatch(c.ClassIndex, c.SubclassIndex, c.Level)).ToList(),
        Proficiencies = patch.Proficiencies is null
            ? null
            : character.Proficiencies.Select(p => new ProficiencyPatch(p.Type.ToString(), p.Key, p.Expertise, p.Source.ToString())).ToList(),
        Spells = patch.Spells is null
            ? null
            : character.Spells.Select(s => new SpellPatch(s.SpellIndex, s.ClassIndex, s.IsPrepared, s.AlwaysPrepared)).ToList(),
        Overrides = patch.Overrides is null
            ? null
            : character.Overrides.Select(o => new OverridePatch(o.Field, o.Value, o.Note)).ToList(),
        Notes = patch.Notes is null ? null : character.Character.Notes,
        Backstory = patch.Backstory is null ? null : character.Character.Backstory,
        PersonalityTraits = patch.PersonalityTraits is null ? null : character.Character.PersonalityTraits,
        Ideals = patch.Ideals is null ? null : character.Character.Ideals,
        Bonds = patch.Bonds is null ? null : character.Character.Bonds,
        Flaws = patch.Flaws is null ? null : character.Character.Flaws,
        BackgroundDetail = patch.BackgroundDetail is null ? null : character.BackgroundDetail,
        CopperPieces = patch.CopperPieces is null ? null : character.Character.Money,
        HeightInches = patch.HeightInches.IsSet ? new Optional<int?>(character.Character.HeightInches) : default,
        WeightPounds = patch.WeightPounds.IsSet ? new Optional<int?>(character.Character.WeightPounds) : default,
    };
}
