using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using AdCodicem.ValueObjects.Generators.Analyzers;
using AdCodicem.ValueObjects.Generators.Model;
using Microsoft.CodeAnalysis;

namespace AdCodicem.ValueObjects.GeneratorTests;

/// <summary>
/// What <c>VO0048</c> reports: a value object held by a public instance field or property of a Marten compiled query,
/// which Marten refuses as a parameter, or answers with the first call's value once it takes the type for a document's
/// identity.
/// </summary>
/// <remarks>
/// <para>
/// Marten's interfaces and its <c>[MartenIgnore]</c> are matched by full metadata name, so the snippets compile against
/// stand-ins declared under their real names in an assembly of their own (<see cref="MartenReferences"/>), as
/// <see cref="SerializerContractAnalyzerTests"/> declares the serializers' attributes: the real Marten brings its own source
/// generators and a dependency graph into a project held to the generator's Roslyn. The unit suite's Data project runs
/// the analyzer over the real Marten (<c>Persistence/MartenCompiledQueryDiagnosticTests</c> of
/// <c>tests/AdCodicem.ValueObjects.UnitTests.Data</c>), which checks the names.
/// </para>
/// <para>
/// The analyzer runs over the compilation with the generator's output, as the compiler does, so that a value object of
/// the compilation itself, whose contract and <c>Value</c> the generated partial declares, is recognized.
/// </para>
/// </remarks>
public sealed class MartenCompiledQueryAnalyzerTests
{
    /// <summary>The usings every snippet starts with.</summary>
    private const string Usings = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Linq.Expressions;
        using AdCodicem.ValueObjects;
        using AdCodicem.ValueObjects.Annotations;
        using AdCodicem.ValueObjects.Identifiers;
        using Marten.Events.CodeGeneration;
        using Marten.Linq;

        """;

    /// <summary>The documents and value objects every snippet may use, after its namespace.</summary>
    private const string Shop = """
        public sealed class Order
        {
            public Guid Id { get; set; }

            public int Index { get; set; }
        }

        [ValueObject<string>]
        public readonly partial struct Sku;

        [ValueObject<short>]
        public readonly partial struct Quantity;

        [ValueObject<Guid>]
        public readonly partial struct CustomerId;

        [ValueObject<string>]
        public readonly partial struct Reference<TOwner>;

        """;

    /// <summary>The body of a list query over <c>Order</c>.</summary>
    private const string QueryIs = "    public Expression<Func<IMartenQueryable<Order>, IEnumerable<Order>>> QueryIs() => orders => orders;";

    private const string Refused = "Marten refuses as a parameter: it binds no value object";

    private const string Replayed = "Marten refuses as a parameter, or, once it takes the type for a document's identity, "
        + "accepts and answers on every call with the first call's value";

    private const string RefusedNullable = "Marten refuses as a parameter, as it refuses any nullable member";

    private const string Package = "AdCodicem.ValueObjects.Marten's UseValueObjects()";

    private const string Collection = "hold the values in an array or a list of the value object (a type implementing IList<T>), "
        + "read with Contains or IsOneOf, which " + Package + " binds on every call";

    private const string Indexer = "Marten reads a public indexer as a parameter too, and can bind none: make it non-public or "
        + "remove it";

    /// <summary>Every supported underlying type, by the name the diagnostics give them.</summary>
    public static TheoryData<string> SupportedUnderlyingTypes => [.. UnderlyingType.SupportedNames];

    /// <summary>
    /// Each member Marten reads as a parameter is reported on its declaration: a field, a property of every accessor
    /// shape, a positional record's parameter, a member of a struct query, and an indexer, which Marten reads too and can
    /// bind none of.
    /// </summary>
    [Fact]
    public async Task Each_member_Marten_reads_is_reported_on_its_declaration()
    {
        var diagnostics = await RunAsync($$"""
            public sealed class Accessors : ICompiledListQuery<Order>
            {
                private Quantity _written;

                public Quantity Field;

                public Quantity Property { get; set; }

                public Quantity Computed => _written;

                public Quantity PrivateGetter { private get; set; }

                public Quantity PrivateSetter { get; private set; }

                public Quantity InitOnly { get; init; }

                public required Quantity Required { get; set; }

                public Quantity WriteOnly { set => _written = value; }

                public Quantity this[int index] => _written;

            {{QueryIs}}
            }

            public sealed record Positional(Quantity Minimum) : ICompiledListQuery<Order>
            {
            {{QueryIs}}
            }

            public struct Valued : ICompiledListQuery<Order>
            {
                public Quantity Minimum { get; set; }

