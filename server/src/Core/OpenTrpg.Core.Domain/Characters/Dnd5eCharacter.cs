using System.Text.Json;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Items;
using OpenTrpg.Core.Domain.Rules;

namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// D&amp;D 5e part of a character: stored sheet data, combat tracking state and its child collections
/// (classes, proficiencies, spells, spent slots, resources, overrides, choices). It shares the row (and the
/// key) of the core <see cref="Characters.Character"/> (table splitting), which keeps identity, owner,
/// lifecycle, texts, money and inventory. Calculated values live in <see cref="CharacterSheet"/> (see
/// <see cref="SheetCalculator"/>); operations that need one of them (maximum hit points, slot maxima, hit
/// die sizes) take it as a parameter. Every change calls <see cref="Characters.Character.Touch"/> on the core
/// character (optimistic concurrency).
/// </summary>
public sealed partial class Dnd5eCharacter
{
    public const int IndexMaxLength = 100;
    public const int AlignmentMaxLength = 50;

    /// <summary>Result of the optional table of the background, e.g. "Especialidad: Bibliotecario".</summary>
    public const int BackgroundDetailMaxLength = 200;
    public const int MaxDeathSaves = 3;
    public const int MaxExhaustionLevel = 6;
    public const int MaxTemporaryHitPoints = 999;
    public const int MaxConditions = 20;
    public const int ConditionNoteMaxLength = 200;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<CharacterClassLevel> _classes = [];
    private readonly List<CharacterProficiency> _proficiencies = [];
    private readonly List<CharacterSpell> _spells = [];
    private readonly List<SpellSlotState> _spellSlots = [];
    private readonly List<CharacterResource> _resources = [];
    private readonly List<CharacterOverride> _overrides = [];

    private Dnd5eCharacter()
    {
    }

    /// <summary>Same key as the core character (table splitting).</summary>
    public Guid Id { get; private set; }

    /// <summary>The core character this sheet belongs to (identity, owner, lifecycle, texts, money and inventory).</summary>
    public Character Character { get; private set; } = null!;

    // Shortcuts to the core character used by the rules (not mapped).
    public Guid CampaignId => Character.CampaignId;

    public Guid? OwnerUserId => Character.OwnerUserId;

    public string Name => Character.Name;

    public CharacterStatus Status => Character.Status;

    public IReadOnlyCollection<CharacterItem> Items => Character.Items;

    public DateTimeOffset CreatedAt => Character.CreatedAt;

    public string? RaceIndex { get; private set; }

    public string? SubraceIndex { get; private set; }

    public string? BackgroundIndex { get; private set; }

    public string? Alignment { get; private set; }

    public bool ApplyRacialBonuses { get; private set; } = true;

    public HpMode HpMode { get; private set; }

    public int BaseStr { get; private set; } = 10;

    public int BaseDex { get; private set; } = 10;

    public int BaseCon { get; private set; } = 10;

    public int BaseInt { get; private set; } = 10;

    public int BaseWis { get; private set; } = 10;

    public int BaseCha { get; private set; } = 10;

    public int HitPointsCurrent { get; private set; }

    public int TemporaryHitPoints { get; private set; }

    public int DeathSaveSuccesses { get; private set; }

    public int DeathSaveFailures { get; private set; }

    public int ExhaustionLevel { get; private set; }

    /// <summary>Persisted form of <see cref="Conditions"/>: <c>[{"index":"poisoned","note":null}]</c>.</summary>
    public string ConditionsJson { get; private set; } = "[]";

    public string? ConcentratingOnSpellIndex { get; private set; }

    public bool Inspiration { get; private set; }

    /// <summary>Persisted form of <see cref="HitDiceUsed"/>: <c>{"fighter":2}</c>.</summary>
    public string HitDiceUsedJson { get; private set; } = "{}";

    /// <summary>Result of the optional table of the background (specialty, scheme, origin...).</summary>
    public string BackgroundDetail { get; private set; } = string.Empty;

    public IReadOnlyCollection<CharacterClassLevel> Classes => _classes;

    public IReadOnlyCollection<CharacterProficiency> Proficiencies => _proficiencies;

    public IReadOnlyCollection<CharacterSpell> Spells => _spells;

    public IReadOnlyCollection<SpellSlotState> SpellSlots => _spellSlots;

    public IReadOnlyCollection<CharacterResource> Resources => _resources;

    public IReadOnlyCollection<CharacterOverride> Overrides => _overrides;

    /// <summary>Classes sorted by <see cref="CharacterClassLevel.Order"/> (main class first).</summary>
    public IReadOnlyList<CharacterClassLevel> OrderedClasses => [.. _classes.OrderBy(c => c.Order)];

    public AbilityScores BaseAbilities => new(BaseStr, BaseDex, BaseCon, BaseInt, BaseWis, BaseCha);

