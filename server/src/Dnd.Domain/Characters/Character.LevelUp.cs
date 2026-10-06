using Dnd.Domain.Catalog;
using Dnd.Domain.Common;
using Dnd.Domain.Rules;

namespace Dnd.Domain.Characters;

/// <summary>An option, spell, skill or subclass a character currently has from its level choices.</summary>
/// <param name="Level">Class level of the choice that picked it.</param>
/// <param name="Kind">Kind of the choice (a <c>LevelChoiceKind</c> name).</param>
public sealed record ActivePick(string ClassIndex, string Key, int Level, string Kind, string? SetId, ChoiceItem Item);

// Level-ups granted by the DM (phase 16b): the DM grants the next level and the player completes it with the
// level-up wizard (phase 16c), which records its choices (CharacterChoice) and applies their effects.
public sealed partial class Character
{
    private readonly List<CharacterChoice> _choices = [];

    /// <summary>Choices made when gaining class levels, hit point rolls included.</summary>
    public IReadOnlyCollection<CharacterChoice> Choices => _choices;

    /// <summary>Total level the character may advance to (granted by a DM), or null when nothing is pending.</summary>
    public int? PendingLevelUpTo { get; private set; }

    /// <summary>DM who granted <see cref="PendingLevelUpTo"/>.</summary>
    public Guid? LevelGrantedByUserId { get; private set; }

    public DateTimeOffset? LevelGrantedAt { get; private set; }

    /// <summary>
    /// Grants the next level (total level + 1). Does nothing and returns false when a level-up is
    /// already pending (grants never accumulate) or the character is already at the maximum level.
    /// </summary>
    public bool GrantLevelUp(Guid grantedByUserId, DateTimeOffset now)
    {
        if (PendingLevelUpTo is not null || TotalLevel >= AbilityRules.MaxLevel)
        {
            return false;
        }

        PendingLevelUpTo = TotalLevel + 1;
        LevelGrantedByUserId = grantedByUserId;
        LevelGrantedAt = now;
        Touch(now);
        return true;
    }

    /// <summary>Withdraws the pending level-up; false when there was none.</summary>
    public bool RevokeLevelUp(DateTimeOffset now)
    {
        if (PendingLevelUpTo is null)
        {
            return false;
        }

        ClearLevelUp();
        Touch(now);
        return true;
    }

    /// <summary>
    /// The hit dice of <paramref name="requested"/> the character can still spend: classes it no longer
    /// has are dropped and each count is capped at the remaining dice of its class (zero entries dropped).
    /// </summary>
    public IReadOnlyDictionary<string, int> ClampHitDiceToRemaining(IReadOnlyDictionary<string, int> requested)
    {
        ArgumentNullException.ThrowIfNull(requested);
        return requested
            .Select(e => (e.Key, Count: Math.Min(Math.Max(0, e.Value), HitDiceRemaining(e.Key))))
            .Where(e => e.Count > 0)
            .ToDictionary(e => e.Key, e => e.Count, StringComparer.Ordinal);
    }

    /// <summary>
    /// Total level the next level-up reaches. The owner needs a level granted by a DM; DMs apply level-ups
    /// directly. Throws 409 at level 20 or without a grant.
    /// </summary>
    public int LevelUpTarget(bool actorIsDm)
    {
        if (TotalLevel >= AbilityRules.MaxLevel)
        {
            throw DomainException.Conflict($"El personaje ya está en el nivel {AbilityRules.MaxLevel}.");
        }

        if (PendingLevelUpTo is null && !actorIsDm)
        {
            throw DomainException.Conflict("El DM todavía no ha concedido una subida de nivel a este personaje.");
        }

        return TotalLevel + 1;
    }

