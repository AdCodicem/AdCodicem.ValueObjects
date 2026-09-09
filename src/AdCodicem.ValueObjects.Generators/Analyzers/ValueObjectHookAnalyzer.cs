using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Reports members that look like a generator hook but are not one.
/// </summary>
/// <remarks>
/// The hooks are detected by name, so a member called <c>Normalize</c> instead of <c>NormalizeCore</c> is
/// silently ignored and the value object quietly stops normalizing. That is a failure mode worth catching at
/// build time rather than in production.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueObjectHookAnalyzer : DiagnosticAnalyzer
{
    private const string ValueObjectAttributeName = "AdCodicem.ValueObjects.Annotations.ValueObjectAttribute`1";

    /// <summary>
    /// Reports a member whose name is one letter away from a hook the generator would call.
    /// </summary>
    public static readonly DiagnosticDescriptor MisnamedHook = new(
        "VO0011",
        "Member looks like a value object hook",
        "'{0}' is declared on a value object but the generator looks for '{0}Core'. Rename it, or the rule it "
        + "implements will never run.",
        "AdCodicem.ValueObjects",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly ImmutableArray<string> HookNames = ["Normalize", "Validate", "TryFormat", "Format"];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [MisnamedHook];

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
            var attributeSymbol = compilationContext.Compilation.GetTypeByMetadataName(ValueObjectAttributeName);
            if (attributeSymbol is null)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, attributeSymbol),
                SymbolKind.Method);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol attributeSymbol)
    {
        if (context.Symbol is not IMethodSymbol { IsStatic: true } method
            || method.ContainingType is not { TypeKind: TypeKind.Struct } containingType
            || !HookNames.Contains(method.Name))
        {
            return;
        }

        var isValueObject = containingType.GetAttributes().Any(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.AttributeClass?.OriginalDefinition, attributeSymbol));

        if (!isValueObject)
        {
            return;
        }

        // The generated members carry these names too; only the author's own declarations are reported.
        if (method.DeclaringSyntaxReferences.Length == 0
            || method.DeclaredAccessibility == Accessibility.Public)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MisnamedHook,
            method.Locations[0],
            method.Name));
    }
}
