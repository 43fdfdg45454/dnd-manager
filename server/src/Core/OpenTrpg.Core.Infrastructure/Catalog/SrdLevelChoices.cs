using System.Text.Json;
using OpenTrpg.Core.Domain.Catalog;

namespace OpenTrpg.Core.Infrastructure.Catalog;

/// <summary>Level choice catalog rows of the SRD, ready to be inserted.</summary>
internal sealed record LevelChoiceCatalog(
    IReadOnlyList<OptionSetDefinition> Sets,
    IReadOnlyList<OptionDefinition> Options,
    IReadOnlyList<LevelChoiceRule> Rules);

/// <summary>
/// Reads the hand-written SRD level choice files embedded from <c>server/seed/srd</c>
/// (<c>option-sets.json</c> and <c>level-choices.json</c>, described in <c>README-level-choices.md</c>).
/// The JSON sub-objects (prerequisites, modifiers, grants, resource, filter...) are stored as they are.
/// </summary>
internal static class SrdLevelChoices
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static LevelChoiceCatalog Load()
    {
        var sets = Read<OptionSetsFile>("option-sets.json").Sets ?? [];
        var rules = Read<RulesFile>("level-choices.json").Rules ?? [];

        return new LevelChoiceCatalog(
            sets.Select(s => new OptionSetDefinition { SetId = s.SetId!, Name = s.Name ?? s.SetId!, Source = Dnd5eCatalogSources.Srd }).ToList(),
            sets.SelectMany(s => (s.Options ?? []).Select(o => MapOption(s.SetId!, o))).ToList(),
            rules.Select(MapRule).ToList());
    }

    /// <summary>Raw JSON of an optional sub-object; null for missing or JSON null values.</summary>
    public static string? Raw(JsonElement? element) =>
        element is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } value ? value.GetRawText() : null;

    private static OptionDefinition MapOption(string setId, OptionJson o) => new()
    {
        Index = o.Index!,
        SetId = setId,
        Name = o.Name ?? o.Index!,
        Description = o.Description ?? [],
        PrerequisitesText = o.PrerequisitesText,
        PrerequisitesJson = Raw(o.Prerequisites),
        ModifiersJson = Raw(o.Modifiers) ?? "[]",
        AbilityIncreaseJson = Raw(o.AbilityIncrease),
        GrantsJson = Raw(o.Grants),
        ResourceJson = Raw(o.Resource),
        Source = Dnd5eCatalogSources.Srd,
    };

    private static LevelChoiceRule MapRule(RuleJson r) => new()
    {
        Id = LevelChoiceRule.IdFor(r.ClassIndex!, r.SubclassIndex, r.Level ?? 0, r.Key!),
        ClassIndex = r.ClassIndex!,
        SubclassIndex = r.SubclassIndex,
        Level = r.Level ?? 0,
        Key = r.Key!,
        Name = r.Name ?? r.Key!,
        Kind = Enum.Parse<LevelChoiceKind>(r.Kind ?? nameof(LevelChoiceKind.Custom), ignoreCase: true),
        SetId = r.SetId,
        Choose = r.Choose ?? 0,
        FromJson = Raw(r.From),
        FilterJson = Raw(r.Filter),
        Replaces = r.Replaces ?? false,
        Cumulative = r.Cumulative ?? false,
        Note = r.Note ?? string.Empty,
        Source = Dnd5eCatalogSources.Srd,
    };

    private static T Read<T>(string fileName)
        where T : new()
    {
        var assembly = typeof(SrdLevelChoices).Assembly;
        var resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith($".{fileName}", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"SRD level choice file '{fileName}' is not embedded in {assembly.GetName().Name}.");

        using var stream = assembly.GetManifestResourceStream(resource)!;
        return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? new T();
    }

    private sealed class OptionSetsFile
    {
        public List<SetJson>? Sets { get; set; }
    }

    private sealed class SetJson
    {
        public string? SetId { get; set; }

        public string? Name { get; set; }

        public List<OptionJson>? Options { get; set; }
    }

    private sealed class OptionJson
    {
        public string? Index { get; set; }

        public string? Name { get; set; }

        public List<string>? Description { get; set; }

        public string? PrerequisitesText { get; set; }

        public JsonElement? Prerequisites { get; set; }

        public JsonElement? Modifiers { get; set; }

        public JsonElement? AbilityIncrease { get; set; }

        public JsonElement? Grants { get; set; }

        public JsonElement? Resource { get; set; }
    }

    private sealed class RulesFile
    {
        public List<RuleJson>? Rules { get; set; }
    }

    private sealed class RuleJson
    {
        public string? ClassIndex { get; set; }

        public string? SubclassIndex { get; set; }

        public int? Level { get; set; }

        public string? Key { get; set; }

        public string? Name { get; set; }

        public string? Kind { get; set; }

        public string? SetId { get; set; }

        public int? Choose { get; set; }

        public JsonElement? From { get; set; }

        public bool? Replaces { get; set; }

        public bool? Cumulative { get; set; }

        public string? Note { get; set; }

        public JsonElement? Filter { get; set; }
    }
}
