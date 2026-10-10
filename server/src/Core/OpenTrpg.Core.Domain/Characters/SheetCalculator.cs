using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Core.Domain.Rules;

namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// Pure calculation of the character sheet (SRD 5.1) from stored data and catalog info. Every
/// calculated value can be replaced by a <see cref="CharacterOverride"/>; overridden values (ability
/// scores, proficiency bonus, Perception) feed the values derived from them. Modifiers of active items
/// (<see cref="EquippedGear.Modifiers"/>) are added to the calculated values before the overrides,
/// except for the maximum hit points (see <see cref="Calculate"/>).
/// </summary>
public static class SheetCalculator
{
    /// <summary>Speed used when the character has no race.</summary>
    public const int DefaultSpeed = 30;

    /// <summary>Armor class of a shield without an armor class of its own.</summary>
    public const int ShieldBonus = 2;

    /// <summary>Ability Score Improvements and feats cannot raise a score above 20.</summary>
    public const int ImprovementMaxScore = 20;

    /// <summary>Breakdown key of the breath weapon DC.</summary>
    public const string BreathWeaponDcKey = "breathWeapon.dc";

    private const string Barbarian = "barbarian";
    private const string Monk = "monk";
    private const string Warlock = "warlock";
    private const string PerceptionSkill = "perception";
    private const string SkillProficiencyPrefix = "skill-";
    private const string SavingThrowProficiencyPrefix = "saving-throw-";

