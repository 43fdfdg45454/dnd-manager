using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Common;
using OpenTrpg.Core.Domain.Items;

namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// Character aggregate of the core: identity, campaign, owner, lifecycle, portrait, free texts, height and
/// weight, money and inventory. What the game system stores (the sheet, combat state…) lives in the
/// system's own entity, stored in its own table keyed by the character and loaded by the system.
/// Campaign roles are resolved outside the aggregate: permission helpers take <c>actorIsDm</c>
/// (true when the actor is at least DM in the campaign).
/// Every change goes through <see cref="Touch"/>, which bumps <see cref="Version"/> (optimistic
/// concurrency token: two concurrent purchases cannot both spend the same money); the system entity
/// calls it too.
/// </summary>
public sealed class Character : EntityBase
{
    public const int NameMaxLength = 100;
    public const int TextMaxLength = 20000;

    /// <summary>Personality traits, ideals, bonds and flaws (phase 22).</summary>
    public const int PersonalityMaxLength = 1000;

    /// <summary>Largest amount of money, in the minor unit of the system's currency.</summary>
    public const int MaxMoney = 1_000_000_000;

    /// <summary>Height and weight: free data without mechanical effect (the system presents the units).</summary>
    public const int MinHeightInches = 1;
    public const int MaxHeightInches = 200;
    public const int MinWeightPounds = 1;
    public const int MaxWeightPounds = 2000;

    private readonly List<CharacterItem> _items = [];

    private Character()
    {
    }

    public Guid CampaignId { get; private set; }

    /// <summary>Null for non-player characters created by a DM.</summary>
    public Guid? OwnerUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public CharacterStatus Status { get; private set; }

    /// <summary>Money in the minor unit of the system's currency (copper pieces in D&amp;D 5e).</summary>
    public int Money { get; private set; }

    public string Notes { get; private set; } = string.Empty;

    public string Backstory { get; private set; } = string.Empty;

    /// <summary>Personality (phase 22): free texts the owner edits like the notes.</summary>
    public string PersonalityTraits { get; private set; } = string.Empty;

    public string Ideals { get; private set; } = string.Empty;

    public string Bonds { get; private set; } = string.Empty;

    public string Flaws { get; private set; } = string.Empty;

    /// <summary>Height in inches; null when not given.</summary>
    public int? HeightInches { get; private set; }

    /// <summary>Weight in pounds; null when not given.</summary>
    public int? WeightPounds { get; private set; }

    public Guid? PortraitFileId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token, bumped on every change.</summary>
    public int Version { get; private set; }

    public IReadOnlyCollection<CharacterItem> Items => _items;

    public int AttunedCount => _items.Count(i => i.Attuned);

    /// <summary>Creates a draft. The game system initializes its own part of the character.</summary>
    public static Character Create(Guid campaignId, Guid? ownerUserId, string name, DateTimeOffset now) => new()
    {
        CampaignId = campaignId,
        OwnerUserId = ownerUserId,
        Name = NormalizeName(name),
        Status = CharacterStatus.Draft,
        CreatedAt = now,
        UpdatedAt = now,
    };

    // ---- Permissions ---------------------------------------------------------------------------

    public bool IsOwnedBy(Guid userId) => OwnerUserId == userId;

    /// <summary>Full sheet: owner and DMs. Every member sees the summary.</summary>
    public bool CanViewSheet(Guid actorUserId, bool actorIsDm) => actorIsDm || IsOwnedBy(actorUserId);

    /// <summary>
    /// Decides how a sheet edit by the actor is applied: DMs and the owner of a draft edit directly; the
    /// owner of an active character needs approval. Anyone else is forbidden.
    /// </summary>
    public SheetEditMode ResolveSheetEdit(Guid actorUserId, bool actorIsDm)
    {
        if (actorIsDm)
        {
            return SheetEditMode.Direct;
        }

        if (!IsOwnedBy(actorUserId))
        {
            throw DomainException.Forbidden("Solo el dueño del personaje o un DM pueden editar la hoja.");
        }

        return Status == CharacterStatus.Draft ? SheetEditMode.Direct : SheetEditMode.RequiresApproval;
    }

