namespace OpenTrpg.Core.Domain.Characters;

/// <summary>
/// One term of a calculated value ("origin of every point"). <see cref="Source"/> is a source id defined by
/// the game system (for example "ability" or "item"); <see cref="Label"/> is user-facing text (Spanish),
/// e.g. "Destreza" or the name of an item.
/// </summary>
public sealed record BreakdownPart(string Source, string Label, int Value);

/// <summary>
/// A calculated value explained point by point: the values of <see cref="Parts"/> always add up to
/// <see cref="Total"/>. A manual override is a final part worth (override − calculated).
/// </summary>
public sealed record ValueBreakdown(int Total, IReadOnlyList<BreakdownPart> Parts);
