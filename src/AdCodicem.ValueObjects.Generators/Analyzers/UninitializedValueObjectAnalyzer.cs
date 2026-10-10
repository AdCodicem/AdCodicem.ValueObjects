using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
/// <c>Iban iban = default</c> is reported on its declaration, whether a method, a lambda or a local function declares
/// it; a call omitting that argument holds an implicit <c>default</c> the compiler supplies, which is left alone, since
/// fixing the declaration fixes every call. A minimal API handler is where a lambda's parameter usually takes a
/// default value, and the Request Delegate Generator hands <c>(Iban iban = default) =&gt; …</c> a default instance for
/// a request that leaves the value out.
/// </para>
/// <para>
/// The compiler hands an analyzer the default value of a member's parameter as an operation, but not that of a lambda's
/// or a local function's parameter, which belongs to no operation block: that one is read off the declaration, and
/// bound through the semantic model into the same parameter initializer.
/// </para>
/// <para>
/// Generated code is left alone, this generator's own first: its <c>TryCreate</c> and <c>TryParse</c> assign
/// <c>default</c> on their rejection paths, and the Request Delegate Generator writes a lambda whose parameter defaults
/// to <c>default</c> for each handler that declares one. The <c>new T()</c> another generator writes is
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
                operationContext => AnalyzeOperation(operationContext, annotations),
                OperationKind.DefaultValue,
                OperationKind.ObjectCreation);

            // The default value of a lambda's or a local function's parameter is in no operation block the compiler
            // hands an analyzer, so the action above never sees it.
            compilationContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeDefaultValues(
                    syntaxContext,
                    ((ParenthesizedLambdaExpressionSyntax)syntaxContext.Node).ParameterList,
                    annotations),
                SyntaxKind.ParenthesizedLambdaExpression);

            compilationContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeDefaultValues(
                    syntaxContext,
                    ((LocalFunctionStatementSyntax)syntaxContext.Node).ParameterList,
                    annotations),
                SyntaxKind.LocalFunctionStatement);
        });
    }

    private static void AnalyzeOperation(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> annotations)
    {
        if (Uninitialized(context.Operation, annotations) is { } diagnostic)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    /// Reports what the default values of a lambda's or a local function's parameters produce, as the operation action
    /// reports what those of a member's parameters do.
    /// </summary>
    private static void AnalyzeDefaultValues(
        SyntaxNodeAnalysisContext context,
        ParameterListSyntax parameters,
        ImmutableArray<INamedTypeSymbol> annotations)
    {
        foreach (var parameter in parameters.Parameters)
        {
            // Bound as a whole, the initializer is the operation a member's parameter is handed as. Its value alone is
            // not: the semantic model binds a parenthesized expression, `(default)`, to no operation.
            if (parameter.Default is not { } initializer
                || context.SemanticModel.GetOperation(initializer, context.CancellationToken) is not { } value)
            {
                continue;
            }

            foreach (var operation in value.Descendants())
            {
                if (Uninitialized(operation, annotations) is { } diagnostic)
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    /// <summary>
    /// Gets the diagnostic for an operation that produces a value object refusing its default instance without
    /// validating it: a <c>default</c> or a construction without arguments that is written, not one the compiler
    /// supplies.
    /// </summary>
    /// <returns>The diagnostic, or <see langword="null"/> when the operation produces no such value object.</returns>
    private static Diagnostic? Uninitialized(IOperation operation, ImmutableArray<INamedTypeSymbol> annotations)
        => operation is IDefaultValueOperation { IsImplicit: false }
               or IObjectCreationOperation { Arguments.Length: 0, IsImplicit: false }
           && ValueObjectAnnotations.RefusingDefault(operation.Type, annotations) is { } valueObject
            ? Diagnostic.Create(UninitializedValueObject, operation.Syntax.GetLocation(), valueObject.Name)
            : null;
}
