using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Reports a member that looks like a generator hook but that the generator will never call.
/// </summary>
/// <remarks>
/// <para>
/// Hooks are declared by implementing an interface, so a mis-signed one is a compiler error rather than a
/// silent no-op. What the compiler cannot catch is a correctly written rule whose interface was never declared:
/// the member sits there looking right and never runs. That is what this reports.
/// </para>
/// <para>
/// An entity identifier calls a validator or a formatter it declares, like any value object, and is held to the
/// same rule. It never calls a normalizer, since it normalizes its own format: a normalizer on one is
/// <c>VO0017</c>'s to report, and is left to it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueObjectHookAnalyzer : DiagnosticAnalyzer
{
    private const string ValueObjectAttributeName = "AdCodicem.ValueObjects.Annotations.ValueObjectAttribute`1";
    private const string EntityIdAttributeName = "AdCodicem.ValueObjects.Identifiers.EntityIdAttribute";
    private const string HookNamespace = "AdCodicem.ValueObjects";

    /// <summary>
    /// Reports a hook-shaped member on a value object that declares no matching hook interface.
    /// </summary>
    public static readonly DiagnosticDescriptor UndeclaredHook = new(
        "VO0011",
        "Value object hook is not declared",
        "'{0}' looks like a value object rule but '{1}' does not implement '{2}'. Declare the interface, or the "
        + "rule will never run.",
        "AdCodicem.ValueObjects",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A hook only runs when its interface is implemented. A member that merely has the right "
        + "name is ignored by the generator.");

    /// <summary>Hook member names, mapped to the interface that declares each of them.</summary>
    private static readonly Dictionary<string, string> Interfaces = new(StringComparer.Ordinal)
    {
        ["NormalizeValue"] = "IValueObjectNormalizer<T>",
        ["ValidateValue"] = "IValueObjectValidator<T>",
        ["TryFormatValue"] = "IValueObjectFormatter<T>",
        ["FormatValue"] = "IValueObjectStringFormatter<T>",

        // The names these hooks carried before they became interfaces, so an upgrade is not silent.
        ["NormalizeCore"] = "IValueObjectNormalizer<T>",
        ["ValidateCore"] = "IValueObjectValidator<T>",
        ["TryFormatCore"] = "IValueObjectFormatter<T>",
        ["FormatCore"] = "IValueObjectStringFormatter<T>",
    };

    /// <summary>Metadata names of the hook interfaces, to test what a type already declares.</summary>
    private static readonly Dictionary<string, string[]> Declared = new(StringComparer.Ordinal)
    {
        ["NormalizeValue"] = ["IValueObjectNormalizer`1", "IValueObjectSpanNormalizer"],
        ["ValidateValue"] = ["IValueObjectValidator`1"],
        ["TryFormatValue"] = ["IValueObjectFormatter`1"],
        ["FormatValue"] = ["IValueObjectStringFormatter`1"],
        ["NormalizeCore"] = ["IValueObjectNormalizer`1", "IValueObjectSpanNormalizer"],
        ["ValidateCore"] = ["IValueObjectValidator`1"],
        ["TryFormatCore"] = ["IValueObjectFormatter`1"],
        ["FormatCore"] = ["IValueObjectStringFormatter`1"],
    };

    /// <summary>The hook member names of a normalizer, which an entity identifier leaves to <c>VO0017</c>.</summary>
    private static readonly string[] Normalizers = ["NormalizeValue", "NormalizeCore"];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [UndeclaredHook];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var valueObjectAttribute = compilationContext.Compilation.GetTypeByMetadataName(ValueObjectAttributeName);
            if (valueObjectAttribute is null)
            {
                return;
            }

            // An identifier needs the identifiers package, which a project using only [ValueObject<T>] does not
            // reference.
            var entityIdAttribute = compilationContext.Compilation.GetTypeByMetadataName(EntityIdAttributeName);

            compilationContext.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, valueObjectAttribute, entityIdAttribute),
                SymbolKind.Method);
        });
    }

    private static void Analyze(
        SymbolAnalysisContext context,
        INamedTypeSymbol valueObjectAttribute,
        INamedTypeSymbol? entityIdAttribute)
    {
        if (context.Symbol is not IMethodSymbol { IsStatic: true } method
            || method.ContainingType is not { TypeKind: TypeKind.Struct } containingType
            || !Interfaces.TryGetValue(method.Name, out var expected))
        {
            return;
        }

        var isEntityId = Carries(containingType, entityIdAttribute);
        if ((!isEntityId && !Carries(containingType, valueObjectAttribute)) || method.DeclaringSyntaxReferences.Length == 0)
        {
            return;
        }

        // An identifier normalizes its own format and never calls a normalizer: one on an identifier is VO0017's.
        if (isEntityId && Normalizers.Contains(method.Name, StringComparer.Ordinal))
        {
            return;
        }

        var accepted = Declared[method.Name];
        var alreadyDeclared = containingType.AllInterfaces.Any(candidate =>
            candidate.ContainingNamespace.ToDisplayString() == HookNamespace
            && accepted.Contains(candidate.MetadataName, StringComparer.Ordinal));

        if (alreadyDeclared)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            UndeclaredHook,
            method.Locations[0],
            method.Name,
            containingType.Name,
            expected));
    }

    private static bool Carries(INamedTypeSymbol type, INamedTypeSymbol? attribute)
        => attribute is not null && type.GetAttributes().Any(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.AttributeClass?.OriginalDefinition, attribute));
}
