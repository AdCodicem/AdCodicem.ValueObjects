using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.EntityFrameworkCore;
using AdCodicem.ValueObjects.HotChocolate;
using AdCodicem.ValueObjects.HotChocolate.Data;
using AdCodicem.ValueObjects.UnitTests.TestData;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Data.Filters;
using HotChocolate.Execution;
using HotChocolate.Execution.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// HotChocolate.Data's filters over value objects, through <c>AddValueObjectFiltering()</c>, on Entity Framework Core
/// over SQLite in memory and on rows held in memory: every value-object member is filtered through the operations of its
/// scalar, whose operands are read through its rules and translated to its column, and a refused one is answered with
/// the rule's code.
/// </summary>
public sealed class GraphQLFilteringTests : IDisposable
{
    private const string French = FilterRows.French;
    private const string German = FilterRows.German;
    private const string Spaced = "fr76 3000 6000 0112 3456 7890 189";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly List<string> _sql = [];

    public GraphQLFilteringTests()
    {
        _connection.Open();
        using var context = new FilterContext(new DbContextOptionsBuilder<FilterContext>().UseSqlite(_connection).Options);
        context.Database.EnsureCreated();
        context.Rows.AddRange(FilterRows.Seed());
        context.SaveChanges();
    }

    public static TheoryData<bool> Providers => [true, false];

    public static TheoryData<string> Every => GeneratedSurface.Samples.Names;

