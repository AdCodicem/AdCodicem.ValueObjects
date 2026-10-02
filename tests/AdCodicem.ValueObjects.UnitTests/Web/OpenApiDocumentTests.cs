using System.Text.Json;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// What the OpenAPI document says of each value object, read from a document ASP.NET Core built for the domain.
/// </summary>
/// <param name="document">The document.</param>
public class OpenApiDocumentTests(OpenApiDocument document) : IClassFixture<OpenApiDocument>
{
    /// <summary>One instance of value objects over each underlying type, keyed by the name of the type.</summary>
    private static readonly Dictionary<string, object> Instances = new()
    {
        [nameof(Consent)] = Consent.Create(true),
        [nameof(TermsAccepted)] = TermsAccepted.Accepted,
        [nameof(Grade)] = Grade.Create('A'),
        [nameof(Adjustment)] = Adjustment.Create(-1),
        [nameof(Score)] = Score.Create(1),
        [nameof(Quantity)] = Quantity.Create(1),
        [nameof(Port)] = Port.Create(443),
        [nameof(PageNumber)] = PageNumber.Create(1),
        [nameof(SequenceNumber)] = SequenceNumber.Create(1),
        [nameof(FileSize)] = FileSize.Create(1),
        [nameof(ByteCount)] = ByteCount.Create(1),
        [nameof(LedgerBalance)] = LedgerBalance.Create(1),
        [nameof(Fingerprint)] = Fingerprint.Create(1),
        [nameof(Amount)] = Amount.Create(1m),
        [nameof(Latitude)] = Latitude.Create(1d),
        [nameof(Ratio)] = Ratio.Create(0.5f),
        [nameof(CustomerId)] = CustomerId.New(),
        [nameof(BirthDate)] = BirthDate.Create(new DateOnly(1990, 1, 1)),
        [nameof(OpeningTime)] = OpeningTime.Create(new TimeOnly(7, 0)),
        [nameof(RecordedAt)] = RecordedAt.Create(new DateTime(2001, 1, 1)),
        [nameof(OccurredAt)] = OccurredAt.Create(new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        [nameof(Duration)] = Duration.Create(TimeSpan.FromHours(1)),
        [nameof(Iban)] = Iban.Create("FR7630006000011234567890189"),
    };

    /// <summary>The members of each closed value set of the domain, keyed by the name of the type.</summary>
    private static readonly Dictionary<string, object[]> ClosedSets = new()
    {
        [nameof(CountryCode)] = [CountryCode.France, CountryCode.Belgium, CountryCode.Luxembourg],
        [nameof(DocumentStatus)] = [DocumentStatus.Draft, DocumentStatus.Final],
        [nameof(Priority)] = [Priority.Low, Priority.High],
        [nameof(StorageQuota)] = [StorageQuota.Standard, StorageQuota.Large],
        [nameof(VatRate)] = [VatRate.Standard, VatRate.Reduced],
        [nameof(VoteWeight)] = [VoteWeight.Half, VoteWeight.Full],
        [nameof(Opacity)] = [Opacity.Translucent, Opacity.Opaque],
        [nameof(TermsAccepted)] = [TermsAccepted.Accepted],
        [nameof(HttpStatus)] = [HttpStatus.Ok, HttpStatus.NotFound],
        [nameof(BlockSize)] = [BlockSize.Small, BlockSize.Large],
        [nameof(CutOffDate)] = [CutOffDate.Epoch, CutOffDate.Millennium],
        [nameof(ShiftStart)] = [ShiftStart.Early, ShiftStart.Late],
        [nameof(LaunchMoment)] = [LaunchMoment.Launch, LaunchMoment.Relaunch],
        [nameof(Answer)] = [Answer.Yes, Answer.No],
    };

    public static TheoryData<string> EveryClosedSet => [.. ClosedSets.Keys];

    public static TheoryData<string> EveryUnderlyingType => [.. Instances.Keys];

    /// <summary>
    /// The schema's type is chosen from the underlying type, the payload's from the converter; a client trusts the
    /// one to describe the other. A 128-bit integer is a string on the wire, where a JSON number would lose digits.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryUnderlyingType))]
    public void A_value_object_is_documented_with_the_JSON_type_it_is_written_as(string name)
    {
        var instance = Instances[name];
        var written = JsonSerializer.SerializeToElement(instance, instance.GetType()).ValueKind;

        var documented = document.Schema(name).GetProperty("type").GetString();

        documented.Should().Be(written switch
        {
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Number => instance is Amount or Latitude or Ratio ? "number" : "integer",
            _ => "string",
        });
    }

    [Fact]
    public void An_object_shape_inferred_for_a_value_object_is_replaced_by_its_underlying_type()
    {
        var iban = document.Schema(nameof(Iban));

        iban.GetProperty("type").GetString().Should().Be("string");
        iban.TryGetProperty("properties", out _).Should().BeFalse("a value object is not an object on the wire");
        iban.TryGetProperty("required", out _).Should().BeFalse("a value object has no member to require");
    }

    [Fact]
    public void The_rules_declared_on_a_string_are_published_with_it()
    {
        var iban = document.Schema(nameof(Iban));

        iban.GetProperty("format").GetString().Should().Be("iban");
        iban.GetProperty("pattern").GetString().Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
        iban.GetProperty("minLength").GetInt32().Should().Be(15);
        iban.GetProperty("maxLength").GetInt32().Should().Be(34);
        iban.GetProperty("examples").EnumerateArray().Select(example => example.GetString())
            .Should().Equal("FR7630006000011234567890189");
    }

    /// <summary>
    /// An example is declared as text, and published as the type writes it: a client or a mock server checking the
    /// example against the schema finds a number where the schema says number, and a boolean where it says boolean.
    /// </summary>
    [Fact]
    public void An_example_is_published_as_the_type_writes_it()
    {
        var amount = document.Schema(nameof(Amount)).GetProperty("examples").EnumerateArray().Single();
        var port = document.Schema(nameof(Port)).GetProperty("examples").EnumerateArray().Single();
        var consent = document.Schema(nameof(Consent)).GetProperty("examples").EnumerateArray().Single();

        amount.ValueKind.Should().Be(JsonValueKind.Number);
        amount.GetRawText().Should().Be("1250.00");
        port.ValueKind.Should().Be(JsonValueKind.Number);
        port.GetUInt16().Should().Be(8080);
        consent.ValueKind.Should().Be(JsonValueKind.True);
    }

    /// <summary>
    /// The type's XML summary describes it unless the declaration says otherwise; what the declaration says wins.
    /// </summary>
    [Fact]
    public void A_declared_description_is_published_in_place_of_the_summary()
        => document.Schema(nameof(LedgerEntryId)).GetProperty("description").GetString()
            .Should().Be("Identifies one line of the ledger.");

    [Fact]
    public void A_type_that_is_no_value_object_keeps_the_schema_ASP_NET_Core_gave_it()
    {
        var body = document.Schema(nameof(EveryValueObject));

        body.GetProperty("type").GetString().Should().Be("object");
        body.GetProperty("properties").GetProperty("iban").GetProperty("$ref").GetString()
            .Should().Be("#/components/schemas/Iban");
    }

    /// <summary>
    /// A client validates a payload against the listed values, so each one must be the very JSON the type writes:
    /// a number of any width as a number, a date or a time in the round-trip form its converter writes.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryClosedSet))]
    public void A_closed_value_set_lists_its_values_as_the_type_writes_them(string name)
    {
        var listed = document.Schema(name).GetProperty("enum").EnumerateArray().Select(value => value.GetRawText());

        listed.Should().Equal(ClosedSets[name].Select(value => JsonSerializer.Serialize(value, value.GetType())));
    }

    [Fact]
    public void A_closed_set_of_narrow_integers_lists_numbers_and_one_of_dates_lists_their_ISO_form()
    {
        document.Schema(nameof(HttpStatus)).GetProperty("enum").EnumerateArray().Select(value => value.ValueKind)
            .Should().AllBeEquivalentTo(JsonValueKind.Number);
        document.Schema(nameof(HttpStatus)).GetProperty("enum")[1].GetInt16().Should().Be(404);
        document.Schema(nameof(CutOffDate)).GetProperty("enum")[0].GetString().Should().Be("2000-01-01");
    }

    [Fact]
    public void The_declared_bounds_of_a_number_are_published_as_numbers()
    {
        document.Schema(nameof(Percentage)).GetProperty("minimum").GetDecimal().Should().Be(0m);
        document.Schema(nameof(Percentage)).GetProperty("maximum").GetDecimal().Should().Be(100m);
        document.Schema(nameof(Quantity)).GetProperty("maximum").GetInt32().Should().Be(1000);
        document.Schema(nameof(Latitude)).GetProperty("minimum").GetDouble().Should().Be(-90d);
    }

    /// <summary>
    /// The attribute reads a double or float bound as a floating-point literal, exponent included, and the type
    /// enforces what it read. A double's bound may also lie beyond the range of decimal or below its precision,
    /// where reading it as a decimal would publish another value or none. A decimal bound takes no exponent.
    /// </summary>
    [Fact]
    public void A_bound_written_with_an_exponent_is_published_as_the_number_the_type_enforces()
    {
        document.Schema(nameof(Mass)).GetProperty("minimum").GetDouble().Should().Be(9.1e-31);
        document.Schema(nameof(Mass)).GetProperty("maximum").GetDouble().Should().Be(2e32);
        document.Schema(nameof(TransferLimit)).GetProperty("maximum").GetDecimal().Should().Be(1_000_000m);
        document.Schema(nameof(Luminance)).GetProperty("maximum").GetDouble().Should().Be(1500d);
    }

    [Fact]
    public void A_bound_that_is_no_number_is_left_out()
    {
        var born = document.Schema(nameof(BirthDate));

        born.TryGetProperty("minimum", out _).Should().BeFalse("a date is a string on the wire");
        born.TryGetProperty("maximum", out _).Should().BeFalse("a date is a string on the wire");
    }
}
