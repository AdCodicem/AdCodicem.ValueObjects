using System.Collections.Immutable;
using System.Globalization;
using AdCodicem.ValueObjects.AzureFunctions;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.TestData;
using AdCodicem.ValueObjects.UnitTests.Web;
using Microsoft.Azure.Functions.Worker.Converters;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace AdCodicem.ValueObjects.UnitTests.Functions;

/// <summary>
/// The converter the isolated worker tries first among its registered converters: a value object bound from the text of
/// a route value, a query value or other binding data, through its rules, called directly as the worker calls it.
/// </summary>
public sealed class ValueObjectInputConverterTests
{
    private const string Guid = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";

    private static readonly ValueObjectInputConverter Converter = new();

    [Fact]
    public async Task A_missing_context_is_refused()
    {
        var convert = async () => await Converter.ConvertAsync(null!);

        await convert.Should().ThrowAsync<ArgumentNullException>().WithParameterName("context");
    }

    /// <summary>
    /// Absent binding data is left to the worker, which binds <see langword="null"/> to a nullable value object and fails a
    /// required one itself: a failure whose source is <see langword="null"/> is one the worker drops. A source that is not
    /// text, such as the bytes of a payload, is left to the converters that read it.
    /// </summary>
    [Fact]
    public async Task A_source_that_is_no_text_is_left_to_the_next_converter()
    {
        (await ConvertAsync<CustomerId>(null)).Status.Should().Be(ConversionStatus.Unhandled);
        (await ConvertAsync<CustomerId?>(null)).Status.Should().Be(ConversionStatus.Unhandled);
        (await ConvertAsync<CustomerId>(new ReadOnlyMemory<byte>([0x22, 0x22]))).Status.Should().Be(ConversionStatus.Unhandled);
        (await ConvertAsync<Quantity>(7)).Status.Should().Be(ConversionStatus.Unhandled);
    }

    [Fact]
    public async Task A_type_that_is_no_value_object_is_left_to_the_next_converter()
    {
        (await ConvertAsync<int>("007")).Status.Should().Be(ConversionStatus.Unhandled);
        (await ConvertAsync<Guid>(Guid)).Status.Should().Be(ConversionStatus.Unhandled);
        (await ConvertAsync<string>("text")).Status.Should().Be(ConversionStatus.Unhandled);
        (await ConvertAsync<int>("007")).Status.Should().Be(ConversionStatus.Unhandled, "a type described once is not, as one missed is not kept");
    }

    /// <summary>
    /// A parameter carrying an input or trigger binding attribute is the binding's: its payload goes to the binding's
    /// converters and the worker's serializer, which reads the generated JSON converter.
    /// </summary>
    [Fact]
    public async Task A_payload_its_binding_attribute_claims_is_left_to_the_binding()
    {
        var properties = ImmutableDictionary<string, object>.Empty.Add("bindingAttribute", new object());

        (await ConvertAsync<CustomerId>(Guid, properties)).Status.Should().Be(ConversionStatus.Unhandled);
        (await ConvertAsync<EmailAddress>("\"someone@example.com\"", properties)).Status.Should().Be(ConversionStatus.Unhandled);
    }

    /// <summary>
    /// A parameter whose <c>[InputConverter]</c> names another converter, <c>[FromBody]</c> among them, is that
    /// converter's; one that names this converter is read, a payload included, which is how a raw-text queue message binds.
    /// </summary>
    [Fact]
    public async Task A_parameter_naming_a_converter_is_read_only_when_it_names_this_one()
    {
        var body = ImmutableDictionary<string, object>.Empty.Add("converterType", new FromBodyAttribute().ConverterType.AssemblyQualifiedName!);
        var named = ImmutableDictionary<string, object>.Empty.Add("converterType", typeof(ValueObjectInputConverter).AssemblyQualifiedName!);
        var unnamed = ImmutableDictionary<string, object>.Empty.Add("converterType", typeof(ValueObjectInputConverter));

        (await ConvertAsync<Quantity>("7", body)).Status.Should().Be(ConversionStatus.Unhandled);
        (await ConvertAsync<Quantity>("7", unnamed)).Status.Should().Be(ConversionStatus.Unhandled, "the worker lists the name, never the type");
        (await ConvertAsync<Quantity>("7", named)).Value.Should().Be(Quantity.Create(7));
        (await ConvertAsync<EmailAddress>("someone@example.com", named)).Value.Should().Be(EmailAddress.Create("someone@example.com"));
    }

