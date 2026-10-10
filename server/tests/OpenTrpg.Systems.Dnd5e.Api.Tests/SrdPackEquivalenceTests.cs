using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OpenTrpg.Core.Api.Tests;
using OpenTrpg.Core.Domain.Catalog;
using OpenTrpg.Core.Infrastructure.Persistence;
using OpenTrpg.Systems.Dnd5e.Domain.Catalog;

namespace OpenTrpg.Systems.Dnd5e.Api.Tests;

/// <summary>Factory of its own that loads the SRD at startup and nothing else (no pack is ever imported into it).</summary>
public sealed class SrdOnlyApiFactory : ApiFactory
{
    protected override bool SeedCatalog => true;
}

/// <summary>
/// Equivalence of the SRD catalog with the capture taken before the SRD became a content pack (phase 34C,
/// <c>Fixtures/srd-before-34c.json</c>): the row count of every catalog table, a sample of complete definitions (a class
/// with its 20 levels, features and level choices, a subclass, a race with its subrace and traits, a spell, items, a
/// background, a creature, a condition, vocabularies, an option set and a skill) and a digest of every SRD row. JSON
/// columns are compared by meaning: keys sorted, and null, false, empty arrays and empty objects left out (the parsers
/// read them as absent). The few rows the pack changes on purpose are listed, with the reason, in
/// <c>SrdSnapshot.AcceptedDifferences</c>. Set <c>UPDATE_API_FIXTURES=1</c> to rewrite the fixture.
/// </summary>
public sealed class SrdPackEquivalenceTests(SrdOnlyApiFactory factory) : IClassFixture<SrdOnlyApiFactory>
{
    private const string FixtureName = "srd-before-34c.json";

    [Fact]
    public async Task The_srd_catalog_matches_the_capture_taken_before_the_pack()
    {
        factory.CreateClient().Dispose();
        JsonObject actual = null!;
        await factory.WithDbAsync(db =>
        {
            actual = SrdSnapshot.Capture(db);
            return Task.CompletedTask;
        });

        var fixturePath = Path.GetFullPath(Path.Combine(SourceDirectory(), "Fixtures", FixtureName));
        if (Environment.GetEnvironmentVariable("UPDATE_API_FIXTURES") == "1" || !File.Exists(fixturePath))
        {
            await File.WriteAllTextAsync(fixturePath, actual.ToJsonString(SrdSnapshot.Indented) + "\n");
            return;
        }

        var expected = JsonNode.Parse(await File.ReadAllTextAsync(fixturePath))!.AsObject();
        var differences = SrdSnapshot.Compare(expected, actual);
        if (differences.Count > 0)
        {
            var actualPath = Path.ChangeExtension(fixturePath, ".actual.json");
            await File.WriteAllTextAsync(actualPath, actual.ToJsonString(SrdSnapshot.Indented) + "\n");
            Assert.Fail($"The SRD catalog differs from {FixtureName} ({differences.Count} differences; actual written to {actualPath}):\n"
                + string.Join("\n", differences.Take(60)));
        }
    }

    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}

/// <summary>Snapshot of the SRD rows of the catalog tables (see <see cref="SrdPackEquivalenceTests"/>).</summary>
internal static class SrdSnapshot
{
    public static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly JsonSerializerOptions ValueOptions = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Columns of the item templates that change with every import (or are not part of the catalog data).</summary>
    private static readonly HashSet<string> ItemColumnsLeftOut = ["Id", "CreatedAt", "UpdatedAt", "CampaignId"];

    // Differences accepted on purpose when the SRD became a pack (phase 34C): row -> (digest before, digest now, why).
    private const string AsiRogue = "The rogue gets an ability score improvement at levels 4, 8, 10, 12, 16 and 19: the old "
        + "dataset counted them wrong from level 11 on; the pack takes them from the AsiOrFeat rules of level-choices.json.";

    private const string SupremeHealing = "Supreme Healing is a feature of the Life Domain (life-17 lists it); the old dataset "
        + "left it without subclass.";

    private const string ResourceMax = "The pack importer writes the maximum of a resource as the formula text (\"1\" "
        + "instead of 1); LevelChoiceJson.ParseResource reads both the same.";

    private const string Trimmed = "The pack importer trims the paragraphs and drops the empty ones (trailing spaces "
        + "and an empty paragraph of the old dataset).";

