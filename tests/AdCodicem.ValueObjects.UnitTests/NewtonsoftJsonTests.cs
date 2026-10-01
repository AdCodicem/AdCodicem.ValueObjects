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
