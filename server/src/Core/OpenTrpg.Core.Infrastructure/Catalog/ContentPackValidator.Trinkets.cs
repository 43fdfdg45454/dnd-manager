using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Infrastructure.Catalog;

// Trinket table of a pack (phase 21): d100 results pointing to items of the SRD or of the pack itself. The item
// references are checked with those of the starting equipment, once every item of the pack is known.
internal sealed partial class ContentPackValidator
{
    private void Trinkets(List<PackTrinketJson?>? trinkets, ContentPackRows rows)
    {
        var rolls = new HashSet<int>();
        ForEach("trinkets", trinkets, (path, trinket) =>
        {
            var roll = RequiredInt($"{path}.roll", trinket.Roll, TrinketEntry.MinRoll, TrinketEntry.MaxRoll);
            var item = RequiredText($"{path}.item", trinket.Item, IndexMaxLength);
            if (roll is { } r && !rolls.Add(r))
            {
                AddError($"{path}.roll", $"La tirada {r} está repetida en la tabla de baratijas.");
                return;
            }

            if (roll is null || item.Length == 0)
            {
                return;
            }

            _itemReferences.Add(($"{path}.item", item));
            rows.Trinkets.Add(new TrinketEntry { Source = _id, Roll = roll.Value, ItemIndex = item });
        });
    }
}
