using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.TestData;

// Declared<TDeclaration, TValue>: a value object written by hand over a declaration a test owns, so that the type a
// boxed path registers is the test's own (tests/AdCodicem.ValueObjects.UnitTests/CLAUDE.md).

/// <summary>A schema a value object written by hand declares, with the rule its <c>Validate</c> enforces.</summary>
/// <typeparam name="TValue">The underlying type.</typeparam>
public interface IDeclaration<TValue>
{
    static abstract ValueObjectSchema Schema { get; }

    /// <summary>Validates a value; anything but <see langword="null"/> by default.</summary>
    static virtual ValidationResult Validate(TValue value)
        => value is null ? ValidationResult.Required() : ValidationResult.Success;

    /// <summary>Reads text; nothing by default.</summary>
    static virtual bool TryRead(string text, out TValue value)
    {
        value = default!;
        return false;
    }

    /// <summary>Normalizes a value; leaves it as it is by default.</summary>
    static virtual TValue NormalizeValue(TValue value) => value;

    /// <summary>Validates a value <c>Create</c> is given, if not as <see cref="Validate"/> does; <see langword="null"/> for the same.</summary>
    static virtual ValidationResult? ValidateOnCreate(TValue value) => null;
}

/// <summary>
/// A value object written by hand over any underlying type, whose schema and rule <typeparamref name="TDeclaration"/>
/// declares: the schemas the generator never writes. Nothing registers it, and the sampler never asks the registry.
/// </summary>
/// <typeparam name="TDeclaration">The declaration.</typeparam>
/// <typeparam name="TValue">The underlying type.</typeparam>
/// <remarks>Its JSON converter reads any value unchecked, as a converter written carelessly does.</remarks>
#pragma warning disable CA1000 // The members of a value object are static, and the generic type is the point of the fixture.
[JsonConverter(typeof(UncheckedJsonConverterFactory))]
public readonly struct Declared<TDeclaration, TValue> : IValueObject<Declared<TDeclaration, TValue>, TValue>
    where TDeclaration : IDeclaration<TValue>
{
    private readonly TValue _value;

    private Declared(TValue value) => _value = value;

    public static ValueObjectSchema Schema => TDeclaration.Schema;

    public TValue Value => _value;

    public bool IsDefault => EqualityComparer<TValue>.Default.Equals(_value, default!);

    public static TValue Normalize(TValue value) => TDeclaration.NormalizeValue(value);

    public static ValidationResult Validate(in TValue value) => TDeclaration.Validate(value);

    public static Declared<TDeclaration, TValue> Create(TValue value)
    {
        (TDeclaration.ValidateOnCreate(value) ?? Validate(value)).ThrowIfInvalid(typeof(Declared<TDeclaration, TValue>), value);

        return new(value);
    }

    public static bool TryCreate(TValue value, out Declared<TDeclaration, TValue> result) => TryCreate(value, out result, out _);

    public static bool TryCreate(TValue value, out Declared<TDeclaration, TValue> result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new(value) : default;

        return validation.IsValid;
    }

    public static Declared<TDeclaration, TValue> CreateUnchecked(TValue value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out Declared<TDeclaration, TValue> result, out ValidationResult validation)
    {
        if (TDeclaration.TryRead(text.ToString(), out var value))
        {
            return TryCreate(value, out result, out validation);
        }

        result = default;
        validation = ValidationResult.Failure(ValueObjectErrorCodes.NotParsable, "The text is not a value.");

        return false;
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Declared<TDeclaration, TValue> result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out Declared<TDeclaration, TValue> result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static Declared<TDeclaration, TValue> Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        => TryParse(s, provider, out var result) ? result : throw new FormatException("The text is not a value.");

    public static Declared<TDeclaration, TValue> Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    public bool Equals(Declared<TDeclaration, TValue> other) => EqualityComparer<TValue>.Default.Equals(_value, other._value);

    public override bool Equals(object? obj) => obj is Declared<TDeclaration, TValue> other && Equals(other);

    public override int GetHashCode() => _value is null ? 0 : EqualityComparer<TValue>.Default.GetHashCode(_value);

    public int CompareTo(Declared<TDeclaration, TValue> other) => Comparer<TValue>.Default.Compare(_value, other._value);

    public override string ToString() => ToString(null, null);

    public string ToString(string? format, IFormatProvider? formatProvider) => Convert.ToString(_value, CultureInfo.InvariantCulture) ?? string.Empty;

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        var text = ToString();
        charsWritten = text.TryCopyTo(destination) ? text.Length : 0;

        return charsWritten == text.Length;
    }

    public static bool operator ==(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => left.Equals(right);

    public static bool operator !=(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => !left.Equals(right);

    public static bool operator <(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => left.CompareTo(right) < 0;

    public static bool operator >(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => left.CompareTo(right) > 0;

    public static bool operator <=(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Declared<TDeclaration, TValue> left, Declared<TDeclaration, TValue> right) => left.CompareTo(right) >= 0;
}
#pragma warning restore CA1000

/// <summary>Reads and writes a <see cref="Declared{TDeclaration, TValue}"/> as its value, read through no rule.</summary>
public sealed class UncheckedJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Declared<,>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(UncheckedJsonConverter<,>).MakeGenericType(typeToConvert.GetGenericArguments()))!;

    private sealed class UncheckedJsonConverter<TDeclaration, TValue> : JsonConverter<Declared<TDeclaration, TValue>>
        where TDeclaration : IDeclaration<TValue>
    {
        public override Declared<TDeclaration, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => Declared<TDeclaration, TValue>.CreateUnchecked(JsonSerializer.Deserialize<TValue>(ref reader, options)!);

        public override void Write(Utf8JsonWriter writer, Declared<TDeclaration, TValue> value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value.Value, options);
    }
}
