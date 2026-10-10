using OpenTrpg.Core.Domain.Rules;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Rules;

namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>An automatic class resource as derived from class and level (see <see cref="ClassResourceRules"/>).</summary>
public sealed record ResourceTemplate(string Key, string Name, int Max, ResourceRecharge Recharge)
{
    /// <summary>Dice rolled after a rest whose results are kept in the resource (null for most resources).</summary>
    public RollOnRest? RollOnRest { get; init; }

    /// <summary>Die spent with each use ("d8": Bardic Inspiration), or null.</summary>
    public string? Dice { get; init; }

    /// <summary>Feature or option that grants the resource, with its level ("Ventaja táctica (nivel 3)"); null for SRD class resources.</summary>
    public string? Source { get; init; }

    /// <summary>How <see cref="Max"/> was obtained (pack resources); null for SRD class resources.</summary>
    public ValueBreakdown? Breakdown { get; init; }
}

/// <summary>
/// Automatic limited-use resources by class and level (SRD 5.1). Each resource appears from the level
/// at which the class gains the feature. Warlocks have none (pact slots cover them); rogues and rangers neither.
/// </summary>
public static class ClassResourceRules
{
    /// <summary>Maximum used for "unlimited" (rage at barbarian level 20).</summary>
    public const int Unlimited = 999;

    public const string Rage = "rage";
    public const string BardicInspiration = "bardic-inspiration";
    public const string ChannelDivinity = "channel-divinity";
    public const string WildShape = "wild-shape";
    public const string SecondWind = "second-wind";
    public const string ActionSurge = "action-surge";
    public const string Indomitable = "indomitable";
    public const string Ki = "ki";
    public const string LayOnHands = "lay-on-hands";
    public const string SorceryPoints = "sorcery-points";
    public const string ArcaneRecovery = "arcane-recovery";
    public const string NaturalRecovery = "natural-recovery";

    /// <summary>Druid level at which the Circle of the Land grants Natural Recovery.</summary>
    public const int NaturalRecoveryMinLevel = 2;

    /// <summary>Whether a druid subclass is the Circle of the Land ("land" in the SRD; "circle-of-the-land…" in packs).</summary>
    public static bool IsCircleOfTheLand(string? subclassIndex) =>
        subclassIndex is not null
        && (subclassIndex == "land" || subclassIndex.StartsWith("circle-of-the-land", StringComparison.Ordinal));

    /// <summary>Bardic Inspiration die by bard level (SRD): d6, d8 at 5, d10 at 10, d12 at 15.</summary>
    public static string BardicInspirationDie(int level) => level >= 15 ? "d12" : level >= 10 ? "d10" : level >= 5 ? "d8" : "d6";

    /// <summary>Rages per long rest by barbarian level 1-20 (SRD <c>class_specific.rage_count</c>).</summary>
    private static readonly int[] RageCount = [2, 2, 3, 3, 3, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, Unlimited];

    /// <summary>Resources granted by one class at a class level.</summary>
    /// <param name="abilityModifiers">Final ability modifiers by index (<see cref="CharacterSheet.AbilityModifiers"/>); missing ones count as 0.</param>
    public static IReadOnlyList<ResourceTemplate> For(string classIndex, int level, IReadOnlyDictionary<string, int> abilityModifiers)
    {
        ArgumentNullException.ThrowIfNull(abilityModifiers);
        if (level is < AbilityRules.MinLevel or > AbilityRules.MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"Class level must be between {AbilityRules.MinLevel} and {AbilityRules.MaxLevel}.");
        }

        var result = new List<ResourceTemplate>();
        void Add(string key, string name, int max, ResourceRecharge recharge) => result.Add(new ResourceTemplate(key, name, max, recharge));

