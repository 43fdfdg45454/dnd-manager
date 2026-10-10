using OpenTrpg.Core.Application.Systems;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Application.Items;

/// <summary>Tells the game system of a character that its inventory changed (<see cref="IItemSystem.OnInventoryChangedAsync"/>).</summary>
public sealed class InventoryHooks(CampaignSystems systems)
{
    public async Task ChangedAsync(Character character, string kind, Guid? itemId, CancellationToken cancellationToken)
    {
        var system = await systems.ForCharacterAsync(character, cancellationToken);
        await system.Items.OnInventoryChangedAsync(new CharacterRef(character), new InventoryChange(kind, itemId), cancellationToken);
    }

    /// <summary>Throws a rule violation when the game system does not let the character attune to an item of <paramref name="template"/>.</summary>
    public async Task EnsureCanAttuneAsync(Character character, ItemTemplate template, CancellationToken cancellationToken)
    {
        var system = await systems.ForCharacterAsync(character, cancellationToken);
        if (await system.Items.AttunementProblemAsync(new CharacterRef(character), template, cancellationToken) is { } problem)
        {
            throw DomainException.RuleViolation(problem);
        }
    }

    /// <summary>Throws a rule violation when the game system does not let the character equip <paramref name="item"/>.</summary>
    public async Task EnsureCanEquipAsync(Character character, CharacterItem item, IReadOnlyDictionary<Guid, ItemTemplate> templates, CancellationToken cancellationToken)
    {
        var system = await systems.ForCharacterAsync(character, cancellationToken);
        if (await system.Items.EquipProblemAsync(new CharacterRef(character), item, templates, cancellationToken) is { } problem)
        {
            throw DomainException.RuleViolation(problem);
        }
    }
}
