using System.Collections.Immutable;
using AdCodicem.ValueObjects.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Reports a value object whose own declaration lists no parsable interface, in a project where the Request Delegate
/// Generator writes minimal API binding.
/// </summary>
/// <remarks>
/// <para>
/// The Request Delegate Generator (RDG) is a source generator, and source generators never see each other's output. It
/// reads a value object declared in its own project without the partial this generator adds, so without
/// <c>IParsable&lt;T&gt;</c>, and binds it from the request body: a route value is answered with a 400, and a query
/// value is bound to <see langword="null"/> without a word. The build stays clean. A value object from a referenced
/// assembly is unaffected, its interfaces being in metadata.
/// </para>
/// <para>
/// What the RDG reads is the consumer's syntax, so the remedy lives there: an interface listed on the declaration is
/// seen, and the members it requires still come from the generator. Any interface that brings
/// <c>IParsable&lt;TSelf&gt;</c> will do; the code fix lists the contract the generator implements,
/// <c>IValueObject&lt;TSelf, TValue&gt;</c>, <c>INumericValueObject&lt;TSelf, TValue&gt;</c> for an arithmetic value
/// object, or <c>IEntityId&lt;TSelf&gt;</c>.
/// </para>
/// <para>
/// The SDK turns the RDG on in every build of a project setting <c>PublishAot</c> or <c>PublishTrimmed</c>, not only
/// when it publishes, through <c>EnableRequestDelegateGenerator</c>, which the package makes visible to the compiler.
/// The same property is set in a console application, where no RDG runs, so the compilation must also reference
/// ASP.NET Core's endpoint routing. Every value object of such a project is reported, whether an endpoint binds it or
/// not: following the <c>Map*</c> calls, and the types gathered with <c>[AsParameters]</c>, would cost more than the one
/// interface the fix lists, which is harmless where no RDG runs.
/// </para>
/// <para>
/// Generated code is not analyzed: what the RDG cannot see, this generator's partial first among it, is generated, and
/// a generated declaration that listed the interface would not be seen either.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequestDelegateGeneratorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The key under which the compiler sees <c>EnableRequestDelegateGenerator</c>, once the package's build props have
    /// listed it as a <c>CompilerVisibleProperty</c>.
    /// </summary>
    internal const string EnabledKey = "build_property.EnableRequestDelegateGenerator";

    /// <summary>The interface every endpoint mapping extends, present in any compilation that maps a minimal API.</summary>
    private const string EndpointRouteBuilderName = "Microsoft.AspNetCore.Routing.IEndpointRouteBuilder";

    /// <summary>What the RDG looks for on a parameter's type, and what <c>ISpanParsable&lt;T&gt;</c> and the contracts extend.</summary>
    private const string ParsableName = "System.IParsable`1";

    private const string ValueObjectContractName = "AdCodicem.ValueObjects.IValueObject`2";

    private const string NumericContractName = "AdCodicem.ValueObjects.INumericValueObject`2";

    private const string EntityIdContractName = "AdCodicem.ValueObjects.Identifiers.IEntityId`1";

    /// <summary>The format the message names a type in: as the consumer writes it, keywords included.</summary>
    private static readonly SymbolDisplayFormat MessageFormat = SymbolDisplayFormat.CSharpShortErrorMessageFormat;

    /// <summary>
    /// Reports a value object that the Request Delegate Generator would bind from the request body.
    /// </summary>
    public static readonly DiagnosticDescriptor HiddenFromRequestDelegateGenerator = new(
        "VO0033",
        "Value object hidden from the Request Delegate Generator",
        "The Request Delegate Generator cannot see what the generator adds to '{0}', so a minimal API would bind it "
        + "from the request body rather than from a route value or the query string. List '{1}' on its declaration, "
        + "or declare it in another project.",
        "AdCodicem.ValueObjects",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Under PublishAot, PublishTrimmed or EnableRequestDelegateGenerator, minimal API binding is "
        + "written at build time by the Request Delegate Generator, which reads the project without the output of "
        + "other generators. It does not see the IParsable<T> a value object of the same project gets, and binds that "
        + "value object from the request body: a route value is refused with a 400 and a query value is bound to null. "
        + "An interface listed on the declaration is seen.",
        helpLinkUri: "https://adcodicem.github.io/AdCodicem.ValueObjects/docs/how-to/aspnet-core"
        + "#the-request-delegate-generator");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [HiddenFromRequestDelegateGenerator];

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
            var compilation = compilationContext.Compilation;
            if (IsEnabled(compilationContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions)
                && !compilation.GetTypesByMetadataName(EndpointRouteBuilderName).IsEmpty
                && Reachable(compilation, ParsableName) is { } parsable)
            {
                compilationContext.RegisterSymbolStartAction(
                    symbolContext => AnalyzeType(symbolContext, parsable),
                    SymbolKind.NamedType);
            }
        });
    }

    /// <summary>
    /// Gets the contract the generator implements on a value object, the interface the code fix lists.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="compilation">The compilation declaring it.</param>
    /// <returns>
    /// The contract, constructed over the type, or <see langword="null"/> when the generator implements none: the
    /// type is not a readonly struct, record and ref structs aside (<c>VO0002</c>), annotated with exactly one of
    /// <c>[ValueObject&lt;T&gt;]</c> and <c>[EntityId]</c>, or its underlying type is not supported.
    /// </returns>
    internal static INamedTypeSymbol? Contract(INamedTypeSymbol type, Compilation compilation)
    {
        // The shapes VO0002 refuses get nothing from the generator, so nothing the RDG could miss.
        if (type.TypeKind != TypeKind.Struct || !type.IsReadOnly || type.IsRecord || type.IsRefLikeType)
        {
            return null;
        }

        var valueObject = ValueObjectGenerator.FindAttribute(type, ValueObjectGenerator.ValueObjectAttributeName);
        var entityId = ValueObjectGenerator.CarriesAttribute(type, ValueObjectGenerator.EntityIdAttributeName);

        // Both annotations on one type is VO0018, and nothing is generated for it.
        if (entityId)
        {
            return valueObject is null ? Reachable(compilation, EntityIdContractName)?.Construct(type) : null;
        }

        if (valueObject?.AttributeClass?.TypeArguments.FirstOrDefault() is not { } value
            || !UnderlyingType.TryResolve(value.ToDisplayString(ValueObjectGenerator.QualifiedFormat), out var underlying))
        {
            return null;
        }

        // Arithmetic over a type that is not a number is VO0007, and the generator then implements the plain contract.
        var arithmetic = underlying.IsNumeric
            && valueObject.NamedArguments.Any(argument => argument.Key == "Arithmetic" && argument.Value.Value is true);

        return Reachable(compilation, arithmetic ? NumericContractName : ValueObjectContractName)
            ?.Construct(type, value);
    }

    /// <summary>
    /// Resolves a type by its full name as the compiler binds it from the compilation's own code, the generated partial
    /// included: a declaration of the compilation itself wins, as it does for the compiler (CS0436), and otherwise the
    /// one declaration the compilation can reach.
    /// </summary>
    /// <remarks>
    /// <c>GetTypeByMetadataName</c> answers <see langword="null"/> as soon as two assemblies declare the name, an
    /// internal copy in a referenced assembly included, while the generated code still binds the public type, the
    /// only one it can reach, and the RDG still misses it.
    /// </remarks>
    /// <param name="compilation">The compilation being analyzed.</param>
    /// <param name="metadataName">The type's full metadata name.</param>
    /// <returns>
    /// The type, or <see langword="null"/> when the compilation reaches none, or more than one, which the compiler
    /// refuses as ambiguous (CS0433).
    /// </returns>
    private static INamedTypeSymbol? Reachable(Compilation compilation, string metadataName)
    {
        INamedTypeSymbol? reachable = null;
        foreach (var candidate in compilation.GetTypesByMetadataName(metadataName))
        {
            if (SymbolEqualityComparer.Default.Equals(candidate.ContainingAssembly, compilation.Assembly))
            {
                return candidate;
            }

            if (!compilation.IsSymbolAccessibleWithin(candidate, compilation.Assembly))
            {
                continue;
            }

            if (reachable is not null)
            {
                return null;
            }

            reachable = candidate;
        }

        return reachable;
    }

    private static bool IsEnabled(AnalyzerConfigOptions options)
        => options.TryGetValue(EnabledKey, out var enabled)
           && string.Equals(enabled.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static void AnalyzeType(SymbolStartAnalysisContext context, INamedTypeSymbol parsable)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (Contract(type, context.Compilation) is not { } contract)
        {
            return;
        }

        var parsableOfType = parsable.Construct(type);

        // Written by the declarations of the type, each analyzed on its own, possibly at once; read once they all have.
        var listed = 0;

        // Run on each declaration of the type itself: one nested in it is analyzed in the scope of its own symbol.
        context.RegisterSyntaxNodeAction(
            nodeContext =>
            {
                var declaration = (TypeDeclarationSyntax)nodeContext.Node;
                if (Lists(declaration, parsableOfType, nodeContext.SemanticModel, nodeContext.CancellationToken))
                {
                    Interlocked.Exchange(ref listed, 1);
                }
            },
            SyntaxKind.StructDeclaration);

        context.RegisterSymbolEndAction(endContext =>
        {
            if (Volatile.Read(ref listed) == 0)
            {
                endContext.ReportDiagnostic(Diagnostic.Create(
                    HiddenFromRequestDelegateGenerator,
                    Location(type, endContext.CancellationToken),
                    type.ToDisplayString(MessageFormat),
                    contract.ToDisplayString(MessageFormat)));
            }
        });
    }

    /// <summary>
    /// Determines whether one declaration of a type lists an interface that is, or extends, <c>IParsable</c> of that type.
    /// </summary>
    private static bool Lists(
        TypeDeclarationSyntax declaration,
        INamedTypeSymbol parsable,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
        => declaration.BaseList is { } baseList
           && baseList.Types.Any(baseType =>
               semanticModel.GetTypeInfo(baseType.Type, cancellationToken).Type is INamedTypeSymbol listed
               && (SymbolEqualityComparer.Default.Equals(listed, parsable)
                   || listed.AllInterfaces.Contains(parsable, SymbolEqualityComparer.Default)));

    /// <summary>
    /// Gets where to report: the name of the declaration carrying the annotation, which only the consumer writes.
    /// </summary>
    private static Location Location(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        var annotation = ValueObjectGenerator.FindAttribute(type, ValueObjectGenerator.ValueObjectAttributeName)
            ?? ValueObjectGenerator.FindAttribute(type, ValueObjectGenerator.EntityIdAttributeName);

        return annotation!.ApplicationSyntaxReference!.GetSyntax(cancellationToken)
            .FirstAncestorOrSelf<TypeDeclarationSyntax>()!
            .Identifier.GetLocation();
    }
}
