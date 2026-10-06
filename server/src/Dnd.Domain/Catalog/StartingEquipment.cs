using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dnd.Domain.Catalog;

/// <summary>
/// Structured starting equipment of a class or a background, stored as JSON in
/// <see cref="ClassDefinition.StartingEquipmentJson"/> and <see cref="BackgroundDefinition.StartingEquipmentJson"/>:
/// the items every character gets, the (a)/(b) choices, the alternative starting wealth of the class and the
/// fixed gold included (backgrounds). Item indexes are catalog <see cref="ItemTemplate.Index"/> values and
/// categories are <see cref="EquipmentCategory.Index"/> values.
/// </summary>
/// <param name="Fixed">Items given without choosing.</param>
/// <param name="Choices">Choices of the class or background, in the order of the book.</param>
/// <param name="Gold">Alternative starting wealth of the class (rolled physically); null for backgrounds.</param>
/// <param name="FixedGoldCp">Money included with the equipment, in copper pieces (background <c>starting_gold</c>).</param>
public sealed record StartingEquipment(
    IReadOnlyList<StartingItem> Fixed,
    IReadOnlyList<StartingEquipmentChoice> Choices,
    StartingGold? Gold,
    int? FixedGoldCp)
{
    public const int MaxQuantity = 1000;
    public const int MaxChoose = 20;

    public static StartingEquipment Empty { get; } = new([], [], null, null);

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Every item index referenced (fixed, options and pack contents), without duplicates.</summary>
    public IReadOnlyList<string> ItemIndexes() =>
        Fixed.Concat(Choices.SelectMany(c => c.Options).SelectMany(o => o.Items))
            .SelectMany(i => (i.Contents ?? []).Prepend(i))
            .Select(i => i.Item)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Every category index referenced, without duplicates.</summary>
    public IReadOnlyList<string> CategoryIndexes() =>
        Choices.SelectMany(c => c.Options).SelectMany(o => o.Categories).Select(c => c.Category).Distinct(StringComparer.Ordinal).ToList();

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>
    /// Tolerant parse of the stored JSON: null when the column is empty or malformed (the definition has no
    /// structured starting equipment), and entries without an item or category index are dropped.
    /// </summary>
    public static StartingEquipment? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StartingEquipment>(json, Options);
            return stored is null ? null : Normalize(stored);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static StartingEquipment Normalize(StartingEquipment stored) => new(
        Items(stored.Fixed),
        (stored.Choices ?? [])
            .Where(c => c is not null)
            .Select(c => new StartingEquipmentChoice(
                c.Description ?? string.Empty,
                Math.Max(1, c.Choose),
                (c.Options ?? [])
                    .Where(o => o is not null)
                    .Select(o => new StartingEquipmentOption(
                        o.Label ?? string.Empty,
                        Items(o.Items),
                        (o.Categories ?? []).Where(k => !string.IsNullOrWhiteSpace(k?.Category)).Select(k => k with { Choose = Math.Max(1, k.Choose) }).ToList()))
                    .ToList()))
            .ToList(),
        stored.Gold is { Dice.Length: > 0, Multiplier: > 0 } gold ? gold : null,
        stored.FixedGoldCp is > 0 ? stored.FixedGoldCp : null);

    private static List<StartingItem> Items(IReadOnlyList<StartingItem>? items) =>
        (items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i?.Item))
            .Select(i => i with
            {
                Quantity = Math.Max(1, i.Quantity),
                Contents = i.Contents is { Count: > 0 } contents ? Items(contents) : null,
            })
            .ToList();
}

/// <summary>An item and its quantity.</summary>
/// <param name="Item">Catalog item index ("chain-mail").</param>
/// <param name="Name">Name from the source dataset, shown when the index does not resolve to a catalog item.</param>
/// <param name="Contents">What an equipment pack contains ("explorers-pack"), when known.</param>
public sealed record StartingItem(string Item, int Quantity, string? Name = null, IReadOnlyList<StartingItem>? Contents = null);

/// <summary>Pick <see cref="Choose"/> of <see cref="Options"/> ("(a) chain mail or (b) leather armor, longbow, and 20 arrows").</summary>
public sealed record StartingEquipmentChoice(string Description, int Choose, IReadOnlyList<StartingEquipmentOption> Options);

/// <summary>One option of a choice: fixed items plus picks from equipment categories ("a martial weapon and a shield").</summary>
public sealed record StartingEquipmentOption(string Label, IReadOnlyList<StartingItem> Items, IReadOnlyList<StartingCategoryPick> Categories);

/// <summary>Pick <see cref="Choose"/> items of the equipment category <see cref="Category"/> ("martial-weapons").</summary>
public sealed record StartingCategoryPick(string Category, int Choose);

/// <summary>Alternative starting wealth: roll <see cref="Dice"/> and multiply by <see cref="Multiplier"/> gold pieces.</summary>
public sealed record StartingGold(string Dice, int Multiplier);

/// <summary>
/// Group of equipment of the SRD ("martial-weapons", "holy-symbols", "musical-instruments") used by the choices
/// "any martial weapon". <see cref="ItemIndexes"/> are catalog <see cref="ItemTemplate.Index"/> values.
/// </summary>
public sealed class EquipmentCategory
{
    public required string Index { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<string> ItemIndexes { get; init; } = [];

    /// <summary>"srd" or the id of the content pack that added it (see <see cref="CatalogSources"/>).</summary>
    public string Source { get; init; } = CatalogSources.Srd;
}