    /// <summary>Combat tracking (HP, slots, resources, rests...): owner and DMs, without approval.</summary>
    public void EnsureCanTrack(Guid actorUserId, bool actorIsDm)
    {
        if (!CanViewSheet(actorUserId, actorIsDm))
        {
            throw DomainException.Forbidden("Solo el dueño del personaje o un DM pueden modificar su estado.");
        }
    }

    /// <summary>DMs always; the owner only while the character is a draft.</summary>
    public void EnsureCanDelete(Guid actorUserId, bool actorIsDm)
    {
        if (actorIsDm)
        {
            return;
        }

        if (!IsOwnedBy(actorUserId))
        {
            throw DomainException.Forbidden("Solo el dueño del personaje o un DM pueden borrarlo.");
        }

        if (Status != CharacterStatus.Draft)
        {
            throw DomainException.Forbidden("Un personaje activo solo lo puede borrar un DM.");
        }
    }

    /// <summary>Only the owner submits a draft for activation (the caller then creates the <see cref="ChangeRequest"/>).</summary>
    public void EnsureCanSubmit(Guid actorUserId)
    {
        if (!IsOwnedBy(actorUserId))
        {
            throw DomainException.Forbidden("Solo el dueño del personaje puede enviarlo para activar.");
        }

        if (Status != CharacterStatus.Draft)
        {
            throw DomainException.Conflict("El personaje ya está activo.");
        }
    }

    // ---- Lifecycle -----------------------------------------------------------------------------

    /// <summary>Draft → Active. The game system prepares its part (e.g. full hit points) in the same operation.</summary>
    public void Activate(DateTimeOffset now)
    {
        if (Status != CharacterStatus.Draft)
        {
            throw DomainException.Conflict("El personaje ya está activo.");
        }

        Status = CharacterStatus.Active;
        Touch(now);
    }

    public void SetPortrait(Guid? fileId, DateTimeOffset now)
    {
        PortraitFileId = fileId;
        Touch(now);
    }

    // ---- Profile -------------------------------------------------------------------------------

    /// <summary>
    /// Checks a profile edit (name, texts, money, height and weight) without changing anything and
    /// returns the normalized values to give to <see cref="ApplyProfile"/>. Throws a <see cref="DomainException"/>
    /// when a value is invalid.
    /// </summary>
    public CharacterProfileEdit PrepareProfile(CharacterProfileEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);

        var name = edit.Name is null ? null : NormalizeName(edit.Name);
        var notes = edit.Notes is null ? null : NormalizeText(edit.Notes, "Las notas");
        var backstory = edit.Backstory is null ? null : NormalizeText(edit.Backstory, "La historia");
        var traits = edit.PersonalityTraits is null ? null : NormalizePersonality(edit.PersonalityTraits, "Los rasgos de personalidad", PersonalityMaxLength);
        var ideals = edit.Ideals is null ? null : NormalizePersonality(edit.Ideals, "Los ideales", PersonalityMaxLength);
        var bonds = edit.Bonds is null ? null : NormalizePersonality(edit.Bonds, "Los vínculos", PersonalityMaxLength);
        var flaws = edit.Flaws is null ? null : NormalizePersonality(edit.Flaws, "Los defectos", PersonalityMaxLength);
        if (edit.Money is { } money)
        {
            ValidateMoney(money);
        }

        if (edit.HeightInches is { } height)
        {
            NormalizeHeight(height);
        }

        if (edit.WeightPounds is { } weight)
        {
            NormalizeWeight(weight);
        }