    /// <summary>Sum of the class levels; 0 while the character has no class.</summary>
    public int TotalLevel => _classes.Sum(c => c.Level);

    public IReadOnlyList<CharacterCondition> Conditions =>
        JsonSerializer.Deserialize<List<CharacterCondition>>(ConditionsJson, JsonOptions) ?? [];

    /// <summary>Spent hit dice per class index.</summary>
    public IReadOnlyDictionary<string, int> HitDiceUsed =>
        JsonSerializer.Deserialize<Dictionary<string, int>>(HitDiceUsedJson, JsonOptions) ?? [];

    /// <summary>The 5e part of a new draft: all base scores at 10, average hit points and racial bonuses on.</summary>
    public static Dnd5eCharacter Create(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return new Dnd5eCharacter { Id = character.Id, Character = character, HpMode = HpMode.Average };
    }

    // ---- Permissions (core) -------------------------------------------------------------------

    public bool IsOwnedBy(Guid userId) => Character.IsOwnedBy(userId);

    public bool CanViewSheet(Guid actorUserId, bool actorIsDm) => Character.CanViewSheet(actorUserId, actorIsDm);

    public SheetEditMode ResolveSheetEdit(Guid actorUserId, bool actorIsDm) => Character.ResolveSheetEdit(actorUserId, actorIsDm);

    public void EnsureCanTrack(Guid actorUserId, bool actorIsDm) => Character.EnsureCanTrack(actorUserId, actorIsDm);

    // ---- Lifecycle -----------------------------------------------------------------------------

    /// <summary>Draft → Active (<see cref="Characters.Character.Activate"/>). The character enters play at full hit points.</summary>
    public void Activate(int maxHp, DateTimeOffset now)
    {
        Character.Activate(now);
        EnterPlay(maxHp);
    }

    /// <summary>The 5e part of an activation, once the core character is active: full hit points.</summary>
    public void EnterPlay(int maxHp) => HitPointsCurrent = Math.Max(0, maxHp);

    /// <summary>
    /// Keeps the current hit points consistent after the maximum changed (sheet edits): a draft always
    /// has full hit points; an active character is capped at the new maximum.
    /// </summary>
    public void RefreshHitPoints(int maxHp)
    {
        var max = Math.Max(0, maxHp);
        HitPointsCurrent = Status == CharacterStatus.Draft ? max : Math.Min(HitPointsCurrent, max);
    }

    // ---- Sheet edits ---------------------------------------------------------------------------

    /// <summary>
    /// Applies a sheet edit (direct edit or approved change request). Everything is validated before
    /// anything changes. Afterwards the caller should recalculate the sheet, then call
    /// <see cref="SyncAutoResources"/> and <see cref="RefreshHitPoints"/>.
    /// </summary>
    public void ApplySheetEdit(SheetEdit edit, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(edit);

        var profile = Character.PrepareProfile(edit.Profile);
        var race = edit.RaceIndex is null ? RaceIndex : NormalizeOptional(edit.RaceIndex, IndexMaxLength, "raza");
        var subrace = edit.SubraceIndex is not null
            ? NormalizeOptional(edit.SubraceIndex, IndexMaxLength, "subraza")
            : race == RaceIndex ? SubraceIndex : null;
        if (subrace is not null && race is null)
        {
            throw DomainException.RuleViolation("No se puede elegir una subraza sin raza.");
        }

        var background = edit.BackgroundIndex is null ? BackgroundIndex : NormalizeOptional(edit.BackgroundIndex, IndexMaxLength, "trasfondo");
        var alignment = edit.Alignment is null ? Alignment : NormalizeOptional(edit.Alignment, AlignmentMaxLength, "alineamiento");
        edit.BaseAbilities?.Validate();
        var classes = edit.Classes is null ? null : NormalizeClasses(edit.Classes);
        var proficiencies = edit.Proficiencies is null ? null : NormalizeProficiencies(edit.Proficiencies);
        var spells = edit.Spells is null ? null : NormalizeSpells(edit.Spells);
        if (spells is not null && edit.KeepSpellPreparation)
        {
            spells = spells
                .Select(e => _spells.FirstOrDefault(s => s.SpellIndex == e.SpellIndex && s.ClassIndex == e.ClassIndex) is { } existing && !e.AlwaysPrepared
                    ? e with { IsPrepared = existing.IsPrepared }
                    : e)
                .ToList();
        }

        var overrides = edit.Overrides is null ? null : NormalizeOverrides(edit.Overrides);
        var hpMode = edit.HpMode ?? HpMode;
        EnsureHpModeConsistent(hpMode, overrides?.Select(o => o.Field) ?? _overrides.Select(o => o.Field));
        var backgroundDetail = edit.BackgroundDetail is null
            ? BackgroundDetail
            : NormalizePersonality(edit.BackgroundDetail, "Los detalles del trasfondo", BackgroundDetailMaxLength);

        DropStaleOriginChoices(race, subrace, background);
        Character.ApplyProfile(profile);
        RaceIndex = race;
        SubraceIndex = subrace;
        BackgroundIndex = background;
        Alignment = alignment;
        ApplyRacialBonuses = edit.ApplyRacialBonuses ?? ApplyRacialBonuses;
        HpMode = hpMode;
        if (edit.BaseAbilities is { } abilities)
        {
            ApplyBaseAbilities(abilities);
        }

        if (classes is not null)
        {
            ApplyClasses(classes);
        }

        if (proficiencies is not null)
        {
            ApplyProficiencies(proficiencies);
        }

        if (spells is not null)
        {
            ApplySpells(spells);
        }

        if (overrides is not null)
        {
            ApplyOverrides(overrides);
        }

        if (proficiencies is not null || spells is not null)
        {
            // Proficiencies and the cantrip chosen for the race or the background survive a full replacement.
            ReapplyOriginChoiceEffects();
        }

        BackgroundDetail = backgroundDetail;
        Touch(now);
    }

