using System.Text.Json.Nodes;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>
/// A character as the core hands it to its game system: the core character (tracked when the use case modifies it)
/// and the system's part of the same character once loaded. The system loads its part (same key) the first time it
/// needs it and keeps it in <see cref="System"/>, so several calls on the same reference load it once.
/// </summary>
public sealed class CharacterRef(Character character, object? system = null)
{
    public Character Character { get; } = character;

    /// <summary>The system's part of the character (opaque for the core), or null while it is not loaded.</summary>
    public object? System { get; set; } = system;

    public Guid Id => Character.Id;

    public Guid CampaignId => Character.CampaignId;
}

/// <summary>A sheet calculated by a game system. Opaque for the core, which only hands it back to the system.</summary>
public abstract class SystemSheet
{
}

/// <summary>
/// Fields a game system adds to the summary of a character in the campaign roster, at the same level as the
/// core fields (for example race, classes, level and hit points).
/// </summary>
public sealed record RosterLine(JsonObject Fields);

/// <summary>What a character sheet of the system stores and what can be overridden by hand.</summary>
/// <param name="EditableFields">Fields of a sheet edit (JSON names), the core profile fields included.</param>
/// <param name="OverridableFields">Calculated values that can be overridden (fixed names).</param>
/// <param name="OverridablePrefixes">Prefixes of parameterized overridable values (for example <c>skill.</c>).</param>
public sealed record SheetSchema(
    IReadOnlyList<string> EditableFields,
    IReadOnlyList<string> OverridableFields,
    IReadOnlyList<string> OverridablePrefixes);
