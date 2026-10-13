using System.Text.Json;
using AdCodicem.ValueObjects.HotChocolate;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.TestData;
using HotChocolate.Execution;
using HotChocolate.Language;
using HotChocolate.Types;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// What each underlying type becomes as a scalar of its own: the literal and the JSON value it reads, through the type's
/// rules, and the value it writes.
/// </summary>
public sealed class GraphQLScalarTests
{
    private static readonly Dictionary<string, Func<Task<IRequestExecutor>>> Echoes = new(StringComparer.Ordinal)
    {
        [nameof(Consent)] = EchoAsync<Consent>,
        [nameof(Grade)] = EchoAsync<Grade>,
        [nameof(Adjustment)] = EchoAsync<Adjustment>,
        [nameof(Score)] = EchoAsync<Score>,
        [nameof(Quantity)] = EchoAsync<Quantity>,
        [nameof(Port)] = EchoAsync<Port>,
        [nameof(PageNumber)] = EchoAsync<PageNumber>,
        [nameof(SequenceNumber)] = EchoAsync<SequenceNumber>,
        [nameof(FileSize)] = EchoAsync<FileSize>,
        [nameof(ByteCount)] = EchoAsync<ByteCount>,
        [nameof(LedgerBalance)] = EchoAsync<LedgerBalance>,
        [nameof(Fingerprint)] = EchoAsync<Fingerprint>,
        [nameof(Latitude)] = EchoAsync<Latitude>,
        [nameof(Ratio)] = EchoAsync<Ratio>,
        [nameof(Amount)] = EchoAsync<Amount>,
        [nameof(UnboundedDecimal)] = EchoAsync<UnboundedDecimal>,
        [nameof(OpeningTime)] = EchoAsync<OpeningTime>,
        [nameof(RecordedAt)] = EchoAsync<RecordedAt>,
        [nameof(OccurredAt)] = EchoAsync<OccurredAt>,
        [nameof(Duration)] = EchoAsync<Duration>,
        [nameof(EffectiveDate)] = EchoAsync<EffectiveDate>,
        [nameof(CustomerId)] = EchoAsync<CustomerId>,
        [nameof(Iban)] = EchoAsync<Iban>,
        [nameof(Label)] = EchoAsync<Label>,
        [nameof(DocumentStatus)] = EchoAsync<DocumentStatus>,
    };

    /// <summary>
    /// A literal of the scalar's kind is read exactly as the underlying type, normalized, and written back as the
    /// underlying value: an integer of any width up to 64 bits as an Int, a real as a Float, from an Int literal too, and
    /// everything else as text in its invariant form.
    /// </summary>
    [Theory]
    [InlineData(nameof(Consent), "true", "true")]
    [InlineData(nameof(Adjustment), "-3", "-3")]
    [InlineData(nameof(Score), "100", "100")]
    [InlineData(nameof(Quantity), "1000", "1000")]
    [InlineData(nameof(Port), "65535", "65535")]
    [InlineData(nameof(PageNumber), "2147483647", "2147483647")]
    [InlineData(nameof(SequenceNumber), "4294967295", "4294967295")]
    [InlineData(nameof(FileSize), "9223372036854775807", "9223372036854775807")]
    [InlineData(nameof(ByteCount), "18446744073709551615", "18446744073709551615")]
    [InlineData(nameof(Latitude), "48.85", "48.85")]
    [InlineData(nameof(Latitude), "-90", "-90")]
    [InlineData(nameof(Latitude), "1e1", "10")]
    [InlineData(nameof(Ratio), "0.1", "0.1")]
    [InlineData(nameof(Amount), "12.5", "12.50")]
    [InlineData(nameof(Amount), "1e3", "1000.00")]
    [InlineData(nameof(UnboundedDecimal), "-79228162514264337593543950335", "-79228162514264337593543950335")]
    [InlineData(nameof(UnboundedDecimal), "-0.0000000000000000000000000001", "-0.0000000000000000000000000001")]
    [InlineData(nameof(LedgerBalance), "\"-999999999999999999999\"", "\"-999999999999999999999\"")]
    [InlineData(nameof(Fingerprint), "\"340282366920938463463374607431768211455\"", "\"340282366920938463463374607431768211455\"")]
    [InlineData(nameof(Grade), "\"b\"", "\"B\"")]
    [InlineData(nameof(OpeningTime), "\"07:30\"", "\"07:30:00.0000000\"")]
    [InlineData(nameof(RecordedAt), "\"2026-10-13T09:15:00\"", "\"2026-10-13T09:15:00.0000000\"")]
    [InlineData(nameof(RecordedAt), "\"2026-10-13\"", "\"2026-10-13T00:00:00.0000000\"")]
    [InlineData(nameof(OccurredAt), "\"2026-10-13T09:15:00+02:00\"", "\"2026-10-13T09:15:00.0000000+02:00\"")]
    [InlineData(nameof(Duration), "\"01:30:00\"", "\"01:30:00\"")]
    [InlineData(nameof(EffectiveDate), "\"2026-10-13\"", "\"2026-10-13\"")]
    [InlineData(nameof(CustomerId), "\"3F2504E0-4F89-11D3-9A0C-0305E82C3301\"", "\"3f2504e0-4f89-11d3-9a0c-0305e82c3301\"")]
    [InlineData(nameof(Iban), "\"fr76 3000 6000 0112 3456 7890 189\"", "\"FR7630006000011234567890189\"")]
    [InlineData(nameof(Label), "\"\"", "\"\"")]
    [InlineData(nameof(DocumentStatus), "\"FINAL\"", "\"FINAL\"")]
    public async Task A_literal_is_read_through_the_rules_and_written_as_the_underlying_value(string type, string literal, string written)
    {
        var executor = await Echoes[type]();

        (await executor.ExecuteAsync($"{{ echo(value: {literal}) }}")).Data().Should().Be($$"""{"echo":{{written}}}""");
    }

