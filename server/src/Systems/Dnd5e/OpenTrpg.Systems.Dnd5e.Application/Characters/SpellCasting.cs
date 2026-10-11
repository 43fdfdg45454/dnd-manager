using FluentValidation;
using OpenTrpg.Core.Application.Abstractions;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Common;
using OpenTrpg.Systems.Dnd5e.Application.Abstractions.Persistence;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;
using OpenTrpg.Systems.Dnd5e.Domain.Characters;

namespace OpenTrpg.Systems.Dnd5e.Application.Characters;

/// <summary>
/// Body of <c>POST …/spells/{spellIndex}/cast</c>. <see cref="ClassIndex"/>: the entry of the spell that is cast when the
/// character has it from several classes or origins (null: the first one). <see cref="Level"/>: the slot level (null: the
/// spell level; ignored for cantrips, rituals and spells paid with a use). <see cref="Ritual"/>: cast as a ritual, which
/// spends nothing.
/// </summary>
public sealed record CastSpellRequest(string? ClassIndex = null, int? Level = null, bool Ritual = false);

public sealed class CastSpellRequestValidator : AbstractValidator<CastSpellRequest>
{
    public CastSpellRequestValidator()
    {
        RuleFor(x => x.Level).InclusiveBetween(1, 9).When(x => x.Level is not null)
            .WithMessage("El nivel de lanzamiento debe estar entre 1 y 9.");
    }
}

/// <summary>Values of <see cref="SpellCastResultDto.Cost"/>.</summary>
public static class SpellCastCosts
{
    /// <summary>A cantrip: nothing is spent.</summary>
    public const string Cantrip = "cantrip";

    /// <summary>Cast as a ritual: nothing is spent.</summary>
    public const string Ritual = "ritual";

    /// <summary>One use of the resource of the spell (granted with uses per long rest).</summary>
    public const string Use = "use";

    /// <summary>A spell slot of <see cref="SpellCastResultDto.SlotLevel"/>.</summary>
    public const string Slot = "slot";

    /// <summary>A pact magic slot (of <see cref="SpellCastResultDto.SlotLevel"/>).</summary>
    public const string PactSlot = "pactSlot";

    /// <summary>A use of the class resource and <see cref="SpellCastResultDto.HitPointsLost"/> hit points.</summary>
    public const string HitPoints = "hitPoints";
}

/// <param name="Character">The character after the cast.</param>
/// <param name="Cost">What the cast spent (<see cref="SpellCastCosts"/>).</param>
/// <param name="SlotLevel">Level the spell was cast at (null for cantrips).</param>
/// <param name="UsesLeft">Uses left of the spell's resource when <see cref="Cost"/> is "use".</param>
/// <param name="HitPointsLost">Hit points the cast cost when <see cref="Cost"/> is "hitPoints".</param>
public sealed record SpellCastResultDto(CharacterDetailDto Character, string Cost, int? SlotLevel, int? UsesLeft, int? HitPointsLost);

