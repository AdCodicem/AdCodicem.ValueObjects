using System.Globalization;
using AdCodicem.ValueObjects.NewtonsoftJson;
using Newtonsoft.Json;
using StjSerializer = System.Text.Json.JsonSerializer;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// The Newtonsoft.Json converter, held to the System.Text.Json converter the generator emits: same rules, same wire.
/// </summary>
public class NewtonsoftJsonTests
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

    public static TheoryData<string> Every => [.. EveryUnderlyingType.Keys];

    [Theory]
    [MemberData(nameof(Every))]
    public void Newtonsoft_writes_what_System_Text_Json_writes(string name)
    {
        var value = EveryUnderlyingType[name];

        var newtonsoft = JsonConvert.SerializeObject(value, value.GetType(), Defaults);

        newtonsoft.Should().Be(StjSerializer.Serialize(value, value.GetType()));
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

    [Theory]
    [InlineData("70000", typeof(Quantity))]
    [InlineData("-1", typeof(Score))]
    [InlineData("-1", typeof(ByteCount))]
    [InlineData("18446744073709551616", typeof(ByteCount))]
    [InlineData("1.5", typeof(PageNumber))]
    [InlineData("2.0", typeof(PageNumber))]
    [InlineData("NaN", typeof(Latitude))]
    [InlineData("1e400", typeof(Latitude))]
    public void A_number_the_underlying_type_cannot_hold_is_refused_as_JSON(string json, Type type)
    {
        var act = () => JsonConvert.DeserializeObject(json, type, Defaults);

        act.Should().Throw<JsonSerializationException>().WithMessage($"The value could not be read as {type.Name}.");
    }

    /// <summary>
    /// System.Text.Json reads a number from a string only when its options allow it, and Newtonsoft.Json has no such
    /// option to honour.
    /// </summary>
    [Theory]
    [InlineData("\"12\"", typeof(Amount), "number", "String")]
    [InlineData("{}", typeof(Amount), "number", "StartObject")]
    [InlineData("true", typeof(PageNumber), "number", "Boolean")]
    [InlineData("\"true\"", typeof(Consent), "boolean", "String")]
    [InlineData("1", typeof(Consent), "boolean", "Integer")]
    public void A_token_of_another_kind_is_refused_rather_than_coerced(string json, Type type, string expected, string found)
    {
        var act = () => JsonConvert.DeserializeObject(json, type, Defaults);

        act.Should().Throw<JsonSerializationException>()
            .WithMessage($"Expected a JSON {expected} for {type.Name} but found {found}.");
    }

    [Fact]
    public void A_number_is_read_through_the_rules_of_the_value_object()
    {
        var act = () => JsonConvert.DeserializeObject<Amount>("-1", Defaults);

        JsonConvert.DeserializeObject<Amount>("12.345", Recommended).Value.Should().Be(12.34m, "the amount normalizes");
        JsonConvert.DeserializeObject<Consent>("false", Defaults).Value.Should().BeFalse();
        act.Should().Throw<JsonSerializationException>().WithMessage("The value is not a valid Amount: *greater than or equal to 0*");
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

        act.Should().Throw<JsonSerializationException>().WithMessage("*Label*DateParseHandling*None*");
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

    [Theory]
    [InlineData("12", typeof(Label), "Integer")]
    [InlineData("{}", typeof(Iban), "StartObject")]
    [InlineData("5", typeof(LedgerBalance), "Integer")]
    [InlineData("true", typeof(CustomerId), "Boolean")]
    public void A_value_written_as_a_string_refuses_a_token_of_another_kind(string json, Type type, string found)
    {
        var act = () => JsonConvert.DeserializeObject(json, type, Defaults);

        act.Should().Throw<JsonSerializationException>()
            .WithMessage($"Expected a JSON string for {type.Name} but found {found}.");
    }

    [Fact]
    public void Text_is_read_through_the_rules_of_the_value_object()
    {
        var wrongDigits = () => JsonConvert.DeserializeObject<Iban>("\"FR7630006000011234567890188\"", Defaults);
        var notADate = () => JsonConvert.DeserializeObject<RecordedAt>("\"yesterday\"", Recommended);
        var tooEarly = () => JsonConvert.DeserializeObject<RecordedAt>("\"1999-01-01T00:00:00\"", Recommended);

        JsonConvert.DeserializeObject<Iban>("\"fr76 3000 6000 0112 3456 7890 189\"", Defaults).Value
            .Should().Be("FR7630006000011234567890189");
        wrongDigits.Should().Throw<JsonSerializationException>().WithMessage("The value is not a valid Iban: *check digits*");
        notADate.Should().Throw<JsonSerializationException>().WithMessage("The value is not a valid RecordedAt: *not a valid*DateTime*");
        tooEarly.Should().Throw<JsonSerializationException>().WithMessage("The value is not a valid RecordedAt: *greater than*");
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
}