        return edit with
        {
            Name = name,
            Notes = notes,
            Backstory = backstory,
            PersonalityTraits = traits,
            Ideals = ideals,
            Bonds = bonds,
            Flaws = flaws,
        };
    }

    /// <summary>
    /// Applies a profile edit checked by <see cref="PrepareProfile"/> (null fields keep their value; a height or
    /// weight of 0 clears it). Does not call <see cref="Touch"/>: the caller does, once for the whole edit.
    /// </summary>
    public void ApplyProfile(CharacterProfileEdit prepared)
    {
        var edit = PrepareProfile(prepared);
        Name = edit.Name ?? Name;
        Notes = edit.Notes ?? Notes;
        Backstory = edit.Backstory ?? Backstory;
        PersonalityTraits = edit.PersonalityTraits ?? PersonalityTraits;
        Ideals = edit.Ideals ?? Ideals;
        Bonds = edit.Bonds ?? Bonds;
        Flaws = edit.Flaws ?? Flaws;
        Money = edit.Money ?? Money;
        HeightInches = edit.HeightInches is null ? HeightInches : NormalizeHeight(edit.HeightInches.Value);
        WeightPounds = edit.WeightPounds is null ? WeightPounds : NormalizeWeight(edit.WeightPounds.Value);
    }

    /// <summary>
    /// Sets height and weight (null keeps the value, 0 clears it). They have no mechanical effect, so the
    /// owner changes them without approval even on an active character.
    /// </summary>
    public void SetHeightAndWeight(int? heightInches, int? weightPounds, DateTimeOffset now)
    {
        var height = heightInches is null ? HeightInches : NormalizeHeight(heightInches.Value);
        var weight = weightPounds is null ? WeightPounds : NormalizeWeight(weightPounds.Value);
        HeightInches = height;
        WeightPounds = weight;
        Touch(now);
    }

    /// <summary>0 clears; otherwise between <see cref="MinHeightInches"/> and <see cref="MaxHeightInches"/>.</summary>
    private static int? NormalizeHeight(int value) => value == 0
        ? null
        : value is >= MinHeightInches and <= MaxHeightInches
            ? value
            : throw DomainException.RuleViolation($"La altura debe estar entre {MinHeightInches} y {MaxHeightInches} pulgadas.");

    /// <summary>0 clears; otherwise between <see cref="MinWeightPounds"/> and <see cref="MaxWeightPounds"/>.</summary>
    private static int? NormalizeWeight(int value) => value == 0
        ? null
        : value is >= MinWeightPounds and <= MaxWeightPounds
            ? value
            : throw DomainException.RuleViolation($"El peso debe estar entre {MinWeightPounds} y {MaxWeightPounds} libras.");

    public void Rename(string name, DateTimeOffset now)
    {
        Name = NormalizeName(name);
        Touch(now);
    }

    /// <summary>
    /// Hands the character to another owner, or makes it a non-player character with <c>null</c>.
    /// The caller checks that the new owner is a player of the campaign.
    /// </summary>
    public void ChangeOwner(Guid? ownerUserId, DateTimeOffset now)
    {
        OwnerUserId = ownerUserId;
        Touch(now);
    }

    // ---- Inventory and money -------------------------------------------------------------------

    public CharacterItem FindItem(Guid itemId) =>
        _items.FirstOrDefault(i => i.Id == itemId) ?? throw DomainException.NotFound("El objeto no está en el inventario.");

    /// <summary>
    /// Adds an item. A stackable item (<see cref="EffectiveItem.IsStackable"/>) with a template and no
    /// overrides is added to an existing entry of the same template; otherwise a new entry is created at
    /// the end of the list. <paramref name="overrides"/> must be an instance owned by nobody else.
    /// </summary>
    public CharacterItem AddItem(Guid? templateId, ItemOverrides overrides, int quantity, EffectiveItem effective, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(effective);
        ValidateQuantity(quantity);
        var normalized = overrides.Normalize();
        if (templateId is null && normalized.Name is null)
        {
            throw DomainException.RuleViolation("Un objeto sin plantilla necesita un nombre.");
        }

        var stack = effective.IsStackable ? _items.FirstOrDefault(i => i.CanStackWith(templateId, normalized)) : null;
        if (stack is not null)
        {
            stack.AddQuantity(quantity, now);
            Touch(now);
            return stack;
        }

        var sortOrder = _items.Count == 0 ? 0 : _items.Max(i => i.SortOrder) + 1;
        var item = CharacterItem.Create(Id, CampaignId, templateId, normalized, quantity, sortOrder, now);
        _items.Add(item);
        Touch(now);
        return item;
    }

    /// <summary>
    /// Adds an item that comes with its own charges (e.g. taken from the party stash). Without charges it
    /// behaves like <see cref="AddItem"/>; with charges a new entry is always created (charges belong to
    /// one entry and never stack).
    /// </summary>
    public CharacterItem ReceiveItem(
        Guid? templateId,
        ItemOverrides overrides,
        int quantity,
        EffectiveItem effective,
        int? charges,
        int? chargesMax,
        DateTimeOffset now)
    {
        if (charges is null || chargesMax is null)
        {
            return AddItem(templateId, overrides, quantity, effective, now);
        }

        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(effective);
        ValidateQuantity(quantity);
        var normalized = overrides.Normalize();
        if (templateId is null && normalized.Name is null)
        {
            throw DomainException.RuleViolation("Un objeto sin plantilla necesita un nombre.");
        }

        var sortOrder = _items.Count == 0 ? 0 : _items.Max(i => i.SortOrder) + 1;
        var item = CharacterItem.Create(Id, CampaignId, templateId, normalized, quantity, sortOrder, now);
        item.RestoreCharges(charges.Value, chargesMax.Value, now);
        _items.Add(item);
        Touch(now);
        return item;
    }

    /// <summary>Removes <paramref name="quantity"/> units (all of them when null); the entry goes away at 0.</summary>
    public void RemoveItem(Guid itemId, int? quantity, DateTimeOffset now)
    {
        var item = FindItem(itemId);
        var amount = quantity ?? item.Quantity;
        ValidateQuantity(amount);
        item.RemoveQuantity(amount, now);
        if (item.Quantity == 0)
        {
            _items.Remove(item);
        }

        Touch(now);
    }

    /// <summary>
    /// Changes the play state of an entry. Equipping requires an equippable item (<see cref="EffectiveItem.IsEquippable"/>); equipping an
    /// armor (or a shield) unequips the one worn before. Attuning requires an item that needs it and at
    /// most <see cref="ItemLimits.MaxAttunedItems"/> attuned items (409 with code <see cref="ItemLimits.AttunementLimitCode"/>;
    /// <see cref="ItemUpdate.ReplaceAttunedItemId"/> drops another attuned item in the same operation). Everything is checked
    /// before anything changes. <paramref name="resolve"/> gives the effective item of any entry.
    /// </summary>
    public CharacterItem UpdateItem(Guid itemId, ItemUpdate update, Func<CharacterItem, EffectiveItem> resolve, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(resolve);

        var item = FindItem(itemId);
        var effective = resolve(item);
        if (update.Equipped == true && !item.Equipped && !effective.IsEquippable)
        {
            throw DomainException.RuleViolation("Solo se pueden equipar armas, armaduras, escudos, objetos mágicos y objetos con efectos o modificadores.");
        }

        CharacterItem? released = null;
        if (update.ReplaceAttunedItemId is { } replaceId)
        {
            if (update.Attuned != true)
            {
                throw DomainException.RuleViolation("Solo se puede dejar otro objeto sintonizado al sintonizar este.");
            }

            released = FindItem(replaceId);
            if (!released.Attuned || released.Id == item.Id)
            {
                throw DomainException.RuleViolation("El objeto que quieres dejar no está sintonizado.");
            }
        }

        if (update.Attuned == true && !item.Attuned)
        {
            if (!effective.RequiresAttunement)
            {
                throw DomainException.RuleViolation("Este objeto no requiere sintonización.");
            }

            if (AttunedCount - (released is null ? 0 : 1) >= ItemLimits.MaxAttunedItems)
            {
                throw DomainException.Conflict(
                    $"Ya tienes {ItemLimits.MaxAttunedItems} objetos sintonizados: elige cuál dejar para sintonizar este.",
                    ItemLimits.AttunementLimitCode);
            }
        }

        if (update.SetNotes && update.Notes?.Trim() is { Length: > ItemLimits.NotesMaxLength })
        {
            throw DomainException.RuleViolation($"Las notas no pueden superar los {ItemLimits.NotesMaxLength} caracteres.");
        }

        if (update.SetCharges && update.Charges is { } charges && (charges < 0 || charges > (item.ChargesMax ?? ItemLimits.MaxCharges)))
        {
            throw DomainException.RuleViolation($"Las cargas deben estar entre 0 y {item.ChargesMax ?? ItemLimits.MaxCharges}.");
        }

        if (update.Equipped is { } equipped && equipped != item.Equipped)
        {
            if (equipped && effective.Category is ItemCategory.Armor or ItemCategory.Shield)
            {
                foreach (var other in _items.Where(i => i.Id != item.Id && i.Equipped && resolve(i).Category == effective.Category))
                {
                    other.SetEquipped(false, now);
                }
            }

            item.SetEquipped(equipped, now);
        }

        if (released is not null && !item.Attuned)
        {
            released.SetAttuned(false, now);
        }

        if (update.Attuned is { } attuned && attuned != item.Attuned)
        {
            item.SetAttuned(attuned, now);
        }

        if (update.SetNotes)
        {
            item.SetNotes(update.Notes, now);
        }

        if (update.SortOrder is { } sortOrder)
        {
            item.SetSortOrder(sortOrder, now);
        }

        if (update.SetCharges)
        {
            item.SetCharges(update.Charges, now);
        }

        Touch(now);
        return item;
    }

    /// <summary>
    /// Uses an item: an item with charges spends <paramref name="amount"/> charges; a consumable
    /// without charges loses <paramref name="amount"/> units and is removed at 0. Returns the entry, or
    /// null when it was used up and removed.
    /// </summary>
    public CharacterItem? UseItem(Guid itemId, int amount, EffectiveItem effective, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(effective);
        if (amount < 1)
        {
            throw DomainException.RuleViolation("La cantidad debe ser al menos 1.");
        }

        var item = FindItem(itemId);
        if (item.HasCharges)
        {
            item.SpendCharges(amount, now);
            Touch(now);
            return item;
        }

        if (!effective.IsConsumable)
        {
            throw DomainException.RuleViolation("Este objeto no es consumible ni tiene cargas.");
        }

        item.RemoveQuantity(amount, now);
        Touch(now);
        if (item.Quantity > 0)
        {
            return item;
        }

        _items.Remove(item);
        return null;
    }

    /// <summary>Adds (or, when negative, subtracts) money. The result must stay between 0 and <see cref="MaxMoney"/>.</summary>
    public void AdjustMoney(long delta, DateTimeOffset now)
    {
        var result = Money + delta;
        if (result < 0)
        {
            throw DomainException.RuleViolation("No hay dinero suficiente.");
        }

        if (result > MaxMoney)
        {
            throw DomainException.RuleViolation($"El dinero no puede superar {MaxMoney} pc.");
        }

        Money = (int)result;
        Touch(now);
    }

    /// <summary>Records a change: updates <see cref="UpdatedAt"/> and bumps <see cref="Version"/>.</summary>
    public void Touch(DateTimeOffset now)
    {
        UpdatedAt = now;
        Version++;
    }

    // ---- Helpers -------------------------------------------------------------------------------

    private static void ValidateQuantity(int quantity)
    {
        if (quantity is < 1 or > ItemLimits.MaxQuantity)
        {
            throw DomainException.RuleViolation($"La cantidad debe estar entre 1 y {ItemLimits.MaxQuantity}.");
        }
    }

    private static string NormalizeName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > NameMaxLength)
        {
            throw DomainException.RuleViolation($"El nombre debe tener entre 1 y {NameMaxLength} caracteres.");
        }

        return trimmed;
    }

    private static string NormalizePersonality(string value, string label, int maxLength)
    {
        var text = value.Trim();
        if (text.Length > maxLength)
        {
            throw DomainException.RuleViolation($"{label} no pueden superar los {maxLength} caracteres.");
        }

        return text;
    }

    private static string NormalizeText(string value, string label)
    {
        if (value.Length > TextMaxLength)
        {
            throw DomainException.RuleViolation($"{label} no pueden superar los {TextMaxLength} caracteres.");
        }

        return value;
    }

    private static void ValidateMoney(int money)
    {
        if (money is < 0 or > MaxMoney)
        {
            throw DomainException.RuleViolation($"El dinero debe estar entre 0 y {MaxMoney} pc.");
        }
    }
}

/// <summary>
/// Changes to the profile of a character (core fields of a sheet edit). Null keeps the current value; a
/// height or weight of 0 clears it.
/// </summary>
public sealed record CharacterProfileEdit
{
    public string? Name { get; init; }

    public string? Notes { get; init; }

    public string? Backstory { get; init; }

    public string? PersonalityTraits { get; init; }

    public string? Ideals { get; init; }

    public string? Bonds { get; init; }

    public string? Flaws { get; init; }

    /// <summary>Money in the minor unit of the system's currency.</summary>
    public int? Money { get; init; }

    public int? HeightInches { get; init; }

    public int? WeightPounds { get; init; }
}
