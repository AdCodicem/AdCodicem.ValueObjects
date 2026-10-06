using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AdCodicem.ValueObjects.Generators.Diagnostics;
using AdCodicem.ValueObjects.Generators.Emit;
using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace AdCodicem.ValueObjects.Generators;

/// <summary>
/// Generates the full implementation of the value objects annotated with <c>[ValueObject&lt;T&gt;]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    internal const string ValueObjectAttributeName = "AdCodicem.ValueObjects.Annotations.ValueObjectAttribute`1";
    private const string KnownValueAttributeName = "AdCodicem.ValueObjects.Annotations.KnownValueAttribute";
    internal const string EntityIdAttributeName = "AdCodicem.ValueObjects.Identifiers.EntityIdAttribute";
    private const string JsonRegistryTypeName = "AdCodicem.ValueObjects.Json.ValueObjectJsonRegistry";

    /// <summary>The assembly attribute opting every value object of the assembly into XML serialization.</summary>
    private const string XmlSerializationAttributeName = "AdCodicem.ValueObjects.Annotations.ValueObjectXmlSerializationAttribute";

    /// <summary>The interface a value object implements itself to keep the generator from implementing it.</summary>
    private const string XmlSerializableName = "System.Xml.Serialization.IXmlSerializable";

    /// <summary>The attribute naming a schema provider, which a value object may declare itself as well.</summary>
    private const string XmlSchemaProviderName = "System.Xml.Serialization.XmlSchemaProviderAttribute";

    /// <summary>The namespace of <c>DataClassificationAttribute</c>, which classifies a type as sensitive data.</summary>
    private const string ClassificationNamespace = "Microsoft.Extensions.Compliance.Classification";

    /// <summary>
    /// Fully qualified names without the C# keyword shorthand, so that <c>string</c> reads as
    /// <c>global::System.String</c> and matches the underlying type table.
    /// </summary>
    private const string HookNamespace = "AdCodicem.ValueObjects";

    /// <summary>Metadata name of the pattern hook, which is not generic: a pattern only applies to strings.</summary>
    private const string PatternHook = "IValueObjectPatternValidator";

    private const string GeneratedRegexAttributeName = "System.Text.RegularExpressions.GeneratedRegexAttribute";

    /// <summary>
    /// The <c>RegexOptions</c> that change what a pattern matches, which its text, the OpenAPI pattern, cannot carry:
    /// IgnoreCase, Multiline, Singleline and IgnorePatternWhitespace.
    /// </summary>
    private static readonly (int Flag, string Name)[] UnpublishedRegexOptions =
        [(1, "IgnoreCase"), (2, "Multiline"), (16, "Singleline"), (32, "IgnorePatternWhitespace")];

    internal static readonly SymbolDisplayFormat QualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>
    /// A namespace as a declaration writes it: qualified, without <c>global::</c>, and a keyword escaped.
    /// </summary>
    private static readonly SymbolDisplayFormat NamespaceFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>
    /// A type as a declaration writes it: its name alone, and a keyword escaped, so that <c>@event</c> is reopened
    /// as <c>@event</c> rather than as the keyword.
    /// </summary>
    private static readonly SymbolDisplayFormat IdentifierFormat = new(
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static readonly string[] LineSeparators = ["\r\n", "\n"];

    /// <summary>The C# keyword of each special type, by its full name, as a summary reads it.</summary>
    private static readonly Dictionary<string, string> Keywords = new(StringComparer.Ordinal)
    {
        ["System.Boolean"] = "bool",
        ["System.Byte"] = "byte",
        ["System.SByte"] = "sbyte",
        ["System.Char"] = "char",
        ["System.Decimal"] = "decimal",
        ["System.Double"] = "double",
        ["System.Single"] = "float",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.Int32"] = "int",
        ["System.UInt32"] = "uint",
        ["System.Int64"] = "long",
        ["System.UInt64"] = "ulong",
        ["System.Object"] = "object",
        ["System.String"] = "string",
        ["System.Void"] = "void",
    };

    /// <summary>The symbol of each operator, by the name of the method the compiler gives it.</summary>
    private static readonly Dictionary<string, string> Operators = new(StringComparer.Ordinal)
    {
        ["op_Addition"] = "+",
        ["op_Subtraction"] = "-",
        ["op_Multiply"] = "*",
        ["op_Division"] = "/",
        ["op_Modulus"] = "%",
        ["op_UnaryPlus"] = "+",
        ["op_UnaryNegation"] = "-",
        ["op_Increment"] = "++",
        ["op_Decrement"] = "--",
        ["op_LogicalNot"] = "!",
        ["op_OnesComplement"] = "~",
        ["op_BitwiseAnd"] = "&",
        ["op_BitwiseOr"] = "|",
        ["op_ExclusiveOr"] = "^",
        ["op_LeftShift"] = "<<",
        ["op_RightShift"] = ">>",
        ["op_UnsignedRightShift"] = ">>>",
        ["op_Equality"] = "==",
        ["op_Inequality"] = "!=",
        ["op_LessThan"] = "<",
        ["op_GreaterThan"] = ">",
        ["op_LessThanOrEqual"] = "<=",
        ["op_GreaterThanOrEqual"] = ">=",
        ["op_True"] = "true",
        ["op_False"] = "false",
    };

    /// <summary>The base classes of the converters the generator nests in a value object, by metadata name.</summary>
    private static readonly string[] ConverterBaseNames =
        ["System.ComponentModel.TypeConverter", "System.Text.Json.Serialization.JsonConverter`1"];

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var parsed = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ValueObjectAttributeName,
                // Any type declaration is admitted so that a value object written as a class or a record struct
                // reaches VO0002. Narrowing to a struct here would make the attribute silently do nothing.
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (attributeContext, _) => Parse(attributeContext))
            .WithTrackingName("ValueObjects");

        var parsedIds = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                EntityIdAttributeName,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (attributeContext, _) => ParseEntityId(attributeContext))
            .WithTrackingName("EntityIds");

        context.RegisterSourceOutput(parsed, static (production, result) => Produce(production, result));
        context.RegisterSourceOutput(parsedIds, static (production, result) => Produce(production, result));

        // Two types claiming one prefix can only be seen once every declaration has been visited, so the claims
        // travel in their own provider. Keeping them out of the model matters: a claim carries a source
        // location, and folding a location into the model would re-emit every generated file whenever an edit
        // above a declaration shifted its line.
        context.RegisterSourceOutput(
            parsedIds.Select(static (result, _) => result.Claim).Where(static claim => claim is not null).Collect(),
            static (production, claims) => ReportDuplicatePrefixes(production, claims));

        var models = parsed
            .Select(static (result, _) => result.Model)
            .Where(static model => model is not null)
            .Collect()
            .Combine(parsedIds
                .Select(static (result, _) => result.Model)
                .Where(static model => model is not null)
                .Collect())
            .Select(static (both, _) => both.Left.AddRange(both.Right));

        // Only for an AdCodicem.ValueObjects.Json older than the generator, which reads its own registry alone.
        var legacyJsonRegistry = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName(JsonRegistryTypeName) is not null);

        context.RegisterSourceOutput(models.Combine(legacyJsonRegistry), static (production, input) =>
        {
            var (collected, legacy) = input;
            if (collected.Length == 0)
            {
                return;
            }

            var source = RegistrationEmitter.Emit(collected.Select(static model => model!).ToImmutableArray(), legacy);

            production.AddSource("ValueObjectRegistration.g.cs", SourceText.From(source, Encoding.UTF8));
        });
    }

    private static ParseResult Parse(GeneratorAttributeSyntaxContext context)
    {
        var diagnostics = new List<DiagnosticInfo>();

        if (context.TargetSymbol is not INamedTypeSymbol symbol || context.TargetNode is not TypeDeclarationSyntax declaration)
        {
            return new ParseResult(null, EquatableArray<DiagnosticInfo>.Empty);
        }

        var location = declaration.Identifier.GetLocation();

        // A type carrying both annotations is reported once, from the entity identifier side. Emitting from
        // both paths would collide on the hint name and bring the whole generator down.
        if (CarriesAttribute(symbol, EntityIdAttributeName))
        {
            return new ParseResult(null, EquatableArray<DiagnosticInfo>.Empty);
        }

        if (!ValidateDeclaration(symbol, declaration, location, diagnostics)
            || !ValidateContext(symbol, context.SemanticModel.Compilation, entityId: false, location, diagnostics))
        {
            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        var containingTypes = CollectContainingTypes(symbol, location, diagnostics);

        var attribute = context.Attributes[0];
        var underlyingSymbol = attribute.AttributeClass?.TypeArguments.FirstOrDefault();
        var underlyingName = underlyingSymbol?.ToDisplayString(QualifiedFormat) ?? string.Empty;

        if (!UnderlyingType.TryResolve(underlyingName, out var underlying))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.UnsupportedUnderlyingType,
                location,
                underlyingSymbol?.ToDisplayString() ?? "?",
                string.Join(", ", UnderlyingType.SupportedNames)));

            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        var arguments = NamedArguments(attribute);

        var arithmetic = GetBool(arguments, "Arithmetic");
        if (arithmetic && !underlying.IsNumeric)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.ArithmeticRequiresNumeric, location, symbol.Name, underlying.Keyword));
            arithmetic = false;
        }

        var minLength = GetInt32(arguments, "MinLength");
        var maxLength = GetInt32(arguments, "MaxLength");
        if (!underlying.IsString && (minLength >= 0 || maxLength >= 0))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.LengthRequiresString, location, symbol.Name, underlying.Keyword));
            minLength = -1;
            maxLength = -1;
        }

        // The text options, Pattern, Minimum and Maximum, are compile errors (VO0021, VO0028) that nothing reads: what
        // they set would not survive the compiler. The hooks replace them.
        var patternHook = ReadPatternHook(symbol, underlying, location, diagnostics);
        var minimumHook = ReadBoundHook(symbol, underlying, "IValueObjectMinimum`1", location, diagnostics);
        var maximumHook = ReadBoundHook(symbol, underlying, "IValueObjectMaximum`1", location, diagnostics);
        var exampleHook = ReadExampleHook(symbol, location, diagnostics);

        // An option holding a value its enum does not define stops generation once every mistake is reported:
        // falling back to the default would drop what the author wrote without a word.
        var definedValueSet = TryGetEnumName(arguments, "ValueSet", symbol, location, diagnostics, out var valueSet);
        var definedComparison = TryGetEnumName(arguments, "Comparison", symbol, location, diagnostics, out var comparison);

        var isClosed = valueSet == "Closed";
        var hasSpanNormalizeHook = underlying.IsString && ImplementsHook(symbol, "IValueObjectSpanNormalizer");
        var implicitConversion = GetBool(arguments, "ImplicitConversionToValue");
        var explicitConversion = GetBool(arguments, "ExplicitConversionFromValue");
        var formatsThroughSpanHook = FormatsThroughSpanHook(symbol);
        var xml = ReadXmlSerialization(symbol, context.SemanticModel.Compilation, out var xmlNamespace);
        var usableName = ValidateName(
            symbol,
            ValueObjectEmitter.MemberNames(
                underlying,
                arithmetic,
                implicitConversion,
                explicitConversion,
                isClosed,
                hasSpanNormalizeHook,
                entityId: false,
                formatsThroughSpanHook,
                xml),
            location,
            diagnostics);
        var compilation = context.SemanticModel.Compilation;
        var declaredKnownValues = new List<DeclaredValue>();
        var knownValues = ParseKnownValues(symbol, underlying, compilation, diagnostics, declaredKnownValues, out var declaresKnownValues);

        if (!definedValueSet || !definedComparison || !usableName)
        {
            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        // A closed set whose every known value was refused has already been reported, and generates as an open one.
        if (isClosed && knownValues.Count == 0)
        {
            if (!declaresKnownValues)
            {
                diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.ClosedSetWithoutValues, location, symbol.Name));
            }

            isClosed = false;
        }

        var hasNormalizeHook = ImplementsHook(symbol, "IValueObjectNormalizer`1");

        // The membership the generator can evaluate: every known value, when each is a constant. One built by an
        // expression the compiler does not evaluate leaves membership to the type initializer and the contract kit.
        var closedSet = isClosed && declaredKnownValues.Count == knownValues.Count
            ? declaredKnownValues.Select(static known => known.Key!).ToList()
            : null;
        var rules = new DeclaredRules
        {
            Underlying = underlying,
            AllowEmpty = GetBool(arguments, "AllowEmpty"),
            MinLength = minLength,
            MaxLength = maxLength,
            Minimum = minimumHook
                ? ReadConstantBound(symbol, compilation, underlying, "IValueObjectMinimum`1", "Minimum")
                : null,
            Maximum = maximumHook
                ? ReadConstantBound(symbol, compilation, underlying, "IValueObjectMaximum`1", "Maximum")
                : null,
            ClosedSet = closedSet,
            ComparisonName = comparison ?? "Ordinal",
        };
        CheckDeclaredValues(
            symbol,
            rules,
            exampleHook ? ReadConstantExample(symbol, underlying, compilation) : null,
            declaredKnownValues,
            normalizes: hasNormalizeHook || hasSpanNormalizeHook,
            diagnostics);

        var summary = ExtractSummary(symbol, declaration);

        var model = new ValueObjectModel
        {
            Namespace = symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString(NamespaceFormat),
            TypeName = symbol.Name,
            Identifier = symbol.ToDisplayString(IdentifierFormat),
            QualifiedName = symbol.ToDisplayString(QualifiedFormat),
            ContainingTypes = EquatableArray<string>.From(containingTypes),
            TypeParameters = TypeParameterList(symbol),
            CrefName = symbol.ToDisplayString(IdentifierFormat) + TypeParameterList(symbol, '{', '}'),
            OpenQualifiedName = OpenName(symbol),
            IsGeneric = Chain(symbol).Any(static type => type.Arity > 0),
            RegistrationRoute = EquatableArray<string>.From(RegistrationRoute(symbol, out var routeStart)),
            RegistrationRouteStart = routeStart,
            RegistrationStep = "ValueObjectRegistration_" + HintNames.Hash(symbol.ToDisplayString(QualifiedFormat).Replace("global::", string.Empty)),
            Kind = underlying.Kind,
            UnderlyingFullName = underlying.FullName,
            HintName = BuildHintName(symbol),
            ComparisonName = comparison ?? "Ordinal",
            ImplicitConversionToValue = implicitConversion,
            ExplicitConversionFromValue = explicitConversion,
            Arithmetic = arithmetic,
            IsClosedValueSet = isClosed,
            AllowEmpty = GetBool(arguments, "AllowEmpty"),
            HasPatternHook = patternHook.Active,
            HasMinimumHook = minimumHook,
            HasMaximumHook = maximumHook,
            HasExampleHook = exampleHook,
            PatternHookText = patternHook.Text,
            MinLength = minLength,
            MaxLength = maxLength,
            SchemaFormat = GetString(arguments, "SchemaFormat") ?? underlying.SchemaFormat,
            Description = GetString(arguments, "Description") ?? summary,
            HasNormalizeHook = hasNormalizeHook,
            HasSpanNormalizeHook = hasSpanNormalizeHook,
            HasValidateHook = ImplementsHook(symbol, "IValueObjectValidator`1"),
            HasTryFormatHook = ImplementsHook(symbol, "IValueObjectFormatter`1"),
            HasFormatHook = ImplementsHook(symbol, "IValueObjectStringFormatter`1"),
            KnownValues = EquatableArray<KnownValueModel>.From(knownValues),
            IsClassified = IsClassified(symbol),
            XmlSerializable = xml,
            XmlNamespace = xmlNamespace,
        };

        return new ParseResult(model, EquatableArray<DiagnosticInfo>.From(diagnostics));
    }

    /// <summary>
    /// Builds the model of a type annotated with <c>[EntityId]</c>.
    /// </summary>
    /// <remarks>
    /// The result is an ordinary string value object model carrying a profile. Everything downstream — parsing,
    /// formatting, equality, JSON, the registry entry — is therefore shared with every other value object, and
    /// only normalization, validation and the minting members read the profile.
    /// </remarks>
    /// <param name="context">The annotated declaration.</param>
    /// <returns>The model, the diagnostics, and the prefix this type claims.</returns>
    private static ParseResult ParseEntityId(GeneratorAttributeSyntaxContext context)
    {
        var diagnostics = new List<DiagnosticInfo>();

        if (context.TargetSymbol is not INamedTypeSymbol symbol || context.TargetNode is not TypeDeclarationSyntax declaration)
        {
            return new ParseResult(null, EquatableArray<DiagnosticInfo>.Empty);
        }

        var location = declaration.Identifier.GetLocation();

        if (CarriesAttribute(symbol, ValueObjectAttributeName))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.ConflictingValueObjectAnnotations, location, symbol.Name));

            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        if (!ValidateDeclaration(symbol, declaration, location, diagnostics)
            || !ValidateContext(symbol, context.SemanticModel.Compilation, entityId: true, location, diagnostics))
        {
            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        var containingTypes = CollectContainingTypes(symbol, location, diagnostics);
        var attribute = context.Attributes[0];

        var prefix = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string
            : null;

        if (!EntityIdLayout.IsValidPrefix(prefix, out var prefixError))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.InvalidEntityIdPrefix, location, prefix ?? string.Empty, symbol.Name, prefixError));

            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        // The generator owns the normalization of the format, so a hook would be written and never called —
        // exactly the silent failure the hook interfaces exist to prevent.
        if (ImplementsHook(symbol, "IValueObjectNormalizer`1") || ImplementsHook(symbol, "IValueObjectSpanNormalizer"))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.EntityIdOwnsNormalization, location, symbol.Name));

            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        // Its format, too: a pattern would run beside the generated check and publish a second OpenAPI pattern.
        if (ImplementsHook(symbol, PatternHook))
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.EntityIdOwnsPattern, location, symbol.Name));

            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        // An identifier is text, which takes no bound: a bound hook would be declared and never checked.
        ReadBoundHook(symbol, UnderlyingType.String, "IValueObjectMinimum`1", location, diagnostics, entityId: true);
        ReadBoundHook(symbol, UnderlyingType.String, "IValueObjectMaximum`1", location, diagnostics, entityId: true);

        // Nothing reads a known value of an identifier, so a [KnownValue] would be read by no one. The type still
        // generates, so that every use of it does not fail as well.
        foreach (var known in KnownValueMembers(symbol))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.EntityIdTakesNoKnownValue,
                known.Member.Locations[0],
                symbol.Name,
                known.Member.Name));
        }

        var exampleHook = ReadExampleHook(symbol, location, diagnostics);

        var xml = ReadXmlSerialization(symbol, context.SemanticModel.Compilation, out var xmlNamespace);
        var members = ValueObjectEmitter.MemberNames(
            UnderlyingType.String,
            arithmetic: false,
            implicitConversion: false,
            explicitConversion: false,
            closedValueSet: false,
            normalizesFromSpan: true,
            entityId: true,
            FormatsThroughSpanHook(symbol),
            xml);
        var usableName = ValidateName(symbol, members, location, diagnostics);

        var arguments = NamedArguments(attribute);
        if (!TryGetEnumName(arguments, "Granularity", symbol, location, diagnostics, out var declaredGranularity)
            || !usableName)
        {
            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

        var granularity = declaredGranularity ?? EntityIdLayout.DefaultGranularity;
        var totalLength = EntityIdLayout.TotalLength(prefix!, granularity);
        var summary = ExtractSummary(symbol, declaration);

        var model = new ValueObjectModel
        {
            Namespace = symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString(NamespaceFormat),
            TypeName = symbol.Name,
            Identifier = symbol.ToDisplayString(IdentifierFormat),
            QualifiedName = symbol.ToDisplayString(QualifiedFormat),
            ContainingTypes = EquatableArray<string>.From(containingTypes),
            TypeParameters = TypeParameterList(symbol),
            CrefName = symbol.ToDisplayString(IdentifierFormat) + TypeParameterList(symbol, '{', '}'),
            OpenQualifiedName = OpenName(symbol),
            IsGeneric = Chain(symbol).Any(static type => type.Arity > 0),
            RegistrationRoute = EquatableArray<string>.From(RegistrationRoute(symbol, out var routeStart)),
            RegistrationRouteStart = routeStart,
            RegistrationStep = "ValueObjectRegistration_" + HintNames.Hash(symbol.ToDisplayString(QualifiedFormat).Replace("global::", string.Empty)),
            Kind = UnderlyingKind.String,
            UnderlyingFullName = UnderlyingType.String.FullName,
            HintName = BuildHintName(symbol),

            // The length is derived from the profile rather than declared, and reaches the database column and
            // the OpenAPI schema through the same field every other rule uses. Fixed on both ends, so the
            // column is CHAR rather than VARCHAR.
            MinLength = totalLength,
            MaxLength = totalLength,

            SchemaFormat = null,
            Description = GetString(arguments, "Description") ?? summary,
            HasExampleHook = exampleHook,
            HasValidateHook = ImplementsHook(symbol, "IValueObjectValidator`1"),
            HasTryFormatHook = ImplementsHook(symbol, "IValueObjectFormatter`1"),
            HasFormatHook = ImplementsHook(symbol, "IValueObjectStringFormatter`1"),
            Id = new EntityIdProfile(prefix!, granularity, totalLength),
            IsClassified = IsClassified(symbol),
            XmlSerializable = xml,
            XmlNamespace = xmlNamespace,
        };

        return new ParseResult(
            model,
            EquatableArray<DiagnosticInfo>.From(diagnostics),
            new PrefixClaim(symbol.ToDisplayString(), prefix!, LocationInfo.From(location)));
    }

    private static void Produce(SourceProductionContext production, ParseResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            production.ReportDiagnostic(diagnostic.ToDiagnostic());
        }

        if (result.Model is not null)
        {
            production.AddSource(
                result.Model.HintName,
                SourceText.From(ValueObjectEmitter.Emit(result.Model), Encoding.UTF8));
        }
    }

    /// <summary>
    /// Reports the prefixes claimed by more than one type in the compilation.
    /// </summary>
    /// <remarks>
    /// Ordered by type name so the diagnostic names the same offender on every build: which of two colliding
    /// types is reported must not depend on the order the compiler happened to visit them in.
    /// </remarks>
    /// <param name="production">Diagnostic sink.</param>
    /// <param name="claims">Every prefix claimed in the compilation.</param>
    private static void ReportDuplicatePrefixes(SourceProductionContext production, ImmutableArray<PrefixClaim?> claims)
    {
        var owners = new Dictionary<string, PrefixClaim>(StringComparer.Ordinal);

        foreach (var claim in claims.Where(static claim => claim is not null)
                     .Select(static claim => claim!.Value)
                     .OrderBy(static claim => claim.TypeName, StringComparer.Ordinal))
        {
            if (!owners.TryGetValue(claim.Prefix, out var owner))
            {
                owners.Add(claim.Prefix, claim);
                continue;
            }

            if (owner.TypeName == claim.TypeName)
            {
                continue;
            }

            production.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.DuplicateEntityIdPrefix,
                claim.Location?.ToLocation(),
                claim.TypeName,
                owner.TypeName,
                claim.Prefix));
        }
    }

    /// <summary>
    /// Reports the ways a declaration cannot host generated members.
    /// </summary>
    /// <param name="symbol">Annotated type.</param>
    /// <param name="declaration">Its syntax.</param>
    /// <param name="location">Where to report.</param>
    /// <param name="diagnostics">Sink.</param>
    /// <returns><see langword="false"/> when the declaration is unusable and parsing must stop.</returns>
    private static bool ValidateDeclaration(
        INamedTypeSymbol symbol,
        TypeDeclarationSyntax declaration,
        Location location,
        List<DiagnosticInfo> diagnostics)
    {
        if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.MustBePartial, location, symbol.Name));
        }

        // A class, an interface, a record struct and a ref struct all reach here so that each is told why it was
        // rejected, rather than being handed a type missing every member the attribute promised. A ref struct
        // would get members it cannot compile: it can be neither boxed nor a type argument, and the generated
        // code implements IValueObject<TSelf, TValue> over the type and registers a descriptor that boxes it.
        if (declaration is not StructDeclarationSyntax || !symbol.IsReadOnly || symbol.IsRecord || symbol.IsRefLikeType)
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.MustBeReadOnlyStruct, location, symbol.Name));

            return false;
        }

        return true;
    }

    /// <summary>
    /// Reports a declaration the generated code cannot reopen or reach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The generated code reopens the value object and each type around it, with their type parameters, in a file of
    /// its own, where a file-local type is another type. It registers the value object from a class of its own, or,
    /// for a private or a protected type, through a step written on each type around it down to the one that can see
    /// it, which it can only call on a type it can name without type arguments. Its statements name the type of every
    /// local and discard nothing, so no type named <c>var</c> or <c>_</c> changes what they mean, and it names the
    /// type through its type parameters, which a nested type of the same name would hide.
    /// </para>
    /// <para>
    /// What it wrote would not compile, in a file the author cannot edit, so the type is reported and left alone. An
    /// entity identifier is refused when generic as well: its prefix names one type, which every construction would
    /// claim.
    /// </para>
    /// </remarks>
    /// <param name="symbol">Annotated type.</param>
    /// <param name="compilation">The compilation, which holds the base classes of the generated converters.</param>
    /// <param name="entityId">Whether the type is an entity identifier.</param>
    /// <param name="location">Where to report.</param>
    /// <param name="diagnostics">Sink.</param>
    /// <returns><see langword="false"/> when nothing must be generated for the type.</returns>
    private static bool ValidateContext(
        INamedTypeSymbol symbol,
        Compilation compilation,
        bool entityId,
        Location location,
        List<DiagnosticInfo> diagnostics)
    {
        var (reason, remedy) = RefuseFileLocal(symbol)
            ?? (entityId ? RefuseGenericIdentifier(symbol) : null)
            ?? RefuseUnreachableRegistration(symbol)
            ?? RefuseHiddenTypeParameter(symbol, compilation)
            ?? default;

        if (reason is null)
        {
            return true;
        }

        diagnostics.Add(DiagnosticInfo.Create(
            DiagnosticDescriptors.UnsupportedDeclaration, location, symbol.Name, reason, remedy));

        return false;
    }

    /// <summary>
    /// Says why the generated file cannot reopen a file-local type, or a type nested in one.
    /// </summary>
    private static (string Reason, string Remedy)? RefuseFileLocal(INamedTypeSymbol symbol)
    {
        for (var type = symbol; type is not null; type = type.ContainingType)
        {
            if (type.IsFileLocal)
            {
                return (
                    Describe(symbol, type, "file-local"),
                    "Declare it, and every type around it, without the file modifier: the generated code reopens them in "
                    + "a file of its own, where a file-local type is out of reach");
            }
        }

        return null;
    }

    /// <summary>
    /// Says why an entity identifier cannot be generic, or nested in a generic type.
    /// </summary>
    private static (string Reason, string Remedy)? RefuseGenericIdentifier(INamedTypeSymbol symbol)
    {
        const string remedy = "Declare it without type parameters, outside any generic type: its prefix identifies one "
            + "type, and every construction of a generic identifier would claim the same one";

        if (symbol.Arity > 0)
        {
            return ("is generic", remedy);
        }

        for (var containing = symbol.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            if (containing.Arity > 0)
            {
                return (
                    $"is nested in the generic type '{containing.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}'",
                    remedy);
            }
        }

        return null;
    }

    /// <summary>
    /// Says why the registration cannot reach a private or protected type nested in a generic one.
    /// </summary>
    /// <remarks>
    /// A private or protected type is reachable only from the type declaring it, so its registration is a step
    /// written on each type around it. A step on a generic type can only be called on a construction of it, which the
    /// registration of the assembly does not know.
    /// </remarks>
    private static (string Reason, string Remedy)? RefuseUnreachableRegistration(INamedTypeSymbol symbol)
    {
        var chain = Chain(symbol);
        var deepest = chain.FindLastIndex(IsHidden);
        if (deepest < 0)
        {
            return null;
        }

        var generic = chain.Take(deepest).FirstOrDefault(static type => type.Arity > 0);
        if (generic is null)
        {
            return null;
        }

        return (
            $"{Describe(symbol, chain[deepest], Modifier(chain[deepest]))}, inside the generic type "
            + $"'{generic.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}'",
            "Declare the private or protected type internal or public, or move it out of the generic type: the registration "
            + "reaches it from the type around it, which it cannot name without type arguments");
    }

    /// <summary>
    /// Says why the generated code cannot name a type parameter of the value object or of a type around it.
    /// </summary>
    /// <remarks>
    /// The generated code names the value object through the type parameters of the types around it,
    /// <c>Outer&lt;T&gt;.Code</c>, and through its own, from inside it and from inside the converters it nests in it. A
    /// type between the two, declared or inherited, or a type parameter of the same name on a type between, would take
    /// the name, and the generated code would name another type. Inside the converters, the nested types of their base
    /// classes, such as <c>TypeConverter.StandardValuesCollection</c>, are in scope as well.
    /// </remarks>
    private static (string Reason, string Remedy)? RefuseHiddenTypeParameter(INamedTypeSymbol symbol, Compilation compilation)
    {
        var chain = Chain(symbol);
        var converterBases = ConverterBaseNames
            .Select(compilation.GetTypeByMetadataName)
            .OfType<INamedTypeSymbol>()
            .ToList();

        for (var owner = 0; owner < chain.Count; owner++)
        {
            foreach (var parameter in chain[owner].TypeParameters)
            {
                var hider = HiderOf(parameter.Name, chain, owner, converterBases);
                if (hider is not null)
                {
                    return (
                        $"names the type parameter '{parameter.Name}' of "
                        + $"'{chain[owner].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}', which "
                        + $"{hider} hides",
                        "Rename the type parameter: the generated code names the types around the value object through "
                        + "their type parameters, from inside it");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Describes what takes the name of a type parameter in the scopes the generated code names it from, if anything does.
    /// </summary>
    private static string? HiderOf(string name, List<INamedTypeSymbol> chain, int owner, List<INamedTypeSymbol> converterBases)
    {
        for (var inner = owner + 1; inner < chain.Count; inner++)
        {
            var type = chain[inner];
            if (type.TypeParameters.Any(other => other.Name == name))
            {
                return $"the type parameter of '{type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}'";
            }

            if (NestedType(type, name) is { } nested)
            {
                return $"the type '{nested.ContainingType.Name}.{name}'";
            }
        }

        if (name is "ValueJsonConverter" or "ValueTypeConverter")
        {
            return $"the type '{chain[chain.Count - 1].Name}.{name}'";
        }

        return converterBases.Select(converter => NestedType(converter, name)).FirstOrDefault(nested => nested is not null) is { } inherited
            ? $"the type '{inherited.ContainingType.Name}.{name}', which a generated converter inherits,"
            : null;
    }

    /// <summary>
    /// Finds the nested type a simple name binds to inside a type: one it declares, or one it inherits from its base
    /// classes, or from its base interfaces when it is an interface, without type parameters of its own.
    /// </summary>
    private static INamedTypeSymbol? NestedType(INamedTypeSymbol type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var own = SymbolEqualityComparer.Default.Equals(current, type);
            var found = current.GetTypeMembers(name)
                .FirstOrDefault(nested => nested.Arity == 0 && (own || nested.DeclaredAccessibility != Accessibility.Private));
            if (found is not null)
            {
                return found;
            }
        }

        return type.TypeKind != TypeKind.Interface
            ? null
            : type.AllInterfaces
                .SelectMany(baseInterface => baseInterface.GetTypeMembers(name))
                .FirstOrDefault(nested => nested.Arity == 0 && nested.DeclaredAccessibility != Accessibility.Private);
    }

    /// <summary>
    /// Gets a type and every type around it, outermost first.
    /// </summary>
    private static List<INamedTypeSymbol> Chain(INamedTypeSymbol symbol)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var type = symbol; type is not null; type = type.ContainingType)
        {
            chain.Insert(0, type);
        }

        return chain;
    }

    /// <summary>
    /// Tells whether a type is reachable only from the type declaring it, or from types deriving from it.
    /// </summary>
    private static bool IsHidden(INamedTypeSymbol type)
        => type.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal;

    private static string Modifier(INamedTypeSymbol type) => type.DeclaredAccessibility switch
    {
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        _ => "private protected",
    };

    /// <summary>
    /// Gets the types around a value object that its registration goes through, outermost first, or none when the
    /// registration of the assembly reaches it directly.
    /// </summary>
    /// <param name="symbol">The value object.</param>
    /// <param name="start">Position of the first type of the route among the types around the value object, outermost at 0.</param>
    /// <returns>
    /// The type declaring the outermost private or protected type, which the registration of the assembly still reaches,
    /// then every type down to the one declaring the most deeply nested private or protected type.
    /// </returns>
    private static List<string> RegistrationRoute(INamedTypeSymbol symbol, out int start)
    {
        var chain = Chain(symbol);
        var first = chain.FindIndex(IsHidden);
        var deepest = chain.FindLastIndex(IsHidden);

        // A top-level type is never private or protected, so a hidden type always has a type around it.
        start = Math.Max(first - 1, 0);
        return first <= 0
            ? []
            : [.. chain.Skip(start).Take(deepest - start).Select(static type => type.ToDisplayString(QualifiedFormat))];
    }

    /// <summary>
    /// Writes a type the way <c>typeof</c> names it unbound, every type parameter left out: <c>global::Shop.Outer&lt;&gt;.Code&lt;,&gt;</c>.
    /// </summary>
    private static string OpenName(INamedTypeSymbol symbol)
    {
        var names = Chain(symbol).Select(static type => type.ToDisplayString(IdentifierFormat)
            + (type.Arity > 0 ? $"<{new string(',', type.Arity - 1)}>" : string.Empty));
        var prefix = symbol.ContainingNamespace.IsGlobalNamespace
            ? "global::"
            : $"global::{symbol.ContainingNamespace.ToDisplayString(NamespaceFormat)}.";

        return prefix + string.Join(".", names);
    }

    /// <summary>
    /// Gets the type parameter list of a type, written as its declaration writes it.
    /// </summary>
    private static string TypeParameterList(INamedTypeSymbol symbol, char open = '<', char close = '>')
        => symbol.Arity == 0
            ? string.Empty
            : open + string.Join(", ", symbol.TypeParameters.Select(static parameter => parameter.ToDisplayString(IdentifierFormat))) + close;

    /// <summary>
    /// Describes a value object as hidden by its own declaration or by that of a type around it.
    /// </summary>
    private static string Describe(INamedTypeSymbol symbol, INamedTypeSymbol hiding, string modifier)
        => SymbolEqualityComparer.Default.Equals(hiding, symbol)
            ? $"is {modifier}"
            : $"is nested in the {modifier} type '{hiding.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}'";

    /// <summary>
    /// Reports a value object named after a member the generated code writes on it.
    /// </summary>
    /// <remarks>
    /// C# does not let a member take the name of the type that declares it, so the member written there would not
    /// compile, in a file the author cannot edit.
    /// </remarks>
    /// <param name="symbol">Annotated type.</param>
    /// <param name="members">The names the generated members take on the type.</param>
    /// <param name="location">Where to report.</param>
    /// <param name="diagnostics">Sink.</param>
    /// <returns><see langword="false"/> when nothing must be generated for the type.</returns>
    private static bool ValidateName(
        INamedTypeSymbol symbol,
        HashSet<string> members,
        Location location,
        List<DiagnosticInfo> diagnostics)
    {
        if (members.Contains(symbol.Name))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.UnsupportedDeclaration,
                location,
                symbol.Name,
                "takes the name of a member the generated code writes on it",
                "Rename it: C# does not let a member take the name of the type that declares it"));

            return false;
        }

        // A type parameter shares the declaration space of the type's members, so a member of its name would not
        // compile either.
        var parameter = symbol.TypeParameters.FirstOrDefault(candidate => members.Contains(candidate.Name));
        if (parameter is null)
        {
            return true;
        }

        diagnostics.Add(DiagnosticInfo.Create(
            DiagnosticDescriptors.UnsupportedDeclaration,
            location,
            symbol.Name,
            $"has a type parameter, '{parameter.Name}', named after a member the generated code writes on it",
            "Rename the type parameter: C# does not let a member take the name of a type parameter of its type"));

        return false;
    }

    private static List<string> CollectContainingTypes(
        INamedTypeSymbol symbol,
        Location location,
        List<DiagnosticInfo> diagnostics)
    {
        var containingTypes = new List<string>();

        for (var containing = symbol.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            if (!containing.DeclaringSyntaxReferences
                    .Select(static reference => reference.GetSyntax())
                    .OfType<TypeDeclarationSyntax>()
                    .Any(static syntax => syntax.Modifiers.Any(SyntaxKind.PartialKeyword)))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.ContainingTypeMustBePartial, location, symbol.Name, containing.Name));
            }

            containingTypes.Insert(0, $"{DeclarationKeyword(containing)} {containing.ToDisplayString(IdentifierFormat)}{TypeParameterList(containing)}");
        }

        return containingTypes;
    }

    /// <summary>
    /// Determines whether a type carries an annotation, by metadata name so that generic arity is respected.
    /// </summary>
    /// <param name="symbol">Type to inspect.</param>
    /// <param name="metadataName">Fully qualified metadata name of the attribute, arity included.</param>
    /// <returns><see langword="true"/> when the attribute is present.</returns>
    internal static bool CarriesAttribute(INamedTypeSymbol symbol, string metadataName)
        => FindAttribute(symbol, metadataName) is not null;

    /// <summary>
    /// Finds an annotation of a type or an assembly, by metadata name so that generic arity is respected.
    /// </summary>
    /// <param name="symbol">Type or assembly to inspect.</param>
    /// <param name="metadataName">Fully qualified metadata name of the attribute, arity included.</param>
    /// <returns>The first application of the attribute, or <see langword="null"/> when the symbol does not carry it.</returns>
    internal static AttributeData? FindAttribute(ISymbol symbol, string metadataName)
    {
        var separator = metadataName.LastIndexOf('.');
        var containingNamespace = metadataName.Substring(0, separator);
        var name = metadataName.Substring(separator + 1);

        return symbol.GetAttributes().FirstOrDefault(attribute =>
            attribute.AttributeClass is { } attributeClass
            && string.Equals(attributeClass.MetadataName, name, StringComparison.Ordinal)
            && string.Equals(attributeClass.ContainingNamespace.ToDisplayString(), containingNamespace, StringComparison.Ordinal));
    }

    /// <summary>
    /// Reads whether the assembly opts its value objects into XML serialization, and the namespace it names.
    /// </summary>
    /// <remarks>
    /// Read off the compilation inside the transform, the flag joins the model: the transform runs again whenever the
    /// compilation changes, and the model's equality keeps every emission cached until the attribute itself changes. A
    /// type that implements <c>IXmlSerializable</c> or declares a schema provider itself is left alone, its author's
    /// implementation standing, where a second one would not compile in a file they cannot edit.
    /// </remarks>
    /// <param name="symbol">Type to inspect.</param>
    /// <param name="compilation">The compilation, whose assembly carries the attribute.</param>
    /// <param name="xmlNamespace">The namespace the attribute names, or <see langword="null"/> for the default.</param>
    /// <returns><see langword="true"/> when the generator implements XML serialization on the type.</returns>
    private static bool ReadXmlSerialization(INamedTypeSymbol symbol, Compilation compilation, out string? xmlNamespace)
    {
        xmlNamespace = null;
        var attribute = FindAttribute(compilation.Assembly, XmlSerializationAttributeName);
        if (attribute is null
            || symbol.AllInterfaces.Any(static contract => string.Equals(contract.ToDisplayString(), XmlSerializableName, StringComparison.Ordinal))
            || CarriesAttribute(symbol, XmlSchemaProviderName))
        {
            return false;
        }

        xmlNamespace = attribute.NamedArguments
            .FirstOrDefault(static argument => string.Equals(argument.Key, "Namespace", StringComparison.Ordinal))
            .Value.Value as string;
        return true;
    }

    /// <summary>
    /// Determines whether the author classifies a type as sensitive data, with an attribute derived from
    /// <c>DataClassificationAttribute</c>.
    /// </summary>
    /// <remarks>
    /// The attribute is the author's own, so it is recognized by the metadata name of the base it derives from, and the
    /// library references Microsoft.Extensions.Compliance.Abstractions nowhere. <c>NoDataClassificationAttribute</c>
    /// derives from it to say the opposite, and does not count. Every other derived attribute does,
    /// <c>UnknownDataClassificationAttribute</c> included: data nobody has classified yet is read as sensitive.
    /// </remarks>
    /// <param name="symbol">Type to inspect.</param>
    /// <returns><see langword="true"/> when one of the attributes of the type classifies it.</returns>
    private static bool IsClassified(INamedTypeSymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            for (var type = attribute.AttributeClass; type is not null; type = type.BaseType)
            {
                if (!string.Equals(type.ContainingNamespace.ToDisplayString(), ClassificationNamespace, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(type.MetadataName, "NoDataClassificationAttribute", StringComparison.Ordinal))
                {
                    break;
                }

                if (string.Equals(type.MetadataName, "DataClassificationAttribute", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Reads the known values a value object declares: its static members marked <c>[KnownValue]</c>, reporting the ones
    /// that cannot be one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A known value is the author's member, so the compiler checks its name and the type of its value. What it cannot
    /// check is reported here (<c>VO0036</c>): a member that is not static, that can be written, that is of another type
    /// than the value object, or that is not initialized through <c>Known</c>, whose one argument is the value. The
    /// generated code reads the members once the author's part of the type has initialized them, and builds the
    /// membership lookup, <c>KnownValues</c> and the schema from them.
    /// </para>
    /// <para>
    /// The members come in the order the compiler lists them: declaration order within a file, then the order of the
    /// files for a type split across partial declarations. A <c>[KnownValue]</c> on the type itself, the form that took a
    /// name and a value, is a compile error (<c>VO0034</c>) and declares nothing.
    /// </para>
    /// </remarks>
    /// <param name="symbol">Annotated type.</param>
    /// <param name="underlying">Its underlying type.</param>
    /// <param name="compilation">The compilation, which holds every declaration of the type.</param>
    /// <param name="diagnostics">Sink.</param>
    /// <param name="declared">
    /// Receives each known value whose argument is a constant the generator can evaluate, for the rules of the type to be
    /// checked against.
    /// </param>
    /// <param name="declaresAny">
    /// Whether the type marks any member, or carries the attribute itself, so that a closed set is not reported as
    /// declaring no value besides.
    /// </param>
    /// <returns>The known values, in declaration order.</returns>
    private static List<KnownValueModel> ParseKnownValues(
        INamedTypeSymbol symbol,
        UnderlyingType underlying,
        Compilation compilation,
        List<DiagnosticInfo> diagnostics,
        List<DeclaredValue> declared,
        out bool declaresAny)
    {
        var knownValues = new List<KnownValueModel>();
        declaresAny = symbol.GetAttributes().Any(IsKnownValueAttribute);

        foreach (var (member, attribute) in KnownValueMembers(symbol))
        {
            declaresAny = true;
            var (declaration, initializer) = KnownValueDeclaration(member);

            var refusal = RefuseKnownValueMember(member, symbol, initializer, out var argument);
            if (refusal is not null)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.InvalidKnownValueMember,
                    member.Locations[0],
                    member.Name,
                    symbol.Name,
                    refusal));
                continue;
            }

            // Read as [ValueObject<T>] reads its own: the attribute first, then the summary of the member.
            var description = attribute.NamedArguments
                .FirstOrDefault(static pair => string.Equals(pair.Key, "Description", StringComparison.Ordinal))
                .Value.Value as string;

            // A member accepted as a known value has an initializer, read off the declaration that holds it.
            if (string.IsNullOrWhiteSpace(description))
            {
                description = ExtractSummary(member, declaration!);
            }

            knownValues.Add(new KnownValueModel(member.Name, EscapedName(member.Name), description));

            if (TryReadConstant(underlying, compilation, argument!, out var text, out var key))
            {
                declared.Add(new DeclaredValue($"known value {member.Name}", text, key, argument!.GetLocation()));
            }
        }

        return knownValues;
    }

    /// <summary>
    /// Finds the fields and properties of a type marked <c>[KnownValue]</c>, in the order the compiler lists them.
    /// </summary>
    /// <param name="symbol">The type.</param>
    /// <returns>Each member, with its attribute.</returns>
    private static IEnumerable<(ISymbol Member, AttributeData Attribute)> KnownValueMembers(INamedTypeSymbol symbol)
    {
        foreach (var member in symbol.GetMembers())
        {
            if (member is not (IFieldSymbol or IPropertySymbol) || member.IsImplicitlyDeclared)
            {
                continue;
            }

            var attribute = member.GetAttributes().FirstOrDefault(IsKnownValueAttribute);
            if (attribute is not null)
            {
                yield return (member, attribute);
            }
        }
    }

    private static bool IsKnownValueAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() == KnownValueAttributeName;

    /// <summary>
    /// Finds the declaration of a known value, whose leading comment documents it, and the expression that initializes it.
    /// </summary>
    /// <param name="member">The field or the property.</param>
    /// <returns>The declaration and the initializer, either <see langword="null"/> when there is none.</returns>
    private static (SyntaxNode? Declaration, ExpressionSyntax? Initializer) KnownValueDeclaration(ISymbol member)
    {
        foreach (var reference in member.DeclaringSyntaxReferences)
        {
            switch (reference.GetSyntax())
            {
                // A field is declared by its variable, inside the declaration that the comment and the attribute lead.
                case VariableDeclaratorSyntax variable:
                    return (variable.Parent?.Parent, variable.Initializer?.Value);

                case PropertyDeclarationSyntax property:
                    return (property, property.Initializer?.Value);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Says why a member marked <c>[KnownValue]</c> cannot be a known value, if it cannot.
    /// </summary>
    /// <remarks>
    /// A known value is created once, as the type initializes, and read for the life of the process: a member that can
    /// be written would let the membership lookup and the schema, built from it, describe a value it no longer holds. Its
    /// initializer is <c>Known(...)</c>, written as a call of that one name, since the generated factory is not part of
    /// the compilation the generator reads and cannot be bound.
    /// </remarks>
    /// <param name="member">The field or the property.</param>
    /// <param name="symbol">The value object.</param>
    /// <param name="initializer">The expression that initializes the member, if any.</param>
    /// <param name="argument">The argument of <c>Known</c>, when the member is usable.</param>
    /// <returns>The rule the member breaks, or <see langword="null"/> when it is a known value.</returns>
    private static string? RefuseKnownValueMember(
        ISymbol member,
        INamedTypeSymbol symbol,
        ExpressionSyntax? initializer,
        out ArgumentSyntax? argument)
    {
        argument = null;
        var type = member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type;

        if (!member.IsStatic)
        {
            return "it is not static";
        }

        if (member is IFieldSymbol { IsReadOnly: false })
        {
            return "it can be written: the field is not readonly";
        }

        if (member is IPropertySymbol { SetMethod: not null })
        {
            return "it can be written: the property has a setter";
        }

        if (!SymbolEqualityComparer.Default.Equals(type, symbol))
        {
            return $"it is of type '{type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}' rather than '{symbol.Name}'";
        }

        if (initializer is not InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "Known" },
                ArgumentList.Arguments: { Count: 1 } arguments,
            }
            || arguments[0].RefKindKeyword.RawKind != (int)SyntaxKind.None)
        {
            return "it is not initialized through Known(...), the value as its one argument";
        }

        argument = arguments[0];
        return null;
    }

    /// <summary>
    /// Writes the name of a member as code refers to it: a keyword escaped.
    /// </summary>
    private static string EscapedName(string name)
        => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    /// <summary>
    /// Reads the value of an argument the compiler evaluates, in the form the generator compares values of the
    /// underlying type in.
    /// </summary>
    /// <remarks>
    /// The call to <c>Known</c> or <c>Create</c> cannot be bound, since the generated code declaring them is not part of
    /// the compilation the generator reads, so the argument is evaluated alone, before the conversion the call applies. A
    /// constant is read only where that conversion changes nothing the generator tells apart: a constant of the
    /// underlying type, or an integer into a number. A <c>float</c> widened to a <c>double</c> names another value than
    /// its text, and a <c>char</c> converted to a number is no text of it, so neither is read: the type initializer and
    /// the contract kit check them.
    /// </remarks>
    /// <param name="underlying">The underlying type.</param>
    /// <param name="compilation">The compilation, which holds the argument.</param>
    /// <param name="argument">The argument.</param>
    /// <param name="text">The value as a diagnostic quotes it.</param>
    /// <param name="key">The value, as <see cref="DeclaredRules"/> compares it.</param>
    /// <returns><see langword="true"/> when the argument is a constant the generator can evaluate.</returns>
    private static bool TryReadConstant(
        UnderlyingType underlying,
        Compilation compilation,
        ArgumentSyntax argument,
        out string text,
        out IComparable key)
    {
        text = string.Empty;
        key = string.Empty;

        var constant = compilation.GetSemanticModel(argument.SyntaxTree).GetConstantValue(argument.Expression);
        if (!constant.HasValue || constant.Value is not { } value)
        {
            return false;
        }

        var readable = value switch
        {
            string => underlying.Kind == UnderlyingKind.String,
            bool => underlying.Kind == UnderlyingKind.Boolean,
            char => underlying.Kind == UnderlyingKind.Char,
            float => underlying.Kind == UnderlyingKind.Single,
            double => underlying.Kind == UnderlyingKind.Double,
            decimal => underlying.Kind == UnderlyingKind.Decimal,
            _ => underlying.IsNumeric,
        };

        if (!readable || !LiteralFactory.TryCreate(underlying, value, out _, out key))
        {
            return false;
        }

        text = LiteralFactory.Text(value)!;
        return true;
    }

    /// <summary>
    /// Reports a value the author declares on a value object — its example, a known value — that its own rules refuse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Neither has a use once refused. An example is published as the OpenAPI example, which generated clients, mock
    /// servers and readers take at its word, and a refused known value or example throws from the type initializer,
    /// which the registration of the assembly runs before <c>Main</c>: the application would not start.
    /// </para>
    /// <para>
    /// Only a value the compiler evaluates is checked, a constant passed to <c>Known</c> or to <c>Create</c>, against the
    /// rules of <see cref="DeclaredRules"/>. On a type that normalizes its value, the rules are left to the type
    /// initializer and the contract kit: the normalization may turn a value they refuse into one they accept.
    /// </para>
    /// </remarks>
    /// <param name="symbol">The value object.</param>
    /// <param name="rules">The rules the generator can evaluate.</param>
    /// <param name="example">The example, when the generator can evaluate it.</param>
    /// <param name="knownValues">The known values the generator can evaluate.</param>
    /// <param name="normalizes">Whether the type normalizes its value before checking it.</param>
    /// <param name="diagnostics">Sink.</param>
    private static void CheckDeclaredValues(
        INamedTypeSymbol symbol,
        DeclaredRules rules,
        DeclaredValue? example,
        List<DeclaredValue> knownValues,
        bool normalizes,
        List<DiagnosticInfo> diagnostics)
    {
        if (normalizes)
        {
            return;
        }

        var checkedValues = new List<DeclaredValue>();
        if (example is { } declared)
        {
            checkedValues.Add(declared);
        }

        checkedValues.AddRange(knownValues);
        foreach (var value in checkedValues)
        {
            if (rules.Refuse(value.Key!) is { } refusal)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.DeclaredValueRefused,
                    value.Location,
                    value.Label,
                    value.Text,
                    symbol.Name,
                    refusal.Code,
                    refusal.Message));
            }
        }
    }

    /// <summary>
    /// Reads a bound declared through <c>IValueObjectMinimum&lt;T&gt;</c> or <c>IValueObjectMaximum&lt;T&gt;</c> when its
    /// getter returns a constant, <c>public static int Maximum =&gt; 100;</c>, which the compiler evaluates.
    /// </summary>
    /// <remarks>
    /// Any other getter runs only at run time, and so does an initialized property, <c>{ get; } = 100</c>, whose value a
    /// static constructor may change and the type's other static fields may read before it is assigned. The constant is
    /// the value the getter returns, converted to the type of the property, so <c>=&gt; 0.1f</c> bounds a <c>double</c>
    /// with the float it names rather than with 0.1.
    /// </remarks>
    /// <param name="symbol">The value object.</param>
    /// <param name="compilation">The compilation, which holds every declaration of the hook.</param>
    /// <param name="underlying">The underlying type.</param>
    /// <param name="metadataName">Metadata name of the hook, <c>IValueObjectMinimum`1</c> or <c>IValueObjectMaximum`1</c>.</param>
    /// <param name="member">The property the hook declares, <c>Minimum</c> or <c>Maximum</c>.</param>
    /// <returns>The bound, or <see langword="null"/> when the getter returns no constant.</returns>
    private static DeclaredBound? ReadConstantBound(
        INamedTypeSymbol symbol,
        Compilation compilation,
        UnderlyingType underlying,
        string metadataName,
        string member)
    {
        var hook = symbol.AllInterfaces.First(candidate =>
            string.Equals(candidate.MetadataName, metadataName, StringComparison.Ordinal)
            && candidate.ContainingNamespace.ToDisplayString() == HookNamespace
            && candidate.TypeArguments[0].ToDisplayString(QualifiedFormat) == underlying.FullName);

        var declared = hook.GetMembers(member).OfType<IPropertySymbol>().Single();
        if (symbol.FindImplementationForInterfaceMember(declared) is not IPropertySymbol implementation)
        {
            return null;
        }

        // A partial property is found by its defining declaration, whose getter has no body: the implementing one returns.
        var properties = (implementation.PartialImplementationPart ?? implementation).DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .OfType<PropertyDeclarationSyntax>();

        foreach (var property in properties)
        {
            if (Returned(property) is not { } returned)
            {
                continue;
            }

            var constant = compilation.GetSemanticModel(returned.SyntaxTree)
                .GetOperation(returned)?
                .DescendantsAndSelf()
                .OfType<IReturnOperation>()
                .FirstOrDefault()?
                .ReturnedValue?
                .ConstantValue;

            // A constant the form of the type has no text for, such as an infinity, bounds nothing a value can break.
            if (constant is { HasValue: true } && LiteralFactory.TryCreate(underlying, constant.Value.Value, out _, out var key))
            {
                return new DeclaredBound(key, LiteralFactory.Text(constant.Value.Value!)!);
            }
        }

        return null;
    }

    /// <summary>
    /// Finds what the getter of a property returns, when it is written as one expression: <c>=&gt; 100</c>,
    /// <c>get =&gt; 100;</c> or <c>get { return 100; }</c>.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns>The arrow clause or the return statement, or <see langword="null"/>.</returns>
    private static SyntaxNode? Returned(PropertyDeclarationSyntax property)
    {
        if (property.ExpressionBody is { } arrow)
        {
            return arrow;
        }

        var getter = property.AccessorList?.Accessors.FirstOrDefault(static accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
        if (getter?.ExpressionBody is { } accessorArrow)
        {
            return accessorArrow;
        }

        return getter?.Body is { Statements.Count: 1 } body && body.Statements[0] is ReturnStatementSyntax statement
            ? statement
            : null;
    }

    /// <summary>
    /// Whether the value object implements one of the hook interfaces.
    /// </summary>
    /// <remarks>
    /// Hooks are declared by implementing an interface rather than by naming a member, so the compiler checks
    /// the signature and a misspelled or mis-signed rule fails the build instead of being silently ignored.
    /// </remarks>
    /// <param name="symbol">The value object.</param>
    /// <param name="metadataName">Unqualified metadata name of the hook interface, arity included.</param>
    /// <returns><see langword="true"/> when the interface is implemented.</returns>
    private static bool ImplementsHook(INamedTypeSymbol symbol, string metadataName)
        => symbol.AllInterfaces.Any(candidate =>
            string.Equals(candidate.MetadataName, metadataName, StringComparison.Ordinal)
            && candidate.ContainingNamespace.ToDisplayString() == HookNamespace);

    /// <summary>
    /// Whether <c>ToString</c> goes through the span formatting hook, which only happens when no string formatting
    /// hook answers in its place.
    /// </summary>
    /// <param name="symbol">The value object.</param>
    /// <returns><see langword="true"/> when the span hook formats the value.</returns>
    private static bool FormatsThroughSpanHook(INamedTypeSymbol symbol)
        => ImplementsHook(symbol, "IValueObjectFormatter`1") && !ImplementsHook(symbol, "IValueObjectStringFormatter`1");

    /// <summary>
    /// Reads the pattern hook of a value object: whether it applies, and the text of its pattern for the schema.
    /// </summary>
    /// <remarks>
    /// The text is read off the <c>[GeneratedRegex]</c> attribute of the <c>Pattern</c> property, which the author
    /// wrote and this generator therefore sees, so the schema publishes it as a literal and no regular expression is
    /// built to describe the type. The same attribute tells which options the published text would drop, and whether
    /// a match can run unbounded. A pattern written without the attribute is asked for its text at run time instead.
    /// </remarks>
    /// <param name="symbol">The value object.</param>
    /// <param name="underlying">Its underlying type, which must be a string for the hook to apply.</param>
    /// <param name="location">Where to report a hook on another type.</param>
    /// <param name="diagnostics">Sink.</param>
    /// <returns>Whether the hook applies, and the text of its pattern when the attribute gives it.</returns>
    private static (bool Active, string? Text) ReadPatternHook(
        INamedTypeSymbol symbol,
        UnderlyingType underlying,
        Location location,
        List<DiagnosticInfo> diagnostics)
    {
        if (!ImplementsHook(symbol, PatternHook))
        {
            return (false, null);
        }

        // The interface is not generic, so the compiler accepts it on any value object: the rule would be declared
        // and never run, which is the silent failure the hook interfaces exist to prevent.
        if (!underlying.IsString)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.PatternRequiresString, location, symbol.Name, underlying.Keyword));

            return (false, null);
        }

        var generated = symbol.GetMembers("Pattern")
            .OfType<IPropertySymbol>()
            .Where(property => property.IsStatic)
            .SelectMany(property => property.GetAttributes())
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == GeneratedRegexAttributeName);

        if (generated?.AttributeConstructor is not { } constructor)
        {
            return (true, null);
        }

        string? text = null;
        var options = 0;
        int? timeout = null;
        for (var index = 0; index < generated.ConstructorArguments.Length && index < constructor.Parameters.Length; index++)
        {
            var argument = generated.ConstructorArguments[index].Value;
            switch (constructor.Parameters[index].Name)
            {
                case "pattern":
                    text = argument as string;
                    break;
                case "options":
                    options = argument is int flags ? flags : 0;
                    break;
                case "matchTimeoutMilliseconds":
                    timeout = argument as int?;
                    break;
            }
        }

        var attributeLocation = generated.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? location;

        var unpublished = UnpublishedRegexOptions.Where(option => (options & option.Flag) != 0).Select(option => option.Name).ToList();
        if (unpublished.Count > 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.PatternOptionsNotPublished, attributeLocation, symbol.Name, string.Join(", ", unpublished)));
        }

        // Timeout.Infinite, -1, is how the attribute spells "no timeout" when it is written out.
        if (timeout is null or -1)
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.PatternWithoutTimeout, attributeLocation, symbol.Name));
        }

        return (true, text);
    }

    /// <summary>
    /// Reads a bound hook of a value object: whether it applies.
    /// </summary>
    /// <param name="symbol">The value object.</param>
    /// <param name="underlying">Its underlying type.</param>
    /// <param name="metadataName">Metadata name of the hook, <c>IValueObjectMinimum`1</c> or <c>IValueObjectMaximum`1</c>.</param>
    /// <param name="location">Where to report.</param>
    /// <param name="diagnostics">Sink.</param>
    /// <param name="entityId">Whether the value object is an entity identifier, whose format is its own.</param>
    /// <returns><see langword="true"/> when the generated code checks and publishes the bound.</returns>
    /// <remarks>
    /// The compiler accepts the interface over any type, on any value object: a bound over a type that takes none, or
    /// over another type than the underlying one, would be declared and never checked, which is the silent failure
    /// the hook interfaces exist to prevent. The message points at what the value object can take instead: a length
    /// or a pattern for a string, a validator for a <see cref="Guid"/>, a boolean or an identifier, whose format is
    /// fixed.
    /// </remarks>
    private static bool ReadBoundHook(
        INamedTypeSymbol symbol,
        UnderlyingType underlying,
        string metadataName,
        Location location,
        List<DiagnosticInfo> diagnostics,
        bool entityId = false)
    {
        var hooks = symbol.AllInterfaces
            .Where(candidate => string.Equals(candidate.MetadataName, metadataName, StringComparison.Ordinal)
                                && candidate.ContainingNamespace.ToDisplayString() == HookNamespace)
            .ToList();
        if (hooks.Count == 0)
        {
            return false;
        }

        // Each hook is judged on its own: beside the one over the underlying type, a second over another type would
        // still be declared and never checked.
        var name = metadataName.Substring(0, metadataName.IndexOf('`'));
        var active = false;
        foreach (var hook in hooks)
        {
            string reason;
            if (!underlying.SupportsBounds)
            {
                reason = entityId
                    ? "An identifier's format is fixed: check anything more in a validator hook"
                    : underlying.Kind == UnderlyingKind.String
                        ? "A string takes no bound: constrain its length with MinLength or MaxLength, and its form with "
                          + "IValueObjectPatternValidator or a validator hook"
                        : "A value object over that type takes no bound: remove the hook, or check the value in a validator hook";
            }
            else if (hook.TypeArguments[0].ToDisplayString(QualifiedFormat) == underlying.FullName)
            {
                active = true;
                continue;
            }
            else
            {
                reason = $"A bound is a value of the underlying type: implement {name}<{underlying.Keyword}> instead";
            }

            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.BoundHookCannotBound,
                location,
                symbol.Name,
                hook.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                underlying.Keyword,
                reason));
        }

        return active;
    }

    /// <summary>
    /// Reads the example hook of a value object: whether it applies.
    /// </summary>
    /// <remarks>
    /// The compiler accepts the interface over any type: an example over another type than the value object would be
    /// declared and never published, which is the silent failure the hook interfaces exist to prevent.
    /// </remarks>
    /// <param name="symbol">The value object.</param>
    /// <param name="location">Where to report.</param>
    /// <param name="diagnostics">Sink.</param>
    /// <returns><see langword="true"/> when the schema publishes the example the hook declares.</returns>
    private static bool ReadExampleHook(INamedTypeSymbol symbol, Location location, List<DiagnosticInfo> diagnostics)
    {
        var active = false;
        foreach (var hook in symbol.AllInterfaces.Where(IsExampleHook))
        {
            if (SymbolEqualityComparer.Default.Equals(hook.TypeArguments[0], symbol))
            {
                active = true;
                continue;
            }

            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.ExampleHookOverAnotherType,
                location,
                symbol.Name,
                hook.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        return active;
    }

    private static bool IsExampleHook(INamedTypeSymbol candidate)
        => string.Equals(candidate.MetadataName, "IValueObjectExample`1", StringComparison.Ordinal)
           && candidate.ContainingNamespace.ToDisplayString() == HookNamespace;

    /// <summary>
    /// Reads the example a value object declares when its getter creates it from a constant the generator can evaluate,
    /// <c>public static Percentage Example =&gt; Create(42);</c>.
    /// </summary>
    /// <remarks>
    /// Any other example is checked by the type initializer, which creates it as the schema reads it, and by the contract
    /// kit. A known value returned as the example has been checked as a known value.
    /// </remarks>
    /// <param name="symbol">The value object.</param>
    /// <param name="underlying">Its underlying type.</param>
    /// <param name="compilation">The compilation, which holds every declaration of the hook.</param>
    /// <returns>The example, or <see langword="null"/> when its getter creates none from a constant.</returns>
    private static DeclaredValue? ReadConstantExample(INamedTypeSymbol symbol, UnderlyingType underlying, Compilation compilation)
    {
        var hook = symbol.AllInterfaces.First(candidate =>
            IsExampleHook(candidate) && SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], symbol));
        var declared = hook.GetMembers("Example").OfType<IPropertySymbol>().Single();
        if (symbol.FindImplementationForInterfaceMember(declared) is not IPropertySymbol implementation)
        {
            return null;
        }

        var properties = (implementation.PartialImplementationPart ?? implementation).DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .OfType<PropertyDeclarationSyntax>();

        foreach (var property in properties)
        {
            var created = property.Initializer?.Value ?? Returned(property) switch
            {
                ArrowExpressionClauseSyntax arrow => arrow.Expression,
                ReturnStatementSyntax statement => statement.Expression,
                _ => null,
            };

            if (created is InvocationExpressionSyntax
                {
                    Expression: IdentifierNameSyntax { Identifier.ValueText: "Create" },
                    ArgumentList.Arguments: { Count: 1 } arguments,
                }
                && arguments[0].RefKindKeyword.RawKind == (int)SyntaxKind.None
                && TryReadConstant(underlying, compilation, arguments[0], out var text, out var key))
            {
                return new DeclaredValue("Example", text, key, arguments[0].GetLocation());
            }
        }

        return null;
    }

    private static string DeclarationKeyword(INamedTypeSymbol symbol) => symbol switch
    {
        { IsRecord: true, TypeKind: TypeKind.Struct } => "partial record struct",
        { IsRecord: true } => "partial record",
        { TypeKind: TypeKind.Struct } => "partial struct",
        { TypeKind: TypeKind.Interface } => "partial interface",
        _ => "partial class",
    };

    private static string BuildHintName(INamedTypeSymbol symbol)
        => HintNames.For(symbol.ToDisplayString(QualifiedFormat).Replace("global::", string.Empty));

    /// <summary>
    /// Reads the summary of a declaration, a value object or one of its known values, as plain text.
    /// </summary>
    /// <param name="symbol">The declared symbol.</param>
    /// <param name="declaration">Its syntax, which its comment leads.</param>
    /// <returns>The summary text, or <see langword="null"/> when the declaration has none.</returns>
    /// <remarks>
    /// A project that does not produce a documentation file compiles with <c>DocumentationMode.None</c>, and
    /// <c>GetDocumentationCommentXml</c> then returns nothing at all. Since most consumers leave that setting
    /// off, the trivia is read directly as a fallback: <c>///</c> lines, and a <c>/** */</c> comment, which the
    /// compiler reads as documentation too.
    /// </remarks>
    private static string? ExtractSummary(ISymbol symbol, SyntaxNode declaration)
    {
        var fromCompilation = ExtractSummary(symbol.GetDocumentationCommentXml());
        if (fromCompilation is not null)
        {
            return fromCompilation;
        }

        var builder = new StringBuilder();
        foreach (var trivia in declaration.GetLeadingTrivia())
        {
            var text = trivia.ToFullString();

            // The lexer's rule: /** opens a documentation comment unless another * or a / follows it, as in a /***
            // banner or an empty /**/.
            var delimited = text.Length > 3 && text.StartsWith("/**", StringComparison.Ordinal) && text[3] is not '*' and not '/';
            if (delimited)
            {
                text = text.EndsWith("*/", StringComparison.Ordinal) ? text.Substring(3, text.Length - 5) : text.Substring(3);
            }

            foreach (var line in text.Split(LineSeparators, StringSplitOptions.None))
            {
                var trimmed = line.Trim();
                if (delimited)
                {
                    // A delimited comment may start each line with an asterisk, which is not part of its text.
                    builder.Append(trimmed.StartsWith("*", StringComparison.Ordinal) ? trimmed.Substring(1) : trimmed).Append(' ');
                }
                else if (trimmed.StartsWith("///", StringComparison.Ordinal) && !trimmed.StartsWith("////", StringComparison.Ordinal))
                {
                    builder.Append(trimmed, 3, trimmed.Length - 3).Append(' ');
                }
            }
        }

        return ExtractSummary(builder.ToString());
    }

    private static string? ExtractSummary(string? documentation)
    {
        if (string.IsNullOrWhiteSpace(documentation))
        {
            return null;
        }

        const string open = "<summary>";
        const string close = "</summary>";

        var start = documentation!.IndexOf(open, StringComparison.Ordinal);
        var end = documentation.IndexOf(close, StringComparison.Ordinal);
        if (start < 0 || end < start)
        {
            return null;
        }

        var summary = documentation.Substring(start + open.Length, end - start - open.Length);
        var collapsed = Regex.Replace(PlainText(summary), @"\s+", " ").Trim();

        return collapsed.Length == 0 ? null : collapsed;
    }

    /// <summary>
    /// Renders the content of a summary as the plain text a reader of the OpenAPI document sees.
    /// </summary>
    /// <param name="summary">The content of the <c>summary</c> element, as XML.</param>
    /// <returns>The text.</returns>
    /// <remarks>
    /// A reference renders as the name it refers to, a <c>see langword</c> as its keyword, and any other element as
    /// its text. Content that is not well-formed XML, which a project producing no documentation file never has
    /// checked, loses its tags instead.
    /// </remarks>
    private static string PlainText(string summary)
    {
        try
        {
            var element = XElement.Parse("<summary>" + summary + "</summary>", LoadOptions.PreserveWhitespace);
            var builder = new StringBuilder();
            Render(element, builder);

            return builder.ToString();
        }
        catch (XmlException)
        {
            return WebUtility.HtmlDecode(Regex.Replace(summary, "<[^>]*>", " "));
        }
    }

    private static void Render(XElement element, StringBuilder builder)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;

                // A reference with no text of its own, self-closing or not, reads as what it refers to.
                case XElement { Name.LocalName: "see" or "seealso" } reference when reference.Attribute("cref") is { } cref:
                    builder.Append(string.IsNullOrWhiteSpace(reference.Value) ? SimpleName(cref.Value) : reference.Value);
                    break;

                case XElement { Name.LocalName: "see" or "seealso" } reference when reference.Attribute("langword") is { } keyword:
                    builder.Append(keyword.Value);
                    break;

                case XElement { Name.LocalName: "see" or "seealso" } reference when reference.Attribute("href") is { } link:
                    builder.Append(string.IsNullOrWhiteSpace(reference.Value) ? link.Value : reference.Value);
                    break;

                case XElement { Name.LocalName: "paramref" or "typeparamref" } reference:
                    builder.Append(reference.Attribute("name")?.Value);
                    break;

                case XElement child:
                    // A block - a paragraph, a line break, a list and its parts - separates words; anything else, <c>
                    // first among them, is read for its text in the flow of the sentence.
                    var block = child.Name.LocalName is "para" or "br" or "code" or "list" or "listheader" or "item" or "term" or "description";
                    builder.Append(block ? " " : string.Empty);
                    Render(child, builder);
                    builder.Append(block ? " " : string.Empty);
                    break;
            }
        }
    }

    /// <summary>
    /// Reads the name a cref refers to, as a reader of the summary sees it, whether the compiler resolved the cref or
    /// not: <c>T:Ns.Other`1</c> and <c>Other{T}</c> are <c>Other</c>, <c>P:Ns.List`1.Count</c> and <c>List{T}.Count</c>
    /// are <c>Count</c>, <c>T:System.String</c> and <c>string</c> are <c>string</c>.
    /// </summary>
    /// <param name="cref">The cref, resolved by the compiler or as written.</param>
    /// <returns>The name.</returns>
    /// <remarks>
    /// A constructor reads as its type, an operator as <c>operator +</c> or <c>implicit operator int</c>, an indexer as
    /// <c>this[int]</c>: the compiler resolves them to <c>#ctor</c>, <c>op_Addition</c> and <c>Item</c>, which a project
    /// producing its documentation file would otherwise publish where any other publishes what the author wrote.
    /// </remarks>
    private static string SimpleName(string cref)
        => cref.Length > 1 && cref[1] == ':' ? ResolvedName(cref[0], cref.Substring(2)) : WrittenName(cref);

    /// <summary>
    /// Reads the name a documentation ID refers to, <c>M:Ns.Type`1.Member(System.Int32)</c>.
    /// </summary>
    private static string ResolvedName(char kind, string id)
    {
        var returned = string.Empty;
        var tilde = id.LastIndexOf('~');
        if (tilde >= 0 && id.LastIndexOf(')') < tilde)
        {
            returned = id.Substring(tilde + 1);
            id = id.Substring(0, tilde);
        }

        var parameters = new List<string>();
        var open = id.IndexOf('(');
        if (open >= 0)
        {
            parameters = SplitArguments(id.Substring(open + 1, id.Length - open - 2));
            id = id.Substring(0, open);
        }

        var segments = id.Split('.').Select(segment => Regex.Replace(segment, "``?[0-9]+", string.Empty)).ToList();
        var member = segments[segments.Count - 1];
        member = member.Substring(member.LastIndexOf('#') + 1);

        if (kind == 'T')
        {
            return TypeName(id);
        }

        if (member is "ctor" or "cctor")
        {
            return segments.Count > 1 ? segments[segments.Count - 2] : member;
        }

        if (member is "op_Implicit" or "op_Explicit")
        {
            return $"{(member == "op_Implicit" ? "implicit" : "explicit")} operator {TypeName(returned)}";
        }

        if (member.StartsWith("op_", StringComparison.Ordinal) && Operators.TryGetValue(member, out var symbol))
        {
            return "operator " + symbol;
        }

        return kind == 'P' && member == "Item" && parameters.Count > 0
            ? $"this[{string.Join(", ", parameters.Select(TypeName))}]"
            : member;
    }

    /// <summary>
    /// Reads the name a cref the compiler did not resolve refers to, as its author wrote it.
    /// </summary>
    private static string WrittenName(string cref)
    {
        var parameters = cref.IndexOf('(');
        var name = parameters >= 0 ? cref.Substring(0, parameters) : cref;

        // An operator names a type after the keyword, which a dot may qualify: only the type is shortened.
        var operatorAt = Regex.Match(name, @"\b(?:(?:implicit|explicit)\s+)?operator\b");
        if (operatorAt.Success)
        {
            var conversion = Regex.Match(name.Substring(operatorAt.Index), @"^(implicit|explicit)\s+operator\s+(.+)$");
            return conversion.Success
                ? $"{conversion.Groups[1].Value} operator {TypeName(conversion.Groups[2].Value.Trim())}"
                : Regex.Replace(name.Substring(operatorAt.Index), @"\s+", " ").Trim();
        }

        var segments = SplitQualified(name);
        var last = segments[segments.Count - 1];
        if (last.StartsWith("this[", StringComparison.Ordinal) && last.EndsWith("]", StringComparison.Ordinal))
        {
            return $"this[{string.Join(", ", SplitArguments(last.Substring(5, last.Length - 6)).Select(TypeName))}]";
        }

        return TypeName(name);
    }

    /// <summary>
    /// Shortens a type, qualified or not, to the name a reader sees: its keyword for a special type, else its simple
    /// name without type arguments, an array keeping its brackets.
    /// </summary>
    private static string TypeName(string type)
    {
        type = type.Trim();
        var rank = string.Empty;
        while (type.EndsWith("[]", StringComparison.Ordinal))
        {
            rank += "[]";
            type = type.Substring(0, type.Length - 2);
        }

        var bare = StripArguments(type);
        if (Keywords.TryGetValue(bare.StartsWith("global::", StringComparison.Ordinal) ? bare.Substring(8) : bare, out var keyword))
        {
            return keyword + rank;
        }

        var segments = SplitQualified(bare);
        return segments[segments.Count - 1] + rank;
    }

    /// <summary>
    /// Removes every type argument list and arity marker from a name: <c>Dictionary{TKey, List{T}}</c> is <c>Dictionary</c>.
    /// </summary>
    private static string StripArguments(string name)
    {
        string previous;
        do
        {
            previous = name;
            name = Regex.Replace(name, @"``?[0-9]+|\{[^{}]*\}|<[^<>]*>", string.Empty);
        }
        while (name != previous);

        return name;
    }

    /// <summary>
    /// Splits a qualified name on the dots outside its type argument lists and brackets.
    /// </summary>
    private static List<string> SplitQualified(string name)
        => SplitOutside(name, '.');

    /// <summary>
    /// Splits a parameter or type argument list on the commas outside the lists it nests.
    /// </summary>
    private static List<string> SplitArguments(string list)
        => SplitOutside(list, ',');

    private static List<string> SplitOutside(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            depth += text[index] switch
            {
                '{' or '<' or '[' or '(' => 1,
                '}' or '>' or ']' or ')' => -1,
                _ => 0,
            };

            if (depth == 0 && text[index] == separator)
            {
                parts.Add(text.Substring(start, index - start).Trim());
                start = index + 1;
            }
        }

        parts.Add(text.Substring(start).Trim());
        return parts;
    }

    /// <summary>
    /// Reads the named arguments of an annotation by name, the first of a name when it is written twice.
    /// </summary>
    /// <remarks>
    /// The compiler refuses a repeated named argument (CS0643) but still hands both to the generator, in the IDE as it
    /// is typed. Keeping the first, as the <c>Description</c> of a <c>[KnownValue]</c> is read, lets the generator
    /// produce every value object meanwhile, where throwing would drop its whole output and bury the compiler's error
    /// under the missing members of every other type.
    /// </remarks>
    /// <param name="attribute">The annotation.</param>
    /// <returns>Each named argument's value, by name.</returns>
    private static Dictionary<string, TypedConstant> NamedArguments(AttributeData attribute)
    {
        var arguments = new Dictionary<string, TypedConstant>(attribute.NamedArguments.Length, StringComparer.Ordinal);
        foreach (var argument in attribute.NamedArguments)
        {
            if (!arguments.ContainsKey(argument.Key))
            {
                arguments.Add(argument.Key, argument.Value);
            }
        }

        return arguments;
    }

    private static bool GetBool(Dictionary<string, TypedConstant> arguments, string name)
        => arguments.TryGetValue(name, out var value) && value.Value is bool boolean && boolean;

    private static int GetInt32(Dictionary<string, TypedConstant> arguments, string name)
        => arguments.TryGetValue(name, out var value) && value.Value is int number ? number : -1;

    private static string? GetText(Dictionary<string, TypedConstant> arguments, string name)
        => arguments.TryGetValue(name, out var value) ? value.Value as string : null;

    private static string? GetString(Dictionary<string, TypedConstant> arguments, string name)
    {
        if (!arguments.TryGetValue(name, out var value) || value.Value is not string text)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    /// <summary>
    /// Reads an enum option as the name of the member it holds.
    /// </summary>
    /// <remarks>
    /// A cast makes any number a constant of the enum type, so an option can hold a value its enum does not
    /// define. That is reported rather than read as the default, which would drop what the author wrote.
    /// </remarks>
    /// <param name="arguments">Named arguments of the attribute.</param>
    /// <param name="option">Name of the option.</param>
    /// <param name="symbol">Annotated type.</param>
    /// <param name="location">Where to report.</param>
    /// <param name="diagnostics">Sink.</param>
    /// <param name="member">The member the option holds, or <see langword="null"/> when it is not set.</param>
    /// <returns><see langword="false"/> when the option holds a value its enum does not define.</returns>
    private static bool TryGetEnumName(
        Dictionary<string, TypedConstant> arguments,
        string option,
        INamedTypeSymbol symbol,
        Location location,
        List<DiagnosticInfo> diagnostics,
        out string? member)
    {
        member = null;
        if (!arguments.TryGetValue(option, out var value) || value.Value is null || value.Type is not INamedTypeSymbol enumType)
        {
            return true;
        }

        foreach (var field in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.HasConstantValue && Equals(field.ConstantValue, value.Value))
            {
                member = field.Name;
                return true;
            }
        }

        diagnostics.Add(DiagnosticInfo.Create(
            DiagnosticDescriptors.UndefinedEnumValue,
            location,
            symbol.Name,
            option,
            string.Format(CultureInfo.InvariantCulture, "{0}", value.Value),
            enumType.Name));

        return false;
    }

    /// <summary>
    /// A value the author declares on a value object, as written, to be checked against its rules.
    /// </summary>
    /// <param name="Label">What the value is, as the diagnostic names it: <c>Example</c>, or <c>known value Euro</c>.</param>
    /// <param name="Text">The value as written, in the invariant form of a constant.</param>
    /// <param name="Key">The value, in the form <c>LiteralFactory</c> compares it in, once converted.</param>
    /// <param name="Location">Where to report a refusal.</param>
    private readonly record struct DeclaredValue(string Label, string Text, IComparable? Key, Location Location);

    private readonly record struct ParseResult(
        ValueObjectModel? Model,
        EquatableArray<DiagnosticInfo> Diagnostics,
        PrefixClaim? Claim = null);

    /// <summary>
    /// One type's claim on a prefix, carried separately from the model so that a shifted source location does
    /// not invalidate the generated output.
    /// </summary>
    /// <param name="TypeName">Display name of the claiming type.</param>
    /// <param name="Prefix">The prefix claimed.</param>
    /// <param name="Location">Where to report a collision.</param>
    private readonly record struct PrefixClaim(string TypeName, string Prefix, LocationInfo? Location);
}