/// <summary>
/// Casts a spell the character has and decides what it costs (owner or DM, without approval, like any combat tracking):
/// a ritual or a cantrip spends nothing; a spell granted with uses per long rest spends a use of its resource
/// (<c>&lt;class index of the spell&gt;.&lt;spell&gt;</c>); a class that casts with hit points pays with its resource and
/// hit points; any other spell spends a slot of the chosen level, or a pact slot of that level. 400 when there is nothing
/// to pay with.
/// </summary>
public sealed class CastSpellHandler(CharacterTracker tracker, ICatalogRepository catalog, IDateTimeProvider clock)
{
    public async Task<SpellCastResultDto> HandleAsync(Guid currentUserId, Guid characterId, string spellIndex, CastSpellRequest? request, CancellationToken cancellationToken = default)
    {
        request ??= new CastSpellRequest();
        var character = await tracker.LoadAsync(currentUserId, characterId, cancellationToken);
        var entries = character.Spells.Where(s => s.SpellIndex == spellIndex).ToList();
        if (entries.Count == 0)
        {
            throw AppException.Validation("spellIndex", "El personaje no tiene ese conjuro.");
        }

        var definition = await catalog.GetSpellAsync(spellIndex, cancellationToken)
            ?? throw AppException.Validation("spellIndex", $"El conjuro '{spellIndex}' no existe en el catálogo.");
        string ResourceKey(CharacterSpell s) => $"{s.ClassIndex}.{spellIndex}";
        CharacterResource? UsesOf(CharacterSpell s) =>
            character.Resources.FirstOrDefault(r => r.IsAuto && r.Key == ResourceKey(s));

        var entry = request.ClassIndex is { } classIndex
            ? entries.FirstOrDefault(s => s.ClassIndex == classIndex)
                ?? throw AppException.Validation("classIndex", "El personaje no tiene ese conjuro de esa clase u origen.")
            : entries.FirstOrDefault(s => UsesOf(s) is not null) ?? entries[0];
        var sheet = await tracker.SheetAsync(character, cancellationToken);
        var casting = sheet.Spellcasting.FirstOrDefault(s => s.ClassIndex == entry.ClassIndex);
        var now = clock.UtcNow;

        if (request.Ritual)
        {
            var classInfo = (await catalog.GetClassAsync(entry.ClassIndex, cancellationToken))?.Spellcasting;
            var ritual = classInfo?.CastsAsRitual(spellIndex, definition.Ritual) ?? definition.Ritual;
            if (!ritual)
            {
                throw AppException.Validation("ritual", $"{definition.Name} no se puede lanzar como ritual.");
            }

            return await ResultAsync(SpellCastCosts.Ritual, definition.Level == 0 ? null : definition.Level);
        }

        if (definition.Level == 0)
        {
            return await ResultAsync(SpellCastCosts.Cantrip, null);
        }

        if (UsesOf(entry) is { } uses)
        {
            if (uses.Used >= uses.Max)
            {
                throw AppException.Validation("spellIndex", $"Sin usos de {definition.Name} hasta el descanso largo.");
            }

            character.SpendResource(uses.Id, 1, now);
            return await ResultAsync(SpellCastCosts.Use, definition.Level, usesLeft: uses.Max - uses.Used);
        }

        var level = request.Level ?? definition.Level;
        if (level < definition.Level)
        {
            throw AppException.Validation("level", $"{definition.Name} es de nivel {definition.Level}: no se puede lanzar a nivel {level}.");
        }

        if (casting?.Progression == ClassSpellcastingInfo.RitualsOnly)
        {
            throw AppException.Validation("ritual", $"Esta clase solo lanza conjuros como rituales.");
        }

        if (casting?.Progression == ClassSpellcastingInfo.HitPoints)
        {
            if (level > casting.MaxSpellLevel || level > casting.HitPointCost.Count)
            {
                throw AppException.Validation("level", $"Con tu nivel solo lanzas conjuros hasta nivel {casting.MaxSpellLevel}.");
            }

            var resource = character.Resources.FirstOrDefault(r => r.IsAuto && r.Key == casting.UsesResource);
            var lost = character.CastWithHitPoints(casting.UsesResource!, resource?.Name ?? casting.UsesResource!, casting.HitPointCost[level - 1], now);
            return await ResultAsync(SpellCastCosts.HitPoints, level, hitPointsLost: lost);
        }

        if (character.SpellSlotsUsed(level) < sheet.SpellSlotMax(level))
        {
            character.SpendSpellSlot(level, 1, sheet.SpellSlotMax(level), now);
            return await ResultAsync(SpellCastCosts.Slot, level);
        }

        if (sheet.PactMagic is { } pact && pact.SlotLevel == level
            && character.SpellSlotsUsed(SpellSlotState.PactLevel) < pact.Slots)
        {
            character.SpendSpellSlot(SpellSlotState.PactLevel, 1, pact.Slots, now);
            return await ResultAsync(SpellCastCosts.PactSlot, level);
        }

        throw AppException.Validation("level", $"No quedan espacios de nivel {level}.");

        async Task<SpellCastResultDto> ResultAsync(string cost, int? slotLevel, int? usesLeft = null, int? hitPointsLost = null) =>
            new(await tracker.SaveAsync(character, cancellationToken), cost, slotLevel, usesLeft, hitPointsLost);
    }
}
