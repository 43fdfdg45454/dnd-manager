namespace Dnd.Domain.Common;

/// <summary>
/// Small key/value setting of the instance that the server learns by itself (not operator configuration),
/// for example the public origin last seen through the reverse proxy.
/// </summary>
public sealed class InstanceSetting
{
    public const int KeyMaxLength = 64;
    public const int ValueMaxLength = 512;

    /// <summary>Key of the last public origin (<c>scheme://host[:port]</c>) a request arrived through.</summary>
    public const string PublicOriginKey = "public-origin";

    private InstanceSetting()
    {
    }

    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    public static InstanceSetting Create(string key, string value, DateTimeOffset now) =>
        new() { Key = key, Value = value, UpdatedAt = now };

    public void Update(string value, DateTimeOffset now)
    {
        Value = value;
        UpdatedAt = now;
    }
}
