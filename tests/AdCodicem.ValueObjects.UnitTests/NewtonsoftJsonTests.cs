using System.Globalization;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.JsonPatch;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using StjSerializer = System.Text.Json.JsonSerializer;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The Newtonsoft.Json converter, held to the System.Text.Json converter the generator emits: same rules, same wire.
/// </summary>
public partial class NewtonsoftJsonTests
{
    /// <summary>The converter alone, under Newtonsoft.Json's defaults.</summary>
    private static readonly JsonSerializerSettings Defaults = new() { Converters = { new ValueObjectConverter() } };

    /// <summary>
    /// The settings the JSON how-to recommends, under which Newtonsoft.Json hands the converter the text of every
    /// string and the digits of every number.
    /// </summary>
    private static readonly JsonSerializerSettings Recommended = new()
    {
        DateParseHandling = DateParseHandling.None,
        FloatParseHandling = FloatParseHandling.Decimal,
        Converters = { new ValueObjectConverter() },
    };

    /// <summary>The options under which System.Text.Json reads a number written as text.</summary>
    private static readonly System.Text.Json.JsonSerializerOptions NumbersFromText =
        new() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };

    /// <summary>One value object for each of the 22 underlying types, keyed by a description of the case.</summary>
    private static readonly Dictionary<string, object> EveryUnderlyingType = new()
    {
        ["string"] = Iban.Create("FR7630006000011234567890189"),
        ["Guid"] = CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001")),
        ["bool"] = Consent.Create(true),
        ["char"] = Grade.Create('B'),
        ["sbyte"] = Adjustment.Create(-10),
        ["byte"] = Score.Create(100),
        ["short"] = Quantity.Create(1000),
        ["ushort"] = Port.Create(ushort.MaxValue),
        ["int"] = PageNumber.Create(int.MaxValue),
        ["uint"] = SequenceNumber.Create(uint.MaxValue),
        ["long"] = FileSize.Create(long.MaxValue),
        ["ulong"] = ByteCount.Create(ulong.MaxValue),
        ["Int128"] = LedgerBalance.Create(Int128.Parse("-1000000000000000000000", CultureInfo.InvariantCulture)),
        ["UInt128"] = Fingerprint.Create(UInt128.MaxValue),
        ["decimal with sixteen digits"] = Amount.Create(12345678901234.56m),
        ["double"] = Latitude.Create(-45.5),
        ["float"] = Ratio.Create(0.25f),
        ["DateOnly"] = BirthDate.Create(new DateOnly(1980, 5, 17)),
        ["TimeOnly"] = OpeningTime.Create(new TimeOnly(9, 30, 15)),
        ["DateTime in UTC"] = RecordedAt.Create(new DateTime(2024, 6, 1, 12, 30, 45, 123, DateTimeKind.Utc)),
        ["DateTime of no zone, in whole seconds"] = RecordedAt.Create(new DateTime(2024, 6, 1, 12, 30, 45)),
        ["DateTimeOffset"] = OccurredAt.Create(new DateTimeOffset(2024, 6, 1, 12, 30, 45, 123, TimeSpan.FromHours(2))),
        ["DateTimeOffset in whole seconds"] = OccurredAt.Create(new DateTimeOffset(2024, 6, 1, 12, 30, 45, TimeSpan.Zero)),
        ["TimeSpan"] = Duration.Create(new TimeSpan(0, 2, 30, 15, 500)),
    };

    /// <summary>
    /// A value object over each real type holding a whole value, keyed by a description of the case. Newtonsoft.Json
    /// writes such a value with a fraction, where System.Text.Json writes none.
    /// </summary>
    private static readonly Dictionary<string, (object Value, string Newtonsoft, string SystemTextJson)> WholeReals = new()
    {
        ["decimal"] = (TransferLimit.Create(1250m), "1250.0", "1250"),
        ["double"] = (Latitude.Create(12), "12.0", "12"),
        ["float"] = (Ratio.Create(1f), "1.0", "1"),
    };

    /// <summary>
    /// A value object over each numeric type, keyed by the type. An integer stays within the range of a long, which
    /// Newtonsoft.Json reads a JSON integer as; beyond it, Newtonsoft.Json reads a BigInteger, which no type converter
    /// takes.
    /// </summary>
    private static readonly Dictionary<string, object> NumbersBothWays = new()
    {
        ["sbyte"] = Adjustment.Create(-10),
        ["byte"] = Score.Create(100),
        ["short"] = Quantity.Create(1000),
        ["ushort"] = Port.Create(ushort.MaxValue),
        ["int"] = PageNumber.Create(int.MaxValue),
        ["uint"] = SequenceNumber.Create(uint.MaxValue),
        ["long"] = FileSize.Create(long.MaxValue),
        ["ulong"] = ByteCount.Create((ulong)long.MaxValue),
        ["Int128"] = LedgerBalance.Create(Int128.Parse("-1000000000000000000000", CultureInfo.InvariantCulture)),
        ["UInt128"] = Fingerprint.Create(UInt128.MaxValue),
        ["decimal with sixteen digits"] = Amount.Create(12345678901234.56m),
        ["whole decimal"] = TransferLimit.Create(1250m),
        ["double"] = Latitude.Create(-45.5),
        ["float"] = Ratio.Create(0.25f),
    };

    /// <summary>
    /// The DateTimeOffset value objects of the round trips under the default date handling, keyed by their offset.
    /// </summary>
    private static readonly Dictionary<string, OccurredAt> Instants = new()
    {
        ["UTC"] = OccurredAt.Create(new DateTimeOffset(2024, 6, 1, 12, 30, 45, TimeSpan.Zero)),
        ["two hours east"] = OccurredAt.Create(new DateTimeOffset(2024, 6, 1, 12, 30, 45, TimeSpan.FromHours(2))),
    };

    public static TheoryData<string> Every => [.. EveryUnderlyingType.Keys];

    public static TheoryData<string> EveryWholeReal => [.. WholeReals.Keys];

    public static TheoryData<string> EveryInstant => [.. Instants.Keys];

    public static TheoryData<string> EveryNumber =>
    [
        .. EveryUnderlyingType.Where(entry => StjSerializer.Serialize(entry.Value, entry.Value.GetType()) is [not '"', ..] and not ("true" or "false"))
            .Select(entry => entry.Key),
    ];

    public static TheoryData<string> EveryNumberBothWays => [.. NumbersBothWays.Keys];

    /// <summary>
    /// The payment <see cref="JsonTests"/> writes with System.Text.Json, written with Newtonsoft.Json: the converter
    /// claims each value object, an optional one included, and leaves the payment itself to the serializer.
    /// </summary>
    [Fact]
    public void A_value_object_travels_as_its_bare_underlying_value()
    {
        var payment = new Payment(
            Iban.Create("FR7630006000011234567890189"),
            Amount.Create(1250m),
            CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001")),
            BirthDate.Create(new DateOnly(1980, 5, 17)),
            Quantity.Create(3));

        var json = JsonConvert.SerializeObject(payment, Defaults);

        json.Should().Be(
            """
            {"Account":"FR7630006000011234567890189","Total":1250.00,"Customer":"0192f4a0-0000-7000-8000-000000000001","Birth":"1980-05-17","Lines":3}
            """);
        JsonConvert.DeserializeObject<Payment>(json, Defaults).Should().Be(payment);
    }

    [Fact]
    public void A_null_is_no_value_for_an_optional_value_object_and_refused_for_a_required_one()
    {
        const string json =
            """
            {"Account":"FR7630006000011234567890189","Total":1250,"Customer":"0192f4a0-0000-7000-8000-000000000001","Birth":null,"Lines":3}
            """;

        var required = () => JsonConvert.DeserializeObject<Iban>("null", Defaults);

        JsonConvert.DeserializeObject<Payment>(json, Defaults)!.Birth.Should().BeNull();
        required.Should().Throw<JsonSerializationException>().WithMessage("Cannot convert null to 'Iban'.")
            .Which.ShouldCarry(ValueObjectErrorCodes.Required);
    }

    /// <summary>
    /// A value object written by hand may carry a type the generator does not support. Its value then travels the
    /// way Newtonsoft.Json writes and reads that type, as the general-purpose System.Text.Json converter lets
    /// System.Text.Json handle it, and its rules still apply.
    /// </summary>
    [Fact]
    public void A_value_object_over_another_type_travels_as_Newtonsoft_carries_that_type()
    {
        var link = HandWrittenLink.Create(new Uri("https://example.com/a"));

        var json = JsonConvert.SerializeObject(link, Defaults);
        var relative = () => JsonConvert.DeserializeObject<HandWrittenLink>("\"/a\"", Defaults);

        json.Should().Be("\"https://example.com/a\"");
        JsonConvert.DeserializeObject<HandWrittenLink>(json, Defaults).Should().Be(link);
        relative.Should().Throw<JsonSerializationException>()
            .WithMessage("The value is not a valid HandWrittenLink: A link is an absolute URI.")
            .Which.ShouldCarry(ValueObjectErrorCodes.InvalidFormat);
    }

    /// <summary>
    /// A token Newtonsoft.Json cannot read as the type a value object written by hand carries fails with Newtonsoft.Json's
    /// own exception, which carries no code. A handler of the serializer's Error event that handles it, as MVC's does,
    /// leaves the read without a value, and the converter then refuses the token as not parsable, never as missing: an
    /// undefined token among them. An empty string, which Newtonsoft.Json reads as no value of a type that can be null,
    /// is judged by the value object's rules.
    /// </summary>
    [Fact]
    public void A_token_a_value_object_written_by_hand_cannot_carry_is_not_parsable_once_its_error_is_handled()
    {
        var codes = new List<string>();
        var handling = new JsonSerializerSettings
        {
            Converters = { new ValueObjectConverter() },
            Error = (_, arguments) =>
            {
                codes.Add(ValueObjectErrors.TryGetCode(arguments.ErrorContext.Error, out var code) ? code : "none");
                arguments.ErrorContext.Handled = true;
            },
        };
        var unhandled = () => JsonConvert.DeserializeObject<Linked>("""{"Link":42}""", Defaults);
        var undefined = () => JsonConvert.DeserializeObject<Linked>("""{"Link":undefined}""", Defaults);
        var empty = () => JsonConvert.DeserializeObject<Linked>("""{"Link":""}""", Defaults);

        unhandled.Should().Throw<JsonSerializationException>().WithMessage("Error converting value 42 to type 'System.Uri'.*")
            .Which.Data.Contains(ValueObjectErrors.ErrorCodeKey).Should().BeFalse();
        JsonConvert.DeserializeObject<Linked>("""{"Link":42}""", handling)!.Link.IsDefault.Should().BeTrue();
        codes.Should().Equal("none", ValueObjectErrorCodes.NotParsable);
        undefined.Should().Throw<JsonSerializationException>().WithMessage("The value could not be read as HandWrittenLink.")
            .Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);
        empty.Should().Throw<JsonSerializationException>().WithMessage("The value is not a valid HandWrittenLink: A link is required.")
            .Which.ShouldCarry(ValueObjectErrorCodes.Required);
    }

    /// <summary>
    /// The converter refuses to write what the System.Text.Json converter refuses to write: an instance equal to the
    /// default whose value its type rejects, a member of an object or an optional value object holding one included,
    /// with the rule and never the value. A type that accepts its zero writes it.
    /// </summary>
    [Fact]
    public void A_value_the_value_object_rejects_is_never_written()
    {
#pragma warning disable VO0010 // The uninitialized instance is what the writer refuses.
        var country = () => JsonConvert.SerializeObject(default(CountryCode), Defaults);
        var birth = () => JsonConvert.SerializeObject((BirthDate?)default(BirthDate), Defaults);
        var payment = () => JsonConvert.SerializeObject(
            new Payment(Iban.Create("FR7630006000011234567890189"), Amount.Create(1m), default, null, Quantity.Create(3)),
            Defaults);
        var amount = JsonConvert.SerializeObject(default(Amount), Defaults);
#pragma warning restore VO0010

        country.Should().Throw<JsonSerializationException>().WithMessage("The value to write is not a valid CountryCode: ?*")
            .Which.ShouldCarry(CodeCarrying.CodeOfDefault<CountryCode, string>());
        birth.Should().Throw<JsonSerializationException>().WithMessage("The value to write is not a valid BirthDate: ?*")
            .Which.ShouldCarry(CodeCarrying.CodeOfDefault<BirthDate, DateOnly>());
        payment.Should().Throw<JsonSerializationException>()
            .WithMessage("The value to write is not a valid CustomerId: A customer identifier must not be empty.")
            .Which.ShouldCarry(CodeCarrying.CodeOfDefault<CustomerId, Guid>());
        amount.Should().Be("0.0", "zero is an amount");
    }

    /// <summary>
    /// The serializer writes a null itself before it looks for a converter, so only a caller of the converter - one
    /// that delegates to it, say - hands it one.
    /// </summary>
    [Fact]
    public void Writing_null_through_the_converter_writes_a_JSON_null()
    {
        using var text = new StringWriter(CultureInfo.InvariantCulture);
        using var writer = new JsonTextWriter(text);

        new ValueObjectConverter().WriteJson(writer, null, JsonSerializer.CreateDefault());
        writer.Flush();

        text.ToString().Should().Be("null");
    }

    /// <summary>
    /// The serializer does not ask a converter named on a member whether it converts the member's type.
    /// </summary>
    [Fact]
    public void The_converter_refuses_a_member_that_is_not_a_value_object()
    {
        var write = () => JsonConvert.SerializeObject(new Tagged { Name = "x" });
        var read = () => JsonConvert.DeserializeObject<Tagged>("""{"Name":"x"}""");

        new ValueObjectConverter().CanConvert(typeof(string)).Should().BeFalse();
        write.Should().Throw<JsonSerializationException>().WithMessage("'String' is not a value object.");
        read.Should().Throw<JsonSerializationException>().WithMessage("'String' is not a value object.");
    }

    /// <summary>
    /// Carrying a value, or the marker, does not make a value object: the converter claims what the registry can
    /// describe, and leaves anything else to Newtonsoft.Json, which writes it as the object it is.
    /// </summary>
    [Fact]
    public void A_type_that_carries_a_value_without_the_contract_of_a_value_object_is_left_to_the_serializer()
    {
        var converter = new ValueObjectConverter();

        converter.CanConvert(typeof(IEntityId)).Should().BeFalse();
        converter.CanConvert(typeof(MarkerOnlyValue)).Should().BeFalse();
        JsonConvert.SerializeObject(new ClassBackedValue(), Defaults).Should().Be("""{"Value":"class"}""");
        JsonConvert.SerializeObject(new SelflessValue(), Defaults).Should().Be("""{"Value":"selfless"}""");
    }

    [Theory]
    [MemberData(nameof(Every))]
    public void Newtonsoft_writes_what_System_Text_Json_writes(string name)
    {
        var value = EveryUnderlyingType[name];

        var newtonsoft = JsonConvert.SerializeObject(value, value.GetType(), Defaults);

        newtonsoft.Should().Be(StjSerializer.Serialize(value, value.GetType()));
    }

    /// <summary>
    /// Newtonsoft.Json always writes a real with a fraction, so a whole value is written <c>1250.0</c> where
    /// System.Text.Json writes <c>1250</c>: the same value, not the same text, which each serializer reads back as
    /// the value the other wrote.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryWholeReal))]
    public void A_whole_real_is_written_with_a_fraction_and_read_back_as_the_same_value(string name)
    {
        var (value, expected, systemTextJson) = WholeReals[name];

        var newtonsoft = JsonConvert.SerializeObject(value, value.GetType(), Defaults);

        newtonsoft.Should().Be(expected);
        StjSerializer.Serialize(value, value.GetType()).Should().Be(systemTextJson);
        StjSerializer.Deserialize(newtonsoft, value.GetType()).Should().Be(value);
        JsonConvert.DeserializeObject(systemTextJson, value.GetType(), Defaults).Should().Be(value);
        JsonConvert.DeserializeObject(systemTextJson, value.GetType(), Recommended).Should().Be(value);
    }

    [Theory]
    [MemberData(nameof(Every))]
    public void Newtonsoft_reads_back_what_System_Text_Json_writes(string name)
    {
        var value = EveryUnderlyingType[name];

        var read = JsonConvert.DeserializeObject(StjSerializer.Serialize(value, value.GetType()), value.GetType(), Recommended);

        read.Should().Be(value);
    }

    /// <summary>
    /// Under its default float handling, Newtonsoft.Json reads a number with a fraction as a double before the
    /// converter sees it. The double's shortest round-trip text gives back the digits a double can carry; a decimal
    /// with more than that wants <see cref="FloatParseHandling.Decimal"/>.
    /// </summary>
    [Fact]
    public void A_decimal_keeps_its_digits_under_the_default_float_handling()
    {
        JsonConvert.DeserializeObject<Amount>("12345678901234.56", Defaults).Value.Should().Be(12345678901234.56m);
        JsonConvert.DeserializeObject<Amount>("1250", Defaults).Value.Should().Be(1250.00m);
    }

    /// <summary>
    /// Under <see cref="FloatParseHandling.Decimal"/>, Newtonsoft.Json reads every number with a fraction or an
    /// exponent as a decimal before the converter sees it. A double or a float beyond what a decimal holds does not
    /// survive that: one too large makes the reader throw, one too small for a decimal's 28 places is rounded, to
    /// zero when nothing is left of it. Under the default float handling each travels as System.Text.Json carries it.
    /// </summary>
    [Fact]
    public void Decimal_float_handling_loses_a_double_or_a_float_a_decimal_cannot_hold()
    {
        var decimals = new JsonSerializerSettings
        {
            FloatParseHandling = FloatParseHandling.Decimal,
            Converters = { new ValueObjectConverter() },
        };
        var large = Mass.Create(1e30);
        var small = Mass.Create(1.2345678901234567e-20);
        var lightest = Mass.Create(9.1e-31);
        var tiny = Ratio.Create(1e-30f);
        var largeJson = JsonConvert.SerializeObject(large, Defaults);
        var smallJson = JsonConvert.SerializeObject(small, Defaults);
        var lightestJson = JsonConvert.SerializeObject(lightest, Defaults);
        var tinyJson = JsonConvert.SerializeObject(tiny, Defaults);

        var readLarge = () => JsonConvert.DeserializeObject<Mass>(largeJson, decimals);
        var readLightest = () => JsonConvert.DeserializeObject<Mass>(lightestJson, decimals);

        largeJson.Should().Be("1E+30").And.Be(StjSerializer.Serialize(large));
        JsonConvert.DeserializeObject<Mass>(largeJson, Defaults).Should().Be(large);
        JsonConvert.DeserializeObject<Mass>(smallJson, Defaults).Should().Be(small);
        JsonConvert.DeserializeObject<Mass>(lightestJson, Defaults).Should().Be(lightest);
        JsonConvert.DeserializeObject<Ratio>(tinyJson, Defaults).Should().Be(tiny);

        readLarge.Should().Throw<JsonReaderException>();
        JsonConvert.DeserializeObject<Mass>(smallJson, decimals).Value
            .Should().NotBe(small.Value, "a decimal keeps 28 places, and so nine digits of this one");
        readLightest.Should().Throw<JsonSerializationException>()
            .WithMessage("The value is not a valid Mass: *", "read as zero, the lightest mass is below its own minimum")
            .Which.ShouldCarry(ValueObjectErrorCodes.OutOfRange);
        JsonConvert.DeserializeObject<Ratio>(tinyJson, decimals).Value.Should().Be(0f);
    }

    /// <summary>
    /// A number out of the range of the type, with a fraction for an integer, or not finite is refused before any rule
    /// runs, whether it is written as a number or as text: NaN and the infinities are no number the converter reads,
    /// from a string either.
    /// </summary>
    [Theory]
    [InlineData("70000", typeof(Quantity))]
    [InlineData("-1", typeof(Score))]
    [InlineData("-1", typeof(ByteCount))]
    [InlineData("18446744073709551616", typeof(ByteCount))]
    [InlineData("1.5", typeof(PageNumber))]
    [InlineData("2.0", typeof(PageNumber))]
    [InlineData("NaN", typeof(Latitude))]
    [InlineData("1e400", typeof(Latitude))]
    [InlineData("\"70000\"", typeof(Quantity))]
    [InlineData("\"-1\"", typeof(ByteCount))]
    [InlineData("\"1e400\"", typeof(Latitude))]
    [InlineData("\"NaN\"", typeof(Latitude))]
    [InlineData("\"-Infinity\"", typeof(Ratio))]
    public void A_number_the_underlying_type_cannot_hold_is_refused_as_JSON(string json, Type type)
    {
        var act = () => JsonConvert.DeserializeObject(json, type, Defaults);

        act.Should().Throw<JsonSerializationException>().WithMessage($"The value could not be read as {type.Name}.")
            .Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// A value is read from the kind of token it is written as, or from a string for a number: a boolean, an object
    /// or a date is no number, and a number or a string is no boolean.
    /// </summary>
    [Theory]
    [InlineData("\"2024-05-17T10:00:00Z\"", typeof(PageNumber), "number", "Date")]
    [InlineData("{}", typeof(Amount), "number", "StartObject")]
    [InlineData("true", typeof(PageNumber), "number", "Boolean")]
    [InlineData("\"true\"", typeof(Consent), "boolean", "String")]
    [InlineData("1", typeof(Consent), "boolean", "Integer")]
    public void A_token_of_another_kind_is_refused_rather_than_coerced(string json, Type type, string expected, string found)
    {
        var act = () => JsonConvert.DeserializeObject(json, type, Defaults);

        act.Should().Throw<JsonSerializationException>()
            .WithMessage($"Expected a JSON {expected} for {type.Name} but found {found}.")
            .Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// Newtonsoft.Json without the converter writes every value object through its type converter, a number as the
    /// text of the number. The converter reads that text as System.Text.Json reads a number written as text under
    /// <c>AllowReadingFromString</c>, so data stored before the converter was added still reads once it is.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryNumber))]
    public void A_number_written_as_a_string_is_read_as_System_Text_Json_reads_one_from_text(string name)
    {
        var value = EveryUnderlyingType[name];
        var quoted = $"\"{StjSerializer.Serialize(value, value.GetType())}\"";

        JsonConvert.DeserializeObject(quoted, value.GetType(), Defaults).Should().Be(value);
        JsonConvert.DeserializeObject(quoted, value.GetType(), Recommended).Should().Be(value);
        StjSerializer.Deserialize(quoted, value.GetType(), NumbersFromText).Should().Be(value);
    }

    /// <summary>
    /// The text of a number is read whole, with no white space, no group separator and no culture, so what
    /// System.Text.Json refuses to read as a number from text is refused here too.
    /// </summary>
    [Theory]
    [InlineData("\" 7\"", typeof(PageNumber))]
    [InlineData("\"7 \"", typeof(PageNumber))]
    [InlineData("\"1,000\"", typeof(PageNumber))]
    [InlineData("\"7.0\"", typeof(PageNumber))]
    [InlineData("\"0x10\"", typeof(PageNumber))]
    [InlineData("\"seven\"", typeof(PageNumber))]
    [InlineData("\"+7\"", typeof(SequenceNumber))]
    [InlineData("\"12,5\"", typeof(Amount))]
    [InlineData("\"\"", typeof(Amount))]
    [InlineData("\"1.5 \"", typeof(Latitude))]
    public void Text_that_System_Text_Json_reads_as_no_number_is_refused(string json, Type type)
    {
        var newtonsoft = () => JsonConvert.DeserializeObject(json, type, Defaults);
        var systemTextJson = () => StjSerializer.Deserialize(json, type, NumbersFromText);

        newtonsoft.Should().Throw<JsonSerializationException>().WithMessage($"The value could not be read as {type.Name}.")
            .Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);
        systemTextJson.Should().Throw<System.Text.Json.JsonException>();
    }

    /// <summary>
    /// Without the converter, Newtonsoft.Json falls back to the type converter of each value object: it writes every
    /// one as a string, a number as its text, and reads a string or a number of any numeric type. With it, a number is
    /// written as a number. Each reads what the other writes, so a host can add the converter without draining what it
    /// stored before, and a host without it reads the numbers System.Text.Json and the converter write.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryNumberBothWays))]
    public void Newtonsoft_with_the_converter_and_without_it_read_each_other(string name)
    {
        var value = NumbersBothWays[name];
        var type = value.GetType();
        var plain = new JsonSerializerSettings();

        var fallback = JsonConvert.SerializeObject(value, type, plain);
        var converted = JsonConvert.SerializeObject(value, type, Defaults);

        fallback.Should().StartWith("\"", "the type converter writes text");
        JsonConvert.DeserializeObject(fallback, type, Defaults).Should().Be(value);
        JsonConvert.DeserializeObject(fallback, type, Recommended).Should().Be(value);
        JsonConvert.DeserializeObject(converted, type, plain).Should().Be(value);
        JsonConvert.DeserializeObject(StjSerializer.Serialize(value, type), type, plain).Should().Be(value);
    }

    [Fact]
    public void A_number_is_read_through_the_rules_of_the_value_object()
    {
        var act = () => JsonConvert.DeserializeObject<Amount>("-1", Defaults);

        JsonConvert.DeserializeObject<Amount>("12.345", Recommended).Value.Should().Be(12.34m, "the amount normalizes");
        JsonConvert.DeserializeObject<Consent>("false", Defaults).Value.Should().BeFalse();
        act.Should().Throw<JsonSerializationException>().WithMessage("The value is not a valid Amount: *greater than or equal to 0*")
            .Which.ShouldCarry(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// A hand-written value object can refuse a number without naming the rule; the refusal still carries a code, the
    /// one a token that cannot be read at all carries.
    /// </summary>
    [Fact]
    public void A_number_refused_without_a_reason_is_refused_as_not_parsable()
    {
        var act = () => JsonConvert.DeserializeObject<HandWrittenRating>("9", Defaults);

        JsonConvert.DeserializeObject<HandWrittenRating>("4", Defaults).Should().Be(HandWrittenRating.Create(4));
        act.Should().Throw<JsonSerializationException>().WithMessage("The value is not a valid HandWrittenRating: ")
            .Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// Under its default date handling, Newtonsoft.Json reads a string that looks like a date as a date before the
    /// converter sees it, and the text is gone: a string value object refuses the date rather than take another
    /// text, and reads the text itself once the settings leave strings alone.
    /// </summary>
    [Fact]
    public void A_string_value_object_keeps_text_that_looks_like_a_date_or_refuses_it()
    {
        const string json = "\"2024-05-17T10:00:00Z\"";

        var act = () => JsonConvert.DeserializeObject<Label>(json, Defaults);

        act.Should().Throw<JsonSerializationException>().WithMessage("*Label*DateParseHandling*None*")
            .Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);
        JsonConvert.DeserializeObject<Label>(json, Recommended).Value.Should().Be("2024-05-17T10:00:00Z");
    }

    /// <summary>
    /// A DateTime keeps the kind System.Text.Json reads from the same text: UTC stays UTC.
    /// </summary>
    [Fact]
    public void A_DateTime_value_object_reads_its_kind_as_System_Text_Json_does()
    {
        const string json = "\"2024-06-01T12:30:45.123Z\"";
        var expected = StjSerializer.Deserialize<RecordedAt>(json).Value;

        var fromText = JsonConvert.DeserializeObject<RecordedAt>(json, Recommended).Value;
        var fromDate = JsonConvert.DeserializeObject<RecordedAt>(json, Defaults).Value;

        fromText.Should().Be(expected).And.BeIn(DateTimeKind.Utc);
        fromDate.Should().Be(expected).And.BeIn(DateTimeKind.Utc);
    }

    /// <summary>
    /// Under the default date handling a DateTimeOffset reaches the converter as a DateTime. One in UTC, or of no
    /// zone, says what the text said; one that carried another offset was converted to local time, and its offset
    /// is gone. Read as text, or as a DateTimeOffset, it keeps it.
    /// </summary>
    [Fact]
    public void A_DateTimeOffset_value_object_reads_whatever_still_says_what_the_text_said()
    {
        var withOffset = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.DateTimeOffset,
            Converters = { new ValueObjectConverter() },
        };
        var utc = new DateTimeOffset(2024, 6, 1, 12, 30, 45, TimeSpan.Zero);
        var paris = new DateTimeOffset(2024, 6, 1, 12, 30, 45, TimeSpan.FromHours(2));

        JsonConvert.DeserializeObject<OccurredAt>("\"2024-06-01T12:30:45Z\"", Defaults).Value.Should().Be(utc);
        JsonConvert.DeserializeObject<OccurredAt>("\"2024-06-01T12:30:45\"", Defaults).Value
            .Should().Be(StjSerializer.Deserialize<OccurredAt>("\"2024-06-01T12:30:45\"").Value);
        JsonConvert.DeserializeObject<OccurredAt>("\"2024-06-01T12:30:45+02:00\"", Recommended).Value.Offset
            .Should().Be(paris.Offset);
        JsonConvert.DeserializeObject<OccurredAt>("\"2024-06-01T12:30:45+02:00\"", withOffset).Value.Offset
            .Should().Be(paris.Offset);

        var converted = () => JsonConvert.DeserializeObject<OccurredAt>("\"2024-06-01T12:30:45+02:00\"", Defaults);
        var reinterpreted = () => JsonConvert.DeserializeObject<RecordedAt>("\"2024-06-01T12:30:45Z\"", withOffset);

        converted.Should().Throw<JsonSerializationException>().WithMessage("*OccurredAt*DateParseHandling*");
        reinterpreted.Should().Throw<JsonSerializationException>().WithMessage("*RecordedAt*DateParseHandling*");
    }

    /// <summary>
    /// A DateTimeOffset is written with its offset, <c>+00:00</c> for UTC, never with <c>Z</c>. Under the default
    /// date handling Newtonsoft.Json converts any text with an offset to local time before the converter sees it,
    /// and the offset is gone: the value object refuses it, its own output included, whatever the offset.
    /// </summary>
    /// <param name="name">The offset of the instant.</param>
    [Theory]
    [MemberData(nameof(EveryInstant))]
    public void A_DateTimeOffset_value_object_refuses_its_own_output_under_the_default_date_handling(string name)
    {
        var instant = Instants[name];

        var json = JsonConvert.SerializeObject(instant, Defaults);
        var read = () => JsonConvert.DeserializeObject<OccurredAt>(json, Defaults);

        json.Should().MatchRegex("""^"2024-06-01T12:30:45[+-]\d\d:\d\d"$""");
        read.Should().Throw<JsonSerializationException>().WithMessage("*OccurredAt*DateParseHandling to None*")
            .Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);
    }

    [Theory]
    [MemberData(nameof(EveryInstant))]
    public void A_DateTimeOffset_value_object_reads_back_its_own_output_when_strings_are_left_alone(string name)
    {
        var instant = Instants[name];
        var settings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
            Converters = { new ValueObjectConverter() },
        };

        var read = JsonConvert.DeserializeObject<OccurredAt>(JsonConvert.SerializeObject(instant, settings), settings);

        read.Should().Be(instant);
        read.Value.Offset.Should().Be(instant.Value.Offset);
    }

    [Theory]
    [InlineData("12", typeof(Label), "Integer")]
    [InlineData("{}", typeof(Iban), "StartObject")]
    [InlineData("5", typeof(LedgerBalance), "Integer")]
    [InlineData("true", typeof(CustomerId), "Boolean")]
    public void A_value_written_as_a_string_refuses_a_token_of_another_kind(string json, Type type, string found)
    {
        var act = () => JsonConvert.DeserializeObject(json, type, Defaults);

        act.Should().Throw<JsonSerializationException>()
            .WithMessage($"Expected a JSON string for {type.Name} but found {found}.")
            .Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);
    }

    [Fact]
    public void Text_is_read_through_the_rules_of_the_value_object()
    {
        var wrongDigits = () => JsonConvert.DeserializeObject<Iban>("\"FR7630006000011234567890188\"", Defaults);
        var notADate = () => JsonConvert.DeserializeObject<RecordedAt>("\"yesterday\"", Recommended);
        var tooEarly = () => JsonConvert.DeserializeObject<RecordedAt>("\"1999-01-01T00:00:00\"", Recommended);

        JsonConvert.DeserializeObject<Iban>("\"fr76 3000 6000 0112 3456 7890 189\"", Defaults).Value
            .Should().Be("FR7630006000011234567890189");
        wrongDigits.Should().Throw<JsonSerializationException>().WithMessage("The value is not a valid Iban: *check digits*")
            .Which.ShouldCarry(ValueObjectErrorCodes.InvalidFormat);
        notADate.Should().Throw<JsonSerializationException>().WithMessage("The value is not a valid RecordedAt: *not a valid*DateTime*")
            .Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);
        tooEarly.Should().Throw<JsonSerializationException>().WithMessage("The value is not a valid RecordedAt: *greater than*")
            .Which.ShouldCarry(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// Newtonsoft.Json writes a DateTime in the form its settings ask for; a value object is written as
    /// System.Text.Json writes it, whatever they say.
    /// </summary>
    [Fact]
    public void A_date_is_written_as_System_Text_Json_writes_it_whatever_the_date_settings()
    {
        var settings = new JsonSerializerSettings
        {
            DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Local,
            Converters = { new ValueObjectConverter() },
        };
        var recorded = RecordedAt.Create(new DateTime(2024, 6, 1, 12, 30, 45, 123, DateTimeKind.Utc));

        JsonConvert.SerializeObject(recorded, settings).Should().Be("\"2024-06-01T12:30:45.123Z\"");
    }

    /// <summary>
    /// <c>AddValueObjects</c> adds the converter once, hands it the text of every string, and reads every real as a
    /// decimal, so a decimal value object keeps its digits and its scale: under the default float handling, a stored
    /// <c>12.50</c> reads back as 12.5, and <c>1234567890123456789.12</c> as 1234567890123456800.
    /// </summary>
    [Fact]
    public void AddValueObjects_adds_the_converter_once_with_the_settings_it_reads_best_under()
    {
        var enums = new Newtonsoft.Json.Converters.StringEnumConverter();
        var settings = new JsonSerializerSettings { Converters = { enums } }.AddValueObjects().AddValueObjects();
        var nothing = () => ((JsonSerializerSettings)null!).AddValueObjects();

        settings.Converters.Should().HaveCount(2).And.Contain(enums);
        settings.Converters.OfType<ValueObjectConverter>().Should().ContainSingle();
        settings.DateParseHandling.Should().Be(DateParseHandling.None);
        settings.FloatParseHandling.Should().Be(FloatParseHandling.Decimal);
        JsonConvert.DeserializeObject<TransferLimit>("12.50", settings).ToString().Should().Be("12.50");
        JsonConvert.DeserializeObject<TransferLimit>("12.50", Defaults).ToString().Should().Be("12.5");
        JsonConvert.DeserializeObject<Amount>("1234567890123456789.12", settings).Value.Should().Be(1234567890123456789.12m);
        JsonConvert.DeserializeObject<Amount>("1234567890123456789.12", Defaults).Value.Should().Be(1234567890123456800m);
        JsonConvert.DeserializeObject<Label>("\"2024-05-17T10:00:00Z\"", settings).Value.Should().Be("2024-05-17T10:00:00Z");
        nothing.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    /// <summary>
    /// Every number of the payload is read as a decimal under <see cref="FloatParseHandling.Decimal"/>, and a double
    /// beyond the range of a decimal then makes the reader throw: a payload carrying one keeps its float handling.
    /// </summary>
    [Fact]
    public void AddValueObjects_leaves_the_float_handling_alone_when_reals_are_not_decimal()
    {
        var settings = new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Double }.AddValueObjects(decimalReals: false);
        var decimals = new JsonSerializerSettings().AddValueObjects();
        var large = () => JsonConvert.DeserializeObject<Mass>("1E+30", decimals);

        settings.FloatParseHandling.Should().Be(FloatParseHandling.Double);
        settings.DateParseHandling.Should().Be(DateParseHandling.None);
        settings.Converters.OfType<ValueObjectConverter>().Should().ContainSingle();
        JsonConvert.DeserializeObject<Mass>("1E+30", settings).Should().Be(Mass.Create(1e30));
        large.Should().Throw<JsonReaderException>();
    }

    /// <summary>
    /// The resolver answers every value object, and its nullable type, with a contract of its own carrying the converter,
    /// the same instance each time, under which a serializer given the resolver alone writes and reads what one given
    /// the converter in its settings does.
    /// </summary>
    /// <param name="name">The case.</param>
    [Theory]
    [MemberData(nameof(Every))]
    public void The_resolver_gives_every_value_object_and_its_nullable_type_a_contract_carrying_the_converter(string name)
    {
        var value = EveryUnderlyingType[name];
        var resolver = new ValueObjectContractResolver();
        var settings = new JsonSerializerSettings
        {
            ContractResolver = resolver,
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
        };

        foreach (var type in new[] { value.GetType(), typeof(Nullable<>).MakeGenericType(value.GetType()) })
        {
            var contract = resolver.ResolveContract(type);

            contract.Should().BeOfType<JsonStringContract>().Which.Converter.Should().BeOfType<ValueObjectConverter>();
            resolver.ResolveContract(type).Should().BeSameAs(contract);

            var json = JsonConvert.SerializeObject(value, type, settings);
            json.Should().Be(JsonConvert.SerializeObject(value, type, Recommended));
            JsonConvert.DeserializeObject(json, type, settings).Should().Be(value);
        }

        JsonConvert.DeserializeObject("null", typeof(Nullable<>).MakeGenericType(value.GetType()), settings).Should().BeNull();
    }

    /// <summary>
    /// The resolver claims what the converter claims, through the registry: a construction of a generic value object
    /// and value objects written by hand, nullable or not, whose refusals are the converter's, with their codes.
    /// </summary>
    [Fact]
    public void The_resolver_claims_a_generic_construction_and_value_objects_written_by_hand()
    {
        var resolver = new ValueObjectContractResolver();
        var settings = new JsonSerializerSettings { ContractResolver = resolver };
        var quantity = () => JsonConvert.DeserializeObject<ResolvedHolder>("""{"Qty":1001}""", settings);
        var code = () => JsonConvert.DeserializeObject<ResolvedHolder>("""{"Code":"x1"}""", settings);
        var missing = () => JsonConvert.DeserializeObject<ResolvedHolder>("""{"Qty":null}""", settings);

        foreach (var type in new[] { typeof(Reference<ResolvedHolder>), typeof(HandWrittenCode), typeof(HandWrittenLink?) })
        {
            resolver.ResolveContract(type).Converter.Should().BeOfType<ValueObjectConverter>("{0} is a value object", type);
        }

        var holder = JsonConvert.DeserializeObject<ResolvedHolder>(
            """{"Qty":7,"Maybe":null,"Code":"abc","Ref":" po-9 ","Link":"https://example.com/a"}""",
            settings)!;
        JsonConvert.SerializeObject(holder, settings).Should()
            .Be("""{"Qty":7,"Maybe":null,"Code":"ABC","Ref":"PO-9","Link":"https://example.com/a"}""");
        quantity.Should().Throw<JsonSerializationException>().WithMessage("The value is not a valid Quantity: *")
            .Which.ShouldCarry(ValueObjectErrorCodes.OutOfRange);
        code.Should().Throw<JsonSerializationException>()
            .WithMessage("The value is not a valid HandWrittenCode: A code holds ASCII letters only.")
            .Which.ShouldCarry(ValueObjectErrorCodes.InvalidFormat);
        missing.Should().Throw<JsonSerializationException>().WithMessage("Cannot convert null to 'Quantity'.")
            .Which.ShouldCarry(ValueObjectErrorCodes.Required);
    }

    /// <summary>
    /// A contract of the wrapped resolver that carries a converter already, the one a <c>[JsonConverter]</c> on the type
    /// names, is answered as it is: that converter writes the value object.
    /// </summary>
    [Fact]
    public void The_resolver_keeps_the_converter_a_value_object_names_on_its_type()
    {
        var inner = new DefaultContractResolver();
        var resolver = new ValueObjectContractResolver(inner);

        var contract = resolver.ResolveContract(typeof(PrefixedShelf));

        contract.Should().BeSameAs(inner.ResolveContract(typeof(PrefixedShelf)));
        contract.Converter.Should().BeOfType<PrefixedShelfConverter>();
        JsonConvert.SerializeObject(new ShelfHolder { Shelf = PrefixedShelf.Create(7) }, new JsonSerializerSettings { ContractResolver = resolver })
            .Should().Be("""{"Shelf":"S7"}""");
    }

    /// <summary>
    /// A value object carrying Newtonsoft.Json's <c>[JsonObject]</c>, which <c>VO0043</c> refuses, gets an object
    /// contract carrying no converter from the wrapped resolver, so the resolver answers it with its own: the resolver
    /// alone writes and reads the value through its rules, where a serializer without it writes an object holding
    /// <c>Value</c>, as the documentation states.
    /// </summary>
    [Fact]
    public void The_resolver_writes_a_value_object_carrying_JsonObject_as_its_value()
    {
        var settings = new JsonSerializerSettings { ContractResolver = new ValueObjectContractResolver() };
        var holder = new ObjectShapedHolder { Code = ObjectShapedCode.Create("ab") };
        var tooLong = () => JsonConvert.DeserializeObject<ObjectShapedHolder>("""{"Code":"abcd"}""", settings);

        JsonConvert.SerializeObject(holder).Should().Be("""{"Code":{"Value":"ab"}}""");
        JsonConvert.SerializeObject(holder, settings).Should().Be("""{"Code":"ab"}""");
        JsonConvert.DeserializeObject<ObjectShapedHolder>("""{"Code":"cd"}""", settings)!.Code.Should().Be(ObjectShapedCode.Create("cd"));
        tooLong.Should().Throw<JsonSerializationException>().Which.ShouldCarry(ValueObjectErrorCodes.TooLong);
    }

    /// <summary>
    /// The resolver never changes a contract of the resolver it wraps, whose cache may be shared by every instance of its
    /// type, as the camel-case resolver's is: the wrapped resolver still answers a value object without the converter,
    /// and a serializer over it alone writes a value object as its type converter's text.
    /// </summary>
    [Fact]
    public void The_resolver_leaves_the_contracts_of_the_resolver_it_wraps_as_they_are()
    {
        var inner = new DefaultContractResolver();
        var camelCase = new ValueObjectContractResolver(new CamelCasePropertyNamesContractResolver());

        new ValueObjectContractResolver(inner).ResolveContract(typeof(Quantity));
        camelCase.ResolveContract(typeof(Quantity)).Converter.Should().BeOfType<ValueObjectConverter>();

        inner.ResolveContract(typeof(Quantity)).Converter.Should().BeNull();
        new CamelCasePropertyNamesContractResolver().ResolveContract(typeof(Quantity)).Converter.Should().BeNull();
        JsonConvert.SerializeObject(new ApartHolder { Qty = Quantity.Create(7) }, new JsonSerializerSettings { ContractResolver = inner })
            .Should().Be("""{"Qty":"7"}""");
    }

    /// <summary>
    /// The contract of a type holding value objects is the wrapped resolver's, on which Newtonsoft.Json keeps the
    /// contract of each member the first time a serializer meets it: a serializer over the same wrapped resolver then
    /// writes those members as the first one did, in either order, the limit the documentation tells a resolver of its
    /// own for.
    /// </summary>
    [Fact]
    public void A_serializer_sharing_the_wrapped_resolver_sees_the_member_contracts_the_first_one_cached()
    {
        var shared = new DefaultContractResolver();
        var wrappedSettings = new JsonSerializerSettings { ContractResolver = new ValueObjectContractResolver(shared) };
        var plainSettings = new JsonSerializerSettings { ContractResolver = shared };
        var wrappedFirst = new SharedHolder { Qty = Quantity.Create(7) };
        var plainFirst = new OtherSharedHolder { Qty = Quantity.Create(7) };

        JsonConvert.SerializeObject(wrappedFirst, wrappedSettings).Should().Be("""{"Qty":7}""");
        JsonConvert.SerializeObject(wrappedFirst, plainSettings).Should().Be("""{"Qty":7}""", "the plain serializer took the member contract the wrapped one cached");
        JsonConvert.SerializeObject(plainFirst, plainSettings).Should().Be("""{"Qty":"7"}""");
        JsonConvert.SerializeObject(plainFirst, wrappedSettings).Should().Be("""{"Qty":"7"}""", "the wrapped serializer took the member contract the plain one cached");
    }

    /// <summary>
    /// Any type but a value object is the wrapped resolver's, its contract handed back as it is.
    /// </summary>
    [Fact]
    public void The_resolver_answers_any_other_type_with_the_contract_of_the_resolver_it_wraps()
    {
        var inner = new DefaultContractResolver();
        var resolver = new ValueObjectContractResolver(inner);

        resolver.Inner.Should().BeSameAs(inner);
        new ValueObjectContractResolver().Inner.Should().BeOfType<DefaultContractResolver>();
        resolver.ResolveContract(typeof(string)).Should().BeSameAs(inner.ResolveContract(typeof(string)));
        resolver.ResolveContract(typeof(ResolvedHolder)).Should().BeSameAs(inner.ResolveContract(typeof(ResolvedHolder)));
    }

    /// <summary>
    /// A contract's converter wins over the converters the settings name, so an application's own converter for a value
    /// object in the settings loses to the resolver's, as the documentation states.
    /// </summary>
    [Fact]
    public void The_converter_of_a_contract_wins_over_a_converter_of_the_settings()
    {
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new ValueObjectContractResolver(),
            Converters = { new QuantityAsText() },
        };

        JsonConvert.SerializeObject(new ApartHolder { Qty = Quantity.Create(7) }, settings).Should().Be("""{"Qty":7}""");
        JsonConvert.SerializeObject(new ApartHolder { Qty = Quantity.Create(7) }, new JsonSerializerSettings { Converters = { new QuantityAsText() } })
            .Should().Be("""{"Qty":"Q7"}""");
    }

    [Fact]
    public void The_resolver_refuses_a_null_resolver_or_type()
    {
        var resolver = () => new ValueObjectContractResolver(null!);
        var contract = () => new ValueObjectContractResolver().ResolveContract(null!);

        resolver.Should().Throw<ArgumentNullException>().WithParameterName("inner");
        contract.Should().Throw<ArgumentNullException>().WithParameterName("type");
    }

    /// <summary>
    /// Given to a JSON Patch document outside MVC, as the documentation shows, the resolver is what JSON Patch converts a
    /// value with: a value object written by hand, which has no type converter, and a <c>DateTimeOffset</c> in UTC are
    /// applied, where the document's own resolver refuses them. A value its rules refuse is still reported in JSON
    /// Patch's terms, which quote the value and carry no code, the limit the documentation states.
    /// </summary>
    [Fact]
    public void A_JSON_Patch_document_outside_MVC_converts_its_values_through_the_resolver()
    {
        const string Document = """
            [{"op":"replace","path":"/code","value":"xyz"},{"op":"replace","path":"/at","value":"2026-10-14T08:00:00Z"},
             {"op":"replace","path":"/qty","value":1001}]
            """;
        var wrapped = JsonConvert.DeserializeObject<JsonPatchDocument<ResolvedPatchTarget>>(Document)!;
        wrapped.ContractResolver = new ValueObjectContractResolver();
        var plain = JsonConvert.DeserializeObject<JsonPatchDocument<ResolvedPatchTarget>>(Document)!;
        var target = new ResolvedPatchTarget();
        var unwrapped = new ResolvedPatchTarget();
        var errors = new List<JsonPatchError>();
        var plainErrors = new List<string>();

        wrapped.ApplyTo(target, errors.Add);
        plain.ApplyTo(unwrapped, error => plainErrors.Add(error.ErrorMessage));

        target.Code.Should().Be(HandWrittenCode.Create("XYZ"));
        target.At.Should().Be(OccurredAt.Create(new DateTimeOffset(2026, 10, 14, 8, 0, 0, TimeSpan.Zero)));
        errors.Should().ContainSingle().Which.ErrorMessage.Should().Be("The value '1001' is invalid for target location.");
        plainErrors.Should().Contain("The value 'xyz' is invalid for target location.");
        unwrapped.Code.IsDefault.Should().BeTrue();
    }

    /// <summary>
    /// JSON Patch hands every document it reads one resolver, through which it reads an object value and its members: a
    /// document applied through it alone leaves the contract of a member it read, without the converter, on the object's
    /// type, which a document wrapping that resolver then reads the member with, while a wrapper read first applies the
    /// value, and a resolver of the application's own always does, as the documentation says.
    /// </summary>
    [Fact]
    public void A_JSON_Patch_document_wrapping_its_own_resolver_reads_the_member_contracts_another_document_cached()
    {
        const string Document = """[{"op":"replace","path":"/home","value":{"code":"abc"}}]""";
        var plain = JsonConvert.DeserializeObject<JsonPatchDocument<PatchedHome<PlainFirstHome>>>(Document)!;
        var wrappedAfter = JsonConvert.DeserializeObject<JsonPatchDocument<PatchedHome<PlainFirstHome>>>(Document)!;
        var wrappedAlone = JsonConvert.DeserializeObject<JsonPatchDocument<PatchedHome<WrappedAloneHome>>>(Document)!;
        var own = JsonConvert.DeserializeObject<JsonPatchDocument<PatchedHome<PlainFirstHome>>>(Document)!;
        var shared = plain.ContractResolver;
        wrappedAfter.ContractResolver = new ValueObjectContractResolver(wrappedAfter.ContractResolver);
        wrappedAlone.ContractResolver = new ValueObjectContractResolver(wrappedAlone.ContractResolver);
        own.ContractResolver = new ValueObjectContractResolver();
        var plainErrors = new List<string>();
        var wrappedAfterErrors = new List<string>();
        var alone = new PatchedHome<WrappedAloneHome>();
        var owned = new PatchedHome<PlainFirstHome>();

        plain.ApplyTo(new PatchedHome<PlainFirstHome>(), error => plainErrors.Add(error.ErrorMessage));
        wrappedAfter.ApplyTo(new PatchedHome<PlainFirstHome>(), error => wrappedAfterErrors.Add(error.ErrorMessage));
        wrappedAlone.ApplyTo(alone);
        own.ApplyTo(owned);

        ((ValueObjectContractResolver)wrappedAfter.ContractResolver).Inner.Should().BeSameAs(shared, "JSON Patch hands every document one resolver");
        plainErrors.Should().ContainSingle().Which.Should().StartWith("The value '{");
        wrappedAfterErrors.Should().Equal(plainErrors);
        alone.Home!.Code.Should().Be(HandWrittenCode.Create("ABC"));
        owned.Home!.Code.Should().Be(HandWrittenCode.Create("ABC"));
    }

    private sealed record Payment(Iban Account, Amount Total, CustomerId Customer, BirthDate? Birth, Quantity Lines);

    /// <summary>A member holding a value object written by hand over a type Newtonsoft.Json reads itself.</summary>
    private sealed class Linked
    {
        public HandWrittenLink Link { get; set; }
    }

    /// <summary>A shelf naming a converter of the application's own on its type, which the resolver keeps.</summary>
    [ValueObject<int>]
    [Newtonsoft.Json.JsonConverter(typeof(PrefixedShelfConverter))]
    public readonly partial struct PrefixedShelf;

    /// <summary>A code carrying Newtonsoft.Json's <c>[JsonObject]</c>, which the resolver overrides.</summary>
    [ValueObject<string>(MaxLength = 3)]
