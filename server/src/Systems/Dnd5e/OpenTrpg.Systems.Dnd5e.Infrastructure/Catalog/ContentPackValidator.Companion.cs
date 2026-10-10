using System.Globalization;
using System.Text.Json;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// Phase 25, block 6: the animal companion of a subclass feature (levels[].features[].companion).
internal sealed partial class ContentPackValidator
{
    /// <summary>Creatures of the closed lists of companions, checked once every creature of the pack is known.</summary>
    private readonly List<(string Path, string Index)> _companionCreatureReferences = [];

    /// <summary>Checks that the creatures of the companions exist in the SRD, a required pack or the pack.</summary>
    private void CheckCompanionCreatureReferences(ContentPackRows rows)
    {
        foreach (var (path, index) in _companionCreatureReferences)
        {
            if (!rows.Creatures.Any(c => c.Index == index) && _context.Creatures?.Contains(index) != true)
            {
                AddError(path, $"La criatura '{index}' no existe en el SRD, en los paquetes requeridos ni en el paquete.");
            }
        }
    }

    /// <summary>
    /// <c>{ "beastFilter": { "maxChallengeRating": 0.25, "sizes": ["Medium"] }, "hitPoints": "max(beast, 4*classLevel)",
    /// "proficiencyBonusFromCharacter": true, "attackBonusFromCharacter": true }</c>, normalized
    /// (<see cref="CompanionRule.ToJson"/>); null (with errors) when invalid.
    /// </summary>
    private string? Companion(string path, PackCompanionJson? companion)
    {
        if (companion is null)
        {
            return null;
        }

        var valid = true;
        double maxCr = CompanionRule.MaxChallengeRatingLimit;
        var sizes = new List<string>();
        var creatures = new List<string>();
        ForEachText($"{path}.creatureIndexes", companion.CreatureIndexes, (creaturePath, value) =>
        {
            if (Reference(creaturePath, value) is { } index && !creatures.Contains(index))
            {
                creatures.Add(index);
                _companionCreatureReferences.Add((creaturePath, index));
            }
        });
        if (creatures.Count > CompanionRule.MaxCreatureIndexes)
        {
            AddError($"{path}.creatureIndexes", $"Como máximo {CompanionRule.MaxCreatureIndexes} criaturas.");
            valid = false;
        }

        if (companion.BeastFilter is not { } filter)
        {
            if (creatures.Count == 0)
            {
                AddError($"{path}.beastFilter", "Campo obligatorio (o indica creatureIndexes).");
                valid = false;
            }
        }
        else
        {
            if (filter.MaxChallengeRating is not { } cr)
            {
                AddError($"{path}.beastFilter.maxChallengeRating", "Campo obligatorio.");
                valid = false;
            }
            else if (cr is < 0 or > CompanionRule.MaxChallengeRatingLimit || double.IsNaN(cr))
            {
                AddError($"{path}.beastFilter.maxChallengeRating", $"Debe ser un número entre 0 y {CompanionRule.MaxChallengeRatingLimit.ToString(CultureInfo.InvariantCulture)} (0.25 para 1/4).");
                valid = false;
            }
            else
            {
                maxCr = cr;
            }

            for (var i = 0; i < (filter.Sizes?.Count ?? 0); i++)
            {
                var size = filter.Sizes![i]?.Trim();
                var known = CompanionRule.AllSizes.FirstOrDefault(s => string.Equals(s, size, StringComparison.OrdinalIgnoreCase));
                if (known is null)
                {
                    AddError($"{path}.beastFilter.sizes[{i}]", $"Tamaño desconocido. Valores admitidos: {string.Join(", ", CompanionRule.AllSizes)}.");
                    valid = false;
                }
                else if (!sizes.Contains(known))
                {
                    sizes.Add(known);
                }
            }
        }

        int? perLevel = null;
        IReadOnlyList<CompanionHitPointsTerm>? terms = null;
        if (companion.HitPoints is not null && !CompanionRule.TryParseHitPoints(companion.HitPoints, out perLevel))
        {
            terms = CompanionRule.ParseHitPointsFormula(companion.HitPoints);
            if (terms is null)
            {
                AddError(
                    $"{path}.hitPoints",
                    $"Debe ser \"{CompanionRule.BeastHitPoints}\", \"max(beast, N*classLevel)\" con N entre {CompanionRule.MinHitPointsMultiplier} y {CompanionRule.MaxHitPointsMultiplier}, " +
                    "o una suma de términos (\"beast\", \"characterLevel\", \"classLevel\", \"halfClassLevel\", \"mod:con\" con factor opcional \"2*\", y enteros): \"beast+2*classLevel\".");
                valid = false;
            }
        }

        var maxCount = 1;
        var byProficiency = false;
        if (companion.MaxCount is { } count && count.ValueKind != JsonValueKind.Null)
        {
            if (count.ValueKind == JsonValueKind.String && count.GetString() == CompanionRule.ProficiencyBonusCount)
            {
                byProficiency = true;
            }
            else if (count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var number) && number is >= 1 and <= CompanionRule.MaxCountLimit)
            {
                maxCount = number;
            }
            else
            {
                AddError($"{path}.maxCount", $"Debe ser un entero entre 1 y {CompanionRule.MaxCountLimit} o \"{CompanionRule.ProficiencyBonusCount}\".");
                valid = false;
            }
        }