    /// <summary>
    /// Calculates the sheet, with a <see cref="ValueBreakdown"/> of every value. Item modifiers: ability
    /// bonuses are added to base + racial scores, then ability sets raise the score when higher, then the
    /// override applies (clamped to 1-30). Save, skill, armor class, speed and initiative bonuses are added
    /// to the calculated value; an override replaces the result. The maximum hit points bonus is added to
    /// the calculated average <b>and</b> to the <see cref="OverrideFields.HitPointsMax"/> override, because
    /// the override stands for the character's own hit points (rolled or chosen), not for the final value.
    /// </summary>
    public static CharacterSheet Calculate(SheetInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var character = input.Character;
        var overrides = character.Overrides.ToDictionary(o => o.Field, StringComparer.Ordinal);
        var classes = ResolveClasses(character, input.Classes);
        var gear = input.Gear ?? EquippedGear.None;
        var choices = input.Choices ?? ChoiceEffects.None;
        var breakdowns = new Dictionary<string, ValueBreakdown>(StringComparer.Ordinal);

        // Attack and damage bonuses of weapons apply only to the weapon's own attack (CombatCalculator).
        var modifiers = gear.Modifiers
            .Where(m => !(m.FromWeapon && m.Modifier.Kind is ItemModifierKind.AttackBonus or ItemModifierKind.DamageBonus))
            .ToList();

        BreakdownBuilder Items(BreakdownBuilder builder, ItemModifierKind kind, string? target = null)
        {
            foreach (var m in modifiers.Where(m => m.Modifier.Kind == kind && (m.Modifier.Target is null || m.Modifier.Target == target)))
            {
                builder.Add(BreakdownSources.Item, m.ItemName, m.Modifier.Value);
            }

            return builder;
        }

        // Modifiers of chosen options: unconditional ones, and "wearingArmor" ones while armor is worn. Attack and
        // damage bonuses go to the attacks (CombatCalculator).
        bool FeatureApplies(FeatureModifier m) =>
            m.Condition is null || (m.Condition == ModifierConditions.WearingArmor && gear.WearsArmor);

        BreakdownBuilder Bonuses(BreakdownBuilder builder, ItemModifierKind kind, string? target = null)
        {
            foreach (var m in choices.Modifiers.Where(m => m.Kind == kind && (m.Target is null || m.Target == target) && FeatureApplies(m)))
            {
                builder.Add(BreakdownSources.Feature, m.Label, m.Value);
            }

            return Items(builder, kind, target);
        }

        BreakdownBuilder WithOverride(BreakdownBuilder builder, string field) =>
            overrides.TryGetValue(field, out var o) ? builder.SetTo(BreakdownSources.Override, BreakdownLabels.Override(o.Note), o.Value) : builder;

        int Record(string key, BreakdownBuilder builder)
        {
            breakdowns[key] = builder.Build();
            return builder.Total;
        }

        var appliedSets = new HashSet<ActiveModifier>(ReferenceEqualityComparer.Instance);
        var abilities = Abilities.All.ToDictionary(
            a => a,
            a =>
            {
                var field = OverrideFields.Ability(a);
                var builder = AbilityScore(character, a, input.Race, input.Subrace, choices, modifiers, appliedSets);
                var score = Record(field, WithOverride(builder, field).Clamp(AbilityRules.MinScore, AbilityRules.MaxScore, BreakdownLabels.ScoreLimit));
                return new AbilityValue(score, AbilityRules.Modifier(score), overrides.ContainsKey(field));
            });
        int Mod(string ability) => abilities[ability].Modifier;
        BreakdownBuilder FromAbility(string ability) =>
            new BreakdownBuilder().Add(BreakdownSources.Ability, BreakdownLabels.Ability(ability), Mod(ability));

        var totalLevel = classes.Sum(c => c.Level.Level);
        var level = Math.Max(AbilityRules.MinLevel, totalLevel);
        var proficiencyBonus = Record(
            OverrideFields.ProficiencyBonus,
            WithOverride(
                new BreakdownBuilder().Add(BreakdownSources.Base, BreakdownLabels.Level(level), AbilityRules.ProficiencyBonus(level)),
                OverrideFields.ProficiencyBonus));

        var savingThrows = Abilities.All.ToDictionary(
            a => a,
            a =>
            {
                var proficient = HasProficiency(character, ProficiencyType.SavingThrow, a, SavingThrowProficiencyPrefix);
                var field = OverrideFields.Save(a);
                var builder = FromAbility(a);
                if (proficient)
                {
                    builder.Add(BreakdownSources.Proficiency, BreakdownLabels.Proficiency, proficiencyBonus);
                }

                var value = Record(field, WithOverride(Bonuses(builder, ItemModifierKind.SaveBonus, a), field));
                return new SavingThrowValue(value, proficient, overrides.ContainsKey(field));
            });

        SkillValue Skill(string index, string name, string ability)
        {
            var proficiency = FindProficiency(character, ProficiencyType.Skill, index, SkillProficiencyPrefix);
            var expertise = proficiency?.Expertise ?? false;
            var field = OverrideFields.Skill(index);
            var builder = FromAbility(ability);
            if (proficiency is not null)
            {
                builder.Add(BreakdownSources.Proficiency, BreakdownLabels.Proficiency, proficiencyBonus);
            }

            if (expertise)
            {
                builder.Add(BreakdownSources.Expertise, BreakdownLabels.Expertise, proficiencyBonus);
            }

            var value = Record(field, WithOverride(Bonuses(builder, ItemModifierKind.SkillBonus, index), field));
            return new SkillValue(index, name, ability, value, proficiency is not null, expertise, overrides.ContainsKey(field));
        }

        var skills = input.Skills.Select(s => Skill(s.Index, s.Name, s.Ability)).ToList();
        var perception = skills.FirstOrDefault(s => s.Index == PerceptionSkill)
            ?? Skill(PerceptionSkill, PerceptionSkill, Abilities.Wis);
        var passivePerception = Record(
            OverrideFields.PassivePerception,
            WithOverride(
                new BreakdownBuilder()
                    .Add(BreakdownSources.Base, BreakdownLabels.Base, 10)
                    .AddAll(breakdowns[OverrideFields.Skill(PerceptionSkill)].Parts),
                OverrideFields.PassivePerception));

        var initiative = Record(
            OverrideFields.Initiative,
            WithOverride(Bonuses(FromAbility(Abilities.Dex), ItemModifierKind.InitiativeBonus), OverrideFields.Initiative));

        var armorClass = Record(
            OverrideFields.ArmorClass,
            WithOverride(Bonuses(ArmorClass(classes, gear, Mod), ItemModifierKind.ArmorClassBonus), OverrideFields.ArmorClass));

        var speedBuilder = input.Race is { } race
            ? new BreakdownBuilder().Add(BreakdownSources.Race, BreakdownLabels.Race, race.Speed)
            : new BreakdownBuilder().Add(BreakdownSources.Base, BreakdownLabels.BaseSpeed, DefaultSpeed);
        if (input.Race is not null && input.Subrace?.Speed is { } subraceSpeed && subraceSpeed != input.Race.Speed)
        {
            // A subrace speed replaces the race's: "Raza 30" + "Wood Elf +5".
            speedBuilder.Add(BreakdownSources.Subrace, input.Subrace.Name.Length > 0 ? input.Subrace.Name : BreakdownLabels.Subrace, subraceSpeed - input.Race.Speed);
        }

        var speed = Record(
            OverrideFields.Speed,
            WithOverride(Bonuses(speedBuilder, ItemModifierKind.SpeedBonus).Clamp(0, int.MaxValue, BreakdownLabels.Minimum), OverrideFields.Speed));

        var hitPointsBuilder = character.HpMode == HpMode.Average
            ? AverageHitPoints(classes, Mod(Abilities.Con), character.HitPointRolls())
            : new BreakdownBuilder();
        var hitPointsMax = Record(
            OverrideFields.HitPointsMax,
            Bonuses(WithOverride(hitPointsBuilder, OverrideFields.HitPointsMax), ItemModifierKind.HitPointsMaxBonus)
                .Clamp(0, int.MaxValue, BreakdownLabels.Minimum));

        var spellcasting = classes
            .Where(c => c.Info.SpellcastingAbility is { } ability && Abilities.IsValid(ability) && c.Info.CastsAt(c.Level.Level))
            .Select(c =>
            {
                var ability = c.Info.SpellcastingAbility!;
                var classIndex = c.Level.ClassIndex;
                var saveDc = Record(
                    $"{OverrideFields.SpellSaveDc}.{classIndex}",
                    WithOverride(
                        new BreakdownBuilder()
                            .Add(BreakdownSources.Base, BreakdownLabels.Base, 8)
                            .Add(BreakdownSources.Proficiency, BreakdownLabels.Proficiency, proficiencyBonus)
                            .Add(BreakdownSources.Ability, BreakdownLabels.Ability(ability), Mod(ability)),
                        OverrideFields.SpellSaveDc));
                var attackBonus = Record(
                    $"{OverrideFields.SpellAttackBonus}.{classIndex}",
                    WithOverride(
                        new BreakdownBuilder()
                            .Add(BreakdownSources.Proficiency, BreakdownLabels.Proficiency, proficiencyBonus)
                            .Add(BreakdownSources.Ability, BreakdownLabels.Ability(ability), Mod(ability)),
                        OverrideFields.SpellAttackBonus));
                return new SpellcastingValue(classIndex, ability, saveDc, attackBonus, PreparedMax(classIndex, c.Level.Level, Mod(ability)))
                {
                    MaxSpellLevel = MaxSpellLevel(c.Info.SlotsByLevel(c.Level.Level)),
                    SpellsKnownMax = c.Info.SubclassSpellcasting?.SpellsKnownAt(c.Level.Level),
                    CantripsKnownMax = c.Info.SubclassSpellcasting?.CantripsKnownAt(c.Level.Level),
                };
            })
            .ToList();

        if (RacialSpellcastingAbility(input.Race, input.Subrace) is { } racialAbility)
        {
            const string raceIndex = CharacterSpell.OriginClassIndex;
            var saveDc = Record(
                $"{OverrideFields.SpellSaveDc}.{raceIndex}",
                WithOverride(
                    new BreakdownBuilder()
                        .Add(BreakdownSources.Base, BreakdownLabels.Base, 8)
                        .Add(BreakdownSources.Proficiency, BreakdownLabels.Proficiency, proficiencyBonus)
                        .Add(BreakdownSources.Ability, BreakdownLabels.Ability(racialAbility), Mod(racialAbility)),
                    OverrideFields.SpellSaveDc));
            var attackBonus = Record(
                $"{OverrideFields.SpellAttackBonus}.{raceIndex}",
                WithOverride(
                    new BreakdownBuilder()
                        .Add(BreakdownSources.Proficiency, BreakdownLabels.Proficiency, proficiencyBonus)
                        .Add(BreakdownSources.Ability, BreakdownLabels.Ability(racialAbility), Mod(racialAbility)),
                    OverrideFields.SpellAttackBonus));
            spellcasting.Add(new SpellcastingValue(raceIndex, racialAbility, saveDc, attackBonus, null));
        }

        var (spellSlots, casterLevel) = CalculateSpellSlots(classes);
        var choiceResources = choices.Resources
            .Select(r => (r.Resource, r.Label, ClassLevel: r.ClassIndex is null ? totalLevel : classes.FirstOrDefault(c => c.Level.ClassIndex == r.ClassIndex)?.Level.Level ?? 0))
            .Where(r => r.ClassLevel > 0)
            .Select(r => (r.Resource, r.Label, r.ClassLevel, Breakdown: r.Resource.Calculate(proficiencyBonus, r.ClassLevel, Mod, r.Label)))
            .Where(r => r.Breakdown is not null)
            .Select(r => new ResourceTemplate(r.Resource.Key, r.Resource.Name, r.Breakdown!.Total, r.Resource.Recharge)
            {
                RollOnRest = r.Resource.RollOnRest,
                Dice = r.Resource.DiceAt(r.ClassLevel),
                Source = r.Label,
                Breakdown = r.Breakdown,
            })
            .ToList();
        choiceResources.AddRange(RacialSpellResources(input.Race, input.Subrace, level));

        var (resistances, breath) = OriginTraits(character, input.Race, input.Subrace);
        BreathWeaponValue? breathWeapon = null;
        if (breath is { } chosen && chosen.Option.BreathWeapon is { } weapon)
        {
            var dc = Record(
                BreathWeaponDcKey,
                new BreakdownBuilder()
                    .Add(BreakdownSources.Base, BreakdownLabels.Base, 8)
                    .Add(BreakdownSources.Ability, BreakdownLabels.Ability(Abilities.Con), Mod(Abilities.Con))
                    .Add(BreakdownSources.Proficiency, BreakdownLabels.Proficiency, proficiencyBonus));
            breathWeapon = new BreathWeaponValue(
                weapon.Name,
                chosen.Source,
                chosen.Option.DamageType ?? string.Empty,
                weapon.DiceAt(level) ?? string.Empty,
                weapon.SaveAbility,
                weapon.Area,
                dc);
        }

        return new CharacterSheet
        {
            Abilities = abilities,
            TotalLevel = totalLevel,
            ProficiencyBonus = proficiencyBonus,
            SavingThrows = savingThrows,
            Skills = skills,
            PassivePerception = passivePerception,
            Initiative = initiative,
            ArmorClass = armorClass,
            Speed = speed,
            HitPointsMax = hitPointsMax,
            HitDice = classes
                .Select(c => new HitDiceValue(c.Level.ClassIndex, c.Info.HitDie, c.Level.Level, character.HitDiceRemaining(c.Level.ClassIndex)))
                .ToList(),
            Spellcasting = spellcasting,
            MulticlassCasterLevel = casterLevel,
            SpellSlotsMax = spellSlots,
            PactMagic = CalculatePactMagic(classes),
            OverriddenFields = [.. overrides.Keys.Order(StringComparer.Ordinal)],
            ItemEffects = modifiers
                .Where(m => m.Modifier.Kind != ItemModifierKind.AbilitySet || appliedSets.Contains(m))
                .Select(m => new AppliedItemEffect(m.ItemName, m.Modifier.Kind, m.Modifier.Target, m.Modifier.Value))
                .ToList(),
            Breakdowns = breakdowns,
            FeatureModifiers = choices.Modifiers,
            ChoiceResources = choiceResources,
            WearsArmor = gear.WearsArmor,
            Resistances = resistances,
            BreathWeapon = breathWeapon,
        };
    }

