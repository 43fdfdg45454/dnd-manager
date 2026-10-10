using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Application;
using OpenTrpg.Systems.Dnd5e.Application;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Application.Catalog;

/// <param name="Search">Lower-case term matched against the name; null for no search.</param>
/// <param name="ClassIndex">Class index ("wizard"); null for every class.</param>
/// <param name="School">Lower-case school name or index ("evocation"); null for every school.</param>
public sealed record SpellFilter(string? Search, int? Level, string? ClassIndex, string? School, bool? Ritual, bool? Concentration);