            {{QueryIs}}
            }
            """);

        diagnostics.Select(Reported).Should().Equal(
            ("Field", Message("Field", "Accessors", "Quantity", Refused, Promoted("short", "Field"))),
            ("Property", Message("Property", "Accessors", "Quantity", Refused, Promoted("short", "Property"))),
            ("Computed", Message("Computed", "Accessors", "Quantity", Refused, Promoted("short", "Computed"))),
            ("PrivateGetter", Message("PrivateGetter", "Accessors", "Quantity", Refused, Promoted("short", "PrivateGetter"))),
            ("PrivateSetter", Message("PrivateSetter", "Accessors", "Quantity", Refused, Promoted("short", "PrivateSetter"))),
            ("InitOnly", Message("InitOnly", "Accessors", "Quantity", Refused, Promoted("short", "InitOnly"))),
            ("Required", Message("Required", "Accessors", "Quantity", Refused, Promoted("short", "Required"))),
            ("WriteOnly", Message("WriteOnly", "Accessors", "Quantity", Refused, Promoted("short", "WriteOnly"))),
            ("this", Message("this[int]", "Accessors", "Quantity", Refused, Indexer)),
            ("Minimum", Message("Minimum", "Positional", "Quantity", Refused, Promoted("short", "Minimum"))),
            ("Minimum", Message("Minimum", "Valued", "Quantity", Refused, Promoted("short", "Minimum"))));
        diagnostics.Should().AllSatisfy(static diagnostic =>
        {
            diagnostic.Id.Should().Be("VO0048");
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        });
    }

    /// <summary>
    /// Every kind of value object is reported: generated in the compilation or in a referenced assembly, written by hand, a
    /// generic construction, one over the query's own type parameter, an identifier, and the nullable type of one, which
    /// Marten refuses as it refuses any nullable member, and which keeps the identity clause: what a document's identity
    /// accepts does not depend on the member. An identifier gets no identity clause: Marten counts its <c>Prefix</c> and
    /// <c>Length</c> beside its value, and takes no struct with three such properties for an identity.
    /// </summary>
    [Fact]
    public async Task Every_kind_of_value_object_is_reported()
    {
        var catalog = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
            """
            using AdCodicem.ValueObjects.Annotations;

            namespace Catalog;

            [ValueObject<string>]
            public readonly partial struct ExternalSku;
            """,
            "MartenCatalog"));

        var diagnostics = await RunAsync(ValidationAttributeAnalyzerTests.HandWritten + $$"""
            [EntityId("ord")]
            public readonly partial struct OrderRef;

            public sealed class Kinds : ICompiledListQuery<Order>
            {
                public Sku Generated { get; set; }

                public Catalog.ExternalSku Referenced { get; set; }

                public Code HandWritten { get; set; }

                public Reference<Order> Construction { get; set; }

                public OrderRef Identifier { get; set; }

                public Sku? Nullable { get; set; }

            {{QueryIs}}
            }

            public sealed class Owned<T> : ICompiledListQuery<Order>
            {
                public Reference<T> Owner { get; set; }

            {{QueryIs}}
            }
            """,
            MartenReferences.WithMarten.Add(catalog));

        diagnostics.Select(Reported).Should().Equal(
            ("Generated", Message("Generated", "Kinds", "Sku", Replayed, Identity(Compared("string", "Generated")))),
            ("Referenced", Message("Referenced", "Kinds", "ExternalSku", Replayed, Identity(Compared("string", "Referenced")))),
            ("HandWritten", Message("HandWritten", "Kinds", "Code", Replayed, Identity(Compared("string", "HandWritten")))),
            ("Construction", Message("Construction", "Kinds", "Reference<Order>", Replayed, Identity(Compared("string", "Construction")))),
            ("Identifier", Message("Identifier", "Kinds", "OrderRef", Refused, Compared("string", "Identifier"))),
            ("Nullable", Message("Nullable", "Kinds", "Sku?", RefusedNullable, Identity(Compared("string", "Nullable")))),
            ("Owner", Message("Owner", "Owned<T>", "Reference<T>", Replayed, Identity(Compared("string", "Owner")))));
    }

    /// <summary>
    /// Every one of Marten's compiled-query interfaces is a compiled query, since each extends
    /// <c>ICompiledQuery&lt;TDoc, TOut&gt;</c>, and so is a type implementing one through its base type.
    /// </summary>
    [Theory]
    [InlineData("ICompiledQuery<Order>", "Order", "orders.First()")]
    [InlineData("ICompiledQuery<Order, int>", "int", "orders.Count()")]
    [InlineData("ICompiledListQuery<Order>", "IEnumerable<Order>", "orders")]
    [InlineData("ICompiledListQuery<Order, Guid>", "IEnumerable<Guid>", "orders.Select(x => x.Id)")]
    public async Task Every_compiled_query_interface_is_read(string contract, string result, string body)
    {
        var diagnostics = await RunAsync($$"""
            public sealed class Direct : {{contract}}
            {
                public Quantity Minimum { get; set; }

                public Expression<Func<IMartenQueryable<Order>, {{result}}>> QueryIs() => orders => {{body}};
            }

            public abstract class Base : {{contract}}
            {
                public abstract Expression<Func<IMartenQueryable<Order>, {{result}}>> QueryIs();
            }

            public sealed class Derived : Base
            {
                public Quantity Minimum { get; set; }

