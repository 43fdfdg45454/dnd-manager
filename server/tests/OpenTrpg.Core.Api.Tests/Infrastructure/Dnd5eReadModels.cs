using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenTrpg.Core.Application.Characters;
using OpenTrpg.Core.Application.Items;
using OpenTrpg.Core.Application.Systems;

namespace OpenTrpg.Core.Api.Tests;

/// <summary>
/// Read access, in the tests, to the D&amp;D 5e fields that travel at the same level as the core ones in the character
/// responses (<see cref="CharacterDetailDto.SystemFields"/>, <see cref="CharacterSummaryDto.SystemFields"/>).
/// </summary>
public static class Dnd5eReadModels
{
    private static readonly ConditionalWeakTable<CharacterDetailDto, Dnd5eCharacterDetailDto> Details = new();
    private static readonly ConditionalWeakTable<CharacterSummaryDto, Dnd5eRosterLineDto> Lines = new();

    public static Dnd5eCharacterDetailDto System(this CharacterDetailDto d) =>
        Details.GetValue(d, x => Read<Dnd5eCharacterDetailDto>(JsonFields.ToObject(x.SystemFields)));

    public static Dnd5eRosterLineDto System(this CharacterSummaryDto s) =>
        Lines.GetValue(s, x => Read<Dnd5eRosterLineDto>(JsonFields.ToObject(x.SystemFields)));

    public static IReadOnlyDictionary<string, int> HitDiceOf(JsonObject? payload) =>
        payload?["hitDice"]?.Deserialize<Dictionary<string, int>>(JsonSerializerOptions.Web) ?? [];

    private static T Read<T>(JsonObject? fields) =>
        (fields ?? []).Deserialize<T>(JsonSerializerOptions.Web) ?? throw new InvalidOperationException("Missing system fields.");

    extension(CharacterDetailDto d)
    {
        public string? RaceIndex => d.System().RaceIndex;
        public string? RaceName => d.System().RaceName;
        public string? SubraceIndex => d.System().SubraceIndex;
        public string? SubraceName => d.System().SubraceName;
        public string? BackgroundIndex => d.System().BackgroundIndex;
        public string? BackgroundName => d.System().BackgroundName;
        public bool RaceCatalogMissing => d.System().RaceCatalogMissing;
        public bool BackgroundCatalogMissing => d.System().BackgroundCatalogMissing;
        public bool CatalogMissing => d.System().CatalogMissing;
        public string? Alignment => d.System().Alignment;
        public bool ApplyRacialBonuses => d.System().ApplyRacialBonuses;
        public string HpMode => d.System().HpMode;
        public int BaseStr => d.System().BaseStr;
        public int BaseDex => d.System().BaseDex;
        public int BaseCon => d.System().BaseCon;
        public int BaseInt => d.System().BaseInt;
        public int BaseWis => d.System().BaseWis;
        public int BaseCha => d.System().BaseCha;
        public int HitPointsCurrent => d.System().HitPointsCurrent;
        public int TemporaryHitPoints => d.System().TemporaryHitPoints;
        public int DeathSaveSuccesses => d.System().DeathSaveSuccesses;
        public int DeathSaveFailures => d.System().DeathSaveFailures;
        public int ExhaustionLevel => d.System().ExhaustionLevel;
        public IReadOnlyList<CharacterConditionDto> Conditions => d.System().Conditions;
        public string? ConcentratingOnSpellIndex => d.System().ConcentratingOnSpellIndex;
        public bool Inspiration => d.System().Inspiration;
        public IReadOnlyDictionary<string, int> HitDiceUsed => d.System().HitDiceUsed;
        public string BackgroundDetail => d.System().BackgroundDetail;
        public IReadOnlyList<CharacterClassDto> Classes => d.System().Classes;
        public IReadOnlyList<CharacterProficiencyDto> Proficiencies => d.System().Proficiencies;
        public IReadOnlyList<CharacterSpellDto> Spells => d.System().Spells;
        public IReadOnlyList<CharacterOverrideDto> Overrides => d.System().Overrides;
        public IReadOnlyList<CharacterResourceDto> Resources => d.System().Resources;
        public IReadOnlyList<SpellSlotDto> SpellSlots => d.System().SpellSlots;
        public CharacterSheetDto Sheet => d.System().Sheet;
        public InventoryDto Inventory => d.System().Inventory;
        public CombatSummaryDto Combat => d.System().Combat;
        public PendingRestDto? PendingRest => d.System().PendingRest;
        public int? PendingLevelUpTo => d.System().PendingLevelUpTo;
        public bool SpellPreparationPending => d.System().SpellPreparationPending;
        public string? SpellPreparationReason => d.System().SpellPreparationReason;
        public IReadOnlyList<InvalidChoiceDto> InvalidChoices => d.System().InvalidChoices;
        public bool RestRollsPending => d.System().RestRollsPending;
        public IReadOnlyList<CharacterChoiceDto> Choices => d.System().Choices;
        public IReadOnlyList<CharacterFeatDto> Feats => d.System().Feats;
        public IReadOnlyList<CharacterOptionCostDto> OptionCosts => d.System().OptionCosts;
        public CharacterCompanionDto? Companion => d.System().Companion;
        public CompanionFeatureDto? CompanionFeature => d.System().CompanionFeature;
        public bool CompanionPending => d.System().CompanionPending;
    }

    extension(CharacterSummaryDto s)
    {
        public string? RaceName => s.System().RaceName;
        public IReadOnlyList<CharacterClassSummaryDto> Classes => s.System().Classes;
        public int Level => s.System().Level;
        public int? HitPointsCurrent => s.System().HitPointsCurrent;
        public int? HitPointsMax => s.System().HitPointsMax;
    }

    extension(RestRequestDto r)
    {
        public IReadOnlyDictionary<string, int> HitDice => HitDiceOf(JsonFields.ToObject(r.Payload));
    }
}
