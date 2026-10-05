namespace AdCodicem.ValueObjects.Annotations;

/// <summary>
/// Marks a static member of a value object as one of its known values.
/// </summary>
/// <remarks>
/// <para>
/// The member is the author's own: a <c>static readonly</c> field, or a static get-only auto-property, of the value
/// object's type, initialized through <c>Known</c>, a factory the generator writes on every value object.
/// <c>[KnownValue] public static readonly CountryCode France = Known("FR");</c> adds the value to
/// <c>CountryCode.KnownValues</c> and surfaces it in the OpenAPI schema. Combined with <see cref="ValueSetKind.Closed"/>
/// the declared values also become the validation rule of the type. The compiler checks the name of the member and the
/// type of its value, which any expression of the underlying type can give:
/// <c>Known(new DateOnly(1970, 1, 1))</c>.
/// </para>
/// <para>
/// <c>Known</c> normalizes the value and applies every rule of the type but membership, which the value satisfies by
/// declaration. A value the rules refuse throws as the type initializes: it is reported at compile time when the argument
/// of <c>Known</c> is a constant the generator can evaluate, and would otherwise stop the application before it starts.
/// <c>Known</c> may be called nowhere else (<c>VO0037</c>), and a member that is not static, can be written, is of
/// another type or is not initialized through <c>Known</c> is <c>VO0036</c>.
/// </para>
/// <para>
/// The schema keeps the name of the member and the description beside the value
/// (<see cref="Metadata.ValueObjectSchema.KnownValueDetails"/>), and the OpenAPI integration publishes them beside the
/// <c>enum</c> of a closed set, so that a client generated from the document names the member of its enumeration
/// <c>France</c> rather than <c>FR</c>. The name is therefore part of that client's contract: renaming a known value
/// renames its member in every client generated afterwards.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class KnownValueAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KnownValueAttribute"/> class, for the member it is applied to.
    /// </summary>
    public KnownValueAttribute()
    {
    }

    /// <summary>
    /// Declared a known value on the type, by name and value. Removed: declare the member instead.
    /// </summary>
    /// <param name="name">The name the generated property took.</param>
    /// <param name="value">The underlying value, written as text for a type an attribute argument cannot carry.</param>
    /// <remarks>
    /// The name was an identifier the generator created and the value a constant or text it read under a grammar of its
    /// own. A member the author declares is checked by the compiler instead. <c>[KnownValue("France", "FR")]</c> on the
    /// type becomes <c>[KnownValue] public static readonly CountryCode France = Known("FR");</c> in it, which a code fix
    /// writes. Any minor version may remove this constructor before 1.0.0.
    /// </remarks>
    [Obsolete(
        "Declare the known value as a member of the value object, [KnownValue] public static readonly TSelf Name = "
        + "Known(value), which a code fix writes. Any minor version may remove this constructor before 1.0.0.",
        error: true,
        DiagnosticId = "VO0034")]
    public KnownValueAttribute(string name, object value)
    {
        _ = name;
        _ = value;
    }

    /// <summary>
    /// Gets or sets the description of the known value.
    /// </summary>
    /// <remarks>
    /// Kept in <see cref="Metadata.ValueObjectSchema.KnownValueDetails"/>, unless it is blank, and published with the
    /// value in the OpenAPI document, where client generators turn it into the documentation of the member. Without it,
    /// the <c>&lt;summary&gt;</c> of the member describes the value, as the summary of a value object describes it.
    /// </remarks>
    public string? Description { get; set; }
}
