using OpenTrpg.Core.Domain.Common;

namespace OpenTrpg.Core.Domain.Items;

/// <summary>
/// An entry of the party stash of a campaign: loot that belongs to nobody yet (template and/or
/// overrides, quantity, optional charges and notes). Every member sees the stash; the DM manages it
/// and characters take items from it or give them back. <see cref="Version"/> is bumped on every
/// change and used as an optimistic concurrency token, so two concurrent takes of the last units
/// cannot both succeed.
/// </summary>
public sealed class PartyStashItem : EntityBase
{
    private PartyStashItem()
    {
    }

    public Guid CampaignId { get; private set; }

    /// <summary>Null for items created entirely by hand.</summary>
    public Guid? TemplateId { get; private set; }

    public ItemOverrides Overrides { get; private set; } = ItemOverrides.None();

    /// <summary>At least 1 (entries reaching 0 are removed by the caller).</summary>
    public int Quantity { get; private set; }

    /// <summary>Remaining charges of an item given back with charges; null otherwise.</summary>
    public int? Charges { get; private set; }

    public int? ChargesMax { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public Guid AddedByUserId { get; private set; }

    /// <summary>Incremented on every change; optimistic concurrency token.</summary>
    public int Version { get; private set; }

    public bool HasCharges => ChargesMax is not null;

    /// <param name="overrides">Normalized instance owned by nobody else (use <see cref="ItemOverrides.Copy"/>).</param>
    public static PartyStashItem Create(
        Guid campaignId,
        Guid? templateId,
        ItemOverrides overrides,
        int quantity,
        string? notes,
        Guid addedByUserId,
        DateTimeOffset now,
        int? charges = null,
        int? chargesMax = null)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        ValidateQuantity(quantity);
        var normalized = overrides.Normalize();
        if (templateId is null && normalized.Name is null)
        {
            throw DomainException.RuleViolation("Un objeto sin plantilla necesita un nombre.");
        }

        if (chargesMax is not null && (chargesMax is < 0 or > ItemLimits.MaxCharges || charges is null || charges < 0 || charges > chargesMax))
        {
            throw DomainException.RuleViolation($"Las cargas deben estar entre 0 y {ItemLimits.MaxCharges}.");
        }

        return new PartyStashItem
        {
            CampaignId = campaignId,
            TemplateId = templateId,
            Overrides = normalized,
            Quantity = quantity,
            Charges = chargesMax is null ? null : charges,
            ChargesMax = chargesMax,
            Notes = NormalizeNotes(notes),
            AddedAt = now,
            AddedByUserId = addedByUserId,
            CreatedAt = now,
        };
    }

    /// <summary>Same template, no overrides and no charges on either side: units can be added to this entry.</summary>
    public bool CanStackWith(Guid? templateId, ItemOverrides overrides, bool hasCharges) =>
        templateId is not null && TemplateId == templateId && Overrides.IsEmpty && overrides.IsEmpty && !HasCharges && !hasCharges;

    public void AddQuantity(int amount)
    {
        ValidateQuantity(amount);
        if ((long)Quantity + amount > ItemLimits.MaxQuantity)
        {
            throw DomainException.RuleViolation($"Un objeto no puede acumular más de {ItemLimits.MaxQuantity} unidades.");
        }

        Quantity += amount;
        Version++;
    }

    /// <summary>Removes units; the caller deletes the entry when it reaches 0.</summary>
    public void RemoveQuantity(int amount)
    {
        ValidateQuantity(amount);
        if (amount > Quantity)
        {
            throw DomainException.RuleViolation($"Solo hay {Quantity} unidades de este objeto en el alijo.");
        }

        Quantity -= amount;
        Version++;
    }

    /// <summary>Null <paramref name="quantity"/> keeps it; <paramref name="setNotes"/> applies <paramref name="notes"/> (null clears them).</summary>
    public void Update(int? quantity, bool setNotes, string? notes)
    {
        if (quantity is { } q)
        {
            ValidateQuantity(q);
        }

        var normalizedNotes = setNotes ? NormalizeNotes(notes) : Notes;
        Quantity = quantity ?? Quantity;
        Notes = normalizedNotes;
        Version++;
    }

    private static void ValidateQuantity(int quantity)
    {
        if (quantity is < 1 or > ItemLimits.MaxQuantity)
        {
            throw DomainException.RuleViolation($"La cantidad debe estar entre 1 y {ItemLimits.MaxQuantity}.");
        }
    }

    private static string? NormalizeNotes(string? notes)
    {
        var trimmed = notes?.Trim();
        if (trimmed is { Length: > ItemLimits.NotesMaxLength })
        {
            throw DomainException.RuleViolation($"Las notas no pueden superar los {ItemLimits.NotesMaxLength} caracteres.");
        }

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
