using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;

namespace Dnd.Application.Characters;

/// <summary>
/// What using an option costs: <see cref="Amount"/> uses of the resource <see cref="Resource"/> (its key), named
/// <see cref="ResourceName"/> as the character's resource is named ("Ki"). <see cref="Label"/> is "2 Ki".
/// </summary>
public sealed record OptionCostDto(string Resource, string ResourceName, int Amount)
{
    public string Label => $"{Amount} {ResourceName}";
}

/// <summary>An option of a choice that is only available when <see cref="Index"/> is picked in the choice <see cref="ChoiceKey"/> of the same level-up.</summary>
public sealed record OptionRequirementDto(string ChoiceKey, string Index);

/// <summary>
/// A chosen option with a cost: what the Combat tab lists next to its resource (with "Usar") and the sheet next to the
/// pick ("2 Ki").
/// </summary>
public sealed record CharacterOptionCostDto(string Index, string Name, string Resource, string ResourceName, int Amount)
{
    public string Label => $"{Amount} {ResourceName}";
}

/// <summary>Option costs with the name of their resource.</summary>
public static class OptionCosts
{
    /// <summary>
    /// The name of a resource key: the character's resource with that key, a class resource of the SRD, a resource the
    /// given options declare, or the key itself.
    /// </summary>
    public static string ResourceName(string key, Character? character, IEnumerable<OptionDefinition>? options = null) =>
        character?.Resources.FirstOrDefault(r => r.Key == key)?.Name
        ?? ClassResourceRules.NameOf(key)
        ?? options?.Select(o => o.Resource).FirstOrDefault(r => r?.Key == key)?.Name
        ?? key;

    public static OptionCostDto? Of(OptionDefinition? option, Character? character, IEnumerable<OptionDefinition>? options = null) =>
        option?.Cost is { } cost ? new OptionCostDto(cost.Resource, ResourceName(cost.Resource, character, options), cost.Amount) : null;

    /// <summary>The options the character has chosen (active picks and feats) that have a cost, in the order they were chosen.</summary>
    public static List<CharacterOptionCostDto> ForCharacter(Character character, Func<string, OptionDefinition?> option)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(option);
        var result = new List<CharacterOptionCostDto>();
        foreach (var pick in character.ActivePicks())
        {
            if (result.Any(r => r.Index == pick.Item.Index) || option(pick.Item.Index) is not { Cost: { } cost } definition)
            {
                continue;
            }

            result.Add(new CharacterOptionCostDto(definition.Index, definition.Name, cost.Resource, ResourceName(cost.Resource, character), cost.Amount));
        }

        return result;
    }
}