    private static readonly Dictionary<string, (string Before, string Now, string Why)> AcceptedDifferences = new(StringComparer.Ordinal)
    {
        ["ClassLevel/rogue-11"] = ("412dfa31598b8990", "b6e7c4543134a5cf", AsiRogue),
        ["ClassLevel/rogue-13"] = ("82444e86bb4b6c4c", "b9a2d2574ff3cd88", AsiRogue),
        ["ClassLevel/rogue-14"] = ("0f2ad91859fbf11a", "37a08c09b57d5b9b", AsiRogue),
        ["ClassLevel/rogue-15"] = ("c84d394459c8bd50", "055ba142e48a5768", AsiRogue),
        ["ClassLevel/rogue-17"] = ("455f4a7407ccfa84", "c464f079a80ea9e5", AsiRogue),
        ["ClassLevel/rogue-18"] = ("98a69d1ec8cc59ad", "b71dbdd2d315d222", AsiRogue),
        ["ClassLevel/rogue-20"] = ("8f147a1fb346ec67", "ab8e9f1511308a55", AsiRogue),
        ["FeatureDefinition/bardic-inspiration-d10"] = ("43750690a5c29858", "d01500e159a2fa16", Trimmed),
        ["FeatureDefinition/bardic-inspiration-d12"] = ("0757ecc66882d78f", "c0998a7f4ee31046", Trimmed),
        ["FeatureDefinition/bardic-inspiration-d6"] = ("0891df495c19fb3f", "897be92ce2a99255", Trimmed),
        ["FeatureDefinition/bardic-inspiration-d8"] = ("b59b2b0dccc47886", "552f1fd52582f78a", Trimmed),
        ["FeatureDefinition/flexible-casting-creating-spell-slots"] = ("92125fa78fc81f2f", "5471163a11e32a74", Trimmed),
        ["FeatureDefinition/intimidating-presence"] = ("178aecebbad423cd", "a6a0233c2b2896f3", Trimmed),
        ["FeatureDefinition/magical-secrets-1"] = ("5b1cf43d49d76383", "470aa58db6403adf", Trimmed),
        ["FeatureDefinition/magical-secrets-2"] = ("e7d4e32c5ac5b082", "804265cea83a2c0a", Trimmed),
        ["FeatureDefinition/magical-secrets-3"] = ("332acbcf20f930a4", "7fbafa3a1f93cb70", Trimmed),
        ["FeatureDefinition/song-of-rest-d10"] = ("5789c12925eea353", "e6247c4b83abf551", Trimmed),
        ["FeatureDefinition/song-of-rest-d12"] = ("9d9c3ee7669daf51", "7366e00d5dc5fe5d", Trimmed),
        ["FeatureDefinition/song-of-rest-d6"] = ("e1a28386dfdd23a8", "b2fcd6c607d6f24f", Trimmed),
        ["FeatureDefinition/song-of-rest-d8"] = ("aad7427f4754980d", "ae4d217e753cbe17", Trimmed),
        ["FeatureDefinition/supreme-healing"] = ("d69ee3e64b85b7e6", "bba5d716b3881205", SupremeHealing),
        ["OptionDefinition/eldritch-invocation-bewitching-whispers"] = ("76195246c17475f2", "bd0c90d2272b6449", ResourceMax),
        ["OptionDefinition/eldritch-invocation-dreadful-word"] = ("3d3a909099786764", "73df5bd6b38eb745", ResourceMax),
        ["OptionDefinition/eldritch-invocation-minions-of-chaos"] = ("ea57901d45d1a1fc", "b397d1ece6c2d3a1", ResourceMax),
        ["OptionDefinition/eldritch-invocation-mire-the-mind"] = ("43c06089067b77ee", "267a4a28b6eb088e", ResourceMax),
        ["OptionDefinition/eldritch-invocation-sculptor-of-flesh"] = ("8718f0e6ffe7c170", "53c1341e247c633c", ResourceMax),
        ["OptionDefinition/eldritch-invocation-sign-of-ill-omen"] = ("454aff1a8e46dc8c", "f4bbbd87ae4f2ed3", ResourceMax),
        ["OptionDefinition/eldritch-invocation-thief-of-five-fates"] = ("1c44cd80db01c0ef", "7891aa84039d0f4f", ResourceMax),
        ["SpellDefinition/scrying"] = ("b30724c548a8ad23", "ab0b46e8742b4776", Trimmed),
    };