    public void SetBaseAbilities(AbilityScores abilities, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        abilities.Validate();
        ApplyBaseAbilities(abilities);
        Touch(now);
    }

    /// <summary>Switching to <see cref="HpMode.Manual"/> requires an existing <c>hitPointsMax</c> override.</summary>
    public void SetHpMode(HpMode mode, DateTimeOffset now)
    {
        EnsureHpModeConsistent(mode, _overrides.Select(o => o.Field));
        HpMode = mode;
        Touch(now);
    }

    /// <summary>
    /// Replaces the classes (list order = <see cref="CharacterClassLevel.Order"/>, first = main class).
    /// Spent hit dice of removed classes are dropped and the rest capped at the new levels.
    /// </summary>
    public void ReplaceClasses(IEnumerable<ClassEntry> classes, DateTimeOffset now)
    {
        ApplyClasses(NormalizeClasses(classes));
        Touch(now);
    }

    public void ReplaceProficiencies(IEnumerable<ProficiencyEntry> proficiencies, DateTimeOffset now)
    {
        ApplyProficiencies(NormalizeProficiencies(proficiencies));
        Touch(now);
    }

    public void ReplaceSpells(IEnumerable<SpellEntry> spells, DateTimeOffset now)
    {
        ApplySpells(NormalizeSpells(spells));
        Touch(now);
    }

    /// <summary>Replaces the overrides. Removing <c>hitPointsMax</c> while in <see cref="HpMode.Manual"/> is rejected.</summary>
    public void ReplaceOverrides(IEnumerable<OverrideEntry> overrides, DateTimeOffset now)
    {
        var normalized = NormalizeOverrides(overrides);
        EnsureHpModeConsistent(HpMode, normalized.Select(o => o.Field));
        ApplyOverrides(normalized);
        Touch(now);
    }

    // ---- Combat tracking -----------------------------------------------------------------------

    /// <summary>Applies combat tracking changes. <paramref name="maxHp"/> is the sheet's maximum hit points.</summary>
    public void ApplyCombatUpdate(CombatUpdate update, int maxHp, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(update);

        var max = Math.Max(0, maxHp);
        if (update.HitPointsCurrent is { } hp && (hp < 0 || hp > max))
        {
            throw DomainException.RuleViolation($"Los puntos de golpe actuales deben estar entre 0 y {max}.");
        }

        if (update.TemporaryHitPoints is < 0 or > MaxTemporaryHitPoints)
        {
            throw DomainException.RuleViolation($"Los puntos de golpe temporales deben estar entre 0 y {MaxTemporaryHitPoints}.");
        }

        if (update.DeathSaveSuccesses is < 0 or > MaxDeathSaves || update.DeathSaveFailures is < 0 or > MaxDeathSaves)
        {
            throw DomainException.RuleViolation($"Las salvaciones contra muerte deben estar entre 0 y {MaxDeathSaves}.");
        }

        if (update.ExhaustionLevel is < 0 or > MaxExhaustionLevel)
        {
            throw DomainException.RuleViolation($"El nivel de agotamiento debe estar entre 0 y {MaxExhaustionLevel}.");
        }

        var conditions = update.Conditions is null ? null : NormalizeConditions(update.Conditions);

        HitPointsCurrent = update.HitPointsCurrent ?? HitPointsCurrent;
        if (update.HitPointsCurrent == 0)
        {
            // Unconscious at 0 hit points: concentration ends.
            ConcentratingOnSpellIndex = null;
        }

        TemporaryHitPoints = update.TemporaryHitPoints ?? TemporaryHitPoints;
        DeathSaveSuccesses = update.DeathSaveSuccesses ?? DeathSaveSuccesses;
        DeathSaveFailures = update.DeathSaveFailures ?? DeathSaveFailures;
        ExhaustionLevel = update.ExhaustionLevel ?? ExhaustionLevel;
        Inspiration = update.Inspiration ?? Inspiration;
        if (conditions is not null)
        {
            ConditionsJson = JsonSerializer.Serialize(conditions, JsonOptions);
        }

        Touch(now);
    }

