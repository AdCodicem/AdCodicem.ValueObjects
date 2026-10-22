using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;

namespace AdCodicem.ValueObjects.UnitTests.Cli;

/// <summary>The owner of a <see cref="Reference{TOwner}"/> the tests of Cli/ alone construct, which nothing registers.</summary>
public sealed class CliOwner;

/// <summary>
/// The owner of a <see cref="Reference{TOwner}"/> named only where dynamic code is off, so that nothing registers it before
/// a test registers it by hand.
/// </summary>
public sealed class UnregisteredCliOwner;

/// <summary>Whether a run touches the live system, which a test refuses with a code of its own.</summary>
[ValueObject<bool>]
public readonly partial struct LiveRun : IValueObjectValidator<bool>
{
    /// <summary>The code a live run is refused with.</summary>
    public const string Forbidden = "cli.live_forbidden";

    public static ValidationResult ValidateValue(in bool value)
        => value ? ValidationResult.Failure(Forbidden, "Live runs are off.") : ValidationResult.Success;
}

/// <summary>
/// A ticket written by hand over <see cref="string"/>, read from <c>T-</c> followed by anything, which nothing registers:
/// the registry describes it the first time a symbol holds it.
/// </summary>
public sealed class CliTicket : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { Description = "A support ticket.", MaxLength = 8 };

    public static ValidationResult Validate(string value)
        => value.Length <= 8 ? ValidationResult.Success : ValidationResult.Failure(ValueObjectErrorCodes.TooLong, "A ticket is at most 8 characters long.");

    public static bool TryRead(string text, out string value)
    {
        value = text.Trim();
        return value.StartsWith("T-", StringComparison.Ordinal);
    }
}

/// <summary>
/// A number written by hand whose schema lists a known value of another type and no details, as a schema written
/// carelessly may.
/// </summary>
public sealed class CliLooseNumber : IDeclaration<int>
{
    public static ValueObjectSchema Schema { get; } = new() { KnownValues = ["seven"] };

    public static bool TryRead(string text, out int value) => int.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out value);
}

/// <summary>
/// A code written by hand whose parser refuses <c>"unexplained"</c> without a reason, and <c>"quiet"</c> with a code and an
/// empty message, as a parser written by hand may; any other text is the code.
/// </summary>
public readonly struct CliUnexplainedCode : IValueObject<CliUnexplainedCode, string>
{
    /// <summary>The code it refuses <c>"quiet"</c> with.</summary>
    public const string QuietCode = "cli.quiet";

    private readonly string? _value;

    private CliUnexplainedCode(string value) => _value = value;

    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value;

    public static ValidationResult Validate(in string value)
        => value == "quiet" ? ValidationResult.Failure(QuietCode, string.Empty) : ValidationResult.Success;

    public static CliUnexplainedCode Create(string value)
    {
        Validate(value).ThrowIfInvalid(typeof(CliUnexplainedCode), value);

        return new CliUnexplainedCode(value);
    }

    public static bool TryCreate(string value, out CliUnexplainedCode result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out CliUnexplainedCode result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new CliUnexplainedCode(value) : default;

        return validation.IsValid;
    }

    public static CliUnexplainedCode CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out CliUnexplainedCode result, out ValidationResult validation)
    {
        if (text.SequenceEqual("unexplained"))
        {
            // Refused without saying why.
            result = default;
            validation = ValidationResult.Success;
            return false;
        }

        return TryCreate(text.ToString(), out result, out validation);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out CliUnexplainedCode result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out CliUnexplainedCode result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static CliUnexplainedCode Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static CliUnexplainedCode Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(CliUnexplainedCode other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is CliUnexplainedCode other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(CliUnexplainedCode other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;

    public string ToString(string? format, IFormatProvider? formatProvider) => Value;

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        charsWritten = Value.TryCopyTo(destination) ? Value.Length : 0;

        return charsWritten == Value.Length;
    }

    public static bool operator ==(CliUnexplainedCode left, CliUnexplainedCode right) => left.Equals(right);

    public static bool operator !=(CliUnexplainedCode left, CliUnexplainedCode right) => !left.Equals(right);

    public static bool operator <(CliUnexplainedCode left, CliUnexplainedCode right) => left.CompareTo(right) < 0;

    public static bool operator >(CliUnexplainedCode left, CliUnexplainedCode right) => left.CompareTo(right) > 0;

    public static bool operator <=(CliUnexplainedCode left, CliUnexplainedCode right) => left.CompareTo(right) <= 0;

    public static bool operator >=(CliUnexplainedCode left, CliUnexplainedCode right) => left.CompareTo(right) >= 0;
}
