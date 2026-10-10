using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Infrastructure.Catalog;

// Phase 25, block 5: option costs (options[].cost) and the explicit order of the choices of a level
// (levelChoices[].after). Both are checked at the end, against the pack and the catalog.
internal sealed partial class ContentPackValidator
{
    private readonly List<(string Path, string SetId, string Resource)> _costReferences = [];
    private readonly List<(string Path, string ClassIndex, string? SubclassIndex, int Level, string Key, string After)> _afterReferences = [];

    /// <summary><c>{ "resource": "ki", "amount": 2 }</c>; null when absent or invalid (the resource is checked at the end).</summary>
    private string? Cost(string path, PackOptionCostJson? cost, string setId)
    {
        if (cost is null)
        {
            return null;
        }

        var resource = Reference($"{path}.resource", cost.Resource);
        if (resource is null && string.IsNullOrWhiteSpace(cost.Resource))
        {
            AddError($"{path}.resource", "Campo obligatorio.");
        }

        var amount = RequiredInt($"{path}.amount", cost.Amount, OptionCost.MinAmount, OptionCost.MaxAmount);
        if (resource is null || amount is null)
        {
            return null;
        }

        _costReferences.Add(($"{path}.resource", setId, resource));
        return LevelChoiceJson.Serialize(new { resource, amount });
    }

    /// <summary>
    /// The resource of each cost must exist for the classes whose choices draw from the option's set: a class resource of
    /// the SRD of one of those classes, a resource of an option of the pack, or of a feature of a subclass of those
    /// classes. When the set is not tied to a class (feats, a set no choice uses yet), the key must be known: a class
    /// resource of the SRD or a resource of the pack or the catalog.
    /// </summary>
    private void CheckCostReferences(ContentPackRows rows)
    {
        if (_costReferences.Count == 0)
        {
            return;
        }

        var setClasses = rows.LevelChoiceRules
            .Where(r => r.SetId is not null)
            .Select(r => (SetId: r.SetId!, r.ClassIndex))
            .Concat((_context.SetClasses ?? []).Where(x => IsOtherSource(x.Source)).Select(x => (x.SetId, x.ClassIndex)))
            .ToLookup(x => x.SetId, x => x.ClassIndex, StringComparer.Ordinal);
        var subclassClasses = rows.Subclasses.ToDictionary(s => s.Index, s => s.ClassIndex, StringComparer.Ordinal);
        var optionResources = rows.Options
            .Where(o => o.Resource is not null)
            .Select(o => (Key: o.Resource!.Key, o.SetId))
            .ToList();
        var featureResources = rows.Features
            .Where(f => f.Resource is not null)
            .Select(f => (Key: f.Resource!.Key, ClassIndex: f.SubclassIndex is { } subclass ? subclassClasses.GetValueOrDefault(subclass, f.ClassIndex) : f.ClassIndex))
            .ToList();
        var catalogResources = (_context.ResourceKeys ?? []).Where(r => IsOtherSource(r.Source)).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var (path, setId, key) in _costReferences)
        {
            var classes = setId == OptionSets.Feats ? [] : setClasses[setId].Distinct(StringComparer.Ordinal).ToList();
            var srdOwners = ClassResourceRules.ClassesWithResources.Where(c => ClassResourceRules.KeysFor(c).ContainsKey(key)).ToList();
            var forClass = classes.Count == 0 ? "" : $" (las elecciones del conjunto '{setId}' son de {string.Join(", ", classes)})";
            if (srdOwners.Count > 0)
            {
                if (classes.Count > 0 && !classes.Intersect(srdOwners, StringComparer.Ordinal).Any())
                {
                    AddError(path, $"El recurso '{key}' es de {string.Join(", ", srdOwners)}, no de la clase de la elección{forClass}.");
                }

                continue;
            }

            if (optionResources.Any(r => r.Key == key))
            {
                continue;
            }

            var owners = featureResources.Where(r => r.Key == key).Select(r => r.ClassIndex).Distinct(StringComparer.Ordinal).ToList();
            if (owners.Count > 0)
            {
                if (classes.Count > 0 && !classes.Intersect(owners, StringComparer.Ordinal).Any())
                {
                    AddError(path, $"El recurso '{key}' es de un rasgo de {string.Join(", ", owners)}, no de la clase de la elección{forClass}.");
                }

                continue;
            }

            if (!catalogResources.Contains(key))
            {
                AddError(path, $"El recurso '{key}' no existe: usa un recurso de clase ({string.Join(", ", ClassResourceRules.ClassesWithResources.SelectMany(c => ClassResourceRules.KeysFor(c).Keys).Distinct(StringComparer.Ordinal))}) o el key de un resource del paquete o del catálogo.");
            }
        }
    }

    /// <summary>
    /// <c>after</c> names another choice of the same class and level (of the pack, of the base class or the same subclass,
    /// or of the catalog), and the choices of the pack must not form a cycle.
    /// </summary>
    private void CheckAfterReferences(ContentPackRows rows)
    {
        if (_afterReferences.Count == 0)
        {
            return;
        }

        var catalogKeys = (_context.LevelChoiceKeys ?? [])
            .Where(k => IsOtherSource(k.Source))
            .Select(k => (k.ClassIndex, k.Level, k.Key))
            .ToHashSet();
        foreach (var (path, classIndex, subclassIndex, level, key, after) in _afterReferences)
        {
            if (after == key)
            {
                AddError(path, "Una elección no puede ir después de sí misma.");
                continue;
            }

            var inPack = rows.LevelChoiceRules.Any(r =>
                r.ClassIndex == classIndex && r.Level == level && r.Key == after && (r.SubclassIndex is null || r.SubclassIndex == subclassIndex));
            if (!inPack && !catalogKeys.Contains((classIndex, level, after)))
            {
                AddError(path, $"No hay ninguna elección '{after}' de {classIndex} en el nivel {level}.");
                continue;
            }

            // Follow the chain inside the pack: coming back to the key is a cycle.
            var seen = new HashSet<string>(StringComparer.Ordinal) { key };
            var current = after;
            var currentSubclass = subclassIndex;
            while (current is not null)
            {
                if (!seen.Add(current))
                {
                    if (current == key)
                    {
                        AddError(path, $"El orden de las elecciones del nivel {level} forma un ciclo ('{key}' → '{after}' → … → '{key}').");
                    }

                    break;
                }

                var next = rows.LevelChoiceRules.FirstOrDefault(r =>
                    r.ClassIndex == classIndex && r.Level == level && r.Key == current && (r.SubclassIndex == currentSubclass || r.SubclassIndex is null));
                current = next?.After;
                currentSubclass = next?.SubclassIndex ?? currentSubclass;
            }
        }
    }
}
