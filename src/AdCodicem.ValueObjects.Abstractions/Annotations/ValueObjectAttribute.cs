namespace AdCodicem.ValueObjects.Annotations;

/// <summary>
/// Marks a <c>readonly partial record struct</c> as a single-value value object and drives code generation for it.
/// </summary>
/// <typeparam name="TValue">
/// Underlying value type. Supported types are <see cref="string"/>, <see cref="Guid"/>, <see cref="bool"/>,
/// <see cref="char"/>, every built-in integer type, <see cref="decimal"/>, <see cref="double"/>,
/// <see cref="float"/>, <see cref="DateOnly"/>, <see cref="TimeOnly"/>, <see cref="DateTime"/>,
/// <see cref="DateTimeOffset"/> and <see cref="TimeSpan"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// The declaring type opts into two hooks, both optional and both detected by name:
/// <c>private static TValue NormalizeCore(TValue value)</c> and
/// <c>private static ValidationResult ValidateCore(in TValue value)</c>. Declarative constraints set on this
/// attribute (<see cref="Pattern"/>, <see cref="MinLength"/>, <see cref="Minimum"/>) are checked first and
/// also feed the generated OpenAPI schema, so a rule is stated once and enforced everywhere.
/// </para>
/// <para>
/// A third hook backs named formats without intermediate allocations:
/// <c>private static bool TryFormatCore(in TValue value, Span&lt;char&gt; destination, out int charsWritten,
/// ReadOnlySpan&lt;char&gt; format, IFormatProvider? provider)</c>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ValueObjectAttribute<TValue> : Attribute
{
    /// <summary>
    /// Gets or sets how two values are compared for equality, ordering and hashing.
    /// </summary>
    /// <remarks>
    /// Only meaningful when the underlying type is <see cref="string"/>. Defaults to
    /// <see cref="StringComparison.Ordinal"/>: culture-independent, the fastest option, and the only one an
    /// EF Core provider can translate faithfully. Choose <see cref="StringComparison.OrdinalIgnoreCase"/> when
    /// the value is not case-normalized, and make sure the database collation agrees.
    /// </remarks>
    public StringComparison Comparison { get; set; } = StringComparison.Ordinal;

    /// <summary>
    /// Gets or sets a value indicating whether an implicit conversion to the underlying value is generated.
    /// </summary>
    /// <remarks>Reading stays terse (<c>string s = iban;</c>) while construction remains explicit and validated.</remarks>
    public bool ImplicitConversionToValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an explicit conversion from the underlying value is generated.
    /// </summary>
    /// <remarks>The conversion validates, and throws <see cref="ValueObjectException"/> on a rejected value.</remarks>
    public bool ExplicitConversionFromValue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether arithmetic operators and <see cref="INumericValueObject{TSelf, TValue}"/> are generated.
    /// </summary>
    /// <remarks>Requires a numeric underlying type. Every result is re-validated.</remarks>
    public bool Arithmetic { get; set; }

    /// <summary>
    /// Gets or sets whether the type accepts any valid value or only the declared <see cref="KnownValueAttribute"/> ones.
    /// </summary>
    public ValueSetKind ValueSet { get; set; } = ValueSetKind.Open;

    /// <summary>
    /// Gets or sets a value indicating whether an empty string is accepted.
    /// </summary>
    /// <remarks>
    /// Only meaningful for <see cref="string"/>. A <see langword="null"/> value is always rejected: a value
    /// object that may be absent is expressed as a nullable value object, never as one wrapping <c>null</c>.
    /// </remarks>
    public bool AllowEmpty { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the analyzer tolerates <c>default</c> and parameterless construction.
    /// </summary>
    /// <remarks>
    /// Those expressions produce an instance that never went through validation. They are reported as errors by
    /// <c>AdCodicem.ValueObjects.Analyzers</c> unless this is set, which is occasionally needed for a value
    /// object whose default state is meaningful, such as a sequence number starting at zero.
    /// </remarks>
    public bool AllowDefault { get; set; }

    /// <summary>
    /// Gets or sets a regular expression the normalized value must match.
    /// </summary>
    /// <remarks>
    /// Compiled through <c>[GeneratedRegex]</c>, so matching is done by generated code with no runtime regex
    /// parsing. Also emitted as the <c>pattern</c> keyword of the OpenAPI schema.
    /// </remarks>
    [StringSyntax(StringSyntaxAttribute.Regex)]
    public string? Pattern { get; set; }

    /// <summary>
    /// Gets or sets the minimum accepted length. A negative value means unconstrained.
    /// </summary>
    /// <remarks>Only meaningful for <see cref="string"/>. Also emitted as the <c>minLength</c> OpenAPI keyword.</remarks>
    public int MinLength { get; set; } = -1;

    /// <summary>
    /// Gets or sets the maximum accepted length. A negative value means unconstrained.
    /// </summary>
    /// <remarks>
    /// Only meaningful for <see cref="string"/>. Emitted as the <c>maxLength</c> OpenAPI keyword and used by the
    /// EF Core integration to size the column, so the value object maps to a bounded column rather than an
    /// unbounded one.
    /// </remarks>
    public int MaxLength { get; set; } = -1;

    /// <summary>
    /// Gets or sets the inclusive lower bound, written in invariant culture.
    /// </summary>
    /// <remarks>
    /// Expressed as text so that <see cref="decimal"/>, <see cref="DateOnly"/> or <see cref="TimeSpan"/> bounds
    /// keep full precision; attribute arguments cannot carry those types. Parsed at compile time and reported
    /// as a diagnostic when malformed. Also emitted as the <c>minimum</c> OpenAPI keyword.
    /// </remarks>
    public string? Minimum { get; set; }

    /// <summary>
    /// Gets or sets the inclusive upper bound, written in invariant culture.
    /// </summary>
    /// <inheritdoc cref="Minimum" path="/remarks"/>
    public string? Maximum { get; set; }

    /// <summary>
    /// Gets or sets the value of the OpenAPI <c>format</c> keyword for the generated schema.
    /// </summary>
    /// <remarks>Defaults to the natural format of the underlying type, such as <c>uuid</c>, <c>date</c> or <c>int64</c>.</remarks>
    public string? SchemaFormat { get; set; }

    /// <summary>
    /// Gets or sets an example value surfaced in the OpenAPI schema.
    /// </summary>
    public string? Example { get; set; }

    /// <summary>
    /// Gets or sets the description surfaced in the OpenAPI schema.
    /// </summary>
    /// <remarks>Defaults to the XML documentation summary of the declaring type when it is available.</remarks>
    public string? Description { get; set; }
}