    /// <summary>
    /// Takes damage: temporary hit points absorb it first, the rest lowers the current hit points
    /// (never below 0). <paramref name="amount"/> must not be negative. A concentrating character that drops
    /// to 0 hit points stops concentrating; one that stays above 0 must make a Constitution saving throw
    /// (DC max(10, damage / 2)), which the result reports (<see cref="DamageResult.ConcentrationCheckDc"/>).
    /// </summary>
    public DamageResult ApplyDamage(int amount, DateTimeOffset now)
    {
        if (amount < 0)
        {
            throw DomainException.RuleViolation("El daño no puede ser negativo.");
        }

        var absorbed = Math.Min(TemporaryHitPoints, amount);
        TemporaryHitPoints -= absorbed;
        HitPointsCurrent = Math.Max(0, HitPointsCurrent - (amount - absorbed));
        var concentrating = ConcentratingOnSpellIndex;
        int? checkDc = null;
        var ended = false;
        if (concentrating is not null && amount > 0)
        {
            if (HitPointsCurrent == 0)
            {
                ConcentratingOnSpellIndex = null;
                ended = true;
            }
            else
            {
                checkDc = ConcentrationCheckDc(amount);
            }
        }

        Touch(now);
        return new DamageResult(amount, absorbed, HitPointsCurrent, concentrating, checkDc, ended);
    }

    /// <summary>DC of the Constitution saving throw to keep concentrating after taking damage: max(10, damage / 2).</summary>
    public static int ConcentrationCheckDc(int damage) => Math.Max(10, damage / 2);

    /// <summary>
    /// Heals up to <paramref name="maxHp"/> (the sheet's maximum hit points). Healing a character at 0
    /// hit points brings it back: the death saves are reset. <paramref name="amount"/> must not be negative.
    /// </summary>
    public void Heal(int amount, int maxHp, DateTimeOffset now)
    {
        if (amount < 0)
        {
            throw DomainException.RuleViolation("La curación no puede ser negativa.");
        }

        if (amount > 0 && HitPointsCurrent == 0)
        {
            DeathSaveSuccesses = 0;
            DeathSaveFailures = 0;
        }

        var max = Math.Max(0, maxHp);
        HitPointsCurrent = Math.Max(HitPointsCurrent, Math.Min(max, HitPointsCurrent + amount));
        Touch(now);
    }

    /// <summary>Starts concentrating on a spell, or stops when <paramref name="spellIndex"/> is null or empty.</summary>
    public void SetConcentration(string? spellIndex, DateTimeOffset now)
    {
        ConcentratingOnSpellIndex = spellIndex is null ? null : NormalizeOptional(spellIndex, IndexMaxLength, "conjuro");
        Touch(now);
    }

    /// <summary>Spent slots of a level (0 = pact).</summary>
    public int SpellSlotsUsed(int level) => _spellSlots.FirstOrDefault(s => s.Level == level)?.Used ?? 0;

    /// <summary>
    /// Spends slots of a level (1-9, or <see cref="SpellSlotState.PactLevel"/> for pact slots).
    /// <paramref name="maxSlots"/> is the sheet's maximum for that level (<see cref="CharacterSheet.SpellSlotMax"/>).
    /// </summary>
    public SpellSlotState SpendSpellSlot(int level, int amount, int maxSlots, DateTimeOffset now)
    {
        ValidateSlotLevel(level);
        ValidateAmount(amount);

        var used = SpellSlotsUsed(level);
        if (used + amount > maxSlots)
        {
            throw DomainException.RuleViolation(level == SpellSlotState.PactLevel
                ? "No quedan espacios de conjuro de pacto."
                : $"No quedan espacios de conjuro de nivel {level}.");
        }

        var slot = GetOrCreateSlot(level);
        slot.SetUsed(used + amount);
        Touch(now);
        return slot;
    }

    /// <summary>Gives back spent slots of a level (0 = pact). Restoring more than spent is rejected.</summary>
    public SpellSlotState RestoreSpellSlot(int level, int amount, DateTimeOffset now)
    {
        ValidateSlotLevel(level);
        ValidateAmount(amount);

        var used = SpellSlotsUsed(level);
        if (amount > used)
        {
            throw DomainException.RuleViolation(level == SpellSlotState.PactLevel
                ? "No hay espacios de conjuro de pacto gastados que recuperar."
                : $"No hay espacios de conjuro de nivel {level} gastados que recuperar.");
        }

        var slot = GetOrCreateSlot(level);
        slot.SetUsed(used - amount);
        Touch(now);
        return slot;
    }

