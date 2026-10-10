using System.Text.Json;
using System.Text.Json.Serialization;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Core.Domain.Rules;
using FluentValidation;

namespace OpenTrpg.Core.Application.Characters;

public sealed record BaseAbilitiesPatch(int Str, int Dex, int Con, int Int, int Wis, int Cha);

public sealed record ClassPatch(string ClassIndex, string? SubclassIndex, int Level);

/// <param name="Type">Skill, SavingThrow, Armor, Weapon, Tool or Language.</param>
/// <param name="Source">Class, Race, Background or Manual (default).</param>
public sealed record ProficiencyPatch(string Type, string Key, bool Expertise = false, string? Source = null);

public sealed record SpellPatch(string SpellIndex, string ClassIndex, bool IsPrepared, bool AlwaysPrepared = false);

public sealed record OverridePatch(string Field, int Value, string? Note = null);

/// <summary>
/// Sheet edit sent by the client (<c>PATCH /characters/{id}/sheet</c>) and stored as the payload of
/// EditSheet change requests. Absent fields do not change; every list given replaces the existing one.
/// <see cref="RaceIndex"/>, <see cref="SubraceIndex"/>, <see cref="BackgroundIndex"/> and
/// <see cref="Alignment"/> distinguish absent (unchanged) from an explicit <c>null</c> (cleared).
/// </summary>
public sealed record SheetPatch
{
    public string? Name { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<string?> RaceIndex { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<string?> SubraceIndex { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<string?> BackgroundIndex { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<string?> Alignment { get; init; }

    public bool? ApplyRacialBonuses { get; init; }

    /// <summary>"Average" or "Manual".</summary>
    public string? HpMode { get; init; }

    public BaseAbilitiesPatch? BaseAbilities { get; init; }

    public IReadOnlyList<ClassPatch>? Classes { get; init; }

    public IReadOnlyList<ProficiencyPatch>? Proficiencies { get; init; }

    public IReadOnlyList<SpellPatch>? Spells { get; init; }

    public IReadOnlyList<OverridePatch>? Overrides { get; init; }

    public string? Notes { get; init; }

    public string? Backstory { get; init; }

    /// <summary>Personality traits of the background (free text; the wizard writes one per line).</summary>
    public string? PersonalityTraits { get; init; }

    public string? Ideals { get; init; }

    public string? Bonds { get; init; }

    public string? Flaws { get; init; }

    /// <summary>Result of the optional table of the background, e.g. "Especialidad: Bibliotecario".</summary>
    public string? BackgroundDetail { get; init; }

    public int? CopperPieces { get; init; }

    /// <summary>
    /// Height in inches (1–200); explicit <c>null</c> clears it. No mechanical effect: the owner of an
    /// active character changes it without approval.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<int?> HeightInches { get; init; }

    /// <summary>Weight in pounds (1–2000); explicit <c>null</c> clears it. Same rules as <see cref="HeightInches"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<int?> WeightPounds { get; init; }

    /// <summary>True when the patch changes height or weight.</summary>
    [JsonIgnore]
    public bool HasHeightOrWeight => HeightInches.IsSet || WeightPounds.IsSet;

    /// <summary>The same patch without height and weight (what still needs approval for an active character's owner).</summary>
    public SheetPatch WithoutHeightAndWeight() => this with { HeightInches = default, WeightPounds = default };

    /// <summary>True when the patch changes nothing.</summary>
    [JsonIgnore]
    public bool IsEmpty => this == new SheetPatch();

    /// <summary>Maps to the domain edit. Call only on a patch accepted by <see cref="SheetPatchValidator"/>.</summary>
    public SheetEdit ToSheetEdit() => new()
    {
        Name = Name,
        RaceIndex = Clearable(RaceIndex),
        SubraceIndex = Clearable(SubraceIndex),
        BackgroundIndex = Clearable(BackgroundIndex),
        Alignment = Clearable(Alignment),
        ApplyRacialBonuses = ApplyRacialBonuses,
        HpMode = HpMode is null ? null : EnumNames.Parse<HpMode>(HpMode),
        BaseAbilities = BaseAbilities is { } a ? new AbilityScores(a.Str, a.Dex, a.Con, a.Int, a.Wis, a.Cha) : null,
        Classes = Classes?.Select(c => new ClassEntry(c.ClassIndex, c.SubclassIndex, c.Level)).ToList(),
        Proficiencies = Proficiencies?
            .Select(p => new ProficiencyEntry(
                EnumNames.Parse<ProficiencyType>(p.Type),
                p.Key,
                p.Expertise,
                p.Source is null ? ProficiencySource.Manual : EnumNames.Parse<ProficiencySource>(p.Source)))
            .ToList(),
        Spells = Spells?.Select(s => new SpellEntry(s.SpellIndex, s.ClassIndex, s.IsPrepared, s.AlwaysPrepared)).ToList(),
        Overrides = Overrides?.Select(o => new OverrideEntry(o.Field, o.Value, o.Note)).ToList(),
        Notes = Notes,
        Backstory = Backstory,
        PersonalityTraits = PersonalityTraits,
        Ideals = Ideals,
        Bonds = Bonds,
        Flaws = Flaws,
        BackgroundDetail = BackgroundDetail,
        CopperPieces = CopperPieces,
        HeightInches = ClearableNumber(HeightInches),
        WeightPounds = ClearableNumber(WeightPounds),
    };

    /// <summary>Absent → null (unchanged); explicit null → 0 (cleared, as <see cref="SheetEdit"/> expects).</summary>
    private static int? ClearableNumber(Optional<int?> value) => value.IsSet ? value.Value ?? 0 : null;

    /// <summary>Absent → null (unchanged); explicit null → "" (cleared, as <see cref="SheetEdit"/> expects).</summary>
    private static string? Clearable(Optional<string?> value) => value.IsSet ? value.Value ?? string.Empty : null;
}

/// <summary>Serialization of <see cref="SheetPatch"/> as a change request payload (absent fields omitted).</summary>
public static class SheetPatchJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(SheetPatch patch) => JsonSerializer.Serialize(patch, Options);

    /// <summary>Null when the payload is not a valid patch.</summary>
    public static SheetPatch? TryDeserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SheetPatch>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed class SheetPatchValidator : AbstractValidator<SheetPatch>
{
    public const int MaxClasses = 20;
    public const int MaxProficiencies = 200;
    public const int MaxSpells = 500;
    public const int MaxOverrides = 100;

    private static readonly string ScoreMessage =
        $"Las puntuaciones de característica deben estar entre {AbilityRules.MinScore} y {AbilityRules.MaxScore}.";

    public SheetPatchValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre no puede estar vacío.")
            .MaximumLength(Character.NameMaxLength).WithMessage($"El nombre no puede superar los {Character.NameMaxLength} caracteres.")
            .When(x => x.Name is not null);

        ClearableIndex(x => x.RaceIndex, "raceIndex", Character.IndexMaxLength, "La raza");
        ClearableIndex(x => x.SubraceIndex, "subraceIndex", Character.IndexMaxLength, "La subraza");
        ClearableIndex(x => x.BackgroundIndex, "backgroundIndex", Character.IndexMaxLength, "El trasfondo");
        ClearableIndex(x => x.Alignment, "alignment", Character.AlignmentMaxLength, "El alineamiento");

        RuleFor(x => x.HpMode)
            .Must(EnumNames.IsValid<HpMode>)
            .WithMessage($"El modo de puntos de golpe debe ser {EnumNames.Describe<HpMode>()}.")
            .When(x => x.HpMode is not null);

        When(x => x.BaseAbilities is not null, () =>
        {
            RuleFor(x => x.BaseAbilities!.Str).InclusiveBetween(AbilityRules.MinScore, AbilityRules.MaxScore).WithMessage(ScoreMessage).OverridePropertyName("baseAbilities.str");
            RuleFor(x => x.BaseAbilities!.Dex).InclusiveBetween(AbilityRules.MinScore, AbilityRules.MaxScore).WithMessage(ScoreMessage).OverridePropertyName("baseAbilities.dex");
            RuleFor(x => x.BaseAbilities!.Con).InclusiveBetween(AbilityRules.MinScore, AbilityRules.MaxScore).WithMessage(ScoreMessage).OverridePropertyName("baseAbilities.con");
            RuleFor(x => x.BaseAbilities!.Int).InclusiveBetween(AbilityRules.MinScore, AbilityRules.MaxScore).WithMessage(ScoreMessage).OverridePropertyName("baseAbilities.int");
            RuleFor(x => x.BaseAbilities!.Wis).InclusiveBetween(AbilityRules.MinScore, AbilityRules.MaxScore).WithMessage(ScoreMessage).OverridePropertyName("baseAbilities.wis");
            RuleFor(x => x.BaseAbilities!.Cha).InclusiveBetween(AbilityRules.MinScore, AbilityRules.MaxScore).WithMessage(ScoreMessage).OverridePropertyName("baseAbilities.cha");
        });

        When(x => x.Classes is not null, () =>
        {
            RuleFor(x => x.Classes!)
                .Must(c => c.Count <= MaxClasses).WithMessage($"No se admiten más de {MaxClasses} clases.")
                .Must(c => c.Where(e => e is not null).Select(e => e.ClassIndex?.Trim()).Distinct().Count() == c.Count(e => e is not null))
                .WithMessage("Una clase no puede repetirse.")
                .Must(c => c.Where(e => e is not null).Sum(e => Math.Max(0, e.Level)) <= AbilityRules.MaxLevel)
                .WithMessage($"El nivel total no puede superar {AbilityRules.MaxLevel}.")
                .OverridePropertyName("classes");
            RuleForEach(x => x.Classes!)
                .NotNull().WithMessage("La clase no puede ser nula.")
                .ChildRules(c =>
                {
                    c.RuleFor(e => e.ClassIndex).NotEmpty().WithMessage("Indica la clase.")
                        .MaximumLength(Character.IndexMaxLength).WithMessage($"El índice de clase no puede superar los {Character.IndexMaxLength} caracteres.");
                    c.RuleFor(e => e.SubclassIndex).MaximumLength(Character.IndexMaxLength)
                        .WithMessage($"El índice de subclase no puede superar los {Character.IndexMaxLength} caracteres.");
                    c.RuleFor(e => e.Level).InclusiveBetween(AbilityRules.MinLevel, AbilityRules.MaxLevel)
                        .WithMessage($"El nivel de clase debe estar entre {AbilityRules.MinLevel} y {AbilityRules.MaxLevel}.");
                })
                .OverridePropertyName("classes");
        });

        When(x => x.Proficiencies is not null, () =>
        {
            RuleFor(x => x.Proficiencies!)
                .Must(p => p.Count <= MaxProficiencies).WithMessage($"No se admiten más de {MaxProficiencies} competencias.")
                .Must(p => p.Where(e => e is not null).Select(e => (e.Type, e.Key?.Trim())).Distinct().Count() == p.Count(e => e is not null))
                .WithMessage("Una competencia no puede repetirse.")
                .OverridePropertyName("proficiencies");
            RuleForEach(x => x.Proficiencies!)
                .NotNull().WithMessage("La competencia no puede ser nula.")
                .ChildRules(p =>
                {
                    p.RuleFor(e => e.Type).Must(EnumNames.IsValid<ProficiencyType>)
                        .WithMessage($"El tipo de competencia debe ser {EnumNames.Describe<ProficiencyType>()}.");
                    p.RuleFor(e => e.Key).NotEmpty().WithMessage("Indica la competencia.")
                        .MaximumLength(Character.IndexMaxLength).WithMessage($"La competencia no puede superar los {Character.IndexMaxLength} caracteres.");
                    p.RuleFor(e => e.Source).Must(EnumNames.IsValid<ProficiencySource>)
                        .WithMessage($"El origen de la competencia debe ser {EnumNames.Describe<ProficiencySource>()}.")
                        .When(e => e.Source is not null);
                    p.RuleFor(e => e.Expertise)
                        .Must((e, expertise) => !expertise || e.Type is nameof(ProficiencyType.Skill) or nameof(ProficiencyType.Tool))
                        .WithMessage("Solo las habilidades y herramientas admiten pericia.");
                })
                .OverridePropertyName("proficiencies");
        });

        When(x => x.Spells is not null, () =>
        {
            RuleFor(x => x.Spells!)
                .Must(s => s.Count <= MaxSpells).WithMessage($"No se admiten más de {MaxSpells} conjuros.")
                .Must(s => s.Where(e => e is not null).Select(e => (e.SpellIndex?.Trim(), e.ClassIndex?.Trim())).Distinct().Count() == s.Count(e => e is not null))
                .WithMessage("Un conjuro no puede repetirse para la misma clase.")
                .OverridePropertyName("spells");
            RuleForEach(x => x.Spells!)
                .NotNull().WithMessage("El conjuro no puede ser nulo.")
                .ChildRules(s =>
                {
                    s.RuleFor(e => e.SpellIndex).NotEmpty().WithMessage("Indica el conjuro.")
                        .MaximumLength(Character.IndexMaxLength).WithMessage($"El índice de conjuro no puede superar los {Character.IndexMaxLength} caracteres.");
                    s.RuleFor(e => e.ClassIndex).NotEmpty().WithMessage("Indica la clase del conjuro.")
                        .MaximumLength(Character.IndexMaxLength).WithMessage($"El índice de clase no puede superar los {Character.IndexMaxLength} caracteres.");
                })
                .OverridePropertyName("spells");
        });

        When(x => x.Overrides is not null, () =>
        {
            RuleFor(x => x.Overrides!)
                .Must(o => o.Count <= MaxOverrides).WithMessage($"No se admiten más de {MaxOverrides} valores sobrescritos.")
                .Must(o => o.Where(e => e is not null).Select(e => e.Field?.Trim()).Distinct().Count() == o.Count(e => e is not null))
                .WithMessage("Un campo no puede sobrescribirse más de una vez.")
                .OverridePropertyName("overrides");
            RuleForEach(x => x.Overrides!)
                .NotNull().WithMessage("El valor sobrescrito no puede ser nulo.")
                .ChildRules(o =>
                {
                    o.RuleFor(e => e.Field)
                        .Must(f => f is not null && OverrideFields.IsValid(f.Trim()))
                        .WithMessage(e => $"El campo '{e.Field}' no admite sobrescritura.");
                    o.RuleFor(e => e.Value)
                        .Must((e, value) => e.Field is null || IsOverrideValueInRange(e.Field.Trim(), value))
                        .WithMessage(e => OverrideRangeMessage(e.Field?.Trim() ?? string.Empty));
                    o.RuleFor(e => e.Note).MaximumLength(CharacterOverride.NoteMaxLength)
                        .WithMessage($"La nota no puede superar los {CharacterOverride.NoteMaxLength} caracteres.");
                })
                .OverridePropertyName("overrides");
        });

        RuleFor(x => x.Notes).MaximumLength(Character.TextMaxLength)
            .WithMessage($"Las notas no pueden superar los {Character.TextMaxLength} caracteres.");
        RuleFor(x => x.Backstory).MaximumLength(Character.TextMaxLength)
            .WithMessage($"La historia no puede superar los {Character.TextMaxLength} caracteres.");
        RuleFor(x => x.PersonalityTraits).MaximumLength(Character.PersonalityMaxLength)
            .WithMessage($"Los rasgos de personalidad no pueden superar los {Character.PersonalityMaxLength} caracteres.");
        RuleFor(x => x.Ideals).MaximumLength(Character.PersonalityMaxLength)
            .WithMessage($"Los ideales no pueden superar los {Character.PersonalityMaxLength} caracteres.");
        RuleFor(x => x.Bonds).MaximumLength(Character.PersonalityMaxLength)
            .WithMessage($"Los vínculos no pueden superar los {Character.PersonalityMaxLength} caracteres.");
        RuleFor(x => x.Flaws).MaximumLength(Character.PersonalityMaxLength)
            .WithMessage($"Los defectos no pueden superar los {Character.PersonalityMaxLength} caracteres.");
        RuleFor(x => x.BackgroundDetail).MaximumLength(Character.BackgroundDetailMaxLength)
            .WithMessage($"El detalle del trasfondo no puede superar los {Character.BackgroundDetailMaxLength} caracteres.");
        RuleFor(x => x.CopperPieces).InclusiveBetween(0, Character.MaxCopperPieces)
            .WithMessage($"El dinero debe estar entre 0 y {Character.MaxCopperPieces} pc.")
            .When(x => x.CopperPieces is not null);
        RuleFor(x => x.HeightInches)
            .Must(v => !v.IsSet || v.Value is null || v.Value is >= Character.MinHeightInches and <= Character.MaxHeightInches)
            .WithMessage($"La altura debe estar entre {Character.MinHeightInches} y {Character.MaxHeightInches} pulgadas.")
            .OverridePropertyName("heightInches");
        RuleFor(x => x.WeightPounds)
            .Must(v => !v.IsSet || v.Value is null || v.Value is >= Character.MinWeightPounds and <= Character.MaxWeightPounds)
            .WithMessage($"El peso debe estar entre {Character.MinWeightPounds} y {Character.MaxWeightPounds} libras.")
            .OverridePropertyName("weightPounds");
    }

    /// <summary>Same ranges the domain enforces (see <see cref="OverrideFields"/>).</summary>
    private static (int Min, int Max) OverrideRange(string field) => field switch
    {
        _ when field.StartsWith(OverrideFields.AbilityPrefix, StringComparison.Ordinal) => (AbilityRules.MinScore, AbilityRules.MaxScore),
        OverrideFields.HitPointsMax or OverrideFields.ArmorClass or OverrideFields.Speed
            or OverrideFields.PassivePerception or OverrideFields.SpellSaveDc => (0, OverrideFields.MaxValue),
        _ => (OverrideFields.MinValue, OverrideFields.MaxValue),
    };

    private static bool IsOverrideValueInRange(string field, int value)
    {
        var (min, max) = OverrideRange(field);
        return value >= min && value <= max;
    }

    private static string OverrideRangeMessage(string field)
    {
        var (min, max) = OverrideRange(field);
        return $"El valor de '{field}' debe estar entre {min} y {max}.";
    }

    private void ClearableIndex(System.Linq.Expressions.Expression<Func<SheetPatch, Optional<string?>>> property, string name, int maxLength, string label)
    {
        RuleFor(property)
            .Must(v => !v.IsSet || v.Value is null || v.Value.Trim().Length <= maxLength)
            .WithMessage($"{label} no puede superar los {maxLength} caracteres.")
            .OverridePropertyName(name);
    }
}
