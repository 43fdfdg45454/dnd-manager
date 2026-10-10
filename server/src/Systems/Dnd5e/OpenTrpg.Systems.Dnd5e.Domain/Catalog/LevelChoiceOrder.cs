using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
namespace OpenTrpg.Systems.Dnd5e.Domain.Catalog;

/// <summary>
/// Order in which the choices of one level are resolved (and shown): what gives skills before the expertise that
/// doubles them. A choice goes after the one its <see cref="LevelChoiceRule.After"/> names, and every
/// <see cref="LevelChoiceKind.Expertise"/> choice goes after the choices that can give skill proficiencies (skills, the
/// subclass, options and feats, which may grant skills), unless one of those is explicitly after it. Otherwise the
/// original order is kept.
/// </summary>
public static class LevelChoiceOrder
{
    /// <summary>Kinds whose picks may give skill proficiencies.</summary>
    public static bool GivesSkills(LevelChoiceKind kind) =>
        kind is LevelChoiceKind.Skill or LevelChoiceKind.Subclass or LevelChoiceKind.OptionSet or LevelChoiceKind.Custom or LevelChoiceKind.AsiOrFeat;

    /// <summary>
    /// The rules in dependency order (stable). <see cref="LevelChoiceRule.After"/> refers to a rule of the list with that
    /// key (of the same subclass when there are several); unknown keys and cycles are ignored.
    /// </summary>
    public static List<LevelChoiceRule> Sort(IReadOnlyList<LevelChoiceRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var count = rules.Count;
        var explicitBefore = new List<int>[count];
        for (var i = 0; i < count; i++)
        {
            explicitBefore[i] = [];
            if (rules[i].After is { Length: > 0 } after && Find(rules, after, rules[i].SubclassIndex, i) is { } j)
            {
                explicitBefore[i].Add(j);
            }
        }

        // Whether rule a goes explicitly (maybe through others) after rule b.
        bool ExplicitlyAfter(int a, int b)
        {
            var seen = new HashSet<int>();
            var pending = new Stack<int>(explicitBefore[a]);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (current == b)
                {
                    return true;
                }

                if (seen.Add(current))
                {
                    foreach (var next in explicitBefore[current])
                    {
                        pending.Push(next);
                    }
                }
            }

            return false;
        }

        var before = new HashSet<int>[count];
        for (var i = 0; i < count; i++)
        {
            before[i] = [.. explicitBefore[i]];
            if (rules[i].Kind != LevelChoiceKind.Expertise)
            {
                continue;
            }

            for (var j = 0; j < count; j++)
            {
                if (j != i && GivesSkills(rules[j].Kind) && !ExplicitlyAfter(j, i))
                {
                    before[i].Add(j);
                }
            }
        }

        var placed = new bool[count];
        var result = new List<LevelChoiceRule>(count);
        while (result.Count < count)
        {
            var next = -1;
            for (var i = 0; i < count && next < 0; i++)
            {
                if (!placed[i] && before[i].All(j => placed[j]))
                {
                    next = i;
                }
            }

            // A cycle (only possible with explicit "after"): keep the original order for the rest.
            if (next < 0)
            {
                next = Array.IndexOf(placed, false);
            }

            placed[next] = true;
            result.Add(rules[next]);
        }

        return result;
    }

    private static int? Find(IReadOnlyList<LevelChoiceRule> rules, string key, string? subclassIndex, int self)
    {
        int? any = null;
        for (var j = 0; j < rules.Count; j++)
        {
            if (j == self || rules[j].Key != key)
            {
                continue;
            }

            if (rules[j].SubclassIndex == subclassIndex)
            {
                return j;
            }

            any ??= j;
        }

        return any;
    }
}