    /// <summary>
    /// A literal the type refuses is answered with the rule's code and message, and a number the underlying type cannot
    /// hold, one past either end of its range or a real that is not finite, with <c>value_object.not_parsable</c>.
    /// </summary>
    [Theory]
    [InlineData(nameof(Adjustment), "11", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 10.")]
    [InlineData(nameof(Adjustment), "-129", ValueObjectErrorCodes.NotParsable, "The number is not a valid sbyte.")]
    [InlineData(nameof(Score), "256", ValueObjectErrorCodes.NotParsable, "The number is not a valid byte.")]
    [InlineData(nameof(Score), "-1", ValueObjectErrorCodes.NotParsable, "The number is not a valid byte.")]
    [InlineData(nameof(Quantity), "32768", ValueObjectErrorCodes.NotParsable, "The number is not a valid short.")]
    [InlineData(nameof(Port), "65536", ValueObjectErrorCodes.NotParsable, "The number is not a valid ushort.")]
    [InlineData(nameof(Port), "0", ValueObjectErrorCodes.OutOfRange, "The value must be greater than or equal to 1.")]
    [InlineData(nameof(PageNumber), "2147483648", ValueObjectErrorCodes.NotParsable, "The number is not a valid int.")]
    [InlineData(nameof(SequenceNumber), "-1", ValueObjectErrorCodes.NotParsable, "The number is not a valid uint.")]
    [InlineData(nameof(FileSize), "9223372036854775808", ValueObjectErrorCodes.NotParsable, "The number is not a valid long.")]
    [InlineData(nameof(ByteCount), "18446744073709551616", ValueObjectErrorCodes.NotParsable, "The number is not a valid ulong.")]
    [InlineData(nameof(Latitude), "90.5", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 90.")]
    [InlineData(nameof(Latitude), "1e400", ValueObjectErrorCodes.NotParsable, "The number is not a valid double.")]
    [InlineData(nameof(Ratio), "1e39", ValueObjectErrorCodes.NotParsable, "The number is not a valid float.")]
    [InlineData(nameof(Amount), "1e400", ValueObjectErrorCodes.NotParsable, "The number is not a valid decimal.")]
    [InlineData(nameof(Amount), "-1", ValueObjectErrorCodes.OutOfRange, "The value must be greater than or equal to 0.")]
    [InlineData(nameof(LedgerBalance), "\"1000000000000000000001\"", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 1000000000000000000000.")]
    [InlineData(nameof(Grade), "\"AB\"", ValueObjectErrorCodes.NotParsable, "The text is not a valid char.")]
    [InlineData(nameof(Grade), "\"Z\"", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to F.")]
    [InlineData(nameof(OpeningTime), "\"noon\"", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.TimeOnly.")]
    [InlineData(nameof(Duration), "\"PT1H30M\"", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.TimeSpan.")]
    [InlineData(nameof(CustomerId), "\"not-a-guid\"", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.Guid.")]
    [InlineData(nameof(Iban), "\"XX\"", ValueObjectErrorCodes.TooShort, "The value must be at least 15 characters long.")]
    [InlineData(nameof(DocumentStatus), "\"archived\"", ValueObjectErrorCodes.NotAKnownValue, "The value is not one of the accepted values.")]
    public async Task A_literal_the_type_refuses_is_answered_with_the_rules_code(string type, string literal, string code, string message)
    {
        var executor = await Echoes[type]();

        var error = (await executor.ExecuteAsync($"{{ echo(value: {literal}) }}")).Error();

        error.Code().Should().Be(code);
        error.Message().Should().Be(message);
        error.Member("inputPath").Should().Be("""["value"]""");
    }

    /// <summary>
    /// HotChocolate's own validation refuses a literal of another kind than the scalar's before the scalar reads it,
    /// with no code: a Float takes an Int literal, nothing else takes another kind.
    /// </summary>
    [Theory]
    [InlineData(nameof(Quantity), "\"3\"")]
    [InlineData(nameof(Quantity), "3.5")]
    [InlineData(nameof(LedgerBalance), "5")]
    [InlineData(nameof(Iban), "12")]
    [InlineData(nameof(Consent), "\"true\"")]
    [InlineData(nameof(Latitude), "\"1\"")]
    public async Task A_literal_of_another_kind_is_refused_by_HotChocolates_validation(string type, string literal)
    {
        var executor = await Echoes[type]();

        var error = (await executor.ExecuteAsync($"{{ echo(value: {literal}) }}")).Error();

        error.Message().Should().Be("The specified argument value does not match the argument type.");
        error.Code().Should().BeNull();
    }

    /// <summary>
    /// A variable is read from its JSON value as a literal is, and one of another JSON kind reaches the scalar, which
    /// refuses it with <c>value_object.not_parsable</c>, naming the kind as GraphQL names it.
    /// </summary>
    [Theory]
    [InlineData(nameof(Quantity), "3", null, "3")]
    [InlineData(nameof(Quantity), "\"3\"", ValueObjectErrorCodes.NotParsable, "Quantity cannot read a String.")]
    [InlineData(nameof(Quantity), "3.5", ValueObjectErrorCodes.NotParsable, "The number is not a valid short.")]
    [InlineData(nameof(Quantity), "1e2", ValueObjectErrorCodes.NotParsable, "The number is not a valid short.")]
    [InlineData(nameof(Quantity), "true", ValueObjectErrorCodes.NotParsable, "Quantity cannot read a Boolean.")]
    [InlineData(nameof(Quantity), "{}", ValueObjectErrorCodes.NotParsable, "Quantity cannot read an Object.")]
    [InlineData(nameof(Quantity), "1001", ValueObjectErrorCodes.OutOfRange, "The value must be less than or equal to 1000.")]
    [InlineData(nameof(Consent), "false", null, "false")]
    [InlineData(nameof(Consent), "\"false\"", ValueObjectErrorCodes.NotParsable, "Consent cannot read a String.")]
    [InlineData(nameof(Latitude), "-90", null, "-90")]
    [InlineData(nameof(Latitude), "1e400", ValueObjectErrorCodes.NotParsable, "The number is not a valid double.")]
    [InlineData(nameof(Latitude), "\"1\"", ValueObjectErrorCodes.NotParsable, "Latitude cannot read a String.")]
    [InlineData(nameof(Amount), "0.01", null, "0.01")]
    [InlineData(nameof(LedgerBalance), "\"12\"", null, "\"12\"")]
    [InlineData(nameof(LedgerBalance), "12", ValueObjectErrorCodes.NotParsable, "LedgerBalance cannot read a Number.")]
    [InlineData(nameof(RecordedAt), "\"2026-10-13\"", null, "\"2026-10-13T00:00:00.0000000\"")]
    [InlineData(nameof(RecordedAt), "\"13/10/2026\"", ValueObjectErrorCodes.NotParsable, "The text is not a valid System.DateTime.")]
    [InlineData(nameof(Iban), "12", ValueObjectErrorCodes.NotParsable, "Iban cannot read a Number.")]
    [InlineData(nameof(Iban), "\"XX\"", ValueObjectErrorCodes.TooShort, "The value must be at least 15 characters long.")]
    public async Task A_variable_is_read_through_the_rules_whatever_its_JSON_kind(string type, string json, string? code, string expected)
    {
        var executor = await Echoes[type]();

        var result = await executor.ExecuteAsync($"query($v: {type}!) {{ echo(value: $v) }}", $$"""{"v":{{json}}}""");

        if (code is null)
        {
            result.Data().Should().Be($$"""{"echo":{{expected}}}""");
            return;
        }

        var error = result.Error();
        error.Code().Should().Be(code);
        error.Message().Should().Be(expected);
        error.Member("path").Should().Be("""["v"]""");
    }

    /// <summary>
    /// A <c>null</c> never reaches the scalar: HotChocolate refuses it for an argument or a variable that cannot be
    /// <c>null</c>, and hands it to the resolver of a nullable one, as an absent argument.
    /// </summary>
    [Fact]
    public async Task A_null_is_HotChocolates_to_refuse_or_to_hand_to_the_resolver()
    {
        var executor = await EchoAsync<Iban>();

        (await executor.ExecuteAsync("{ echo(value: null) }")).Error().Code().Should().BeNull();
        (await executor.ExecuteAsync("query($v: Iban!) { echo(value: $v) }", """{"v":null}""")).Error().Code().Should().Be("HC0018");
        (await executor.ExecuteAsync("{ optional(value: null) }")).Data().Should().Be("""{"optional":null}""");
        (await executor.ExecuteAsync("{ optional }")).Data().Should().Be("""{"optional":null}""");
        (await executor.ExecuteAsync("query($v: Iban) { optional(value: $v) }", """{"v":null}""")).Data().Should().Be("""{"optional":null}""");
        (await executor.ExecuteAsync("""{ optional(value: "FR7630006000011234567890189") }"""))
            .Data().Should().Be("""{"optional":"FR7630006000011234567890189"}""");
    }

    /// <summary>
    /// A list of value objects, or of their nullable type, as a list, a read-only list or an array, reads each element
    /// through the rules, as a literal or as a variable, and a refused element is answered at its index.
    /// </summary>
    [Fact]
    public async Task Each_element_of_a_list_is_read_through_the_rules()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<ShopQuery>().AddValueObjects());
        const string Valid = "FR7630006000011234567890189";

        (await executor.ExecuteAsync($$"""{ many(ibans: ["{{Valid}}", "de89 3704 0044 0532 0130 00"]) }"""))
            .Data().Should().Be($$"""{"many":["{{Valid}}","DE89370400440532013000"]}""");
        (await executor.ExecuteAsync($$"""{ maybeMany(ibans: ["{{Valid}}", null]) }""")).Data().Should().Be($$"""{"maybeMany":["{{Valid}}",null]}""");
        (await executor.ExecuteAsync($$"""{ maybeArray(ibans: [null, "{{Valid}}"]) }""")).Data().Should().Be($$"""{"maybeArray":[null,"{{Valid}}"]}""");
        (await executor.ExecuteAsync($$"""{ arrayOf(ibans: ["{{Valid}}"]) }""")).Data().Should().Be($$"""{"arrayOf":["{{Valid}}"]}""");
        (await executor.ExecuteAsync("query($l: [Iban]!) { maybeMany(ibans: $l) }", $$"""{"l":["{{Valid}}",null]}"""))
            .Data().Should().Be($$"""{"maybeMany":["{{Valid}}",null]}""");

        var literal = (await executor.ExecuteAsync($$"""{ many(ibans: ["{{Valid}}", "XX"]) }""")).Error();
        literal.Code().Should().Be(ValueObjectErrorCodes.TooShort);
        literal.Member("inputPath").Should().Be("""["ibans",1]""");

        var variable = (await executor.ExecuteAsync("query($l: [Iban!]!) { many(ibans: $l) }", $$"""{"l":["{{Valid}}","XX"]}""")).Error();
        variable.Code().Should().Be(ValueObjectErrorCodes.TooShort);
        variable.Member("path").Should().Be("""["l",1]""");

        var nullable = (await executor.ExecuteAsync("query($l: [Iban]!) { maybeArray(ibans: $l) }", """{"l":[null,"XX"]}""")).Error();
        nullable.Code().Should().Be(ValueObjectErrorCodes.TooShort);
        nullable.Member("path").Should().Be("""["l",1]""");
    }

    /// <summary>A field of an input object is read through the rules, and a refused one answered at its path.</summary>
    [Fact]
    public async Task A_field_of_an_input_object_is_read_through_the_rules()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<ShopQuery>().AddValueObjects());

        (await executor.ExecuteAsync("""{ transfer(input: { from: "fr76 3000 6000 0112 3456 7890 189", qty: 3 }) { from qty } }"""))
            .Data().Should().Be("""{"transfer":{"from":"FR7630006000011234567890189","qty":3}}""");

        var error = (await executor.ExecuteAsync("""{ transfer(input: { from: "FR7630006000011234567890189", qty: -1 }) { qty } }""")).Error();
        error.Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
        error.Member("inputPath").Should().Be("""["input","qty"]""");
        error.Member("coordinate").Should().Be("\"TransferInput.qty\"");
    }

    /// <summary>
    /// An uninitialized instance the type refuses is never written, nor a real that is not finite: the field's error keeps
    /// the rule's message and HotChocolate's own code. One the type accepts is written.
    /// </summary>
    [Fact]
    public async Task A_value_no_GraphQL_value_holds_is_never_written()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<OutputQuery>().AddValueObjects());

        var unset = (await executor.ExecuteAsync("{ unset }")).Error();
        unset.Message().Should().Be("The value must not be empty.");
        unset.Code().Should().Be("EXEC_INVALID_LEAF_VALUE");

        var infinite = (await executor.ExecuteAsync("{ infinite }")).Error();
        infinite.Message().Should().Be("A double that is not finite has no GraphQL Float.");
        infinite.Code().Should().Be("EXEC_INVALID_LEAF_VALUE");

        (await executor.ExecuteAsync("{ start }")).Data().Should().Be("""{"start":0}""");
        (await executor.ExecuteAsync("{ origin { x y } }")).Data().Should().Be("""{"origin":{"x":0,"y":0}}""", "a struct that is no value object stays an object type");
    }

    /// <summary>
    /// A value is written as its underlying value, the shortest text that reads back as a real, never through a
    /// formatting hook of the value object.
    /// </summary>
    [Fact]
    public async Task A_value_is_written_as_its_underlying_value_never_through_a_formatting_hook()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<OutputQuery>().AddValueObjects());

        (await executor.ExecuteAsync("{ floor celsius phone grouped large third }")).Data().Should().Be(
            """{"floor":3,"celsius":21,"phone":"+33123456789","grouped":"ABCDEFGH","large":1E-30,"third":0.1}""");
        (await executor.ExecuteAsync("""{ groupedEcho(g: "ABCD EFGH") }""")).Data().Should().Be("""{"groupedEcho":"ABCDEFGH"}""");
    }

    /// <summary>
    /// A value object written by hand, which nothing registered, is described by the registry where dynamic code runs:
    /// one over another type than the 22 is read as text through its <c>TryParse</c> and written as it formats itself, and
    /// one over an integer is read as an Int.
    /// </summary>
    [Fact]
    public async Task A_value_object_written_by_hand_is_read_through_its_rules()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<OutputQuery>().AddValueObjects());

        (await executor.ExecuteAsync("""{ link(l: "https://example.com/a") tally(t: 3) }""")).Data().Should().Be(
            """{"link":"https://example.com/a","tally":3}""");

        var link = (await executor.ExecuteAsync("""{ link(l: "relative/path") }""")).Error();
        link.Code().Should().Be(ValueObjectErrorCodes.InvalidFormat);
        link.Message().Should().Be("A link is an absolute URI.");

        var tally = (await executor.ExecuteAsync("{ tally(t: 0) }")).Error();
        tally.Code().Should().Be(ValueObjectErrorCodes.OutOfRange);
        tally.Message().Should().Be("A tally is at least 1.");
    }

    /// <summary>
    /// A value object written by hand whose <c>TryCreate</c> refuses without a reason is answered with
    /// <c>value_object.not_parsable</c>, naming the scalar, rather than with HotChocolate's unexpected error.
    /// </summary>
    [Fact]
    public async Task A_refusal_without_a_reason_names_the_scalar()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder
            .AddQueryType<EchoQuery<HandWrittenRating>>()
            .AddValueObjectScalar<HandWrittenRating, int>());

        (await executor.ExecuteAsync("{ echo(value: 4) }")).Data().Should().Be("""{"echo":4}""");

        var error = (await executor.ExecuteAsync("{ echo(value: 9) }")).Error();
        error.Code().Should().Be(ValueObjectErrorCodes.NotParsable);
        error.Message().Should().Be("The value is not a valid HandWrittenRating.");
    }

    /// <summary>
    /// A literal of another kind than the scalar's, which HotChocolate's validation keeps from it in a request, is refused
    /// with <c>value_object.not_parsable</c> when code hands it over, naming the kind as GraphQL names it; a
    /// <see langword="null"/> literal is an argument error.
    /// </summary>
    [Fact]
    public async Task The_scalar_refuses_a_literal_of_another_kind()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<ShopQuery>().AddValueObjects());
        var quantity = Scalar(executor, "Quantity");
        var iban = Scalar(executor, "Iban");
        var consent = Scalar(await EchoAsync<Consent>(), "Consent");
        var latitude = Scalar(await EchoAsync<Latitude>(), "Latitude");

        quantity.Invoking(scalar => scalar.CoerceInputLiteral(null!)).Should().Throw<ArgumentNullException>();
        Refusal(() => quantity.CoerceInputLiteral(new ObjectValueNode())).Should().Be("Quantity cannot read an Object.");
        Refusal(() => quantity.CoerceInputLiteral(new StringValueNode("3"))).Should().Be("Quantity cannot read a String.");
        Refusal(() => iban.CoerceInputLiteral(new EnumValueNode("FR"))).Should().Be("Iban cannot read an Enum.");
        Refusal(() => iban.CoerceInputLiteral(new ListValueNode())).Should().Be("Iban cannot read a List.");
        Refusal(() => iban.CoerceInputLiteral(new IntValueNode(3))).Should().Be("Iban cannot read an Int.");
        Refusal(() => consent.CoerceInputLiteral(new StringValueNode("true"))).Should().Be("Consent cannot read a String.");
        Refusal(() => latitude.CoerceInputLiteral(new BooleanValueNode(true))).Should().Be("Latitude cannot read a Boolean.");
        Refusal(() => quantity.CoerceInputValue(JsonElementOf("[1]"), null!)).Should().Be("Quantity cannot read a List.");
        Refusal(() => iban.CoerceInputValue(JsonElementOf("true"), null!)).Should().Be("Iban cannot read a Boolean.");
        Refusal(() => iban.CoerceInputValue(JsonElementOf("null"), null!)).Should().Be("Iban cannot read a Null.");
        quantity.CoerceInputLiteral(new IntValueNode(3)).Should().Be(Quantity.Create(3));
        iban.CoerceInputValue(JsonElementOf("\"FR7630006000011234567890189\""), null!).Should().Be(Iban.Create("FR7630006000011234567890189"));
    }