    public CharacterResource SpendResource(Guid resourceId, int amount, DateTimeOffset now)
    {
        ValidateAmount(amount);
        var resource = FindResource(resourceId);
        if (resource.Used + amount > resource.Max)
        {
            throw DomainException.RuleViolation($"No quedan usos de {resource.Name}.");
        }

        resource.SetUsed(resource.Used + amount);
        Touch(now);
        return resource;
    }

    public CharacterResource RestoreResource(Guid resourceId, int amount, DateTimeOffset now)
    {
        ValidateAmount(amount);
        var resource = FindResource(resourceId);
        if (amount > resource.Used)
        {
            throw DomainException.RuleViolation($"No hay usos gastados de {resource.Name} que recuperar.");
        }

        resource.SetUsed(resource.Used - amount);
        Touch(now);
        return resource;
    }

    /// <summary>Resources whose dice must be rolled after the last rest (<see cref="CharacterResource.RollsPending"/>).</summary>
    public bool RestRollsPending => _resources.Any(r => r.RollsPending);

    /// <summary>Stores the values rolled for a resource that rolls after resting (e.g. two d20 after a long rest).</summary>
    public CharacterResource RecordResourceRolls(Guid resourceId, IReadOnlyList<int> values, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(values);
        var resource = FindResource(resourceId);
        resource.RecordRolls(values);
        Touch(now);
        return resource;
    }

