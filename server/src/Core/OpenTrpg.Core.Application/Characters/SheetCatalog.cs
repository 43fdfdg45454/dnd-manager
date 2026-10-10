using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Characters;

/// <summary>
/// Catalog data needed to calculate and describe a set of characters, loaded with one query per
/// catalog table. Lookups of indexes missing from the catalog return null.
/// </summary>
public sealed class SheetCatalog
{
    /// <summary>Hit die assumed for a class missing from the catalog (indexes are validated on edit, but the catalog can change).</summary>
    private const int FallbackHitDie = 8;

    private readonly Dictionary<string, ClassDefinition> _classes;
    private readonly Dictionary<string, ClassInfo> _classInfos;
    private readonly Dictionary<string, SubclassDefinition> _subclasses;
    private readonly Dictionary<string, RaceDefinition> _races;
    private readonly Dictionary<string, SubraceDefinition> _subraces;
    private readonly ILookup<string, RaceExtensionDefinition> _raceExtensions;
    private readonly Dictionary<string, string> _grantedSpellNames;
    private readonly Dictionary<string, BackgroundDefinition> _backgrounds;
    private readonly Dictionary<string, SpellDefinition> _spells;
    private readonly Dictionary<string, OptionDefinition> _options;
    private readonly IReadOnlyList<SkillInfo> _skills;
    private readonly IReadOnlyList<FeatureDefinition> _featureResources;

    private SheetCatalog(
        IEnumerable<ClassDefinition> classes,
        IReadOnlyList<ClassLevel> levels,
        IEnumerable<SubclassDefinition> subclasses,
        IEnumerable<RaceDefinition> races,
        IEnumerable<SubraceDefinition> subraces,
        IEnumerable<BackgroundDefinition> backgrounds,
        IEnumerable<SpellDefinition> spells,
        IEnumerable<OptionDefinition> options,
        IReadOnlyList<SkillInfo> skills,
        IEnumerable<RaceExtensionDefinition> raceExtensions,
        IEnumerable<SpellDefinition> grantedSpells,
        IReadOnlyList<FeatureDefinition> featureResources)
    {
        _classes = classes.ToDictionary(c => c.Index, StringComparer.Ordinal);
        _classInfos = _classes.Values.ToDictionary(c => c.Index, c => ClassInfo.From(c, levels), StringComparer.Ordinal);
        _subclasses = subclasses.ToDictionary(s => s.Index, StringComparer.Ordinal);
        _races = races.ToDictionary(r => r.Index, StringComparer.Ordinal);
        _subraces = subraces.ToDictionary(s => s.Index, StringComparer.Ordinal);
        _backgrounds = backgrounds.ToDictionary(b => b.Index, StringComparer.Ordinal);
        _spells = spells.ToDictionary(s => s.Index, StringComparer.Ordinal);
        _options = options.ToDictionary(o => o.Index, StringComparer.Ordinal);
        _skills = skills;
        _raceExtensions = raceExtensions.ToLookup(e => e.RaceIndex, StringComparer.Ordinal);
        _grantedSpellNames = grantedSpells.GroupBy(s => s.Index, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().Name, StringComparer.Ordinal);
        _featureResources = featureResources;
    }

