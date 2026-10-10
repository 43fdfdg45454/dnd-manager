using System.Globalization;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// Phase 25, block 6: the animal companion of a subclass feature (levels[].features[].companion).
internal sealed partial class ContentPackValidator
{
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
        double maxCr = 0;
        var sizes = new List<string>();
        if (companion.BeastFilter is not { } filter)
        {
            AddError($"{path}.beastFilter", "Campo obligatorio.");
            valid = false;
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
        if (companion.HitPoints is not null && !CompanionRule.TryParseHitPoints(companion.HitPoints, out perLevel))
        {
            AddError(
                $"{path}.hitPoints",
                $"Debe ser \"{CompanionRule.BeastHitPoints}\" o \"max(beast, N*classLevel)\" con N entre {CompanionRule.MinHitPointsMultiplier} y {CompanionRule.MaxHitPointsMultiplier}.");
            valid = false;
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
            companion.AttackBonusFromCharacter ?? false).ToJson();
    }
}