                public override Expression<Func<IMartenQueryable<Order>, {{result}}>> QueryIs() => orders => {{body}};
            }
            """);

        diagnostics.Select(Reported).Should().Equal(
            ("Minimum", Message("Minimum", "Direct", "Quantity", Refused, Promoted("short", "Minimum"))),
            ("Minimum", Message("Minimum", "Derived", "Quantity", Refused, Promoted("short", "Minimum"))));
    }

    /// <summary>
    /// The remedy names the form Marten binds on every call for the value object's underlying type, by Marten 9.50's
    /// parameter finders, which the unit suite pins: the underlying type itself, a <see cref="DateTimeOffset"/> set in UTC,
    /// which Marten writes as it stands and Npgsql refuses at any other offset, an <see langword="int"/> or a
    /// <see langword="long"/> C# promotes it to, an array or a list of the value object, or removal. A value object over one
    /// of Marten's identity types, whose <c>Value</c> is its one property of such a type, may be taken for a document's
    /// identity, and gets the collection form too.
    /// </summary>
    [Theory]
    [MemberData(nameof(SupportedUnderlyingTypes))]
    public async Task Every_underlying_type_gets_the_form_Marten_binds_for_it(string underlying)
    {
        var diagnostics = await RunAsync($$"""
            [ValueObject<{{underlying}}>]
            public readonly partial struct Held;

            public sealed class Holding : ICompiledListQuery<Order>
            {
                public Held Minimum { get; set; }

            {{QueryIs}}
            }
            """);

        var shown = underlying.StartsWith("System.", StringComparison.Ordinal) ? underlying["System.".Length..] : underlying;
        var identity = underlying is "string" or "int" or "long" or "System.Guid";
        var remedy = underlying switch
        {
            "string" or "System.Guid" or "int" or "long" or "float" or "decimal" or "System.DateTime" => Compared(shown, "Minimum"),
            "System.DateTimeOffset" => "Declare it as DateTimeOffset, set in UTC with ToUniversalTime() (Marten writes the member as "
                + $"it stands, and Npgsql refuses any other offset), and {Comparison("Minimum")}",
            "sbyte" or "byte" or "short" => $"Declare it as int, to which C# promotes {shown}, and {Comparison("Minimum")}",
            "uint" => $"Declare it as long, to which C# promotes uint, and {Comparison("Minimum")}",
            "bool" or "char" or "double" or "System.DateOnly" or "System.TimeOnly" or "System.TimeSpan"
                => $"Marten binds no {shown} member either: {Collection}, or query with LINQ",
            "ushort" or "ulong" or "System.Int128" or "System.UInt128"
                => $"Marten queries no {shown}, in a compiled query or any other: remove it",
            _ => throw new InvalidOperationException($"No form is expected for {underlying}: say which Marten binds."),
        };

        diagnostics.Select(Reported).Should().Equal(
            ("Minimum", Message("Minimum", "Holding", "Held", identity ? Replayed : Refused, identity ? Identity(remedy) : remedy)));
    }

    /// <summary>
    /// Marten takes a public struct for a document's identity when exactly one of its public properties, static ones
    /// counted, is of <see langword="int"/>, <see langword="long"/>, <see cref="Guid"/> or <see langword="string"/>: a
    /// value object with a public bound hook, or a property of the author's, of such a type gets no identity clause,
    /// nor does one that is not public itself, while a public one nested in a type that is not counts, as Marten reads
    /// the type's own accessibility. A value object over another type whose author adds one such property may be taken
    /// for one too, Marten building it through the generated <c>Parse(string)</c> for a <see langword="string"/>
    /// property: one over a type Marten binds no member of keeps its collection remedy, and one compared through an
    /// <see langword="int"/> or a <see langword="long"/> gets the identity clause. Only public properties count: a
    /// constant of such a type, or a property that is not public, leaves the clause. A value object written by hand
    /// over a type outside the table gets LINQ.
    /// </summary>
    [Fact]
    public async Task The_identity_clause_follows_what_Marten_takes_for_a_document_s_identity()
    {
        var diagnostics = await RunAsync(Weekday + $$"""
            [ValueObject<int>]
            public readonly partial struct Stock : IValueObjectMinimum<int>
            {
                public static int Minimum => 0;
            }

            [ValueObject<Guid>]
            public readonly partial struct Ticket
            {
                public static string Label => "ticket";
            }

            [ValueObject<long>]
            internal readonly partial struct Secret;

            internal static partial class Vault
            {
                [ValueObject<long>]
                public readonly partial struct Key;
            }

            [ValueObject<DateOnly>]
            public readonly partial struct Day
            {
                public static string Label => "day";
            }

            [ValueObject<Guid>]
            public readonly partial struct Extras
            {
                public const int Version = 1;

                private string Label => "extras";
            }

            [ValueObject<short>]
            public readonly partial struct Shelf
            {
                public static string Label => "shelf";
            }

            [ValueObject<uint>]
            public readonly partial struct Batch
            {
                public static string Label => "batch";
            }

            internal sealed class Identities : ICompiledListQuery<Order>
            {
                public Stock Stock { get; set; }

                public Ticket Ticket { get; set; }

                public Secret Secret { get; set; }

                public Vault.Key Key { get; set; }

                public Day Day { get; set; }

                public Extras Extras { get; set; }

                public Shelf Shelf { get; set; }

                public Batch Batch { get; set; }

                public Weekday Weekday { get; set; }