    /// <summary>
    /// A value is written as a literal, for a default value of the schema, as it is written into a result: no natural
    /// input reaches it, since a value object is no constant C# can hold as a default.
    /// </summary>
    [Fact]
    public async Task A_value_is_written_as_the_literal_of_its_underlying_value()
    {
        var executor = await GraphQLHarness.BuildAsync(static builder => builder
            .AddQueryType<EchoQuery<Consent>>()
            .AddType<ObjectType<EchoQuery<Adjustment>>>()
            .AddType<ObjectType<EchoQuery<ByteCount>>>()
            .AddType<ObjectType<EchoQuery<Latitude>>>()
            .AddType<ObjectType<EchoQuery<Ratio>>>()
            .AddType<ObjectType<EchoQuery<Amount>>>()
            .AddType<ObjectType<EchoQuery<UnboundedDecimal>>>()
            .AddType<ObjectType<EchoQuery<Tolerance>>>()
            .AddType<ObjectType<EchoQuery<Grade>>>()
            .AddType<ObjectType<EchoQuery<Duration>>>()
            .AddType<ObjectType<EchoQuery<Iban>>>()
            .AddType<ObjectType<EchoQuery<GroupedReference>>>()
            .AddType<ObjectType<EchoQuery<Declared<GraphQLLink, Uri>>>>()
            .AddValueObjects());

        Literal(executor, "Consent", Consent.Create(true)).Should().Be("true");
        Literal(executor, "Adjustment", Adjustment.Create(-3)).Should().Be("-3");
        Literal(executor, "ByteCount", ByteCount.Create(ulong.MaxValue)).Should().Be("18446744073709551615");
        Literal(executor, "Latitude", Latitude.Create(1e-30)).Should().Be("1E-30");
        ((FloatValueNode)Scalar(executor, "Latitude").ValueToLiteral(Latitude.Create(1e-30))).Format.Should().Be(FloatFormat.Exponential);
        ((FloatValueNode)Scalar(executor, "Latitude").ValueToLiteral(Latitude.Create(48.85))).Format.Should().Be(FloatFormat.FixedPoint);
        Literal(executor, "Ratio", Ratio.Create(0.1f)).Should().Be("0.1");
        Literal(executor, "Amount", Amount.Create(12.5m)).Should().Be("12.50");
        Literal(executor, "UnboundedDecimal", UnboundedDecimal.Create(decimal.MinValue)).Should().Be("-79228162514264337593543950335");
        Literal(executor, "UnboundedDecimal", UnboundedDecimal.Create(-0.0000000000000000000000000001m))
            .Should().Be("-0.0000000000000000000000000001", "the longest text of a decimal, 31 characters");
        Literal(executor, "Grade", Grade.Create('b')).Should().Be("\"B\"");
        Literal(executor, "Duration", Duration.Create(new TimeSpan(1, 30, 0))).Should().Be("\"01:30:00\"");
        Literal(executor, "Iban", Iban.Create("FR7630006000011234567890189")).Should().Be("\"FR7630006000011234567890189\"");
        Literal(executor, "GroupedReference", GroupedReference.Create("ABCDEFGH")).Should().Be("\"ABCDEFGH\"", "never through the formatting hook");
        Literal(executor, "DeclaredOfGraphQLLinkAndUri", Declared<GraphQLLink, Uri>.Create(new Uri("https://example.com/a")))
            .Should().Be("\"https://example.com/a\"");

        Refusal(() => Scalar(executor, "Tolerance").ValueToLiteral(Tolerance.Create(double.NegativeInfinity)))
            .Should().Be("A double that is not finite has no GraphQL Float.");
#pragma warning disable VO0010 // The uninitialized instance is what the scalar refuses to write.
        Refusal(() => Scalar(executor, "Iban").ValueToLiteral(default(Iban))).Should().Be("The value must not be empty.");
#pragma warning restore VO0010
        Scalar(executor, "Iban").Invoking(scalar => scalar.ValueToLiteral("FR7630006000011234567890189"))
            .Should().Throw<LeafCoercionException>().WithMessage("Iban cannot write a String.");
        Scalar(executor, "Adjustment").Invoking(scalar => scalar.ValueToLiteral(3))
            .Should().Throw<LeafCoercionException>().WithMessage("Adjustment cannot write an Int32.");
    }

