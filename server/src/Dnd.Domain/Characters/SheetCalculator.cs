using Dnd.Domain.Rules;

namespace Dnd.Domain.Characters;

/// <summary>
/// Pure calculation of the character sheet (SRD 5.1) from stored data and catalog info. Every
/// calculated value can be replaced by a <see cref="CharacterOverride"/>; overridden values (ability
/// scores, proficiency bonus, Perception) feed the values derived from them.
/// </summary>
public static class SheetCalculator
{
    /// <summary>Speed used when the character has no race.</summary>
    public const int DefaultSpeed = 30;

    public const int ShieldBonus = 2;

    private const string Barbarian = "barbarian";
    private const string Monk = "monk";
    private const string Warlock = "warlock";
    private const string PerceptionSkill = "perception";
    private const string SkillProficiencyPrefix = "skill-";
    private const string SavingThrowProficiencyPrefix = "saving-throw-";

    public static CharacterSheet Calculate(SheetInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var character = input.Character;
        var overrides = character.Overrides.ToDictionary(o => o.Field, o => o.Value, StringComparer.Ordinal);
        var classes = ResolveClasses(character, input.Classes);
        var gear = input.Gear ?? EquippedGear.None;

        var abilities = CalculateAbilities(character, input.Race, input.Subrace, overrides);
        int Mod(string ability) => abilities[ability].Modifier;

        var totalLevel = classes.Sum(c => c.Level.Level);
        var proficiencyBonus = Override(overrides, OverrideFields.ProficiencyBonus)
            ?? AbilityRules.ProficiencyBonus(Math.Max(AbilityRules.MinLevel, totalLevel));

        var savingThrows = Abilities.All.ToDictionary(
            a => a,
            a =>
            {
                var proficient = HasProficiency(character, ProficiencyType.SavingThrow, a, SavingThrowProficiencyPrefix);
                var field = OverrideFields.Save(a);
                var computed = Mod(a) + (proficient ? proficiencyBonus : 0);
                return new SavingThrowValue(Override(overrides, field) ?? computed, proficient, overrides.ContainsKey(field));
            });

        SkillValue Skill(string index, string name, string ability)
        {
            var proficiency = FindProficiency(character, ProficiencyType.Skill, index, SkillProficiencyPrefix);
            var expertise = proficiency?.Expertise ?? false;
            var bonus = expertise ? 2 * proficiencyBonus : proficiency is not null ? proficiencyBonus : 0;
            var field = OverrideFields.Skill(index);
            var value = Override(overrides, field) ?? Mod(ability) + bonus;
            return new SkillValue(index, name, ability, value, proficiency is not null, expertise, overrides.ContainsKey(field));
        }

        var skills = input.Skills.Select(s => Skill(s.Index, s.Name, s.Ability)).ToList();
        var perception = skills.FirstOrDefault(s => s.Index == PerceptionSkill)
            ?? Skill(PerceptionSkill, PerceptionSkill, Abilities.Wis);

        var (spellSlots, casterLevel) = CalculateSpellSlots(classes);

        return new CharacterSheet
        {
            Abilities = abilities,
            TotalLevel = totalLevel,
            ProficiencyBonus = proficiencyBonus,
            SavingThrows = savingThrows,
            Skills = skills,
            PassivePerception = Override(overrides, OverrideFields.PassivePerception) ?? 10 + perception.Value,
            Initiative = Override(overrides, OverrideFields.Initiative) ?? Mod(Abilities.Dex),
            ArmorClass = Override(overrides, OverrideFields.ArmorClass) ?? CalculateArmorClass(classes, gear, Mod),
            Speed = Override(overrides, OverrideFields.Speed) ?? input.Race?.Speed ?? DefaultSpeed,
            HitPointsMax = Override(overrides, OverrideFields.HitPointsMax)
                ?? (character.HpMode == HpMode.Average ? AverageHitPoints(classes, Mod(Abilities.Con)) : 0),
            HitDice = classes
                .Select(c => new HitDiceValue(c.Level.ClassIndex, c.Info.HitDie, c.Level.Level, character.HitDiceRemaining(c.Level.ClassIndex)))
                .ToList(),
            Spellcasting = CalculateSpellcasting(classes, proficiencyBonus, overrides, Mod),
            MulticlassCasterLevel = casterLevel,
            SpellSlotsMax = spellSlots,
            PactMagic = CalculatePactMagic(classes),
            OverriddenFields = [.. overrides.Keys.Order(StringComparer.Ordinal)],
        };
    }

    /// <summary>Preparable spells for classes that prepare them (cleric, druid, paladin, wizard); null otherwise. Minimum 1.</summary>
    public static int? PreparedMax(string classIndex, int classLevel, int abilityModifier) => classIndex switch
    {
        "cleric" or "druid" or "wizard" => Math.Max(1, abilityModifier + classLevel),
        "paladin" => Math.Max(1, abilityModifier + classLevel / 2),
        _ => null,
    };

