namespace AdCodicem.ValueObjects.Annotations;

/// <summary>
/// Marks a <c>readonly partial struct</c> as a single-value value object and drives code generation for it.
/// </summary>
/// <typeparam name="TValue">
/// Underlying value type. Supported types are <see cref="string"/>, <see cref="Guid"/>, <see cref="bool"/>,
/// <see cref="char"/>, every built-in integer type, <see cref="decimal"/>, <see cref="double"/>,
/// <see cref="float"/>, <see cref="DateOnly"/>, <see cref="TimeOnly"/>, <see cref="DateTime"/>,
/// <see cref="DateTimeOffset"/> and <see cref="TimeSpan"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// The declaring type opts into a rule by implementing the interface that declares it, so the compiler checks
/// its signature: <see cref="IValueObjectNormalizer{TValue}"/>, <see cref="IValueObjectSpanNormalizer"/>,
/// <see cref="IValueObjectPatternValidator"/>, <see cref="IValueObjectMinimum{TValue}"/>,
/// <see cref="IValueObjectMaximum{TValue}"/>, <see cref="IValueObjectValidator{TValue}"/>,
/// <see cref="IValueObjectFormatter{TValue}"/>, <see cref="IValueObjectStringFormatter{TValue}"/> and
/// <see cref="IValueObjectExample{TSelf}"/>. All are optional, and a rule written without its interface is reported as
/// <c>VO0011</c> rather than silently ignored. Its known values are static members it declares, marked
/// <see cref="KnownValueAttribute"/>.
/// </para>
/// <para>
/// Declarative constraints set on this attribute (<see cref="MinLength"/>, <see cref="MaxLength"/>), like the pattern
/// of <see cref="IValueObjectPatternValidator"/> and the bounds of <see cref="IValueObjectMinimum{TValue}"/> and
/// <see cref="IValueObjectMaximum{TValue}"/>, are checked before <see cref="IValueObjectValidator{TValue}"/> runs, and
/// also feed the generated OpenAPI schema, so a rule is stated once and enforced everywhere.
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
    /// Gets or sets whether the type accepts any valid value or only the members it marks <see cref="KnownValueAttribute"/>.
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
    /// the analyzers shipped with <c>AdCodicem.ValueObjects</c> unless this is set, which is occasionally needed
    /// for a value object whose default state is meaningful, such as a sequence number starting at zero. They are
    /// reported where <c>default</c> or <c>new</c> is written: a parameter defaulting to <c>default</c> on its
    /// declaration, not at each call that leaves the argument out. The same holds in the code another source
    /// generator writes, such as a Riok.Mapperly mapping, where they are <c>VO0032</c> rather than <c>VO0010</c>.
    /// </remarks>
    public bool AllowDefault { get; set; }

    /// <summary>
    /// Was a regular expression the normalized value had to match. Removed: implement
    /// <see cref="IValueObjectPatternValidator"/> instead.
    /// </summary>
    /// <remarks>
    /// The option compiled its regular expression at run time, which native AOT interprets. Setting it is
    /// <c>VO0021</c>, a compile error, and nothing reads it: <see cref="IValueObjectPatternValidator"/> takes a
    /// <c>[GeneratedRegex]</c> the author writes, which the regex generator compiles. Any minor version may remove it
    /// before 1.0.0.
    /// </remarks>
    [Obsolete(
        "Implement IValueObjectPatternValidator with a [GeneratedRegex] partial property instead. Pattern is no longer "
        + "read, and any minor version may remove it before 1.0.0.",
        error: true,
        DiagnosticId = "VO0021")]
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
    /// Was the inclusive lower bound, written as text. Removed: implement <see cref="IValueObjectMinimum{TValue}"/>
    /// instead.
    /// </summary>
    /// <remarks>
    /// The text was read under a grammar of one form per underlying type, which the compiler could not check. Setting
    /// it is <c>VO0028</c>, a compile error, and nothing reads it: <see cref="IValueObjectMinimum{TValue}"/> declares
    /// the bound as a value of the underlying type. Any minor version may remove it before 1.0.0.
    /// </remarks>
    [Obsolete(
        "Implement IValueObjectMinimum<T> with a static Minimum property of the underlying type instead. Minimum is no "
        + "longer read, and any minor version may remove it before 1.0.0.",
        error: true,
        DiagnosticId = "VO0028")]
    public string? Minimum { get; set; }

    /// <summary>
    /// Was the inclusive upper bound, written as text. Removed: implement <see cref="IValueObjectMaximum{TValue}"/>
    /// instead.
    /// </summary>
    /// <remarks>
    /// Setting it is <c>VO0028</c>, a compile error, and nothing reads it, as for <see cref="Minimum"/>. Any minor
    /// version may remove it before 1.0.0.
    /// </remarks>
    [Obsolete(
        "Implement IValueObjectMaximum<T> with a static Maximum property of the underlying type instead. Maximum is no "
        + "longer read, and any minor version may remove it before 1.0.0.",
        error: true,
        DiagnosticId = "VO0028")]
    public string? Maximum { get; set; }

    /// <summary>
    /// Gets or sets the value of the OpenAPI <c>format</c> keyword for the generated schema.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defaults to the natural format of the underlying type, such as <c>uuid</c>, <c>date</c>, <c>date-time</c> for a
    /// <see cref="DateTimeOffset"/>, or <c>int64</c>. Three types have none, and are documented with the pattern of the
    /// form they are written in instead. A <see cref="TimeSpan"/> is written in the invariant constant form
    /// <c>[-][d.]hh:mm:ss[.fffffff]</c>, not the ISO 8601 form the <c>duration</c> format names. A
    /// <see cref="TimeOnly"/> and a <see cref="DateTime"/> are written without the offset RFC 3339 requires of the
    /// <c>time</c> and <c>date-time</c> formats: a time of day never has one, and a <see cref="DateTime"/> of
    /// <see cref="DateTimeKind.Unspecified"/> is written without one.
    /// </para>
    /// <para>
    /// Set it to <c>date-time</c> on a value object over <see cref="DateTime"/> whose normalizer guarantees a kind, such
    /// as <see cref="DateTimeKind.Utc"/>, which is written with its offset.
    /// </para>
    /// </remarks>
    public string? SchemaFormat { get; set; }

    /// <summary>
    /// Was an example value surfaced in the OpenAPI schema, written as text. Removed: implement
    /// <see cref="IValueObjectExample{TSelf}"/> instead.
    /// </summary>
    /// <remarks>
    /// The text was parsed at compile time and again at run time. Setting it is <c>VO0035</c>, a compile error, and
    /// nothing reads it: <see cref="IValueObjectExample{TSelf}"/> declares the example as an instance of the value
    /// object. Any minor version may remove it before 1.0.0.
    /// </remarks>
    [Obsolete(
        "Implement IValueObjectExample<TSelf> with a static Example property of the value object's type instead. Example "
        + "is no longer read, and any minor version may remove it before 1.0.0.",
        error: true,
        DiagnosticId = "VO0035")]
    public string? Example { get; set; }

    /// <summary>
    /// Gets or sets the description surfaced in the OpenAPI schema.
    /// </summary>
    /// <remarks>Defaults to the XML documentation summary of the declaring type when it is available.</remarks>
    public string? Description { get; set; }
}
