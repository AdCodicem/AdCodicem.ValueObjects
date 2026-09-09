using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Reports expressions that produce a value object which never went through validation.
/// </summary>
/// <remarks>
/// <para>
/// A struct can always be brought into existence uninitialized: <c>default(Iban)</c> and <c>new Iban()</c> are
/// legal C# and skip every rule the type declares. That is the one hole a struct value object cannot close on
/// its own, and it is the reason this analyzer exists — it is what makes the struct representation, with its
/// zero allocation and its absence of null, safe to choose.
/// </para>
/// <para>
/// A value object whose default state is genuinely meaningful opts out with
/// <c>[ValueObject&lt;T&gt;(AllowDefault = true)]</c>.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UninitializedValueObjectAnalyzer : DiagnosticAnalyzer
{
    private const string ValueObjectAttributeName = "AdCodicem.ValueObjects.Annotations.ValueObjectAttribute`1";

    /// <summary>
    /// Reports an uninitialized value object.
    /// </summary>
    public static readonly DiagnosticDescriptor UninitializedValueObject = new(
        "VO0010",
        "Uninitialized value object",
        "'{0}' produced here never went through validation. Build it with Create or TryCreate, or declare it "
        + "with AllowDefault when its default state is meaningful.",
        "AdCodicem.ValueObjects",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [UninitializedValueObject];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        // Generated code legitimately assigns `default` on the rejection paths of TryCreate and TryParse.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var attributeSymbol = compilationContext.Compilation.GetTypeByMetadataName(ValueObjectAttributeName);
            if (attributeSymbol is null)
            {
                return;
            }

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeDefault(operationContext, attributeSymbol),
                OperationKind.DefaultValue);

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeCreation(operationContext, attributeSymbol),
                OperationKind.ObjectCreation);
        });
    }

    private static void AnalyzeDefault(OperationAnalysisContext context, INamedTypeSymbol attributeSymbol)
        => Report(context, context.Operation.Type, attributeSymbol);

    private static void AnalyzeCreation(OperationAnalysisContext context, INamedTypeSymbol attributeSymbol)
    {
        if (context.Operation is IObjectCreationOperation { Arguments.Length: 0 } creation)
        {
            Report(context, creation.Type, attributeSymbol);
        }
    }

    private static void Report(OperationAnalysisContext context, ITypeSymbol? type, INamedTypeSymbol attributeSymbol)
    {
        // `default(Iban?)` is null, not an uninitialized value object, and is perfectly legitimate.
        if (type is not INamedTypeSymbol named || named.IsGenericType)
        {
            return;
        }

        var attribute = named.GetAttributes().FirstOrDefault(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.AttributeClass?.OriginalDefinition, attributeSymbol));

        if (attribute is null || AllowsDefault(attribute))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            UninitializedValueObject,
            context.Operation.Syntax.GetLocation(),
            named.Name));
    }

    private static bool AllowsDefault(AttributeData attribute)
        => attribute.NamedArguments.Any(argument =>
            argument.Key == "AllowDefault" && argument.Value.Value is true);
}
