using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Application.Catalog;

/// <param name="Search">Lower-case term matched against the name; null for no search.</param>
/// <param name="ClassIndex">Class index ("wizard"); null for every class.</param>
/// <param name="School">Lower-case school name or index ("evocation"); null for every school.</param>
public sealed record SpellFilter(string? Search, int? Level, string? ClassIndex, string? School, bool? Ritual, bool? Concentration);
