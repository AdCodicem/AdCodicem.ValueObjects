using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Reports a <c>[ParsesAs]</c> or a <c>[NotDefault]</c> that would throw when a value is validated, and, under
/// <c>PublishAot</c>, a <c>[ParsesAs(typeof(T))]</c> a native binary finds only once the value object is registered by
/// hand.
/// </summary>
/// <remarks>
/// <para>
/// The attributes check a member when a value is validated, which .NET 10's <c>AddValidation()</c> turns into a 400
/// carrying the exception's text, and .NET 11's into a 500: a member that holds no text, a type that is no value object,
/// are found here instead. The attribute is read where it is written, on a property, a field or a parameter, a lambda's
/// included, which is where a minimal API declares a handler's; its arguments are constants, so the type it names is
/// known.
/// </para>
/// <para>
/// A value object of the compilation itself implements its contract through the generated partial, which an analyzer
/// sees, unlike another generator.
/// </para>
/// <para>
/// Under <c>PublishAot</c>, which the package makes visible to the compiler, <c>[ParsesAs(typeof(T))]</c> finds the
/// value object in the registry only: a generated value object is registered by its assembly, while a construction of a
/// generic one, or one nested in a generic type, and a value object written by hand are found only once the application
/// registers them. The fix is the generic form, which needs no registry.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValidationAttributeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The key under which the compiler sees <c>PublishAot</c>, once the package's build props list it.</summary>
    internal const string PublishAotKey = "build_property.PublishAot";

    private const string ParsesAsName = "AdCodicem.ValueObjects.Annotations.ParsesAsAttribute";

    private const string GenericParsesAsName = "AdCodicem.ValueObjects.Annotations.ParsesAsAttribute`2";

    private const string NotDefaultName = "AdCodicem.ValueObjects.Annotations.NotDefaultAttribute";

    private const string ContractName = "AdCodicem.ValueObjects.IValueObject`2";

    private const string Category = "AdCodicem.ValueObjects";

    private const string HelpLink = "https://adcodicem.github.io/AdCodicem.ValueObjects/docs/how-to/data-annotations";

    /// <summary>The format a message names a type in: as the consumer writes it, keywords included.</summary>
    private static readonly SymbolDisplayFormat MessageFormat = SymbolDisplayFormat.CSharpShortErrorMessageFormat;

    /// <summary>Reports <c>[ParsesAs]</c> on a member that holds no text.</summary>
    public static readonly DiagnosticDescriptor HoldsNoText = new(
        "VO0039",
        "[ParsesAs] on a member that holds no text",
        "'{0}' is of type '{1}', which is neither text nor a collection of text: [ParsesAs] would throw when it is "
        + "validated. Declare it string or a collection of string, or declare it {2} and drop the attribute.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[ParsesAs] holds raw text to the rules of a value object: a string, or each element of a collection "
        + "of strings. On a member of any other type it throws InvalidOperationException when a value is validated.",
        helpLinkUri: HelpLink);

    /// <summary>Reports <c>[ParsesAs(typeof(T))]</c> naming a type that is no value object.</summary>
    public static readonly DiagnosticDescriptor NamesNoValueObject = new(
        "VO0040",
        "[ParsesAs] naming no value object",
        "[ParsesAs] names {0}, which is no value object: it would throw when a value is validated. Name a struct "
        + "implementing IValueObject<TSelf, TValue> over itself, constructed when it is generic.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[ParsesAs(typeof(T))] finds the descriptor of T the first time a value is validated, and throws "
        + "InvalidOperationException when T is not a value object: a class, a struct that does not implement "
        + "IValueObject<TSelf, TValue> over itself, or a generic definition, of which no instance exists.",
        helpLinkUri: HelpLink);

    /// <summary>Reports <c>[NotDefault]</c> on a member that is no value object.</summary>
    public static readonly DiagnosticDescriptor NotDefaultOnNoValueObject = new(
        "VO0041",
        "[NotDefault] on a member that is no value object",
        "'{0}' is of type '{1}', which is no value object: [NotDefault] would throw when it is validated. Declare it "
        + "as a value object or its nullable type, or drop the attribute.",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "[NotDefault] refuses a value object equal to its default instance. On a member of any other type, a "
        + "collection of value objects included, whose elements it does not check, it throws InvalidOperationException "
        + "when a value is validated.",
        helpLinkUri: HelpLink);

    /// <summary>Reports <c>[ParsesAs(typeof(T))]</c> under <c>PublishAot</c> naming a value object nothing registers.</summary>
    public static readonly DiagnosticDescriptor RegisteredByHandUnderAot = new(
        "VO0042",
        "[ParsesAs(typeof(T))] naming a value object native AOT finds only once registered",
        "Under native AOT, [ParsesAs(typeof({0}))] finds {0} only once the application registers it, since {1}. Write "
        + "[ParsesAs<{0}, {2}>], which needs no registry.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Under native AOT, the Type form of [ParsesAs] reads the registry, which a generated value object "
        + "joins as its assembly loads. A construction of a generic value object, or of one nested in a generic type, and "
        + "a value object written by hand join it only through ValueObjectRegistry.Register, and the attribute throws "
        + "InvalidOperationException until then. The generic form reads the value object's static members.",
        helpLinkUri: HelpLink);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => [HoldsNoText, NamesNoValueObject, NotDefaultOnNoValueObject, RegisteredByHandUnderAot];

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
            var parsesAs = compilation.GetTypeByMetadataName(ParsesAsName);
            var notDefault = compilation.GetTypeByMetadataName(NotDefaultName);
            var contract = compilation.GetTypeByMetadataName(ContractName);
            if ((parsesAs is null && notDefault is null) || contract is null)
            {
                return;
            }

            var symbols = new Symbols(
                compilation,
                parsesAs,
                compilation.GetTypeByMetadataName(GenericParsesAsName),
                notDefault,
                contract,
                compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T)
                    .Construct(compilation.GetSpecialType(SpecialType.System_String)),
                IsTrue(compilationContext.Options.AnalyzerConfigOptionsProvider.GlobalOptions, PublishAotKey));

            compilationContext.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, symbols), SyntaxKind.Attribute);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, Symbols symbols)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol { ContainingType: { } attributeType })
        {
            return;
        }

        var definition = attributeType.OriginalDefinition;
        var parsesAs = SymbolEqualityComparer.Default.Equals(definition, symbols.ParsesAs)
                       || SymbolEqualityComparer.Default.Equals(definition, symbols.GenericParsesAs);
        var notDefault = SymbolEqualityComparer.Default.Equals(definition, symbols.NotDefault);
        if ((!parsesAs && !notDefault) || Target(attribute, context.SemanticModel, context.CancellationToken) is not { } target)
        {
            return;
        }

        if (notDefault)
        {
            if (!MayHoldValueObject(target.Type, symbols.Contract))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    NotDefaultOnNoValueObject,
                    attribute.GetLocation(),
                    target.Name,
                    target.Type.ToDisplayString(MessageFormat)));
            }

            return;
        }

        if (!MayHoldText(target.Type, symbols))
        {
            // The value object is suggested by name when the attribute names one: the generic form always does, and the
            // Type form only when typeof names a value object, which VO0040 reports otherwise.
            context.ReportDiagnostic(Diagnostic.Create(
                HoldsNoText,
                attribute.GetLocation(),
                target.Name,
                target.Type.ToDisplayString(MessageFormat),
                (attributeType.Arity > 0 ? attributeType.TypeArguments[0] : NamedType(attribute, context)) is { } suggested
                && ValueObject(suggested, symbols.Contract) is not null
                    ? $"'{suggested.ToDisplayString(MessageFormat)}'"
                    : "as the value object"));
        }

        // The generic form's type arguments are checked by the compiler, through its constraint.
        if (attributeType.Arity > 0)
        {
            return;
        }

        var named = NamedType(attribute, context);
        if (named is null || ValueObject(named, symbols.Contract) is not { } valueObject)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                NamesNoValueObject,
                attribute.GetLocation(),
                named is null ? "null" : $"'{named.ToDisplayString(MessageFormat)}'"));
            return;
        }

        if (symbols.PublishAot && UnregisteredReason(valueObject.Type) is { } reason)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RegisteredByHandUnderAot,
                attribute.GetLocation(),
                valueObject.Type.ToDisplayString(MessageFormat),
                reason,
                valueObject.Value.ToDisplayString(MessageFormat)));
        }
    }

    /// <summary>Gets the member an attribute is written on, and its type.</summary>
    private static (string Name, ITypeSymbol Type)? Target(AttributeSyntax attribute, SemanticModel model, System.Threading.CancellationToken token)
    {
        switch (attribute.Parent?.Parent)
        {
            case ParameterSyntax parameter when model.GetDeclaredSymbol(parameter, token) is { } symbol:
                return (symbol.Name, symbol.Type);
            case PropertyDeclarationSyntax property when model.GetDeclaredSymbol(property, token) is { } symbol:
                return (symbol.Name, symbol.Type);
            case FieldDeclarationSyntax field when model.GetTypeInfo(field.Declaration.Type, token).Type is { } type:
                return (field.Declaration.Variables.First().Identifier.ValueText, type);
            default:
                return null;
        }
    }

    /// <summary>Gets the type <c>[ParsesAs(typeof(T))]</c> names, or <see langword="null"/> for none.</summary>
    private static ITypeSymbol? NamedType(AttributeSyntax attribute, SyntaxNodeAnalysisContext context)
        => attribute.ArgumentList?.Arguments.FirstOrDefault(argument => argument.NameEquals is null)?.Expression is TypeOfExpressionSyntax typeOf
            ? context.SemanticModel.GetTypeInfo(typeOf.Type, context.CancellationToken).Type
            : null;

    /// <summary>
    /// Tells whether a member is declared to hold text: a string, or a type that converts to a collection of strings. A
    /// type parameter, which a construction may make a string, and a type the compiler could not bind, are left alone.
    /// </summary>
    private static bool MayHoldText(ITypeSymbol type, Symbols symbols)
        => type.SpecialType == SpecialType.System_String
           || type.TypeKind is TypeKind.TypeParameter or TypeKind.Error
           || symbols.Compilation.ClassifyConversion(type, symbols.Texts).IsImplicit;

    /// <summary>
    /// Tells whether a member is declared to hold a value object: one or its nullable type. A type parameter and a type
    /// the compiler could not bind are left alone.
    /// </summary>
    private static bool MayHoldValueObject(ITypeSymbol type, INamedTypeSymbol contract)
        => type.TypeKind is TypeKind.TypeParameter or TypeKind.Error
           || ValueObject(type, contract) is not null;

    /// <summary>
    /// Gets the value object a type is, or makes nullable, with its underlying type; <see langword="null"/> when it is none.
    /// </summary>
    private static (INamedTypeSymbol Type, ITypeSymbol Value)? ValueObject(ITypeSymbol type, INamedTypeSymbol contract)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Struct, IsUnboundGenericType: false } named)
        {
            return null;
        }

        var implemented = named.AllInterfaces.FirstOrDefault(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, contract)
            && SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], named));

        return implemented is null ? null : (named, implemented.TypeArguments[1]);
    }

    /// <summary>
    /// Tells why a value object is in no generated registration, or <see langword="null"/> when its assembly registers it.
    /// </summary>
    private static string? UnregisteredReason(INamedTypeSymbol valueObject)
    {
        for (var around = valueObject; around is not null; around = around.ContainingType)
        {
            if (around.Arity > 0)
            {
                return SymbolEqualityComparer.Default.Equals(around, valueObject)
                    ? "a generic value object registers its definition, not its constructions"
                    : $"its assembly registers the definition of the generic '{around.OriginalDefinition.ToDisplayString(MessageFormat)}' it is nested in, not its constructions";
            }
        }

        return ValueObjectGenerator.CarriesAttribute(valueObject, ValueObjectGenerator.ValueObjectAttributeName)
               || ValueObjectGenerator.CarriesAttribute(valueObject, ValueObjectGenerator.EntityIdAttributeName)
            ? null
            : "it is written by hand, and nothing generated registers it";
    }

    private static bool IsTrue(AnalyzerConfigOptions options, string key)
        => options.TryGetValue(key, out var value) && string.Equals(value.Trim(), "true", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>The symbols the analysis compares with, resolved once per compilation.</summary>
    private sealed class Symbols(
        Compilation compilation,
        INamedTypeSymbol? parsesAs,
        INamedTypeSymbol? genericParsesAs,
        INamedTypeSymbol? notDefault,
        INamedTypeSymbol contract,
        INamedTypeSymbol texts,
        bool publishAot)
    {
        public Compilation Compilation { get; } = compilation;

        public INamedTypeSymbol? ParsesAs { get; } = parsesAs;

        public INamedTypeSymbol? GenericParsesAs { get; } = genericParsesAs;

        public INamedTypeSymbol? NotDefault { get; } = notDefault;

        public INamedTypeSymbol Contract { get; } = contract;

        public INamedTypeSymbol Texts { get; } = texts;

        public bool PublishAot { get; } = publishAot;
    }
}
