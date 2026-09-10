namespace AdCodicem.ValueObjects.Identifiers;

/// <summary>
/// Marks a <c>readonly partial struct</c> as a public entity identifier and drives code generation for it.
/// </summary>
/// <remarks>
/// <para>
/// Everything <c>[ValueObject&lt;string&gt;]</c> generates is generated here too — construction, parsing,
/// formatting, equality, ordering, the JSON converter, the <c>TypeConverter</c> and the registry entry — plus
/// <c>New()</c>, <c>Prefix</c> and <c>Granularity</c>. The length constraints and the OpenAPI pattern are
/// derived from the profile and flow into the database column and the schema exactly as a hand-declared rule
/// would: the rule is still stated once.
/// </para>
/// <para>
/// This attribute lives in the package that carries its runtime rather than in the contracts package. Were it
/// shipped with the core package alone, a consumer could annotate a type and receive a compile error inside
/// generated code they cannot edit; here, without the package the attribute simply does not exist.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [EntityId("acc")]
/// public readonly partial struct AccountId;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class EntityIdAttribute : Attribute
{
    /// <summary>
    /// Initializes the annotation with the prefix identifiers of this type carry.
    /// </summary>
    /// <param name="prefix">
    /// One or more lowercase segments separated by <c>_</c>, each opening on a letter — <c>acc</c>,
    /// <c>cus</c>, <c>sk_live</c>. Reported by <c>VO0015</c> when malformed and by <c>VO0016</c> when another
    /// type in the compilation already claims it.
    /// </param>
    public EntityIdAttribute(string prefix) => Prefix = prefix;

    /// <summary>
    /// Gets the prefix identifiers of this type carry, without its trailing separator.
    /// </summary>
    /// <remarks>
    /// The prefix is what makes a mixed-up identifier fail at the boundary rather than in a repository:
    /// <c>cus_…</c> cannot be parsed as an <c>AccountId</c>. It is stored in the database for the same reason,
    /// so that even a query the application never sees cannot join two tables on each other's keys.
    /// </remarks>
    public string Prefix { get; }

    /// <summary>
    /// Gets or sets the width of the time bucket heading the body. Defaults to <see cref="IdGranularity.Hour"/>.
    /// </summary>
    /// <remarks>
    /// Pick it from the insert rate of the table: a bucket holding roughly 10⁴–10⁵ rows keeps the hot part of
    /// the index cached. It leaks the creation time at this granularity and nothing finer, and never affects
    /// how guessable an identifier is — the random part keeps its 80 bits either way.
    /// </remarks>
    public IdGranularity Granularity { get; set; } = IdGranularity.Hour;

    /// <summary>
    /// Gets or sets the description surfaced in the OpenAPI schema.
    /// </summary>
    /// <remarks>Defaults to the XML documentation summary of the declaring type when it has one.</remarks>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets an example value surfaced in the OpenAPI schema.
    /// </summary>
    /// <remarks>Defaults to an identifier of the right shape, built at compile time from the profile.</remarks>
    public string? Example { get; set; }
}
