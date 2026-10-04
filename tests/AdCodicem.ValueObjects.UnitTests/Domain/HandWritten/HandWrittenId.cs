using System.Diagnostics.CodeAnalysis;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

/// <summary>
/// What a hand-written identifier is told to do, one profile per behaviour a test needs.
/// </summary>
public interface IHandWrittenIdProfile
{
    /// <summary>Gets the prefix the identifier claims.</summary>
    static abstract string Prefix { get; }

    /// <summary>Gets a value indicating whether the parser and the factory refuse everything without giving a reason.</summary>
    static virtual bool RefusesSilently => false;

    /// <summary>Gets a value indicating whether <c>Value</c> answers <see langword="null"/>, which its contract rules out.</summary>
    static virtual bool HidesItsValue => false;
}

/// <summary>
/// An entity identifier written by hand, with no <c>[EntityId]</c>, so that nothing registers it and the generator
/// does not stand between a test and the registry.
/// </summary>
/// <typeparam name="TProfile">Prefix and behaviour of this identifier type.</typeparam>
/// <remarks>
/// Each profile closes a distinct type, with a prefix of its own. Within one compilation the generator refuses two
/// types sharing a prefix; across assemblies, or by hand, only the registry can, which is what
/// <see cref="ImpostorProfile"/> is for. The other profiles break the contract the way a careless hand-written
/// identifier could, to reach the guards <see cref="AnyEntityId"/> keeps against it.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "These members implement the static contract of IEntityId<TSelf>, which a generic identifier can "
                    + "implement no other way; one generic test double stands in for three near-identical types.")]
public readonly struct HandWrittenId<TProfile> : IEntityId<HandWrittenId<TProfile>>
    where TProfile : IHandWrittenIdProfile
{
    private readonly string? _value;

    private HandWrittenId(string value) => _value = value;

    /// <summary>Declares no rule a reflection-driven caller could publish.</summary>
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    public static string Prefix => TProfile.Prefix;

    public static IdGranularity Granularity => IdGranularity.Hour;

    public static int Length => EntityIdFormat.TotalLength(Prefix, Granularity);

    public string Value => TProfile.HidesItsValue ? null! : _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static HandWrittenId<TProfile> New() => New(ValueObjectIds.TimeProvider, ValueObjectIds.Entropy);

    public static HandWrittenId<TProfile> New(TimeProvider timeProvider, IdEntropySource entropy)
        => new(EntityIdFormat.Create(Prefix, Granularity, timeProvider, entropy));

    public static string Normalize(string value) => value is null ? value! : EntityIdFormat.Normalize(value, Prefix);

    public static ValidationResult Validate(in string value)
        => string.IsNullOrEmpty(value) ? ValidationResult.Required() : EntityIdFormat.Validate(value, Prefix, Granularity);

    public static HandWrittenId<TProfile> Create(string value)
    {
        var normalized = Normalize(value);
        Validate(normalized).ThrowIfInvalid(typeof(HandWrittenId<TProfile>), value);

        return new HandWrittenId<TProfile>(normalized);
    }

    public static bool TryCreate(string value, out HandWrittenId<TProfile> result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out HandWrittenId<TProfile> result, out ValidationResult validation)
    {
        if (TProfile.RefusesSilently)
        {
            result = default;
            validation = ValidationResult.Success;
            return false;
        }

        var normalized = Normalize(value);
        validation = Validate(normalized);
        result = validation.IsValid ? new HandWrittenId<TProfile>(normalized) : default;

        return validation.IsValid;
    }

    public static HandWrittenId<TProfile> CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out HandWrittenId<TProfile> result, out ValidationResult validation)
    {
        if (TProfile.RefusesSilently)
        {
            result = default;
            validation = ValidationResult.Success;
            return false;
        }

        return TryCreate(text.ToString(), out result, out validation);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HandWrittenId<TProfile> result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out HandWrittenId<TProfile> result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static HandWrittenId<TProfile> Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static HandWrittenId<TProfile> Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(HandWrittenId<TProfile> other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is HandWrittenId<TProfile> other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(HandWrittenId<TProfile> other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;

    public string ToString(string? format, IFormatProvider? formatProvider) => Value;

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (Value.TryCopyTo(destination))
        {
            charsWritten = Value.Length;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    public static bool operator ==(HandWrittenId<TProfile> left, HandWrittenId<TProfile> right) => left.Equals(right);

    public static bool operator !=(HandWrittenId<TProfile> left, HandWrittenId<TProfile> right) => !left.Equals(right);

    public static bool operator <(HandWrittenId<TProfile> left, HandWrittenId<TProfile> right) => left.CompareTo(right) < 0;

    public static bool operator >(HandWrittenId<TProfile> left, HandWrittenId<TProfile> right) => left.CompareTo(right) > 0;

    public static bool operator <=(HandWrittenId<TProfile> left, HandWrittenId<TProfile> right) => left.CompareTo(right) <= 0;

    public static bool operator >=(HandWrittenId<TProfile> left, HandWrittenId<TProfile> right) => left.CompareTo(right) >= 0;
}

/// <summary>Claims <c>acc</c>, the prefix <see cref="AccountId"/> registered first.</summary>
public sealed class ImpostorProfile : IHandWrittenIdProfile
{
    public static string Prefix => "acc";
}

/// <summary>Refuses every text and every value, and never says why.</summary>
public sealed class MuteProfile : IHandWrittenIdProfile
{
    public static string Prefix => "mute";

    public static bool RefusesSilently => true;
}

/// <summary>Accepts its identifiers and hands back no value for them.</summary>
public sealed class BlankProfile : IHandWrittenIdProfile
{
    public static string Prefix => "blank";

    public static bool HidesItsValue => true;
}
