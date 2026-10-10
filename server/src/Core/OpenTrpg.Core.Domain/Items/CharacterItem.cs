using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Items;

/// <summary>
/// An entry of a character's inventory: a template (SRD or homebrew) and/or overrides, with its
/// quantity and play state. It belongs to the campaign of the character (<see cref="CampaignId"/>
/// always equals the character's). Modified only through <see cref="Character"/>, which enforces the
/// rules that involve several entries (one armor, one shield, at most three attuned items).
/// </summary>
public sealed class CharacterItem
{
    private CharacterItem()
    {
    }

    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid CharacterId { get; private set; }

    public Guid CampaignId { get; private set; }

    /// <summary>Null for items created entirely by hand.</summary>
    public Guid? TemplateId { get; private set; }

    /// <summary>At least 1 (entries reaching 0 are removed).</summary>
    public int Quantity { get; private set; }

    public bool Equipped { get; private set; }

    public bool Attuned { get; private set; }

    /// <summary>Remaining charges; null when the item has no charges.</summary>
    public int? Charges { get; private set; }

    public int? ChargesMax { get; private set; }

    public int SortOrder { get; private set; }

    public string? Notes { get; private set; }

    public ItemOverrides Overrides { get; private set; } = ItemOverrides.None();

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool HasCharges => ChargesMax is not null;

    /// <summary>Same template, no overrides and no charges: a purchase of the same item can be added to this entry.</summary>
    public bool CanStackWith(Guid? templateId, ItemOverrides overrides) =>
        templateId is not null && TemplateId == templateId && Overrides.IsEmpty && overrides.IsEmpty && !HasCharges;

    internal static CharacterItem Create(Guid characterId, Guid campaignId, Guid? templateId, ItemOverrides overrides, int quantity, int sortOrder, DateTimeOffset now) => new()
    {
        CharacterId = characterId,
        CampaignId = campaignId,
        TemplateId = templateId,
        Overrides = overrides,
        Quantity = quantity,
        SortOrder = sortOrder,
        CreatedAt = now,
        UpdatedAt = now,
    };

    internal void AddQuantity(int amount, DateTimeOffset now)
    {
        if ((long)Quantity + amount > ItemLimits.MaxQuantity)
        {
            throw DomainException.RuleViolation($"Un objeto no puede acumular más de {ItemLimits.MaxQuantity} unidades.");
        }

        Quantity += amount;
        UpdatedAt = now;
    }

    /// <summary>Removes units; the caller deletes the entry when it reaches 0.</summary>
    internal void RemoveQuantity(int amount, DateTimeOffset now)
    {
        if (amount > Quantity)
        {
            throw DomainException.RuleViolation($"Solo hay {Quantity} unidades de este objeto.");
        }

        Quantity -= amount;
        UpdatedAt = now;
    }

    internal void SpendCharges(int amount, DateTimeOffset now)
    {
        if (Charges is not { } charges || charges < amount)
        {
            throw DomainException.RuleViolation("No quedan cargas suficientes.");
        }

        Charges = charges - amount;
        UpdatedAt = now;
    }

    /// <summary>
    /// Sets the remaining charges. The first time it also defines the maximum; afterwards the value
    /// must not exceed it. Null removes the charges.
    /// </summary>
    internal void SetCharges(int? charges, DateTimeOffset now)
    {
        if (charges is null)
        {
            Charges = null;
            ChargesMax = null;
        }
        else
        {
            if (charges < 0 || charges > (ChargesMax ?? ItemLimits.MaxCharges))
            {
                throw DomainException.RuleViolation($"Las cargas deben estar entre 0 y {ChargesMax ?? ItemLimits.MaxCharges}.");
            }

            ChargesMax ??= charges;
            Charges = charges;
        }

        UpdatedAt = now;
    }

    /// <summary>Restores charges and maximum kept elsewhere (the party stash), both within range.</summary>
    internal void RestoreCharges(int charges, int chargesMax, DateTimeOffset now)
    {
        if (chargesMax is < 0 or > ItemLimits.MaxCharges || charges < 0 || charges > chargesMax)
        {
            throw DomainException.RuleViolation($"Las cargas deben estar entre 0 y {ItemLimits.MaxCharges}.");
        }

        ChargesMax = chargesMax;
        Charges = charges;
        UpdatedAt = now;
    }

    internal void SetEquipped(bool equipped, DateTimeOffset now)
    {
        Equipped = equipped;
        UpdatedAt = now;
    }

    internal void SetAttuned(bool attuned, DateTimeOffset now)
    {
        Attuned = attuned;
        UpdatedAt = now;
    }

    internal void SetNotes(string? notes, DateTimeOffset now)
    {
        var trimmed = notes?.Trim();
        if (trimmed is { Length: > ItemLimits.NotesMaxLength })
        {
            throw DomainException.RuleViolation($"Las notas no pueden superar los {ItemLimits.NotesMaxLength} caracteres.");
        }

        Notes = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        UpdatedAt = now;
    }

    internal void SetSortOrder(int sortOrder, DateTimeOffset now)
    {
        SortOrder = sortOrder;
        UpdatedAt = now;
    }
}
