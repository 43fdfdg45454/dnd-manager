using Dnd.Application.Abstractions.Persistence;
using Dnd.Domain.Catalog;
using Dnd.Domain.Characters;

namespace Dnd.Application.Characters;

/// <summary>
/// Catalog data needed to calculate and describe a set of characters, loaded with one query per
/// catalog table. Lookups of indexes missing from the catalog return null.
/// </summary>
public sealed class SheetCatalog
{
    /// <summary>Hit die assumed for a class missing from the catalog (should not happen: indexes are validated on edit).</summary>
    private const int FallbackHitDie = 8;

    private readonly Dictionary<string, ClassDefinition> _classes;
    private readonly Dictionary<string, ClassInfo> _classInfos;
    private readonly Dictionary<string, SubclassDefinition> _subclasses;
    private readonly Dictionary<string, RaceDefinition> _races;
    private readonly Dictionary<string, SubraceDefinition> _subraces;
    private readonly Dictionary<string, BackgroundDefinition> _backgrounds;
    private readonly Dictionary<string, SpellDefinition> _spells;
    private readonly IReadOnlyList<SkillInfo> _skills;

    private SheetCatalog(
        IEnumerable<ClassDefinition> classes,
        IReadOnlyList<ClassLevel> levels,
        IEnumerable<SubclassDefinition> subclasses,
        IEnumerable<RaceDefinition> races,
        IEnumerable<SubraceDefinition> subraces,
        IEnumerable<BackgroundDefinition> backgrounds,
        IEnumerable<SpellDefinition> spells,
        IReadOnlyList<SkillInfo> skills)
    {
        _classes = classes.ToDictionary(c => c.Index, StringComparer.Ordinal);
        _classInfos = _classes.Values.ToDictionary(c => c.Index, c => ClassInfo.From(c, levels), StringComparer.Ordinal);
        _subclasses = subclasses.ToDictionary(s => s.Index, StringComparer.Ordinal);
        _races = races.ToDictionary(r => r.Index, StringComparer.Ordinal);
        _subraces = subraces.ToDictionary(s => s.Index, StringComparer.Ordinal);
        _backgrounds = backgrounds.ToDictionary(b => b.Index, StringComparer.Ordinal);
        _spells = spells.ToDictionary(s => s.Index, StringComparer.Ordinal);
        _skills = skills;
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
        var subclasses = await catalog.ListSubclassesByIndexAsync(Distinct(characters.SelectMany(c => c.Classes).Select(c => c.SubclassIndex)), cancellationToken);
        var races = await catalog.ListRacesByIndexAsync(Distinct(characters.Select(c => c.RaceIndex)), cancellationToken);
        var subraces = await catalog.ListSubracesByIndexAsync(Distinct(characters.Select(c => c.SubraceIndex)), cancellationToken);
        var backgrounds = await catalog.ListBackgroundsByIndexAsync(Distinct(characters.Select(c => c.BackgroundIndex)), cancellationToken);
        var spells = includeSpells
            ? await catalog.ListSpellsByIndexAsync(Distinct(characters.SelectMany(c => c.Spells).Select(s => s.SpellIndex)), cancellationToken)
            : [];
        var skills = (await catalog.ListSkillsAsync(cancellationToken)).Select(SkillInfo.From).ToList();

        return new SheetCatalog(classes, levels, subclasses, races, subraces, backgrounds, spells, skills);
    }

    public ClassDefinition? Class(string index) => _classes.GetValueOrDefault(index);

    public SubclassDefinition? Subclass(string? index) => index is null ? null : _subclasses.GetValueOrDefault(index);

    public RaceDefinition? Race(string? index) => index is null ? null : _races.GetValueOrDefault(index);

    public SubraceDefinition? Subrace(string? index) => index is null ? null : _subraces.GetValueOrDefault(index);

    public BackgroundDefinition? Background(string? index) => index is null ? null : _backgrounds.GetValueOrDefault(index);

    public SpellDefinition? Spell(string index) => _spells.GetValueOrDefault(index);

    /// <summary>Input of <see cref="SheetCalculator.Calculate"/> for a character covered by this catalog.</summary>
    public SheetInput InputFor(Character character, EquippedGear gear)
    {
        var classes = character.Classes
            .Select(c => _classInfos.GetValueOrDefault(c.ClassIndex) ?? new ClassInfo { Index = c.ClassIndex, HitDie = FallbackHitDie })
            .ToList();
        var race = Race(character.RaceIndex);
        var subrace = Subrace(character.SubraceIndex);

        return new SheetInput(
            character,
            classes,
            race is null ? null : RaceInfo.From(race),
            subrace is null ? null : SubraceInfo.From(subrace),
            _skills,
            gear);
    }
}
