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
/// <para>
/// This is the general-purpose converter, used for value objects written by hand. A generated value object
/// carries its own converter, which writes the value directly instead of going back through the serializer, and
/// is the one the factory hands out whenever it is available.
/// </para>
/// <para>
/// A value or a key the value object rejects, and a value to write that it rejects, are refused with a
/// <see cref="ValueObjectJsonException"/> carrying the code of the rule, as the generated converter refuses them. The
/// underlying value itself is read by System.Text.Json, whose own exception, for a token that is not one of
/// <typeparamref name="TValue"/>, carries no code.
/// </para>
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
            throw new ValueObjectJsonException(
                $"The value is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}",
                typeof(TSelf),
                validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable);
        }

        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// An instance equal to the default whose value the value object rejects is refused with a
    /// <see cref="ValueObjectJsonException"/> naming the rule and carrying its code, as the converter the generator
    /// emits refuses it, rather than written for a reader to refuse.
    /// </remarks>
    public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
    {
        ThrowIfRefused(value);
        JsonSerializer.Serialize(writer, value.Value, options);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The key is read as System.Text.Json reads a key of <typeparamref name="TValue"/>, then validated through
    /// <c>TryCreate</c>, as a value is: the reverse of <see cref="WriteAsPropertyName"/>, whatever text the value
    /// object's own parser reads. A key that is not one of <typeparamref name="TValue"/> is refused as it is in a
    /// dictionary of its own, and a key the value object rejects with a <see cref="ValueObjectJsonException"/> naming the
    /// rule and carrying its code.
    /// </remarks>
    public override TSelf ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = GetValueConverter(options).ReadAsPropertyName(ref reader, typeof(TValue), options);

        if (!TSelf.TryCreate(value, out var result, out var validation))
        {
            throw new ValueObjectJsonException(
                $"The dictionary key is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}",
                typeof(TSelf),
                validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable);
        }

        return result;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The key is the underlying value, written as System.Text.Json writes a key of <typeparamref name="TValue"/>:
    /// the form the value itself travels in. The value object's own formatting, which may print something its
    /// parser does not read, never reaches the wire. An underlying type System.Text.Json cannot write as a key is
    /// refused as it is in a dictionary of its own, with a <see cref="NotSupportedException"/>, and a key the value
    /// object rejects, as <see cref="Write"/> refuses a value, with a <see cref="ValueObjectJsonException"/> naming the
    /// rule.
    /// </remarks>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
    {
        ThrowIfRefused(value);
        GetValueConverter(options).WriteAsPropertyName(writer, value.Value!, options);
    }

    /// <summary>
    /// Refuses to write a value the value object rejects, which only an instance equal to the default can hold: any
    /// other went through <c>Create</c>. Over a value type, a constructed zero equals the default too, and validation
    /// tells a valid zero from a refused one.
    /// </summary>
    /// <param name="value">Value object about to be written.</param>
    /// <exception cref="ValueObjectJsonException">The value object rejects the value.</exception>
    private static void ThrowIfRefused(TSelf value)
    {
        if (!value.IsDefault)
        {
            return;
        }

        var current = value.Value;
        var validation = TSelf.Validate(in current);
        if (!validation.IsValid)
        {
            throw new ValueObjectJsonException(
                $"The value to write is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}",
                typeof(TSelf),
                validation.ErrorCode);
        }
    }

    private static JsonConverter<TValue> GetValueConverter(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return (JsonConverter<TValue>)options.GetConverter(typeof(TValue));
    }
}
