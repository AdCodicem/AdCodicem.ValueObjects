using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdCodicem.ValueObjects.Json;

/// <summary>
/// Serializes a value object as its bare underlying value, delegating the underlying value to
/// System.Text.Json itself.
/// </summary>
/// <typeparam name="TSelf">Value object type.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// This is the general-purpose converter, used for value objects written by hand. A generated value object
/// carries its own converter, which writes the value directly instead of going back through the serializer, and
/// is the one the factory hands out whenever it is available.
/// </remarks>
[RequiresUnreferencedCode("Delegating the underlying value to the serializer needs its metadata, which trimming may remove. Generated value objects carry their own converter and do not go through this one.")]
[RequiresDynamicCode("Delegating the underlying value to the serializer may need run-time code generation. Generated value objects carry their own converter and do not go through this one.")]
public sealed class ValueObjectJsonConverter<TSelf, TValue> : JsonConverter<TSelf>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <inheritdoc />
    public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<TValue>(ref reader, options)!;

        if (!TSelf.TryCreate(value, out var result, out var validation))
        {
            throw new JsonException($"The value is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}");
        }

        return result;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value.Value, options);

    /// <inheritdoc />
    /// <remarks>
    /// The key is read as System.Text.Json reads a key of <typeparamref name="TValue"/>, then validated through
    /// <c>TryCreate</c>, as a value is: the reverse of <see cref="WriteAsPropertyName"/>, whatever text the value
    /// object's own parser reads. A key that is not one of <typeparamref name="TValue"/> is refused as it is in a
    /// dictionary of its own, and a key the value object rejects with a <see cref="JsonException"/> naming the rule.
    /// </remarks>
    public override TSelf ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = GetValueConverter(options).ReadAsPropertyName(ref reader, typeof(TValue), options);

        if (!TSelf.TryCreate(value, out var result, out var validation))
        {
            throw new JsonException($"The dictionary key is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}");
        }

        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The key is the underlying value, written as System.Text.Json writes a key of <typeparamref name="TValue"/>:
    /// the form the value itself travels in. The value object's own formatting, which may print something its
    /// parser does not read, never reaches the wire. An underlying type System.Text.Json cannot write as a key is
    /// refused as it is in a dictionary of its own, with a <see cref="NotSupportedException"/>.
    /// </remarks>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
        => GetValueConverter(options).WriteAsPropertyName(writer, value.Value!, options);

    private static JsonConverter<TValue> GetValueConverter(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return (JsonConverter<TValue>)options.GetConverter(typeof(TValue));
    }
}