            {{QueryIs}}
            }
            """);

        diagnostics.Select(Reported).Should().Equal(
            ("Stock", Message("Stock", "Identities", "Stock", Refused, Compared("int", "Stock"))),
            ("Ticket", Message("Ticket", "Identities", "Ticket", Refused, Compared("Guid", "Ticket"))),
            ("Secret", Message("Secret", "Identities", "Secret", Refused, Compared("long", "Secret"))),
            ("Key", Message("Key", "Identities", "Vault.Key", Replayed, Identity(Compared("long", "Key")))),
            ("Day", Message("Day", "Identities", "Day", Replayed, $"Marten binds no DateOnly member either: {Collection}, or query with LINQ")),
            ("Extras", Message("Extras", "Identities", "Extras", Replayed, Identity(Compared("Guid", "Extras")))),
            ("Shelf", Message("Shelf", "Identities", "Shelf", Replayed, Identity(Promoted("short", "Shelf")))),
            ("Batch", Message("Batch", "Identities", "Batch", Replayed, Identity($"Declare it as long, to which C# promotes uint, and {Comparison("Batch")}"))),
            ("Weekday", Message("Weekday", "Identities", "Weekday", Refused, "Query it with LINQ")));
    }

    /// <summary>
    /// A member a compiled query inherits is reported once: on its declaration where a type of the compilation implementing
    /// the interface declares it with a value object for its type, on the compiled query otherwise, naming the type
    /// declaring it: a plain base, a base of a referenced assembly, a generic base whose type parameter the compiled query
    /// closes over a value object. Marten reads the most derived declaration of an overridden property, with its own
    /// attributes alone, however long the chain of overrides.
    /// </summary>
    [Fact]
    public async Task An_inherited_member_is_reported_once_where_its_fix_goes()
    {
        var external = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
            """
            using System;
            using System.Collections.Generic;
            using System.Linq.Expressions;
            using AdCodicem.ValueObjects.Annotations;
            using Marten.Linq;

            namespace Catalog;

            [ValueObject<short>]
            public readonly partial struct Stock;

            public abstract class ExternalQuery<TDoc> : ICompiledListQuery<TDoc>
                where TDoc : notnull
            {
                public Stock Minimum { get; set; }

                public abstract Expression<Func<IMartenQueryable<TDoc>, IEnumerable<TDoc>>> QueryIs();
            }
            """,
            "MartenCatalogQueries",
            MartenReferences.WithMarten));

        var diagnostics = await RunAsync($$"""
            public class PlainBase
            {
                public Quantity Inherited { get; set; }

                public virtual Quantity Virtual { get; set; }

                public virtual Quantity Silenced { get; set; }

                [MartenIgnore]
                public virtual Quantity Ignored { get; set; }
            }

            public sealed class FromPlain : PlainBase, ICompiledListQuery<Order>
            {
                public override Quantity Virtual { get; set; }

                [MartenIgnore]
                public override Quantity Silenced { get; set; }

                public override Quantity Ignored { get; set; }

            {{QueryIs}}
            }

            public class Grandparent
            {
                public virtual Quantity Chained { get; set; }
            }

            public class Parent : Grandparent
            {
                public override Quantity Chained { get; set; }
            }

            public sealed class FromChain : Parent, ICompiledListQuery<Order>
            {
                [MartenIgnore]
                public override Quantity Chained { get; set; }

            {{QueryIs}}
            }

            public abstract class QueryBase : ICompiledListQuery<Order>
            {
                public Quantity OnBase { get; set; }

                public virtual Quantity Virtual { get; set; }

                public abstract Expression<Func<IMartenQueryable<Order>, IEnumerable<Order>>> QueryIs();
            }

            public sealed class FromQueryBase : QueryBase
            {
                public override Quantity Virtual { get; set; }

                public override Expression<Func<IMartenQueryable<Order>, IEnumerable<Order>>> QueryIs() => orders => orders;
            }

            public abstract class GenericBase<T> : ICompiledListQuery<Order>
            {
                public T Generic { get; set; } = default!;

                public Reference<T> Owner { get; set; }

                public abstract Expression<Func<IMartenQueryable<Order>, IEnumerable<Order>>> QueryIs();
            }

            public sealed class Closed : GenericBase<Quantity>
            {
                public override Expression<Func<IMartenQueryable<Order>, IEnumerable<Order>>> QueryIs() => orders => orders;
            }

            public sealed class FromExternal : Catalog.ExternalQuery<Order>
            {
                public override Expression<Func<IMartenQueryable<Order>, IEnumerable<Order>>> QueryIs() => orders => orders;
            }
            """,
            MartenReferences.WithMarten.Add(external));

        diagnostics.Select(Reported).Should().Equal(
            ("FromPlain", Message("PlainBase.Inherited", "FromPlain", "Quantity", Refused, Promoted("short", "Inherited"))),
            ("Virtual", Message("Virtual", "FromPlain", "Quantity", Refused, Promoted("short", "Virtual"))),
            ("Ignored", Message("Ignored", "FromPlain", "Quantity", Refused, Promoted("short", "Ignored"))),
            ("OnBase", Message("OnBase", "QueryBase", "Quantity", Refused, Promoted("short", "OnBase"))),
            ("Virtual", Message("Virtual", "QueryBase", "Quantity", Refused, Promoted("short", "Virtual"))),
            ("Virtual", Message("Virtual", "FromQueryBase", "Quantity", Refused, Promoted("short", "Virtual"))),
            ("Owner", Message("Owner", "GenericBase<T>", "Reference<T>", Replayed, Identity(Compared("string", "Owner")))),
            ("Closed", Message("GenericBase<Quantity>.Generic", "Closed", "Quantity", Refused, Promoted("short", "Generic"))),
            ("FromExternal", Message("ExternalQuery<Order>.Minimum", "FromExternal", "Stock", Refused, Promoted("short", "Minimum"))));
    }

    /// <summary>
    /// The shape the Marten suite pins (deviation D16 of the Marten package): a compiled query comparing a document's
    /// value-object identity with a member, which Marten refuses until it has built that document's mapping and answers
    /// with the first call's identity after, out of any package's reach. The build now reports it, with the form Marten
    /// binds on every call.
    /// </summary>
    [Fact]
    public async Task The_identity_query_Marten_answers_with_its_first_identity_is_reported_with_the_form_it_binds()
    {
        var diagnostics = await RunAsync("""
            public sealed class Account
            {
                public CustomerId Id { get; set; }
            }