    public CharacterResource AddManualResource(string name, int max, ResourceRecharge recharge, DateTimeOffset now)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > CharacterResource.NameMaxLength)
        {
            throw DomainException.RuleViolation($"El nombre del recurso debe tener entre 1 y {CharacterResource.NameMaxLength} caracteres.");
        }

        if (max is < 1 or > CharacterResource.MaxUses)
        {
            throw DomainException.RuleViolation($"Los usos máximos deben estar entre 1 y {CharacterResource.MaxUses}.");
        }

        if (!Enum.IsDefined(recharge))
        {
            throw DomainException.RuleViolation("El tipo de recarga no es válido.");
        }

        var resource = CharacterResource.CreateManual(Id, trimmed, max, recharge);
        _resources.Add(resource);
        Touch(now);
        return resource;
    }

    /// <summary>Removes a manual resource. Automatic resources cannot be removed (they follow the classes).</summary>
    public void RemoveManualResource(Guid resourceId, DateTimeOffset now)
    {
        var resource = FindResource(resourceId);
        if (resource.IsAuto)
        {
            throw DomainException.RuleViolation("Los recursos automáticos de clase no se pueden borrar.");
        }

        _resources.Remove(resource);
        Touch(now);
    }

    /// <summary>
    /// Regenerates the automatic resources from <see cref="ClassResourceRules"/> templates: existing keys
    /// are updated keeping their spent uses (capped at the new maximum), new keys are added unused and
    /// automatic resources whose key no longer applies are removed. Duplicate keys keep the highest maximum.
    /// </summary>
    public void SyncAutoResources(IEnumerable<ResourceTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);

        var byKey = templates
            .GroupBy(t => t.Key, StringComparer.Ordinal)
            .Select(g => g.MaxBy(t => t.Max)!)
            .ToDictionary(t => t.Key, StringComparer.Ordinal);

        _resources.RemoveAll(r => r.IsAuto && (r.Key is null || !byKey.ContainsKey(r.Key)));

        foreach (var template in byKey.Values)
        {
            var existing = _resources.FirstOrDefault(r => r.IsAuto && r.Key == template.Key);
            if (existing is null)
            {
                _resources.Add(CharacterResource.CreateAuto(Id, template));
            }
            else
            {
                existing.UpdateFromTemplate(template);
            }
        }
    }

    // ---- Rests ---------------------------------------------------------------------------------

    /// <summary>Remaining hit dice of a class (its level minus the spent ones); 0 if the character lacks the class.</summary>
    public int HitDiceRemaining(string classIndex)
    {
        var level = _classes.FirstOrDefault(c => c.ClassIndex == classIndex)?.Level ?? 0;
        return Math.Max(0, level - HitDiceUsed.GetValueOrDefault(classIndex));
    }

    /// <summary>
    /// Short rest: spends the requested hit dice (each heals die roll + Con, at least 0, never above
    /// <paramref name="maxHp"/>), resets the spent uses of <see cref="ResourceRecharge.ShortRest"/>
    /// resources and recovers the pact slots.
    /// </summary>
    /// <param name="hitDiceToSpend">Dice to spend per class index (0 allowed).</param>
    /// <param name="hitDieByClass">Die size per class index (e.g. "fighter" → 10).</param>
    public ShortRestResult ShortRest(
        IReadOnlyDictionary<string, int> hitDiceToSpend,
        IReadOnlyDictionary<string, int> hitDieByClass,
        int conModifier,
        int maxHp,
        IDiceRoller dice,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(hitDiceToSpend);
        ArgumentNullException.ThrowIfNull(hitDieByClass);
        ArgumentNullException.ThrowIfNull(dice);

        foreach (var (classIndex, count) in hitDiceToSpend)
        {
            if (count < 0)
            {
                throw DomainException.RuleViolation("El número de dados de golpe no puede ser negativo.");
            }

            if (!_classes.Any(c => c.ClassIndex == classIndex))
            {
                throw DomainException.RuleViolation($"El personaje no tiene la clase '{classIndex}'.");
            }

            if (count > HitDiceRemaining(classIndex))
            {
                throw DomainException.RuleViolation($"No quedan suficientes dados de golpe de '{classIndex}'.");
            }

            if (count > 0 && !hitDieByClass.ContainsKey(classIndex))
            {
                throw new ArgumentException($"Missing hit die size for class '{classIndex}'.", nameof(hitDieByClass));
            }
        }

        var max = Math.Max(0, maxHp);
        var used = new Dictionary<string, int>(HitDiceUsed, StringComparer.Ordinal);
        var rolls = new List<HitDieRoll>();
        var restored = 0;
        foreach (var (classIndex, count) in hitDiceToSpend)
        {
            var die = count > 0 ? hitDieByClass[classIndex] : 0;
            for (var i = 0; i < count; i++)
            {
                var roll = dice.Roll(die);
                var healing = Math.Max(0, roll + conModifier);
                rolls.Add(new HitDieRoll(classIndex, die, roll, healing));
                var gained = Math.Min(healing, Math.Max(0, max - HitPointsCurrent));
                HitPointsCurrent += gained;
                restored += gained;
            }

            if (count > 0)
            {
                used[classIndex] = used.GetValueOrDefault(classIndex) + count;
            }
        }

        SetHitDiceUsed(used);
        foreach (var resource in _resources.Where(r => r.Recharge == ResourceRecharge.ShortRest))
        {
            resource.SetUsed(0);
        }

        foreach (var resource in _resources)
        {
            resource.AfterRest(RestKind.Short);
        }

        _spellSlots.FirstOrDefault(s => s.Level == SpellSlotState.PactLevel)?.SetUsed(0);
        Touch(now);
        return new ShortRestResult(rolls, restored);
    }

    /// <summary>Short rest taking die sizes, Con modifier and maximum hit points from the calculated sheet.</summary>
    public ShortRestResult ShortRest(IReadOnlyDictionary<string, int> hitDiceToSpend, CharacterSheet sheet, IDiceRoller dice, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return ShortRest(
            hitDiceToSpend,
            sheet.HitDice.ToDictionary(h => h.ClassIndex, h => h.Die, StringComparer.Ordinal),
            sheet.Modifier(Abilities.Con),
            sheet.HitPointsMax,
            dice,
            now);
    }

    /// <summary>
    /// Long rest: hit points to <paramref name="maxHp"/>, every slot recovered, short and long rest
    /// resources restored, recovers max(1, total level / 2) spent hit dice (main class first),
    /// exhaustion −1, death saves reset and concentration ends. Temporary hit points are kept.
    /// </summary>
    public void LongRest(int maxHp, DateTimeOffset now)
    {
        HitPointsCurrent = Math.Max(0, maxHp);

        foreach (var slot in _spellSlots)
        {
            slot.SetUsed(0);
        }

        foreach (var resource in _resources.Where(r => r.Recharge is ResourceRecharge.ShortRest or ResourceRecharge.LongRest))
        {
            resource.SetUsed(0);
        }

        foreach (var resource in _resources)
        {
            resource.AfterRest(RestKind.Long);
        }

        var toRecover = Math.Max(1, TotalLevel / 2);
        var used = new Dictionary<string, int>(HitDiceUsed, StringComparer.Ordinal);
        foreach (var characterClass in OrderedClasses)
        {
            if (toRecover == 0)
            {
                break;
            }

            var spent = used.GetValueOrDefault(characterClass.ClassIndex);
            var recovered = Math.Min(spent, toRecover);
            used[characterClass.ClassIndex] = spent - recovered;
            toRecover -= recovered;
        }

        SetHitDiceUsed(used);
        ExhaustionLevel = Math.Max(0, ExhaustionLevel - 1);
        DeathSaveSuccesses = 0;
        DeathSaveFailures = 0;
        ConcentratingOnSpellIndex = null;
        Touch(now);
    }

    // ---- Helpers -------------------------------------------------------------------------------

    private void Touch(DateTimeOffset now) => Character.Touch(now);

    /// <summary>Trims; empty becomes null.</summary>
    private static string? NormalizeOptional(string value, int maxLength, string label)
    {
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw DomainException.RuleViolation($"El valor de {label} no puede superar los {maxLength} caracteres.");
        }

        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string RequireIndex(string? value, string label)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > IndexMaxLength)
        {
            throw DomainException.RuleViolation($"El índice de {label} debe tener entre 1 y {IndexMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static string NormalizePersonality(string value, string label, int maxLength)
    {
        var text = value.Trim();
        if (text.Length > maxLength)
        {
            throw DomainException.RuleViolation($"{label} no pueden superar los {maxLength} caracteres.");
        }

        return text;
    }

    private static List<ClassEntry> NormalizeClasses(IEnumerable<ClassEntry> classes)
    {
        ArgumentNullException.ThrowIfNull(classes);

        var result = new List<ClassEntry>();
        foreach (var entry in classes)
        {
            var classIndex = RequireIndex(entry.ClassIndex, "clase");
            var subclass = entry.SubclassIndex is null ? null : NormalizeOptional(entry.SubclassIndex, IndexMaxLength, "subclase");
            if (entry.Level is < AbilityRules.MinLevel or > AbilityRules.MaxLevel)
            {
                throw DomainException.RuleViolation($"El nivel de clase debe estar entre {AbilityRules.MinLevel} y {AbilityRules.MaxLevel}.");
            }

            if (result.Any(c => c.ClassIndex == classIndex))
            {
                throw DomainException.RuleViolation($"La clase '{classIndex}' está repetida.");
            }

            result.Add(new ClassEntry(classIndex, subclass, entry.Level));
        }

        if (result.Sum(c => c.Level) > AbilityRules.MaxLevel)
        {
            throw DomainException.RuleViolation($"El nivel total no puede superar {AbilityRules.MaxLevel}.");
        }

        return result;
    }

    private static List<ProficiencyEntry> NormalizeProficiencies(IEnumerable<ProficiencyEntry> proficiencies)
    {
        ArgumentNullException.ThrowIfNull(proficiencies);

        var result = new List<ProficiencyEntry>();
        foreach (var entry in proficiencies)
        {
            if (!Enum.IsDefined(entry.Type) || !Enum.IsDefined(entry.Source))
            {
                throw DomainException.RuleViolation("El tipo u origen de la competencia no es válido.");
            }

            var key = RequireIndex(entry.Key, "competencia");
            if (entry.Expertise && entry.Type is not (ProficiencyType.Skill or ProficiencyType.Tool))
            {
                throw DomainException.RuleViolation("Solo las habilidades y herramientas admiten pericia.");
            }

            if (result.Any(p => p.Type == entry.Type && p.Key == key))
            {
                throw DomainException.RuleViolation($"La competencia '{key}' está repetida.");
            }

            result.Add(entry with { Key = key });
        }

        return result;
    }

    private static List<SpellEntry> NormalizeSpells(IEnumerable<SpellEntry> spells)
    {
        ArgumentNullException.ThrowIfNull(spells);

        var result = new List<SpellEntry>();
        foreach (var entry in spells)
        {
            var spellIndex = RequireIndex(entry.SpellIndex, "conjuro");
            var classIndex = RequireIndex(entry.ClassIndex, "clase");
            if (result.Any(s => s.SpellIndex == spellIndex && s.ClassIndex == classIndex))
            {
                throw DomainException.RuleViolation($"El conjuro '{spellIndex}' está repetido.");
            }

            result.Add(new SpellEntry(spellIndex, classIndex, entry.IsPrepared || entry.AlwaysPrepared, entry.AlwaysPrepared));
        }

        return result;
    }

    private static List<OverrideEntry> NormalizeOverrides(IEnumerable<OverrideEntry> overrides)
    {
        ArgumentNullException.ThrowIfNull(overrides);

        var result = new List<OverrideEntry>();
        foreach (var entry in overrides)
        {
            var field = (entry.Field ?? string.Empty).Trim();
            OverrideFields.Validate(field, entry.Value);
            var note = entry.Note is null ? null : NormalizeOptional(entry.Note, CharacterOverride.NoteMaxLength, "la nota");
            if (result.Any(o => o.Field == field))
            {
                throw DomainException.RuleViolation($"El campo '{field}' está sobrescrito más de una vez.");
            }

            result.Add(new OverrideEntry(field, entry.Value, note));
        }

        return result;
    }

    private static List<CharacterCondition> NormalizeConditions(IEnumerable<CharacterCondition> conditions)
    {
        var result = new List<CharacterCondition>();
        foreach (var condition in conditions)
        {
            var index = RequireIndex(condition.Index, "condición");
            var note = condition.Note is null ? null : NormalizeOptional(condition.Note, ConditionNoteMaxLength, "la nota");
            if (result.Any(c => c.Index == index))
            {
                throw DomainException.RuleViolation($"La condición '{index}' está repetida.");
            }

            result.Add(new CharacterCondition(index, note));
        }

        if (result.Count > MaxConditions)
        {
            throw DomainException.RuleViolation($"Un personaje no puede tener más de {MaxConditions} condiciones.");
        }

        return result;
    }

    private static void EnsureHpModeConsistent(HpMode mode, IEnumerable<string> overrideFields)
    {
        if (!Enum.IsDefined(mode))
        {
            throw DomainException.RuleViolation("El modo de puntos de golpe no es válido.");
        }

        if (mode == HpMode.Manual && !overrideFields.Contains(OverrideFields.HitPointsMax, StringComparer.Ordinal))
        {
            throw DomainException.RuleViolation("El modo manual de puntos de golpe exige sobrescribir los puntos de golpe máximos.");
        }
    }

    private static void ValidateSlotLevel(int level)
    {
        if (level is < SpellSlotState.PactLevel or > 9)
        {
            throw DomainException.RuleViolation("El nivel del espacio de conjuro debe estar entre 1 y 9 (0 para los de pacto).");
        }
    }

    private static void ValidateAmount(int amount)
    {
        if (amount < 1)
        {
            throw DomainException.RuleViolation("La cantidad debe ser al menos 1.");
        }
    }

    private void ApplyBaseAbilities(AbilityScores abilities)
    {
        BaseStr = abilities.Str;
        BaseDex = abilities.Dex;
        BaseCon = abilities.Con;
        BaseInt = abilities.Int;
        BaseWis = abilities.Wis;
        BaseCha = abilities.Cha;
    }

    private void ApplyClasses(List<ClassEntry> classes)
    {
        _classes.RemoveAll(c => !classes.Any(e => e.ClassIndex == c.ClassIndex));
        for (var order = 0; order < classes.Count; order++)
        {
            var entry = classes[order];
            var existing = _classes.FirstOrDefault(c => c.ClassIndex == entry.ClassIndex);
            if (existing is null)
            {
                _classes.Add(CharacterClassLevel.Create(Id, entry, order));
            }
            else
            {
                existing.Update(entry, order);
            }
        }

        var used = HitDiceUsed
            .Where(u => classes.Any(c => c.ClassIndex == u.Key))
            .ToDictionary(u => u.Key, u => Math.Min(u.Value, classes.First(c => c.ClassIndex == u.Key).Level), StringComparer.Ordinal);
        SetHitDiceUsed(used);
        ClearStaleLevelUp();
    }

    private void ApplyProficiencies(List<ProficiencyEntry> proficiencies)
    {
        _proficiencies.RemoveAll(p => !proficiencies.Any(e => e.Type == p.Type && e.Key == p.Key));
        foreach (var entry in proficiencies)
        {
            var existing = _proficiencies.FirstOrDefault(p => p.Type == entry.Type && p.Key == entry.Key);
            if (existing is null)
            {
                _proficiencies.Add(CharacterProficiency.Create(Id, entry));
            }
            else
            {
                existing.Update(entry);
            }
        }
    }

    private void ApplySpells(List<SpellEntry> spells)
    {
        _spells.RemoveAll(s => !spells.Any(e => e.SpellIndex == s.SpellIndex && e.ClassIndex == s.ClassIndex));
        foreach (var entry in spells)
        {
            var existing = _spells.FirstOrDefault(s => s.SpellIndex == entry.SpellIndex && s.ClassIndex == entry.ClassIndex);
            if (existing is null)
            {
                _spells.Add(CharacterSpell.Create(Id, entry));
            }
            else
            {
                existing.Update(entry);
            }
        }
    }

    private void ApplyOverrides(List<OverrideEntry> overrides)
    {
        _overrides.RemoveAll(o => !overrides.Any(e => e.Field == o.Field));
        foreach (var entry in overrides)
        {
            var existing = _overrides.FirstOrDefault(o => o.Field == entry.Field);
            if (existing is null)
            {
                _overrides.Add(CharacterOverride.Create(Id, entry));
            }
            else
            {
                existing.Update(entry);
            }
        }
    }

    private void SetHitDiceUsed(Dictionary<string, int> used) =>
        HitDiceUsedJson = JsonSerializer.Serialize(used.Where(u => u.Value > 0).ToDictionary(u => u.Key, u => u.Value), JsonOptions);

    private SpellSlotState GetOrCreateSlot(int level)
    {
        var slot = _spellSlots.FirstOrDefault(s => s.Level == level);
        if (slot is null)
        {
            slot = SpellSlotState.Create(Id, level);
            _spellSlots.Add(slot);
        }

        return slot;
    }

    private CharacterResource FindResource(Guid resourceId) =>
        _resources.FirstOrDefault(r => r.Id == resourceId) ?? throw DomainException.NotFound("El recurso no existe.");
}
