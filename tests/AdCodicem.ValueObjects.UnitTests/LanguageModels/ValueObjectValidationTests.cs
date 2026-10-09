using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.AI;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Microsoft.Extensions.AI;
using static AdCodicem.ValueObjects.UnitTests.LanguageModels.LanguageModelTools;

namespace AdCodicem.ValueObjects.UnitTests.LanguageModels;

/// <summary>
/// <c>WithValueObjectValidation()</c>: an argument a value object refuses is answered with the rule, as a result the model
/// reads, rather than an exception it never sees; an argument the value objects accept reaches the function.
/// </summary>
public sealed class ValueObjectValidationTests
{
    private const string Required = "A value is required.";

    /// <summary>
    /// Each argument a rule refuses is answered with that rule's code and the converter's message, which names the value
    /// object and the rule, never the value sent; a token the underlying type cannot hold, whose message the converter
    /// leaves to System.Text.Json, with a message naming the value object alone.
    /// </summary>
    [Theory]
    [InlineData("iban", "\"FR76\"", ValueObjectErrorCodes.TooShort)]
    [InlineData("iban", "\"FR763000600001123456789018900000000000\"", ValueObjectErrorCodes.TooLong)]
    [InlineData("iban", "\"FR7630006000011234567890188\"", ValueObjectErrorCodes.InvalidFormat)]
    [InlineData("iban", "42", ValueObjectErrorCodes.NotParsable)]
    [InlineData("quantity", "1001", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("quantity", "true", ValueObjectErrorCodes.NotParsable)]
    [InlineData("customer", "\"not-a-guid\"", ValueObjectErrorCodes.NotParsable)]
    [InlineData("customer", "\"00000000-0000-0000-0000-000000000000\"", ValueObjectErrorCodes.Required)]
    [InlineData("country", "\"XX\"", ValueObjectErrorCodes.NotAKnownValue)]
    [InlineData("effective", "\"1999-12-31\"", ValueObjectErrorCodes.OutOfRange)]
    public async Task A_refused_argument_is_answered_with_its_rule_and_never_with_the_value(string argument, string json, string code)
    {
        var refusal = argument switch
        {
            "iban" => RefusalOf<Iban>(json),
            "quantity" => RefusalOf<Quantity>(json),
            "customer" => RefusalOf<CustomerId>(json),
            "country" => RefusalOf<CountryCode>(json),
            _ => RefusalOf<EffectiveDate>(json),
        };
        refusal.ErrorCode.Should().Be(code);

        var result = await InvokeAsync(Validated(PlaceOrder), Order(argument, json));

        result.Should().Be(Rejection(argument, code, AnsweredMessage(refusal)));
        result.Should().NotContain(json.Trim('"'), "a refusal never repeats the value sent");
    }

    /// <summary>
    /// The messages, as the model reads them.
    /// </summary>
    [Fact]
    public async Task The_message_names_the_value_object_and_the_rule()
    {
        var function = Validated(PlaceOrder);

        (await InvokeAsync(function, Order("quantity", "1001"))).Should().Be(
            """{"error":"invalid_argument","argument":"quantity","code":"value_object.out_of_range","message":"The value is not a valid Quantity: The value must be less than or equal to 1000."}""");
        (await InvokeAsync(function, Order("iban", "\"FR76\""))).Should().Be(
            """{"error":"invalid_argument","argument":"iban","code":"value_object.too_short","message":"The value is not a valid Iban: The value must be at least 15 characters long."}""");
        (await InvokeAsync(function, Order("country", "\"XX\""))).Should().Be(
            """{"error":"invalid_argument","argument":"country","code":"value_object.not_a_known_value","message":"The value is not a valid CountryCode: The value is not one of the accepted values."}""");
    }

    /// <summary>
    /// The binding hands a value object that cannot be <see langword="null"/> its uninitialized instance for a
    /// <see langword="null"/>, and fails an absent argument with an exception the model never sees: both are answered as
    /// required.
    /// </summary>
    [Fact]
    public async Task A_null_or_an_absent_argument_for_a_value_object_that_cannot_be_null_is_required()
    {
        (await InvokeAsync(AIFunctionFactory.Create(Count), Arguments("""{"quantity":null}""")))
            .Should().Be("\"0 default=True\"", "without the wrapper, the function receives an uninitialized instance");
        await FluentActions.Awaiting(async () => await AIFunctionFactory.Create(Count).InvokeAsync(Arguments("{}"), TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>();

        (await InvokeAsync(Validated(Count), Arguments("""{"quantity":null}"""))).Should().Be(Rejection("quantity", ValueObjectErrorCodes.Required, Required));
        (await InvokeAsync(Validated(Count), Arguments("{}"))).Should().Be(Rejection("quantity", ValueObjectErrorCodes.Required, Required));
        (await InvokeAsync(Validated(PlaceOrder), Order("country", null))).Should().Be(Rejection("country", ValueObjectErrorCodes.Required, Required));

        // A host that hands over the JSON null itself has it read by the converter, which refuses it as required too.
        (await InvokeAsync(Validated(Count), new AIFunctionArguments { ["quantity"] = JsonDocument.Parse("null").RootElement }))
            .Should().Be(Rejection("quantity", ValueObjectErrorCodes.Required, RefusalOf<Quantity>("null").Message));
    }

    /// <summary>
    /// A nullable value object takes a <see langword="null"/>, and an absent argument the binding fills in passes, whether
    /// its default is written in C#, declared by an attribute, or the parameter is optional; one it cannot fill is
    /// required, nullable or not.
    /// </summary>
    [Fact]
    public async Task An_argument_the_binding_fills_in_or_a_null_for_a_nullable_value_object_passes()
    {
        var order = Validated(PlaceOrder);
        (await InvokeAsync(order, Order("effective", "null"))).Should().Contain("from today");
        (await InvokeAsync(order, Order("effective", null))).Should().Contain("from today");
        (await InvokeAsync(order, Order("effective", "\"2024-01-31\""))).Should().Contain("from 2024-01-31");

        var defaults = Validated(Defaults);
        (await InvokeAsync(defaults, Arguments("""{"required":7}"""))).Should().Be("\"- - 7 -\"");
        (await InvokeAsync(defaults, Arguments("""{"declared":1,"omitted":2,"required":null,"optional":4}"""))).Should().Be("\"1 2 - 4\"");
        (await InvokeAsync(defaults, Arguments("{}"))).Should().Be(Rejection("required", ValueObjectErrorCodes.Required, Required));
    }

    /// <summary>
    /// Arguments every rule accepts reach the function, whose result comes back as it is.
    /// </summary>
    [Fact]
    public async Task Accepted_arguments_reach_the_function()
    {
        var expected = await InvokeAsync(AIFunctionFactory.Create(PlaceOrder), Arguments(ValidOrder));

        (await InvokeAsync(Validated(PlaceOrder), Arguments(ValidOrder))).Should().Be(expected)
            .And.Be("\"placed 3 on FR7630006000011234567890189 for 6f9619ff-8b86-d011-b42d-00c04fc964ff in FR, from today\"");
        (await InvokeAsync(Validated(PlaceOrder), Order("country", "\" fr \""))).Should().Contain(" in FR,", "the value object normalizes it");
    }

    /// <summary>
    /// Validation fails fast, as everywhere in the library: the first argument refused, in the order of the parameters,
    /// is the one answered.
    /// </summary>
    [Fact]
    public async Task The_first_argument_refused_in_the_order_of_the_parameters_is_answered()
    {
        var both = """{"iban":"FR76","quantity":1001}""";

        (await InvokeAsync(Validated(PlaceOrder), Arguments(both)))
            .Should().Contain("\"argument\":\"iban\"");
        (await InvokeAsync(Validated(Swap), Arguments(both)))
            .Should().Contain("\"argument\":\"quantity\"");
    }

    /// <summary>
    /// The argument is read as the binding reads it, through the contract of the parameter, which runs the value object's
    /// converter: it is refused exactly when binding it would throw, whatever the options let a number be written as.
    /// </summary>
    [Theory]
    [InlineData("web", "quantity", "7")]
    [InlineData("web", "quantity", "\"7\"")]
    [InlineData("general", "quantity", "7")]
    [InlineData("general", "quantity", "\"7\"")]
    [InlineData("web", "balance", "500")]
    [InlineData("web", "balance", "\"500\"")]
    [InlineData("general", "balance", "500")]
    [InlineData("general", "balance", "\"500\"")]
    [InlineData("general", "balance", "\"1000000000000000000001\"")]
    public async Task An_argument_is_refused_exactly_when_binding_it_would_throw(string defaults, string argument, string json)
    {
        var options = new JsonSerializerOptions(defaults == "web" ? JsonSerializerDefaults.Web : JsonSerializerDefaults.General)
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        };
        Delegate method = argument == "quantity" ? Count : Settle;
        var arguments = Arguments($$"""{"{{argument}}":{{json}}}""");

        var refusedByBinding = await Throws(AIFunctionFactory.Create(method, new AIFunctionFactoryOptions { SerializerOptions = options }), arguments);
        var result = await InvokeAsync(Validated(method, options), arguments);

        IsRejection(result).Should().Be(refusedByBinding, "{0} under the {1} defaults gave {2}", json, defaults, result);
    }

    /// <summary>
    /// A <see cref="JsonElement"/>, a <see cref="JsonNode"/> and a <see cref="JsonDocument"/> are each read; an instance of
    /// the parameter's type, a default one included, is left to the binding, which takes it as it is.
    /// </summary>
    [Fact]
    public async Task Each_shape_of_JSON_is_read_and_an_instance_is_left_to_the_binding()
    {
        var count = Validated(Count);
        var refused = Rejection("quantity", ValueObjectErrorCodes.OutOfRange, RefusalOf<Quantity>("1001").Message);

        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = JsonDocument.Parse("1001").RootElement })).Should().Be(refused);
        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = JsonNode.Parse("1001") })).Should().Be(refused);
        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = JsonDocument.Parse("1001") })).Should().Be(refused);
        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = JsonNode.Parse("5") })).Should().Be("\"5 default=False\"");
        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = JsonDocument.Parse("5") })).Should().Be("\"5 default=False\"");

        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = Quantity.Create(5) })).Should().Be("\"5 default=False\"");
