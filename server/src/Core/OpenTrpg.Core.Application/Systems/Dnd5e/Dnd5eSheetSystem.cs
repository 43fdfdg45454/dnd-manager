using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentValidation;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Abstractions.Persistence;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Core.Domain.Characters;

namespace OpenTrpg.Core.Application.Systems.Dnd5e;

/// <summary>The D&amp;D 5e sheet (<see cref="ISheetSystem"/>), delegating in <see cref="ICharacterSheetService"/>.</summary>
public sealed class Dnd5eSheetSystem(
    Dnd5eCharacterParts parts,
    ICharacterSheetService sheets,
    IDnd5eCharacterRepository characters,
    ICatalogRepository catalog,
    IEquippedGearProvider gearProvider,
    IValidator<SheetPatch> patchValidator) : ISheetSystem
{
    private static readonly SheetSchema SchemaValue = new(
        typeof(SheetPatch).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always })
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .ToList(),
        OverrideFields.Fixed,
        [OverrideFields.AbilityPrefix, OverrideFields.SavePrefix, OverrideFields.SkillPrefix]);

    public SheetSchema Schema => SchemaValue;

    public string EditRequestType => Dnd5eChangeRequestTypes.EditSheet;

    public async Task<SystemSheet> CalculateAsync(CharacterRef character, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        return new Dnd5eSystemSheet(loaded, await sheets.CalculateAsync(loaded, cancellationToken));
    }

    /// <summary>
    /// Sheets of the roster: characters not loaded yet are read without tracking (classes, overrides and choices) and
    /// their equipped gear comes from <see cref="IEquippedGearProvider"/>, with one catalog load for all.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, SystemSheet>> CalculateManyAsync(IReadOnlyList<CharacterRef> references, CancellationToken cancellationToken = default)
    {
        var missing = references.Where(r => r.System is not Dnd5eCharacter).Select(r => r.Id).ToList();
        if (missing.Count > 0)
        {
            var loaded = (await characters.ListByIdsAsync(missing, cancellationToken)).ToDictionary(c => c.Id);
            foreach (var reference in references.Where(r => r.System is not Dnd5eCharacter))
            {
                reference.System = loaded.GetValueOrDefault(reference.Id) ?? throw CharacterErrors.CharacterNotFound();
            }
        }

        var list = references.Select(r => (Dnd5eCharacter)r.System!).ToList();
        var sheetCatalog = await SheetCatalog.LoadAsync(catalog, list, includeSpells: false, cancellationToken);
        var gear = await gearProvider.GetAsync(list.Select(c => c.Id).ToList(), cancellationToken);
        return list.ToDictionary(
            c => c.Id,
            c => (SystemSheet)new Dnd5eSystemSheet(
                c,
                SheetCalculator.Calculate(sheetCatalog.InputFor(c, gear.GetValueOrDefault(c.Id) ?? EquippedGear.None)),
                sheetCatalog));
    }

    public async Task<SystemSheet> RecalculateAsync(CharacterRef character, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        return new Dnd5eSystemSheet(loaded, await sheets.RecalculateAsync(loaded, cancellationToken));
    }

    public async Task<JsonObject> ValidateEditAsync(CharacterRef character, JsonElement patch, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var parsed = await ParseAsync(patch, cancellationToken);
        await sheets.EnsureCatalogReferencesAsync(loaded, parsed.ToSheetEdit(), cancellationToken);
        return JsonNode.Parse(SheetPatchJson.Serialize(parsed)) as JsonObject ?? [];
    }

    public async Task ApplyEditAsync(CharacterRef character, JsonElement patch, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await ApplyAsync(character, await ParseAsync(patch, cancellationToken), keepSpellPreparation: false, now, cancellationToken);

    /// <summary>
    /// Applies a checked patch and recalculates the sheet. <paramref name="keepSpellPreparation"/>: edits approved from
    /// a change request come from the owner of an active character, who cannot prepare spells this way.
    /// </summary>
    public async Task ApplyAsync(CharacterRef character, SheetPatch patch, bool keepSpellPreparation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var edit = patch.ToSheetEdit() with { KeepSpellPreparation = keepSpellPreparation };
        await sheets.EnsureCatalogReferencesAsync(loaded, edit, cancellationToken);
        loaded.ApplySheetEdit(edit, now);
        await sheets.RecalculateAsync(loaded, cancellationToken);
    }

    public async Task<JsonObject> SnapshotAsync(CharacterRef character, JsonElement patch, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        var parsed = Parse(patch);
        return JsonNode.Parse(SheetPatchJson.Serialize(SheetPatchSnapshot.Before(loaded, parsed))) as JsonObject ?? [];
    }

    public async Task<JsonObject> BuildDetailAsync(CharacterRef character, CancellationToken cancellationToken = default)
    {
        var loaded = await parts.LoadAsync(character, cancellationToken);
        return Dnd5eCharacterParts.ToJsonObject(await sheets.BuildSystemDetailAsync(loaded, cancellationToken));
    }

    public RosterLine BuildRosterLine(CharacterRef character, SystemSheet sheet, bool showHitPoints)
    {
        var calculated = (Dnd5eSystemSheet)sheet;
        var c = calculated.Character;
        var sheetCatalog = calculated.Catalog ?? throw new InvalidOperationException("The roster sheet carries its catalog.");
        var line = new Dnd5eRosterLineDto(
            sheetCatalog.Race(c.RaceIndex)?.Name,
            c.OrderedClasses
                .Select(k => new CharacterClassSummaryDto(
                    k.ClassIndex,
                    sheetCatalog.Class(k.ClassIndex)?.Name ?? k.ClassIndex,
                    sheetCatalog.Subclass(k.SubclassIndex)?.Name,
                    k.Level))
                .ToList(),
            c.TotalLevel,
            showHitPoints ? c.HitPointsCurrent : null,
            showHitPoints ? calculated.Sheet.HitPointsMax : null);
        return new RosterLine(Dnd5eCharacterParts.ToJsonObject(line));
    }

    /// <summary>The patch, or a validation error ("payload" when it is not a sheet patch at all).</summary>
    public static SheetPatch Parse(JsonElement patch) =>
        SheetPatchJson.TryDeserialize(patch.GetRawText())
            ?? throw AppException.Validation("payload", "El contenido de la solicitud no es una edición de hoja válida.");

    /// <summary>
    /// The patch checked with <see cref="SheetPatchValidator"/>; the first error is reported under its field, or under
    /// <paramref name="errorField"/> when given.
    /// </summary>
    public async Task<SheetPatch> ParseAsync(JsonElement patch, CancellationToken cancellationToken, string? errorField = null)
    {
        var parsed = Parse(patch);
        var validation = await patchValidator.ValidateAsync(parsed, cancellationToken);
        if (!validation.IsValid)
        {
            var error = validation.Errors[0];
            throw AppException.Validation(errorField ?? JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName), error.ErrorMessage);
        }

        return parsed;
    }
}