    /// <summary>
    /// Adds one level to <paramref name="classIndex"/>, or takes it as a new class at level 1 (after the
    /// current ones). Returns the class entry. Clears the pending level-up once it is reached.
    /// </summary>
    public CharacterClassLevel AdvanceClass(string classIndex, DateTimeOffset now)
    {
        var index = RequireIndex(classIndex, "clase");
        if (TotalLevel >= AbilityRules.MaxLevel)
        {
            throw DomainException.RuleViolation($"El nivel total no puede superar {AbilityRules.MaxLevel}.");
        }

        var existing = _classes.FirstOrDefault(c => c.ClassIndex == index);
        if (existing is null)
        {
            existing = CharacterClassLevel.Create(Id, new ClassEntry(index, null, 1), _classes.Count == 0 ? 0 : _classes.Max(c => c.Order) + 1);
            _classes.Add(existing);
        }
        else
        {
            existing.Update(new ClassEntry(existing.ClassIndex, existing.SubclassIndex, existing.Level + 1), existing.Order);
        }

        ClearStaleLevelUp();
        Touch(now);
        return existing;
    }

    /// <summary>Sets the subclass of a class the character has.</summary>
    public void SetSubclass(string classIndex, string subclassIndex, DateTimeOffset now)
    {
        var entry = _classes.FirstOrDefault(c => c.ClassIndex == classIndex)
            ?? throw DomainException.RuleViolation($"El personaje no tiene la clase '{classIndex}'.");
        entry.Update(new ClassEntry(entry.ClassIndex, RequireIndex(subclassIndex, "subclase"), entry.Level), entry.Order);
        Touch(now);
    }

    /// <summary>Records the answer to a level choice (or the hit points rolled) made at a class level.</summary>
    public CharacterChoice RecordChoice(int level, string classIndex, string key, ChoiceSelection selection, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (level is < AbilityRules.MinLevel or > AbilityRules.MaxLevel)
        {
            throw DomainException.RuleViolation($"El nivel de clase debe estar entre {AbilityRules.MinLevel} y {AbilityRules.MaxLevel}.");
        }

        var choice = CharacterChoice.Create(Id, level, RequireIndex(classIndex, "clase"), RequireIndex(key, "elección"), selection, now);
        _choices.Add(choice);
        Touch(now);
        return choice;
    }

    /// <summary>Hit points rolled per (class, class level) at level-ups.</summary>
    public IReadOnlyDictionary<(string ClassIndex, int Level), int> HitPointRolls() =>
        _choices
            .Where(c => c.Key == CharacterChoice.HitPointsKey)
            .Select(c => (c.ClassIndex, c.Level, Roll: c.Selection.Roll))
            .Where(c => c.Roll is > 0)
            .GroupBy(c => (c.ClassIndex, c.Level))
            .ToDictionary(g => g.Key, g => g.Last().Roll!.Value);

    /// <summary>
    /// What the character currently has from its level choices: for each class and key, the picks of every
    /// level in order, minus the ones replaced later. Feats count as picks of the <c>feats</c> set.
    /// </summary>
    public IReadOnlyList<ActivePick> ActivePicks()
    {
        var result = new List<ActivePick>();
        foreach (var group in _choices.Where(c => c.Key != CharacterChoice.HitPointsKey).GroupBy(c => (c.ClassIndex, c.Key)))
        {
            var active = new List<ActivePick>();
            foreach (var choice in group.OrderBy(c => c.Level).ThenBy(c => c.CreatedAt))
            {
                var selection = choice.Selection;
                foreach (var replaced in selection.Replaced)
                {
                    active.RemoveAll(p => p.Item.Index == replaced.Index);
                }

                active.AddRange(selection.Selected.Select(item => new ActivePick(choice.ClassIndex, choice.Key, choice.Level, selection.Kind, selection.SetId, item)));
                if (selection.Feat is { } feat)
                {
                    active.Add(new ActivePick(choice.ClassIndex, choice.Key, choice.Level, selection.Kind, OptionSets.Feats, feat));
                }
            }

            result.AddRange(active);
        }

        return result;
    }

