using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Tells a value object from any other type, for the analyzers that report one: one that refuses its default instance,
/// brought into existence uninitialized, or one a logging method writes.
/// </summary>
internal static class ValueObjectAnnotations
{
    /// <summary>The suffix C# lets an attribute's name leave out where it is written.</summary>
    private const string AttributeSuffix = "Attribute";

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

    /// <summary>
    /// Determines whether a type carries one of the annotations that make a struct a value object the generator writes,
    /// a construction of a generic one reading its definition's.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="annotations">The annotations <see cref="Resolve"/> found.</param>
    /// <returns><see langword="true"/> when the type carries <c>[ValueObject&lt;T&gt;]</c> or <c>[EntityId]</c>.</returns>
    public static bool IsAnnotated(INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol> annotations)
        => type.OriginalDefinition.GetAttributes().Any(attribute =>
            annotations.Contains(attribute.AttributeClass!.OriginalDefinition, SymbolEqualityComparer.Default));

    /// <summary>
    /// Gets an attribute as a message or a code fix's title shows it: its class name in brackets, without the
    /// <c>Attribute</c> suffix C# lets it leave out.
    /// </summary>
    /// <param name="attributeClass">The class of an attribute.</param>
    /// <returns>The attribute as written, <c>[ProtoContract]</c> for <c>ProtoContractAttribute</c>.</returns>
    public static string Shown(INamedTypeSymbol attributeClass)
    {
        var name = attributeClass.Name;

        // An attribute class needs no suffix, and one named Attribute alone is written in full.
        return name.Length > AttributeSuffix.Length && name.EndsWith(AttributeSuffix, StringComparison.Ordinal)
            ? $"[{name.Substring(0, name.Length - AttributeSuffix.Length)}]"
            : $"[{name}]";
    }

    private static bool AllowsDefault(AttributeData attribute)
        => attribute.NamedArguments.Any(argument =>
            argument.Key == "AllowDefault" && argument.Value.Value is true);
}