        var budget = new Dictionary<int, double>();
        foreach (var (levelText, value) in companion.CrBudgetByLevel ?? [])
        {
            var entryPath = $"{path}.crBudgetByLevel.{levelText}";
            if (!int.TryParse(levelText, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || level is < 1 or > 20)
            {
                AddError(entryPath, "La clave debe ser un nivel de 1 a 20.");
                valid = false;
            }
            else if (value is not { } total || total is < 0 or > 100 || double.IsNaN(total))
            {
                AddError(entryPath, "Debe ser un VD total entre 0 y 100.");
                valid = false;
            }
            else
            {
                budget[level] = total;
            }
        }

        var bonuses = CompanionBonuses.None;
        if (companion.Bonuses is { } bonusJson)
        {
            string? Bonus(string name, JsonElement? element)
            {
                if (element is not { } value || value.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                var text = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.GetRawText(),
                    _ => null,
                };
                if (text is null || !CompanionBonuses.IsValid(text))
                {
                    AddError($"{path}.bonuses.{name}", $"Debe ser un entero entre -10 y 30 o \"{CompanionBonuses.ProficiencyBonus}\".");
                    valid = false;
                    return null;
                }

                return text;
            }

            bonuses = new CompanionBonuses(
                Bonus("armorClass", bonusJson.ArmorClass),
                Bonus("savingThrows", bonusJson.SavingThrows),
                Bonus("attack", bonusJson.Attack),
                Bonus("damage", bonusJson.Damage));
        }

        var features = new Dictionary<int, string>();
        foreach (var (levelText, text) in companion.FeaturesByLevel ?? [])
        {
            var entryPath = $"{path}.featuresByLevel.{levelText}";
            if (!int.TryParse(levelText, NumberStyles.None, CultureInfo.InvariantCulture, out var level) || level is < 1 or > 20)
            {
                AddError(entryPath, "La clave debe ser un nivel de 1 a 20.");
                valid = false;
            }
            else if (NullableText(entryPath, text, CompanionRule.FeatureTextMaxLength) is { } feature)
            {
                features[level] = feature;
            }
        }

        if (!valid)
        {
            return null;
        }

        // Sizes in catalog order, so that equal filters store the same JSON.
        sizes = [.. CompanionRule.AllSizes.Where(sizes.Contains)];
        return new CompanionRule(
            maxCr,
            sizes,
            perLevel,
            companion.ProficiencyBonusFromCharacter ?? false,
            companion.AttackBonusFromCharacter ?? false)
        {
            CreatureIndexes = creatures,
            MaxCount = maxCount,
            MaxCountIsProficiencyBonus = byProficiency,
            ChallengeBudgetByLevel = budget,
            HitPointsTerms = terms,
            Bonuses = bonuses,
            FeaturesByLevel = features,
        }.ToJson();
    }
}