    /// <summary>Loads what the given characters reference (plus the extra indexes, e.g. those of a pending edit).</summary>
    /// <param name="includeSpells">Whether to load the spells known by the characters (only the detail shows them).</param>
    public static async Task<SheetCatalog> LoadAsync(
        ICatalogRepository catalog,
        IReadOnlyCollection<Character> characters,
        bool includeSpells,
        CancellationToken cancellationToken)
    {
        static List<string> Distinct(IEnumerable<string?> values) =>
            values.OfType<string>().Distinct(StringComparer.Ordinal).ToList();

        var classIndexes = Distinct(characters.SelectMany(c => c.Classes).Select(c => c.ClassIndex));
        var classes = await catalog.ListClassesByIndexAsync(classIndexes, cancellationToken);
        var levels = await catalog.ListClassLevelsByClassAsync(classIndexes, cancellationToken);
        var subclassIndexes = Distinct(characters.SelectMany(c => c.Classes).Select(c => c.SubclassIndex));
        var subclasses = await catalog.ListSubclassesByIndexAsync(subclassIndexes, cancellationToken);
        var featureResources = await catalog.ListSubclassFeatureResourcesAsync(subclassIndexes, cancellationToken);
        var raceIndexes = Distinct(characters.Select(c => c.RaceIndex));
        var races = await catalog.ListRacesByIndexAsync(raceIndexes, cancellationToken);
        var subraces = await catalog.ListSubracesByIndexAsync(Distinct(characters.Select(c => c.SubraceIndex)), cancellationToken);
        var raceExtensions = await catalog.ListRaceExtensionsAsync(raceIndexes, cancellationToken);

        // Racial spells cast per long rest name their resource after the spell.
        var grantedSpellIndexes = Distinct(races.Select(r => r.Grants)
            .Concat(subraces.Select(s => s.Grants))
            .Concat(raceExtensions.Select(e => e.Grants))
            .SelectMany(g => g.Spells)
            .Where(s => s.UsesPerLongRest is not null)
            .Select(s => s.Index));
        var grantedSpells = await catalog.ListSpellsByIndexAsync(grantedSpellIndexes, cancellationToken);
        var backgrounds = await catalog.ListBackgroundsByIndexAsync(Distinct(characters.Select(c => c.BackgroundIndex)), cancellationToken);
        var spells = includeSpells
            ? await catalog.ListSpellsByIndexAsync(Distinct(characters.SelectMany(c => c.Spells).Select(s => s.SpellIndex)), cancellationToken)
            : [];
        var skills = (await catalog.ListSkillsAsync(cancellationToken)).Select(SkillInfo.From).ToList();

        // Options picked in level choices (fighting styles, invocations, feats...): their effects enter the sheet.
        var options = await catalog.ListOptionsByIndexAsync(Distinct(characters.SelectMany(ChoiceEffects.OptionIndexes)), cancellationToken);

        return new SheetCatalog(classes, levels, subclasses, races, subraces, backgrounds, spells, options, skills, raceExtensions, grantedSpells, featureResources);
    }

    public ClassDefinition? Class(string index) => _classes.GetValueOrDefault(index);

    public SubclassDefinition? Subclass(string? index) => index is null ? null : _subclasses.GetValueOrDefault(index);

    public RaceDefinition? Race(string? index) => index is null ? null : _races.GetValueOrDefault(index);

    public SubraceDefinition? Subrace(string? index) => index is null ? null : _subraces.GetValueOrDefault(index);

    /// <summary>Fixed grants of the race with those of the packs that extend it; null when the race is not in the catalog.</summary>
    public OptionGrants? RaceGrants(string? raceIndex) =>
        Race(raceIndex) is { } race ? RaceExtensions(race.Index).Aggregate(race.Grants, (grants, extension) => grants.Merge(extension.Grants)) : null;

    /// <summary>What content packs add to the race (traits and grants).</summary>
    public IReadOnlyList<RaceExtensionDefinition> RaceExtensions(string? raceIndex) => raceIndex is null ? [] : [.. _raceExtensions[raceIndex]];

    public BackgroundDefinition? Background(string? index) => index is null ? null : _backgrounds.GetValueOrDefault(index);

    public SpellDefinition? Spell(string index) => _spells.GetValueOrDefault(index);

    public OptionDefinition? Option(string index) => _options.GetValueOrDefault(index);

    /// <summary>The companion feature the character has reached (content packs), or null.</summary>
    public CompanionGrant? Companion(Character character) => CompanionGrants.Find(character, _featureResources);

    /// <summary>Input of <see cref="SheetCalculator.Calculate"/> for a character covered by this catalog.</summary>
    public SheetInput InputFor(Character character, EquippedGear gear)
    {
        var classes = character.Classes
            .Select(c => (_classInfos.GetValueOrDefault(c.ClassIndex) ?? new ClassInfo { Index = c.ClassIndex, HitDie = FallbackHitDie })
                .WithSubclassSpellcasting(Subclass(c.SubclassIndex)?.Spellcasting))
            .ToList();
        var race = Race(character.RaceIndex);
        var subrace = Subrace(character.SubraceIndex);

        // Resources and modifiers of the subclass features reached (content packs) join those of the chosen options.
        var choices = character.Choices.Count == 0 ? null : ChoiceEffects.Build(character, Option);
        var featureResources = ChoiceEffects.FeatureResources(character, _featureResources);
        var featureModifiers = ChoiceEffects.FeatureModifiers(character, _featureResources);
        if (featureResources.Count > 0 || featureModifiers.Count > 0)
        {
            var current = choices ?? ChoiceEffects.None;
            choices = current with
            {
                Resources = [.. current.Resources, .. featureResources],
                Modifiers = [.. current.Modifiers, .. featureModifiers],
            };
        }

        return new SheetInput(
            character,
            classes,
            race is null ? null : RaceInfo.From(race, RaceExtensions(race.Index)) with { SpellNames = _grantedSpellNames },
            subrace is null ? null : SubraceInfo.From(subrace) with { SpellNames = _grantedSpellNames },
            _skills,
            gear,
            choices);
    }
}