            public sealed class AccountsById : ICompiledListQuery<Account>
            {
                public CustomerId Id { get; set; }

                public Expression<Func<IMartenQueryable<Account>, IEnumerable<Account>>> QueryIs()
                    => accounts => accounts.Where(x => x.Id == Id);
            }
            """);

        diagnostics.Select(Reported).Should().Equal(("Id",
            "'Id' of the compiled query 'AccountsById' holds the value object 'CustomerId', which Marten refuses as a "
            + "parameter, or, once it takes the type for a document's identity, accepts and answers on every call with the "
            + "first call's value. Declare it as Guid and compare the value object's Value with it, x.Id.Value == Id, which "
            + "needs AdCodicem.ValueObjects.Marten's UseValueObjects(), without which Marten reads a Value no document holds; "
            + "or, for a document's identity, whose Value Marten does not read, hold the values in an array or a list of the "
            + "value object (a type implementing IList<T>), read with Contains or IsOneOf, which "
            + "AdCodicem.ValueObjects.Marten's UseValueObjects() binds on every call."));
    }

    /// <summary>
    /// The <c>.Value</c> form reads right only under <c>AdCodicem.ValueObjects.Marten</c>'s <c>UseValueObjects()</c>:
    /// without it, Marten reads a <c>Value</c> no document holds and answers no rows, silently. The message names it
    /// whether or not the compilation references the package, since a query type often lives in a library that does not
    /// reference the package its host uses: the same in a library referencing Marten alone as beside the package.
    /// </summary>
    [Fact]
    public async Task The_Value_form_names_the_package_it_needs_whether_or_not_the_compilation_references_it()
    {
        var package = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
            """
            namespace AdCodicem.ValueObjects.Marten;

            public static class ValueObjectStoreOptionsExtensions
            {
                public static T UseValueObjects<T>(this T options) => options;
            }
            """,
            "AdCodicem.ValueObjects.Marten"));
        var query = $$"""
            public sealed class OrdersOfStock : ICompiledListQuery<Order>
            {
                public Quantity Minimum { get; set; }

            {{QueryIs}}
            }
            """;

        var library = (await RunAsync(query)).Select(Reported).ToList();
        var host = (await RunAsync(query, MartenReferences.WithMarten.Add(package))).Select(Reported).ToList();

        library.Should().Equal(("Minimum",
            "'Minimum' of the compiled query 'OrdersOfStock' holds the value object 'Quantity', which Marten refuses as a "
            + "parameter: it binds no value object. Declare it as int, to which C# promotes short, and compare the value "
            + "object's Value with it, x.Minimum.Value == Minimum, which needs AdCodicem.ValueObjects.Marten's "
            + "UseValueObjects(), without which Marten reads a Value no document holds."));
        host.Should().Equal(library);
    }

    /// <summary>
    /// Marten's source generator discovers the same query types, non-generic top-level classes of an assembly marked
    /// <c>[assembly: JasperFxAssembly]</c>, and Marten plans them through the same check before any descriptor it wrote:
    /// they are reported the same way, the analyzer reading no such attribute.
    /// </summary>
    [Fact]
    public async Task A_query_type_Marten_s_source_generator_handles_is_reported_the_same_way()
    {
        var diagnostics = await RunAsync(
            $$"""
            public sealed class OrdersOfSku : ICompiledListQuery<Order>
            {
                public Sku Sku { get; set; }

            {{QueryIs}}
            }
            """,
            assemblyAttributes: "[assembly: JasperFx.JasperFxAssembly]\n");

        diagnostics.Select(Reported).Should().Equal(
            ("Sku", Message("Sku", "OrdersOfSku", "Sku", Replayed, Identity(Compared("string", "Sku")))));
    }

    /// <summary>
    /// A collection of value objects is left alone, whatever its type: the package binds the ones Marten plans, and Marten
    /// refuses the others itself.
    /// </summary>
    [Fact]
    public async Task Collections_of_value_objects_are_left_alone()
    {
        var diagnostics = await RunAsync($$"""
            public sealed class Collections : ICompiledListQuery<Order>
            {
                public Sku[] Array { get; set; } = [];

                public List<Sku> List { get; set; } = [];

                public IList<Sku> Interface { get; set; } = [];

                public IReadOnlyList<Sku> ReadOnly { get; set; } = [];

