namespace Dnd.Application.Common;

/// <summary>
/// Strict parsing of enum names received by the API: exact, case-sensitive names only (numbers and
/// undefined values are rejected, unlike <see cref="Enum.TryParse{TEnum}(string?, out TEnum)"/>).
/// </summary>
public static class EnumNames
{
    public static bool TryParse<T>(string? value, out T result)
        where T : struct, Enum
    {
        result = default;
        return value is not null
            && Enum.GetNames<T>().Contains(value, StringComparer.Ordinal)
            && Enum.TryParse(value, ignoreCase: false, out result);
    }

    public static bool IsValid<T>(string? value)
        where T : struct, Enum => TryParse<T>(value, out _);

    /// <summary>Parses a value already accepted by a validator.</summary>
    public static T Parse<T>(string value)
        where T : struct, Enum =>
        TryParse<T>(value, out var result) ? result : throw new ArgumentOutOfRangeException(nameof(value), value, null);

    /// <summary>Accepted names, for validation messages: "\"A\", \"B\" o \"C\"".</summary>
    public static string Describe<T>()
        where T : struct, Enum
    {
        var names = Enum.GetNames<T>().Select(n => $"\"{n}\"").ToList();
        return names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} o {names[^1]}";
    }
}
