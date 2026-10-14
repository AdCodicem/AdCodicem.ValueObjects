using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Reports a value object classified on its type that a <c>[LoggerMessage]</c> method logs in clear, and a classified
/// value-object property a transitive <c>[LogProperties]</c> expansion logs in clear.
/// </summary>
/// <remarks>
/// <para>
/// Microsoft.Gen.Logging, the logging generator of Microsoft.Extensions.Telemetry.Abstractions, redacts a parameter, a
/// property it expands or a record's constructor parameter of a property's name that carries an attribute derived from
/// <c>DataClassificationAttribute</c>, and the properties of a type it expands that the type classifies. It never reads
/// a classification on the type of a value it logs whole: a parameter, a property, a member of a record it writes through
/// its <c>ToString()</c> or the element of a collection whose type is classified goes to its plain tag array, and is
/// written in clear whatever redactor the application configures (<c>VO0045</c>). A value object is always logged whole, unless <c>[LogProperties]</c> expands it into its
/// <c>Value</c>, classified as its type, which the generator does only for a value object whose members it sees: one of
/// another assembly, or one written by hand. Expanding it that way drops a classification written on the property
/// holding it (<c>VO0046</c>).
/// </para>
/// <para>
/// Every attribute is matched by the metadata name of its class, or of the class it derives from, so the package
/// references neither Microsoft.Extensions.Compliance.Abstractions nor Microsoft.Extensions.Telemetry.Abstractions; a
/// type's classification is read by <see cref="ValueObjectGenerator.Classifications"/>, which empties the
/// <c>AttemptedValue</c> of a rejection, so the two read the same declaration. The analysis starts only where
/// <c>LogPropertiesAttribute</c> is visible, and reads only a method Microsoft.Gen.Logging implements, whose
/// implementation carries its <c>[GeneratedCode]</c>: Microsoft.Extensions.Logging's own generator, which writes the
/// method where Telemetry.Abstractions is not referenced, or where a project keeps it, redacts nothing at all.
/// </para>
/// <para>
/// That <c>[GeneratedCode]</c> is merged onto the author's partial definition, which makes the whole method generated
/// code to the analyzer driver: the analysis reads generated code, or it would never see a <c>[LoggerMessage]</c>
/// method. It reports on the author's own parameter, never inside a generated file.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DataClassificationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic property naming what the code fix classifies.</summary>
    internal const string TargetKey = "Target";

    /// <summary>The code fix classifies the parameter the diagnostic is reported on.</summary>
    internal const string ParameterTarget = "Parameter";

    /// <summary>
    /// The code fix classifies the property, or the record's positional parameter, of the diagnostic's additional
    /// location.
    /// </summary>
    internal const string MemberTarget = "Member";

    /// <summary>
    /// Nothing the code fix may classify: a member declared in another assembly, or a member of a record logged through
    /// its <c>ToString()</c>.
    /// </summary>
    internal const string NoTarget = "None";

    private const string LoggerMessageName = "Microsoft.Extensions.Logging.LoggerMessageAttribute";

    private const string LogPropertiesName = "Microsoft.Extensions.Logging.LogPropertiesAttribute";

    private const string TagProviderName = "Microsoft.Extensions.Logging.TagProviderAttribute";

    private const string LogPropertyIgnoreName = "Microsoft.Extensions.Logging.LogPropertyIgnoreAttribute";

    private const string ContractName = "AdCodicem.ValueObjects.IValueObject`2";

    private const string GeneratedCodeName = "System.CodeDom.Compiler.GeneratedCodeAttribute";

    private const string CompilerGeneratedName = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";

    /// <summary>The tool name Microsoft.Gen.Logging writes in the <c>[GeneratedCode]</c> of each method it implements.</summary>
    private const string LoggingGeneratorName = "Microsoft.Gen.Logging";

    /// <summary>The namespace of <c>DataClassificationAttribute</c>.</summary>
    private const string ClassificationNamespace = "Microsoft.Extensions.Compliance.Classification";

    private const string Category = "AdCodicem.ValueObjects";

    private const string HelpLink = "https://adcodicem.github.io/AdCodicem.ValueObjects/docs/how-to/logging#data-classification";

    /// <summary>What a transitive expansion is written as, in <c>VO0046</c>.</summary>
    private const string TransitiveExpansion = "[LogProperties(Transitive = true)]";

    /// <summary>The format a message names a type in: as the consumer writes it, keywords included.</summary>
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.CSharpShortErrorMessageFormat;

    /// <summary>VO0045, a warning: a value object classified on its type, logged whole without a classification of its own.</summary>
    public static readonly DiagnosticDescriptor LoggedInClear = new(
        "VO0045",
        "Classified value object logged without its classification",
        "'{0}' is logged in clear: its type '{1}' is classified {2}, which the logging generator reads on a parameter or "
        + "a property only. {3}.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Microsoft.Gen.Logging, the logging generator of Microsoft.Extensions.Telemetry.Abstractions, redacts "
        + "a parameter or a property that carries an attribute derived from DataClassificationAttribute, and ignores one "
        + "on the type of the value: a value object classified on its type only is written to the log as it is, whatever "
        + "redactor the application configures.",
        helpLinkUri: HelpLink);

    /// <summary>VO0046, a warning: a classified property whose value object an expansion writes as its unclassified <c>Value</c>.</summary>
    public static readonly DiagnosticDescriptor DroppedByTransitive = new(
        "VO0046",
        "Classification dropped by a transitive expansion",
        "'{0}' is logged in clear as '{0}.Value': {1} expands the value object '{2}' and drops the classification of the "
        + "property. Classify the type '{2}' itself, or {3}.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A transitive [LogProperties], or [LogProperties] on the property, expands a value object of another "
        + "assembly, or one written by hand, into its public properties, Value first, which carry the classification of "
        + "the value object's type alone: the classification of the property holding it is lost, and the value is "
        + "written to the log as it is.",
        helpLinkUri: HelpLink);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [LoggedInClear, DroppedByTransitive];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        // The logging generator implements the partial method with [GeneratedCode], which C# merges onto the author's
        // definition: without Analyze, no [LoggerMessage] method it writes would ever be seen. Every diagnostic lands on
        // the author's own declaration, so none needs ReportDiagnostics, and a [LoggerMessage] another generator declares
        // gets no warning its user could not act on.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var compilation = compilationContext.Compilation;
            var loggerMessage = compilation.GetTypesByMetadataName(LoggerMessageName);
            var logProperties = compilation.GetTypesByMetadataName(LogPropertiesName);
            var contract = compilation.GetTypesByMetadataName(ContractName);

            // LogPropertiesAttribute is Microsoft.Extensions.Telemetry.Abstractions', whose build targets hand
            // [LoggerMessage] to Microsoft.Gen.Logging. Without it, nothing redacts, and nothing is reported.
            if (loggerMessage.IsEmpty || logProperties.IsEmpty || contract.IsEmpty)
            {
                return;
            }

            var symbols = new Symbols(
                compilation,
                loggerMessage,
                logProperties,
                compilation.GetTypesByMetadataName(TagProviderName),
                compilation.GetTypesByMetadataName(LogPropertyIgnoreName),
                contract,
                ValueObjectAnnotations.Resolve(compilation));

            compilationContext.RegisterSymbolAction(symbolContext => AnalyzeMethod(symbolContext, symbols), SymbolKind.Method);
        });
    }

    /// <summary>
    /// Gets the attributes classifying a value object's type, in their declared order, a construction of a generic one
    /// reading its definition's: the ones a message names and the code fix copies.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The attributes, empty for a type that is not classified, or not a named type.</returns>
    internal static ImmutableArray<AttributeData> Classifications(ITypeSymbol type)
        => type is INamedTypeSymbol named ? ValueObjectGenerator.Classifications(named.OriginalDefinition) : [];

    /// <summary>
    /// Determines whether the <c>[AttributeUsage]</c> an attribute class declares, or inherits from the nearest base
    /// declaring one, admits a target: an application's classification may be restricted to types.
    /// </summary>
    /// <param name="attributeClass">The class of the attribute, <see langword="null"/> past the root of its hierarchy.</param>
    /// <param name="target">The target, a parameter or a property.</param>
    /// <returns><see langword="true"/> when the attribute may be written there.</returns>
    internal static bool AllowsTarget(INamedTypeSymbol? attributeClass, AttributeTargets target)
        => attributeClass is null
           || (Usage(attributeClass) is { } validOn ? (validOn & target) != 0 : AllowsTarget(attributeClass.BaseType, target));

    /// <summary>Gets the type a parameter or a property logs: its own, or the one its <see cref="Nullable{T}"/> makes nullable.</summary>
    /// <param name="type">The declared type.</param>
    /// <returns>The type logged.</returns>
    internal static ITypeSymbol Unwrap(ITypeSymbol type)
        => type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;

    /// <summary>Gets the element of an <see cref="IEnumerable{T}"/>, an array's included, or <see langword="null"/>.</summary>
    /// <param name="type">The declared type.</param>
    /// <returns>The element type, which may itself be nullable.</returns>
    internal static ITypeSymbol? Element(ITypeSymbol type)
    {
        var enumerable = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T } self
            ? self
            : type.AllInterfaces.FirstOrDefault(static candidate =>
                candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

        return enumerable?.TypeArguments[0];
    }

    /// <summary>
    /// Gets the type a logging parameter's or a property's classification would come from: the element of a collection, a
    /// nullable one included, or the value object it is or makes nullable, unwrapped. A value object is no collection.
    /// </summary>
    /// <param name="parameterType">The declared type of the parameter or the property.</param>
    /// <returns>The type whose classification the code fix copies.</returns>
    internal static ITypeSymbol LoggedType(ITypeSymbol parameterType)
        => Unwrap(Element(Unwrap(parameterType)) ?? parameterType);

    /// <summary>
    /// Determines whether Microsoft.Gen.Logging implements a partial method: its implementation part carries
    /// <c>[GeneratedCode("Microsoft.Gen.Logging", …)]</c>.
    /// </summary>
    /// <param name="method">The partial definition.</param>
    /// <returns><see langword="true"/> when that generator wrote the method.</returns>
    internal static bool WrittenByTheLoggingGenerator(IMethodSymbol method)
        => method.PartialImplementationPart is { } implementation
           && implementation.GetAttributes().Any(static attribute =>
               attribute.AttributeClass!.ToDisplayString() == GeneratedCodeName
               && attribute.ConstructorArguments.FirstOrDefault().Value as string == LoggingGeneratorName);

    /// <summary>
    /// Determines whether an attribute class derives from <c>DataClassificationAttribute</c>,
    /// <c>NoDataClassificationAttribute</c> included: a classification of the author's own, whatever it says.
    /// </summary>
    /// <param name="attributeClass">The class of an attribute.</param>
    /// <returns><see langword="true"/> for any classification.</returns>
    internal static bool IsClassification(INamedTypeSymbol? attributeClass)
    {
        for (var type = attributeClass; type is not null; type = type.BaseType)
        {
            if (type.MetadataName == "DataClassificationAttribute"
                && type.ContainingNamespace.ToDisplayString() == ClassificationNamespace)
            {
                return true;
            }
        }

        return false;
    }

    private static AttributeTargets? Usage(INamedTypeSymbol attributeClass)
    {
        foreach (var attribute in attributeClass.GetAttributes())
        {
            if (attribute.AttributeClass!.ToDisplayString() == "System.AttributeUsageAttribute")
            {
                // An [AttributeUsage] the compiler refuses names no target: the nearest base's applies, as the
                // compiler would read none.
                return attribute.ConstructorArguments.FirstOrDefault().Value is int validOn ? (AttributeTargets)validOn : null;
            }
        }

        return null;
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, Symbols symbols)
    {
        var method = (IMethodSymbol)context.Symbol;

        // The implementation part, which a generator writes, is analyzed through its definition, the author's.
        if (method.PartialDefinitionPart is not null
            || !method.GetAttributes().Any(attribute => Symbols.Is(attribute, symbols.LoggerMessage))
            || !WrittenByTheLoggingGenerator(method))
        {
            return;
        }

        foreach (var parameter in method.Parameters)
        {
            AnalyzeParameter(context, parameter, symbols);
        }
    }

    private static void AnalyzeParameter(SymbolAnalysisContext context, IParameterSymbol parameter, Symbols symbols)
    {
        var attributes = parameter.GetAttributes();
        var location = parameter.Locations[0];

        // A tag provider decides what is logged, and a classification of the parameter's own, NoDataClassificationAttribute
        // included, is the author's choice. Both come before [LogProperties], which the logging generator refuses beside
        // either (LOGGEN017), and still implements.
        if (attributes.Any(attribute => Symbols.Is(attribute, symbols.TagProvider) || IsClassification(attribute.AttributeClass)))
        {
            return;
        }

        var logged = Unwrap(parameter.Type);
        if (attributes.FirstOrDefault(attribute => Symbols.Is(attribute, symbols.LogProperties)) is { } logProperties)
        {
            // [LogProperties] on a value object expands it into its Value, classified as its type, where the logging
            // generator sees its members, and reports LOGGEN020 where it does not: its properties hold no value object.
            // A collection interface, an array or a collection with no property it reports (LOGGEN018, LOGGEN020), and logs
            // whole: that is left to its warning.
            AnalyzeMembers(
                context,
                location,
                parameter.Name,
                logged,
                IsTransitive(logProperties),
                symbols,
                new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

            return;
        }

        // The parameter itself, or the elements of a collection, a nullable one included, which a classified parameter
        // erases whole.
        var classified = symbols.ClassifiedValueObject(logged)
            ?? (Element(logged) is { } element ? symbols.ClassifiedValueObject(Unwrap(element)) : null);
        if (classified is not null)
        {
            Report(context, location, ParameterTarget, declaration: null, parameter.Name, classified, Remedy(parameter.Name, classified));
            return;
        }

        // A record is logged through its ToString(), which writes its public fields and properties. The logging generator
        // refuses one whose member is classified (LOGGEN035), and misses one whose member's type alone is.
        if (logged is INamedTypeSymbol { IsRecord: true } record)
        {
            foreach (var member in PrintedMembers(record))
            {
                var path = $"{parameter.Name}.{member.Name}";
                if (Own(member).IsEmpty && symbols.ClassifiedValueObject(Unwrap(TypeOf(member))) is { } printed)
                {
                    Report(
                        context,
                        location,
                        NoTarget,
                        declaration: null,
                        path,
                        printed,
                        $"Log '{parameter.Name}' with [LogProperties] and classify '{path}' with {Shown(printed)}: the logging "
                        + "generator refuses a record it logs whole whose property is classified (LOGGEN035)");
                }
            }
        }
    }

    /// <summary>
    /// Reads the properties of a type <c>[LogProperties]</c> expands, as the logging generator does, down every property
    /// it expands in turn: under the parameter's <c>Transitive</c>, or carrying <c>[LogProperties]</c> itself.
    /// </summary>
    private static void AnalyzeMembers(
        SymbolAnalysisContext context,
        Location location,
        string path,
        ITypeSymbol type,
        bool transitive,
        Symbols symbols,
        HashSet<ITypeSymbol> visited)
    {
        // A type already expanded on the way here is not expanded again: a reference to itself ends the walk.
        if (type is not INamedTypeSymbol named || !visited.Add(named))
        {
            return;
        }

        // The logging generator classifies every property of a type it expands as the type, or a base type, is classified.
        var classifiedType = ClassifiedOnItsType(named);

        foreach (var (property, expands) in LoggedProperties(named, transitive, symbols))
        {
            var memberPath = $"{path}.{property.Name}";
            var own = Own(property);
            var propertyType = Unwrap(property.Type);

            if (symbols.ClassifiedValueObject(propertyType) is { } classified)
            {
                // Expanded, the value object's Value carries its type's classification.
                if (own.IsEmpty && !classifiedType && !(expands && symbols.MembersVisible(classified)))
                {
                    ReportMember(context, location, property, memberPath, classified);
                }

                continue;
            }

            if (symbols.IsValueObject(propertyType))
            {
                // NoDataClassificationAttribute asks for the value in clear, which is what the expansion writes.
                if (expands
                    && (classifiedType || own.Any(static attribute => ValueObjectGenerator.IsSensitiveClassification(attribute.AttributeClass)))
                    && symbols.MembersVisible(propertyType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DroppedByTransitive,
                        location,
                        memberPath,
                        transitive ? TransitiveExpansion : "[LogProperties] on the property",
                        propertyType.ToDisplayString(TypeFormat),
                        transitive ? "drop Transitive" : "drop [LogProperties] from the property"));
                }

                continue;
            }

            // The logging generator expands any other type it meets, but for text, a type of System it writes as it is, a
            // collection interface or an array, and a type with no property to log, and drops a classification written on
            // the property holding it as it does for a value object. Transitive is read on the parameter alone: written on a
            // property, [LogProperties(Transitive = true)] expands it one level, as [LogProperties] does.
            if (expands && Expandable(propertyType, transitive, symbols))
            {
                AnalyzeMembers(context, location, memberPath, propertyType, transitive, symbols, visited);
            }
            else if (own.IsEmpty
                     && !classifiedType
                     && Element(property.Type) is { } element
                     && symbols.ClassifiedValueObject(Unwrap(element)) is { } item)
            {
                // A collection it does not expand is written element by element, through LoggerMessageHelper.Stringify.
                ReportMember(context, location, property, memberPath, item);
            }
        }

        visited.Remove(named);
    }

    /// <summary>
    /// Gets the properties the logging generator logs of a type it expands, as it reads them: declared on the type or a
    /// base type, an override or a property hiding another read where it is declared deepest, each with whether it
    /// expands that property in turn, which it does under <c>Transitive</c> or <c>[LogProperties]</c> on the property, for
    /// a public instance property with a public getter, skipping any other.
    /// </summary>
    /// <remarks>
    /// Without either, a property is logged whole when it is a public instance property with a public getter that
    /// <c>[LogPropertyIgnore]</c> does not leave out. Under <c>Transitive</c>, <c>[LogPropertyIgnore]</c> only stops the
    /// expansion, and the property is logged whole all the same; and <c>[TagProvider]</c> on a property is never called:
    /// the property is logged whole, in clear (Microsoft.Gen.Logging 10.11.0).
    /// </remarks>
    private static IEnumerable<(IPropertySymbol Property, bool Expands)> LoggedProperties(
        INamedTypeSymbol type,
        bool transitive,
        Symbols symbols)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsIndexer || !seen.Add(property.Name))
                {
                    continue;
                }

                var attributes = property.GetAttributes();
                var provided = attributes.Any(attribute => Symbols.Is(attribute, symbols.TagProvider));
                var ignored = attributes.Any(attribute => Symbols.Is(attribute, symbols.LogPropertyIgnore));
                var expands = attributes.Any(attribute => Symbols.Is(attribute, symbols.LogProperties))
                    || (transitive && !provided && !ignored);
                var readable = !property.IsStatic && property.GetMethod is { DeclaredAccessibility: Accessibility.Public };

                // A tag provider on a property the logging generator cannot read is its error (LOGGEN034).
                if (expands ? readable : transitive || (readable && !ignored))
                {
                    yield return (property, expands);
                }
            }
        }
    }

    /// <summary>
    /// Determines whether the logging generator expands a property's type into its own properties: a type of no special
    /// kind, neither text, nor a type of System such as <c>DateTime</c>, nor a collection interface such as
    /// <c>IEnumerable&lt;T&gt;</c> or <c>IReadOnlyList&lt;T&gt;</c>, nor an array, that has a property to log.
    /// </summary>
    private static bool Expandable(ITypeSymbol type, bool transitive, Symbols symbols)
        => type is INamedTypeSymbol named
           && named.OriginalDefinition.SpecialType == SpecialType.None
           && LoggedProperties(named, transitive, symbols).Any();

    /// <summary>
    /// Gets the members a record's <c>ToString()</c> writes, as the compiler writes it: the public instance fields and
    /// properties with a getter, an override left to the record declaring the property, of the record and of each base
    /// record, which <c>[LogPropertyIgnore]</c> and <c>[TagProvider]</c> do not leave out. A record, or a base record,
    /// that writes its own <c>ToString()</c> or <c>PrintMembers</c> writes what it likes: none of its members is read.
    /// </summary>
    private static IEnumerable<ISymbol> PrintedMembers(INamedTypeSymbol record)
    {
        if (WrittenByHand(CalledToString(record)))
        {
            yield break;
        }

        // A record derives from a record or from object, which prints nothing, as ValueType does for a record struct.
        for (var current = record; current is not null; current = current.BaseType)
        {
            if (Declared(current, "PrintMembers", 1) is { } printMembers && WrittenByHand(printMembers))
            {
                yield break;
            }

            foreach (var member in current.GetMembers())
            {
                if (member is IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public }
                    or IPropertySymbol { IsStatic: false, IsIndexer: false, IsOverride: false, DeclaredAccessibility: Accessibility.Public, GetMethod: not null })
                {
                    yield return member;
                }
            }
        }
    }

    /// <summary>
    /// Gets the <c>ToString()</c> the logger calls on a record: the one it declares, or the one a base record seals, which
    /// the record then declares none beside.
    /// </summary>
    private static IMethodSymbol CalledToString(INamedTypeSymbol record)
    {
        var current = record;
        IMethodSymbol? toString;
        while ((toString = Declared(current, "ToString", 0)) is null)
        {
            // Object declares one: a base declares it before the walk runs out.
            current = current.BaseType!;
        }

        return toString;
    }

    /// <summary>Gets the instance method of a name and a number of parameters a type declares, or <see langword="null"/>.</summary>
    private static IMethodSymbol? Declared(INamedTypeSymbol type, string name, int parameters)
        => type.GetMembers(name).OfType<IMethodSymbol>().FirstOrDefault(method => !method.IsStatic && method.Parameters.Length == parameters);

    /// <summary>
    /// Determines whether a record's member is the author's rather than the compiler's: the compiler's is implicitly
    /// declared in source, and carries <c>[CompilerGenerated]</c> in metadata.
    /// </summary>
    private static bool WrittenByHand(IMethodSymbol method)
        => !method.IsImplicitlyDeclared
           && !method.GetAttributes().Any(static attribute => attribute.AttributeClass!.ToDisplayString() == CompilerGeneratedName);

    /// <summary>Gets the type of a field or a property.</summary>
    private static ITypeSymbol TypeOf(ISymbol member) => member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type;

    /// <summary>
    /// Determines whether a type, or a base type, carries a classification other than <c>NoDataClassificationAttribute</c>,
    /// which the logging generator applies to every property of the type it expands.
    /// </summary>
    private static bool ClassifiedOnItsType(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetAttributes().Any(static attribute => ValueObjectGenerator.IsSensitiveClassification(attribute.AttributeClass)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the classifications the logging generator reads for a member: its own, and for a record's property those of a
    /// parameter of the same name of one of its constructors, whatever its type. <c>NoDataClassificationAttribute</c>
    /// counts, as the author's choice.
    /// </summary>
    private static ImmutableArray<AttributeData> Own(ISymbol member)
        => [.. member.GetAttributes()
            .Concat(member is IPropertySymbol { ContainingType.IsRecord: true } property ? Positional(property) : [])
            .Where(static attribute => IsClassification(attribute.AttributeClass))];

    /// <summary>
    /// Gets the attributes of the parameters of a record's constructors that carry a property's name, which the logging
    /// generator reads as the property's: the positional parameter the property comes from, or any other.
    /// </summary>
    private static IEnumerable<AttributeData> Positional(IPropertySymbol property)
        => property.ContainingType.InstanceConstructors
            .SelectMany(static constructor => constructor.Parameters)
            .Where(parameter => parameter.Name == property.Name)
            .SelectMany(static parameter => parameter.GetAttributes());

    /// <summary>
    /// Gets where the code fix classifies a property: its declaration, which for a record's positional property is the
    /// positional parameter, read by the logging generator as it reads the property; <see langword="null"/> when it is
    /// not in source.
    /// </summary>
    private static Location? Declaration(IPropertySymbol property)
        => property.Locations.FirstOrDefault(static candidate => candidate.IsInSource);

    private static bool IsTransitive(AttributeData attribute)
        => attribute.NamedArguments.Any(static argument => argument.Key == "Transitive" && argument.Value.Value is true);

    /// <summary>Gets the classifications of a value object's type as a message shows them: <c>[PersonalData]</c>.</summary>
    private static string Shown(ITypeSymbol valueObject)
        => string.Join(", ", Classifications(valueObject).Select(static attribute => ValueObjectAnnotations.Shown(attribute.AttributeClass!)));

    private static string Remedy(string logged, ITypeSymbol valueObject)
        => $"Classify '{logged}' with {Shown(valueObject)}, or with [NoDataClassification] to log it in clear on purpose";

    private static void Report(
        SymbolAnalysisContext context,
        Location location,
        string target,
        Location? declaration,
        string logged,
        ITypeSymbol valueObject,
        string remedy)
        => context.ReportDiagnostic(Diagnostic.Create(
            LoggedInClear,
            location,
            declaration is null ? null : [declaration],
            ImmutableDictionary<string, string?>.Empty.Add(TargetKey, target),
            logged,
            valueObject.ToDisplayString(TypeFormat),
            Shown(valueObject),
            remedy));

    /// <summary>
    /// Reports a member an expansion logs in clear on the logging parameter, with the declaration the code fix classifies
    /// as its additional location, none for a member declared in another assembly.
    /// </summary>
    private static void ReportMember(SymbolAnalysisContext context, Location location, IPropertySymbol property, string path, ITypeSymbol valueObject)
    {
        var declaration = Declaration(property);
        Report(context, location, declaration is null ? NoTarget : MemberTarget, declaration, path, valueObject, Remedy(path, valueObject));
    }

    /// <summary>The symbols the analysis compares with, resolved once per compilation.</summary>
    private sealed class Symbols(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> loggerMessage,
        ImmutableArray<INamedTypeSymbol> logProperties,
        ImmutableArray<INamedTypeSymbol> tagProvider,
        ImmutableArray<INamedTypeSymbol> logPropertyIgnore,
        ImmutableArray<INamedTypeSymbol> contract,
        ImmutableArray<INamedTypeSymbol> annotations)
    {
        public ImmutableArray<INamedTypeSymbol> LoggerMessage { get; } = loggerMessage;

        public ImmutableArray<INamedTypeSymbol> LogProperties { get; } = logProperties;

        public ImmutableArray<INamedTypeSymbol> TagProvider { get; } = tagProvider;

        public ImmutableArray<INamedTypeSymbol> LogPropertyIgnore { get; } = logPropertyIgnore;

        public static bool Is(AttributeData attribute, ImmutableArray<INamedTypeSymbol> candidates)
            => candidates.Contains(attribute.AttributeClass!, SymbolEqualityComparer.Default);

        /// <summary>Gets a type when it is a value object classified on its type, or <see langword="null"/>.</summary>
        public ITypeSymbol? ClassifiedValueObject(ITypeSymbol type)
            => IsValueObject(type) && !Classifications(type).IsEmpty ? type : null;

        /// <summary>
        /// Determines whether a type is a value object: a struct implementing <c>IValueObject&lt;TSelf, TValue&gt;</c> over
        /// itself, through the generated partial, which an analyzer sees, or by hand. The contract constrains
        /// <c>TSelf</c> to a struct.
        /// </summary>
        public bool IsValueObject(ITypeSymbol type)
            => type is INamedTypeSymbol named
               && named.AllInterfaces.Any(candidate =>
                   contract.Contains(candidate.OriginalDefinition, SymbolEqualityComparer.Default)
                   && SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], named));

        /// <summary>
        /// Determines whether the logging generator sees a value object's members: one of another assembly, or one written
        /// by hand. Source generators never see each other's output, so it sees none of the members this package's
        /// generator writes in the same compilation, and logs such a value object whole, through its <c>ToString()</c>.
        /// </summary>
        public bool MembersVisible(ITypeSymbol type)
            => !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)
               || !ValueObjectAnnotations.IsAnnotated((INamedTypeSymbol)type, annotations);
    }
}