    /// <summary>
    /// Text the worker's own JSON conversion refuses binds through the value object's rules, in the invariant culture: a
    /// <see cref="Guid"/>, text, a date, an integer with leading zeros, which JSON refuses, and a decimal with a point.
    /// </summary>
    [Fact]
    public async Task Accepted_text_binds_through_the_value_objects_rules()
    {
        (await ConvertAsync<CustomerId>(Guid)).Value.Should().Be(CustomerId.Create(System.Guid.Parse(Guid)));
        (await ConvertAsync<EmailAddress>(" Someone@Example.com ")).Value.Should().Be(EmailAddress.Create("someone@example.com"));
        (await ConvertAsync<BirthDate>("1990-10-13")).Value.Should().Be(BirthDate.Create(new DateOnly(1990, 10, 13)));
        (await ConvertAsync<Quantity>("007")).Value.Should().Be(Quantity.Create(7));
        (await ConvertAsync<Amount>("12.5")).Value.Should().Be(Amount.Create(12.5m));
    }

    [Fact]
    public async Task A_nullable_value_object_binds_the_value_object()
    {
        var result = await ConvertAsync<CustomerId?>(Guid);

        result.Status.Should().Be(ConversionStatus.Succeeded);
        result.Value.Should().BeOfType<CustomerId>().Which.Should().Be(CustomerId.Create(System.Guid.Parse(Guid)));
        (await ConvertAsync<Quantity?>("12")).Value.Should().Be(Quantity.Create(12));
    }

