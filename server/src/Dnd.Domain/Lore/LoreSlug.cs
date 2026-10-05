using System.Globalization;
using System.Text;

namespace Dnd.Domain.Lore;

/// <summary>Slugs of lore entries: lowercase ASCII letters and digits separated by single hyphens.</summary>
public static class LoreSlug
{
    public const int MaxLength = 100;
    public const string Fallback = "entrada";

    /// <summary>Slug of a title: accents removed, anything else that is not a letter or digit becomes a hyphen.</summary>
    public static string From(string title)
    {
        var builder = new StringBuilder();
        var pendingHyphen = false;
        foreach (var ch in (title ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            {
                if (pendingHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                }

                pendingHyphen = false;
                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                pendingHyphen = true;
            }
        }

        var slug = builder.ToString();
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }

        return slug.Length == 0 ? Fallback : slug;
    }

    /// <summary>The slug for the n-th attempt: <c>base</c>, <c>base-2</c>, <c>base-3</c>... (always within <see cref="MaxLength"/>).</summary>
    public static string Attempt(string baseSlug, int attempt)
    {
        if (attempt <= 1)
        {
            return baseSlug;
        }

        var suffix = "-" + attempt.ToString(CultureInfo.InvariantCulture);
        var head = baseSlug.Length + suffix.Length > MaxLength ? baseSlug[..(MaxLength - suffix.Length)].TrimEnd('-') : baseSlug;
        return head + suffix;
    }
}