#pragma warning disable VO0010 // An application handing the function the default instance itself, which the binding takes as it is.
        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = default(Quantity) })).Should().Be("\"0 default=True\"");
        (await InvokeAsync(Validated((Iban iban) => "taken"), new AIFunctionArguments { ["iban"] = default(Iban) }))
            .Should().Be("\"taken\"", "an instance is never written, which its converter would refuse for a default its rules reject");
#pragma warning restore VO0010
    }

    /// <summary>
    /// An argument handed over as text or as a number, which the binding converts rather than refuses, is read as the
    /// binding converts it: text that may be JSON as JSON first, then the value written through the contract of its own
    /// type and read back. It is answered with what reading the text as JSON told, unless that told only that the value was
    /// of another kind; a collection or an object handed over as its JSON is answered for a value object it holds.
    /// </summary>
    [Fact]
    public async Task An_argument_handed_over_as_text_or_as_a_number_is_read_as_the_binding_converts_it()
    {
        var count = Validated(Count);
        var outOfRange = Rejection("quantity", ValueObjectErrorCodes.OutOfRange, RefusalOf<Quantity>("1001").Message);

        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = "1001" })).Should().Be(outOfRange);
        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = 1001 })).Should().Be(outOfRange);
        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = "abc" }))
            .Should().Be(Rejection("quantity", ValueObjectErrorCodes.NotParsable, RefusalOf<Quantity>("\"abc\"").Message));
        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = "7" })).Should().Be("\"7 default=False\"");
        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = 7L })).Should().Be("\"7 default=False\"");

        // Where a number cannot be written as text, the same text read as a string tells only that it is not a number.
        var general = new JsonSerializerOptions(JsonSerializerDefaults.General) { TypeInfoResolver = AIJsonUtilities.DefaultOptions.TypeInfoResolver };
        RefusalOf<Quantity>("\"1001\"", general).ErrorCode.Should().Be(ValueObjectErrorCodes.NotParsable);
        (await InvokeAsync(Validated(Count, general), new AIFunctionArguments { ["quantity"] = "1001" })).Should().Be(outOfRange);

        // Text that looks like a number is read as a string once it is not one: an IBAN too short, not a number.
        var order = Validated(PlaceOrder);
        (await InvokeAsync(order, AsText(Order("iban", "\"76\""))))
            .Should().Be(Rejection("iban", ValueObjectErrorCodes.TooShort, RefusalOf<Iban>("\"76\"").Message));
        (await InvokeAsync(order, AsText(Order("country", "\"XX\""))))
            .Should().Be(Rejection("country", ValueObjectErrorCodes.NotAKnownValue, RefusalOf<CountryCode>("\"XX\"").Message));
        (await InvokeAsync(order, AsText(Arguments(ValidOrder)))).Should().Be(await InvokeAsync(order, Arguments(ValidOrder)));

        var book = Validated(Book);
        const string Line = """{"account":"FR7630006000011234567890189","quantity":3,"count":1}""";
        (await InvokeAsync(book, AsText(Arguments($$"""{"accounts":["FR76"],"perCountry":{},"line":{{Line}}}"""))))
            .Should().Be(Rejection("accounts", ValueObjectErrorCodes.TooShort, RefusalOf<Iban>("\"FR76\"").Message));
        (await InvokeAsync(book, AsText(Arguments($$"""{"accounts":[42],"perCountry":{},"line":{{Line}}}"""))))
            .Should().Be(Rejection("accounts", ValueObjectErrorCodes.NotParsable, RefusalOf<Iban>("42").Message));
        (await InvokeAsync(book, AsText(Arguments("""{"accounts":[],"perCountry":{},"line":{"account":"FR76","quantity":3,"count":1}}"""))))
            .Should().Be(Rejection("line", ValueObjectErrorCodes.TooShort, RefusalOf<Iban>("\"FR76\"").Message));
        (await InvokeAsync(book, AsText(Arguments($$"""{"accounts":[],"perCountry":{"FR":1},"line":{{Line}}}"""))))
            .Should().Be("\"0 1 FR7630006000011234567890189\"");
        await FluentActions.Awaiting(async () => await book.InvokeAsync(
                new AIFunctionArguments { ["accounts"] = 5, ["perCountry"] = "{}", ["line"] = Line },
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>("a value that no reading turns into a list is the binding's to refuse");
    }

    /// <summary>
    /// An argument handed over as text or as a number is refused exactly when binding it fails, whatever the options let
    /// a number be written as.
    /// </summary>
    [Theory]
    [InlineData("web", "quantity", "7")]
    [InlineData("web", "quantity", " 7")]
    [InlineData("web", "quantity", "\"7\"")]
    [InlineData("web", "quantity", "1001")]
    [InlineData("web", "quantity", "abc")]
    [InlineData("web", "quantity", 7)]
    [InlineData("web", "quantity", 1001)]
    [InlineData("web", "quantity", 7.5)]
    [InlineData("web", "quantity", true)]
    [InlineData("general", "quantity", "7")]
    [InlineData("general", "quantity", " 7")]
    [InlineData("general", "quantity", "\"7\"")]
    [InlineData("general", "quantity", "1001")]
    [InlineData("general", "quantity", "abc")]
    [InlineData("general", "quantity", 7)]
    [InlineData("general", "quantity", 1001)]
    [InlineData("web", "balance", "500")]
    [InlineData("web", "balance", 500)]
    [InlineData("general", "balance", "500")]
    [InlineData("general", "balance", "\"500\"")]
    [InlineData("general", "balance", 500)]
    [InlineData("general", "balance", "1000000000000000000001")]
    public async Task An_argument_handed_over_as_text_or_a_number_is_refused_exactly_when_binding_it_fails(string defaults, string argument, object value)
    {
        var options = new JsonSerializerOptions(defaults == "web" ? JsonSerializerDefaults.Web : JsonSerializerDefaults.General)
        {
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
        };
        Delegate method = argument == "quantity" ? Count : Settle;

        var refusedByBinding = await Fails(AIFunctionFactory.Create(method, new AIFunctionFactoryOptions { SerializerOptions = options }), new() { [argument] = value });
        var result = await InvokeAsync(Validated(method, options), new() { [argument] = value });

        IsRejection(result).Should().Be(refusedByBinding, "{0} under the {1} defaults gave {2}", value, defaults, result);
    }

    /// <summary>
    /// An argument whose own type the function's options have no contract for is read as JSON alone: refused for the rule
    /// its text breaks, and otherwise left to the binding, which cannot write it either.
    /// </summary>
    [Fact]
    public async Task An_argument_of_a_type_the_options_cannot_write_is_read_as_JSON_alone()
    {
        var options = QuantityOnlyContext.Default.Options;
        options.TryGetTypeInfo(typeof(string), out _).Should().BeFalse();
        options.TryGetTypeInfo(typeof(int), out _).Should().BeFalse();
        var function = new HandWrittenFunction(typeof(LanguageModelTools).GetMethod(nameof(Count)), """{"type":"object","properties":{"quantity":{}}}""")
        {
            Options = options,
        }.WithValueObjectValidation();

        (await InvokeAsync(function, new AIFunctionArguments { ["quantity"] = "1001" }))
            .Should().Be(Rejection("quantity", ValueObjectErrorCodes.OutOfRange, RefusalOf<Quantity>("1001").Message));
        (await InvokeAsync(function, new AIFunctionArguments { ["quantity"] = "7" })).Should().Be("\"called\"");
        (await InvokeAsync(function, new AIFunctionArguments { ["quantity"] = "abc" })).Should().Be("\"called\"");
        (await InvokeAsync(function, new AIFunctionArguments { ["quantity"] = 1001 })).Should().Be("\"called\"");
    }

    /// <summary>
    /// A collection, a dictionary or an object is read through its contract too, and answered under the parameter's name
    /// for a value object it holds; any other error is left to the function, which throws it as it would without the
    /// wrapper, and an absent or null one is the function's to refuse.
    /// </summary>
    [Fact]
    public async Task A_value_object_held_by_a_collection_a_dictionary_or_an_object_is_answered_under_the_parameter()
    {
        var book = Validated(Book);
        const string Line = """{"account":"FR7630006000011234567890189","quantity":3,"count":1}""";

        (await InvokeAsync(book, Arguments($$"""{"accounts":["FR7630006000011234567890189","FR76"],"perCountry":{},"line":{{Line}}}""")))
            .Should().Be(Rejection("accounts", ValueObjectErrorCodes.TooShort, RefusalOf<Iban>("\"FR76\"").Message));
        (await InvokeAsync(book, Arguments($$"""{"accounts":[],"perCountry":{"FR":1001},"line":{{Line}}}""")))
            .Should().Be(Rejection("perCountry", ValueObjectErrorCodes.OutOfRange, RefusalOf<Quantity>("1001").Message));
        (await InvokeAsync(book, Arguments($$"""{"accounts":[],"perCountry":{"XX":1},"line":{{Line}}}""")))
            .Should().Contain("\"argument\":\"perCountry\",\"code\":\"value_object.not_a_known_value\"");
        (await InvokeAsync(book, Arguments("""{"accounts":[],"perCountry":{},"line":{"account":"FR76","quantity":3,"count":1}}""")))
            .Should().Be(Rejection("line", ValueObjectErrorCodes.TooShort, RefusalOf<Iban>("\"FR76\"").Message));
        (await InvokeAsync(book, Arguments($$"""{"accounts":["FR7630006000011234567890189"],"perCountry":{"BE":2},"line":{{Line}}}""")))
            .Should().Be("\"1 1 FR7630006000011234567890189\"");

        await FluentActions.Awaiting(async () => await book.InvokeAsync(
                Arguments("""{"accounts":[],"perCountry":{},"line":{"account":"FR7630006000011234567890189","quantity":3,"count":"many"}}"""),
                TestContext.Current.CancellationToken))
            .Should().ThrowAsync<JsonException>().Where(exception => !(exception.GetType() == typeof(ValueObjectJsonException)));
        await FluentActions.Awaiting(async () => await book.InvokeAsync(Arguments("""{"accounts":[],"perCountry":{}}"""), TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// A converter of the application's own, which takes precedence over the value object's, may refuse a value without
    /// a code: the refusal is then not parsable, with a message naming the type alone; a code it does store is kept.
    /// </summary>
    [Fact]
    public async Task A_refusal_of_a_converter_of_the_application_s_own_is_not_parsable_unless_it_carries_a_code()
    {
        var plain = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions) { Converters = { new RefusingConverter(code: null) } };
        var coded = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions) { Converters = { new RefusingConverter("orders.too_many") } };

        (await InvokeAsync(Validated(Count, plain), Arguments("""{"quantity":3}""")))
            .Should().Be(Rejection("quantity", ValueObjectErrorCodes.NotParsable, "The value is not a valid Quantity."));
        (await InvokeAsync(Validated(Count, coded), Arguments("""{"quantity":3}""")))
            .Should().Be(Rejection("quantity", "orders.too_many", "The value is not a valid Quantity."));
        (await InvokeAsync(Validated((Quantity? quantity) => $"{quantity}", plain), Arguments("""{"quantity":3}""")))
            .Should().Be(Rejection("quantity", ValueObjectErrorCodes.NotParsable, "The value is not a valid Quantity."), "a nullable value object is named as its type");
    }

    /// <summary>
    /// A converter of the application's own that runs the value object's and wraps its refusal is answered with the rule
    /// it wraps, alone and in a list; a number the underlying type cannot hold, whose refusal the value object's converter
    /// leaves without a message, is answered with a message naming the value object alone.
    /// </summary>
    [Fact]
    public async Task A_refusal_a_converter_of_the_application_s_own_wraps_is_answered_with_the_rule_it_wraps()
    {
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions) { Converters = { new WrappingConverter() } };
        var count = Validated(Count, options);
        var lines = Validated((List<Quantity> quantities) => $"{quantities.Count}", options);
        var outOfRange = RefusalOf<Quantity>("1001").Message;
        const string NotValid = "The value is not a valid Quantity.";

        (await InvokeAsync(count, Arguments("""{"quantity":1001}""")))
            .Should().Be(Rejection("quantity", ValueObjectErrorCodes.OutOfRange, outOfRange));
        (await InvokeAsync(lines, Arguments("""{"quantities":[3,1001]}""")))
            .Should().Be(Rejection("quantities", ValueObjectErrorCodes.OutOfRange, outOfRange));
        (await InvokeAsync(count, Arguments("""{"quantity":99999}""")))
            .Should().Be(Rejection("quantity", ValueObjectErrorCodes.NotParsable, NotValid));
        (await InvokeAsync(lines, Arguments("""{"quantities":[99999]}""")))
            .Should().Be(Rejection("quantities", ValueObjectErrorCodes.NotParsable, NotValid));
        (await InvokeAsync(lines, Arguments("""{"quantities":[3]}"""))).Should().Be("\"1\"");
    }

    /// <summary>
    /// A number the underlying type cannot hold leaves the converter's message to System.Text.Json, which writes the path
    /// of the value, a dictionary's keys included: the answer names the value object alone, and never repeats a key the
    /// model sent.
    /// </summary>
    [Fact]
    public async Task A_number_the_underlying_type_cannot_hold_is_answered_without_the_path_of_the_value()
    {
        const string NotValid = "The value is not a valid Quantity.";
        const string Account = "FR7630006000011234567890189";
        const string Customer = "ada@example.com";
        RefusalOf<Quantity>("99999").Message.Should().Contain("Path: $", "System.Text.Json writes the message");
        RefusalOf<Dictionary<string, Quantity>>($$$"""{"{{{Customer}}}":99999}""").Message.Should().Contain(Customer, "the path holds the key");

        (await InvokeAsync(Validated(Count), Arguments("""{"quantity":99999}""")))
            .Should().Be(Rejection("quantity", ValueObjectErrorCodes.NotParsable, NotValid));
        (await InvokeAsync(Validated(Count), Arguments("""{"quantity":3.5}""")))
            .Should().Be(Rejection("quantity", ValueObjectErrorCodes.NotParsable, NotValid));

        var allot = Validated(Allot);
        (await InvokeAsync(allot, Arguments($$$"""{"perAccount":{"{{{Account}}}":99999},"perCustomer":{}}""")))
            .Should().Be(Rejection("perAccount", ValueObjectErrorCodes.NotParsable, NotValid));
        (await InvokeAsync(allot, Arguments($$$"""{"perAccount":{},"perCustomer":{"{{{Customer}}}":99999}}""")))
            .Should().Be(Rejection("perCustomer", ValueObjectErrorCodes.NotParsable, NotValid));

        // A message the converter writes, a rule's or a key's, holds no key either, and is kept.
        (await InvokeAsync(allot, Arguments($$$"""{"perAccount":{"{{{Account}}}":1001},"perCustomer":{}}""")))
            .Should().Be(Rejection("perAccount", ValueObjectErrorCodes.OutOfRange, RefusalOf<Quantity>("1001").Message));
        (await InvokeAsync(allot, Arguments("""{"perAccount":{"FR76":1},"perCustomer":{}}""")))
            .Should().Be(Rejection("perAccount", ValueObjectErrorCodes.TooShort, RefusalOf<Dictionary<Iban, Quantity>>("""{"FR76":1}""").Message))
            .And.NotContain("FR76\"");
        (await InvokeAsync(allot, Arguments($$$"""{"perAccount":{"{{{Account}}}":1},"perCustomer":{"{{{Customer}}}":2}}""")))
            .Should().Be("\"1 1\"");
    }

    /// <summary>
    /// A value object written by hand, which the converter factory's general-purpose converter reads, is answered with its
    /// rule; a token the underlying type cannot hold carries no code there, and is not parsable.
    /// </summary>
    [Fact]
    public async Task A_value_object_written_by_hand_is_answered_too()
    {
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions).AddValueObjects();
        var code = Validated(Code, options);

        (await InvokeAsync(code, Arguments("""{"code":"abc1"}""")))
            .Should().Be(Rejection("code", ValueObjectErrorCodes.InvalidFormat, "The value is not a valid HandWrittenCode: A code holds ASCII letters only."));
        (await InvokeAsync(code, Arguments("""{"code":42}""")))
            .Should().Be(Rejection("code", ValueObjectErrorCodes.NotParsable, "The value is not a valid HandWrittenCode."));
        (await InvokeAsync(code, Arguments("""{"code":"abc"}"""))).Should().Be("\"ABC\"");
    }

    /// <summary>
    /// A value object is told by its type, not by a registration or by its converter: one written by hand that nothing
    /// registered, read by a converter of its own, is still required, and telling it registers nothing.
    /// </summary>
    [Fact]
    public async Task A_value_object_nothing_registered_is_told_from_its_type()
    {
        var function = AIFunctionFactory.Create((Declared<Unregistered, string> code) => code.Value).WithValueObjectValidation();

        (await InvokeAsync(function, Arguments("{}"))).Should().Be(Rejection("code", ValueObjectErrorCodes.Required, Required));
        (await InvokeAsync(function, Arguments("""{"code":"abc"}"""))).Should().Be("\"abc\"");
        ValueObjectRegistry.TryGet(typeof(Declared<Unregistered, string>), out _).Should().BeFalse("telling a value object registers nothing");
    }

    /// <summary>
    /// A parameter renamed by <c>AIParameterNameAttribute</c> is answered under the name the model sends it under.
    /// </summary>
    [Fact]
    public async Task A_renamed_parameter_is_answered_under_its_name_in_the_schema()
    {
        var ship = Validated(Ship);

        ToolSchemaTests.Parse(ship.JsonSchema)["required"]!.AsArray().Select(static name => name!.GetValue<string>()).Should().Equal("qty");
        (await InvokeAsync(ship, Arguments("""{"qty":1001}"""))).Should().Contain("\"argument\":\"qty\",\"code\":\"value_object.out_of_range\"");
        (await InvokeAsync(ship, Arguments("{}"))).Should().Be(Rejection("qty", ValueObjectErrorCodes.Required, Required));
        (await InvokeAsync(ship, Arguments("""{"qty":4}"""))).Should().Be("\"shipped 4\"");
    }

    /// <summary>
    /// A parameter the schema leaves out, a cancellation token, the arguments, a service, or one a callback binds, is bound
    /// from no argument, and never required.
    /// </summary>
    [Fact]
    public async Task A_parameter_the_schema_leaves_out_is_not_checked()
    {
        (await InvokeAsync(Validated(Hosted), Arguments("""{"quantity":2}"""))).Should().Be("\"2 1 True\"");
        (await InvokeAsync(Validated(Hosted), Arguments("""{"quantity":1001}"""))).Should().Contain("value_object.out_of_range");

        var bound = AIFunctionFactory.Create(
            Swap,
            new AIFunctionFactoryOptions
            {
                JsonSchemaCreateOptions = Rules,
                ConfigureParameterBinding = static parameter => parameter.Name == "iban"
                    ? new AIFunctionFactoryOptions.ParameterBindingOptions { BindParameter = static (_, _) => Iban.Example, ExcludeFromSchema = true }
                    : default,
            }).WithValueObjectValidation();
        (await InvokeAsync(bound, Arguments("""{"quantity":2}"""))).Should().Be("\"2 on FR7630006000011234567890189\"");

        var excluded = AIFunctionFactory.Create(
            Defaults,
            new AIFunctionFactoryOptions { JsonSchemaCreateOptions = Rules with { IncludeParameter = static parameter => parameter.Name != "required" } })
            .WithValueObjectValidation();
        (await InvokeAsync(excluded, Arguments("""{"required":9}"""))).Should().Be("\"- - 9 -\"", "the binding still reads an argument the schema leaves out");
    }

    /// <summary>
    /// A function no method backs, one whose schema names no parameter, one whose parameters have no name, and one whose
    /// parameters hold no value object are called as they are.
    /// </summary>
    [Fact]
    public async Task A_function_whose_parameters_cannot_be_read_or_hold_no_value_object_is_called_as_it_is()
    {
        var count = typeof(LanguageModelTools).GetMethod(nameof(Count))!;
        var unnamed = new System.Reflection.Emit.DynamicMethod("unnamed", typeof(void), [typeof(Quantity)]);
        var arguments = Arguments("""{"quantity":1001}""");

        foreach (var function in new AIFunction[]
                 {
                     new HandWrittenFunction(null, """{"type":"object","properties":{"quantity":{}}}"""),
                     new HandWrittenFunction(count, "true"),
                     new HandWrittenFunction(count, """{"type":"object"}"""),
                     new HandWrittenFunction(count, """{"type":"object","properties":true}"""),
                     new HandWrittenFunction(count, """{"type":"object","properties":{"other":{}}}"""),
                     new HandWrittenFunction(unnamed, """{"type":"object","properties":{"quantity":{}}}"""),
                     new HandWrittenFunction(count, """{"type":"object","properties":{"quantity":{}}}""") { Options = LanguageModelContextWithoutQuantity.Default.Options },
                 })
        {
            (await InvokeAsync(function.WithValueObjectValidation(), arguments)).Should().Be("\"called\"");
        }

        (await InvokeAsync(new HandWrittenFunction(count, """{"type":"object","properties":{"quantity":{}}}""").WithValueObjectValidation(), arguments))
            .Should().Contain("value_object.out_of_range", "a function written by hand is checked as any other");
        (await InvokeAsync(Validated(Note), Arguments("""{"text":"x","count":1,"raw":{}}"""))).Should().Be("\"x 1 Object\"");
    }

    /// <summary>
    /// Wrapping twice wraps once, a null function is refused, and the wrapper shows the model the function it wraps.
    /// </summary>
    [Fact]
    public void The_wrapper_shows_the_function_it_wraps_and_wraps_it_once()
    {
        var inner = AIFunctionFactory.Create(PlaceOrder, new AIFunctionFactoryOptions { JsonSchemaCreateOptions = Rules, AdditionalProperties = new Dictionary<string, object?> { ["strict"] = true } });
        var wrapped = inner.WithValueObjectValidation();

        wrapped.WithValueObjectValidation().Should().BeSameAs(wrapped);
        wrapped.Should().NotBeSameAs(inner);
        wrapped.Name.Should().Be(inner.Name).And.Be("PlaceOrder");
        wrapped.Description.Should().Be(inner.Description).And.Be("Places an order.");
        wrapped.JsonSchema.GetRawText().Should().Be(inner.JsonSchema.GetRawText());
        wrapped.UnderlyingMethod.Should().BeSameAs(inner.UnderlyingMethod);
        wrapped.JsonSerializerOptions.Should().BeSameAs(inner.JsonSerializerOptions);
        wrapped.AdditionalProperties.Should().BeSameAs(inner.AdditionalProperties);
        FluentActions.Invoking(() => ((AIFunction)null!).WithValueObjectValidation())
            .Should().Throw<ArgumentNullException>().WithParameterName("function");
    }

    /// <summary>
    /// Under a source-generated context naming the value objects, with no reflection, the wrapper refuses and passes as it
    /// does under reflection.
    /// </summary>
    [Fact]
    public async Task A_source_generated_context_refuses_and_passes_as_reflection_does()
    {
        var function = Validated(PlaceOrder, LanguageModelContext.Default.Options);

        (await InvokeAsync(function, Order("quantity", "1001"))).Should().Be(await InvokeAsync(Validated(PlaceOrder), Order("quantity", "1001")));
        (await InvokeAsync(function, Order("quantity", "\"7\""))).Should().Contain("placed 7");
        (await InvokeAsync(function, Arguments(ValidOrder))).Should().Contain("placed 3");

        // A context that knows no JSON node or document still reads one, as the binding does.
        var count = Validated(Count, LanguageModelContext.Default.Options);
        LanguageModelContext.Default.Options.TryGetTypeInfo(typeof(JsonNode), out _).Should().BeFalse();
        LanguageModelContext.Default.Options.TryGetTypeInfo(typeof(JsonDocument), out _).Should().BeFalse();
        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = JsonNode.Parse("1001") })).Should().Contain("value_object.out_of_range");
        (await InvokeAsync(count, new AIFunctionArguments { ["quantity"] = JsonDocument.Parse("1001") })).Should().Contain("value_object.out_of_range");
    }

    private static bool IsRejection(string result)
        => JsonNode.Parse(result) is JsonObject answer && answer["error"]?.GetValue<string>() == "invalid_argument";

    private static async Task<bool> Fails(AIFunction function, AIFunctionArguments arguments)
    {
        try
        {
            await function.InvokeAsync(arguments, TestContext.Current.CancellationToken);
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException)
        {
            return true;
        }
    }

    private static async Task<bool> Throws(AIFunction function, AIFunctionArguments arguments)
    {
        try
        {
            await function.InvokeAsync(arguments, TestContext.Current.CancellationToken);
            return false;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    /// <summary>
    /// A converter of the application's own for <see cref="Quantity"/>, which runs the value object's and wraps what it
    /// throws with a message of its own.
    /// </summary>
    private sealed class WrappingConverter : JsonConverter<Quantity>
    {
        private static readonly Quantity.ValueJsonConverter Inner = new();

        public override Quantity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try
            {
                return Inner.Read(ref reader, typeToConvert, options);
            }
            catch (JsonException exception)
            {
                throw new JsonException("The quantity of an order line is not valid.", exception);
            }
        }

        public override void Write(Utf8JsonWriter writer, Quantity value, JsonSerializerOptions options) => Inner.Write(writer, value, options);
    }

    /// <summary>
    /// A converter of the application's own for <see cref="Quantity"/>, which refuses every value, with or without a code.
    /// </summary>
    private sealed class RefusingConverter(string? code) : JsonConverter<Quantity>
    {
        public override Quantity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var refusal = new JsonException("Not a quantity this application takes.");
            if (code is not null)
            {
                refusal.Data[ValueObjectErrors.ErrorCodeKey] = code;
            }

            throw refusal;
        }

        public override void Write(Utf8JsonWriter writer, Quantity value, JsonSerializerOptions options)
            => writer.WriteNumberValue(value.Value);
    }
}

/// <summary>A declaration no other test closes a value object over, so that nothing else can have registered it.</summary>
public sealed class Unregistered : IDeclaration<string>
{
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;
}

/// <summary>A context that knows the result of a tool, not its parameter.</summary>
[JsonSerializable(typeof(string))]
internal sealed partial class LanguageModelContextWithoutQuantity : JsonSerializerContext;
