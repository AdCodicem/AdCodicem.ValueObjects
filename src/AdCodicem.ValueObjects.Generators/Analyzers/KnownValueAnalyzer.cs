using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Reports <c>Known</c> called anywhere but in the initializer of a member marked <c>[KnownValue]</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>Known</c> is the factory the generator writes on every value object for its known values. It applies every rule
/// of the type but membership, which a known value satisfies by declaration, and which a closed set cannot check while
/// its known values are created: its lookup is built from them, after them. Called anywhere else, it would create an
/// instance a closed set refuses, which nothing else can.
/// </para>
/// <para>
/// The factory is private, so only the value object itself can call it. A call is accepted only as the whole initializer
/// of a field or a property of the type that carries <c>[KnownValue]</c>; a reference to the method, which a delegate
/// could call at any time, is reported as well. The generator does not see <c>Known</c>, which it writes itself, so this
/// is an analyzer: it runs on the compilation that holds the generated code.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class KnownValueAnalyzer : DiagnosticAnalyzer
{
    private const string ValueObjectAttributeName = "AdCodicem.ValueObjects.Annotations.ValueObjectAttribute`1";
    private const string KnownValueAttributeName = "AdCodicem.ValueObjects.Annotations.KnownValueAttribute";
    private const string KnownFactory = "Known";

    /// <summary>
    /// Reports a call of <c>Known</c> outside the initializer of a known value.
    /// </summary>
    public static readonly DiagnosticDescriptor KnownOutsideKnownValue = new(
        "VO0037",
        "Known called outside a known value",
        "'{0}.Known' is called outside the initializer of a member marked [KnownValue]. It skips the membership of a "
        + "closed set, which only a known value satisfies by declaration: create the value through Create instead.",
        "AdCodicem.ValueObjects",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Known builds the known values of a value object, as the type initializes. Anywhere else, Create "
        + "applies every rule of the type, membership of a closed set included.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [KnownOutsideKnownValue];

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
            // Every type of each name counts, as ValueObjectAnnotations explains.
            var compilation = compilationContext.Compilation;
            var valueObjectAttributes = compilation.GetTypesByMetadataName(ValueObjectAttributeName);
            var knownValueAttributes = compilation.GetTypesByMetadataName(KnownValueAttributeName);
            if (valueObjectAttributes.IsEmpty || knownValueAttributes.IsEmpty)
            {
                return;
            }

            compilationContext.RegisterOperationAction(
                operationContext => Analyze(operationContext, valueObjectAttributes, knownValueAttributes),
                OperationKind.Invocation,
                OperationKind.MethodReference);
        });
    }

    private static void Analyze(
        OperationAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> valueObjectAttributes,
        ImmutableArray<INamedTypeSymbol> knownValueAttributes)
    {
        var method = context.Operation switch
        {
            IInvocationOperation invocation => invocation.TargetMethod,
            IMethodReferenceOperation reference => reference.Method,
            _ => null,
        };

        if (method is not { IsStatic: true, Name: KnownFactory, Parameters.Length: 1 }
            || !SymbolEqualityComparer.Default.Equals(method.ReturnType, method.ContainingType)
            || !Carries(method.ContainingType.OriginalDefinition, valueObjectAttributes)
            || (context.Operation is IInvocationOperation && InitializesKnownValue(context.Operation, method.ContainingType, knownValueAttributes)))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            KnownOutsideKnownValue,
            context.Operation.Syntax.GetLocation(),
            method.ContainingType.Name));
    }

    /// <summary>
    /// Tells whether a call is the whole initializer of a field or a property of the value object marked
    /// <c>[KnownValue]</c>.
    /// </summary>
    private static bool InitializesKnownValue(
        IOperation call,
        INamedTypeSymbol valueObject,
        ImmutableArray<INamedTypeSymbol> knownValueAttributes)
    {
        ImmutableArray<ISymbol> members = call.Parent switch
        {
            IFieldInitializerOperation field => [.. field.InitializedFields.Cast<ISymbol>()],
            IPropertyInitializerOperation property => [.. property.InitializedProperties.Cast<ISymbol>()],
            _ => [],
        };

        return !members.IsEmpty && members.All(member =>
            SymbolEqualityComparer.Default.Equals(member.ContainingType, valueObject)
            && member.GetAttributes().Any(attribute =>
                attribute.AttributeClass is { } attributeClass
                && knownValueAttributes.Contains(attributeClass, SymbolEqualityComparer.Default)));
    }

    private static bool Carries(INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol> attributes)
        => type.GetAttributes().Any(candidate =>
            candidate.AttributeClass is { } attributeClass
            && attributes.Contains(attributeClass.OriginalDefinition, SymbolEqualityComparer.Default));
}