    /// <summary>Complete definitions kept in the fixture, by table.</summary>
    private static readonly Dictionary<string, Func<JsonObject, bool>> Samples = new(StringComparer.Ordinal)
    {
        [nameof(ClassDefinition)] = r => Is(r, "Index", "wizard"),
        [nameof(ClassLevel)] = r => Is(r, "ClassIndex", "wizard"),
        [nameof(FeatureDefinition)] = r => Is(r, "ClassIndex", "wizard"),
        [nameof(LevelChoiceRule)] = r => Is(r, "ClassIndex", "wizard"),
        [nameof(SubclassDefinition)] = r => Is(r, "Index", "evocation"),
        [nameof(SubclassLevel)] = r => Is(r, "SubclassIndex", "evocation"),
        [nameof(RaceDefinition)] = r => Is(r, "Index", "dwarf"),
        [nameof(SubraceDefinition)] = r => Is(r, "Index", "hill-dwarf"),
        [nameof(TraitDefinition)] = r => Contains(r, "RaceIndexes", "dwarf") || Contains(r, "SubraceIndexes", "hill-dwarf"),
        [nameof(SpellDefinition)] = r => Is(r, "Index", "fireball") || Is(r, "Index", "cure-wounds"),
        [nameof(ItemTemplate)] = r => Is(r, "Index", "cloak-of-protection") || Is(r, "Index", "longsword") || Is(r, "Index", "explorers-pack") || Is(r, "Index", "arrow"),
        [nameof(BackgroundDefinition)] = r => Is(r, "Index", "acolyte"),
        [nameof(CreatureDefinition)] = r => Is(r, "Index", "wolf"),
        [nameof(ConditionDefinition)] = r => Is(r, "Index", "poisoned"),
        [nameof(ReferenceEntry)] = r => Is(r, "Kind", ReferenceEntry.WeaponProperties) || Is(r, "Index", "dwarvish"),
        [nameof(OptionSetDefinition)] = r => Is(r, "SetId", "fighting-styles"),
        [nameof(OptionDefinition)] = r => Is(r, "SetId", "fighting-styles"),
        [nameof(EquipmentCategory)] = r => Is(r, "Index", "holy-symbols"),
        [nameof(SkillDefinition)] = r => Is(r, "Index", "perception"),
    };

    public static JsonObject Capture(AppDbContext db)
    {
        var counts = new JsonObject();
        var samples = new JsonObject();
        var digests = new JsonObject();
        var dump = new JsonObject();
        foreach (var entityType in db.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace == typeof(ClassDefinition).Namespace || t.ClrType == typeof(ItemTemplate))
                     .OrderBy(t => t.ClrType.Name, StringComparer.Ordinal))
        {
            var table = entityType.ClrType.Name;
            var rows = Rows(db, entityType);
            counts[table] = rows.Count;
            if (Samples.TryGetValue(table, out var sample))
            {
                samples[table] = new JsonArray(rows.Where(r => sample(r.Row)).Select(r => (JsonNode)r.Row.DeepClone()).ToArray());
            }

            var tableDigests = new JsonObject();
            foreach (var (key, row) in rows)
            {
                tableDigests[key] = Digest(row);
            }

            digests[table] = tableDigests;
            dump[table] = new JsonObject(rows.Select(r => KeyValuePair.Create(r.Key, Canonical(r.Row))));
        }

        // Diagnostics: SRD_SNAPSHOT_DUMP=<file> writes every row in full, to diff two captures.
        if (Environment.GetEnvironmentVariable("SRD_SNAPSHOT_DUMP") is { Length: > 0 } dumpPath)
        {
            File.WriteAllText(dumpPath, dump.ToJsonString(Indented));
        }