    /// <summary>
    /// The host sends binding data of another type than <see cref="string"/> as JSON, quotes included: a queue message's
    /// <c>InsertionTime</c> reaches the converter as a JSON string literal, which the value object reads once decoded,
    /// escapes and all, and whose verdict then stands.
    /// </summary>
    [Fact]
    public async Task A_JSON_string_literal_is_read_as_the_string_it_encodes_when_the_text_itself_is_refused()
    {
        (await ConvertAsync<OccurredAt?>("\"2026-10-13T10:13:54+00:00\"")).Value
            .Should().Be(OccurredAt.Create(new DateTimeOffset(2026, 10, 13, 10, 13, 54, TimeSpan.Zero)));
        (await ConvertAsync<Quantity>("\"\\u0031\\u0032\"")).Value.Should().Be(Quantity.Create(12));

        var decoded = await ConvertAsync<Quantity>("\"5000\"");
        decoded.Status.Should().Be(ConversionStatus.Failed);
        decoded.Error!.ShouldCarry(ValueObjectErrorCodes.OutOfRange);
        (await ConvertAsync<OccurredAt>("\"1999-12-31T23:59:59+00:00\"")).Error!.ShouldCarry(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// A decimal's <c>TryParse</c> takes no exponent, which a JSON number may carry, and the host writes a message's number
    /// through a double, <c>0.0000001</c> as <c>1E-07</c>: a value object over <see cref="decimal"/> reads text the type
    /// refuses as it stands, and that is a JSON number, as the worker's JSON conversion reads it, and that verdict then
    /// stands.
    /// </summary>
    [Fact]
    public async Task A_JSON_number_is_read_as_a_decimal_when_the_text_itself_is_refused()
    {
        (await ConvertAsync<Salary>("1E-07")).Value.Should().Be(Salary.Create(0.0000001m));
        (await ConvertAsync<Salary?>("1E+21")).Value.Should().Be(Salary.Create(1E21m));
        (await ConvertAsync<Percentage>(" 5e1 ")).Value.Should().Be(Percentage.Create(50m));

        var refused = (await ConvertAsync<Percentage>("1.01E+2")).Error!;
        refused.ShouldCarry(ValueObjectErrorCodes.OutOfRange);
        refused.Message.Should().Be("The value is not a valid Percentage: The value must be less than or equal to 100. (value_object.out_of_range)");
    }

    /// <summary>
    /// Text that is no JSON number a decimal holds keeps the verdict of the text as it stands: a number beyond the range of
    /// a decimal, text after a number, a decimal comma, another JSON value.
    /// </summary>
    [Theory]
    [InlineData("1E+29")]
    [InlineData("1e2x")]
    [InlineData("1e2 3")]
    [InlineData("12,5")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[1e2]")]
    [InlineData("")]
    public async Task Text_that_is_no_JSON_number_a_decimal_holds_keeps_the_verdict_of_the_text_as_it_stands(string text)
    {
        var refusal = (await ConvertAsync<Percentage>(text)).Error!;

        refusal.ShouldCarry(ValueObjectErrorCodes.NotParsable);
        refusal.Message.Should().Be("The value is not a valid Percentage: The text is not a valid decimal. (value_object.not_parsable)");
    }

    /// <summary>
    /// Only a decimal reads a JSON number apart: an integer's <c>TryParse</c> reads every JSON number an integer holds, a
    /// double's every one, and the worker's JSON conversion refuses an exponent for an integer too.
    /// </summary>
    [Fact]
    public async Task Only_a_decimal_reads_a_JSON_number_apart()
    {
        (await ConvertAsync<Quantity>("1e2")).Error!.ShouldCarry(ValueObjectErrorCodes.NotParsable);
        (await ConvertAsync<Latitude>("1E-07")).Value.Should().Be(Latitude.Create(1E-07));
    }

    /// <summary>
    /// The text is read in the invariant culture, whatever the worker's: a point separates the decimals, and a comma is
    /// refused rather than read as the French decimal separator, as it stands and once decoded.
    /// </summary>
    [Fact]
    public async Task Text_is_read_in_the_invariant_culture_whatever_the_current_one()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            (await ConvertAsync<Latitude>("12.5")).Value.Should().Be(Latitude.Create(12.5));
            (await ConvertAsync<Amount>("12.5")).Value.Should().Be(Amount.Create(12.5m));
            (await ConvertAsync<Amount>("12,5")).Error!.ShouldCarry(ValueObjectErrorCodes.NotParsable);
            (await ConvertAsync<Amount>("\"12,5\"")).Error!.ShouldCarry(ValueObjectErrorCodes.NotParsable);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>Text the type accepts as it stands is never decoded: a quote is part of a value over text.</summary>
    [Fact]
    public async Task Text_the_type_accepts_as_it_stands_is_not_decoded()
    {
        (await ConvertAsync<Ordering.OrderReference>("\"PO-1042\"")).Value.Should().Be(Ordering.OrderReference.Create("\"PO-1042\""));
    }

    /// <summary>
    /// Text that only looks like a JSON string literal, unclosed, followed by a second value, holding an escape the
    /// reader refuses or one of half a surrogate pair, keeps the verdict of the text as it stands: <c>not_parsable</c>
    /// where the decoded text would be out of range.
    /// </summary>
    [Theory]
    [InlineData("\"5000")]
    [InlineData("5000\"")]
    [InlineData("\"")]
    [InlineData("\"5000\" \"1\"")]
    [InlineData("\"5000\"x\"")]
    [InlineData("\"50\\x00\"")]
    [InlineData("\"\\uD800\"")]
    public async Task Text_that_is_no_JSON_string_literal_keeps_the_verdict_of_the_text_as_it_stands(string text)
    {
        (await ConvertAsync<Quantity>(text)).Error!.ShouldCarry(ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// Empty text is parsed, nullable or not, and refused by the type's own rule: <c>?customer=</c> is not an absent value.
    /// </summary>
    [Fact]
    public async Task Empty_text_is_refused_by_the_types_own_rule()
    {
        (await ConvertAsync<EmailAddress>(string.Empty)).Error!.ShouldCarry(ValueObjectErrorCodes.Required);
        (await ConvertAsync<EmailAddress?>(string.Empty)).Error!.ShouldCarry(ValueObjectErrorCodes.Required);
        (await ConvertAsync<CustomerId>(string.Empty)).Error!.ShouldCarry(ValueObjectErrorCodes.NotParsable);
        (await ConvertAsync<CustomerId?>(string.Empty)).Error!.ShouldCarry(ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// The worker keeps only the text of a converter's exception, in the one it throws, so the message names the type, the
    /// rule and its code; neither it nor the data carry the refused text.
    /// </summary>
    [Fact]
    public async Task A_refusal_names_the_type_the_rule_and_its_code_and_never_the_text()
    {
        var result = await ConvertAsync<Quantity>("5000");

        result.Status.Should().Be(ConversionStatus.Failed);
        var refusal = result.Error.Should().BeOfType<FormatException>().Which;
        refusal.Message.Should().Be("The value is not a valid Quantity: The value must be less than or equal to 1000. (value_object.out_of_range)");
        refusal.ShouldCarry(ValueObjectErrorCodes.OutOfRange);
        refusal.InnerException.Should().BeNull();
        refusal.Message.Should().NotContain("5000");

        var (message, code) = ProblemReading.Refusal<CustomerId, Guid>("not-a-guid");
        (await ConvertAsync<CustomerId>("not-a-guid")).Error!.Message.Should().Be($"The value is not a valid CustomerId: {message} ({code})");
        (await ConvertAsync<Ordering.OrderReference>("ab")).Error!.Message
            .Should().StartWith("The value is not a valid Ordering.OrderReference: ", "a nested type is named as C# names it");
    }

    /// <summary>
    /// The Azure Functions worker keeps no exception a converter fails a parameter with, only its text: the converter's
    /// <see cref="FormatException"/> carries the code in its data, as any other ecosystem's does, and at the end of its
    /// message, the one place the worker's own failure keeps it, where <see cref="ValueObjectErrors.TryGetCode"/> reads it.
    /// </summary>
    [Fact]
    public async Task The_code_is_read_from_the_Functions_converters_refusal_and_ends_its_message()
    {
        var result = await ConvertAsync<Iban>("FR76");

        var refusal = result.Error.Should().BeOfType<FormatException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);
        refusal.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.TooShort);
        refusal.Message.Should().StartWith("The value is not a valid Iban: ").And.EndWith($" ({ValueObjectErrorCodes.TooShort})");
    }

    /// <summary>
    /// A value object written by hand and a construction of a generic one, which nothing registered, are described by
    /// reflection the first time the converter meets them, and found registered from then on; a generic construction is
    /// named as C# names it.
    /// </summary>
    [Fact]
    public async Task A_value_object_nothing_registered_is_described_the_first_time_and_registered_from_then_on()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<ValueObjectInputConverterTests>), out _).Should().BeFalse("no test has met it yet");
        ValueObjectRegistry.TryGet(typeof(Declared<ConverterTicket, string>), out _).Should().BeFalse("no test has met it yet");

        (await ConvertAsync<Reference<ValueObjectInputConverterTests>>(" po-1042 ")).Value
            .Should().Be(Reference<ValueObjectInputConverterTests>.Create("PO-1042"));
        (await ConvertAsync<Declared<ConverterTicket, string>?>("abc")).Value
            .Should().Be(Declared<ConverterTicket, string>.Create("abc"));

        ValueObjectRegistry.TryGet(typeof(Reference<ValueObjectInputConverterTests>), out _).Should().BeTrue();
        ValueObjectRegistry.TryGet(typeof(Declared<ConverterTicket, string>), out _).Should().BeTrue();

        var refusal = (await ConvertAsync<Reference<ValueObjectInputConverterTests>>("ABCDEFGHIJKLMNOP")).Error!;
        refusal.Message.Should().Be(
            "The value is not a valid Reference<ValueObjectInputConverterTests>: The value must be at most 12 characters long. (value_object.too_long)");
        (await ConvertAsync<Declared<ConverterTicket, string>>("a1")).Error!.Message.Should().Be(
            "The value is not a valid Declared<ConverterTicket, string>: A ticket holds ASCII letters only. (value_object.invalid_format)");
    }

    /// <summary>
    /// A parser written by hand may refuse without a reason, which is reported as the minimal API package reports it: not
    /// a valid instance of the type, <c>not_parsable</c>.
    /// </summary>
    [Fact]
    public async Task A_refusal_without_a_reason_is_reported_as_not_parsable()
    {
        var refusal = (await ConvertAsync<HandWrittenId<SilentFunctionsProfile>>("fnz_anything")).Error!;

        refusal.Message.Should().Be("The value is not a valid HandWrittenId<SilentFunctionsProfile>. (value_object.not_parsable)");
        refusal.ShouldCarry(ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, binds through the worker as its own
    /// <c>TryParse</c> reads the text, and a refused one is answered over HTTP as a minimal API answers it.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(GeneratedSurface.Samples.Names), MemberType = typeof(GeneratedSurface.Samples))]
    public async Task Every_value_object_binds_through_the_Functions_worker_as_minimal_APIs_bind_it(string type)
        => await GeneratedSurface.Samples.All[type].BindsThroughTheFunctionsWorkerAsync();

    private static async Task<ConversionResult> ConvertAsync<T>(object? source, IReadOnlyDictionary<string, object>? properties = null)
        => await Converter.ConvertAsync(new TextContext(typeof(T), source, properties));
}
