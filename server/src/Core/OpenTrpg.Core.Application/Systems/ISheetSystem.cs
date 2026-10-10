using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenTrpg.Core.Application.Systems;

/// <summary>Sheet of a game system: what a character stores and how it is calculated (with the breakdown of every value).</summary>
public interface ISheetSystem
{
    /// <summary>Editable and overridable fields.</summary>
    SheetSchema Schema { get; }

    /// <summary>The change request type (one of <see cref="IChangeRequestSystem.Types"/>) of a sheet edit that needs approval.</summary>
    string EditRequestType { get; }

    /// <summary>Calculates the sheet of a character.</summary>
    Task<SystemSheet> CalculateAsync(CharacterRef character, CancellationToken cancellationToken = default);

    /// <summary>Calculates the sheets of several characters at once (roster), by character id.</summary>
    Task<IReadOnlyDictionary<Guid, SystemSheet>> CalculateManyAsync(IReadOnlyList<CharacterRef> characters, CancellationToken cancellationToken = default);

    /// <summary>
    /// After an edit, an inventory change or an approved request: calculates the sheet and keeps the derived state of
    /// the character in sync. Returns the new sheet.
    /// </summary>
    Task<SystemSheet> RecalculateAsync(CharacterRef character, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a sheet edit (the system's sheet patch, which includes the core profile fields) without applying it and
    /// returns it normalized (absent fields omitted), as the core stores it in a change request.
    /// </summary>
    Task<JsonObject> ValidateEditAsync(CharacterRef character, JsonElement patch, CancellationToken cancellationToken = default);

    /// <summary>Applies a sheet edit checked by <see cref="ValidateEditAsync"/> and recalculates the sheet.</summary>
    Task ApplyEditAsync(CharacterRef character, JsonElement patch, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>The current values of the fields the edit changes, in the same shape ("before" of a change request).</summary>
    Task<JsonObject> SnapshotAsync(CharacterRef character, JsonElement patch, CancellationToken cancellationToken = default);

    /// <summary>The fields of the system in the detail of a character, written at the same level as the core ones.</summary>
    Task<JsonObject> BuildDetailAsync(CharacterRef character, CancellationToken cancellationToken = default);

    /// <summary>The fields of the system in the roster line of a character; <paramref name="showHitPoints"/> for the owner and DMs.</summary>
    RosterLine BuildRosterLine(CharacterRef character, SystemSheet sheet, bool showHitPoints);
}
