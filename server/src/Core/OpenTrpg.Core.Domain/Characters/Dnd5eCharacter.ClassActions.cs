using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// Class actions of the combat view (combat tracking: owner and DMs, without approval). Each one
/// checks that the character has the class feature, then spends the matching resource or slot.
/// </summary>
public sealed partial class Dnd5eCharacter
{
    private const string BarbarianClass = "barbarian";
    private const string PaladinClass = "paladin";
    private const string WizardClass = "wizard";
    private const string DruidClass = "druid";

    /// <summary>Divine Smite is gained at paladin level 2.</summary>
    public const int DivineSmiteMinLevel = 2;

    /// <summary>Barbarian Rage: spends one use of the <see cref="ClassResourceRules.Rage"/> resource.</summary>
    public CharacterResource Rage(DateTimeOffset now)
    {
        RequireClass(BarbarianClass, 1, "Solo un bárbaro puede entrar en furia.");
        var resource = FindAutoResource(ClassResourceRules.Rage, "Furia");
        return SpendResource(resource.Id, 1, now);
    }

    /// <summary>
    /// Paladin Lay on Hands: spends <paramref name="amount"/> points of the pool. With
    /// <paramref name="targetSelf"/> the character heals that amount, never above <paramref name="maxHp"/>;
    /// otherwise another creature is healed and only the pool changes. Returns the hit points healed.
    /// </summary>
    public int LayOnHands(int amount, bool targetSelf, int maxHp, DateTimeOffset now)
    {
        RequireClass(PaladinClass, 1, "Solo un paladín puede usar Imposición de manos.");
        ValidateAmount(amount);
        var pool = FindAutoResource(ClassResourceRules.LayOnHands, "Imposición de manos");
        if (amount > pool.Remaining)
        {
            throw DomainException.RuleViolation($"Solo quedan {pool.Remaining} puntos de Imposición de manos.");
        }

        pool.SetUsed(pool.Used + amount);
        var healed = 0;
        if (targetSelf)
        {
            var max = Math.Max(0, maxHp);
            healed = Math.Clamp(max - HitPointsCurrent, 0, amount);
            HitPointsCurrent += healed;
        }

        Touch(now);
        return healed;
    }

    /// <summary>
    /// Paladin Divine Smite: spends one regular slot of <paramref name="slotLevel"/> (1-9) and returns
    /// the extra radiant damage dice (<see cref="CombatCalculator.DivineSmiteDice"/>).
    /// <paramref name="maxSlots"/> is the sheet's maximum for that level.
    /// </summary>
    public string DivineSmite(int slotLevel, int maxSlots, DateTimeOffset now)
    {
        RequireClass(PaladinClass, DivineSmiteMinLevel, $"Castigo divino requiere paladín de nivel {DivineSmiteMinLevel}.");
        if (slotLevel is < 1 or > 9)
        {
            throw DomainException.RuleViolation("El nivel del espacio de conjuro debe estar entre 1 y 9.");
        }

        SpendSpellSlot(slotLevel, 1, maxSlots, now);
        return CombatCalculator.DivineSmiteDice(slotLevel);
    }

    /// <summary>
    /// Wizard Arcane Recovery (once per long rest): recovers spent slots of the given levels (one entry
    /// per slot). Their sum cannot exceed half the wizard level rounded up and no slot can be above
    /// 5th level. Marks the <see cref="ClassResourceRules.ArcaneRecovery"/> resource as used.
    /// </summary>
    public void ArcaneRecovery(IReadOnlyList<int> slotLevels, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(slotLevels);
        var wizardLevel = RequireClass(WizardClass, 1, "Solo un mago puede usar Recuperación arcana.");
        var resource = FindAutoResource(ClassResourceRules.ArcaneRecovery, "Recuperación arcana");
        RecoverSlots(resource, "Recuperación arcana", slotLevels, CombatCalculator.ArcaneRecoveryLevels(wizardLevel), CombatCalculator.ArcaneRecoveryMaxSlotLevel, now);
    }

    /// <summary>
    /// Druid Natural Recovery (Circle of the Land, druid level 2; once per long rest, after a short rest): recovers
    /// spent slots of the given levels (one entry per slot). Their sum cannot exceed half the druid level rounded up
    /// and no slot can be of 6th level or higher. Marks the <see cref="ClassResourceRules.NaturalRecovery"/> resource as used.
    /// </summary>
    public void NaturalRecovery(IReadOnlyList<int> slotLevels, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(slotLevels);
        var druidLevel = RequireClass(DruidClass, ClassResourceRules.NaturalRecoveryMinLevel, "Solo un druida del Círculo de la Tierra de nivel 2 puede usar Recuperación natural.");
        if (!ClassResourceRules.IsCircleOfTheLand(_classes.First(c => c.ClassIndex == DruidClass).SubclassIndex))
        {
            throw DomainException.RuleViolation("Recuperación natural es un rasgo del Círculo de la Tierra.");
        }

        var resource = FindAutoResource(ClassResourceRules.NaturalRecovery, "Recuperación natural");
        RecoverSlots(resource, "Recuperación natural", slotLevels, CombatCalculator.ArcaneRecoveryLevels(druidLevel), CombatCalculator.NaturalRecoveryMaxSlotLevel, now);
    }

    /// <summary>Shared rules of Arcane and Natural Recovery: one use, slot levels summing at most <paramref name="allowed"/>, none above <paramref name="maxSlotLevel"/>.</summary>
    private void RecoverSlots(CharacterResource resource, string label, IReadOnlyList<int> slotLevels, int allowed, int maxSlotLevel, DateTimeOffset now)
    {
        if (resource.Remaining == 0)
        {
            throw DomainException.RuleViolation($"Ya has usado {label} desde el último descanso largo.");
        }

        if (slotLevels.Count == 0)
        {
            throw DomainException.RuleViolation("Indica al menos un espacio de conjuro que recuperar.");
        }

        if (slotLevels.Any(l => l < 1))
        {
            throw DomainException.RuleViolation("El nivel del espacio de conjuro debe ser al menos 1.");
        }

        if (slotLevels.Any(l => l > maxSlotLevel))
        {
            throw DomainException.RuleViolation($"{label} no recupera espacios de nivel superior a {maxSlotLevel}.");
        }

        if (slotLevels.Sum() > allowed)
        {
            throw DomainException.RuleViolation($"{label} recupera como máximo {allowed} niveles de espacios de conjuro.");
        }

        var byLevel = slotLevels.GroupBy(l => l).ToDictionary(g => g.Key, g => g.Count());
        foreach (var (level, count) in byLevel)
        {
            if (count > SpellSlotsUsed(level))
            {
                throw DomainException.RuleViolation($"No hay suficientes espacios de conjuro de nivel {level} gastados que recuperar.");
            }
        }

        foreach (var (level, count) in byLevel)
        {
            GetOrCreateSlot(level).SetUsed(SpellSlotsUsed(level) - count);
        }

        resource.SetUsed(resource.Used + 1);
        Touch(now);
    }

    /// <summary>Level in the class; throws a rule violation when it is below <paramref name="minLevel"/>.</summary>
    private int RequireClass(string classIndex, int minLevel, string message)
    {
        var level = _classes.FirstOrDefault(c => c.ClassIndex == classIndex)?.Level ?? 0;
        if (level < minLevel)
        {
            throw DomainException.RuleViolation(message);
        }

        return level;
    }

    private CharacterResource FindAutoResource(string key, string label) =>
        _resources.FirstOrDefault(r => r.IsAuto && r.Key == key)
            ?? throw DomainException.RuleViolation($"El personaje no tiene el recurso {label}.");
}