        switch (classIndex)
        {
            case "barbarian":
                Add(Rage, "Rage", RageCount[level - 1], ResourceRecharge.LongRest);
                break;
            case "bard":
                result.Add(new ResourceTemplate(
                    BardicInspiration,
                    "Bardic Inspiration",
                    Math.Max(1, abilityModifiers.GetValueOrDefault(Abilities.Cha)),
                    level >= 5 ? ResourceRecharge.ShortRest : ResourceRecharge.LongRest)
                {
                    Dice = BardicInspirationDie(level),
                });
                break;
            case "cleric" when level >= 2:
                Add(ChannelDivinity, "Channel Divinity", level >= 18 ? 3 : level >= 6 ? 2 : 1, ResourceRecharge.ShortRest);
                break;
            case "druid" when level >= 2:
                // Archdruid (level 20): unlimited uses.
                Add(WildShape, "Wild Shape", level >= 20 ? Unlimited : 2, ResourceRecharge.ShortRest);
                break;
            case "fighter":
                Add(SecondWind, "Second Wind", 1, ResourceRecharge.ShortRest);
                if (level >= 2)
                {
                    Add(ActionSurge, "Action Surge", level >= 17 ? 2 : 1, ResourceRecharge.ShortRest);
                }

                if (level >= 9)
                {
                    Add(Indomitable, "Indomitable", level >= 17 ? 3 : level >= 13 ? 2 : 1, ResourceRecharge.LongRest);
                }

                break;
            case "monk" when level >= 2:
                Add(Ki, "Ki", level, ResourceRecharge.ShortRest);
                break;
            case "paladin":
                Add(LayOnHands, "Lay on Hands", 5 * level, ResourceRecharge.LongRest);
                if (level >= 3)
                {
                    Add(ChannelDivinity, "Channel Divinity", 1, ResourceRecharge.ShortRest);
                }

                break;
            case "sorcerer" when level >= 2:
                Add(SorceryPoints, "Sorcery Points", level, ResourceRecharge.LongRest);
                break;
            case "wizard":
                Add(ArcaneRecovery, "Arcane Recovery", 1, ResourceRecharge.LongRest);
                break;
        }

        return result;
    }

    /// <summary>
    /// Resources of every class of a character, ready for <see cref="Character.SyncAutoResources"/>.
    /// A key granted by several classes (cleric and paladin Channel Divinity) does not stack: the highest maximum wins.
    /// </summary>
    public static IReadOnlyList<ResourceTemplate> ForClasses(IEnumerable<CharacterClassLevel> classes, IReadOnlyDictionary<string, int> abilityModifiers)
    {
        ArgumentNullException.ThrowIfNull(classes);

        return classes
            .OrderBy(c => c.Order)
            .SelectMany(c => For(c.ClassIndex, c.Level, abilityModifiers).Concat(ForSubclass(c)))
            .GroupBy(t => t.Key, StringComparer.Ordinal)
            .Select(g => g.MaxBy(t => t.Max)!)
            .ToList();
    }

    /// <summary>Classes of the SRD with automatic resources.</summary>
    public static IReadOnlyList<string> ClassesWithResources { get; } =
        ["barbarian", "bard", "cleric", "druid", "fighter", "monk", "paladin", "sorcerer", "wizard"];

    /// <summary>
    /// Every resource key a class can give at any level (its subclass ones included), with its name: what an option
    /// cost may spend (<c>"ki"</c> → "Ki"). Empty for classes without resources.
    /// </summary>
    public static IReadOnlyDictionary<string, string> KeysFor(string classIndex)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var template in For(classIndex, AbilityRules.MaxLevel, new Dictionary<string, int>()))
        {
            result[template.Key] = template.Name;
        }

        if (classIndex == "druid")
        {
            result[NaturalRecovery] = "Natural Recovery";
        }

        return result;
    }

    /// <summary>Name of a class resource key of any class ("sorcery-points" → "Sorcery Points"), or null.</summary>
    public static string? NameOf(string key) =>
        ClassesWithResources.Select(c => KeysFor(c).GetValueOrDefault(key)).FirstOrDefault(n => n is not null);

    /// <summary>Resources granted by a subclass (Circle of the Land: Natural Recovery from druid level 2).</summary>
    public static IReadOnlyList<ResourceTemplate> ForSubclass(CharacterClassLevel entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.ClassIndex == "druid" && entry.Level >= NaturalRecoveryMinLevel && IsCircleOfTheLand(entry.SubclassIndex)
            ? [new ResourceTemplate(NaturalRecovery, "Natural Recovery", 1, ResourceRecharge.LongRest)]
            : [];
    }
}
