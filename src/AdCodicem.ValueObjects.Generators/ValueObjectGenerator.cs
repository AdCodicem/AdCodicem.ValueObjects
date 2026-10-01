using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Generators.Diagnostics;
using AdCodicem.ValueObjects.Generators.Emit;
using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AdCodicem.ValueObjects.Generators;

/// <summary>
/// Generates the full implementation of the value objects annotated with <c>[ValueObject&lt;T&gt;]</c>.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    private const string ValueObjectAttributeName = "AdCodicem.ValueObjects.Annotations.ValueObjectAttribute`1";
    private const string KnownValueAttributeName = "AdCodicem.ValueObjects.Annotations.KnownValueAttribute";
    private const string EntityIdAttributeName = "AdCodicem.ValueObjects.Identifiers.EntityIdAttribute";
    private const string JsonRegistryTypeName = "AdCodicem.ValueObjects.Json.ValueObjectJsonRegistry";

    /// <summary>
    /// Fully qualified names without the C# keyword shorthand, so that <c>string</c> reads as
    /// <c>global::System.String</c> and matches the underlying type table.
    /// </summary>
    private const string HookNamespace = "AdCodicem.ValueObjects";

    private static readonly SymbolDisplayFormat QualifiedFormat = SymbolDisplayFormat.FullyQualifiedFormat
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

    /// <summary>
    /// The members of <see cref="object"/> that no value object overrides and a static property would hide.
    /// </summary>
    private static readonly string[] InheritedNames = ["GetType", "MemberwiseClone", "ReferenceEquals"];

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

        // Reduced to a bool so that the compilation changing on every keystroke does not invalidate the output.
        var jsonPackageReferenced = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName(JsonRegistryTypeName) is not null);

        context.RegisterSourceOutput(models.Combine(jsonPackageReferenced), static (production, input) =>
        {
            var (collected, withJson) = input;
            if (collected.Length == 0)
            {
                return;
            }

            var source = RegistrationEmitter.Emit(
                collected.Select(static model => model!).ToImmutableArray(),
                withJson);

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
            || !ValidateContext(symbol, location, diagnostics))
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

        var arguments = attribute.NamedArguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

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

        var pattern = GetString(arguments, "Pattern");
        if (pattern is not null && !IsValidRegex(pattern, out var regexError))
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.InvalidPattern, location, symbol.Name, regexError));
            pattern = null;
        }

        // Read as written, white space included: a bound is held to the one form of its type, and an empty or a
        // blank one is no more that form than any other text.
        var minimumText = GetText(arguments, "Minimum");
        var maximumText = GetText(arguments, "Maximum");
        var minimumLiteral = ParseBound(underlying, minimumText, "Minimum", symbol, location, diagnostics);
        var maximumLiteral = ParseBound(underlying, maximumText, "Maximum", symbol, location, diagnostics);

        // An option holding a value its enum does not define stops generation once every mistake is reported:
        // falling back to the default would drop what the author wrote without a word.
        var definedValueSet = TryGetEnumName(arguments, "ValueSet", symbol, location, diagnostics, out var valueSet);
        var definedComparison = TryGetEnumName(arguments, "Comparison", symbol, location, diagnostics, out var comparison);

        var isClosed = valueSet == "Closed";
        var hasSpanNormalizeHook = underlying.IsString && ImplementsHook(symbol, "IValueObjectSpanNormalizer");
        var implicitConversion = GetBool(arguments, "ImplicitConversionToValue");
        var explicitConversion = GetBool(arguments, "ExplicitConversionFromValue");
        var generated = ValueObjectEmitter.TakenNames(
            symbol.Name,
            underlying,
            arithmetic,
            implicitConversion,
            explicitConversion,
            isClosed,
            pattern is not null,
            hasSpanNormalizeHook);
        var usableName = ValidateName(
            symbol,
            ValueObjectEmitter.MemberNames(
                underlying,
                arithmetic,
                implicitConversion,
                explicitConversion,
                isClosed,
                pattern is not null,
                hasSpanNormalizeHook,
                entityId: false),
            location,
            diagnostics);
        var knownValues = ParseKnownValues(symbol, underlying, generated, location, diagnostics);

        if (isClosed && knownValues.Count == 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(DiagnosticDescriptors.ClosedSetWithoutValues, location, symbol.Name));
            isClosed = false;
        }

        if (!definedValueSet || !definedComparison || !usableName)
        {
            return new ParseResult(null, EquatableArray<DiagnosticInfo>.From(diagnostics));
        }

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
            Kind = underlying.Kind,
            UnderlyingFullName = underlying.FullName,
            HintName = BuildHintName(symbol),
            ComparisonName = comparison ?? "Ordinal",
            ImplicitConversionToValue = implicitConversion,
            ExplicitConversionFromValue = explicitConversion,
            Arithmetic = arithmetic,
            IsClosedValueSet = isClosed,
            AllowEmpty = GetBool(arguments, "AllowEmpty"),
            Pattern = pattern,
            MinLength = minLength,
            MaxLength = maxLength,
            MinimumLiteral = minimumLiteral,
            MaximumLiteral = maximumLiteral,
            MinimumText = minimumLiteral is null ? null : minimumText,
            MaximumText = maximumLiteral is null ? null : maximumText,
            SchemaFormat = GetString(arguments, "SchemaFormat") ?? underlying.SchemaFormat,
            Example = GetString(arguments, "Example"),
            Description = GetString(arguments, "Description") ?? summary,
            HasNormalizeHook = ImplementsHook(symbol, "IValueObjectNormalizer`1"),
            HasSpanNormalizeHook = hasSpanNormalizeHook,
            HasValidateHook = ImplementsHook(symbol, "IValueObjectValidator`1"),
            HasTryFormatHook = ImplementsHook(symbol, "IValueObjectFormatter`1"),
            HasFormatHook = ImplementsHook(symbol, "IValueObjectStringFormatter`1"),
            KnownValues = EquatableArray<KnownValueModel>.From(knownValues),
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
            || !ValidateContext(symbol, location, diagnostics))
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

        var members = ValueObjectEmitter.MemberNames(
            UnderlyingType.String,
            arithmetic: false,
            implicitConversion: false,
            explicitConversion: false,
            closedValueSet: false,
            pattern: false,
            normalizesFromSpan: true,
            entityId: true);
        var usableName = ValidateName(symbol, members, location, diagnostics);

        var arguments = attribute.NamedArguments.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
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
            Kind = UnderlyingKind.String,
            UnderlyingFullName = UnderlyingType.String.FullName,
            HintName = BuildHintName(symbol),

            // The length is derived from the profile rather than declared, and reaches the database column and
            // the OpenAPI schema through the same field every other rule uses. Fixed on both ends, so the
            // column is CHAR rather than VARCHAR.
            MinLength = totalLength,
            MaxLength = totalLength,

            // Left null on purpose: the schema publishes a pattern, but validation is a span scan, so no Regex
            // is ever compiled for an identifier type.
            Pattern = null,
            SchemaFormat = null,
            Description = GetString(arguments, "Description") ?? summary,
            Example = GetString(arguments, "Example"),
            HasValidateHook = ImplementsHook(symbol, "IValueObjectValidator`1"),
            HasTryFormatHook = ImplementsHook(symbol, "IValueObjectFormatter`1"),
            HasFormatHook = ImplementsHook(symbol, "IValueObjectStringFormatter`1"),
            Id = new EntityIdProfile(prefix!, granularity, totalLength),
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
    /// The generated code reopens the value object and each type around it by name alone, which drops their type
    /// parameters, and reopens a containing type as a class, a struct or a record, which an interface is not. It
    /// reopens them in a file of its own, where a file-local type is another type, and registers the value object
    /// from a class of its own, which a private or a protected type, or one nested in such a type, is hidden from.
    /// Inside the value object, its statements write <c>var</c> and the discard <c>_</c>, which a type of either
    /// name, the value object or one around it, would capture.
    /// </para>
    /// <para>
    /// What it wrote would not compile, in a file the author cannot edit, so the type is reported and left alone.
    /// </para>
    /// </remarks>
    /// <param name="symbol">Annotated type.</param>
    /// <param name="location">Where to report.</param>
    /// <param name="diagnostics">Sink.</param>
    /// <returns><see langword="false"/> when nothing must be generated for the type.</returns>
    private static bool ValidateContext(INamedTypeSymbol symbol, Location location, List<DiagnosticInfo> diagnostics)
    {
        var (reason, remedy) = RefuseGenericContext(symbol)
            ?? RefuseHiddenContext(symbol)
            ?? RefuseCapturingName(symbol)
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
    /// Says why the generated code cannot reopen a type with type parameters, or nested in a generic type or an
    /// interface.
    /// </summary>
    private static (string Reason, string Remedy)? RefuseGenericContext(INamedTypeSymbol symbol)
    {
        const string remedy = "Declare it without type parameters, either at namespace level or nested in non-generic "
            + "classes, structs and records";

        if (symbol.Arity > 0)
        {
            return ("is generic", remedy);
        }

        for (var containing = symbol.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            var name = containing.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            if (containing.TypeKind == TypeKind.Interface)
            {
                return ($"is nested in the interface '{name}'", remedy);
            }

            if (containing.Arity > 0)
            {
                return ($"is nested in the generic type '{name}'", remedy);
            }
        }

        return null;
    }

    /// <summary>
    /// Says why the generated code cannot reach a type, or a type around it: one private, protected or private
    /// protected, which the registration cannot see, or a file-local one, which the generated file cannot reopen.
    /// </summary>
    private static (string Reason, string Remedy)? RefuseHiddenContext(INamedTypeSymbol symbol)
    {
        for (var type = symbol; type is not null; type = type.ContainingType)
        {
            var hidden = type.DeclaredAccessibility switch
            {
                Accessibility.Private => "private",
                Accessibility.Protected => "protected",
                Accessibility.ProtectedAndInternal => "private protected",
                _ => null,
            };

            if (hidden is not null)
            {
                return (
                    Describe(symbol, type, hidden),
                    "Declare it, and every type around it, internal or public: the registration the generator writes for "
                    + "the assembly refers to it from a class of its own");
            }

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
    /// Says why the statements the generator writes inside a type would not compile: the type, or one around it,
    /// takes the name <c>var</c> or <c>_</c>, which they write, and which would then refer to that type.
    /// </summary>
    private static (string Reason, string Remedy)? RefuseCapturingName(INamedTypeSymbol symbol)
    {
        for (var type = symbol; type is not null; type = type.ContainingType)
        {
            if (type.Name is "var" or "_")
            {
                return (
                    SymbolEqualityComparer.Default.Equals(type, symbol)
                        ? $"takes the name {type.Name}"
                        : $"is nested in the type '{type.Name}'",
                    $"Rename the type '{type.Name}': the generated code writes {type.Name} in its statements, where it would "
                    + "refer to that type instead");
            }
        }

        return null;
    }

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
        if (!members.Contains(symbol.Name))
        {
            return true;
        }

        diagnostics.Add(DiagnosticInfo.Create(
            DiagnosticDescriptors.UnsupportedDeclaration,
            location,
            symbol.Name,
            "takes the name of a member the generated code writes on it",
            "Rename it: C# does not let a member take the name of the type that declares it"));

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

            containingTypes.Insert(0, $"{DeclarationKeyword(containing)} {containing.ToDisplayString(IdentifierFormat)}");
        }

        return containingTypes;
    }

    /// <summary>
    /// Determines whether a type carries an annotation, by metadata name so that generic arity is respected.
    /// </summary>
    /// <param name="symbol">Type to inspect.</param>
    /// <param name="metadataName">Fully qualified metadata name of the attribute, arity included.</param>
    /// <returns><see langword="true"/> when the attribute is present.</returns>
    private static bool CarriesAttribute(INamedTypeSymbol symbol, string metadataName)
    {
        var separator = metadataName.LastIndexOf('.');
        var containingNamespace = metadataName.Substring(0, separator);
        var name = metadataName.Substring(separator + 1);

        return symbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass is { } attributeClass
            && string.Equals(attributeClass.MetadataName, name, StringComparison.Ordinal)
            && string.Equals(attributeClass.ContainingNamespace.ToDisplayString(), containingNamespace, StringComparison.Ordinal));
    }

    /// <summary>
    /// Reads the values declared through <c>[KnownValue]</c>, reporting the ones that cannot be generated.
    /// </summary>
    /// <param name="symbol">Annotated type.</param>
    /// <param name="underlying">Its underlying type.</param>
    /// <param name="generated">The names the generated code uses on the type.</param>
    /// <param name="location">Where to report.</param>
    /// <param name="diagnostics">Sink.</param>
    /// <returns>The known values that can be generated, in declaration order.</returns>
    private static List<KnownValueModel> ParseKnownValues(
        INamedTypeSymbol symbol,
        UnderlyingType underlying,
        HashSet<string> generated,
        Location location,
        List<DiagnosticInfo> diagnostics)
    {
        var knownValues = new List<KnownValueModel>();
        var accepted = new HashSet<string>(StringComparer.Ordinal);
        var declarations = new List<AttributeData>();
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != KnownValueAttributeName)
            {
                continue;
            }

            if (attribute.ConstructorArguments.Length < 2)
            {
                continue;
            }

            declarations.Add(attribute);
        }

        // Each known value is a property, whose getter takes the name get_ followed by its own, whichever of the
        // two is declared first.
        var getters = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
        {
            if (declaration.ConstructorArguments[0].Value is string requested)
            {
                getters.Add($"get_{requested}");
            }
        }

        foreach (var attribute in declarations)
        {

            var name = attribute.ConstructorArguments[0].Value as string;
            var argument = attribute.ConstructorArguments[1];

            var refusal = RefuseKnownValueName(name, symbol, generated, getters, accepted);
            if (refusal is not null)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.InvalidKnownValueName, location, name ?? "?", symbol.Name, refusal));
                continue;
            }

            // An array, a type and an enum member are legal arguments for the object parameter, but none is a
            // value of an underlying type. Reading the Value of an array constant throws, and read as text a type
            // would become its name and an enum member its number, so each is refused as written.
            var notAValue = argument.Kind is TypedConstantKind.Array or TypedConstantKind.Type or TypedConstantKind.Enum;
            if (notAValue || !LiteralFactory.TryCreate(underlying, argument.Value, out var literal))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    DiagnosticDescriptors.InvalidKnownValueLiteral,
                    location,
                    notAValue ? argument.ToCSharpString() : argument.Value?.ToString() ?? "null",
                    symbol.Name,
                    underlying.Keyword,
                    ExpectedForm(underlying)));
                continue;
            }

            var description = attribute.NamedArguments
                .FirstOrDefault(pair => string.Equals(pair.Key, "Description", StringComparison.Ordinal))
                .Value.Value as string;

            knownValues.Add(new KnownValueModel(name!, literal, description));
        }

        return knownValues;
    }

    /// <summary>
    /// Says why a known value cannot take a name, if it cannot.
    /// </summary>
    /// <remarks>
    /// A keyword is refused rather than escaped: a member a caller has to write as <c>@class</c> is no constant
    /// anyone wants. A contextual keyword is an ordinary identifier in a member's name, and stays allowed. The
    /// members the type has are the author's, getters included, which the generator's compilation holds without the
    /// generated ones, and the members of <see cref="object"/> a static property would hide, with a warning in the
    /// generated file. The getter of another known value is checked after them, so that a name the author's own
    /// property already holds is reported as the author's, even when a refused known value asked for it too.
    /// </remarks>
    /// <param name="name">The name the known value asks for.</param>
    /// <param name="symbol">The value object.</param>
    /// <param name="generated">The names the generated code uses on the type.</param>
    /// <param name="getters">The names the getters of the known values take.</param>
    /// <param name="accepted">The names of the known values accepted so far, to which this one is added.</param>
    /// <returns>The rule the name breaks, or <see langword="null"/> when it is usable.</returns>
    private static string? RefuseKnownValueName(
        string? name,
        INamedTypeSymbol symbol,
        HashSet<string> generated,
        HashSet<string> getters,
        HashSet<string> accepted)
    {
        if (string.IsNullOrEmpty(name) || !SyntaxFacts.IsValidIdentifier(name))
        {
            return "it is not a C# identifier";
        }

        if (SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
        {
            return "it is a C# keyword";
        }

        if (generated.Contains(name!))
        {
            return "the generated code already uses that name";
        }

        if (!symbol.GetMembers(name!).IsEmpty || InheritedNames.Contains(name!, StringComparer.Ordinal))
        {
            return "the type already has a member of that name";
        }

        if (getters.Contains(name!))
        {
            return "the generated code already uses that name";
        }

        return accepted.Add(name!) ? null : "another known value already takes that name";
    }

    private static string? ParseBound(
        UnderlyingType underlying,
        string? text,
        string boundName,
        INamedTypeSymbol symbol,
        Location location,
        List<DiagnosticInfo> diagnostics)
    {
        if (text is null)
        {
            return null;
        }

        // A string, a Guid or a bool has no order a bound could hold to. Accepted, the text would be published in
        // the schema as a limit that nothing enforces.
        if (!underlying.SupportsBounds)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                DiagnosticDescriptors.InvalidBound,
                location,
                text,
                boundName,
                underlying.Keyword,
                underlying.IsString
                    ? "a string takes no bound; constrain it with MinLength, MaxLength or Pattern"
                    : "the type takes no bound"));

            return null;
        }

        if (LiteralFactory.TryCreate(underlying, text, out var literal))
        {
            return literal;
        }

        diagnostics.Add(DiagnosticInfo.Create(
            DiagnosticDescriptors.InvalidBound, location, text, boundName, underlying.Keyword, ExpectedForm(underlying)));

        _ = symbol;
        return null;
    }

    private static string ExpectedForm(UnderlyingType underlying) => $"write a value of that type as {underlying.LiteralForm}";

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

    private static string DeclarationKeyword(INamedTypeSymbol symbol) => symbol switch
    {
        { IsRecord: true, TypeKind: TypeKind.Struct } => "partial record struct",
        { IsRecord: true } => "partial record",
        { TypeKind: TypeKind.Struct } => "partial struct",
        _ => "partial class",
    };

    private static string BuildHintName(INamedTypeSymbol symbol)
    {
        var qualified = symbol.ToDisplayString(QualifiedFormat).Replace("global::", string.Empty);

        var builder = new StringBuilder(qualified.Length + 8);
        foreach (var character in qualified)
        {
            builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '.' ? character : '_');
        }

        return builder.Append(".g.cs").ToString();
    }

    private static bool IsValidRegex(string pattern, out string error)
    {
        try
        {
            _ = new Regex(pattern);
            error = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message.Replace("\r", " ").Replace("\n", " ");
            return false;
        }
    }

    /// <summary>
    /// Reads the summary of the declaring type.
    /// </summary>
    /// <param name="symbol">Declared value object.</param>
    /// <param name="declaration">Its syntax.</param>
    /// <returns>The summary text, or <see langword="null"/> when the type has none.</returns>
    /// <remarks>
    /// A project that does not produce a documentation file compiles with <c>DocumentationMode.None</c>, and
    /// <c>GetDocumentationCommentXml</c> then returns nothing at all. Since most consumers leave that setting
    /// off, the trivia is read directly as a fallback.
    /// </remarks>
    private static string? ExtractSummary(INamedTypeSymbol symbol, TypeDeclarationSyntax declaration)
    {
        var fromCompilation = ExtractSummary(symbol.GetDocumentationCommentXml());
        if (fromCompilation is not null)
        {
            return fromCompilation;
        }

        var builder = new StringBuilder();
        foreach (var line in declaration.GetLeadingTrivia().ToFullString().Split(LineSeparators, StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("///", StringComparison.Ordinal))
            {
                builder.Append(trimmed, 3, trimmed.Length - 3).Append(' ');
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
        var collapsed = Regex.Replace(summary, @"\s+", " ").Trim();

        return collapsed.Length == 0 ? null : collapsed;
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
