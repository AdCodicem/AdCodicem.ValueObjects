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
/// An entity identifier is a value object like any other and is held to the same rule. A value object whose
/// default state is genuinely meaningful opts out with <c>[ValueObject&lt;T&gt;(AllowDefault = true)]</c>, an
/// identifier with <c>[EntityId("acc", AllowDefault = true)]</c>.
/// </para>
/// <para>
/// It is reported where <c>default</c> or <c>new</c> is written, and nowhere else. A parameter declared
/// <c>Iban iban = default</c> is reported on its declaration; a call omitting that argument holds an implicit
/// <c>default</c> the compiler supplies, which is left alone, since fixing the declaration fixes every call.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UninitializedValueObjectAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The annotations that make a struct a value object, both carrying <c>AllowDefault</c>.</summary>
    private static readonly string[] AnnotationNames =
    [
        "AdCodicem.ValueObjects.Annotations.ValueObjectAttribute`1",
        "AdCodicem.ValueObjects.Identifiers.EntityIdAttribute",
    ];

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
            var annotations = Annotations(compilationContext.Compilation);
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

    /// <summary>
    /// Resolves the annotations the compilation can see. An identifier needs the identifiers package, which a
    /// project using only <c>[ValueObject&lt;T&gt;]</c> does not reference.
    /// </summary>
    /// <remarks>
    /// Every type of each name counts. <c>GetTypeByMetadataName</c> answers <see langword="null"/> when two referenced
    /// assemblies define the same full name - a copy of the annotations, a mismatched package - while the generator,
    /// which matches attributes by name, keeps generating for both.
    /// </remarks>
    private static ImmutableArray<INamedTypeSymbol> Annotations(Compilation compilation)
    {
        var annotations = ImmutableArray.CreateBuilder<INamedTypeSymbol>(AnnotationNames.Length);
        foreach (var name in AnnotationNames)
        {
            annotations.AddRange(compilation.GetTypesByMetadataName(name));
        }

        return annotations.ToImmutable();
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
        // `default(Iban?)` is null, not an uninitialized value object, and is perfectly legitimate. A construction of a
        // generic value object, `default(Code<Order>)`, carries the annotations of its definition and is reported.
        if (type is not INamedTypeSymbol named || named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            return;
        }

        // A struct carries at most one of the annotations: both on one type is VO0018, and nothing is generated.
        var attribute = named.GetAttributes().FirstOrDefault(candidate =>
            candidate.AttributeClass is { } attributeClass
            && annotations.Contains(attributeClass.OriginalDefinition, SymbolEqualityComparer.Default));

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
