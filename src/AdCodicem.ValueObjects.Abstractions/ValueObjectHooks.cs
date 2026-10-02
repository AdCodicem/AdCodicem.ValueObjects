namespace AdCodicem.ValueObjects;

/// <summary>
/// Declares that a value object normalizes its underlying value before validating it.
/// </summary>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// Implement this to give a value object a normalization rule: an IBAN without its separators and upper-cased,
/// a phone number carrying its country code. The generator calls <see cref="NormalizeValue"/> from the
/// <c>Normalize</c> member of <see cref="IValueObject{TSelf, TValue}"/>, which additionally guards against a
/// null underlying value, so the rule itself never has to.
/// </para>
/// <para>
/// Normalization must be idempotent and must never reject: normalizing an already normalized value returns it
/// unchanged, and a value that cannot be normalized is rejected by <see cref="IValueObjectValidator{TValue}"/>
/// instead.
/// </para>
/// </remarks>
public interface IValueObjectNormalizer<TValue>
{
    /// <summary>
    /// Puts a value into its canonical form.
    /// </summary>
    /// <param name="value">Value to normalize. Never <see langword="null"/>.</param>
    /// <returns>The canonical form of the value.</returns>
    static abstract TValue NormalizeValue(TValue value);
}

/// <summary>
/// Declares that a string value object can normalize straight from text, without materializing it first.
/// </summary>
/// <remarks>
/// <para>
/// Implement this alongside <see cref="IValueObjectNormalizer{TValue}"/> on a value object whose underlying
/// type is <see cref="string"/>. Parsing and JSON reading then route through the span overload, so ingesting a
/// value allocates the normalized string and nothing else; without it, the raw text is materialized first and
/// immediately thrown away.
/// </para>
/// <para>
/// The two overloads must agree. Write the value-typed one as a one-line delegation:
/// <c>public static string NormalizeValue(string value) =&gt; NormalizeValue(value.AsSpan());</c>
/// </para>
/// </remarks>
public interface IValueObjectSpanNormalizer
{
    /// <summary>
    /// Puts text into its canonical form.
    /// </summary>
    /// <param name="value">Text to normalize.</param>
    /// <returns>The canonical form of the text.</returns>
    static abstract string NormalizeValue(ReadOnlySpan<char> value);
}

/// <summary>
/// Declares that a string value object must match a regular expression.
/// </summary>
/// <remarks>
/// <para>
/// Implement it with a source-generated regular expression, which the regex generator can only write for code a
/// consumer wrote by hand:
/// <c>[GeneratedRegex("^[A-Z]{3}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)] public static partial Regex Pattern { get; }</c>.
/// It runs compiled under native AOT, where a <see cref="System.Text.RegularExpressions.Regex"/> built at run time
/// is interpreted, and costs nothing until it first runs.
/// </para>
/// <para>
/// The pattern runs after <c>MinLength</c> and <c>MaxLength</c>, before the known values and
/// <see cref="IValueObjectValidator{TValue}"/>, and a value it does not match is rejected as
/// <see cref="ValueObjectErrorCodes.InvalidFormat"/>. It is declared once: its text is also the OpenAPI
/// <c>pattern</c>, read off the <c>[GeneratedRegex]</c> attribute when the type compiles. That text carries no
/// <see cref="System.Text.RegularExpressions.RegexOptions"/>, so a rule like case insensitivity belongs in the
/// pattern itself.
/// </para>
/// <para>
/// It replaces the <c>Pattern</c> option of <c>[ValueObject&lt;T&gt;]</c>, which builds its regular expression at
/// run time. Implementing it on a value object that is not a string, or on an <c>[EntityId]</c>, which owns its
/// format, is a build error.
/// </para>
/// </remarks>
public interface IValueObjectPatternValidator
{
    /// <summary>
    /// Gets the regular expression a value must match.
    /// </summary>
    static abstract System.Text.RegularExpressions.Regex Pattern { get; }
}

/// <summary>
/// Declares that a value object enforces a rule its declarative constraints cannot express.
/// </summary>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// A length is better declared on <c>[ValueObject&lt;T&gt;]</c>, bounds through <see cref="IValueObjectMinimum{TValue}"/>
/// and <see cref="IValueObjectMaximum{TValue}"/>, and a pattern through <see cref="IValueObjectPatternValidator"/>,
/// where they also size the database column and describe the OpenAPI schema. Implement this for what is left: an IBAN's MOD-97 check digits, a Luhn checksum, a rule spanning several
/// characters. The declared constraints run first, so this method only sees values that already satisfy them.
/// </remarks>
public interface IValueObjectValidator<TValue>
{
    /// <summary>
    /// Decides whether a normalized value is acceptable.
    /// </summary>
    /// <param name="value">Normalized value to check.</param>
    /// <returns>Success, or the rule that rejected the value.</returns>
    static abstract ValidationResult ValidateValue(in TValue value);
}

