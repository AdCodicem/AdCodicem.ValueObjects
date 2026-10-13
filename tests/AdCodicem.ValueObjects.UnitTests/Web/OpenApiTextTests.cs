using System.Text.Json;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// What the built-in stack's OpenAPI document says of text a member or a parameter holds to a value object's rules with
/// <see cref="ParsesAsAttribute"/>: the text of the value object, as the key of a dictionary is described.
/// </summary>
/// <param name="document">The document.</param>
public class OpenApiTextTests(OpenApiDocument document) : IClassFixture<OpenApiDocument>
{
    /// <summary>The members of the body holding the text of every value object, as written in JSON, with their type.</summary>
    public static TheoryData<string, Type> EveryTextMember => [.. typeof(EveryValueObjectAsText).GetConstructors().Single().GetParameters()
        .Where(static parameter => parameter.GetCustomAttributes(typeof(ParsesAsAttribute), inherit: false).Length > 0
                                   && parameter.ParameterType == typeof(string))
        .Select(static parameter => (JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!),
            ((ParsesAsAttribute)parameter.GetCustomAttributes(typeof(ParsesAsAttribute), inherit: false).Single()).ValueObjectType))];

    private JsonElement Text => document.Schema(nameof(EveryValueObjectAsText)).GetProperty("properties");

    /// <summary>
    /// Text is a string whatever the underlying type, allowing <see langword="null"/> as the member does: a value object
    /// over text is described as its component describes it, its known values followed by <c>null</c>; one over another
    /// type as the text it is written in as a dictionary key, its bounds in <c>x-minimum</c> and <c>x-maximum</c>, which a
    /// string cannot take in <c>minimum</c> and <c>maximum</c>.
    /// </summary>
    /// <param name="member">The member of the body.</param>
    /// <param name="valueObject">The value object it holds the text of.</param>
    [Theory]
    [MemberData(nameof(EveryTextMember))]
    public void Each_member_holding_text_is_described_as_the_text_of_its_value_object(string member, Type valueObject)
    {
        var text = Text.GetProperty(member);
        var component = document.Schema(valueObject == typeof(NoticeChannel<AccountFilter>) ? "NoticeChannelOfAccountFilter" : valueObject.Name);
        ValueObjectRegistry.TryGet(valueObject, out var descriptor).Should().BeTrue();

        Types(text).Should().Equal("null", "string");
        text.TryGetProperty("minimum", out _).Should().BeFalse();
        text.TryGetProperty("maximum", out _).Should().BeFalse();
        text.TryGetProperty("properties", out _).Should().BeFalse();
        if (descriptor!.ValueType == typeof(string))
        {
            Keywords(text).Should().BeEquivalentTo(Keywords(component), $"{member}: {text.GetRawText()} describes {component.GetRawText()}");
        }
        else
        {
            component.TryGetProperty("format", out var format).Should().Be(text.TryGetProperty("format", out var described));
            if (format.ValueKind == JsonValueKind.String)
            {
                described.GetString().Should().Be(format.GetString());
            }

            (component.TryGetProperty("minimum", out _) || component.TryGetProperty("x-minimum", out _))
                .Should().Be(text.TryGetProperty("x-minimum", out _), $"{member} states the value object's least bound as text");
        }

        // Each known value, in the text it is written in, then null.
        if (component.TryGetProperty("enum", out var known))
        {
            var values = text.GetProperty("enum").EnumerateArray().ToList();
            values.Should().HaveCount(known.GetArrayLength() + 1);
            values.SkipLast(1).Should().AllSatisfy(static value => value.ValueKind.Should().Be(JsonValueKind.String));
            values[^1].ValueKind.Should().Be(JsonValueKind.Null);
        }

        static IEnumerable<(string, string)> Keywords(JsonElement schema)
            => schema.EnumerateObject()
                .Where(static keyword => keyword.Name is not ("type" or "enum"))
                .Select(static keyword => (keyword.Name, Raw(keyword.Value)));
    }

    /// <summary>
    /// A value object over a number is held to the text its converter writes, which <c>format</c> still names: the
    /// pattern of an integer, and the bounds as text, as the key of a dictionary over it is described.
    /// </summary>
    [Fact]
    public void Text_of_a_value_object_over_a_number_is_described_as_its_dictionary_key()
    {
        var quantity = Text.GetProperty("quantity");
        var key = document.BodyProperty("countPerQuantity").GetProperty("propertyNames");

        quantity.EnumerateObject().Where(static keyword => keyword.Name != "type").Select(static keyword => (keyword.Name, Raw(keyword.Value)))
            .Should().Equal(key.EnumerateObject().Where(static keyword => keyword.Name != "type").Select(static keyword => (keyword.Name, Raw(keyword.Value))));
        Raw(Text.GetProperty("genericQuantity")).Should().Be(Raw(quantity), "the generic form names the same value object");
        Text.GetProperty("page").GetProperty("format").GetString().Should().Be("int32", "format still names the underlying type beside type: string");
    }

    [Fact]
    public void A_collection_of_text_has_its_elements_described()
    {
        var alternates = Text.GetProperty("alternates");
        var iban = document.Schema(nameof(Iban));

        Types(alternates).Should().Equal("null", "array");
        JsonElement.DeepEquals(alternates.GetProperty("items"), iban).Should().BeTrue(alternates.GetRawText());
    }

    [Fact]
    public void Text_no_attribute_holds_to_a_value_object_is_left_as_ASP_NET_Core_describes_it()
    {
        Raw(Text.GetProperty("plain")).Should().Be("""{"type":["null","string"]}""");
        Raw(document.Schema(nameof(TextMembers)).GetProperty("properties").GetProperty("note")).Should().Be("""{"type":["null","string"]}""");
    }

    /// <summary>
    /// A class takes the attributes on its properties: <c>[Required]</c> makes the member required, and a rule the
    /// member's own attribute states, stricter than the value object's, is kept, as for a route constraint.
    /// </summary>
    [Fact]
    public void A_class_member_keeps_its_own_stricter_rules_beside_its_value_objects()
    {
        var members = document.Schema(nameof(TextMembers));
        var bounded = members.GetProperty("properties").GetProperty("bounded");

        members.GetProperty("required").EnumerateArray().Select(static name => name.GetString()).Should().Equal("country");
        members.GetProperty("properties").GetProperty("country").GetProperty("enum").EnumerateArray().Select(static value => value.ToString())
            .Should().Equal("FR", "BE", "LU", string.Empty);
        bounded.GetProperty("maxLength").GetInt32().Should().Be(20, "[StringLength(20)] asks for less than the value object's 34");
        bounded.GetProperty("minLength").GetInt32().Should().Be(15);
        bounded.GetProperty("pattern").GetString().Should().Be(document.Schema(nameof(Iban)).GetProperty("pattern").GetString());
    }

    /// <summary>
    /// A parameter holding text is described as that text wherever the attribute is read: on the parameter of a minimal
    /// API or an MVC action, on a member gathered with <c>[AsParameters]</c>, a class's or a record's, and on a member of a
    /// model MVC binds from the query string, a class's property or a record's constructor parameter.
    /// </summary>
    /// <param name="path">Route template of the operation.</param>
    /// <param name="name">Name of the parameter.</param>
    /// <param name="valueObject">The value object it holds the text of.</param>
    [Theory]
    [InlineData("/text/{iban}", "quantity", nameof(Quantity))]
    [InlineData("/text/{iban}", "X-Country", nameof(CountryCode))]
    [InlineData("/text/{iban}", "page", nameof(PageNumber))]
    [InlineData("/text-criteria", "account", nameof(Iban))]
    [InlineData("/text-criteria", "X-Quantity", nameof(Quantity))]
    [InlineData("/text-search", "Iban", nameof(Iban))]
    [InlineData("/text-search", "Quantity", nameof(Quantity))]
    [InlineData("/mvc/accounts/text", "iban", nameof(Iban))]
    [InlineData("/mvc/accounts/text-search", "Iban", nameof(Iban))]
    [InlineData("/mvc/accounts/text-search", "Quantity", nameof(Quantity))]
    [InlineData("/mvc/accounts/text-search-record", "Iban", nameof(Iban))]
    [InlineData("/mvc/accounts/text-search-record", "Quantity", nameof(Quantity))]
    [InlineData("/mvc/hiding/search", "Account", nameof(Iban))]
    [InlineData("/mvc/hiding/search", "Country", nameof(CountryCode))]
    public void A_parameter_holding_text_is_described_as_the_text_of_its_value_object(string path, string name, string valueObject)
    {
        var parameter = document.Parameter(path, name);
        var expected = Text.GetProperty(valueObject switch { nameof(Quantity) => "quantity", nameof(CountryCode) => "country", nameof(PageNumber) => "page", _ => "iban" });

        Types(parameter).Should().Equal("string");
        parameter.EnumerateObject().Where(static keyword => keyword.Name is not ("type" or "enum")).Select(static keyword => (keyword.Name, Raw(keyword.Value)))
            .Should().Equal(expected.EnumerateObject().Where(static keyword => keyword.Name is not ("type" or "enum")).Select(static keyword => (keyword.Name, Raw(keyword.Value))));
    }

    [Fact]
    public void A_route_parameter_keeps_its_stricter_constraint_and_a_collection_has_its_elements_described()
    {
        var iban = document.Parameter("/text/{iban}", "iban");
        var ibans = document.Parameter("/text/{iban}", "ibans");

        iban.GetProperty("minLength").GetInt32().Should().Be(20, "the route asks for more than the value object's 15");
        iban.GetProperty("maxLength").GetInt32().Should().Be(30, "the route asks for less than the value object's 34");
        iban.GetProperty("pattern").GetString().Should().Be(document.Schema(nameof(Iban)).GetProperty("pattern").GetString());
        ibans.GetProperty("type").GetString().Should().Be("array");
        JsonElement.DeepEquals(ibans.GetProperty("items"), document.Schema(nameof(Iban))).Should().BeTrue(ibans.GetRawText());
        Raw(document.Parameter("/text/{iban}", "note")).Should().Be("""{"type":"string"}""");
    }

    /// <summary>
    /// A model MVC binds from the query string may hide an inherited member with <c>new</c>, of another type, which MVC
    /// binds as the most derived one declares it: that member's attribute is the one read, so a member hiding text with a
    /// number is described as any number is, and an inherited member keeps its own (the theory above).
    /// </summary>
    [Fact]
    public void A_query_model_hiding_an_inherited_member_is_described_as_MVC_binds_it()
    {
        var page = document.Parameter("/mvc/hiding/search", "Page");
        var number = document.Parameter("/accounts/{iban}", "count");

        JsonElement.DeepEquals(page, number).Should().BeTrue($"{Raw(page)} describes the number, not the hidden member's {nameof(Quantity)}");
    }

    /// <summary>
    /// A construction of a generic value object nothing else in the process names is described by reflection the first
    /// time the document meets it, as the attribute finds it when a value is validated.
    /// </summary>
    [Fact]
    public void A_construction_nothing_registered_is_described_the_first_time_the_document_names_it()
    {
        var reference = document.Parameter("/text-generic", "reference");

        Types(reference).Should().Equal("string");
        reference.GetProperty("maxLength").GetInt32().Should().Be(12);
        reference.GetProperty("examples").EnumerateArray().Select(static example => example.GetString()).Should().Equal("PO-1042");
    }

    /// <summary>Writes a schema without the indentation the document was served with, so that it reads as its JSON.</summary>
    private static string Raw(JsonElement schema) => JsonSerializer.Serialize(schema);

    private static IEnumerable<string?> Types(JsonElement schema)
        => schema.GetProperty("type") is { ValueKind: JsonValueKind.Array } types
            ? types.EnumerateArray().Select(static type => type.GetString())
            : [schema.GetProperty("type").GetString()];
}
