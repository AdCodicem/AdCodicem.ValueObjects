using System;
using AdCodicem.ValueObjects.Generators.Internal;

namespace AdCodicem.ValueObjects.Generators.Model;

/// <summary>
/// A named constant of a value object, declared through <c>KnownValueAttribute</c>.
/// </summary>
/// <param name="Name">Name of the generated static property.</param>
/// <param name="Literal">The underlying value as a C# literal expression.</param>
/// <param name="Description">Documentation of the generated static property.</param>
internal readonly record struct KnownValueModel(string Name, string Literal, string? Description);

/// <summary>
/// Everything the emitters need about one value object declaration.
/// </summary>
/// <remarks>
/// This is a fully equatable value: two structurally identical declarations compare equal, so an edit elsewhere
/// in the file does not invalidate the cached generated output.
/// </remarks>
internal sealed record ValueObjectModel
{
    public required string Namespace { get; init; }

    /// <summary>Gets the simple name of the declared type.</summary>
    public required string TypeName { get; init; }

    /// <summary>Gets the globally qualified name of the declared type.</summary>
    public required string QualifiedName { get; init; }

    /// <summary>Gets the enclosing type declarations, outermost first, for a nested value object.</summary>
    public required EquatableArray<string> ContainingTypes { get; init; }

    public required UnderlyingKind Kind { get; init; }

    public required string UnderlyingFullName { get; init; }

    /// <summary>Gets the file name of the generated source.</summary>
    public required string HintName { get; init; }

    /// <summary>Gets the XML summary of the declared type, reused as the OpenAPI description.</summary>
    public string? XmlSummary { get; init; }

    public string ComparisonName { get; init; } = "Ordinal";

    public bool ImplicitConversionToValue { get; init; }

    public bool ExplicitConversionFromValue { get; init; }

    public bool Arithmetic { get; init; }

    public bool IsClosedValueSet { get; init; }

    public bool AllowEmpty { get; init; }

    public string? Pattern { get; init; }

    public int MinLength { get; init; } = -1;

    public int MaxLength { get; init; } = -1;

    /// <summary>Gets the inclusive lower bound as a C# literal expression.</summary>
    public string? MinimumLiteral { get; init; }

    /// <summary>Gets the inclusive upper bound as a C# literal expression.</summary>
    public string? MaximumLiteral { get; init; }

    /// <summary>Gets the inclusive lower bound as written by the author, for the OpenAPI schema.</summary>
    public string? MinimumText { get; init; }

    /// <summary>Gets the inclusive upper bound as written by the author, for the OpenAPI schema.</summary>
    public string? MaximumText { get; init; }

    public string? SchemaFormat { get; init; }

    public string? Example { get; init; }

    public string? Description { get; init; }

    public bool HasNormalizeHook { get; init; }

    public bool HasValidateHook { get; init; }

    public bool HasTryFormatHook { get; init; }

    public bool HasFormatHook { get; init; }

    public EquatableArray<KnownValueModel> KnownValues { get; init; } = EquatableArray<KnownValueModel>.Empty;

    /// <summary>Gets the descriptor of the underlying type.</summary>
    public UnderlyingType Underlying
        => UnderlyingType.TryResolve(UnderlyingFullName, out var underlying)
            ? underlying
            : throw new InvalidOperationException($"Unsupported underlying type '{UnderlyingFullName}'.");

    /// <summary>Gets a value indicating whether the type declares constraints enforced by generated code.</summary>
    public bool HasDeclarativeRules
        => Pattern is not null
           || MinLength >= 0
           || MaxLength >= 0
           || MinimumLiteral is not null
           || MaximumLiteral is not null
           || IsClosedValueSet;
}
