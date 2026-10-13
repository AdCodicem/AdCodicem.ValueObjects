using System.Text.Json;
using System.Text.Json.Nodes;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// What Swashbuckle's documents say of text a member or a parameter holds to a value object's rules with
/// <see cref="ParsesAsAttribute"/>, in OpenAPI 3.0 and 3.1: the text of the value object, as the built-in stack's
/// transformer describes it.
/// </summary>
/// <param name="fixture">The documents.</param>
public sealed class SwashbuckleTextTests(SwashbuckleDocument fixture) : IClassFixture<SwashbuckleDocument>
{
    public static TheoryData<string> Versions => SwashbuckleDocuments.Versions;

    private SwashbuckleDocuments Documents => fixture.Documents;

    /// <summary>
    /// Text is a string whatever the underlying type, with the value object's pattern, lengths, format, bounds as text,
    /// example and known values, allowing <see langword="null"/> as the member does, in the way of each version.
    /// </summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void A_member_holding_text_is_described_as_the_text_of_its_value_object(string version)
    {
        var iban = Documents.Property(version, nameof(EveryValueObjectAsText), "iban");
        var quantity = Documents.Property(version, nameof(EveryValueObjectAsText), "quantity");
        var country = Documents.Property(version, nameof(EveryValueObjectAsText), "country");
        var page = Documents.Property(version, nameof(EveryValueObjectAsText), "page");

        AllowsNull(version, iban).Should().BeTrue();
        StringType(iban).Should().BeTrue();
        iban.GetProperty("minLength").GetInt32().Should().Be(15);
        iban.GetProperty("maxLength").GetInt32().Should().Be(34);
        iban.GetProperty("pattern").GetString().Should().Be(Documents.Schema(version, nameof(Iban)).GetProperty("pattern").GetString());
        iban.GetProperty("format").GetString().Should().Be("iban");
        StringType(quantity).Should().BeTrue("a quantity's text is a string, though the quantity is a number");
        quantity.GetProperty("pattern").GetString().Should().Be(@"^-?(?:0|[1-9]\d*)$");
        quantity.GetProperty("x-minimum").GetString().Should().Be("0");
        quantity.GetProperty("x-maximum").GetString().Should().Be("1000");
        quantity.TryGetProperty("minimum", out _).Should().BeFalse();
        page.GetProperty("format").GetString().Should().Be("int32");
        country.GetProperty("enum").EnumerateArray().Select(static value => value.ToString()).Should().Equal("FR", "BE", "LU", string.Empty);
        country.GetProperty("x-ms-enum").GetProperty("name").GetString().Should().Be(nameof(CountryCode));
        Documents.Property(version, nameof(EveryValueObjectAsText), "genericQuantity").GetRawText().Should().Be(quantity.GetRawText());
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void A_collection_of_text_has_its_elements_described_and_plain_text_is_left_alone(string version)
    {
        var alternates = Documents.Property(version, nameof(EveryValueObjectAsText), "alternates");
        var plain = Documents.Property(version, nameof(EveryValueObjectAsText), "plain");

        alternates.GetProperty("items").GetProperty("pattern").GetString().Should().Be(Documents.Schema(version, nameof(Iban)).GetProperty("pattern").GetString());
        alternates.GetProperty("items").GetProperty("maxLength").GetInt32().Should().Be(34);
        plain.EnumerateObject().Select(static keyword => keyword.Name).Should().BeEquivalentTo(version == "3.0" ? ["type", "nullable"] : ["type"]);
    }

    /// <summary>
    /// A rule the member's own attribute states, which Swashbuckle writes before the filter describes the member, is kept
    /// where it is stricter than the value object's; <c>[Required]</c> makes the member required and refuses <c>null</c>,
    /// as Swashbuckle says of a required member.
    /// </summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void A_class_member_keeps_its_own_stricter_rules(string version)
    {
        var bounded = Documents.Property(version, nameof(TextMembers), "bounded");
        var country = Documents.Property(version, nameof(TextMembers), "country");

        bounded.GetProperty("maxLength").GetInt32().Should().Be(20);
        bounded.GetProperty("minLength").GetInt32().Should().Be(15);
        AllowsNull(version, country).Should().BeFalse();
        country.GetProperty("enum").EnumerateArray().Select(static value => value.GetString()).Should().Equal("FR", "BE", "LU");
    }

    /// <summary>
    /// A parameter holding text is described wherever the attribute is read, and Swashbuckle copies the description onto
    /// the parameter, as it does for any parameter whose schema has one.
    /// </summary>
    /// <param name="path">Route template of the operation.</param>
    /// <param name="name">Name of the parameter.</param>
    /// <param name="pattern">The pattern of its value object's text.</param>
    [Theory]
    [InlineData("/text/{iban}", "quantity", @"^-?(?:0|[1-9]\d*)$")]
    [InlineData("/text/{iban}", "page", @"^-?(?:0|[1-9]\d*)$")]
    [InlineData("/text-criteria", "account", "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    [InlineData("/text-criteria", "X-Quantity", @"^-?(?:0|[1-9]\d*)$")]
    [InlineData("/text-search", "Iban", "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    [InlineData("/mvc/accounts/text", "iban", "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    [InlineData("/mvc/accounts/text-search", "Quantity", @"^-?(?:0|[1-9]\d*)$")]
    [InlineData("/mvc/accounts/text-search-record", "Iban", "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    public void A_parameter_holding_text_is_described_as_the_text_of_its_value_object(string path, string name, string pattern)
    {
        foreach (var version in (string[])["3.0", "3.1"])
        {
            var parameter = Documents.Parameter(version, path, name);
            var schema = parameter.GetProperty("schema");

            StringType(schema).Should().BeTrue();
            schema.GetProperty("pattern").GetString().Should().Be(pattern);
            parameter.GetProperty("description").GetString().Should().Be(schema.GetProperty("description").GetString());
        }
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void A_route_parameter_keeps_its_stricter_constraint_and_a_header_its_known_values(string version)
    {
        var iban = Documents.Parameter(version, "/text/{iban}", "iban").GetProperty("schema");
        var country = Documents.Parameter(version, "/text/{iban}", "X-Country").GetProperty("schema");
        var ibans = Documents.Parameter(version, "/text/{iban}", "ibans").GetProperty("schema");

        iban.GetProperty("minLength").GetInt32().Should().Be(20);
        iban.GetProperty("maxLength").GetInt32().Should().Be(30);
        country.GetProperty("enum").EnumerateArray().Select(static value => value.GetString()).Should().Equal("FR", "BE", "LU");
        ibans.GetProperty("items").GetProperty("maxLength").GetInt32().Should().Be(34);
    }

    /// <summary>
    /// A construction of a generic value object nothing else in the process names is described by reflection the first
    /// time the filter meets it, as the attribute finds it when a value is validated.
    /// </summary>
    /// <param name="version"><c>3.0</c> or <c>3.1</c>.</param>
    [Theory]
    [MemberData(nameof(Versions))]
    public void A_construction_nothing_registered_is_described_the_first_time_a_document_names_it(string version)
    {
        var reference = Documents.Parameter(version, "/text-generic", "reference").GetProperty("schema");

        StringType(reference).Should().BeTrue();
        reference.GetProperty("maxLength").GetInt32().Should().Be(12);
        (version == "3.0" ? reference.GetProperty("example").GetString() : reference.GetProperty("examples")[0].GetString()).Should().Be("PO-1042");
    }

    private static bool StringType(JsonElement schema)
        => schema.GetProperty("type") is { ValueKind: JsonValueKind.Array } types
            ? types.EnumerateArray().Select(static type => type.GetString()).Where(static type => type != "null").SequenceEqual(["string"])
            : schema.GetProperty("type").GetString() == "string";

    private static bool AllowsNull(string version, JsonElement schema)
        => version == "3.0"
            ? schema.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean()
            : schema.GetProperty("type") is { ValueKind: JsonValueKind.Array } types && types.EnumerateArray().Any(static type => type.GetString() == "null");
}

/// <summary>
/// Swashbuckle's description of text held to a value object's rules against the built-in stack's, for an application
/// whose minimal API options read and write numbers as numbers only, where both describe a number as a number.
/// </summary>
/// <param name="fixture">The two documents.</param>
public sealed class SwashbuckleTextParityTests(SwashbuckleParityDocument fixture) : IClassFixture<SwashbuckleParityDocument>
{
    /// <summary>The members of the body holding the text of every value object, as written in JSON.</summary>
    public static TheoryData<string> EveryMember => [.. typeof(EveryValueObjectAsText).GetConstructors().Single().GetParameters()
        .Select(static parameter => JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!))];

    /// <summary>
    /// The two packages share their description of text, but for the name an enumeration takes after its component,
    /// which each host names its own way.
    /// </summary>
    /// <param name="member">The member of the body.</param>
    [Theory]
    [MemberData(nameof(EveryMember))]
    public void Each_member_holding_text_is_described_as_the_built_in_package_describes_it(string member)
    {
        var swashbuckle = Member(fixture.Documents.V31, member);
        var builtIn = Member(fixture.Documents.BuiltIn!.Value, member);

        JsonNode.DeepEquals(swashbuckle, builtIn).Should().BeTrue($"{member}: {swashbuckle.ToJsonString()} should be {builtIn.ToJsonString()}");
    }

    private static JsonNode Member(JsonElement document, string member)
    {
        var node = JsonNode.Parse(document.GetProperty("components").GetProperty("schemas").GetProperty(nameof(EveryValueObjectAsText))
            .GetProperty("properties").GetProperty(member).GetRawText())!;
        (node["x-ms-enum"] as JsonObject)?.Remove("name");

        return node;
    }
}