    /// <summary>
    /// Ability of the "Raza" spellcasting: the <c>spellcastingAbility</c> of the subrace grants, else of the race ones,
    /// when either grants spells or cantrips. Null otherwise.
    /// </summary>
    private static string? RacialSpellcastingAbility(RaceInfo? race, SubraceInfo? subrace)
    {
        var grantsSpells = (race?.Grants.HasSpells ?? false) || (subrace?.Grants.HasSpells ?? false);
        var ability = subrace?.Grants.SpellcastingAbility ?? race?.Grants.SpellcastingAbility;
        return grantsSpells && ability is not null && Abilities.IsValid(ability) ? ability : null;
    }

    /// <summary>
    /// Automatic resources of the racial spells cast a number of times per long rest (<c>usesPerLongRest</c>), named after
    /// the spell, from the total level the spell is granted at. Key <c>race.&lt;spell&gt;</c>.
    /// </summary>
    private static IEnumerable<ResourceTemplate> RacialSpellResources(RaceInfo? race, SubraceInfo? subrace, int level)
    {
        var sources = new[] { (Grants: race?.Grants, Names: race?.SpellNames), (Grants: subrace?.Grants, Names: subrace?.SpellNames) };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (grants, names) in sources)
        {
            foreach (var spell in grants?.Spells ?? [])
            {
                if (spell.UsesPerLongRest is not { } uses || uses <= 0 || (spell.MinLevel ?? 0) > level || !seen.Add(spell.Index))
                {
                    continue;
                }

                var key = $"{CharacterSpell.OriginClassIndex}.{spell.Index}";
                var name = names?.GetValueOrDefault(spell.Index) ?? spell.Index;
                yield return new ResourceTemplate(
                    key.Length > Character.IndexMaxLength ? key[..Character.IndexMaxLength] : key,
                    name.Length > CharacterResource.NameMaxLength ? name[..CharacterResource.NameMaxLength] : name,
                    Math.Min(uses, CharacterResource.MaxUses),
                    ResourceRecharge.LongRest);
            }
        }
    }

    /// <summary>
    /// Resistances of the race and subrace and of the chosen trait options (key <c>race.trait.&lt;key&gt;</c> or
    /// <c>race.subrace.trait.&lt;key&gt;</c>), and the breath weapon of a chosen option that has one.
    /// </summary>
    private static (List<ResistanceValue> Resistances, (TraitOption Option, string Source)? Breath) OriginTraits(Character character, RaceInfo? race, SubraceInfo? subrace)
    {
        var resistances = new List<ResistanceValue>();
        (TraitOption, string)? breath = null;
        void Add(string damageType, string source, string label)
        {
            if (resistances.All(r => r.DamageType != damageType))
            {
                resistances.Add(new ResistanceValue(damageType, source, label));
            }
        }

        foreach (var type in race?.Resistances ?? [])
        {
            Add(type, BreakdownSources.Race, race!.Name.Length > 0 ? race.Name : BreakdownLabels.Race);
        }

        foreach (var type in subrace?.Resistances ?? [])
        {
            Add(type, BreakdownSources.Subrace, subrace!.Name.Length > 0 ? subrace.Name : BreakdownLabels.Subrace);
        }

        foreach (var choice in character.OriginChoices.Where(c => c.Selection.Kind == OriginChoiceKeys.TraitOptionKind))
        {
            var isSubrace = OriginChoiceKeys.IsSubrace(choice.Key);
            var prefix = (isSubrace ? OriginChoiceKeys.SubracePrefix : OriginChoiceKeys.RacePrefix) + OriginChoiceKeys.TraitPrefix;
            if (!choice.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var traitKey = choice.Key[prefix.Length..];
            var trait = (isSubrace ? subrace?.Choices : race?.Choices)?.TraitOptions.FirstOrDefault(t => t.Key == traitKey);
            var source = isSubrace ? BreakdownSources.Subrace : BreakdownSources.Race;
            foreach (var item in choice.Selection.Selected)
            {
                if (trait?.Options.FirstOrDefault(o => o.Index == item.Index) is not { } option)
                {
                    continue;
                }

                if (option.DamageType is { } damageType)
                {
                    Add(damageType, source, option.Name);
                }

                if (option.BreathWeapon is not null && breath is null)
                {
                    breath = (option, source);
                }
            }
        }

        return (resistances, breath);
    }

    /// <summary>Preparable spells for classes that prepare them (cleric, druid, paladin, wizard); null otherwise. Minimum 1.</summary>
    public static int? PreparedMax(string classIndex, int classLevel, int abilityModifier) => classIndex switch
    {
        "cleric" or "druid" or "wizard" => Math.Max(1, abilityModifier + classLevel),
        "paladin" => Math.Max(1, abilityModifier + classLevel / 2),
        _ => null,
    };

    /// <summary>Highest spell level (1-9) with at least one slot in a slots row; 0 when there are none.</summary>
    public static int MaxSpellLevel(IReadOnlyList<int> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        for (var level = slots.Count; level >= 1; level--)
        {
            if (slots[level - 1] > 0)
            {
                return level;
            }
        }

        return 0;
    }

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

    /// <summary>
    /// Base score + racial bonuses + level choice increases (Ability Score Improvements and feats, which cannot
    /// raise the score above 20) + item bonuses, raised by the highest item set when it is higher (recorded in
    /// <paramref name="appliedSets"/>). Before the override and the 1-30 limit.
    /// </summary>
    private static BreakdownBuilder AbilityScore(
        Character character,
        string ability,
        RaceInfo? race,
        SubraceInfo? subrace,
        ChoiceEffects choices,
        List<ActiveModifier> modifiers,
        HashSet<ActiveModifier> appliedSets)
    {
        var builder = new BreakdownBuilder().Add(BreakdownSources.Base, BreakdownLabels.BaseScore, character.BaseAbilities[ability]);
        if (character.ApplyRacialBonuses)
        {
            var raceBonus = (race?.AbilityBonuses ?? []).Where(b => b.Ability == ability).Sum(b => b.Bonus);
            if (raceBonus != 0)
            {
                builder.Add(BreakdownSources.Race, BreakdownLabels.Race, raceBonus);
            }

            var subraceBonus = (subrace?.AbilityBonuses ?? []).Where(b => b.Ability == ability).Sum(b => b.Bonus);
            if (subraceBonus != 0)
            {
                builder.Add(BreakdownSources.Subrace, BreakdownLabels.Subrace, subraceBonus);
            }

            // Bonuses chosen for the race (half-elf: +1 to two abilities).
            foreach (var bonus in choices.OriginAbilityBonuses.Where(b => b.Ability == ability))
            {
                builder.Add(bonus.Source, bonus.Label, bonus.Amount);
            }
        }

        var natural = builder.Total;
        foreach (var increase in choices.AbilityIncreases.Where(i => i.Ability == ability))
        {
            builder.Add(increase.Source, increase.Label, increase.Amount);
        }

        foreach (var m in choices.Modifiers.Where(m => m.Kind == ItemModifierKind.AbilityBonus && m.Target == ability && m.Condition is null))
        {
            builder.Add(BreakdownSources.Feature, m.Label, m.Value);
        }

        if (builder.Total > ImprovementMaxScore && builder.Total > natural)
        {
            builder.SetTo(BreakdownSources.Feature, BreakdownLabels.ImprovementLimit, Math.Max(ImprovementMaxScore, natural));
        }

        foreach (var m in modifiers.Where(m => m.Modifier.Kind == ItemModifierKind.AbilityBonus && m.Modifier.Target == ability))
        {
            builder.Add(BreakdownSources.Item, m.ItemName, m.Modifier.Value);
        }

        var set = modifiers
            .Where(m => m.Modifier.Kind == ItemModifierKind.AbilitySet && m.Modifier.Target == ability)
            .OrderByDescending(m => m.Modifier.Value)
            .FirstOrDefault();
        if (set is not null && set.Modifier.Value > builder.Total)
        {
            builder.SetTo(BreakdownSources.Item, set.ItemName, set.Modifier.Value);
            appliedSets.Add(set);
        }

        return builder;
    }

    /// <summary>
    /// Armor: its base, Dex (capped) when it adds it, and the shield. Without armor: 10 + Dex + shield, or
    /// Unarmored Defense when higher (barbarian: + Con, shield allowed; monk: + Wis, without shield).
    /// </summary>
    private static BreakdownBuilder ArmorClass(List<ResolvedClass> classes, EquippedGear gear, Func<string, int> mod)
    {
        var dex = mod(Abilities.Dex);
        var shield = gear.HasShield ? gear.ShieldArmorClass : 0;
        var builder = new BreakdownBuilder();

        if (gear.ArmorClassBase is { } armorBase)
        {
            builder.Add(BreakdownSources.Armor, gear.ArmorName ?? BreakdownLabels.Armor, armorBase);
            if (gear.AddDexModifier)
            {
                builder.Add(BreakdownSources.Ability, BreakdownLabels.Ability(Abilities.Dex), gear.MaxDexBonus is { } maxDex ? Math.Min(dex, maxDex) : dex);
            }
        }
        else
        {
            builder.Add(BreakdownSources.Base, BreakdownLabels.Unarmored, 10).Add(BreakdownSources.Ability, BreakdownLabels.Ability(Abilities.Dex), dex);

            // Unarmored Defense: the higher option wins (ties keep the plain 10 + Dex).
            var bonus = 0;
            string? bonusAbility = null;
            if (classes.Any(c => c.Level.ClassIndex == Barbarian) && mod(Abilities.Con) > bonus)
            {
                (bonus, bonusAbility) = (mod(Abilities.Con), Abilities.Con);
            }

            if (!gear.HasShield && classes.Any(c => c.Level.ClassIndex == Monk) && mod(Abilities.Wis) > bonus)
            {
                (bonus, bonusAbility) = (mod(Abilities.Wis), Abilities.Wis);
            }

            if (bonusAbility is not null)
            {
                builder.Add(BreakdownSources.Class, BreakdownLabels.UnarmoredDefense(bonusAbility), bonus);
            }
        }

        if (gear.HasShield)
        {
            builder.Add(BreakdownSources.Shield, gear.ShieldName ?? BreakdownLabels.Shield, shield);
        }

        return builder;
    }

    /// <summary>
    /// Max die + Con at 1st level of the main class; for every other level the roll made when it was gained
    /// (<paramref name="rolls"/>) or, without one, (die / 2 + 1); plus Con; at least 1 per level. One part per
    /// class (its dice, naming the rolls) and one part for Constitution.
    /// </summary>
    private static BreakdownBuilder AverageHitPoints(List<ResolvedClass> classes, int conModifier, IReadOnlyDictionary<(string ClassIndex, int Level), int> rolls)
    {
        var builder = new BreakdownBuilder();
        var conTotal = 0;
        for (var i = 0; i < classes.Count; i++)
        {
            var die = classes[i].Info.HitDie;
            var classIndex = classes[i].Level.ClassIndex;
            var classLevel = classes[i].Level.Level;
            var total = 0;
            var rolled = new List<int>();
            for (var level = 1; level <= classLevel; level++)
            {
                if (i == 0 && level == 1)
                {
                    total += Math.Max(1, die + conModifier);
                }
                else if (rolls.TryGetValue((classIndex, level), out var roll))
                {
                    rolled.Add(roll);
                    total += Math.Max(1, roll + conModifier);
                }
                else
                {
                    total += Math.Max(1, die / 2 + 1 + conModifier);
                }
            }

            conTotal += classLevel * conModifier;
            var rollsText = rolled.Count == 0 ? string.Empty : $"; tiradas {string.Join(", ", rolled)}";
            builder.Add(
                BreakdownSources.Class,
                $"{BreakdownLabels.Class(classIndex)} {classLevel} (d{die}{rollsText})",
                total - classLevel * conModifier);
        }

        return classes.Count == 0 ? builder : builder.Add(BreakdownSources.Ability, BreakdownLabels.Ability(Abilities.Con), conTotal);
    }

    /// <summary>
    /// One non-pact caster class: its own table. Several: spellcaster level = Σ floor(level / SpellcastingLevel)
    /// and the multiclass table.
    /// </summary>
    private static (IReadOnlyList<int> Slots, int CasterLevel) CalculateSpellSlots(List<ResolvedClass> classes)
    {
        var casters = classes.Where(c => c.Info.SpellcastingLevel > 0 && !IsPact(c.Info) && c.Level.Level >= c.Info.SpellcastingFromLevel).ToList();
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

    private sealed record ResolvedClass(CharacterClassLevel Level, ClassInfo Info);
}
