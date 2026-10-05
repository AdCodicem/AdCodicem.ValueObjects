using System;
using AdCodicem.ValueObjects.Generators.Internal;

namespace AdCodicem.ValueObjects.Generators.Model;

/// <summary>
/// A known value of a value object: a static member the author declares and marks <c>KnownValueAttribute</c>.
/// </summary>
/// <param name="Name">Name of the member, which the schema publishes beside the value.</param>
/// <param name="Identifier">Name of the member as the generated code refers to it, a keyword escaped.</param>
/// <param name="Description">Description of the value: the attribute's, or the summary of the member.</param>
internal readonly record struct KnownValueModel(string Name, string Identifier, string? Description);

/// <summary>
/// The entity identifier profile of a value object declared through <c>[EntityId]</c>.
/// </summary>
/// <param name="Prefix">Prefix the identifiers carry, without their trailing separator.</param>
/// <param name="GranularityName">Name of the declared <c>IdGranularity</c> member.</param>
/// <param name="TotalLength">Exact length of an identifier, and the width of its database column.</param>
/// <remarks>
/// A record struct of primitives, so it compares structurally and an unrelated edit does not invalidate the
/// cached output of the pipeline.
/// </remarks>
internal readonly record struct EntityIdProfile(string Prefix, string GranularityName, int TotalLength);

/// <summary>
/// Everything the emitters need about one value object declaration.
/// </summary>
/// <remarks>
/// This is a fully equatable value: two structurally identical declarations compare equal, so an edit elsewhere
/// in the file does not invalidate the cached generated output.
/// </remarks>
internal sealed record ValueObjectModel
{
    /// <summary>Gets the namespace of the declared type as C# code writes it, or empty for the global namespace.</summary>
    public required string Namespace { get; init; }

    /// <summary>Gets the simple name of the declared type, as messages quote it.</summary>
    public required string TypeName { get; init; }

    /// <summary>Gets the simple name of the declared type as C# code writes it, escaped when it is a keyword.</summary>
    public required string Identifier { get; init; }

    /// <summary>Gets the globally qualified name of the declared type.</summary>
    public required string QualifiedName { get; init; }

    /// <summary>Gets the enclosing type declarations, outermost first, for a nested value object.</summary>
    public required EquatableArray<string> ContainingTypes { get; init; }