                public HashSet<Sku> Set { get; set; } = [];

                public IEnumerable<Sku> Sequence { get; set; } = [];

                public List<Sku?> Nullables { get; set; } = [];

                public Dictionary<Sku, int> Counts { get; set; } = new();

            {{QueryIs}}
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// What is no value object is left alone: the underlying type, another struct, a class, a struct implementing the
    /// contract over another type, a type parameter, the nullable type of a primitive.
    /// </summary>
    [Fact]
    public async Task Members_holding_no_value_object_are_left_alone()
    {
        var diagnostics = await RunAsync(ValidationAttributeAnalyzerTests.HandWritten + ValidationAttributeAnalyzerTests.Imposter + $$"""
            public sealed class Plain<T> : ICompiledListQuery<Order>
            {
                public string Text { get; set; } = string.Empty;

                public TimeSpan Duration { get; set; }

                public Order Holder { get; set; } = new();

                public Imposter Pretender { get; set; }

                public T Open { get; set; } = default!;

                public int? Maybe { get; set; }

            {{QueryIs}}
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// What Marten does not read is left alone: a member that is not public, a static one, a constant, an explicit
    /// implementation, and a property or a field marked <c>[MartenIgnore]</c> or an attribute deriving from it. Another attribute keeps a
    /// member read, JasperFx's <c>[JasperFxIgnore]</c>, the base of <c>[MartenIgnore]</c>, among them: only Marten's source
    /// generator honours it, not the planning every compiled query goes through. An attribute named <c>MartenIgnore</c> in
    /// another namespace, or nested in a type of Marten's namespace, is another attribute.
    /// </summary>
    [Fact]
    public async Task Members_Marten_does_not_read_are_left_alone()
    {
        var diagnostics = await RunAsync($$"""
            public interface INamed
            {
                Sku Named { get; }
            }

            public sealed class SkipAttribute : MartenIgnoreAttribute;

            public class Hidden : ICompiledListQuery<Order>, INamed
            {
                internal Sku Internal { get; set; }

                protected Sku Protected { get; set; }

                protected internal Sku ProtectedInternal { get; set; }

                private protected Sku PrivateProtected { get; set; }

                private Sku Private { get; set; }

                public static Sku Static { get; set; }

                public const int Constant = 1;

                Sku INamed.Named => Private;

                [MartenIgnore]
                public Sku Ignored { get; set; }

                [MartenIgnore]
                public Sku IgnoredField;

                [Skip]
                public Sku Skipped { get; set; }

                [Obsolete("Unrelated.")]
                public Sku Unrelated { get; set; }

                [JasperFx.Core.JasperFxIgnore]
                public Sku JasperFxIgnored { get; set; }

                [Elsewhere.MartenIgnore]
                public Sku ElsewhereIgnored { get; set; }

                [Marten.Events.CodeGeneration.Holder.MartenIgnore]
                public Sku NestedIgnored { get; set; }

            {{QueryIs}}
            }
            """);

        diagnostics.Select(static diagnostic => Reported(diagnostic).Text)
            .Should().Equal("Unrelated", "JasperFxIgnored", "ElsewhereIgnored", "NestedIgnored");
    }

    /// <summary>
    /// A type that is no compiled query is left alone: one implementing Marten's marker alone, which Marten refuses to
    /// register, an interface extending a compiled-query interface, which Marten never plans, a class implementing none,
    /// and a class implementing an interface of Marten's name declared in another namespace or nested in a type.
    /// </summary>
    [Fact]
    public async Task Types_that_are_no_compiled_query_are_left_alone()
    {
        var diagnostics = await RunAsync("""
            public sealed class MarkerOnly : ICompiledQueryMarker
            {
                public Sku Sku { get; set; }
            }

            public interface IOrdersOfSku : ICompiledListQuery<Order>
            {
                Sku Sku { get; }
            }

            public sealed class NoQuery
            {
                public Sku Sku { get; set; }
            }

            public sealed class OtherQuery : Elsewhere.ICompiledQuery<Order, int>
            {
                public Sku Sku { get; set; }
            }

            public sealed class Nested : Marten.Linq.Holder.ICompiledQuery<Order, int>
            {
                public Sku Sku { get; set; }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    /// <summary>Nothing changes for a project without Marten, an interface of Marten's name in another namespace aside.</summary>
    [Fact]
    public async Task Nothing_is_reported_without_Marten()
    {
        var diagnostics = await RunAsync(
            $$"""
            public sealed class OrdersOfSku : ICompiledQuery<Order, int>
            {
                public Sku Sku { get; set; }
            }
            """,
            MartenReferences.WithoutMarten,
            usings: Usings.Replace("using Marten.Events.CodeGeneration;\nusing Marten.Linq;\n", "using Elsewhere;\n", StringComparison.Ordinal));

        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// The diagnostic is an analyzer's, which <c>#pragma</c> silences, as a test pinning Marten's refusal of such a query
    /// type does.
    /// </summary>
    [Fact]
    public async Task A_pragma_silences_it()
    {
        var diagnostics = await RunAsync($$"""
            public sealed class Pinned : ICompiledListQuery<Order>
            {
            #pragma warning disable VO0048 // The shape a test pins: Marten refuses a single value object as a parameter.
                public Sku Silenced { get; set; }
            #pragma warning restore VO0048

                public Sku Reported { get; set; }

            {{QueryIs}}
            }
            """);

        diagnostics.Select(static diagnostic => Reported(diagnostic).Text).Should().Equal("Reported");
    }

    /// <summary>The help link lands on the section of the Marten guide that gives each form.</summary>
    [Fact]
    [Trait("Reads", "Documentation")]
    public void The_help_link_lands_on_the_compiled_queries_section_of_the_Marten_guide()
    {
        var link = new Uri(MartenCompiledQueryAnalyzer.ValueObjectInCompiledQuery.HelpLinkUri);

        link.GetLeftPart(UriPartial.Path).Should().Be("https://adcodicem.github.io/AdCodicem.ValueObjects/docs/how-to/marten");
        link.Fragment.Should().Be("#compiled-queries");

        var page = File.ReadAllLines(Path.Combine(RepositoryRoot(), "website", "docs", "how-to", "marten.md"));
        page.Should().Contain("slug: /how-to/marten");
        page.Select(Anchor).Should().Contain(link.Fragment.TrimStart('#'));
    }

    /// <summary>
    /// The compiler never hands an analyzer a null context, so the guard is only reached by a direct call, which it
    /// tolerates. The one diagnostic is an error, which a severity setting may lower.
    /// </summary>
    [Fact]
    public void The_analyzer_initialized_without_a_context_does_nothing()
    {
        var initialize = () => new MartenCompiledQueryAnalyzer().Initialize(null!);

        initialize.Should().NotThrow();
        new MartenCompiledQueryAnalyzer().SupportedDiagnostics.Should().ContainSingle()
            .Which.Should().BeSameAs(MartenCompiledQueryAnalyzer.ValueObjectInCompiledQuery);
        MartenCompiledQueryAnalyzer.ValueObjectInCompiledQuery.Id.Should().Be("VO0048");
        MartenCompiledQueryAnalyzer.ValueObjectInCompiledQuery.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
    }

    /// <summary>A value object written by hand over a type outside the table, which only one written by hand can have.</summary>
    private const string Weekday = """
        public readonly struct Weekday : IValueObject<Weekday, DayOfWeek>
        {
            private Weekday(DayOfWeek value) => Value = value;
            public static AdCodicem.ValueObjects.Metadata.ValueObjectSchema Schema => AdCodicem.ValueObjects.Metadata.ValueObjectSchema.Unconstrained;
            public DayOfWeek Value { get; }
            public bool IsDefault => false;
            public static DayOfWeek Normalize(DayOfWeek value) => value;
            public static ValidationResult Validate(in DayOfWeek value) => ValidationResult.Success;
            public static Weekday Create(DayOfWeek value) => new(value);
            public static bool TryCreate(DayOfWeek value, out Weekday result) => TryCreate(value, out result, out _);
            public static bool TryCreate(DayOfWeek value, out Weekday result, out ValidationResult validation) { validation = default; result = new(value); return true; }
            public static Weekday CreateUnchecked(DayOfWeek value) => new(value);
            public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out Weekday result, out ValidationResult validation) { validation = default; var parsed = Enum.TryParse(text, out DayOfWeek value); result = new(value); return parsed; }
            public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Weekday result) => TryParse(s, provider, out result, out _);
            public static bool TryParse(string? s, IFormatProvider? provider, out Weekday result) => TryParse(s.AsSpan(), provider, out result, out _);
            public static Weekday Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Create(Enum.Parse<DayOfWeek>(s));
            public static Weekday Parse(string s, IFormatProvider? provider) => Create(Enum.Parse<DayOfWeek>(s));
            public bool Equals(Weekday other) => Value == other.Value;
            public override bool Equals(object? obj) => obj is Weekday other && Equals(other);
            public override int GetHashCode() => Value.GetHashCode();
            public int CompareTo(Weekday other) => Value.CompareTo(other.Value);
            public override string ToString() => Value.ToString();
            public string ToString(string? format, IFormatProvider? formatProvider) => Value.ToString();
            public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) { var text = Value.ToString(); charsWritten = text.Length; return text.TryCopyTo(destination); }
            public static bool operator ==(Weekday left, Weekday right) => left.Equals(right);
            public static bool operator !=(Weekday left, Weekday right) => !left.Equals(right);
            public static bool operator <(Weekday left, Weekday right) => left.CompareTo(right) < 0;
            public static bool operator >(Weekday left, Weekday right) => left.CompareTo(right) > 0;
            public static bool operator <=(Weekday left, Weekday right) => left.CompareTo(right) <= 0;
            public static bool operator >=(Weekday left, Weekday right) => left.CompareTo(right) >= 0;
        }

        """;

    /// <summary>
    /// Runs the analyzer over a snippet that is otherwise valid C#, the usings, the namespace and <see cref="Shop"/> added,
    /// so that what it reports is never a side effect of code the compiler would refuse anyway, in the order of the source.
    /// </summary>
    private static async Task<IReadOnlyList<Diagnostic>> RunAsync(
        string source,
        ImmutableArray<MetadataReference>? references = null,
        string usings = Usings,
        string assemblyAttributes = "")
    {
        var text = usings + assemblyAttributes + "\nnamespace Shop;\n\n" + Shop + source;
        var compiled = references ?? MartenReferences.WithMarten;

        GeneratorHarness.Run(text, references: compiled).CompilationDiagnostics.Should().BeEmpty();

        var diagnostics = await GeneratorHarness.RunAnalyzerAsync<MartenCompiledQueryAnalyzer>(text, compiled);

        return [.. diagnostics.OrderBy(static diagnostic => diagnostic.Location.SourceSpan.Start)];
    }

    /// <summary>Gets the text a diagnostic is located on, and its message.</summary>
    private static (string Text, string Message) Reported(Diagnostic diagnostic)
        => (diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan),
            diagnostic.GetMessage(CultureInfo.InvariantCulture));

    private static string Message(string member, string query, string valueObject, string effect, string remedy)
        => $"'{member}' of the compiled query '{query}' holds the value object '{valueObject}', which {effect}. {remedy}.";

    private static string Comparison(string member)
        => $"compare the value object's Value with it, x.{member}.Value == {member}, which needs {Package}, without which Marten "
           + "reads a Value no document holds";

    private static string Compared(string type, string member) => $"Declare it as {type} and {Comparison(member)}";

    private static string Promoted(string type, string member) => $"Declare it as int, to which C# promotes {type}, and {Comparison(member)}";

    private static string Identity(string remedy) => $"{remedy}; or, for a document's identity, whose Value Marten does not read, {Collection}";

    /// <summary>
    /// Gets the anchor Docusaurus gives a heading of any level: lower case, punctuation dropped, spaces as hyphens. Any
    /// other line gives none.
    /// </summary>
    private static string Anchor(string line)
    {
        if (!line.StartsWith('#'))
        {
            return string.Empty;
        }

        var heading = line.TrimStart('#').Trim().ToLowerInvariant();

        return string.Concat(heading.Where(character => char.IsLetterOrDigit(character) || character is ' ' or '-'))
            .Replace(' ', '-');
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AdCodicem.ValueObjects.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}

/// <summary>What the snippets of <see cref="MartenCompiledQueryAnalyzerTests"/> compile against.</summary>
internal static class MartenReferences
{
    /// <summary>
    /// Stand-ins for Marten 9.50's compiled-query interfaces and <c>[MartenIgnore]</c>, under their real full names, with
    /// the attribute it derives from and the assembly attribute Marten's source generator reads.
    /// </summary>
    private static readonly MetadataReference StandIns = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
        """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Linq.Expressions;

        namespace Marten.Linq
        {
            public interface ICompiledQueryMarker;

            public interface IMartenQueryable<T> : IQueryable<T>;

            public interface ICompiledQuery<TDoc, TOut> : ICompiledQueryMarker
                where TDoc : notnull
            {
                Expression<Func<IMartenQueryable<TDoc>, TOut>> QueryIs();
            }

            public interface ICompiledQuery<TDoc> : ICompiledQuery<TDoc, TDoc>, ICompiledQueryMarker
                where TDoc : notnull;

            public interface ICompiledListQuery<TDoc, TOut> : ICompiledQuery<TDoc, IEnumerable<TOut>>, ICompiledQueryMarker
                where TDoc : notnull;

            public interface ICompiledListQuery<TDoc> : ICompiledListQuery<TDoc, TDoc>, ICompiledQuery<TDoc, IEnumerable<TDoc>>, ICompiledQueryMarker
                where TDoc : notnull;
        }

        namespace JasperFx.Core
        {
            public class JasperFxIgnoreAttribute : Attribute;
        }

        namespace JasperFx
        {
            [AttributeUsage(AttributeTargets.Assembly)]
            public sealed class JasperFxAssemblyAttribute : Attribute;
        }

        namespace Marten.Events.CodeGeneration
        {
            public class MartenIgnoreAttribute : JasperFx.Core.JasperFxIgnoreAttribute;
        }
        """,
        "MartenStandIns",
        GeneratorHarness.FrameworkReferences));

    /// <summary>
    /// Types of the names the analyzer reads, declared in another namespace or nested in a type of Marten's namespace.
    /// </summary>
    private static readonly MetadataReference Decoys = MetadataReference.CreateFromImage(GeneratorHarness.Emit(
        """
        using System;

        namespace Elsewhere
        {
            public interface ICompiledQuery<TDoc, TOut>;

            public sealed class MartenIgnoreAttribute : Attribute;
        }

        namespace Marten.Linq
        {
            public static class Holder
            {
                public interface ICompiledQuery<TDoc, TOut>;
            }
        }

        namespace Marten.Events.CodeGeneration
        {
            public static class Holder
            {
                public sealed class MartenIgnoreAttribute : Attribute;
            }
        }
        """,
        "MartenDecoys",
        GeneratorHarness.FrameworkReferences));

    /// <summary>Gets the library, the stand-ins of Marten and the decoys: an application querying through Marten.</summary>
    public static ImmutableArray<MetadataReference> WithMarten { get; } = [.. GeneratorHarness.LibraryReferences, StandIns, Decoys];

    /// <summary>Gets the library and the decoys alone: an application that does not reference Marten.</summary>
    public static ImmutableArray<MetadataReference> WithoutMarten { get; } = [.. GeneratorHarness.LibraryReferences, Decoys];
}
