using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace OpenTrpg.Core.Infrastructure.Persistence;

/// <summary>
/// Stores a list as a JSON array in a text column. Used instead of provider-specific array or
/// JSON types so the same model works on PostgreSQL and SQLite. Enums are stored by name.
/// </summary>
public static class JsonListConversion
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static PropertyBuilder<IReadOnlyList<T>> HasJsonListConversion<T>(this PropertyBuilder<IReadOnlyList<T>> builder)
    {
        var converter = new ValueConverter<IReadOnlyList<T>, string>(
            list => JsonSerializer.Serialize(list, Options),
            json => Deserialize<T>(json));

        var comparer = new ValueComparer<IReadOnlyList<T>>(
            (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
            list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item == null ? 0 : item.GetHashCode())),
            list => list.ToArray());

        return builder.HasConversion(converter, comparer).IsRequired();
    }

    /// <summary>Optional list: null is stored as SQL NULL (distinct from an empty array).</summary>
    public static PropertyBuilder<IReadOnlyList<T>?> HasNullableJsonListConversion<T>(this PropertyBuilder<IReadOnlyList<T>?> builder)
    {
        // Value converters are not invoked for nulls: the database NULL maps straight to a null list.
        var converter = new ValueConverter<IReadOnlyList<T>?, string?>(
            list => JsonSerializer.Serialize(list, Options),
            json => Deserialize<T>(json!));

        var comparer = new ValueComparer<IReadOnlyList<T>?>(
            (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
            list => list == null ? 0 : list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item == null ? 0 : item.GetHashCode())),
            list => list == null ? null : list.ToArray());

        return builder.HasConversion(converter, comparer).IsRequired(false);
    }

    private static T[] Deserialize<T>(string json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<T[]>(json, Options) ?? [];
}