    private static List<ResolvedClass> ResolveClasses(Character character, IReadOnlyList<ClassInfo> infos)
    {
        var byIndex = infos.GroupBy(i => i.Index, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        return character.OrderedClasses
            .Select(c => new ResolvedClass(
                c,
                byIndex.GetValueOrDefault(c.ClassIndex)
                    ?? throw new ArgumentException($"Missing catalog info for class '{c.ClassIndex}'.", nameof(infos))))
            .ToList();
    }

    private static Dictionary<string, AbilityValue> CalculateAbilities(
        Character character,
        RaceInfo? race,
        SubraceInfo? subrace,
        Dictionary<string, int> overrides)
    {
        var bonuses = character.ApplyRacialBonuses
            ? (race?.AbilityBonuses ?? []).Concat(subrace?.AbilityBonuses ?? []).ToList()
            : [];
        var baseScores = character.BaseAbilities;

        return Abilities.All.ToDictionary(
            a => a,
            a =>
            {
                var field = OverrideFields.Ability(a);
                var computed = baseScores[a] + bonuses.Where(b => b.Ability == a).Sum(b => b.Bonus);
                var score = Math.Clamp(Override(overrides, field) ?? computed, AbilityRules.MinScore, AbilityRules.MaxScore);
                return new AbilityValue(score, AbilityRules.Modifier(score), overrides.ContainsKey(field));
            });
    }

    private static int CalculateArmorClass(List<ResolvedClass> classes, EquippedGear gear, Func<string, int> mod)
    {
        var dex = mod(Abilities.Dex);
        var shield = gear.HasShield ? ShieldBonus : 0;

        if (gear.ArmorClassBase is { } armorBase)
        {
            var dexBonus = !gear.AddDexModifier ? 0 : gear.MaxDexBonus is { } maxDex ? Math.Min(dex, maxDex) : dex;
            return armorBase + dexBonus + shield;
        }

        var armorClass = 10 + dex + shield;
        if (classes.Any(c => c.Level.ClassIndex == Barbarian))
        {
            armorClass = Math.Max(armorClass, 10 + dex + mod(Abilities.Con) + shield);
        }

        if (!gear.HasShield && classes.Any(c => c.Level.ClassIndex == Monk))
        {
            armorClass = Math.Max(armorClass, 10 + dex + mod(Abilities.Wis));
        }

        return armorClass;
    }

    /// <summary>Max die + Con at 1st level of the main class; (die / 2 + 1) + Con per other level; at least 1 per level.</summary>
    private static int AverageHitPoints(List<ResolvedClass> classes, int conModifier)
    {
        var total = 0;
        for (var i = 0; i < classes.Count; i++)
        {
            var die = classes[i].Info.HitDie;
            var perLevel = Math.Max(1, die / 2 + 1 + conModifier);
            var levels = classes[i].Level.Level;
            if (i == 0)
            {
                total += Math.Max(1, die + conModifier);
                levels--;
            }

            total += levels * perLevel;
        }

        return total;
    }

    private static List<SpellcastingValue> CalculateSpellcasting(
        List<ResolvedClass> classes,
        int proficiencyBonus,
        Dictionary<string, int> overrides,
        Func<string, int> mod)
    {
        var saveDcOverride = Override(overrides, OverrideFields.SpellSaveDc);
        var attackOverride = Override(overrides, OverrideFields.SpellAttackBonus);

        return classes
            .Where(c => c.Info.SpellcastingAbility is { } ability && Abilities.IsValid(ability))
            .Select(c =>
            {
                var ability = c.Info.SpellcastingAbility!;
                var modifier = mod(ability);
                return new SpellcastingValue(
                    c.Level.ClassIndex,
                    ability,
                    saveDcOverride ?? 8 + proficiencyBonus + modifier,
                    attackOverride ?? proficiencyBonus + modifier,
                    PreparedMax(c.Level.ClassIndex, c.Level.Level, modifier));
            })
            .ToList();
    }

    /// <summary>
    /// One non-pact caster class: its own table. Several: spellcaster level = Σ floor(level / SpellcastingLevel)
    /// and the multiclass table.
    /// </summary>
    private static (IReadOnlyList<int> Slots, int CasterLevel) CalculateSpellSlots(List<ResolvedClass> classes)
    {
        var casters = classes.Where(c => c.Info.SpellcastingLevel > 0 && !IsPact(c.Info)).ToList();
        switch (casters.Count)
        {
            case 0:
                return (new int[9], 0);
            case 1:
                return ([.. casters[0].Info.SlotsByLevel(casters[0].Level.Level)], 0);
            default:
                var casterLevel = casters.Sum(c => c.Level.Level / c.Info.SpellcastingLevel);
                return (SpellSlotTables.MulticlassSlots(Math.Min(casterLevel, AbilityRules.MaxLevel)), casterLevel);
        }
    }

    /// <summary>All pact slots share the highest slot level of the warlock table at its level.</summary>
    private static PactMagicValue? CalculatePactMagic(List<ResolvedClass> classes)
    {
        var pact = classes.FirstOrDefault(c => IsPact(c.Info));
        if (pact is null)
        {
            return null;
        }

        var slots = pact.Info.SlotsByLevel(pact.Level.Level);
        for (var level = slots.Count; level >= 1; level--)
        {
            if (slots[level - 1] > 0)
            {
                return new PactMagicValue(level, slots[level - 1]);
            }
        }

        return null;
    }

    private static bool IsPact(ClassInfo info) => info.IsPactCaster || info.Index == Warlock;

    private static bool HasProficiency(Character character, ProficiencyType type, string index, string datasetPrefix) =>
        FindProficiency(character, type, index, datasetPrefix) is not null;

    /// <summary>Matches the plain index ("stealth") or the dataset proficiency index ("skill-stealth").</summary>
    private static CharacterProficiency? FindProficiency(Character character, ProficiencyType type, string index, string datasetPrefix) =>
        character.Proficiencies
            .Where(p => p.Type == type && (p.Key == index || p.Key == datasetPrefix + index))
            .OrderByDescending(p => p.Expertise)
            .FirstOrDefault();

    private static int? Override(Dictionary<string, int> overrides, string field) =>
        overrides.TryGetValue(field, out var value) ? value : null;

    private sealed record ResolvedClass(CharacterClassLevel Level, ClassInfo Info);
}
