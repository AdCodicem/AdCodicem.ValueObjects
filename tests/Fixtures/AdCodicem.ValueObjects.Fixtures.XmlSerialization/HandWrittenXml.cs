using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using AdCodicem.ValueObjects.Metadata;

// A value object is generic over its rules here, and its contract is made of static members.
#pragma warning disable CA1000

namespace AdCodicem.ValueObjects.Fixtures.XmlSerialization;

/// <summary>
/// The rules of a value object written by hand: a schema the generator never writes, and refusals it never makes.
/// </summary>
/// <typeparam name="TValue">The underlying type.</typeparam>
public interface IHandWrittenRules<TValue>
{
    /// <summary>Gets the schema the value object states.</summary>
    static abstract ValueObjectSchema Schema { get; }

    /// <summary>Gets the value of a default instance.</summary>
    static abstract TValue Empty { get; }

    /// <summary>Validates a value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The rule it breaks, if any.</returns>
    static abstract ValidationResult Validate(in TValue value);

    /// <summary>Tells whether <c>TryCreate</c> takes a value <see cref="Validate"/> accepts, as a hand-written one may not.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="false"/> to refuse it without a code.</returns>
    static abstract bool Takes(TValue value);
}

/// <summary>
/// A value object written by hand that implements <see cref="IXmlSerializable"/> through <see cref="ValueObjectXml"/>,
/// as one may, with rules of its own and a namespace of its own.
/// </summary>
/// <typeparam name="TValue">The underlying type.</typeparam>
/// <typeparam name="TRules">Its rules.</typeparam>
[XmlSchemaProvider("GetXmlSchema")]
public readonly struct HandWrittenXml<TValue, TRules> : IValueObject<HandWrittenXml<TValue, TRules>, TValue>, IXmlSerializable
    where TValue : notnull
    where TRules : IHandWrittenRules<TValue>
{
    /// <summary>The namespace its schema type is put under.</summary>
    public const string Namespace = "urn:handwritten";

    private readonly TValue? _value;

    private HandWrittenXml(TValue value) => _value = value;

    /// <inheritdoc />
    public static ValueObjectSchema Schema => TRules.Schema;

    /// <inheritdoc />
    public TValue Value => IsDefault ? TRules.Empty : _value!;

    /// <inheritdoc />
    public bool IsDefault => EqualityComparer<TValue?>.Default.Equals(_value, default);

    /// <inheritdoc />
    public static TValue Normalize(TValue value) => value;

    /// <inheritdoc />
    public static ValidationResult Validate(in TValue value) => TRules.Validate(value);

    /// <inheritdoc />
    public static HandWrittenXml<TValue, TRules> Create(TValue value)
    {
        Validate(value).ThrowIfInvalid(typeof(HandWrittenXml<TValue, TRules>), value);

        return new HandWrittenXml<TValue, TRules>(value);
    }

    /// <inheritdoc />
    public static bool TryCreate(TValue value, out HandWrittenXml<TValue, TRules> result) => TryCreate(value, out result, out _);

    /// <inheritdoc />
    public static bool TryCreate(TValue value, out HandWrittenXml<TValue, TRules> result, out ValidationResult validation)
    {
        validation = Validate(value);
        var taken = validation.IsValid && TRules.Takes(value);
        result = taken ? new HandWrittenXml<TValue, TRules>(value) : default;

        return taken;
    }

    /// <inheritdoc />
    public static HandWrittenXml<TValue, TRules> CreateUnchecked(TValue value) => new(value);

    /// <summary>Describes the type to the serializers.</summary>
    /// <param name="schemas">The schemas the serializer is building.</param>
    /// <returns>The name of the type in them.</returns>
    public static XmlQualifiedName GetXmlSchema(XmlSchemaSet schemas)
        => ValueObjectXml.ProvideSchema<HandWrittenXml<TValue, TRules>, TValue>(schemas, Namespace);

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out HandWrittenXml<TValue, TRules> result, out ValidationResult validation)
        => throw new NotSupportedException("Read from XML alone.");

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HandWrittenXml<TValue, TRules> result)
        => TryParse(s, provider, out result, out _);

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out HandWrittenXml<TValue, TRules> result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    /// <inheritdoc />
    public static HandWrittenXml<TValue, TRules> Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => throw new NotSupportedException("Read from XML alone.");

    /// <inheritdoc />
    public static HandWrittenXml<TValue, TRules> Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <inheritdoc />
    XmlSchema? IXmlSerializable.GetSchema() => null;

    /// <inheritdoc />
    void IXmlSerializable.ReadXml(XmlReader reader)
        => Unsafe.AsRef(in this) = ValueObjectXml.Read<HandWrittenXml<TValue, TRules>, TValue>(reader);

    /// <inheritdoc />
    void IXmlSerializable.WriteXml(XmlWriter writer) => ValueObjectXml.Write<HandWrittenXml<TValue, TRules>, TValue>(writer, this);

    /// <inheritdoc />
    public bool Equals(HandWrittenXml<TValue, TRules> other) => EqualityComparer<TValue>.Default.Equals(Value, other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is HandWrittenXml<TValue, TRules> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public int CompareTo(HandWrittenXml<TValue, TRules> other) => string.CompareOrdinal(ToString(), other.ToString());

    /// <inheritdoc />
    public override string ToString() => Value.ToString() ?? string.Empty;

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

    /// <inheritdoc />
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        var text = ToString();
        charsWritten = text.TryCopyTo(destination) ? text.Length : 0;
        return charsWritten == text.Length;
    }

    /// <inheritdoc />
    public static bool operator ==(HandWrittenXml<TValue, TRules> left, HandWrittenXml<TValue, TRules> right) => left.Equals(right);

    /// <inheritdoc />
    public static bool operator !=(HandWrittenXml<TValue, TRules> left, HandWrittenXml<TValue, TRules> right) => !left.Equals(right);

    /// <inheritdoc />
    public static bool operator <(HandWrittenXml<TValue, TRules> left, HandWrittenXml<TValue, TRules> right) => left.CompareTo(right) < 0;

    /// <inheritdoc />
    public static bool operator >(HandWrittenXml<TValue, TRules> left, HandWrittenXml<TValue, TRules> right) => left.CompareTo(right) > 0;

    /// <inheritdoc />
    public static bool operator <=(HandWrittenXml<TValue, TRules> left, HandWrittenXml<TValue, TRules> right) => left.CompareTo(right) <= 0;

    /// <inheritdoc />
    public static bool operator >=(HandWrittenXml<TValue, TRules> left, HandWrittenXml<TValue, TRules> right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// A weight up to 500, whose schema states a lower bound no reader takes and known values of another type than its
/// own, and whose <c>TryCreate</c> refuses a negative weight without saying why.
/// </summary>
public sealed class WeightRules : IHandWrittenRules<int>
{
    /// <inheritdoc />
    public static ValueObjectSchema Schema { get; } = new()
    {
        Minimum = "light",
        Maximum = "500",
        IsClosedValueSet = true,
        KnownValues = ["one", 2],
        Description = "A weight, in kilograms.",
    };

    /// <inheritdoc />
    public static int Empty => 0;

    /// <inheritdoc />
    public static ValidationResult Validate(in int value)
        => value > 500 ? ValidationResult.OutOfRange("The weight must be at most 500.") : ValidationResult.Success;

    /// <inheritdoc />
    public static bool Takes(int value) => value >= 0;
}

/// <summary>
/// A code whose schema states lengths that contradict each other, and a pattern anchored at neither end.
/// </summary>
public sealed class CodeRules : IHandWrittenRules<string>
{
    /// <inheritdoc />
    public static ValueObjectSchema Schema { get; } = new()
    {
        MinLength = 5,
        MaxLength = 2,
        Pattern = "[A-Z]+",
        Description = "A code no value meets.",
    };

    /// <inheritdoc />
    public static string Empty => string.Empty;

    /// <inheritdoc />
    public static ValidationResult Validate(in string value) => ValidationResult.Success;

    /// <inheritdoc />
    public static bool Takes(string value) => true;
}

/// <summary>A link, over a type none of the generator's: XML has no form for it.</summary>
public sealed class LinkRules : IHandWrittenRules<Uri>
{
    /// <inheritdoc />
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    /// <inheritdoc />
    public static Uri Empty { get; } = new("about:blank");

    /// <inheritdoc />
    public static ValidationResult Validate(in Uri value) => ValidationResult.Success;

    /// <inheritdoc />
    public static bool Takes(Uri value) => true;
}
