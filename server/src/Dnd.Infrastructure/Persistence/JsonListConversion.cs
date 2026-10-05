using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dnd.Infrastructure.Persistence;

/// <summary>
/// Stores a list as a JSON array in a text column. Used instead of provider-specific array or
/// JSON types so the same model works on PostgreSQL and SQLite.
/// </summary>
internal static class JsonListConversion
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General);

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

    private static T[] Deserialize<T>(string json) =>
        string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<T[]>(json, Options) ?? [];
}
