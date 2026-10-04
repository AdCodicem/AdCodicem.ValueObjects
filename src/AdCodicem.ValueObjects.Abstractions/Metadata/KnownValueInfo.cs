namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// A value declared through <c>[KnownValue]</c>: the underlying value, the name of the static property the generator
/// creates for it, and the description the attribute gives it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ValueObjectSchema.KnownValueDetails"/> lists one per known value, in declaration order. The OpenAPI
/// integration publishes the names beside the <c>enum</c> of a closed value set, so that a client generated from the
/// document names the members of its enumeration as the server code names its constants, <c>France</c> rather than
/// <c>FR</c>.
/// </para>
/// <para>
/// The name therefore becomes part of the contract a generated client holds: renaming a known value renames that member
/// in every client generated afterwards, as renaming a member of an enumeration would.
/// </para>
/// </remarks>
public sealed record KnownValueInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KnownValueInfo"/> record.
    /// </summary>
    /// <param name="value">
    /// The underlying value, as <see cref="ValueObjectSchema.KnownValues"/> holds it: normalized, and of the underlying
    /// type wherever the generator wrote the schema.
    /// </param>
    /// <param name="name">The name of the known value, the name of its static property on the value object.</param>
    /// <param name="description">Its description, if any.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> or <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or consists only of white space.</exception>
    public KnownValueInfo(object value, string name, string? description = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Value = value;
        Name = name;
        Description = description;
    }

    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Gets the name of the known value, the name of its static property on the value object.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the description of the known value, if one was declared.
    /// </summary>
    public string? Description { get; }
}