    /// <summary>Adds a proficiency unless the character already has it ("stealth" also matches "skill-stealth"). True when added.</summary>
    public bool AddProficiency(ProficiencyType type, string key, ProficiencySource source)
    {
        var normalized = RequireIndex(key, "competencia");
        if (FindProficiency(type, normalized) is not null)
        {
            return false;
        }

        _proficiencies.Add(CharacterProficiency.Create(Id, new ProficiencyEntry(type, normalized, false, source)));
        return true;
    }

    /// <summary>Gives expertise to an existing skill or tool proficiency. False when the character lacks it.</summary>
    public bool GrantExpertise(ProficiencyType type, string key)
    {
        if (type is not (ProficiencyType.Skill or ProficiencyType.Tool) || FindProficiency(type, key) is not { } proficiency)
        {
            return false;
        }

        proficiency.Update(new ProficiencyEntry(proficiency.Type, proficiency.Key, true, proficiency.Source));
        return true;
    }

    /// <summary>The proficiency of that type and key ("stealth" also matches "skill-stealth"; tools compare by slug).</summary>
    public CharacterProficiency? FindProficiency(ProficiencyType type, string key) =>
        _proficiencies.FirstOrDefault(p => p.Type == type && (p.Key == key || p.Key == $"skill-{key}" || $"skill-{p.Key}" == key || ProficiencySlug(p.Key) == ProficiencySlug(key)));

    /// <summary>
    /// Adds a spell for a class. When the character already has it, an always-prepared grant upgrades the
    /// entry. True when something changed.
    /// </summary>
    public bool AddSpell(string spellIndex, string classIndex, bool isPrepared, bool alwaysPrepared)
    {
        var spell = RequireIndex(spellIndex, "conjuro");
        var characterClass = RequireIndex(classIndex, "clase");
        var existing = _spells.FirstOrDefault(s => s.SpellIndex == spell && s.ClassIndex == characterClass);
        if (existing is null)
        {
            _spells.Add(CharacterSpell.Create(Id, new SpellEntry(spell, characterClass, isPrepared || alwaysPrepared, alwaysPrepared)));
            return true;
        }

        if (alwaysPrepared && !existing.AlwaysPrepared)
        {
            existing.Update(new SpellEntry(spell, characterClass, true, true));
            return true;
        }

        return false;
    }

    /// <summary>Removes a spell of a class; false when the character does not have it.</summary>
    public bool RemoveSpell(string spellIndex, string classIndex) =>
        _spells.RemoveAll(s => s.SpellIndex == spellIndex && s.ClassIndex == classIndex) > 0;

    /// <summary>Manual hit points mode: raises the <c>hitPointsMax</c> override (the character's own maximum).</summary>
    public void RaiseHitPointsMaxOverride(int amount)
    {
        var current = _overrides.FirstOrDefault(o => o.Field == OverrideFields.HitPointsMax);
        if (current is null)
        {
            return;
        }

        var value = current.Value + amount;
        OverrideFields.Validate(OverrideFields.HitPointsMax, value);
        current.Update(new OverrideEntry(current.Field, value, current.Note));
    }

    /// <summary>After a level-up: the current hit points grow by what the maximum grew (never above <paramref name="maxHp"/>).</summary>
    public void GainHitPoints(int amount, int maxHp, DateTimeOffset now)
    {
        var max = Math.Max(0, maxHp);
        HitPointsCurrent = Status == CharacterStatus.Draft ? max : Math.Min(max, HitPointsCurrent + Math.Max(0, amount));
        Touch(now);
    }

    private static string ProficiencySlug(string key) =>
        new string(key.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray()).Replace("skill", string.Empty, StringComparison.Ordinal);

    /// <summary>A direct class edit that reaches the pending level makes the grant moot.</summary>
    private void ClearStaleLevelUp()
    {
        if (PendingLevelUpTo is { } target && TotalLevel >= target)
        {
            ClearLevelUp();
        }
    }

    private void ClearLevelUp()
    {
        PendingLevelUpTo = null;
        LevelGrantedByUserId = null;
        LevelGrantedAt = null;
    }
}
