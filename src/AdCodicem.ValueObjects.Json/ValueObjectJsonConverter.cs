using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
/// The underlying value is read and written through the contract the options hold for <typeparamref name="TValue"/>
/// (<see cref="JsonSerializerOptions.GetTypeInfo(Type)"/>), so the converter needs neither reflection nor dynamic code
/// of its own: a source-generated context listing the value object holds that contract too, since the value object
/// exposes its value as a property. Called directly, it takes options, never <see langword="null"/>, which it refuses
/// with an <see cref="ArgumentNullException"/>; options no serializer has used and that set no resolver are given the
/// serializer's default one and locked, as serializing through them would lock them, so that setting one of their
/// properties afterwards throws an <see cref="InvalidOperationException"/>, whichever member was called.
/// </para>
/// <para>
/// A value or a key the value object rejects, and a value to write that it rejects, are refused with a
/// <see cref="ValueObjectJsonException"/> carrying the code of the rule, as the generated converter refuses them. The
/// underlying value itself is read by System.Text.Json, whose own exception, for a token that is not one of
/// <typeparamref name="TValue"/>, carries no code.
/// </para>
/// </remarks>
public sealed class ValueObjectJsonConverter<TSelf, TValue> : JsonConverter<TSelf>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    private const string NoResolverOnly =
        "Reached only when the converter is called directly, with options that set no resolver and that no serializer "
        + "has used, since the serializer populates the resolver before calling any converter. Where reflection-based "
        + "serialization is disabled, as trimming and native AOT disable it by default, System.Text.Json refuses with its "
        + "own InvalidOperationException rather than reflect.";

    /// <inheritdoc />
    public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize(ref reader, GetValueTypeInfo(options))!;

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
        JsonSerializer.Serialize(writer, value.Value, GetValueTypeInfo(options));
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

    /// <summary>
    /// Gets the contract the options hold for the underlying type, from whatever resolver they chain: a
    /// source-generated context, or reflection where the application allows it.
    /// </summary>
    /// <param name="options">Options the serializer runs with.</param>
    /// <returns>The contract of the underlying type.</returns>
    /// <exception cref="NotSupportedException">The options hold no contract for the underlying type.</exception>
    /// <exception cref="InvalidOperationException">
    /// The options set no resolver, and reflection-based serialization is disabled.
    /// </exception>
    private static JsonTypeInfo<TValue> GetValueTypeInfo(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.TypeInfoResolver is null)
        {
            PopulateMissingResolver(options);
        }

        return (JsonTypeInfo<TValue>)options.GetTypeInfo(typeof(TValue));
    }

    /// <summary>
    /// Gives options no serializer has used yet the resolver the serializer would, and locks them, as the serializer
    /// does before it calls any converter: only a converter called directly sees options with no resolver.
    /// </summary>
    /// <param name="options">Options that set no resolver.</param>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = NoResolverOnly)]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = NoResolverOnly)]
    private static void PopulateMissingResolver(JsonSerializerOptions options)
        => options.MakeReadOnly(populateMissingResolver: true);

    private static JsonConverter<TValue> GetValueConverter(JsonSerializerOptions options)
        => (JsonConverter<TValue>)GetValueTypeInfo(options).Converter;
}
