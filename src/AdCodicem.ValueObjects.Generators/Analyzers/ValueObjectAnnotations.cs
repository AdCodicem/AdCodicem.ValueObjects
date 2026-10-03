using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Tells a value object that refuses its default instance from any other type, for the analyzers that report one
/// brought into existence uninitialized.
/// </summary>
internal static class ValueObjectAnnotations
{
    /// <summary>The annotations that make a struct a value object, both carrying <c>AllowDefault</c>.</summary>
    private static readonly string[] AnnotationNames =
    [
        "AdCodicem.ValueObjects.Annotations.ValueObjectAttribute`1",
        "AdCodicem.ValueObjects.Identifiers.EntityIdAttribute",
    ];

    /// <summary>
    /// Resolves the annotations the compilation can see. An identifier needs the identifiers package, which a
    /// project using only <c>[ValueObject&lt;T&gt;]</c> does not reference.
    /// </summary>
    /// <remarks>
    /// Every type of each name counts. <c>GetTypeByMetadataName</c> answers <see langword="null"/> when two referenced
    /// assemblies define the same full name - a copy of the annotations, a mismatched package - while the generator,
    /// which matches attributes by name, keeps generating for both.
    /// </remarks>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <returns>The annotations, empty when the compilation references none.</returns>
    public static ImmutableArray<INamedTypeSymbol> Resolve(Compilation compilation)
    {
        var annotations = ImmutableArray.CreateBuilder<INamedTypeSymbol>(AnnotationNames.Length);
        foreach (var name in AnnotationNames)
        {
            annotations.AddRange(compilation.GetTypesByMetadataName(name));
        }

        return annotations.ToImmutable();
    }

    /// <summary>
    /// Gets the value object a type is, when that value object refuses its default instance.
    /// </summary>
    /// <param name="type">The type of the expression that produces an instance.</param>
    /// <param name="annotations">The annotations <see cref="Resolve"/> found.</param>
    /// <returns>
    /// The value object, or <see langword="null"/> when the type is not one, is a nullable one, or declares
    /// <c>AllowDefault = true</c>.
    /// </returns>
    public static INamedTypeSymbol? RefusingDefault(ITypeSymbol? type, ImmutableArray<INamedTypeSymbol> annotations)
    {
        // `default(Iban?)` is null, not an uninitialized value object, and is perfectly legitimate. A construction of a
        // generic value object, `default(Code<Order>)`, carries the annotations of its definition and is reported.
        if (type is not INamedTypeSymbol named || named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return null;
        }

        // A struct carries at most one of the annotations: both on one type is VO0018, and nothing is generated.
        var attribute = named.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass is { } attributeClass
            && annotations.Contains(attributeClass.OriginalDefinition, SymbolEqualityComparer.Default));

        return attribute is null || AllowsDefault(attribute) ? null : named;
    }

    private static bool AllowsDefault(AttributeData attribute)
        => attribute.NamedArguments.Any(argument =>
            argument.Key == "AllowDefault" && argument.Value.Value is true);
}
