using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dnd.Application.Common;

/// <summary>
/// A request field that distinguishes "absent" (<see cref="IsSet"/> false) from an explicit value,
/// including an explicit JSON <c>null</c>. Absent properties are never visited by the serializer, so
/// they keep the default (unset) value. Declare the property with
/// <c>[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]</c> so an unset value is also
/// omitted when the request is serialized again (e.g. as a change request payload).
/// </summary>
[JsonConverter(typeof(OptionalJsonConverterFactory))]
public readonly record struct Optional<T>
{
    public Optional(T value)
    {
        IsSet = true;
        Value = value;
    }

    /// <summary>True when the property was present in the JSON (even as <c>null</c>).</summary>
    public bool IsSet { get; }

    /// <summary>The value when <see cref="IsSet"/>; default otherwise.</summary>
    public T Value { get; }

    public static implicit operator Optional<T>(T value) => new(value);
}

/// <summary>Creates the converter of each <see cref="Optional{T}"/> type.</summary>
public sealed class OptionalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Optional<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(OptionalJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;

    private sealed class OptionalJsonConverter<T> : JsonConverter<Optional<T>>
    {
        public override bool HandleNull => true;

        public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(JsonSerializer.Deserialize<T>(ref reader, options)!);

        public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
        {
            if (value.IsSet)
            {
                JsonSerializer.Serialize(writer, value.Value, options);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}