        return new JsonObject { ["counts"] = counts, ["samples"] = samples, ["rows"] = digests };
    }

    public static List<string> Compare(JsonObject expected, JsonObject actual)
    {
        var differences = new List<string>();
        var expectedCounts = expected["counts"]!.AsObject();
        var actualCounts = actual["counts"]!.AsObject();
        foreach (var table in expectedCounts.Select(p => p.Key).Union(actualCounts.Select(p => p.Key)).Order(StringComparer.Ordinal))
        {
            var before = expectedCounts[table]?.GetValue<int>();
            var after = actualCounts[table]?.GetValue<int>();
            if (before != after)
            {
                differences.Add($"count {table}: {before?.ToString() ?? "-"} before, {after?.ToString() ?? "-"} now");
            }
        }

        var expectedSamples = expected["samples"]!.AsObject();
        var actualSamples = actual["samples"]!.AsObject();
        foreach (var (table, rows) in expectedSamples)
        {
            var before = rows!.AsArray().Select(r => Canonical(r)?.ToJsonString()).ToList();
            var after = (actualSamples[table]?.AsArray() ?? []).Select(r => Canonical(r)?.ToJsonString()).ToList();
            for (var i = 0; i < Math.Max(before.Count, after.Count); i++)
            {
                var b = i < before.Count ? before[i] : null;
                var a = i < after.Count ? after[i] : null;
                if (b != a)
                {
                    differences.Add($"sample {table}[{i}]:\n  before {b}\n  now    {a}");
                }
            }
        }

        var expectedRows = expected["rows"]!.AsObject();
        var actualRows = actual["rows"]!.AsObject();
        foreach (var (table, rows) in expectedRows)
        {
            var before = rows!.AsObject();
            var after = actualRows[table]?.AsObject() ?? [];
            foreach (var key in before.Select(p => p.Key).Union(after.Select(p => p.Key)).Order(StringComparer.Ordinal))
            {
                var b = before[key]?.GetValue<string>();
                var a = after[key]?.GetValue<string>();
                if (b != a && !(AcceptedDifferences.TryGetValue($"{table}/{key}", out var accepted) && accepted.Before == b && accepted.Now == a))
                {
                    differences.Add($"row {table}/{key}: {(b is null ? "new" : a is null ? "missing" : "changed")}");
                }
            }
        }

        return differences;
    }

    /// <summary>The SRD rows of a table, sorted by key, with the JSON columns parsed.</summary>
    private static List<(string Key, JsonObject Row)> Rows(AppDbContext db, IEntityType entityType)
    {
        var set = (IQueryable)typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(entityType.ClrType).Invoke(db, null)!;
        var key = entityType.FindPrimaryKey()!.Properties;
        var isItem = entityType.ClrType == typeof(ItemTemplate);
        var result = new List<(string Key, JsonObject Row)>();
        foreach (var entity in set.Cast<object>().ToList())
        {
            if (isItem && ((ItemTemplate)entity).CampaignId is not null)
            {
                continue;
            }

            var row = new JsonObject();
            foreach (var property in entityType.GetProperties().Where(p => !p.IsShadowProperty()).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (isItem && ItemColumnsLeftOut.Contains(property.Name))
                {
                    continue;
                }

                var value = property.PropertyInfo?.GetValue(entity) ?? property.FieldInfo?.GetValue(entity);
                row[property.Name] = value is string text && property.Name.EndsWith("Json", StringComparison.Ordinal) && TryParse(text) is { } json
                    ? json
                    : JsonSerializer.SerializeToNode(value, ValueOptions);
            }

            if (row["Source"] is { } source && source.GetValue<string>() != Dnd5eCatalogSources.Srd)
            {
                continue;
            }

            var keyText = isItem
                ? ((ItemTemplate)entity).Index ?? string.Empty
                : string.Join("/", key.Select(p => Convert.ToString(p.PropertyInfo?.GetValue(entity), System.Globalization.CultureInfo.InvariantCulture)));
            result.Add((keyText, row));
        }

        return result.OrderBy(r => r.Key, StringComparer.Ordinal).ToList();
    }

    private static JsonNode? TryParse(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Digest(JsonObject row) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(row)?.ToJsonString() ?? "null")))[..16];

    /// <summary>Keys sorted; null, false, empty arrays and empty objects left out of objects.</summary>
    private static JsonNode? Canonical(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
                var result = new JsonObject();
                foreach (var (name, value) in o.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    var canonical = Canonical(value);
                    if (canonical is null
                        || (canonical is JsonValue v && v.GetValueKind() == JsonValueKind.False)
                        || canonical is JsonArray { Count: 0 }
                        || canonical is JsonObject { Count: 0 })
                    {
                        continue;
                    }

                    result[name] = canonical;
                }

                return result;
            case JsonArray a:
                return new JsonArray(a.Select(Canonical).ToArray());
            case null:
                return null;
            default:
                return node.DeepClone();
        }
    }

    private static bool Is(JsonObject row, string property, string value) =>
        row[property] is JsonValue v && v.GetValueKind() == JsonValueKind.String && v.GetValue<string>() == value;

    private static bool Contains(JsonObject row, string property, string value) =>
        row[property] is JsonArray a && a.Any(e => e is JsonValue v && v.GetValueKind() == JsonValueKind.String && v.GetValue<string>() == value);
}