#pragma warning disable VO0043 // The trap the resolver overrides, on purpose: Newtonsoft.Json alone writes it as an object.
    [Newtonsoft.Json.JsonObject]
#pragma warning restore VO0043
    public readonly partial struct ObjectShapedCode;

    /// <summary>Members of every kind the resolver answers, of a type no other test serializes.</summary>
    private sealed class ResolvedHolder
    {
        public Quantity Qty { get; set; }

        public Quantity? Maybe { get; set; }

        public HandWrittenCode? Code { get; set; }

        public Reference<ResolvedHolder>? Ref { get; set; }

        public HandWrittenLink? Link { get; set; }
    }

    /// <summary>A member holding the shelf.</summary>
    private sealed class ShelfHolder
    {
        public PrefixedShelf Shelf { get; set; }
    }

    /// <summary>A member holding the code carrying <c>[JsonObject]</c>.</summary>
    private sealed class ObjectShapedHolder
    {
        public ObjectShapedCode Code { get; set; }
    }

    /// <summary>A member a serializer over the wrapped resolver alone writes.</summary>
    private sealed class ApartHolder
    {
        public Quantity Qty { get; set; }
    }

    /// <summary>A member two serializers sharing a wrapped resolver write, the wrapped one first.</summary>
    private sealed class SharedHolder
    {
        public Quantity Qty { get; set; }
    }

    /// <summary>A member two serializers sharing a wrapped resolver write, the plain one first.</summary>
    private sealed class OtherSharedHolder
    {
        public Quantity Qty { get; set; }
    }

    /// <summary>The object a JSON Patch document applied outside MVC patches, which no other test patches.</summary>
    private sealed class ResolvedPatchTarget
    {
        public HandWrittenCode Code { get; set; }

        public OccurredAt At { get; set; }

        public Quantity Qty { get; set; }
    }

    /// <summary>An object holding a home, an object value a JSON Patch document replaces whole.</summary>
    /// <typeparam name="THome">The home, a type of each case's own.</typeparam>
    private sealed class PatchedHome<THome>
        where THome : class
    {
        public THome? Home { get; set; }
    }

    /// <summary>A home a document applied through JSON Patch's resolver alone reads first.</summary>
    private sealed class PlainFirstHome
    {
        public HandWrittenCode Code { get; set; }
    }

    /// <summary>A home only a document wrapping JSON Patch's resolver reads.</summary>
    private sealed class WrappedAloneHome
    {
        public HandWrittenCode Code { get; set; }
    }

    /// <summary>Writes a shelf with a prefix, and reads nothing.</summary>
    private sealed class PrefixedShelfConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(PrefixedShelf);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            => throw new NotSupportedException();

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            => writer.WriteValue($"S{((PrefixedShelf)value!).Value.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>Writes a quantity as text with a prefix, a converter of the application's own, and reads nothing.</summary>
    private sealed class QuantityAsText : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Quantity);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
            => throw new NotSupportedException();

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            => writer.WriteValue($"Q{((Quantity)value!).Value.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>A member naming the converter although it holds no value object.</summary>
    private sealed class Tagged
    {
        [JsonConverter(typeof(ValueObjectConverter))]
        public string? Name { get; set; }
    }
}
