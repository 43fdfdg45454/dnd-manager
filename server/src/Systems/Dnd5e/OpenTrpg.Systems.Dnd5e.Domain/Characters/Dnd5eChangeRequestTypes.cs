using OpenTrpg.Core.Domain.Characters;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;
namespace OpenTrpg.Systems.Dnd5e.Domain.Characters;

/// <summary>Change request types of the D&amp;D 5e system (see <see cref="ChangeRequestTypes"/>).</summary>
public static class Dnd5eChangeRequestTypes
{
    /// <summary>A sheet edit (payload: the sheet patch).</summary>
    public const string EditSheet = "EditSheet";

    /// <summary>A player changes the beast of the animal companion (payload <c>{ beastIndex, name }</c>).</summary>
    public const string Companion = "Companion";

    public static IReadOnlyList<string> All { get; } = [EditSheet, Companion];
}
