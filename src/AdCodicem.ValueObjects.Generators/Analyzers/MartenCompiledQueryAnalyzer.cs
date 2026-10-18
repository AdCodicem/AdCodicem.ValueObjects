using System.Collections.Immutable;
using System.Linq;
using AdCodicem.ValueObjects.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AdCodicem.ValueObjects.Generators.Analyzers;

/// <summary>
/// Reports a value object held by a member of a Marten compiled query, which Marten refuses as a parameter, or answers
/// with the first call's value once it takes the type for a document's identity.
/// </summary>
/// <remarks>
/// <para>
/// Marten plans a compiled query from the type implementing <c>ICompiledQuery&lt;TDoc, TOut&gt;</c>, which its other
/// compiled-query interfaces extend (<c>ICompiledQueryMarker</c> alone is no compiled query: Marten refuses to register a
/// type implementing only it), and reads every public instance field and property of that type as a parameter, declared
/// or inherited, read-only, write-only and indexers included, but one marked <c>[MartenIgnore]</c> or an attribute
/// deriving from it (<c>CompiledQueryPlan.findMembers</c>). It binds a parameter of its own types alone, refuses a member
/// of any other type with <c>InvalidCompiledQueryException</c> unless the type is in its list of value types, and answers
/// every call with the first call's value of one it accepts. <c>AdCodicem.ValueObjects.Marten</c> keeps every value object
/// out of that list, so that Marten refuses a single one; Marten lists one itself once it has built the mapping of a
/// document whose identity it is, out of any package's reach, and from then on accepts the query type and replays its
/// first identity. A collection of value objects is left alone: the package binds an array or a list, a type implementing
/// <c>IList&lt;T&gt;</c>, on every call, and Marten refuses any other itself. Marten's source generator discovers the same
/// query types, and Marten plans them through the same check before any descriptor it wrote.
/// </para>
/// <para>
/// The remedy names what to write instead, by Marten 9.50's parameter finders: a member compared with the value object's
/// <c>Value</c>, of the underlying type for <see langword="string"/>, <see cref="System.Guid"/>, <see langword="int"/>,
/// <see langword="long"/>, <see langword="float"/>, <see langword="decimal"/>, <see cref="System.DateTime"/> and
/// <see cref="System.DateTimeOffset"/>, set in UTC for the last, since Marten writes the member as it stands and Npgsql
/// refuses any other offset, an <see langword="int"/> for an <see langword="sbyte"/>, a <see langword="byte"/> or a
/// <see langword="short"/>, and a <see langword="long"/> for a <see langword="uint"/>, as C# compares their <c>Value</c>;
/// an array or a list of the value object for the other types Marten queries, whose member Marten binds none of; removal
/// for the four Marten queries nowhere; for an indexer, which Marten reads and can bind none of, making it non-public or
/// removing it; and a LINQ query for a value object written by hand over a type outside the table, for which the analyzer
/// knows no form. It never names a widening Marten matches by value, such as a <see langword="float"/> member against a
/// <see langword="double"/>, which it answers with the first call's value. A value object Marten may take for a
/// document's identity, as <c>ValueTypeIdGeneration</c> takes a public struct with exactly one public property, static
/// ones counted, of an identity type, gets the collection form too: Marten reads no <c>Value</c> of a document's identity.
/// </para>
/// <para>
/// Both forms read right only under <c>AdCodicem.ValueObjects.Marten</c>'s <c>UseValueObjects()</c>: without it, Marten
/// reads a <c>Value</c> no document holds, answering no rows, and nothing binds a collection of value objects. The message
/// names it whether or not the compilation references the package, since a query type often lives in a library that does
/// not reference the package its host uses.
/// </para>
/// <para>
/// The interface and the attribute are matched by full metadata name, so the analyzer needs no reference to Marten, and
/// one of the same name in another namespace, or nested in a type, is left alone. Nothing is reported in a compilation
/// that does not reference Marten's interface. A member is reported on its declaration, or, inherited from a type that is
/// no compiled query of the compilation's source, or whose type became a value object through a derived type's type
/// arguments, on the compiled query, naming the type declaring it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MartenCompiledQueryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The interface every compiled query implements, directly or through another of Marten's.</summary>
    private const string CompiledQueryName = "Marten.Linq.ICompiledQuery`2";

    /// <summary>The attribute that keeps a member out of Marten's parameters.</summary>
    private const string MartenIgnoreName = "Marten.Events.CodeGeneration.MartenIgnoreAttribute";

    private const string ContractName = "AdCodicem.ValueObjects.IValueObject`2";

    /// <summary>The call both forms need, under which the Marten package teaches the store every value object.</summary>
    private const string Package = "AdCodicem.ValueObjects.Marten's UseValueObjects()";

    /// <summary>The form Marten's planning lets the package bind, an array or a list member alone.</summary>
    private const string Collection = "hold the values in an array or a list of the value object (a type implementing "
        + "IList<T>), read with Contains or IsOneOf, which " + Package + " binds on every call";

    /// <summary>What a <see cref="System.DateTimeOffset"/> member needs: Marten writes it as it stands, at its offset.</summary>
    private const string InUtc = ", set in UTC with ToUniversalTime() (Marten writes the member as it stands, and Npgsql "
        + "refuses any other offset),";

    /// <summary>The types Marten takes a struct's one property of for a document's identity, as the format names them.</summary>
    private static readonly ImmutableHashSet<string> IdentityTypes = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "global::System.Int32",
        "global::System.Int64",
        "global::System.Guid",
        "global::System.String");

    /// <summary>The underlying types Marten binds a compiled query's member of, among those a value object may hold.</summary>
    private static readonly ImmutableHashSet<UnderlyingKind> BoundByMarten = ImmutableHashSet.Create(
        UnderlyingKind.String,
        UnderlyingKind.Guid,
        UnderlyingKind.Int32,
        UnderlyingKind.Int64,
        UnderlyingKind.Single,
        UnderlyingKind.Decimal,
        UnderlyingKind.DateTime,
        UnderlyingKind.DateTimeOffset);

    /// <summary>The underlying types C# promotes to an <see langword="int"/> when it compares them.</summary>
    private static readonly ImmutableHashSet<UnderlyingKind> PromotedToInt = ImmutableHashSet.Create(
        UnderlyingKind.SByte,
        UnderlyingKind.Byte,
        UnderlyingKind.Int16);

    /// <summary>The underlying types Marten maps to no column, which it queries nowhere.</summary>
    private static readonly ImmutableHashSet<UnderlyingKind> QueriedNowhere = ImmutableHashSet.Create(
        UnderlyingKind.UInt16,
        UnderlyingKind.UInt64,
        UnderlyingKind.Int128,
        UnderlyingKind.UInt128);

    /// <summary>The format a message names a type in: as the consumer writes it, keywords included.</summary>
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.CSharpShortErrorMessageFormat;

    /// <summary>VO0048, an error: a value object held by a member of a Marten compiled query.</summary>
    public static readonly DiagnosticDescriptor ValueObjectInCompiledQuery = new(
        "VO0048",
        "Value object held by a Marten compiled query",
        "'{0}' of the compiled query '{1}' holds the value object '{2}', which {3}. {4}.",
        "AdCodicem.ValueObjects",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Marten reads every public instance field and property of a compiled query as a parameter, and binds "
        + "none of a value object: it refuses the query type with InvalidCompiledQueryException, or, once it takes the "
        + "value object for a document's identity, answers every call with the first call's value. The forms the message "
        + "names, the value object's Value compared with a member of a type Marten binds, or an array or a list of value "
        + "objects, need " + Package + ": without it, Marten reads a Value no document holds. A collection of value "
        + "objects is not reported: the package binds an array or a list, and Marten refuses any other itself.",
        helpLinkUri: "https://adcodicem.github.io/AdCodicem.ValueObjects/docs/how-to/marten#compiled-queries");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [ValueObjectInCompiledQuery];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            return;
        }

        // The query types the consumer writes: Marten's source generator writes descriptors, never a query type.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            if (!compilationContext.Compilation.GetTypesByMetadataName(CompiledQueryName).IsEmpty)
            {
                compilationContext.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
            }
        });
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct) || !IsCompiledQuery(type))
        {
            return;
        }

        // Reflection hands Marten the most derived declaration of an overridden property alone, with its own attributes.
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        for (var current = type; current is { SpecialType: not SpecialType.System_Object }; current = current.BaseType)
        {
            var declaredHere = SymbolEqualityComparer.Default.Equals(current, type);
            foreach (var member in current.GetMembers())
            {
                if (overridden.Contains(member.OriginalDefinition))
                {
                    continue;
                }

                for (var replaced = (member as IPropertySymbol)?.OverriddenProperty; replaced is not null; replaced = replaced.OverriddenProperty)
                {
                    overridden.Add(replaced.OriginalDefinition);
                }

                if (ParameterType(member) is not { } memberType
                    || ValueObject(memberType) is not (var valueObject, var value, var nullable)
                    || (!declaredHere && ReportedWhereDeclared(member)))
                {
                    continue;
                }

                var identity = MayBeIdentity(valueObject);
                context.ReportDiagnostic(Diagnostic.Create(
                    ValueObjectInCompiledQuery,
                    declaredHere ? member.Locations[0] : type.Locations[0],
                    declaredHere ? Shown(member) : $"{current.ToDisplayString(TypeFormat)}.{Shown(member)}",
                    type.ToDisplayString(TypeFormat),
                    memberType.ToDisplayString(TypeFormat),
                    Effect(identity, nullable),
                    Remedy(member, value, identity)));
            }
        }
    }

    /// <summary>Determines whether a type implements Marten's <c>ICompiledQuery&lt;TDoc, TOut&gt;</c>.</summary>
    private static bool IsCompiledQuery(INamedTypeSymbol type)
        => type.AllInterfaces.Any(static contract => SerializerContractAnalyzer.FullName(contract.OriginalDefinition) == CompiledQueryName);

    /// <summary>
    /// Gets the type of a member Marten reads as a parameter, a public instance field or property, an indexer included,
    /// not marked <c>[MartenIgnore]</c>; or <see langword="null"/> for any other member.
    /// </summary>
    private static ITypeSymbol? ParameterType(ISymbol member)
    {
        var type = member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => null,
        };

        return type is not null
               && !member.IsStatic
               && member.DeclaredAccessibility == Accessibility.Public
               && !member.GetAttributes().Any(static attribute => Ignores(attribute.AttributeClass))
            ? type
            : null;
    }

    /// <summary>Determines whether an attribute class is Marten's <c>[MartenIgnore]</c>, or derives from it.</summary>
    private static bool Ignores(INamedTypeSymbol? attributeClass)
    {
        for (var current = attributeClass; current is not null; current = current.BaseType)
        {
            if (SerializerContractAnalyzer.FullName(current) == MartenIgnoreName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets a member as the message names it: an indexer as C# declares it, <c>this[int]</c>.</summary>
    private static string Shown(ISymbol member)
        => member is IPropertySymbol { IsIndexer: true } indexer
            ? $"this[{string.Join(", ", indexer.Parameters.Select(static parameter => parameter.Type.ToDisplayString(TypeFormat)))}]"
            : member.Name;

    /// <summary>
    /// Determines whether a member a compiled query inherits is reported on its own declaration instead: declared in
    /// source, by a type implementing the interface, with a value object for its declared type.
    /// </summary>
    private static bool ReportedWhereDeclared(ISymbol member)
    {
        var definition = member.OriginalDefinition;

        return definition.Locations[0].IsInSource
               && IsCompiledQuery(definition.ContainingType)
               && ValueObject(ParameterType(definition)) is not null;
    }

    /// <summary>
    /// Gets the value object a type is, or makes nullable, its underlying type and whether it is nullable; or
    /// <see langword="null"/> when it is none, a collection of value objects among them.
    /// </summary>
    private static (INamedTypeSymbol ValueObject, ITypeSymbol Value, bool Nullable)? ValueObject(ITypeSymbol? type)
    {
        var nullable = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
        if ((nullable ? ((INamedTypeSymbol)type!).TypeArguments[0] : type) is not INamedTypeSymbol { TypeKind: TypeKind.Struct } named)
        {
            return null;
        }

        var contract = named.AllInterfaces.FirstOrDefault(candidate =>
            SerializerContractAnalyzer.FullName(candidate.OriginalDefinition) == ContractName
            && SymbolEqualityComparer.Default.Equals(candidate.TypeArguments[0], named));

        return contract is null ? null : (named, contract.TypeArguments[1], nullable);
    }

    /// <summary>
    /// Determines whether Marten may take a value object for a document's identity, approximating
    /// <c>ValueTypeIdGeneration.tryMatch</c>: a struct declared public with exactly one public property, static ones
    /// counted, of <see langword="int"/>, <see langword="long"/>, <see cref="System.Guid"/> or <see langword="string"/>,
    /// which is not an identifier, whose <c>Prefix</c> and <c>Length</c> count beside its value, nor a value object with a
    /// public bound hook over an integer.
    /// </summary>
    /// <remarks>
    /// <c>tryMatch</c> also wants no public constructor of more than one parameter, and a public constructor or public
    /// static method returning the struct that takes that property's type alone, which every generated value object meets
    /// (<c>Create</c>). A value object written by hand that fails either gets the clause, though Marten never takes it for
    /// an identity; the collection form it names binds all the same.
    /// </remarks>
    private static bool MayBeIdentity(INamedTypeSymbol valueObject)
        => valueObject.DeclaredAccessibility == Accessibility.Public
           && valueObject.GetMembers().OfType<IPropertySymbol>().Count(static property =>
               property.DeclaredAccessibility == Accessibility.Public
               && IdentityTypes.Contains(property.Type.ToDisplayString(ValueObjectGenerator.QualifiedFormat))) == 1;

    /// <summary>What Marten does with the member.</summary>
    private static string Effect(bool identity, bool nullable)
        => nullable
            ? "Marten refuses as a parameter, as it refuses any nullable member"
            : identity
                ? "Marten refuses as a parameter, or, once it takes the type for a document's identity, accepts and "
                  + "answers on every call with the first call's value"
                : "Marten refuses as a parameter: it binds no value object";

    /// <summary>
    /// Gets the form to write instead, by what Marten 9.50 binds in a compiled query, adding the collection form for a
    /// value object Marten may take for a document's identity, nullable or not, where the remedy compares its
    /// <c>Value</c>.
    /// </summary>
    private static string Remedy(ISymbol member, ITypeSymbol value, bool identity)
    {
        if (member is IPropertySymbol { IsIndexer: true })
        {
            return "Marten reads a public indexer as a parameter too, and can bind none: make it non-public or remove it";
        }

        var shown = value.ToDisplayString(TypeFormat);
        var compared = $"compare the value object's Value with it, x.{member.Name}.Value == {member.Name}, which needs "
            + $"{Package}, without which Marten reads a Value no document holds";
        _ = UnderlyingType.TryResolve(value.ToDisplayString(ValueObjectGenerator.QualifiedFormat), out var underlying);

        var (remedy, comparesValue) = Form(underlying?.Kind, shown, compared);

        return identity && comparesValue
            ? $"{remedy}; or, for a document's identity, whose Value Marten does not read, {Collection}"
            : remedy;
    }

    /// <summary>
    /// Gets the form Marten binds for an underlying type, and whether it compares the value object's <c>Value</c>: the
    /// type itself, a <see cref="System.DateTimeOffset"/> set in UTC, an <see langword="int"/> or a <see langword="long"/>
    /// C# promotes it to, an array or a list of the value object over <see langword="bool"/>, <see langword="char"/>,
    /// <see langword="double"/>, <c>DateOnly</c>, <c>TimeOnly</c> and <see cref="System.TimeSpan"/>, or removal.
    /// </summary>
    private static (string Text, bool ComparesValue) Form(UnderlyingKind? kind, string shown, string compared)
    {
        // Only a value object written by hand can have an underlying type outside the table.
        if (kind is not { } underlying)
        {
            return ("Query it with LINQ", false);
        }

        if (BoundByMarten.Contains(underlying))
        {
            return ($"Declare it as {shown}{(underlying == UnderlyingKind.DateTimeOffset ? InUtc : string.Empty)} and {compared}", true);
        }

        if (PromotedToInt.Contains(underlying))
        {
            return ($"Declare it as int, to which C# promotes {shown}, and {compared}", true);
        }

        if (underlying == UnderlyingKind.UInt32)
        {
            return ($"Declare it as long, to which C# promotes {shown}, and {compared}", true);
        }

        return QueriedNowhere.Contains(underlying)
            ? ($"Marten queries no {shown}, in a compiled query or any other: remove it", false)
            : ($"Marten binds no {shown} member either: {Collection}, or query with LINQ", false);
    }
}
