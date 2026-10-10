using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Infrastructure.Catalog;

// Sections of format 3 that the SRD as base pack needs (phase 34C): the skills of the game (base pack only) and the
// traits shared by several races or subraces (traits[]), which the races reference by index.
internal sealed partial class ContentPackValidator
{
    private readonly List<(string Path, string Index)> _sharedTraitReferences = [];
    private readonly HashSet<string> _sharedTraits = new(StringComparer.Ordinal);
    private readonly List<(string Path, string Index, bool Subrace)> _traitOwnerReferences = [];

    /// <summary><c>skills[]</c>: only the base pack defines the skills; they can be referenced by the rest of the pack.</summary>
    private void Skills(List<PackSkillJson?>? skills, ContentPackRows rows)
    {
        if (skills is null)
        {
            return;
        }

        if (!IsBasePack)
        {
            AddError("skills", "Solo el paquete base del sistema define habilidades.");
            return;
        }

        ForEach("skills", skills, (path, skill) =>
        {
            var index = Index($"{path}.index", "skills", skill.Index);
            var name = RequiredText($"{path}.name", skill.Name, NameMaxLength);
            var ability = skill.Ability?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(ability) || !Abilities.IsValid(ability))
            {
                AddError($"{path}.ability", "Característica desconocida (str, dex, con, int, wis o cha).");
                ability = null;
            }

            var description = Paragraphs($"{path}.description", skill.Description);
            if (index is null || ability is null)
            {
                return;
            }

            _skills[index] = name;
            rows.Skills.Add(new SkillDefinition { Index = index, Name = name, AbilityIndex = ability, Description = description });
        });
    }

    /// <summary><c>traits[]</c>: traits with their races and subraces (of the pack or of the catalog).</summary>
    private void SharedTraits(List<PackSharedTraitJson?>? traits, ContentPackRows rows)
    {
        ForEach("traits", traits, (path, trait) =>
        {
            var index = Index($"{path}.index", "traits", trait.Index);
            var name = RequiredText($"{path}.name", trait.Name, NameMaxLength);
            var description = Paragraphs($"{path}.description", trait.Description);
            var races = new List<string>();
            ForEachText($"{path}.races", trait.Races, (racePath, race) =>
            {
                if (Reference(racePath, race) is { } raceIndex && !races.Contains(raceIndex))
                {
                    races.Add(raceIndex);
                    _traitOwnerReferences.Add((racePath, raceIndex, false));
                }
            });
            var subraces = new List<string>();
            ForEachText($"{path}.subraces", trait.Subraces, (subracePath, subrace) =>
            {
                if (Reference(subracePath, subrace) is { } subraceIndex && !subraces.Contains(subraceIndex))
                {
                    subraces.Add(subraceIndex);
                    _traitOwnerReferences.Add((subracePath, subraceIndex, true));
                }
            });
            if (index is null)
            {
                return;
            }

            _sharedTraits.Add(index);
            rows.Traits.Add(new TraitDefinition
            {
                Index = index,
                Name = name,
                Description = description,
                RaceIndexes = races,
                SubraceIndexes = subraces,
                Source = _id,
            });
        });
    }

    /// <summary>The traits races reference must be in <c>traits[]</c>; the races and subraces of a trait must exist.</summary>
    private void CheckSharedTraitReferences(ContentPackRows rows)
    {
        foreach (var (path, index) in _sharedTraitReferences.Where(r => !_sharedTraits.Contains(r.Index)))
        {
            AddError(path, $"El rasgo '{index}' no está en traits[] del paquete.");
        }

        var races = rows.Races.Select(r => r.Index).ToHashSet(StringComparer.Ordinal);
        var subraces = rows.Subraces.Select(r => r.Index).ToHashSet(StringComparer.Ordinal);
        foreach (var (path, index, subrace) in _traitOwnerReferences)
        {
            if (subrace ? !subraces.Contains(index) : !races.Contains(index) && _context.Races?.ContainsKey(index) != true)
            {
                AddError(path, subrace
                    ? $"La subraza '{index}' no está en el paquete."
                    : $"La raza '{index}' no existe en el catálogo ni en el paquete.");
            }
        }
    }
}
