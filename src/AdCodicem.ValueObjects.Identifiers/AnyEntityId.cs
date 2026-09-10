using System.Diagnostics.CodeAnalysis;

namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// An identifier of any registered type, resolved from its prefix.
/// </summary>
/// <remarks>
/// <para>
/// This serves the places where the type is not known until the text arrives: a webhook body naming the
/// resource it concerns, a deep link, an audit trail recording heterogeneous references. Resolution costs a
/// bounded number of dictionary lookups and no reflection.
/// </para>
/// <para>
/// It deliberately implements neither <see cref="IValueObject"/> nor <see cref="IEntityId"/>, and that is what
/// makes it non-persistable by construction rather than by convention: the Entity Framework Core integration
/// keys off those interfaces, so this type is invisible to it and no one can map a polymorphic column by
/// accident. It is a transport and resolution type, nothing more.
/// </para>
/// </remarks>
[System.Text.Json.Serialization.JsonConverter(typeof(AnyEntityIdJsonConverter))]
[System.ComponentModel.TypeConverter(typeof(AnyEntityIdTypeConverter))]
public readonly struct AnyEntityId : IEquatable<AnyEntityId>, ISpanParsable<AnyEntityId>, ISpanFormattable
{
    private readonly string? _value;
    private readonly EntityIdDescriptor? _descriptor;

    private AnyEntityId(string value, EntityIdDescriptor descriptor)
    {
        _value = value;
        _descriptor = descriptor;
    }

    /// <summary>
    /// Gets the canonical text of the identifier.
    /// </summary>
    public string Value => _value ?? string.Empty;

    /// <summary>
    /// Gets the prefix the identifier carries, without its trailing separator.
    /// </summary>
    public string Prefix => _descriptor?.Prefix ?? string.Empty;

    /// <summary>
    /// Gets the identifier type this text belongs to, or <see langword="null"/> for the default instance.
    /// </summary>
    public Type? ValueObjectType => _descriptor?.ValueObjectType;

    /// <summary>
    /// Gets a value indicating whether this instance is the uninitialized <see langword="default"/>.
    /// </summary>
    public bool IsDefault => _descriptor is null;

    /// <summary>
    /// Parses text carrying any registered prefix.
    /// </summary>
    /// <param name="s">Text to parse.</param>
    /// <param name="provider">Unused; identifiers are culture-independent.</param>
    /// <returns>The resolved identifier.</returns>
    /// <exception cref="ValueObjectException">The text is not a valid identifier of any registered type.</exception>
    public static AnyEntityId Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
    {
        if (TryParse(s, provider, out var result, out var validation))
        {
            return result;
        }

        throw new ValueObjectException(
            $"'{s.ToString()}' is not a valid entity identifier: {validation.ErrorMessage}",
            typeof(AnyEntityId),
            validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable,
            s.ToString());
    }

    /// <inheritdoc cref="Parse(ReadOnlySpan{char}, IFormatProvider?)" />
    public static AnyEntityId Parse(string s, IFormatProvider? provider = null)
        => Parse(MemoryExtensions.AsSpan(s), provider);

    /// <summary>
    /// Parses text carrying any registered prefix, without throwing.
    /// </summary>
    /// <param name="s">Text to parse.</param>
    /// <param name="provider">Unused; identifiers are culture-independent.</param>
    /// <param name="result">The resolved identifier, or <see langword="default"/> when the text is rejected.</param>
    /// <returns><see langword="true"/> when the text was accepted.</returns>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out AnyEntityId result)
        => TryParse(s, provider, out result, out _);

    /// <summary>
    /// Parses text carrying any registered prefix, reporting why it was rejected.
    /// </summary>
    /// <param name="s">Text to parse.</param>
    /// <param name="provider">Unused; identifiers are culture-independent.</param>
    /// <param name="result">The resolved identifier, or <see langword="default"/> when the text is rejected.</param>
    /// <param name="validation">
    /// The outcome, distinguishing a prefix no type claims from a prefix that resolves but whose body is
    /// corrupt — a caller that cannot tell the two apart cannot write a useful error message.
    /// </param>
    /// <returns><see langword="true"/> when the text was accepted.</returns>
    public static bool TryParse(
        ReadOnlySpan<char> s,
        IFormatProvider? provider,
        out AnyEntityId result,
        out ValidationResult validation)
    {
        var trimmed = s.Trim();

        if (!EntityIdRegistry.TryResolve(trimmed, out var descriptor))
        {
            result = default;
            validation = ValidationResult.Failure(
                IdentifierErrorCodes.UnknownPrefix,
                "The text carries no prefix belonging to a registered identifier type.");

            return false;
        }

        if (!descriptor.TryParse(trimmed, provider, out var parsed, out validation) || parsed is null)
        {
            result = default;
            return false;
        }

        result = new AnyEntityId(parsed.ToString()!, descriptor);
        return true;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out AnyEntityId result)
        => TryParse(MemoryExtensions.AsSpan(s), provider, out result, out _);

    /// <summary>
    /// Determines whether this identifier belongs to a given type.
    /// </summary>
    /// <typeparam name="TId">Identifier type to test against.</typeparam>
    /// <returns><see langword="true"/> when the prefix resolved to <typeparamref name="TId"/>.</returns>
    public bool Is<TId>()
        where TId : struct, IEntityId<TId>
        => _descriptor?.ValueObjectType == typeof(TId);

    /// <summary>
    /// Converts to a concrete identifier type.
    /// </summary>
    /// <typeparam name="TId">Identifier type to convert to.</typeparam>
    /// <param name="result">The typed identifier, or <see langword="default"/> when this is not one.</param>
    /// <returns><see langword="true"/> when the conversion succeeded.</returns>
    /// <remarks>
    /// The type test comes first, so converting to the wrong type costs a reference comparison rather than a
    /// parse that was always going to fail.
    /// </remarks>
    public bool TryConvertTo<TId>(out TId result)
        where TId : struct, IEntityId<TId>
    {
        if (!Is<TId>())
        {
            result = default;
            return false;
        }

        return TId.TryParse(MemoryExtensions.AsSpan(_value), null, out result);
    }

    /// <summary>
    /// Builds the concrete identifier, boxed.
    /// </summary>
    /// <returns>The boxed identifier, or <see langword="null"/> for the default instance.</returns>
    /// <remarks>
    /// For callers that only hold a <see cref="Type"/> — a dispatcher looking up a handler, for instance.
    /// Prefer <see cref="TryConvertTo{TId}"/> wherever the type is known.
    /// </remarks>
    public object? ToValueObject()
    {
        if (_descriptor is null || _value is null)
        {
            return null;
        }

        return _descriptor.TryParse(MemoryExtensions.AsSpan(_value), null, out var boxed, out _) ? boxed : null;
    }

    /// <inheritdoc />
    public bool Equals(AnyEntityId other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is AnyEntityId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value is null ? 0 : _value.GetHashCode(StringComparison.Ordinal);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) => Value;

    /// <inheritdoc />
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (MemoryExtensions.AsSpan(Value).TryCopyTo(destination))
        {
            charsWritten = Value.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    /// <summary>Determines whether two identifiers are equal.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when both are equal.</returns>
    public static bool operator ==(AnyEntityId left, AnyEntityId right) => left.Equals(right);

    /// <summary>Determines whether two identifiers differ.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    public static bool operator !=(AnyEntityId left, AnyEntityId right) => !left.Equals(right);
}