    /// <summary>Gets the SQL the context ran since the test last read it, on one line.</summary>
    private string Sql
    {
        get
        {
            var sql = Regex.Replace(string.Join(" ", _sql), @"\s+", " ");
            _sql.Clear();
            return sql;
        }
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>
    /// Without the package, HotChocolate.Data infers a filter from the struct, whose <c>value</c> Entity Framework Core
    /// translates only under <c>UseValueObjectQueries()</c>, and compares raw text the normalizer never saw.
    /// </summary>
    [Fact]
    public async Task Without_the_package_a_filter_names_the_value_and_compares_raw_text()
    {
        var untranslated = await SqliteAsync(static builder => builder.AddQueryType<FilterQuery>().AddSorting().AddProjections().AddValueObjects().AddFiltering());
        Sdl(untranslated).Should().Contain("input IbanFilterInput { and: [IbanFilterInput!] or: [IbanFilterInput!] countryCode: StringOperationFilterInput value: StringOperationFilterInput }");
        (await untranslated.ExecuteAsync($$"""{ rows(where: { iban: { value: { eq: "{{French}}" } } }) { id } }"""))
            .Error().Message().Should().Be("Unexpected Execution Error");
        (await untranslated.ExecuteAsync($$"""{ rows(where: { iban: { eq: "{{French}}" } }) { id } }"""))
            .Error().Message().Should().Be("The specified input object field `eq` does not exist.");

        var translated = await SqliteAsync(
            static builder => builder.AddQueryType<FilterQuery>().AddSorting().AddProjections().AddValueObjects().AddFiltering(),
            translated: true);
        (await translated.ExecuteAsync($$"""{ rows(where: { iban: { value: { eq: "{{French}}" } } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await translated.ExecuteAsync($$"""{ rows(where: { iban: { value: { eq: "{{Spaced}}" } } }) { id } }""")).Data().Should().Be("""{"rows":[]}""");
    }

    /// <summary>
    /// Every value-object member, required or nullable, of a generic construction, in a list or in a part of the row, is
    /// filtered through the comparable operations of its scalar, named after it; one over <see cref="bool"/> through
    /// <c>eq</c> and <c>neq</c>; every other member keeps HotChocolate's input.
    /// </summary>
    [Fact]
    public async Task Every_value_object_member_is_filtered_through_the_operations_of_its_scalar()
    {
        var sdl = Sdl(await MemoryAsync());

        sdl.Should()
            .Contain("input FilterRowFilterInput { and: [FilterRowFilterInput!] or: [FilterRowFilterInput!] id: IntOperationFilterInput name: StringOperationFilterInput iban: IbanOperationFilterInput qty: QuantityOperationFilterInput email: EmailAddressOperationFilterInput backup: EmailAddressOperationFilterInput balance: AmountOperationFilterInput customer: CustomerIdOperationFilterInput born: BirthDateOperationFilterInput country: CountryCodeOperationFilterInput consent: ConsentOperationFilterInput ref: ReferenceOfFilterOwnerOperationFilterInput previous: ListIbanOperationFilterInput inner: FilterInnerFilterInput }")
            .And.Contain("input IbanOperationFilterInput { eq: Iban neq: Iban in: [Iban!] nin: [Iban!] gt: Iban ngt: Iban gte: Iban ngte: Iban lt: Iban nlt: Iban lte: Iban nlte: Iban }")
            .And.Contain("input ListIbanOperationFilterInput { all: IbanOperationFilterInput none: IbanOperationFilterInput some: IbanOperationFilterInput any: Boolean }")
            .And.Contain("input ConsentOperationFilterInput { eq: Consent neq: Consent }")
            .And.Contain("input FilterInnerFilterInput { and: [FilterInnerFilterInput!] or: [FilterInnerFilterInput!] iban: IbanOperationFilterInput }")
            .And.Contain("scalar Iban")
            .And.NotContain("IbanFilterInput {", "no value object is inferred as an input of its properties");
    }

    /// <summary>
    /// The inputs are named after the scalar, so that a scalar the application names names its operations, and two value
    /// objects of one CLR name, whose scalars it sets apart, get two inputs, where HotChocolate's own comparable input,
    /// named after the CLR type, fails the schema.
    /// </summary>
    [Fact]
    public async Task The_inputs_are_named_after_the_scalar()
    {
        Sdl(await MemoryAsync(static builder => builder.AddValueObjects(static options => options.Name<Iban>("BankAccount")))).Should()
            .Contain("iban: BankAccountOperationFilterInput")
            .And.Contain("previous: ListBankAccountOperationFilterInput")
            .And.Contain("input BankAccountOperationFilterInput { eq: BankAccount neq: BankAccount");

        var codes = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<CodeRow>>().AddFiltering().AddValueObjectFiltering()
                .AddValueObjects(static options => options.Name<Shipping.Code>("ShippingCode")),
            static services => services.AddRows<CodeRow>());
        Sdl(codes).Should()
            .Contain("billing: CodeOperationFilterInput shipping: ShippingCodeOperationFilterInput")
            .And.Contain("input ShippingCodeOperationFilterInput { eq: ShippingCode");

        var comparable = () => GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<CodeRow>>()
                .AddFiltering(static convention => convention.AddDefaults()
                    .BindRuntimeType<Billing.Code, ComparableOperationFilterInputType<Billing.Code>>()
                    .BindRuntimeType<Shipping.Code, ComparableOperationFilterInputType<Shipping.Code>>())
                .AddValueObjects(static options => options.Name<Shipping.Code>("ShippingCode")),
            static services => services.AddRows<CodeRow>());
        (await comparable.Should().ThrowAsync<SchemaException>())
            .WithMessage("*The name `ComparableCodeOperationFilterInput` was already registered by another type.*");
    }

    /// <summary>
    /// Every comparable operation reads its operand through the scalar, normalized, and compares it with the member through
    /// the value object's operators, which Entity Framework Core translates to the column, on SQLite as in memory.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task The_comparable_operations_read_their_operands_through_the_scalar(bool onSqlite)
    {
        var executor = await ExecutorAsync(onSqlite);

        (await executor.ExecuteAsync($$"""{ rows(where: { iban: { eq: "{{Spaced}}" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        SqlShouldContain(onSqlite, """WHERE "r"."Iban" = @p""", $"@p='{French}'");
        (await executor.ExecuteAsync($$"""{ rows(where: { iban: { neq: "{{Spaced}}" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        (await executor.ExecuteAsync($$"""{ rows(where: { iban: { in: ["{{German}}", "{{Spaced}}"] } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1},{"id":2}]}""");
        SqlShouldContain(onSqlite, """WHERE "r"."Iban" IN (@p1, @p2)""");
        (await executor.ExecuteAsync($$"""{ rows(where: { iban: { nin: ["{{German}}"] } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");

        (string Operation, string Expected)[] cases =
            [
                ("gt: 3", "[{\"id\":2}]"), ("ngt: 3", "[{\"id\":1}]"), ("gte: 9", "[{\"id\":2}]"), ("ngte: 9", "[{\"id\":1}]"),
                ("lt: 9", "[{\"id\":1}]"), ("nlt: 9", "[{\"id\":2}]"), ("lte: 3", "[{\"id\":1}]"), ("nlte: 3", "[{\"id\":2}]"),
            ];
        foreach (var (operation, expected) in cases)
        {
            (await executor.ExecuteAsync($$"""{ rows(where: { qty: { {{operation}} } }) { id } }""")).Data()
                .Should().Be("{\"rows\":" + expected + "}", "qty {0}", operation);
        }

        SqlShouldContain(onSqlite, """WHERE "r"."Qty" > @p""", """WHERE "r"."Qty" <= @p""");

        (await executor.ExecuteAsync("{ rows(where: { backup: { eq: null } }) { id } }")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        SqlShouldContain(onSqlite, """WHERE "r"."Backup" IS NULL""");
        (await executor.ExecuteAsync("{ rows(where: { backup: { neq: null } }) { id } }")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { backup: { eq: "ADA@BACKUP.ORG" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { backup: { in: ["ada@backup.org"] } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { backup: { nin: ["ada@backup.org"] } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        SqlShouldContain(onSqlite, """WHERE "r"."Backup" <> @p1 OR "r"."Backup" IS NULL""");
        (await executor.ExecuteAsync("{ rows(where: { balance: { gte: 10 } }) { id } }")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync($$"""{ rows(where: { customer: { eq: "{{FilterRows.Second}}" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { born: { lt: "1990-01-01" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { country: { eq: " fr " } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync("{ rows(where: { consent: { eq: true } }) { id } }")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync("{ rows(where: { consent: { neq: true } }) { id } }")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { ref: { eq: " po-1 " } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        SqlShouldContain(onSqlite, """WHERE "r"."Ref" = @p""", "@p='PO-1'");
        (await executor.ExecuteAsync($$"""{ rows(where: { inner: { iban: { eq: "{{Spaced}}" } } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync($$"""{ rows(where: { or: [{ iban: { eq: "{{German}}" } }, { qty: { lt: 5 } }] }) { id } }""")).Data()
            .Should().Be("""{"rows":[{"id":1},{"id":2}]}""");
        (await executor.ExecuteAsync($$"""{ rows(where: { and: [{ iban: { eq: "{{German}}" } }, { qty: { lt: 5 } }] }) { id } }""")).Data()
            .Should().Be("""{"rows":[]}""");
        (await executor.ExecuteAsync("query($w: FilterRowFilterInput) { rows(where: $w) { id } }", $$$"""{"w":{"iban":{"eq":"{{{Spaced}}}"} } }""")).Data()
            .Should().Be("""{"rows":[{"id":1}]}""");
    }

    /// <summary>
    /// A list of value objects is filtered through HotChocolate's list operations over the operations of its element,
    /// translated to the JSON SQLite stores it as, and a refused element is answered with its rule.
    /// </summary>
    [Theory]
    [MemberData(nameof(Providers))]
    public async Task A_list_of_value_objects_is_filtered_through_the_operations_of_its_element(bool onSqlite)
    {
        var executor = await ExecutorAsync(onSqlite);

        (await executor.ExecuteAsync("""{ rows(where: { previous: { some: { eq: "de89 3704 0044 0532 0130 00" } } }) { id } }""")).Data()
            .Should().Be("""{"rows":[{"id":1}]}""");
        SqlShouldContain(onSqlite, """WHERE @p IN ( SELECT "p"."value" FROM json_each("r"."Previous") AS "p" )""", $"@p='{German}'");
        (await executor.ExecuteAsync($$"""{ rows(where: { previous: { all: { neq: "{{German}}" } } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        (await executor.ExecuteAsync($$"""{ rows(where: { previous: { none: { eq: "{{German}}" } } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        (await executor.ExecuteAsync("{ rows(where: { previous: { any: true } }) { id } }")).Data().Should().Be("""{"rows":[{"id":1}]}""");

        var error = (await executor.ExecuteAsync("""{ rows(where: { previous: { some: { eq: "XX" } } }) { id } }""")).Error();
        error.Code().Should().Be(ValueObjectErrorCodes.TooShort);
        error.Member("inputPath").Should().Be("""["eq"]""");
    }

    /// <summary>
    /// A refused operand is the scalar's own refusal: the rule's code and message, at the operation HotChocolate names, in
    /// a literal, an element of <c>in</c> or a variable, which an error filter of the application reads the code of; a
    /// literal of another kind is refused by HotChocolate's validation, with no rule's code.
    /// </summary>
    [Fact]
    public async Task A_refused_operand_is_answered_with_its_rules_code_at_its_operation()
    {
        var executor = await MemoryAsync(static builder => builder.AddErrorFilter(static error =>
            error.Exception is { } exception && ValueObjectErrors.TryGetCode(exception, out var code) ? error.SetExtension("read", code) : error));

        var literal = (await executor.ExecuteAsync("""{ rows(where: { iban: { eq: "XX" } }) { id } }""")).Error();
        literal.Code().Should().Be(ValueObjectErrorCodes.TooShort);
        literal.Message().Should().Be("The value must be at least 15 characters long.");
        literal.Member("inputPath").Should().Be("""["eq"]""");
        literal.Member("coordinate").Should().Be("\"IbanOperationFilterInput.eq\"");
        literal.Member("path").Should().Be("""["rows"]""");
        literal.Member("read").Should().Be($"\"{ValueObjectErrorCodes.TooShort}\"");

        var element = (await executor.ExecuteAsync($$"""{ rows(where: { iban: { in: ["{{German}}", "XX"] } }) { id } }""")).Error();
        element.Code().Should().Be(ValueObjectErrorCodes.TooShort);
        element.Member("inputPath").Should().Be("""["in",1]""");
        element.Member("coordinate").Should().Be("\"IbanOperationFilterInput.in\"");

        var bound = (await executor.ExecuteAsync("{ rows(where: { qty: { gt: 5000 } }) { id } }")).Error();
        bound.Code().Should().Be(ValueObjectErrorCodes.OutOfRange);

        var variable = (await executor.ExecuteAsync("query($w: FilterRowFilterInput) { rows(where: $w) { id } }", """{"w":{"iban":{"eq":"XX"}}}""")).Error();
        variable.Code().Should().Be(ValueObjectErrorCodes.TooShort);
        variable.Member("inputPath").Should().Be("""["w","iban","eq"]""");
        variable.Member("path").Should().BeNull("HotChocolate coerces a variable before any field");

        var kind = (await executor.ExecuteAsync("""{ rows(where: { qty: { eq: "3" } }) { id } }""")).Error();
        kind.Message().Should().Be("The specified value type of field `eq` does not match the field type.");
        kind.Code().Should().BeNull();
    }

    /// <summary>
    /// Sorting orders by the column, a nullable member and a generic construction included, and a projection selects the
    /// columns of value objects, with nothing the package adds.
    /// </summary>
    [Fact]
    public async Task Sorting_and_projections_read_the_columns()
    {
        var executor = await ExecutorAsync(onSqlite: true);

        (await executor.ExecuteAsync("{ rows(order: { iban: ASC }) { id } }")).Data().Should().Be("""{"rows":[{"id":2},{"id":1}]}""");
        Sql.Should().Contain("ORDER BY \"r\".\"Iban\"");
        (await executor.ExecuteAsync("{ rows(order: { balance: DESC }) { id balance } }")).Data().Should().Be("""{"rows":[{"id":1,"balance":12.5},{"id":2,"balance":null}]}""");
        Sql.Should().Contain("""ORDER BY "r"."Balance" DESC""");
        (await executor.ExecuteAsync("{ rows(order: { ref: DESC }) { ref } }")).Data().Should().Be("""{"rows":[{"ref":"PO-2"},{"ref":"PO-1"}]}""");
        (await executor.ExecuteAsync("{ projected(where: { qty: { gt: 1 } }) { iban backup } }")).Data()
            .Should().Be($$"""{"projected":[{"iban":"{{French}}","backup":"ada@backup.org"},{"iban":"{{German}}","backup":null}]}""");
        Sql.Should().Contain("""SELECT "r"."Iban", "r"."Backup" FROM "Rows" AS "r" WHERE "r"."Qty" > @p""");
    }

    /// <summary>
    /// A binding of the application's own, a field its filter types itself and HotChocolate's <c>[ID]</c> are left alone,
    /// while a value-object field its filter does not type is replaced; a subclass of the package's input keeps the name
    /// it gives itself, and its list is named after it; a field returning value objects themselves keeps the input
    /// HotChocolate infers from the struct.
    /// </summary>
    [Fact]
    public async Task What_the_application_binds_or_types_itself_is_left_alone()
    {
        var bound = await MemoryAsync<ExplicitFilterQuery>(static convention => convention.AddDefaults().BindRuntimeType<Quantity, QuantityEqOnly>());
        Sdl(bound).Should()
            .Contain("qty: QuantityEqOnlyFilterInput")
            .And.Contain("input ExplicitRowFilter { and: [ExplicitRowFilter!] or: [ExplicitRowFilter!] iban: IbanOperationFilterInput backup: EmailAddressOperationFilterInput qty: QuantityEqOnlyFilterInput name: StringOperationFilterInput }");
        (await bound.ExecuteAsync($$"""{ explicit(where: { iban: { eq: "{{Spaced}}" }, backup: { eq: "ADA@BACKUP.ORG" }, name: { eq: "first" } }) { id } }""")).Data()
            .Should().Be("""{"explicit":[{"id":1}]}""");

        var named = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<QuantityRow>>()
                .AddFiltering(static convention => convention.AddDefaults().BindRuntimeType<Quantity, QuantityRange>().BindRuntimeType<Quantity?, QuantityRange>())
                .AddValueObjectFiltering(),
            static services => services.AddRows(new QuantityRow { Id = 1, Qty = Quantity.Create(3), Counts = [Quantity.Create(4)] }));
        Sdl(named).Should()
            .Contain("qty: QuantityRange counts: ListQuantityRange")
            .And.Contain("input ListQuantityRange { all: QuantityRange none: QuantityRange some: QuantityRange any: Boolean }");
        (await named.ExecuteAsync("{ rows(where: { counts: { some: { gt: 3 } } }) { id } }")).Data().Should().Be("""{"rows":[{"id":1}]}""");

        var narrow = await MemoryAsync<FilterQuery>(static convention => convention.AddDefaults().BindRuntimeType<Iban, NarrowIban>().BindRuntimeType<Iban?, NarrowIban>());
        Sdl(narrow).Should().Contain("input IbanOperationFilterInput { eq: Iban neq: Iban in: [Iban!] nin: [Iban!] gte: Iban ngte: Iban lt: Iban");
        (await narrow.ExecuteAsync($$"""{ rows(where: { iban: { gt: "{{German}}" } }) { id } }""")).Error().Message()
            .Should().Be("The specified input object field `gt` does not exist.");

        var identified = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<IdRow>>().AddDefaultNodeIdSerializer().AddFiltering().AddValueObjectFiltering(),
            static services => services.AddRows<IdRow>());
        Sdl(identified).Should().Contain("customer: IdOperationFilterInput");

        // A field returning value objects themselves filters them through the input HotChocolate infers from the struct,
        // which is no member a filter input holds.
        var values = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<Iban>>().AddFiltering().AddValueObjectFiltering(),
            static services => services.AddRows(Iban.Create(French), Iban.Create(German)));
        Sdl(values).Should().Contain(
            "input IbanFilterInput { and: [IbanFilterInput!] or: [IbanFilterInput!] countryCode: StringOperationFilterInput value: StringOperationFilterInput }");
        (await values.ExecuteAsync("""{ rows(where: { or: [{ value: { startsWith: "DE" } }] }) }""")).Data().Should().Be($$"""{"rows":["{{German}}"]}""");
    }

    /// <summary>
    /// A filter convention of the application's own is covered, and a named one too, whose types keep HotChocolate's
    /// prefix beside the default convention's and answer alike; the text operations extend the default convention alone.
    /// </summary>
    [Fact]
    public async Task Every_filter_convention_is_covered_and_a_named_one_keeps_its_prefix()
    {
        var convention = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<FilterQuery>().AddSorting().AddProjections().AddFiltering<ShopFilterConvention>().AddValueObjectFiltering(),
            static services => services.AddFilterRows());
        Sdl(convention).Should().Contain("iban: IbanOperationFilterInput");
        (await convention.ExecuteAsync($$"""{ rows(where: { iban: { eq: "{{Spaced}}" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");

        var scoped = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<ScopedFilterQuery>().AddSorting().AddProjections().AddFiltering().AddFiltering("s")
                .AddValueObjectFiltering(static options => options.IncludeTextOperations()),
            static services => services.AddFilterRows());
        var sdl = Sdl(scoped);
        sdl.Should()
            .Contain("input s_FilterRowFilterInput { and: [s_FilterRowFilterInput!] or: [s_FilterRowFilterInput!] id: s_IntOperationFilterInput name: s_StringOperationFilterInput iban: s_IbanOperationFilterInput")
            .And.Contain("previous: s_ListIbanOperationFilterInput")
            .And.Contain("input s_ListIbanOperationFilterInput { all: s_IbanOperationFilterInput none: s_IbanOperationFilterInput some: s_IbanOperationFilterInput any: Boolean }")
            .And.Contain("input s_IbanOperationFilterInput { eq: Iban neq: Iban in: [Iban!] nin: [Iban!] gt: Iban ngt: Iban gte: Iban ngte: Iban lt: Iban nlt: Iban lte: Iban nlte: Iban }")
            .And.Contain("input IbanOperationFilterInput { eq: Iban neq: Iban in: [Iban!] nin: [Iban!] gt: Iban ngt: Iban gte: Iban ngte: Iban lt: Iban nlt: Iban lte: Iban nlte: Iban contains: String");
        (await scoped.ExecuteAsync($$"""{ scoped(where: { iban: { eq: "{{Spaced}}" }, previous: { some: { eq: "{{German}}" } } }) { id } }""")).Data()
            .Should().Be("""{"scoped":[{"id":1}]}""");
        (await scoped.ExecuteAsync("""{ rows(where: { iban: { startsWith: "DE" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
    }

    /// <summary>
    /// <c>BindValueObject&lt;TSelf, TValue&gt;()</c> binds a value object and its nullable type at compile time, named
    /// after its scalar, a list of them too, and reads its operands through the scalar.
    /// </summary>
    [Fact]
    public async Task A_typed_binding_names_its_value_object_and_its_lists_after_the_scalar()
    {
        var executor = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<ReferenceRow>>()
                .AddFiltering(static convention => convention.AddDefaults().BindValueObject<Reference<FilterOwner>, string>())
                .AddValueObjectFiltering(),
            static services => services.AddRows(new ReferenceRow { Id = 1, Ref = Reference<FilterOwner>.Create("PO-1"), Refs = [Reference<FilterOwner>.Create("PO-2")] }));

        Sdl(executor).Should()
            .Contain("ref: ReferenceOfFilterOwnerOperationFilterInput maybe: ReferenceOfFilterOwnerOperationFilterInput refs: ListReferenceOfFilterOwnerOperationFilterInput")
            .And.Contain("input ReferenceOfFilterOwnerOperationFilterInput { eq: ReferenceOfFilterOwner");
        (await executor.ExecuteAsync("""{ rows(where: { ref: { eq: " po-1 " }, maybe: { eq: null }, refs: { some: { eq: "po-2" } } }) { id } }""")).Data()
            .Should().Be("""{"rows":[{"id":1}]}""");

        var alone = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<ReferenceRow>>().AddValueObjects()
                .AddFiltering(static convention => convention.AddDefaults().BindValueObject<Reference<FilterOwner>, string>()),
            static services => services.AddRows<ReferenceRow>());
        Sdl(alone).Should().Contain("ref: ReferenceOfFilterOwnerOperationFilterInput maybe: ReferenceOfFilterOwnerOperationFilterInput", "both forms are bound");
        FluentActions.Invoking(static () => ((IFilterConventionDescriptor)null!).BindValueObject<Iban, string>())
            .Should().Throw<ArgumentNullException>().WithParameterName("descriptor");
    }

    /// <summary>
    /// A value object nothing registered, a construction of a generic one or one written by hand, is described by the
    /// registry when the filter meets it; a struct that is no value object keeps HotChocolate's input.
    /// </summary>
    [Fact]
    public async Task A_value_object_nothing_registered_is_described_when_the_filter_meets_it()
    {
        var executor = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<ResolvedRow>>().AddFiltering().AddValueObjectFiltering(),
            static services => services.AddRows(
                new ResolvedRow { Id = 1, Tag = Declared<FilterTag, string>.Create("AB"), Ref = Reference<ResolvedOwner>.Create("R-1"), Point = new(1, 2), Points = [new(5, 6)] },
                new ResolvedRow { Id = 2, Tag = Declared<FilterTag, string>.Create("CD"), Ref = Reference<ResolvedOwner>.Create("R-2"), Point = new(3, 4) }));

        Sdl(executor).Should()
            .Contain("ref: ReferenceOfResolvedOwnerOperationFilterInput point: PointFilterInput points: ListFilterInputTypeOfPointFilterInput")
            .And.Contain("input PointFilterInput { and: [PointFilterInput!] or: [PointFilterInput!] x: IntOperationFilterInput y: IntOperationFilterInput }")
            .And.MatchRegex("tag: [A-Za-z]+OperationFilterInput");
        (await executor.ExecuteAsync("""{ rows(where: { tag: { eq: "CD" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { ref: { in: [" r-1 "] }, point: { x: { eq: 1 } }, points: { some: { y: { eq: 6 } } } }) { id } }""")).Data()
            .Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { tag: { eq: "TOOLONGTAG" } }) { id } }""")).Error().Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// The comparable operations work wherever the call stands; the text operations extend the default convention, so
    /// called before <c>AddFiltering()</c> they fail the schema at start, naming it; every call on a schema configures one
    /// set of options, and each schema of an application its own.
    /// </summary>
    [Fact]
    public async Task Every_call_on_a_schema_configures_one_set_of_options_after_AddFiltering()
    {
        var before = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<FilterQuery>().AddSorting().AddProjections().AddValueObjectFiltering().AddFiltering(),
            static services => services.AddFilterRows());
        (await before.ExecuteAsync($$"""{ rows(where: { iban: { eq: "{{Spaced}}" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");

        var textBefore = () => GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<FilterQuery>().AddSorting().AddProjections()
                .AddValueObjectFiltering(static options => options.IncludeTextOperations()).AddFiltering(),
            static services => services.AddFilterRows());
        (await textBefore.Should().ThrowAsync<SchemaException>()).WithMessage("*No default filter convention found. Call `AddFiltering()`*");

        var twice = await MemoryAsync(static builder => builder.AddValueObjectFiltering().AddValueObjectFiltering(static options => options.IncludeTextOperations()));
        Sdl(twice).Should().Contain("nlte: Iban contains: String", "a later call turns the text operations on");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("other", new object());
        services.AddFilterRows();
        Filtering<FilterQuery>(services.AddGraphQL("a"), text: true);
        Filtering<FilterQuery>(services.AddGraphQL("b")).AddValueObjectFiltering();
        services.Where(static service => service.ServiceType == typeof(ValueObjectFilteringRegistration))
            .Select(static service => ((ValueObjectFilteringRegistration)service.ImplementationInstance!).SchemaName)
            .Should().Equal("a", "b");
        var provider = services.BuildServiceProvider();
        var cancellation = TestContext.Current.CancellationToken;
        Sdl(await provider.GetRequestExecutorAsync("a", cancellation)).Should().Contain("nlte: Iban contains: String");
        Sdl(await provider.GetRequestExecutorAsync("b", cancellation)).Should().Contain("nlte: Iban }").And.NotContain("nlte: Iban contains");

        FluentActions.Invoking(static () => ValueObjectFilteringRequestExecutorBuilderExtensions.AddValueObjectFiltering(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    /// <summary>
    /// The text operations compare the underlying text of a value object over <see cref="string"/> with a fragment read as
    /// a <c>String</c>, as typed, which Entity Framework Core translates under <c>UseValueObjectQueries()</c> as it does
    /// for a <c>String</c> member, a nullable member guarded and a list's element included.
    /// </summary>
    [Fact]
    public async Task The_text_operations_compare_the_underlying_text_translated_under_the_value_translator()
    {
        var executor = await SqliteAsync(static builder => Filtering<FilterQuery>(builder, text: true), translated: true);

        Sdl(executor).Should()
            .Contain("input IbanOperationFilterInput { eq: Iban neq: Iban in: [Iban!] nin: [Iban!] gt: Iban ngt: Iban gte: Iban ngte: Iban lt: Iban nlt: Iban lte: Iban nlte: Iban contains: String ncontains: String startsWith: String nstartsWith: String endsWith: String nendsWith: String }")
            .And.Contain("input QuantityOperationFilterInput { eq: Quantity neq: Quantity in: [Quantity!] nin: [Quantity!] gt: Quantity ngt: Quantity gte: Quantity ngte: Quantity lt: Quantity nlt: Quantity lte: Quantity nlte: Quantity }");

        (await executor.ExecuteAsync("""{ rows(where: { iban: { startsWith: "FR" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        Sql.Should().Contain("""WHERE "r"."Iban" LIKE @p_startswith ESCAPE '\'""").And.Contain("@p_startswith='FR%'");
        (await executor.ExecuteAsync("""{ rows(where: { iban: { nstartsWith: "FR" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        Sql.Should().Contain("""WHERE "r"."Iban" NOT LIKE @p_startswith ESCAPE '\'""");
        (await executor.ExecuteAsync("""{ rows(where: { iban: { contains: "7630" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        Sql.Should().Contain("""WHERE instr("r"."Iban", @p) > 0""");
        (await executor.ExecuteAsync("""{ rows(where: { iban: { ncontains: "7630" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        Sql.Should().Contain("""WHERE instr("r"."Iban", @p) <= 0""");
        (await executor.ExecuteAsync("""{ rows(where: { email: { endsWith: "@example.com" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        Sql.Should().Contain("""WHERE "r"."Email" LIKE @p_endswith ESCAPE '\'""");
        (await executor.ExecuteAsync("""{ rows(where: { email: { nendsWith: "@example.com" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { iban: { contains: "fr76" } }) { id } }""")).Data().Should().Be("""{"rows":[]}""", "a fragment is never normalized");

        (await executor.ExecuteAsync("""{ rows(where: { backup: { contains: "backup" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        Sql.Should().Contain("""WHERE "r"."Backup" IS NOT NULL AND "r"."Backup" IS NOT NULL AND instr("r"."Backup", @p) > 0""");
        (await executor.ExecuteAsync("""{ rows(where: { backup: { ncontains: "backup" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        Sql.Should().Contain("""WHERE "r"."Backup" IS NULL OR "r"."Backup" IS NULL OR instr("r"."Backup", @p) <= 0""");
        (await executor.ExecuteAsync("""{ rows(where: { country: { startsWith: "F" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { ref: { endsWith: "-2" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { previous: { some: { startsWith: "DE" } } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        Sql.Should().Contain("""CAST("p"."value" AS TEXT) LIKE @p_startswith ESCAPE '\'""");
        (await executor.ExecuteAsync("""{ rows(where: { email: { eq: "ADA@EXAMPLE.COM" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""", "eq stays the scalar's");
        (await executor.ExecuteAsync("query($t: String) { rows(where: { iban: { startsWith: $t } }) { id } }", """{"t":"DE"}""")).Data()
            .Should().Be("""{"rows":[{"id":2}]}""");

        var untranslated = await SqliteAsync(
            static builder => Filtering<FilterQuery>(builder, text: true).ModifyRequestOptions(static options => options.IncludeExceptionDetails = true));
        var error = (await untranslated.ExecuteAsync("""{ rows(where: { iban: { startsWith: "FR" } }) { id } }""")).Error();
        error.Message().Should().Be("Unexpected Execution Error");
        error["extensions"]!["exception"]!["message"]!.GetValue<string>().Should().Contain("could not be translated");
    }

    /// <summary>
    /// In memory, every text operation compares the underlying text, a nullable member's <see langword="null"/> neither
    /// containing nor starting with anything, and a value object written by hand whose value is implemented explicitly.
    /// </summary>
    [Fact]
    public async Task In_memory_the_text_operations_read_the_value_through_its_interface()
    {
        var executor = await MemoryAsync(static builder => builder.AddValueObjectFiltering(static options => options.IncludeTextOperations()));

        (string Operation, string Expected)[] cases =
            [
                ("""iban: { contains: "7630" }""", "[{\"id\":1}]"), ("""iban: { ncontains: "7630" }""", "[{\"id\":2}]"),
                ("""iban: { startsWith: "DE" }""", "[{\"id\":2}]"), ("""iban: { nstartsWith: "DE" }""", "[{\"id\":1}]"),
                ("""iban: { endsWith: "189" }""", "[{\"id\":1}]"), ("""iban: { nendsWith: "189" }""", "[{\"id\":2}]"),
                ("""backup: { contains: "a" }""", "[{\"id\":1}]"), ("""backup: { ncontains: "a" }""", "[{\"id\":2}]"),
                ("""backup: { startsWith: "a" }""", "[{\"id\":1}]"), ("""backup: { nendsWith: "org" }""", "[{\"id\":2}]"),
            ];
        foreach (var (operation, expected) in cases)
        {
            (await executor.ExecuteAsync($$"""{ rows(where: { {{operation}} }) { id } }""")).Data()
                .Should().Be("{\"rows\":" + expected + "}", "{0}", operation);
        }

        var operatorless = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<OperatorlessRow>>().AddFiltering().AddValueObjectFiltering(static options => options.IncludeTextOperations()),
            static services => services.AddRows(new OperatorlessRow { Id = 1, Code = OperatorlessCode.Create("abc") }, new OperatorlessRow { Id = 2, Code = OperatorlessCode.Create("xyz") }));
        (await operatorless.ExecuteAsync("""{ rows(where: { code: { startsWith: "AB" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
    }

    /// <summary>
    /// A <see langword="null"/> fragment is refused as HotChocolate refuses one for a <c>String</c> member, word for word,
    /// from a literal or a variable, on a member of the value object's type and of its nullable type, and with no rule's
    /// code.
    /// </summary>
    [Fact]
    public async Task A_null_fragment_is_refused_as_for_a_String_member()
    {
        var executor = await MemoryAsync(static builder => builder.AddValueObjectFiltering(static options => options.IncludeTextOperations()));

        var refused = (await executor.ExecuteAsync("{ rows(where: { iban: { contains: null } }) { id } }")).Error();
        var text = (await executor.ExecuteAsync("{ rows(where: { name: { contains: null } }) { id } }")).Error();
        refused.Message().Should().Be("The provided value for filter `contains` of type IbanOperationFilterInput is invalid. Null values are not supported.");
        text.Message().Should().Be(refused.Message().Replace("IbanOperationFilterInput", "StringOperationFilterInput", StringComparison.Ordinal));
        refused.Code().Should().Be("HC0026").And.Be(text.Code());
        refused.Member("expectedType").Should().Be("\"String!\"").And.Be(text.Member("expectedType"));

        // A nullable member, which HotChocolate's base handler refuses a null for only where the handler cannot take one.
        foreach (var operation in (string[])["contains", "nstartsWith"])
        {
            var nullable = (await executor.ExecuteAsync($$"""{ rows(where: { backup: { {{operation}}: null } }) { id } }""")).Error();
            nullable.Message().Should().Be(
                $"The provided value for filter `{operation}` of type EmailAddressOperationFilterInput is invalid. Null values are not supported.");
            nullable.Code().Should().Be("HC0026");
            nullable.Member("expectedType").Should().Be("\"String!\"");
        }

        var variable = (await executor.ExecuteAsync("query($t: String) { rows(where: { iban: { startsWith: $t } }) { id } }", """{"t":null}""")).Error();
        variable.Code().Should().Be("HC0026");
    }

    /// <summary>
    /// The text operations need a value object over <see cref="string"/>: the package gives them to no other, and an
    /// application binding one to another fails the schema at start, with an <see cref="InvalidOperationException"/>
    /// naming the type.
    /// </summary>
    [Fact]
    public async Task The_text_operations_refuse_a_value_object_over_another_type()
    {
        var start = () => MemoryAsync<FilterQuery>(static convention => convention.AddDefaults()
            .BindRuntimeType<Quantity, ValueObjectTextOperationFilterInputType<Quantity>>()
            .BindRuntimeType<Quantity?, ValueObjectTextOperationFilterInputType<Quantity>>());

        (await start.Should().ThrowAsync<SchemaException>()).WithMessage("*Quantity' is not a value object over string*")
            .Which.Errors.Should().ContainSingle().Which.Exception.Should().BeOfType<InvalidOperationException>();
    }

    /// <summary>
    /// The text operations' type, bound by hand to a value object over <see cref="string"/>, runs them in the default
    /// convention under <c>IncludeTextOperations()</c>, which adds their handlers there alone: without the option, or in a
    /// named convention, the schema fails at start, naming the operation no handler takes.
    /// </summary>
    [Fact]
    public async Task The_text_type_bound_by_hand_needs_the_text_operations_of_the_default_convention()
    {
        static IFilterConventionDescriptor Bind(IFilterConventionDescriptor convention)
            => convention.AddDefaults()
                .BindRuntimeType<Iban, ValueObjectTextOperationFilterInputType<Iban>>()
                .BindRuntimeType<Iban?, ValueObjectTextOperationFilterInputType<Iban>>();

        var bound = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<FilterRow>>().AddFiltering(static convention => Bind(convention))
                .AddValueObjectFiltering(static options => options.IncludeTextOperations()),
            static services => services.AddFilterRows());
        (await bound.ExecuteAsync("""{ rows(where: { iban: { startsWith: "FR" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");

        var without = () => GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<FilterRow>>().AddFiltering(static convention => Bind(convention)).AddValueObjectFiltering(),
            static services => services.AddFilterRows());
        (await without.Should().ThrowAsync<SchemaException>()).WithMessage("*For the field contains of type IbanOperationFilterInput was no handler found.*");

        var named = () => GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<ScopedFilterQuery>().AddFiltering().AddFiltering(static convention => Bind(convention), "s")
                .AddValueObjectFiltering(static options => options.IncludeTextOperations()),
            static services => services.AddFilterRows());
        (await named.Should().ThrowAsync<SchemaException>()).WithMessage("*For the field contains of type s_IbanOperationFilterInput was no handler found.*");
    }

    /// <summary>
    /// Rows holding the uninitialized instance of a value object answer every operation and the sort without an exception.
    /// </summary>
    [Fact]
    public async Task Default_instances_in_memory_answer_every_operation()
    {
        var rows = FilterRows.Seed();
#pragma warning disable VO0010 // The uninitialized instances, as a row built by hand may hold.
        rows.Add(new FilterRow { Id = 3, Iban = default, Qty = default, Email = default, Backup = default(EmailAddress), Ref = default, Previous = [default], Inner = new() });
#pragma warning restore VO0010
        var executor = await MemoryAsync(static builder => builder.AddValueObjectFiltering(static options => options.IncludeTextOperations()), rows);

        (await executor.ExecuteAsync($$"""{ rows(where: { iban: { eq: "{{Spaced}}" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync($$"""{ rows(where: { iban: { gt: "{{German}}" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync("{ rows(where: { qty: { lt: 5 } }) { id } }")).Data().Should().Be("""{"rows":[{"id":1},{"id":3}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { iban: { nstartsWith: "FR" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":2},{"id":3}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { backup: { contains: "a" } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync("""{ rows(where: { previous: { some: { startsWith: "DE" } } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
        (await executor.ExecuteAsync("{ rows(order: { iban: ASC }) { id } }")).Data().Should().Be("""{"rows":[{"id":3},{"id":2},{"id":1}]}""");
    }

    /// <summary>
    /// HotChocolate.Data compares through the value object's operators, which a value object written by hand may lack: its
    /// <c>eq</c> and <c>gt</c> fail when they run, while its <c>in</c>, which goes through equality, answers.
    /// </summary>
    [Fact]
    public async Task A_value_object_written_by_hand_without_operators_answers_in_alone()
    {
        var executor = await GraphQLHarness.BuildAsync(
            static builder => builder.AddQueryType<RowsQuery<OperatorlessRow>>().AddFiltering().AddValueObjectFiltering()
                .ModifyRequestOptions(static options => options.IncludeExceptionDetails = true),
            static services => services.AddRows(new OperatorlessRow { Id = 1, Code = OperatorlessCode.Create("abc") }));

        foreach (var operation in (string[])["eq", "gt"])
        {
            var error = (await executor.ExecuteAsync($$"""{ rows(where: { code: { {{operation}}: "ABC" } }) { id } }""")).Error();
            error.Message().Should().Be("Unexpected Execution Error");
            error["extensions"]!["exception"]!["message"]!.GetValue<string>().Should().Contain("operator").And.Contain("is not defined");
        }

        (await executor.ExecuteAsync("""{ rows(where: { code: { in: [" abc "] } }) { id } }""")).Data().Should().Be("""{"rows":[{"id":1}]}""");
    }

    /// <summary>
    /// Every underlying type, option and hook of the domain, entity identifiers included, is filtered through its scalar,
    /// as the same comparisons of its instances answer, in memory and on SQLite.
    /// </summary>
    [Theory]
    [MemberData(nameof(Every))]
    public async Task Every_value_object_is_filtered_through_its_scalar(string type)
        => await GeneratedSurface.Samples.All[type].FiltersThroughItsScalarAsItsUnderlyingValueAsync();

    private static string Sdl(IRequestExecutor executor) => Regex.Replace(executor.Schema.ToString(), @"\s+", " ");

    private static IRequestExecutorBuilder Filtering<TQuery>(
        IRequestExecutorBuilder builder,
        bool text = false,
        Action<IFilterConventionDescriptor>? convention = null)
        where TQuery : class
    {
        builder.AddQueryType<TQuery>().AddSorting().AddProjections();
        if (convention is null)
        {
            builder.AddFiltering();
        }
        else
        {
            builder.AddFiltering(convention);
        }

        return builder.AddValueObjectFiltering(options =>
        {
            if (text)
            {
                options.IncludeTextOperations();
            }
        });
    }

    private static Task<IRequestExecutor> MemoryAsync(Action<IRequestExecutorBuilder>? configure = null, List<FilterRow>? rows = null)
        => GraphQLHarness.BuildAsync(
            builder =>
            {
                Filtering<FilterQuery>(builder);
                configure?.Invoke(builder);
            },
            services => services.AddFilterRows(rows));

    private static Task<IRequestExecutor> MemoryAsync<TQuery>(Action<IFilterConventionDescriptor> convention)
        where TQuery : class
        => GraphQLHarness.BuildAsync(builder => Filtering<TQuery>(builder, convention: convention), static services => services.AddFilterRows());

    private Task<IRequestExecutor> ExecutorAsync(bool onSqlite)
        => onSqlite ? SqliteAsync(static builder => Filtering<FilterQuery>(builder)) : MemoryAsync();

    private Task<IRequestExecutor> SqliteAsync(Action<IRequestExecutorBuilder> configure, bool translated = false)
        => GraphQLHarness.BuildAsync(
            configure,
            services => services.AddDbContext<FilterContext>(options =>
            {
                options.UseSqlite(_connection)
                    .EnableSensitiveDataLogging()
                    .LogTo(_sql.Add, [RelationalEventId.CommandExecuted], LogLevel.Information, DbContextLoggerOptions.None);
                if (translated)
                {
                    options.UseValueObjectQueries();
                }
            }));

    /// <summary>Checks the SQL run since the last read on SQLite; in memory, nothing runs any.</summary>
    private void SqlShouldContain(bool onSqlite, params string[] fragments)
    {
        var sql = Sql;
        if (onSqlite)
        {
            sql.Should().ContainAll(fragments);
        }
    }
}
