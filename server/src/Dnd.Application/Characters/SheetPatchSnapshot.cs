using Dnd.Application.Common;
using Dnd.Domain.Characters;

namespace Dnd.Application.Characters;

/// <summary>
/// The current values of the fields a <see cref="SheetPatch"/> changes, in the same shape, so a
/// change request can show "before → after" for each field.
/// </summary>
public static class SheetPatchSnapshot
{
    public static SheetPatch Before(Character character, SheetPatch patch) => new()
    {
        Name = patch.Name is null ? null : character.Name,
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
        Notes = patch.Notes is null ? null : character.Notes,
        Backstory = patch.Backstory is null ? null : character.Backstory,
        PersonalityTraits = patch.PersonalityTraits is null ? null : character.PersonalityTraits,
        Ideals = patch.Ideals is null ? null : character.Ideals,
        Bonds = patch.Bonds is null ? null : character.Bonds,
        Flaws = patch.Flaws is null ? null : character.Flaws,
        BackgroundDetail = patch.BackgroundDetail is null ? null : character.BackgroundDetail,
        CopperPieces = patch.CopperPieces is null ? null : character.CopperPieces,
    };
}
