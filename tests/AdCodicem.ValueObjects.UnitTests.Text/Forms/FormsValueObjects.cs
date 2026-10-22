using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;

namespace AdCodicem.ValueObjects.UnitTests.Forms;

/// <summary>The owner of a <see cref="Reference{TOwner}"/> the tests of Forms/ alone construct, which nothing registers.</summary>
public sealed class FormsOwner;

/// <summary>
/// A ticket written by hand over <see cref="string"/>, read from <c>T-</c> followed by digits, which nothing registers: the
/// registry describes it the first time an input binds it.
/// </summary>
public sealed class FormsTicket : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { Description = "A ticket.", MaxLength = 8 };

    public static ValidationResult Validate(string value)
        => value.Length <= 8 ? ValidationResult.Success : ValidationResult.Failure(ValueObjectErrorCodes.TooLong, "A ticket is at most 8 characters long.");

    public static bool TryRead(string text, out string value)
    {
        value = text.Trim();
        return value.StartsWith("T-", StringComparison.Ordinal);
    }
}

/// <summary>
/// A code written by hand whose parser refuses <c>"unexplained"</c> without a reason, and <c>"quiet"</c> with a code and an
/// empty message, as a parser written by hand may; any other text is the code.
/// </summary>
public readonly struct UnexplainedCode : IValueObject<UnexplainedCode, string>
{
    /// <summary>The code it refuses <c>"quiet"</c> with.</summary>
    public const string QuietCode = "forms.quiet";

    private readonly string? _value;

    private UnexplainedCode(string value) => _value = value;

    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value;

    public static ValidationResult Validate(in string value)
        => value switch
        {
            "" => ValidationResult.Required("A code is required."),
            "quiet" => ValidationResult.Failure(QuietCode, string.Empty),
            _ => ValidationResult.Success,
        };

    public static UnexplainedCode Create(string value)
    {
        Validate(value).ThrowIfInvalid(typeof(UnexplainedCode), value);

        return new UnexplainedCode(value);
    }

    public static bool TryCreate(string value, out UnexplainedCode result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out UnexplainedCode result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new UnexplainedCode(value) : default;

        return validation.IsValid;
    }

    public static UnexplainedCode CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out UnexplainedCode result, out ValidationResult validation)
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

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out UnexplainedCode result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out UnexplainedCode result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static UnexplainedCode Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static UnexplainedCode Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(UnexplainedCode other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is UnexplainedCode other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(UnexplainedCode other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;

    public string ToString(string? format, IFormatProvider? formatProvider) => Value;

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        charsWritten = Value.TryCopyTo(destination) ? Value.Length : 0;

        return charsWritten == Value.Length;
    }

    public static bool operator ==(UnexplainedCode left, UnexplainedCode right) => left.Equals(right);

    public static bool operator !=(UnexplainedCode left, UnexplainedCode right) => !left.Equals(right);

    public static bool operator <(UnexplainedCode left, UnexplainedCode right) => left.CompareTo(right) < 0;

    public static bool operator >(UnexplainedCode left, UnexplainedCode right) => left.CompareTo(right) > 0;

    public static bool operator <=(UnexplainedCode left, UnexplainedCode right) => left.CompareTo(right) <= 0;

    public static bool operator >=(UnexplainedCode left, UnexplainedCode right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// A ticket written by hand whose schema counts how often it is read, so that a test sees when an input writes the
/// attributes of its rules; any text is the ticket.
/// </summary>
public sealed class CountedTicket : IDeclaration<string>
{
    private static readonly ValueObjectSchema Rules = new() { Description = "A ticket.", MaxLength = 8 };

    private static int s_reads;

    /// <summary>Gets how often the schema was read.</summary>
    public static int Reads => Volatile.Read(ref s_reads);

    public static ValueObjectSchema Schema
    {
        get
        {
            Interlocked.Increment(ref s_reads);
            return Rules;
        }
    }

    public static bool TryRead(string text, out string value)
    {
        value = text;
        return true;
    }
}
