using System.Collections.Immutable;
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
/// An entity identifier is a value object like any other and is held to the same rule. A value object whose
/// default state is genuinely meaningful opts out with <c>[ValueObject&lt;T&gt;(AllowDefault = true)]</c>, an
/// identifier with <c>[EntityId("acc", AllowDefault = true)]</c>.
/// </para>
/// <para>
/// It is reported where <c>default</c> or <c>new</c> is written, and nowhere else. A parameter declared
/// <c>Iban iban = default</c> is reported on its declaration; a call omitting that argument holds an implicit
/// <c>default</c> the compiler supplies, which is left alone, since fixing the declaration fixes every call.
/// </para>
/// <para>
/// Generated code is left alone, this generator's own first: its <c>TryCreate</c> and <c>TryParse</c> assign
/// <c>default</c> on their rejection paths. The <c>new T()</c> another generator writes is
/// <see cref="GeneratedUninitializedValueObjectAnalyzer"/>'s to report, for the generators it is told about.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UninitializedValueObjectAnalyzer : DiagnosticAnalyzer
{
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

        // Generated code legitimately assigns `default` on the rejection paths of TryCreate and TryParse. What the
        // generators of a list write is GeneratedUninitializedValueObjectAnalyzer's to report, as VO0032.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var annotations = ValueObjectAnnotations.Resolve(compilationContext.Compilation);
            if (annotations.IsEmpty)
            {
                return;
            }

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeDefault(operationContext, annotations),
                OperationKind.DefaultValue);

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeCreation(operationContext, annotations),
                OperationKind.ObjectCreation);
        });
    }

    private static void AnalyzeDefault(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> annotations)
    {
        if (!context.Operation.IsImplicit)
        {
            Report(context, context.Operation.Type, annotations);
        }
    }

    private static void AnalyzeCreation(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> annotations)
    {
        if (context.Operation is IObjectCreationOperation { Arguments.Length: 0, IsImplicit: false } creation)
        {
            Report(context, creation.Type, annotations);
        }
    }

    private static void Report(
        OperationAnalysisContext context,
        ITypeSymbol? type,
        ImmutableArray<INamedTypeSymbol> annotations)
    {
        if (ValueObjectAnnotations.RefusingDefault(type, annotations) is { } valueObject)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UninitializedValueObject,
                context.Operation.Syntax.GetLocation(),
                valueObject.Name));
        }
    }
}
