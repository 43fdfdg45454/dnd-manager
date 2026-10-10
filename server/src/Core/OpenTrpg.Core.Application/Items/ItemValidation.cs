using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Items;
using FluentValidation;

namespace OpenTrpg.Core.Application.Items;

/// <summary>Rules shared by the item validators (same ranges the domain enforces, see <see cref="ItemLimits"/>).</summary>
internal static class ItemRules
{
    public static readonly string CategoryMessage = $"La categoría debe ser {EnumNames.Describe<ItemCategory>()}.";
    public static readonly string RarityMessage = $"La rareza debe ser {EnumNames.Describe<ItemRarity>()}.";

    public static bool IsValidList(IReadOnlyList<string>? values, int entryMaxLength) =>
        values is null || (values.Count <= ItemLimits.MaxListEntries && values.All(v => v is null || v.Trim().Length <= entryMaxLength));

    public static string ListMessage(string label, int entryMaxLength) =>
        $"{label} admiten como máximo {ItemLimits.MaxListEntries} entradas de {entryMaxLength} caracteres.";

    public const string NullModifierMessage = "El modificador no puede estar vacío.";

    public static bool IsValidModifierCount(IReadOnlyList<ItemModifierDto>? modifiers) =>
        modifiers is null || modifiers.Count <= ItemLimits.MaxModifiers;
}

/// <summary>A modifier: a known kind, then the domain rules (<see cref="ItemModifier.Validate"/>).</summary>
public sealed class ItemModifierDtoValidator : AbstractValidator<ItemModifierDto>
{
    public static readonly string KindMessage = $"El tipo de modificador debe ser {EnumNames.Describe<ItemModifierKind>()}.";

    public ItemModifierDtoValidator()
    {
        RuleFor(x => x.Kind).Must(EnumNames.IsValid<ItemModifierKind>).WithMessage(KindMessage);
        RuleFor(x => x)
            .Custom((modifier, context) =>
            {
                if (modifier.ToDomain().Validate() is { } error)
                {
                    context.AddFailure(nameof(ItemModifierDto.Value), error);
                }
            })
            .When(x => EnumNames.IsValid<ItemModifierKind>(x.Kind));
    }
}

public sealed class ItemOverridesDtoValidator : AbstractValidator<ItemOverridesDto>
{
    public ItemOverridesDtoValidator()
    {
        RuleFor(x => x.Name).Must(n => n is null || n.Trim().Length <= ItemLimits.NameMaxLength)
            .WithMessage($"El nombre no puede superar los {ItemLimits.NameMaxLength} caracteres.");
        RuleFor(x => x.Category).Must(EnumNames.IsValid<ItemCategory>).When(x => x.Category is not null).WithMessage(ItemRules.CategoryMessage);
        RuleFor(x => x.Rarity).Must(EnumNames.IsValid<ItemRarity>).When(x => x.Rarity is not null).WithMessage(ItemRules.RarityMessage);
        RuleFor(x => x.DamageDice).MaximumLength(ItemLimits.DiceMaxLength)
            .WithMessage($"El dado de daño no puede superar los {ItemLimits.DiceMaxLength} caracteres.");
        RuleFor(x => x.VersatileDice).MaximumLength(ItemLimits.DiceMaxLength)
            .WithMessage($"El dado de daño a dos manos no puede superar los {ItemLimits.DiceMaxLength} caracteres.");
        RuleFor(x => x.DamageType).MaximumLength(ItemLimits.DamageTypeMaxLength)
            .WithMessage($"El tipo de daño no puede superar los {ItemLimits.DamageTypeMaxLength} caracteres.");
        RuleFor(x => x.Description).Must(d => ItemRules.IsValidList(d, ItemLimits.DescriptionEntryMaxLength))
            .WithMessage(ItemRules.ListMessage("La descripción", ItemLimits.DescriptionEntryMaxLength));
        RuleFor(x => x.Properties).Must(p => ItemRules.IsValidList(p, ItemLimits.PropertyMaxLength))
            .WithMessage(ItemRules.ListMessage("Las propiedades", ItemLimits.PropertyMaxLength));
        RuleFor(x => x.Effects).Must(e => ItemRules.IsValidList(e, ItemLimits.EffectMaxLength))
            .WithMessage(ItemRules.ListMessage("Los efectos", ItemLimits.EffectMaxLength));
        RuleFor(x => x.RangeNormal).InclusiveBetween(0, ItemLimits.MaxRange).WithMessage($"El alcance debe estar entre 0 y {ItemLimits.MaxRange}.");
        RuleFor(x => x.RangeLong).InclusiveBetween(0, ItemLimits.MaxRange).WithMessage($"El alcance debe estar entre 0 y {ItemLimits.MaxRange}.");
        RuleFor(x => x.ArmorClassBase).InclusiveBetween(0, ItemLimits.MaxArmorClass)
            .WithMessage($"La CA base debe estar entre 0 y {ItemLimits.MaxArmorClass}.");
        RuleFor(x => x.MaxDexBonus).InclusiveBetween(0, ItemLimits.MaxDexBonus)
            .WithMessage($"El máximo de Destreza debe estar entre 0 y {ItemLimits.MaxDexBonus}.");
        RuleFor(x => x.StrengthMinimum).InclusiveBetween(0, ItemLimits.MaxStrengthMinimum)
            .WithMessage($"La Fuerza mínima debe estar entre 0 y {ItemLimits.MaxStrengthMinimum}.");
        RuleFor(x => x.WeightLb).InclusiveBetween(0m, ItemLimits.MaxWeightLb)
            .WithMessage($"El peso debe estar entre 0 y {ItemLimits.MaxWeightLb} lb.");
        RuleFor(x => x.AttackBonus).InclusiveBetween(ItemLimits.MinBonus, ItemLimits.MaxBonus)
            .WithMessage($"El bono de ataque debe estar entre {ItemLimits.MinBonus} y {ItemLimits.MaxBonus}.");
        RuleFor(x => x.DamageBonus).InclusiveBetween(ItemLimits.MinBonus, ItemLimits.MaxBonus)
            .WithMessage($"El bono de daño debe estar entre {ItemLimits.MinBonus} y {ItemLimits.MaxBonus}.");
        RuleFor(x => x.Modifiers).Must(ItemRules.IsValidModifierCount).WithMessage(ItemModifier.TooManyMessage);
        RuleForEach(x => x.Modifiers).NotNull().WithMessage(ItemRules.NullModifierMessage).SetValidator(new ItemModifierDtoValidator());
    }
}

