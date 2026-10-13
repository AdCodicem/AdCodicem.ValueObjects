using System.ComponentModel.DataAnnotations;
using System.Globalization;
using AdCodicem.ValueObjects.Metadata;
using AnnotationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace AdCodicem.ValueObjects.UnitTests.DataAnnotations;

/// <summary>
/// What the DataAnnotations tests share: validating a value through an attribute as a host does, the attributes of both
/// forms over a value object a descriptor hands back, and the cultures of a test.
/// </summary>
internal static class Annotated
{
    /// <summary>The member a value is validated as, when nothing else names one.</summary>
    public const string Member = "Account";

    /// <summary>
    /// Validates a value through an attribute as <c>Validator</c> does, under a member of a name of its own.
    /// </summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="value">The value.</param>
    /// <param name="member">The member, its name and its display name.</param>
    /// <returns>The result, or <see langword="null"/> for a value the attribute accepts.</returns>
    public static AnnotationResult? Check(ValidationAttribute attribute, object? value, string member = Member)
        => attribute.GetValidationResult(value, Context(member));

    /// <summary>
    /// Validates a value through an attribute as <c>Validator</c> does, and reads the refusal it reports.
    /// </summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="value">The value.</param>
    /// <param name="member">The member, its name and its display name.</param>
    /// <returns>The refusal.</returns>
    public static ValueObjectValidationResult Refusal(ValidationAttribute attribute, object? value, string member = Member)
        => Check(attribute, value, member).Should().BeOfType<ValueObjectValidationResult>().Which;

    /// <summary>Gets a context validating a member of an object of no rule of its own.</summary>
    /// <param name="member">The member, its name and its display name.</param>
    /// <returns>The context.</returns>
    public static ValidationContext Context(string member = Member)
        => new(new object(), null, null) { MemberName = member, DisplayName = member };

    /// <summary>Gets <c>[ParsesAs(typeof(TSelf))]</c> and <c>[ParsesAs&lt;TSelf, TValue&gt;]</c> over a value object.</summary>
    /// <param name="type">The value object type, registered or described by the registry.</param>
    /// <returns>Both forms, the <see cref="Type"/> form first.</returns>
    public static ParsesAsAttribute[] BothForms(Type type)
    {
        ValueObjectRegistry.TryResolve(type, out var descriptor).Should().BeTrue("{0} is a value object", type.Name);

        return [new ParsesAsAttribute(type), descriptor!.Accept(GenericForm.Instance)];
    }

    /// <summary>Sets the cultures of the current execution, and gives the previous ones back when disposed.</summary>
    /// <param name="culture">The culture.</param>
    /// <param name="uiCulture">The UI culture, the culture itself when <see langword="null"/>.</param>
    /// <returns>What gives them back.</returns>
    public static IDisposable Culture(string culture, string? uiCulture = null) => new CultureScope(culture, uiCulture ?? culture);

    /// <summary>Closes the generic form of the attribute over the type arguments a descriptor hands back.</summary>
    private sealed class GenericForm : IValueObjectVisitor<ParsesAsAttribute>
    {
        public static readonly GenericForm Instance = new();

        public ParsesAsAttribute Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => new ParsesAsAttribute<TSelf, TValue>();
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string culture, string uiCulture)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(uiCulture);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }
}

/// <summary>
/// A message DataAnnotations reads from a resource by name, as <c>ErrorMessageResourceName</c> names it.
/// </summary>
public static class AnnotationMessages
{
    /// <summary>Gets the message of a refused account.</summary>
    public static string RefusedAccount => "{0} is not an account we hold.";
}

/// <summary>
/// The form a person fills in to transfer money: the text of each value object, held to its rules.
/// </summary>
public sealed class TransferForm
{
    [Required]
    [ParsesAs(typeof(Iban))]
    [Display(Name = "Settlement account")]
    public string? Account { get; set; }

    [ParsesAs<Iban, string>]
    public string? Beneficiary { get; set; }

    [ParsesAs(typeof(Quantity))]
    public string? Quantity { get; set; }

    [ParsesAs(typeof(Amount))]
    public string? Amount { get; set; }

    [ParsesAs(typeof(Amount), UseCurrentCulture = true)]
    public string? TypedAmount { get; set; }

    [ParsesAs(typeof(CountryCode))]
    public string? Country { get; set; }

    [ParsesAs(typeof(Iban))]
    public IReadOnlyList<string?>? Alternates { get; set; }
}

/// <summary>
/// A command holding value objects, which <c>[Required]</c> lets through at their default and <c>[NotDefault]</c> refuses.
/// </summary>
public sealed class SettleCommand
{
    [Required]
    public Iban Unchecked { get; set; }

    [Required]
    [NotDefault]
    public Iban Account { get; set; }

    [NotDefault]
    public CustomerId Customer { get; set; }

    [NotDefault]
    public Iban? Fallback { get; set; }
}

/// <summary>
/// A string value object written by hand whose parser refuses <c>""</c> without a code, and <c>"quiet"</c> with a code
/// and no message, as a parser written by hand may.
/// </summary>
internal readonly struct SilentCode : IValueObject<SilentCode, string>
{
    /// <summary>The code it refuses <c>"quiet"</c> with.</summary>
    public const string QuietCode = "silent.quiet";

    private readonly string? _value;

    private SilentCode(string value) => _value = value;

    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;

    public string Value => _value ?? string.Empty;

    public bool IsDefault => _value is null;

    public static string Normalize(string value) => value;

    public static ValidationResult Validate(in string value)
        => value == "quiet" ? ValidationResult.Failure(QuietCode, string.Empty) : ValidationResult.Success;

    public static SilentCode Create(string value)
    {
        Validate(value).ThrowIfInvalid(typeof(SilentCode), value);

        return new SilentCode(value);
    }

    public static bool TryCreate(string value, out SilentCode result) => TryCreate(value, out result, out _);

    public static bool TryCreate(string value, out SilentCode result, out ValidationResult validation)
    {
        validation = Validate(value);
        result = validation.IsValid ? new SilentCode(value) : default;

        return validation.IsValid;
    }

    public static SilentCode CreateUnchecked(string value) => new(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out SilentCode result, out ValidationResult validation)
    {
        if (text.IsEmpty)
        {
            // Refused without saying why.
            result = default;
            validation = ValidationResult.Success;
            return false;
        }

        return TryCreate(text.ToString(), out result, out validation);
    }

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out SilentCode result)
        => TryParse(s, provider, out result, out _);

    public static bool TryParse(string? s, IFormatProvider? provider, out SilentCode result)
        => TryParse(s.AsSpan(), provider, out result, out _);

    public static SilentCode Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(s.ToString());

    public static SilentCode Parse(string s, IFormatProvider? provider) => Create(s);

    public bool Equals(SilentCode other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is SilentCode other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public int CompareTo(SilentCode other) => string.CompareOrdinal(Value, other.Value);

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

    public static bool operator ==(SilentCode left, SilentCode right) => left.Equals(right);

    public static bool operator !=(SilentCode left, SilentCode right) => !left.Equals(right);

    public static bool operator <(SilentCode left, SilentCode right) => left.CompareTo(right) < 0;

    public static bool operator >(SilentCode left, SilentCode right) => left.CompareTo(right) > 0;

    public static bool operator <=(SilentCode left, SilentCode right) => left.CompareTo(right) <= 0;

    public static bool operator >=(SilentCode left, SilentCode right) => left.CompareTo(right) >= 0;
}