    public static TheoryData<string> Every => GeneratedSurface.Samples.Names;

    /// <summary>
    /// Every underlying type, option and hook of the domain, entity identifiers included, is a scalar of its own, read
    /// through its rules.
    /// </summary>
    [Theory]
    [MemberData(nameof(Every))]
    public async Task Every_value_object_is_a_GraphQL_scalar_read_through_its_rules(string type)
        => await GeneratedSurface.Samples.All[type].AnswersARefusedGraphQLValueWithItsRuleAsync();

    /// <summary>Gets the scalar a schema names.</summary>
    internal static ScalarType Scalar(IRequestExecutor executor, string name)
        => executor.Schema.Types.OfType<ScalarType>().Should().ContainSingle(type => type.Name == name).Subject;

    private static Task<IRequestExecutor> EchoAsync<TSelf>()
        where TSelf : struct
        => GraphQLHarness.BuildAsync(static builder => builder.AddQueryType<EchoQuery<TSelf>>().AddValueObjects());

    private static string Literal(IRequestExecutor executor, string scalar, object value)
        => Scalar(executor, scalar).ValueToLiteral(value).ToString();

    private static string Refusal(Action coercion)
    {
        var exception = coercion.Should().Throw<LeafCoercionException>().Which;
        ValueObjectErrors.TryGetCode(exception, out var code).Should().BeTrue();
        exception.Errors.Should().ContainSingle().Which.Code.Should().Be(code);
        return exception.Errors[0].Message;
    }

    private static JsonElement JsonElementOf(string json) => JsonDocument.Parse(json).RootElement;
}
