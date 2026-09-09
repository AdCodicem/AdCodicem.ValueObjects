namespace AdCodicem.ValueObjects.Annotations;

/// <summary>
/// Declares a named constant of a value object, exposed as a static property on the generated type.
/// </summary>
/// <remarks>
/// <c>[KnownValue("France", "FR")]</c> on a <c>CountryCode</c> value object generates <c>CountryCode.France</c>,
/// adds the value to <c>CountryCode.KnownValues</c>, and surfaces it in the OpenAPI schema. Combined with
/// <see cref="ValueSetKind.Closed"/> the declared values also become the validation rule of the type.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class KnownValueAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KnownValueAttribute"/> class.
    /// </summary>
    /// <param name="name">Name of the generated static property. Must be a valid identifier.</param>
    /// <param name="value">
    /// The underlying value. Types that cannot appear as an attribute argument, such as <see cref="Guid"/>,
    /// <see cref="decimal"/> or <see cref="DateOnly"/>, are written as invariant-culture text and parsed at
    /// compile time.
    /// </param>
    public KnownValueAttribute(string name, object value)
    {
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Gets the name of the generated static property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Gets or sets the documentation of the generated static property.
    /// </summary>
    public string? Description { get; set; }
}
