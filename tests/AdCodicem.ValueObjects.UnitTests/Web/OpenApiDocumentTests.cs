using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Schema;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

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
        ["NoticeChannelOfAccountFilter"] = [NoticeChannel<AccountFilter>.Email, NoticeChannel<AccountFilter>.Sms],
    };

    public static TheoryData<string> EveryClosedSet => [.. ClosedSets.Keys];

    public static TheoryData<string> EveryUnderlyingType => [.. Instances.Keys];

    /// <summary>The value objects over a number the document describes, and their underlying types.</summary>
    public static TheoryData<string, Type> EveryNumber => new()
    {
        { nameof(Adjustment), typeof(sbyte) },
        { nameof(Score), typeof(byte) },
        { nameof(Quantity), typeof(short) },
        { nameof(Port), typeof(ushort) },
        { nameof(PageNumber), typeof(int) },
        { nameof(SequenceNumber), typeof(uint) },
        { nameof(FileSize), typeof(long) },
        { nameof(ByteCount), typeof(ulong) },
        { nameof(Amount), typeof(decimal) },
        { nameof(Latitude), typeof(double) },
        { nameof(Ratio), typeof(float) },
    };

    /// <summary>The options ASP.NET Core describes the wire with, unless an application changes them.</summary>
    private static JsonSerializerOptions WebOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver(),
    };

    /// <summary>
    /// The schema's type is chosen from the underlying type, the payload's from the converter; a client trusts the
    /// one to describe the other. A 128-bit integer is a string on the wire, where a JSON number would lose digits.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryUnderlyingType))]
    public void A_value_object_is_documented_with_the_JSON_type_it_is_written_as(string name)
    {
        var instance = Instances[name];
        var written = JsonSerializer.SerializeToElement(instance, instance.GetType(), WebOptions).ValueKind;

        var documented = Types(document.Schema(name).GetProperty("type"));

        documented.Should().Contain(written switch
        {
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Number => instance is Amount or Latitude or Ratio ? "number" : "integer",
            _ => "string",
        });
    }

    /// <summary>
    /// ASP.NET Core reads a number written as text by default, and documents a bare number as a number or a string held
    /// to a numeric pattern. A value object over a number reads what its underlying type reads, and is documented the
    /// same way, as System.Text.Json documents the underlying type under the same options.
    /// </summary>
    /// <param name="name">The value object.</param>
    /// <param name="underlying">Its underlying type.</param>
    [Theory]
    [MemberData(nameof(EveryNumber))]
    public void A_number_is_documented_as_its_underlying_type_is(string name, Type underlying)
    {
        var schema = document.Schema(name);
        var expected = JsonSchemaExporter.GetJsonSchemaAsNode(WebOptions, underlying);

        Types(schema.GetProperty("type")).Should().BeEquivalentTo(
            expected["type"]!.AsArray().Select(type => type!.GetValue<string>()));
        schema.GetProperty("pattern").GetString().Should().Be(expected["pattern"]!.GetValue<string>());
    }

    /// <summary>
    /// A duration goes on the wire in the invariant constant form, <c>01:30:00</c>, which JSON Schema's
    /// <c>duration</c> format, ISO 8601, does not describe: a client trusting that format would send <c>PT1H30M</c>,
    /// which the type refuses. It is documented as System.Text.Json documents a bare <see cref="TimeSpan"/>, a string
    /// held to that form, and what the document writes of it, its example and its bounds, matches the pattern.
    /// </summary>
    [Fact]
    public void A_duration_is_documented_in_the_form_it_is_written_in_rather_than_as_ISO_8601()
    {
        var duration = document.Schema(nameof(Duration));
        var expected = JsonSchemaExporter.GetJsonSchemaAsNode(WebOptions, typeof(TimeSpan));
        string[] written =
        [
            duration.GetProperty("examples")[0].GetString()!,
            duration.GetProperty("x-minimum").GetString()!,
            duration.GetProperty("x-maximum").GetString()!,
        ];

        duration.TryGetProperty("format", out _).Should().BeFalse("the duration format is ISO 8601, which the type does not read");
        duration.GetProperty("type").GetString().Should().Be("string");
        duration.GetProperty("pattern").GetString().Should().Be(expected["pattern"]!.GetValue<string>());
        written.Should().Equal("01:30:00", "00:00:00", "1.00:00:00");
        written.Should().AllSatisfy(value => value.Should().MatchRegex(duration.GetProperty("pattern").GetString()!));
        JsonSerializer.Serialize(Duration.Create(new TimeSpan(0, 23, 59, 59, 999)), WebOptions)
            .Trim('"').Should().MatchRegex(duration.GetProperty("pattern").GetString()!);
    }

    /// <summary>
    /// A time of day is written without an offset, and so is a <see cref="DateTime"/> of an unspecified kind, which
    /// RFC 3339 requires of the <c>time</c> and <c>date-time</c> formats: a client or a gateway taking either format at
    /// its word would refuse what the server writes. Each is documented with the pattern of the form it is written in,
    /// the one the JSON Schema of the Json package publishes, which its bounds, its known values and what it writes match.
    /// A date and an instant with its offset keep their format.
    /// </summary>
    /// <param name="name">The value object.</param>
    [Theory]
    [InlineData(nameof(OpeningTime))]
    [InlineData(nameof(ShiftStart))]
    [InlineData(nameof(RecordedAt))]
    public void A_time_of_day_and_a_DateTime_are_documented_in_the_form_they_are_written_in_rather_than_as_RFC_3339(string name)
    {
        var schema = document.Schema(name);
        var type = Instances.TryGetValue(name, out var instance) ? instance.GetType() : ClosedSets[name][0].GetType();
        var exported = JsonSchemaExporter.GetJsonSchemaAsNode(
            WebOptions,
            type,
            new JsonSchemaExporterOptions { TransformSchemaNode = ValueObjectJsonSchema.TransformSchemaNode });
        var pattern = schema.GetProperty("pattern").GetString()!;
        var written = (instance is null ? ClosedSets[name] : [instance])
            .Select(value => JsonSerializer.SerializeToElement(value, type, WebOptions).GetString()!)
            .Concat(schema.TryGetProperty("x-minimum", out var minimum) ? [minimum.GetString()!, schema.GetProperty("x-maximum").GetString()!] : [])
            .Concat(schema.TryGetProperty("enum", out var values) ? values.EnumerateArray().Select(value => value.GetString()!) : []);

        schema.TryGetProperty("format", out _).Should().BeFalse("RFC 3339 requires an offset the type does not write");
        schema.GetProperty("type").GetString().Should().Be("string");
        pattern.Should().Be(exported["pattern"]!.GetValue<string>());
        written.Should().NotBeEmpty().And.AllSatisfy(text => text.Should().MatchRegex(pattern));
        document.Schema(nameof(OccurredAt)).GetProperty("format").GetString().Should().Be("date-time");
        document.Schema(nameof(BirthDate)).GetProperty("format").GetString().Should().Be("date");
    }

    /// <summary>
    /// A character is written as a string of one character, which the schema states, as System.Text.Json documents a
    /// bare <see cref="char"/>.
    /// </summary>
    /// <param name="name">The value object.</param>
    [Theory]
    [InlineData(nameof(Grade))]
    [InlineData(nameof(Answer))]
    public void A_character_is_documented_as_a_string_of_one_character(string name)
    {
        var schema = document.Schema(name);
        var expected = JsonSchemaExporter.GetJsonSchemaAsNode(WebOptions, typeof(char));

        schema.GetProperty("minLength").GetInt32().Should().Be(1).And.Be(expected["minLength"]!.GetValue<int>());
        schema.GetProperty("maxLength").GetInt32().Should().Be(1).And.Be(expected["maxLength"]!.GetValue<int>());
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

    /// <summary>
    /// A client generator names the members of its enumeration after the raw values unless the document names them:
    /// <c>FR</c> where the server says <c>France</c>, and a value such as <c>01</c> is no identifier at all. Each
    /// closed set names its values after its known values, in the order of its <c>enum</c>, in the extension each
    /// generator reads: <c>x-enum-varnames</c> for openapi-generator and Scalar, <c>x-enumNames</c> for NSwag, and
    /// <c>x-ms-enum</c> for Kiota and AutoRest, which names the enumeration as the component is named, a construction
    /// of a generic value object included.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryClosedSet))]
    public void A_closed_value_set_names_its_values_for_the_clients_generated_from_the_document(string name)
    {
        var schema = document.Schema(name);
        var names = ClosedSets[name].Select(NameOf).ToArray();

        schema.GetProperty("x-enum-varnames").EnumerateArray().Select(each => each.GetString()).Should().Equal(names);
        schema.GetProperty("x-enumNames").EnumerateArray().Select(each => each.GetString()).Should().Equal(names);

        var named = schema.GetProperty("x-ms-enum");
        named.GetProperty("name").GetString().Should().Be(name, "the enumeration is named as the component is");
        named.GetProperty("modelAsString").GetBoolean().Should().BeFalse("the set is closed");
        var values = named.GetProperty("values").EnumerateArray().ToArray();
        values.Select(value => value.GetProperty("value").GetRawText())
            .Should().Equal(schema.GetProperty("enum").EnumerateArray().Select(value => value.GetRawText()));
        values.Select(value => value.GetProperty("name").GetString()).Should().Equal(names);
    }

    /// <summary>
    /// A value carries the description its known value declares, and none where it declares none: repeating the name
    /// would tell nothing. No description is written in the object form keyed by value, which NSwag refuses the whole
    /// document over, and an open value set, which lists no <c>enum</c>, names nothing either.
    /// </summary>
    [Fact]
    public void A_value_of_a_closed_set_carries_its_description_where_one_is_declared_and_nowhere_else()
    {
        var rates = document.Schema(nameof(VatRate)).GetProperty("x-ms-enum").GetProperty("values");
        var channels = document.Schema("NoticeChannelOfAccountFilter").GetProperty("x-ms-enum").GetProperty("values");

        rates[0].TryGetProperty("description", out _).Should().BeFalse("Standard declares no description");
        rates[1].GetProperty("description").GetString().Should().Be("Food, books and medicine.");
        channels[0].GetProperty("description").GetString().Should().Be("Sent to the address on file.");
        channels[1].TryGetProperty("description", out _).Should().BeFalse("Sms declares no description");

        foreach (var component in document.Schemas.EnumerateObject())
        {
            component.Value.TryGetProperty("x-enum-descriptions", out _).Should().BeFalse(component.Name);
            component.Value.TryGetProperty("x-enumDescriptions", out _).Should().BeFalse(component.Name);
        }

        document.Schema(nameof(PageNumber)).TryGetProperty("x-ms-enum", out _).Should().BeFalse("its known value is one of many");
        document.Schema(nameof(PageNumber)).TryGetProperty("x-enum-varnames", out _).Should().BeFalse("its known value is one of many");
    }

    /// <summary>
    /// An application may name its components its own way, and the enumeration is named as the component is, so that
    /// a client generator reading <c>x-ms-enum</c> and one reading the reference agree.
    /// </summary>
    [Fact]
    public async Task A_closed_value_set_is_named_as_the_application_names_its_component()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOpenApi(static options =>
        {
            options.CreateSchemaReferenceId = static info
                => info.Type == typeof(CountryCode) ? "Country" : OpenApiOptions.CreateDefaultSchemaReferenceId(info);
            options.AddValueObjects();
        });
        await using var application = builder.Build();
        application.MapPost("/countries", static ([FromBody] CountryCode country) => Results.Ok());
        application.MapOpenApi();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        var openApi = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);

        openApi.GetProperty("components").GetProperty("schemas").GetProperty("Country")
            .GetProperty("x-ms-enum").GetProperty("name").GetString().Should().Be("Country");
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

    /// <summary>
    /// JSON Schema applies <c>minimum</c> and <c>maximum</c> to numbers only. A value object written as a string - a
    /// 128-bit integer, a character, a date - carries its bounds in extensions instead, in the form the type writes
    /// them, and in a sentence of its description, after what the description already said.
    /// </summary>
    /// <param name="name">The value object.</param>
    /// <param name="minimum">Its minimum, as the type writes it.</param>
    /// <param name="maximum">Its maximum, as the type writes it.</param>
    [Theory]
    [InlineData(nameof(LedgerBalance), "-1000000000000000000000", "1000000000000000000000")]
    [InlineData(nameof(Grade), "A", "F")]
    [InlineData(nameof(BirthDate), "1900-01-01", "2100-12-31")]
    [InlineData(nameof(OpeningTime), "06:00:00.0000000", "12:00:00.0000000")]
    public void A_bound_of_a_value_object_written_as_a_string_is_an_extension_and_a_sentence(
        string name,
        string minimum,
        string maximum)
    {
        var schema = document.Schema(name);

        schema.TryGetProperty("minimum", out _).Should().BeFalse("JSON Schema applies minimum to numbers only");
        schema.TryGetProperty("maximum", out _).Should().BeFalse("JSON Schema applies maximum to numbers only");
        schema.GetProperty("x-minimum").GetString().Should().Be(minimum);
        schema.GetProperty("x-maximum").GetString().Should().Be(maximum);
        schema.GetProperty("description").GetString().Should().EndWith($"\n\nBetween {minimum} and {maximum}, inclusive.");
    }

    [Fact]
    public void A_single_bound_of_a_value_object_written_as_a_string_is_stated_alone()
    {
        var schema = document.Schema(nameof(OccurredAt));

        schema.GetProperty("x-minimum").GetString().Should().Be("2000-01-01T00:00:00+00:00");
        schema.TryGetProperty("x-maximum", out _).Should().BeFalse();
        schema.GetProperty("description").GetString().Should().EndWith("At least 2000-01-01T00:00:00+00:00.");
    }

    [Fact]
    public void A_bound_of_a_value_object_written_as_a_number_is_no_extension()
    {
        var percentage = document.Schema(nameof(Percentage));

        percentage.TryGetProperty("x-minimum", out _).Should().BeFalse();
        percentage.TryGetProperty("x-maximum", out _).Should().BeFalse();
    }

    /// <summary>
    /// A route, query or header parameter is bound from text, and ASP.NET Core hands its schema to the transformers
    /// as a string's. It is described as its value object nonetheless, in minimal APIs, in MVC actions, in a type
    /// gathered with <c>[AsParameters]</c> and in a model MVC binds from the query string, nullable or not: the very
    /// schema of the value object's component, in place or as a reference to it.
    /// </summary>
    /// <param name="path">Route template of the operation.</param>
    /// <param name="name">Name of the parameter.</param>
    /// <param name="valueObject">The value object it is.</param>
    [Theory]
    [InlineData("/accounts/{iban}", "iban", nameof(Iban))]
    [InlineData("/accounts/{iban}", "quantity", nameof(Quantity))]
    [InlineData("/accounts/{iban}", "country", nameof(CountryCode))]
    [InlineData("/accounts/{iban}", "X-Page", nameof(PageNumber))]
    [InlineData("/accounts/{iban}", "within", nameof(Duration))]
    [InlineData("/accounts/{iban}", "notice", "NoticeChannelOfAccountFilter")]
    [InlineData("/ledgers/{account}", "account", nameof(AccountId))]
    [InlineData("/ledgers/{account}", "Above", nameof(Amount))]
    [InlineData("/ledgers/{account}", "X-Grade", nameof(Grade))]
    [InlineData("/mvc/accounts/{iban}", "iban", nameof(Iban))]
    [InlineData("/mvc/accounts/{iban}", "quantity", nameof(Quantity))]
    [InlineData("/mvc/accounts/{iban}", "country", nameof(CountryCode))]
    [InlineData("/mvc/accounts/{iban}", "X-Page", nameof(PageNumber))]
    [InlineData("/mvc/accounts/search", "Least", nameof(Quantity))]
    [InlineData("/mvc/accounts/search", "Country", nameof(CountryCode))]
    public void A_parameter_carries_the_rules_of_its_value_object(string path, string name, string valueObject)
    {
        var parameter = Resolve(document.Parameter(path, name));

        JsonElement.DeepEquals(parameter, document.Schema(valueObject)).Should().BeTrue(
            $"{name} is a {valueObject}: {parameter.GetRawText()} should be {document.Schema(valueObject).GetRawText()}");
    }

    /// <summary>
    /// A route constraint is a rule of the parameter, which a request satisfies beside the value object's: describing
    /// the parameter as its value object keeps the stricter bound and length of the two, and a pattern the value object
    /// has none of.
    /// </summary>
    [Fact]
    public void A_parameter_keeps_the_stricter_rules_its_route_constraints_add_to_its_value_object()
    {
        const string path = "/constrained/{page}/{quantity}/{iban}/{country}";
        var page = document.Parameter(path, "page");
        var quantity = document.Parameter(path, "quantity");
        var iban = document.Parameter(path, "iban");
        var country = document.Parameter(path, "country");

        page.GetProperty("minimum").GetInt32().Should().Be(5, "the route asks for more than the value object's 1");
        page.GetProperty("maximum").GetInt32().Should().Be(50, "the value object has no maximum");
        page.GetProperty("description").GetString().Should().Be(document.Schema(nameof(PageNumber)).GetProperty("description").GetString());
        quantity.GetProperty("minimum").GetInt32().Should().Be(0);
        quantity.GetProperty("maximum").GetInt32().Should().Be(1000, "the value object asks for less than the route's 5000");
        iban.GetProperty("minLength").GetInt32().Should().Be(20, "the route asks for more than the value object's 15");
        iban.GetProperty("maxLength").GetInt32().Should().Be(30, "the route asks for less than the value object's 34");
        iban.GetProperty("pattern").GetString().Should().Be(document.Schema(nameof(Iban)).GetProperty("pattern").GetString());
        country.GetProperty("minLength").GetInt32().Should().Be(2, "the value object asks for more than the route's 1");
        country.GetProperty("maxLength").GetInt32().Should().Be(2, "the value object asks for less than the route's 5");
        country.GetProperty("pattern").GetString().Should().Be("^[A-Z]+$", "the value object declares no pattern");
        country.GetProperty("enum").EnumerateArray().Select(code => code.GetString()).Should().Equal("FR", "BE", "LU");
    }

    [Fact]
    public void A_parameter_that_is_no_value_object_keeps_the_schema_ASP_NET_Core_gave_it()
    {
        var note = document.Parameter("/accounts/{iban}", "note");
        var count = document.Parameter("/accounts/{iban}", "count");

        note.EnumerateObject().Select(keyword => keyword.Name).Should().Equal("type");
        note.GetProperty("type").GetString().Should().Be("string");
        Types(count.GetProperty("type")).Should().BeEquivalentTo("integer", "string");
        count.TryGetProperty("minimum", out _).Should().BeFalse();
    }

    /// <summary>
    /// System.Text.Json leaves out the element of a collection whose element type has a converter of its own, so a
    /// collection of value objects was documented as an array of anything. Each element now refers to the component
    /// of its value object, as a property of that type does, whatever the collection, nested ones included, and in a
    /// query string as in a body.
    /// </summary>
    /// <param name="property">The property of the body.</param>
    /// <param name="valueObject">The value object of its elements.</param>
    [Theory]
    [InlineData("alternates", nameof(Iban))]
    [InlineData("quantities", nameof(Quantity))]
    [InlineData("countries", nameof(CountryCode))]
    public void An_element_of_a_collection_of_value_objects_refers_to_the_value_object(string property, string valueObject)
    {
        var collection = document.BodyProperty(property);

        collection.GetProperty("type").GetString().Should().Be("array");
        collection.GetProperty("items").GetProperty("$ref").GetString().Should().Be($"#/components/schemas/{valueObject}");
    }

    [Fact]
    public void A_nested_collection_a_dictionary_and_a_query_string_collection_refer_to_their_value_object()
    {
        var groups = document.BodyProperty("groups");
        var perSku = document.BodyProperty("perSku");
        var quantities = document.Parameter("/quantities", "quantity");

        groups.GetProperty("items").GetProperty("type").GetString().Should().Be("array");
        groups.GetProperty("items").GetProperty("items").GetProperty("$ref").GetString().Should().Be("#/components/schemas/Iban");
        perSku.GetProperty("type").GetString().Should().Be("object");
        perSku.GetProperty("additionalProperties").GetProperty("$ref").GetString().Should().Be("#/components/schemas/Quantity");
        quantities.GetProperty("items").GetProperty("$ref").GetString().Should().Be("#/components/schemas/Quantity");
    }

    /// <summary>
    /// An element of a nullable value object is the value object or <c>null</c>, which a reference to the component
    /// could not say: it is described in place, as the component describes the value object, <c>null</c> allowed.
    /// </summary>
    [Fact]
    public void An_element_of_a_nullable_value_object_is_the_value_object_or_null()
    {
        var element = document.BodyProperty("optional").GetProperty("items");
        var quantity = document.Schema(nameof(Quantity));

        Types(element.GetProperty("type")).Should().BeEquivalentTo(Types(quantity.GetProperty("type")).Append("null"));
        element.EnumerateObject().Where(keyword => keyword.Name != "type").Select(keyword => (keyword.Name, keyword.Value.GetRawText()))
            .Should().BeEquivalentTo(
                quantity.EnumerateObject().Where(keyword => keyword.Name != "type").Select(keyword => (keyword.Name, keyword.Value.GetRawText())));
    }

    /// <summary>
    /// A value object keys a dictionary as the text it writes, so the key's rules hold for the names of its members,
    /// which <c>propertyNames</c> states. A value object over a number is described as the text it writes a key in: a
    /// string held to the pattern of that number, its bounds, which <c>minimum</c> and <c>maximum</c> cannot hold on a
    /// string, in extensions and in a sentence; the keys it writes match it.
    /// </summary>
    [Fact]
    public void A_dictionary_keyed_by_a_value_object_states_its_rules_in_propertyNames_as_the_key_is_written()
    {
        var stock = document.BodyProperty("stockPerCountry");
        var counts = document.BodyProperty("countPerQuantity").GetProperty("propertyNames");
        var keys = JsonSerializer.SerializeToNode(
            new Dictionary<Quantity, int> { [Quantity.Create(0)] = 1, [Quantity.Create(1000)] = 2 },
            WebOptions)!.AsObject().Select(member => member.Key);

        JsonElement.DeepEquals(stock.GetProperty("propertyNames"), document.Schema(nameof(CountryCode))).Should().BeTrue();
        stock.GetProperty("additionalProperties").GetProperty("$ref").GetString().Should().Be("#/components/schemas/Quantity");
        counts.GetProperty("type").GetString().Should().Be("string", "the name of a member is text, never a number");
        counts.GetProperty("pattern").GetString().Should().Be(@"^-?(?:0|[1-9]\d*)$");
        counts.TryGetProperty("minimum", out _).Should().BeFalse("JSON Schema applies minimum to numbers only");
        counts.GetProperty("x-minimum").GetString().Should().Be("0");
        counts.GetProperty("x-maximum").GetString().Should().Be("1000");
        counts.GetProperty("description").GetString().Should().EndWith("\n\nBetween 0 and 1000, inclusive.");
        keys.Should().Equal("0", "1000").And.AllSatisfy(key => key.Should().MatchRegex(counts.GetProperty("pattern").GetString()!));
    }

    /// <summary>
    /// ASP.NET Core hands the element of a collection back to the transformers once the value object's transformer
    /// has given it its schema, which is then described a second time. A value object with no description of its own,
    /// whose bounds written as text are its whole description, still states them once.
    /// </summary>
    [Fact]
    public void A_value_object_in_a_collection_states_its_bounds_once()
    {
        document.BodyProperty("hours").GetProperty("items").GetProperty("$ref").GetString().Should().Be("#/components/schemas/ServiceHour");
        document.Schema(nameof(ServiceHour)).GetProperty("description").GetString()
            .Should().Be("Between 08:00:00.0000000 and 18:00:00.0000000, inclusive.");
    }

    /// <summary>Finds the name of the static property of a value object that holds one of its known values.</summary>
    private static string NameOf(object known)
        => known.GetType().GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Single(property => property.PropertyType == known.GetType() && Equals(property.GetValue(null), known))
            .Name;

    /// <summary>Follows a reference to a component, or answers the schema itself.</summary>
    private JsonElement Resolve(JsonElement schema)
        => schema.TryGetProperty("$ref", out var reference)
            ? document.Schema(reference.GetString()!["#/components/schemas/".Length..])
            : schema;

    private static IEnumerable<string?> Types(JsonElement type)
        => type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Select(each => each.GetString()) : [type.GetString()];

    [Fact]
    public void A_bound_that_is_no_number_is_left_out()
    {
        var born = document.Schema(nameof(BirthDate));

        born.TryGetProperty("minimum", out _).Should().BeFalse("a date is a string on the wire");
        born.TryGetProperty("maximum", out _).Should().BeFalse("a date is a string on the wire");
    }
}
