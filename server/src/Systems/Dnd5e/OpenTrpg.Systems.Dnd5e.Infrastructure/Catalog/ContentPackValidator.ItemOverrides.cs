using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure;
using OpenTrpg.Core.Infrastructure.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// itemOverrides[] of a pack: new descriptions for items of the SRD or of the packs it requires (the same reach as the
// item references of the starting equipment). The pack's own items carry their description in items[], so overriding
// one of them is an error, and the base pack has nothing to override.
internal sealed partial class ContentPackValidator
{
    public const int MaxItemOverrides = 500;

    private void ItemOverrides(List<PackItemOverrideJson?>? overrides, ContentPackRows rows)
    {
        if (overrides is null)
        {
            return;
        }

        if (IsBasePack)
        {
            AddError("itemOverrides", "El paquete base del sistema no sobreescribe objetos.");
            return;
        }

        if (overrides.Count > MaxItemOverrides)
        {
            AddError("itemOverrides", $"No puede tener más de {MaxItemOverrides} entradas.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        ForEach("itemOverrides", overrides, (path, entry) =>
        {
            var item = RequiredText($"{path}.item", entry.Item, IndexMaxLength);
            var description = OverrideDescription($"{path}.description", entry.Description);
            if (item.Length == 0)
            {
                return;
            }

            if (!seen.Add(item))
            {
                AddError($"{path}.item", $"El objeto '{item}' está repetido en itemOverrides.");
                return;
            }

            if (item.StartsWith($"{_id}-", StringComparison.Ordinal))
            {
                AddError($"{path}.item", $"El objeto '{item}' es del propio paquete: dale su descripción en items[].");
                return;
            }

            if (_context.SrdItems?.Contains(item) != true)
            {
                AddError($"{path}.item", $"El objeto '{item}' no existe en el SRD ni en los paquetes alcanzables.");
                return;
            }

            if (description is null)
            {
                return;
            }

            rows.ItemOverrides.Add(new ItemDescriptionOverride { Source = _id, ItemIndex = item, Description = description });
        });
    }

    /// <summary>The paragraphs of an override: at least one, none of them blank, with the limits of the item descriptions.</summary>
    private List<string>? OverrideDescription(string path, List<string?>? value)
    {
        if (value is null || value.Count == 0)
        {
            AddError(path, "Campo obligatorio: al menos un párrafo.");
            return null;
        }

        var blank = false;
        for (var i = 0; i < value.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(value[i]))
            {
                AddError($"{path}[{i}]", "El párrafo no puede estar vacío.");
                blank = true;
            }
        }

        var paragraphs = Paragraphs(path, value);
        return blank || paragraphs.Count == 0 ? null : paragraphs;
    }
}