/// <summary>Homebrew item sent by a DM (<c>POST /campaigns/{id}/items</c>); enums as names.</summary>
public sealed record ItemTemplateInput
{
    public string Name { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string? Subcategory { get; init; }

    public string? Rarity { get; init; }

    public bool RequiresAttunement { get; init; }

    public int? CostCp { get; init; }

    public decimal? WeightLb { get; init; }

    public string? DamageDice { get; init; }

    public string? DamageType { get; init; }

    public string? VersatileDice { get; init; }

    public IReadOnlyList<string>? Properties { get; init; }

    public int? RangeNormal { get; init; }

    public int? RangeLong { get; init; }

    public int? ArmorClassBase { get; init; }

    public bool? AddDexModifier { get; init; }

    public int? MaxDexBonus { get; init; }

    public int? StrengthMinimum { get; init; }

    public bool StealthDisadvantage { get; init; }

    public IReadOnlyList<string>? Description { get; init; }

    public IReadOnlyList<string>? Effects { get; init; }

    public IReadOnlyList<ItemModifierDto>? Modifiers { get; init; }

    public static ItemTemplateInput From(ItemTemplateData d) => new()
    {
        Name = d.Name,
        Category = d.Category.ToString(),
        Subcategory = d.Subcategory,
        Rarity = d.Rarity?.ToString(),
        RequiresAttunement = d.RequiresAttunement,
        CostCp = d.CostCp,
        WeightLb = d.WeightLb,
        DamageDice = d.DamageDice,
        DamageType = d.DamageType,
        VersatileDice = d.VersatileDice,
        Properties = d.Properties,
        RangeNormal = d.RangeNormal,
        RangeLong = d.RangeLong,
        ArmorClassBase = d.ArmorClassBase,
        AddDexModifier = d.AddDexModifier,
        MaxDexBonus = d.MaxDexBonus,
        StrengthMinimum = d.StrengthMinimum,
        StealthDisadvantage = d.StealthDisadvantage,
        Description = d.Description,
        Effects = d.Effects,
        Modifiers = ItemModifierDto.FromAll(d.Modifiers),
    };

    /// <summary>Maps to the domain data (texts trimmed, blank optional texts and list entries dropped). Call only on a validated input.</summary>
    public ItemTemplateData ToData() => new()
    {
        Name = Name.Trim(),
        Category = EnumNames.Parse<ItemCategory>(Category),
        Subcategory = Subcategory?.Trim() ?? string.Empty,
        Rarity = Rarity is null ? null : EnumNames.Parse<ItemRarity>(Rarity),
        RequiresAttunement = RequiresAttunement,
        CostCp = CostCp,
        WeightLb = WeightLb,
        DamageDice = Blank(DamageDice),
        DamageType = Blank(DamageType),
        VersatileDice = Blank(VersatileDice),
        Properties = Clean(Properties),
        RangeNormal = RangeNormal,
        RangeLong = RangeLong,
        ArmorClassBase = ArmorClassBase,
        AddDexModifier = AddDexModifier,
        MaxDexBonus = MaxDexBonus,
        StrengthMinimum = StrengthMinimum,
        StealthDisadvantage = StealthDisadvantage,
        Description = Clean(Description),
        Effects = Clean(Effects),
        Modifiers = (Modifiers ?? []).Select(m => m.ToDomain().Normalize()).ToArray(),
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string[] Clean(IReadOnlyList<string>? values) =>
        (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToArray();
}

public sealed class ItemTemplateInputValidator : AbstractValidator<ItemTemplateInput>
{
    public ItemTemplateInputValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Indica el nombre del objeto.")
            .Must(n => n is null || n.Trim().Length <= ItemLimits.NameMaxLength)
            .WithMessage($"El nombre no puede superar los {ItemLimits.NameMaxLength} caracteres.");
        RuleFor(x => x.Category).Must(EnumNames.IsValid<ItemCategory>).WithMessage(ItemRules.CategoryMessage);
        RuleFor(x => x.Subcategory).Must(s => s is null || s.Trim().Length <= ItemLimits.SubcategoryMaxLength)
            .WithMessage($"La subcategoría no puede superar los {ItemLimits.SubcategoryMaxLength} caracteres.");
        RuleFor(x => x.Rarity).Must(EnumNames.IsValid<ItemRarity>).When(x => x.Rarity is not null).WithMessage(ItemRules.RarityMessage);
        RuleFor(x => x.CostCp).InclusiveBetween(0, ItemLimits.MaxCostCp)
            .WithMessage($"El precio debe estar entre 0 y {ItemLimits.MaxCostCp} pc.");
        RuleFor(x => x.WeightLb).InclusiveBetween(0m, ItemLimits.MaxWeightLb)
            .WithMessage($"El peso debe estar entre 0 y {ItemLimits.MaxWeightLb} lb.");
        RuleFor(x => x.DamageDice).MaximumLength(ItemLimits.DiceMaxLength)
            .WithMessage($"El dado de daño no puede superar los {ItemLimits.DiceMaxLength} caracteres.");
        RuleFor(x => x.VersatileDice).MaximumLength(ItemLimits.DiceMaxLength)
            .WithMessage($"El dado de daño a dos manos no puede superar los {ItemLimits.DiceMaxLength} caracteres.");
        RuleFor(x => x.DamageType).MaximumLength(ItemLimits.DamageTypeMaxLength)
            .WithMessage($"El tipo de daño no puede superar los {ItemLimits.DamageTypeMaxLength} caracteres.");
        RuleFor(x => x.Properties).Must(p => ItemRules.IsValidList(p, ItemLimits.PropertyMaxLength))
            .WithMessage(ItemRules.ListMessage("Las propiedades", ItemLimits.PropertyMaxLength));
        RuleFor(x => x.Description).Must(d => ItemRules.IsValidList(d, ItemLimits.DescriptionEntryMaxLength))
            .WithMessage(ItemRules.ListMessage("La descripción", ItemLimits.DescriptionEntryMaxLength));
        RuleFor(x => x.Effects).Must(e => ItemRules.IsValidList(e, ItemLimits.EffectMaxLength))
            .WithMessage(ItemRules.ListMessage("Los efectos", ItemLimits.EffectMaxLength));
        RuleFor(x => x.RangeNormal).InclusiveBetween(0, ItemLimits.MaxRange).WithMessage($"El alcance debe estar entre 0 y {ItemLimits.MaxRange}.");
        RuleFor(x => x.RangeLong).InclusiveBetween(0, ItemLimits.MaxRange).WithMessage($"El alcance debe estar entre 0 y {ItemLimits.MaxRange}.");
        RuleFor(x => x.ArmorClassBase).InclusiveBetween(0, ItemLimits.MaxArmorClass)
            .WithMessage($"La CA base debe estar entre 0 y {ItemLimits.MaxArmorClass}.");
        RuleFor(x => x.MaxDexBonus).InclusiveBetween(0, ItemLimits.MaxDexBonus)
            .WithMessage($"El máximo de Destreza debe estar entre 0 y {ItemLimits.MaxDexBonus}.");
        RuleFor(x => x.StrengthMinimum).InclusiveBetween(0, ItemLimits.MaxStrengthMinimum)
            .WithMessage($"La Fuerza mínima debe estar entre 0 y {ItemLimits.MaxStrengthMinimum}.");
        RuleFor(x => x.Modifiers).Must(ItemRules.IsValidModifierCount).WithMessage(ItemModifier.TooManyMessage);
        RuleForEach(x => x.Modifiers).NotNull().WithMessage(ItemRules.NullModifierMessage).SetValidator(new ItemModifierDtoValidator());
    }
}

/// <summary>
/// Partial edit of a homebrew item (<c>PATCH /campaigns/{id}/items/{templateId}</c>): absent fields do
/// not change; an explicit <c>null</c> clears an optional field. The merged result is validated as an
/// <see cref="ItemTemplateInput"/>.
/// </summary>
public sealed record ItemTemplatePatch
{
    public Optional<string?> Name { get; init; }

    public Optional<string?> Category { get; init; }

    public Optional<string?> Subcategory { get; init; }

    public Optional<string?> Rarity { get; init; }

    public Optional<bool?> RequiresAttunement { get; init; }

    public Optional<int?> CostCp { get; init; }

    public Optional<decimal?> WeightLb { get; init; }

    public Optional<string?> DamageDice { get; init; }

    public Optional<string?> DamageType { get; init; }

    public Optional<string?> VersatileDice { get; init; }

    public Optional<IReadOnlyList<string>?> Properties { get; init; }

    public Optional<int?> RangeNormal { get; init; }

    public Optional<int?> RangeLong { get; init; }

    public Optional<int?> ArmorClassBase { get; init; }

    public Optional<bool?> AddDexModifier { get; init; }

    public Optional<int?> MaxDexBonus { get; init; }

    public Optional<int?> StrengthMinimum { get; init; }

    public Optional<bool?> StealthDisadvantage { get; init; }

    public Optional<IReadOnlyList<string>?> Description { get; init; }

    public Optional<IReadOnlyList<string>?> Effects { get; init; }

    /// <summary>Replaces the whole list of modifiers; <c>null</c> or <c>[]</c> removes them.</summary>
    public Optional<IReadOnlyList<ItemModifierDto>?> Modifiers { get; init; }

    /// <summary>The current data with the given fields applied (null required fields become empty/false).</summary>
    public ItemTemplateInput ApplyTo(ItemTemplateInput current) => current with
    {
        Name = Name.IsSet ? Name.Value ?? string.Empty : current.Name,
        Category = Category.IsSet ? Category.Value ?? string.Empty : current.Category,
        Subcategory = Pick(Subcategory, current.Subcategory),
        Rarity = Pick(Rarity, current.Rarity),
        RequiresAttunement = RequiresAttunement.IsSet ? RequiresAttunement.Value ?? false : current.RequiresAttunement,
        CostCp = Pick(CostCp, current.CostCp),
        WeightLb = Pick(WeightLb, current.WeightLb),
        DamageDice = Pick(DamageDice, current.DamageDice),
        DamageType = Pick(DamageType, current.DamageType),
        VersatileDice = Pick(VersatileDice, current.VersatileDice),
        Properties = Pick(Properties, current.Properties),
        RangeNormal = Pick(RangeNormal, current.RangeNormal),
        RangeLong = Pick(RangeLong, current.RangeLong),
        ArmorClassBase = Pick(ArmorClassBase, current.ArmorClassBase),
        AddDexModifier = Pick(AddDexModifier, current.AddDexModifier),
        MaxDexBonus = Pick(MaxDexBonus, current.MaxDexBonus),
        StrengthMinimum = Pick(StrengthMinimum, current.StrengthMinimum),
        StealthDisadvantage = StealthDisadvantage.IsSet ? StealthDisadvantage.Value ?? false : current.StealthDisadvantage,
        Description = Pick(Description, current.Description),
        Effects = Pick(Effects, current.Effects),
        Modifiers = Pick(Modifiers, current.Modifiers),
    };

    private static T Pick<T>(Optional<T> value, T current) => value.IsSet ? value.Value : current;
}
