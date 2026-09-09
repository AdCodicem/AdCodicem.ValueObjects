namespace AdCodicem.ValueObjects;

/// <summary>
/// Non-generic marker implemented by every single-value value object.
/// </summary>
/// <remarks>
/// Reflection-driven integrations (OpenAPI schema transformers, EF Core conventions, model binder providers)
/// test for this interface because a single <c>IsAssignableTo</c> check is far cheaper than walking the
/// generic interface list of a type.
/// </remarks>
public interface IValueObject
{
    /// <summary>
    /// Gets the carried value, boxed.
    /// </summary>
    /// <returns>The underlying value.</returns>
    /// <remarks>Reserved for reflection-driven code paths; prefer the strongly typed <c>Value</c> property.</remarks>
    object? GetBoxedValue();
}

/// <summary>
/// A value object carrying a single value of type <typeparamref name="TValue"/>.
/// </summary>
/// <typeparam name="TValue">Underlying value type.</typeparam>
public interface IValueObject<TValue> : IValueObject
{
    /// <summary>
    /// Gets the carried value. Always normalized and always valid, unless the instance is <see langword="default"/>.
    /// </summary>
    TValue Value { get; }

    /// <inheritdoc />
    object? IValueObject.GetBoxedValue() => Value;
}

/// <summary>
/// The full contract of a single-value value object, self-referencing so that construction, parsing and
/// comparison are resolved statically without reflection or boxing.
/// </summary>
/// <typeparam name="TSelf">The value object type itself.</typeparam>
/// <typeparam name="TValue">Underlying value type.</typeparam>
/// <remarks>
/// <para>
/// Implementations are expected to be <c>readonly record struct</c>s produced by the
/// <c>AdCodicem.ValueObjects.Generators</c> source generator. Writing one by hand is supported but tedious.
/// </para>
/// <para>
/// The construction pipeline is always <c>Normalize</c> then <c>Validate</c> then assign, so a non-default
/// instance is by construction both normalized and valid.
/// </para>
/// </remarks>
public interface IValueObject<TSelf, TValue> :
    IValueObject<TValue>,
    IEquatable<TSelf>,
    IComparable<TSelf>,
    IComparable,
    ISpanParsable<TSelf>,
    ISpanFormattable
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Gets a value indicating whether this instance is the uninitialized <see langword="default"/> of its type.
    /// </summary>
    /// <remarks>
    /// <c>default(TSelf)</c> and <c>new TSelf()</c> bypass validation because the CLR always allows them for a
    /// struct. The <c>AdCodicem.ValueObjects.Analyzers</c> package reports those expressions as errors; this
    /// property is the runtime guard for values that cross a boundary the analyzer cannot see.
    /// </remarks>
    bool IsDefault { get; }

    /// <summary>
    /// Normalizes a candidate value into its canonical form.
    /// </summary>
    /// <param name="value">Candidate value.</param>
    /// <returns>The canonical form of <paramref name="value"/>.</returns>
    /// <remarks>
    /// Normalization must be idempotent: <c>Normalize(Normalize(x))</c> equals <c>Normalize(x)</c>. It must not
    /// reject values — an unnormalizable value is rejected by <see cref="Validate"/> instead.
    /// </remarks>
    static abstract TValue Normalize(TValue value);

    /// <summary>
    /// Validates an already normalized candidate value.
    /// </summary>
    /// <param name="value">Normalized candidate value.</param>
    /// <returns>The outcome of the first violated rule, or <see cref="ValidationResult.Success"/>.</returns>
    static abstract ValidationResult Validate(in TValue value);

    /// <summary>
    /// Normalizes, validates, and creates a value object.
    /// </summary>
    /// <param name="value">Candidate value.</param>
    /// <returns>The created value object.</returns>
    /// <exception cref="ValueObjectException"><paramref name="value"/> violates one of the rules.</exception>
    static abstract TSelf Create(TValue value);

    /// <summary>
    /// Normalizes, validates, and creates a value object without throwing.
    /// </summary>
    /// <param name="value">Candidate value.</param>
    /// <param name="result">The created value object, or <see langword="default"/> when the value is rejected.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> was accepted.</returns>
    static abstract bool TryCreate(TValue value, out TSelf result);

    /// <summary>
    /// Normalizes, validates, and creates a value object without throwing, reporting why a value was rejected.
    /// </summary>
    /// <param name="value">Candidate value.</param>
    /// <param name="result">The created value object, or <see langword="default"/> when the value is rejected.</param>
    /// <param name="validation">The outcome of the validation.</param>
    /// <returns><see langword="true"/> when <paramref name="value"/> was accepted.</returns>
    static abstract bool TryCreate(TValue value, out TSelf result, out ValidationResult validation);

    /// <summary>
    /// Parses text without throwing, reporting why the text was rejected.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <param name="provider">Format provider used to parse the underlying value.</param>
    /// <param name="result">The parsed value object, or <see langword="default"/> when the text is rejected.</param>
    /// <param name="validation">
    /// The outcome, distinguishing text that does not even have the shape of the underlying type from text that
    /// parses but breaks one of the type's rules.
    /// </param>
    /// <returns><see langword="true"/> when the text was accepted.</returns>
    /// <remarks>
    /// This is what every boundary wants: model binding, configuration binding and data readers all need to tell
    /// the caller which rule was violated, not merely that something went wrong.
    /// </remarks>
    static abstract bool TryParse(
        ReadOnlySpan<char> text,
        IFormatProvider? provider,
        out TSelf result,
        out ValidationResult validation);

    /// <summary>
    /// Creates a value object from a value that is already known to be normalized and valid.
    /// </summary>
    /// <param name="value">Trusted value.</param>
    /// <returns>The created value object.</returns>
    /// <remarks>
    /// This is the trusted-source fast path: it performs no work at all. It is used when materializing entities
    /// from a database the application itself wrote to. Feeding it unvalidated input defeats the whole point of
    /// the type.
    /// </remarks>
    static abstract TSelf CreateUnchecked(TValue value);

    /// <inheritdoc />
    int IComparable.CompareTo(object? obj) => obj switch
    {
        null => 1,
        TSelf other => CompareTo(other),
        _ => throw new ArgumentException($"Object must be of type {typeof(TSelf).Name}.", nameof(obj)),
    };
}