    /// <summary>
    /// Gets the type parameter list of the declared type as its declaration writes it, <c>&lt;T&gt;</c>, or empty.
    /// </summary>
    public string TypeParameters { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name a <c>cref</c> written inside the declared type uses for it, <c>Code{T}</c> for a generic one.
    /// </summary>
    public required string CrefName { get; init; }

    /// <summary>
    /// Gets the globally qualified name of the declared type as <c>typeof</c> writes it unbound,
    /// <c>global::Shop.Outer&lt;&gt;.Code</c>, or its qualified name when nothing around it is generic.
    /// </summary>
    public required string OpenQualifiedName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the type, or a type around it, has type parameters: the generated code then
    /// registers its generic definition, which the registry closes over each construction asked for.
    /// </summary>
    public bool IsGeneric { get; init; }

    /// <summary>
    /// Gets the globally qualified names of the types around the declared one that its registration goes through,
    /// outermost first, or nothing when the registration reaches the type directly.
    /// </summary>
    /// <remarks>
    /// A private or protected type is reachable only from the type declaring it. Each type on the route, from the one
    /// declaring the outermost private or protected type to the one declaring the most deeply nested, receives a step of
    /// the registration, which the assembly's registration calls on the first.
    /// </remarks>
    public EquatableArray<string> RegistrationRoute { get; init; } = EquatableArray<string>.Empty;

    /// <summary>
    /// Gets the position of the first type of <see cref="RegistrationRoute"/> among <see cref="ContainingTypes"/>.
    /// </summary>
    public int RegistrationRouteStart { get; init; }

    /// <summary>
    /// Gets the name of the class holding the registration step on each type of <see cref="RegistrationRoute"/>, which
    /// the position of the type on the route completes.
    /// </summary>
    public string RegistrationStep { get; init; } = string.Empty;

    public required UnderlyingKind Kind { get; init; }

    public required string UnderlyingFullName { get; init; }

    /// <summary>Gets the file name of the generated source.</summary>
    public required string HintName { get; init; }

    public string ComparisonName { get; init; } = "Ordinal";

    public bool ImplicitConversionToValue { get; init; }

    public bool ExplicitConversionFromValue { get; init; }

    public bool Arithmetic { get; init; }

    public bool IsClosedValueSet { get; init; }

    public bool AllowEmpty { get; init; }

    /// <summary>
    /// Whether the value is matched against the <c>Pattern</c> property of <c>IValueObjectPatternValidator</c>.
    /// </summary>
    public bool HasPatternHook { get; init; }

    /// <summary>Gets a value indicating whether the type declares its lower bound through <c>IValueObjectMinimum&lt;T&gt;</c>.</summary>
    public bool HasMinimumHook { get; init; }

    /// <summary>Gets a value indicating whether the type declares its upper bound through <c>IValueObjectMaximum&lt;T&gt;</c>.</summary>
    public bool HasMaximumHook { get; init; }

    /// <summary>
    /// Gets the text of the hook's pattern, read off its <c>[GeneratedRegex]</c> attribute, for the schema. Null
    /// when the property carries none, and the schema then asks the regular expression for its text at run time.
    /// </summary>
    public string? PatternHookText { get; init; }

    public int MinLength { get; init; } = -1;

    public int MaxLength { get; init; } = -1;

    public string? SchemaFormat { get; init; }

    /// <summary>Gets a value indicating whether the type declares its example through <c>IValueObjectExample&lt;TSelf&gt;</c>.</summary>
    public bool HasExampleHook { get; init; }

    public string? Description { get; init; }

    public bool HasNormalizeHook { get; init; }

    /// <summary>
    /// Whether a <c>NormalizeValue(ReadOnlySpan&lt;char&gt;)</c> overload exists, letting text be normalized
    /// straight from a span so that parsing allocates the normalized string and nothing else.
    /// </summary>
    public bool HasSpanNormalizeHook { get; init; }

    public bool HasValidateHook { get; init; }

    public bool HasTryFormatHook { get; init; }

    public bool HasFormatHook { get; init; }

    public EquatableArray<KnownValueModel> KnownValues { get; init; } = EquatableArray<KnownValueModel>.Empty;

    /// <summary>
    /// Gets a value indicating whether the author classifies the type as sensitive data, with an attribute derived from
    /// <c>DataClassificationAttribute</c> other than <c>NoDataClassificationAttribute</c>: the exception
    /// <c>Create</c> and <c>Parse</c> throw then leaves the rejected value out of its <c>AttemptedValue</c>.
    /// </summary>
    public bool IsClassified { get; init; }

    /// <summary>Gets the entity identifier profile, or <see langword="null"/> for an ordinary value object.</summary>
    public EntityIdProfile? Id { get; init; }

    /// <summary>Gets a value indicating whether the type is a public entity identifier.</summary>
    public bool IsEntityId => Id is not null;

    /// <summary>
    /// Gets a value indicating whether text is normalized straight from a span.
    /// </summary>
    /// <remarks>
    /// Doing so means the normalized string is the only one allocated, where going through the string overload
    /// would materialize the raw text first and immediately throw it away. An entity identifier always
    /// qualifies: the generator owns its normalization, so it can always take the span path.
    /// </remarks>
    public bool NormalizesFromSpan => HasSpanNormalizeHook || IsEntityId;

    /// <summary>Gets the descriptor of the underlying type.</summary>
    public UnderlyingType Underlying
        => UnderlyingType.TryResolve(UnderlyingFullName, out var underlying)
            ? underlying
            : throw new InvalidOperationException($"Unsupported underlying type '{UnderlyingFullName}'.");
}
