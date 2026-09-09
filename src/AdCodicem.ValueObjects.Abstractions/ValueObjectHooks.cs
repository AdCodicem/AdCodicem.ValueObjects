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
/// Declares that a value object enforces a rule its declarative constraints cannot express.
/// </summary>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// Length, pattern and bounds are better declared on <c>[ValueObject&lt;T&gt;]</c>, where they also size the
/// database column and describe the OpenAPI schema. Implement this for what is left: an IBAN's MOD-97 check
/// digits, a Luhn checksum, a rule spanning several characters. The declared constraints run first, so this
/// method only sees values that already satisfy them.
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
/// Declares that a value object formats itself, rather than deferring to its underlying value.
/// </summary>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// Implementing this takes over formatting entirely, including the default format, so it must handle an empty
/// or <see langword="null"/> format specifier. It is what gives a value object named formats: an IBAN printed
/// in groups of four, or masked down to its last four characters.
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