/// <summary>
/// Declares the inclusive lower bound of a value object, as a value of its underlying type.
/// </summary>
/// <typeparam name="TValue">Underlying value type: a number, a <see cref="char"/>, a date, a time or a duration.</typeparam>
/// <remarks>
/// <para>
/// The compiler checks the type of the bound, and the bound can be any expression of that type:
/// <c>public static DateOnly Minimum => new(1900, 1, 1);</c>. The generated <c>Validate</c> refuses a smaller value
/// with <see cref="ValueObjectErrorCodes.OutOfRange"/>, and the OpenAPI schema publishes the bound, in the form the
/// JSON converter writes it.
/// </para>
/// <para>
/// The bound is a constant. The check reads it each time it runs, which costs nothing for a constant, folded into the
/// check by the JIT, while the schema reads it once, when the value object's type initializes, which the generated
/// registration does as the declaring assembly loads. A bound that changed would therefore be checked against a value
/// the schema does not publish, and a bound must neither throw nor depend on the state of the application. A bound
/// relative to the clock, "not in the future" or "within 90 days", is a rule rather than a bound: implement it in
/// <see cref="IValueObjectValidator{TValue}"/>, reading the time from a <see cref="TimeProvider"/> a test can fix.
/// </para>
/// <para>
/// Write it as an expression-bodied property, as above. An initialized property, <c>{ get; } = ...</c>, is assigned
/// with the type's other static fields, in the order they are declared: an instance created from a static field
/// declared before it, <c>public static readonly Percentage Full = Create(100);</c>, would be checked against the
/// default of the type.
/// </para>
/// </remarks>
public interface IValueObjectMinimum<TValue>
{
    /// <summary>
    /// Gets the smallest value the value object accepts.
    /// </summary>
    static abstract TValue Minimum { get; }
}

/// <summary>
/// Declares the inclusive upper bound of a value object, as a value of its underlying type.
/// </summary>
/// <typeparam name="TValue">Underlying value type: a number, a <see cref="char"/>, a date, a time or a duration.</typeparam>
/// <remarks>
/// The bound is checked, published and read as <see cref="IValueObjectMinimum{TValue}"/> describes.
/// </remarks>
public interface IValueObjectMaximum<TValue>
{
    /// <summary>
    /// Gets the largest value the value object accepts.
    /// </summary>
    static abstract TValue Maximum { get; }
}

/// <summary>
/// Declares that a value object formats itself, rather than deferring to its underlying value.
/// </summary>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// Implementing this takes over formatting entirely, including the default format, so it must handle an empty
/// or <see langword="null"/> format specifier. It is what gives a value object named formats: an IBAN printed
/// in groups of four, or masked down to its last four characters. A type that also implements
/// <see cref="IValueObjectStringFormatter{TValue}"/> never calls this one: the string formatter answers everywhere.
/// </para>
/// <para>
/// The generated <c>ToString(format, provider)</c> calls it with a stack buffer, then with a pooled buffer twice as
/// large each time it returns <see langword="false"/>, up to 1,048,576 characters, past which it throws a
/// <see cref="FormatException"/>. A format the rule does not support is therefore refused by throwing that
/// exception, not by returning <see langword="false"/>. On a <see cref="string"/> value object, text equal to the
/// value returns the string the value object holds, so formatting a value unchanged allocates nothing.
/// </para>
/// </remarks>
public interface IValueObjectFormatter<TValue>
{
    /// <summary>
    /// Writes the formatted value into a destination buffer.
    /// </summary>
    /// <param name="value">Value to format.</param>
    /// <param name="destination">Buffer to write into.</param>
    /// <param name="charsWritten">Characters written, when the buffer was large enough.</param>
    /// <param name="format">Format specifier, possibly empty.</param>
    /// <param name="provider">Format provider.</param>
    /// <returns><see langword="false"/> when the destination was too small, as the framework expects.</returns>
    static abstract bool TryFormatValue(
        in TValue value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider);
}

/// <summary>
/// Declares that a value object produces its text directly, when writing into a buffer would be wasteful.
/// </summary>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// Prefer <see cref="IValueObjectFormatter{TValue}"/>, which can format without allocating. This one exists for
/// rules whose output is naturally a string, and takes precedence over it when both are implemented.
/// </remarks>
public interface IValueObjectStringFormatter<TValue>
{
    /// <summary>
    /// Produces the text of a value.
    /// </summary>
    /// <param name="value">Value to format.</param>
    /// <param name="format">Format specifier, possibly empty.</param>
    /// <param name="provider">Format provider.</param>
    /// <returns>The formatted text.</returns>
    static abstract string FormatValue(in TValue value, ReadOnlySpan<char> format, IFormatProvider? provider);
}
