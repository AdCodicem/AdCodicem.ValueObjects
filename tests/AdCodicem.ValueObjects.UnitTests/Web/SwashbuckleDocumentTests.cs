using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.XPath;
using AdCodicem.ValueObjects.Swashbuckle;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// What a Swashbuckle document says of each value object, in OpenAPI 3.0, Swashbuckle's default, and in OpenAPI 3.1,
/// read from documents Swashbuckle built for the domain.
/// </summary>
/// <param name="fixture">The documents, under the web defaults.</param>
public sealed class SwashbuckleDocumentTests(SwashbuckleDocument fixture) : IClassFixture<SwashbuckleDocument>
{
    private const string Members = nameof(SwashbuckleMembers);

    private SwashbuckleDocuments Documents => fixture.Documents;

    public static TheoryData<string> Versions => SwashbuckleDocuments.Versions;

    /// <summary>The value objects over a number, each beside the member of the body over its underlying type.</summary>
    public static TheoryData<string, string, string> EveryNumberInEveryVersion
    {
        get
        {
            (string ValueObject, string Raw)[] numbers =
            [
                (nameof(Adjustment), "rawSByte"),
                (nameof(Score), "rawByte"),
                (nameof(Quantity), "rawInt16"),
                (nameof(Port), "rawUInt16"),
                (nameof(PageNumber), "rawInt32"),
                (nameof(SequenceNumber), "rawUInt32"),
                (nameof(FileSize), "rawInt64"),
                (nameof(ByteCount), "rawUInt64"),
                (nameof(Amount), "rawDecimal"),
                (nameof(Latitude), "rawDouble"),
                (nameof(Ratio), "rawSingle"),
            ];
            var data = new TheoryData<string, string, string>();
            foreach (var version in SwashbuckleDocuments.Versions)
            {
                foreach (var (valueObject, raw) in numbers)
                {
                    data.Add(version, valueObject, raw);
                }
            }

            return data;
        }
    }

    /// <summary>
    /// The parameters of the endpoints the built-in stack's document describes too, with the component of their value
    /// object, named as Swashbuckle names it, in each version.
    /// </summary>
    public static TheoryData<string, string, string, string> EveryParameterInEveryVersion
    {
        get
        {
            (string Path, string Name, string Component)[] parameters =
            [
                ("/accounts/{iban}", "iban", nameof(Iban)),
                ("/accounts/{iban}", "quantity", nameof(Quantity)),
                ("/accounts/{iban}", "country", nameof(CountryCode)),
                ("/accounts/{iban}", "X-Page", nameof(PageNumber)),
                ("/accounts/{iban}", "within", nameof(Duration)),
                ("/accounts/{iban}", "notice", "AccountFilterNoticeChannel"),
                ("/ledgers/{account}", "account", nameof(AccountId)),
                ("/ledgers/{account}", "Above", nameof(Amount)),
                ("/ledgers/{account}", "X-Grade", nameof(Grade)),
                ("/mvc/accounts/{iban}", "iban", nameof(Iban)),
                ("/mvc/accounts/{iban}", "quantity", nameof(Quantity)),
                ("/mvc/accounts/{iban}", "country", nameof(CountryCode)),
                ("/mvc/accounts/{iban}", "X-Page", nameof(PageNumber)),
                ("/mvc/accounts/search", "Least", nameof(Quantity)),
                ("/mvc/accounts/search", "Country", nameof(CountryCode)),
            ];
            var data = new TheoryData<string, string, string, string>();
            foreach (var version in SwashbuckleDocuments.Versions)
            {
                foreach (var (path, name, component) in parameters)
                {
                    data.Add(version, path, name, component);
                }
            }

            return data;
        }
    }

    /// <summary>
    /// Without the package, Swashbuckle describes a value object by its public properties, as an object whose
    /// <c>value</c> a client would send, and a minimal API's parameter as a bare string, whatever its rules. This is
    /// what the package replaces.
    /// </summary>
    [Fact]
    public async Task Without_the_package_a_value_object_is_an_object_and_a_minimal_API_parameter_a_bare_string()
    {
        var bare = await SwashbuckleDocuments.BuildAsync(static _ => { });

        bare.Schema("3.1", nameof(Iban)).GetProperty("type").GetString().Should().Be("object");
        bare.Schema("3.1", nameof(Iban)).GetProperty("properties").TryGetProperty("value", out _).Should().BeTrue();
        bare.Parameter("3.1", "/accounts/{iban}", "quantity").GetProperty("schema").GetRawText().Should().Be("""{"type":"string"}""");
    }

    /// <summary>
    /// Each value object of the domain, over each underlying type, is documented as the value it travels as: no object,
    /// no property, nothing that refuses other properties.
    /// </summary>
    /// <param name="version">The document's version.</param>
    [Theory]
    [MemberData(nameof(Versions))]
    public void A_value_object_is_documented_as_its_underlying_type_rather_than_as_an_object(string version)
    {
        var components = ValueObjectComponents(version).ToList();

        components.Should().HaveCountGreaterThan(40);
        components.Should().AllSatisfy(component =>
        {
            var (id, schema) = component;
            schema.TryGetProperty("properties", out _).Should().BeFalse(id);
            schema.TryGetProperty("required", out _).Should().BeFalse(id);
            schema.TryGetProperty("additionalProperties", out _).Should().BeFalse(id);
            schema.GetProperty("type").GetString().Should().BeOneOf(["string", "integer", "number", "boolean"], id);
        });
    }

    /// <summary>
    /// Swashbuckle documents a number as a number, though the web defaults read one from text; a value object over a
    /// number is documented as the underlying type is in the same document, its bounds as numbers and no numeric pattern.
    /// </summary>
    /// <param name="version">The document's version.</param>
    /// <param name="valueObject">The value object.</param>
    /// <param name="raw">The member of the body over its underlying type.</param>
    [Theory]
    [MemberData(nameof(EveryNumberInEveryVersion))]
    public void A_number_is_documented_as_Swashbuckle_documents_its_underlying_type(string version, string valueObject, string raw)
    {
        var schema = Documents.Schema(version, valueObject);

        schema.GetProperty("type").GetRawText().Should().Be(Documents.Property(version, Members, raw).GetProperty("type").GetRawText());
        schema.TryGetProperty("pattern", out _).Should().BeFalse("a number is not text");
        schema.TryGetProperty("anyOf", out _).Should().BeFalse("no named literal is read under these options");
        schema.TryGetProperty("x-minimum", out _).Should().BeFalse("a bound of a number is a minimum");
    }

    /// <summary>
    /// An example is published as the type writes it, a number as a number though the options would write one as text
    /// when asked, in the keyword each version has: <c>example</c> in OpenAPI 3.0, <c>examples</c> in OpenAPI 3.1.
    /// </summary>
    /// <param name="version">The document's version.</param>
    [Theory]
    [MemberData(nameof(Versions))]
    public void An_example_is_published_as_the_type_writes_it_in_the_keyword_of_each_version(string version)
    {
        JsonElement Example(string id) => version == "3.0"
            ? Documents.Schema(version, id).GetProperty("example")
            : Documents.Schema(version, id).GetProperty("examples").EnumerateArray().Single();

        Example(nameof(Amount)).GetRawText().Should().Be("1250.00");
        Example(nameof(Port)).GetRawText().Should().Be("8080");
        Example(nameof(Consent)).ValueKind.Should().Be(JsonValueKind.True);
        Example(nameof(Iban)).GetString().Should().Be("FR7630006000011234567890189");
        Documents.Of(version).GetProperty("openapi").GetString().Should().StartWith(version);
    }

    /// <summary>
    /// The rules declared on a value object are published with it. A 128-bit integer travels as a JSON string, which
    /// <c>minimum</c> and <c>maximum</c> cannot bound: its value object stays a string, its bounds in extensions and a
    /// sentence, where Swashbuckle documents a bare one as an integer.
    /// </summary>
    /// <param name="version">The document's version.</param>
    [Theory]
    [MemberData(nameof(Versions))]
    public void The_rules_declared_on_a_value_object_are_published_with_it(string version)
    {
        var iban = Documents.Schema(version, nameof(Iban));
        var quantity = Documents.Schema(version, nameof(Quantity));
        var balance = Documents.Schema(version, nameof(LedgerBalance));

        iban.GetProperty("format").GetString().Should().Be("iban");
        iban.GetProperty("pattern").GetString().Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
        iban.GetProperty("minLength").GetInt32().Should().Be(15);
        iban.GetProperty("maxLength").GetInt32().Should().Be(34);
        quantity.GetProperty("minimum").GetRawText().Should().Be("0");
        quantity.GetProperty("maximum").GetRawText().Should().Be("1000");
        balance.GetProperty("type").GetString().Should().Be("string");
        balance.GetProperty("x-minimum").GetString().Should().Be("-1000000000000000000000");
        balance.GetProperty("description").GetString().Should().EndWith("\n\nBetween -1000000000000000000000 and 1000000000000000000000, inclusive.");
        Documents.Property(version, Members, "rawInt128").GetRawText().Should().Be("""{"type":"integer","format":"int128"}""");
    }

    /// <summary>
    /// A closed value set lists its values as the type writes them, and names them after its known values, in the
    /// enumeration each client generator reads, named as Swashbuckle names the component: a construction of a generic
    /// value object after its type arguments first.
    /// </summary>
    /// <param name="version">The document's version.</param>
    [Theory]
    [MemberData(nameof(Versions))]
    public void A_closed_value_set_lists_and_names_its_values_after_its_component(string version)
    {
        var closedSets = ValueObjectComponents(version).Where(static component => component.Schema.TryGetProperty("enum", out _)).ToList();
        var channel = Documents.Schema(version, "AccountFilterNoticeChannel");
        var priority = Documents.Schema(version, nameof(Priority));

        closedSets.Should().HaveCountGreaterThan(10).And.AllSatisfy(static component =>
            component.Schema.GetProperty("x-ms-enum").GetProperty("name").GetString().Should().Be(component.Id));
        channel.GetProperty("enum").GetRawText().Should().Be("""["email","sms"]""");
        channel.GetProperty("x-enum-varnames").GetRawText().Should().Be("""["Email","Sms"]""");
        priority.GetProperty("enum").EnumerateArray().Select(static value => value.ValueKind).Should().AllBeEquivalentTo(JsonValueKind.Number);
    }

    /// <summary>
    /// A route, query or header parameter refers to its value object's component, in minimal APIs, in a type gathered
    /// with <c>[AsParameters]</c> and in MVC alike, as Swashbuckle refers to the component of an enumeration, with the
    /// component's description.
    /// </summary>
    /// <param name="version">The document's version.</param>
    /// <param name="path">Route template of the operation.</param>
    /// <param name="name">Name of the parameter.</param>
    /// <param name="component">The component of its value object.</param>
    [Theory]
    [MemberData(nameof(EveryParameterInEveryVersion))]
    public void A_parameter_refers_to_its_value_object_with_its_description(string version, string path, string name, string component)
    {
        var parameter = Documents.Parameter(version, path, name);

        parameter.GetProperty("schema").GetProperty("$ref").GetString().Should().Be($"#/components/schemas/{component}");
        parameter.GetProperty("description").GetString().Should().Be(Documents.Schema(version, component).GetProperty("description").GetString());
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void A_parameter_that_is_no_value_object_keeps_the_schema_Swashbuckle_gave_it(string version)
    {
        Documents.Parameter(version, "/accounts/{iban}", "note").GetProperty("schema").GetRawText().Should().Be("""{"type":"string"}""");
        Documents.Parameter(version, "/accounts/{iban}", "count").GetProperty("schema").GetRawText()
            .Should().Be("""{"type":"integer","format":"int32"}""");
        Documents.Parameter(version, "/accounts/{iban}", "count").TryGetProperty("description", out _).Should().BeFalse();
    }

    /// <summary>
    /// A minimal API binds an array of value objects from repeated query values, and each element refers to the
    /// value object's component, as an MVC collection's does.
    /// </summary>
    /// <param name="version">The document's version.</param>
    [Theory]
    [MemberData(nameof(Versions))]
    public void An_array_parameter_of_value_objects_refers_to_their_component(string version)
    {
        var schema = Documents.Parameter(version, "/quantities", "quantity").GetProperty("schema");

        schema.GetProperty("type").GetString().Should().Be("array");
        schema.GetProperty("items").GetProperty("$ref").GetString().Should().Be("#/components/schemas/Quantity");
    }

    /// <summary>
    /// A route constraint beside a reference follows Swashbuckle's rule for one: OpenAPI 3.0 takes no keyword beside a
    /// reference, so Swashbuckle drops it.
    /// </summary>
    [Fact]
    public void A_route_constraint_beside_a_reference_is_dropped_as_Swashbuckle_drops_it()
    {
        var page = Documents.Parameter("3.0", "/constrained/{page}/{quantity}/{iban}/{country}", "page").GetProperty("schema");

        page.EnumerateObject().Select(static keyword => keyword.Name).Should().Equal("$ref");
    }

    /// <summary>
    /// Under <c>UseAllOfToExtendReferenceSchemas</c>, Swashbuckle wraps a reference in an <c>allOf</c>, beside which a
    /// route constraint is kept; the wrapper of a parameter or of a member that is not nullable is left as it is, and the
    /// component it refers to is still described.
    /// </summary>
    [Fact]
    public async Task Under_UseAllOfToExtendReferenceSchemas_a_route_constraint_is_kept_beside_the_reference()
    {
        var documents = await AllOfDocuments();
        const string path = "/constrained/{page}/{quantity}/{iban}/{country}";
        var page = documents.Parameter("3.1", path, "page").GetProperty("schema");
        var iban = documents.Parameter("3.1", path, "iban").GetProperty("schema");
        var member = documents.Property("3.1", nameof(EveryValueObject), "iban");

        page.GetProperty("allOf")[0].GetProperty("$ref").GetString().Should().Be("#/components/schemas/PageNumber");
        page.GetProperty("minimum").GetRawText().Should().Be("5");
        page.GetProperty("maximum").GetRawText().Should().Be("50");
        iban.GetProperty("minLength").GetInt32().Should().Be(20);
        iban.GetProperty("maxLength").GetInt32().Should().Be(30);
        member.EnumerateObject().Select(static keyword => keyword.Name).Should().Equal("allOf");
        documents.Schema("3.1", nameof(PageNumber)).GetProperty("type").GetString().Should().Be("integer");
    }

    /// <summary>
    /// Under <c>UseAllOfToExtendReferenceSchemas</c>, Swashbuckle wraps the reference of a nullable member in an
    /// <c>allOf</c> whose type is <c>null</c> alone, which no value satisfies in OpenAPI 3.1, though it writes a nullable
    /// primitive in place. The wrapper is kept, with what Swashbuckle wrote beside the reference, a member's validation
    /// attributes included, as for a non-nullable value object; inside it, the value object is described in place,
    /// <c>null</c> allowed, in each version's way. A member Swashbuckle makes non-nullable, and a nullable enumeration,
    /// keep Swashbuckle's wrapper.
    /// </summary>
    [Fact]
    public async Task Under_UseAllOfToExtendReferenceSchemas_a_nullable_value_object_is_described_in_place_inside_its_wrapper()
    {
        var documents = await AllOfDocuments();

        foreach (var version in SwashbuckleDocuments.Versions)
        {
            var alternate = documents.Property(version, Members, "alternate");
            var limited = documents.Property(version, Members, "limited");
            var bounded = documents.Property(version, Members, "bounded");

            alternate.EnumerateObject().Select(static keyword => keyword.Name).Should().Equal("allOf");
            InPlace(version, alternate.GetProperty("allOf").EnumerateArray().Single(), nameof(Iban), documents);
            limited.GetProperty("maxLength").GetInt32().Should().Be(5);
            limited.TryGetProperty("type", out _).Should().BeFalse();
            limited.TryGetProperty("nullable", out _).Should().BeFalse();
            InPlace(version, limited.GetProperty("allOf").EnumerateArray().Single(), nameof(Iban), documents);
            bounded.GetProperty("maxLength").GetInt32().Should().Be(5);
            bounded.GetProperty("allOf")[0].GetProperty("$ref").GetString().Should().Be("#/components/schemas/Iban");
            documents.Property(version, Members, "demanded").EnumerateObject().Select(static keyword => keyword.Name).Should().Equal("allOf");
            documents.Property(version, Members, "demanded").GetProperty("allOf")[0].GetProperty("$ref").GetString()
                .Should().Be("#/components/schemas/Quantity");
            documents.Property(version, Members, "rawShade").GetProperty("allOf")[0].GetProperty("$ref").GetString()
                .Should().Be("#/components/schemas/Shade");
            AllowsNull(version, documents.Property(version, Members, "rawLimited")).Should().BeTrue("Swashbuckle writes a nullable primitive in place");
        }
    }

    /// <summary>
    /// A reference cannot say that a value may be <c>null</c>, and Swashbuckle loses it for a nullable value object as
    /// for a nullable enumeration. A nullable value object is described in place instead, as Swashbuckle describes a
    /// nullable primitive: as the value object, <c>null</c> allowed, in each version's own way, as a property, an
    /// element of a collection and a value of a dictionary.
    /// </summary>
    /// <param name="version">The document's version.</param>
    [Theory]
    [MemberData(nameof(Versions))]
    public void A_nullable_value_object_is_described_in_place_as_Swashbuckle_describes_a_nullable_primitive(string version)
    {
        var raw = Documents.Property(version, Members, "raw");

        InPlace(version, Documents.Property(version, Members, "alternate"), nameof(Iban));
        InPlace(version, Documents.Property(version, Members, "country"), nameof(CountryCode));
        InPlace(version, Documents.Property(version, Members, "maybePerSku").GetProperty("additionalProperties"), nameof(Quantity));
        InPlace(version, Documents.Property(version, Members, "perShade").GetProperty("properties").GetProperty(nameof(Shade.Light)), nameof(Quantity));
        InPlace(version, Documents.Property(version, Members, "perShade").GetProperty("properties").GetProperty(nameof(Shade.Dark)), nameof(Quantity));
        InPlace(version, Documents.Property(version, Members, "optional").GetProperty("items"), nameof(Quantity));
        InPlace(version, Documents.Property(version, nameof(EveryValueObject), "optional").GetProperty("items"), nameof(Quantity));
        AllowsNull(version, raw).Should().BeTrue("Swashbuckle describes a nullable primitive so");
        AllowsNull(version, Documents.Property(version, Members, "rawOptional").GetProperty("items")).Should().BeTrue();
        AllowsNull(version, Documents.Property(version, Members, "rawPerShade").GetProperty("properties").GetProperty(nameof(Shade.Light))).Should().BeTrue();
    }

    /// <summary>
    /// A property Swashbuckle makes non-nullable, one marked required, on the member or on the type its metadata names,
    /// keeps its reference, as does a nullable enumeration, which the package leaves to Swashbuckle, and a nullable
    /// parameter, whose being optional says it.
    /// </summary>
    [Fact]
    public void A_required_nullable_value_object_and_a_nullable_enumeration_keep_their_reference()
    {
        Documents.Property("3.1", Members, "demanded").GetRawText().Should().Be("""{"$ref":"#/components/schemas/Quantity"}""");
        Documents.Schema("3.1", Members).GetProperty("required").EnumerateArray().Select(static name => name.GetString()).Should().Contain("demanded");
        Documents.Property("3.1", nameof(SwashbuckleAnnotated), "demanded").GetRawText().Should().Be("""{"$ref":"#/components/schemas/Quantity"}""");
        Documents.Schema("3.1", nameof(SwashbuckleAnnotated)).GetProperty("required").EnumerateArray().Select(static name => name.GetString())
            .Should().Equal("demanded");
        Documents.Property("3.1", Members, "rawShade").GetRawText().Should().Be("""{"$ref":"#/components/schemas/Shade"}""");
        Documents.Parameter("3.0", "/accounts/{iban}", "country").GetProperty("schema").GetRawText()
            .Should().Be("""{"$ref":"#/components/schemas/CountryCode"}""");
    }

    /// <summary>
    /// What Swashbuckle reads off the member stays with a nullable value object described in place: that it is
    /// deprecated, its default value, and whether it is only ever read or only ever written.
    /// </summary>
    [Fact]
    public void A_nullable_value_object_described_in_place_keeps_what_Swashbuckle_read_off_its_member()
    {
        var former = Documents.Property("3.1", Members, "former");
        var fallback = Documents.Property("3.1", Members, "fallback");
        var shown = Documents.Property("3.1", Members, "shown");
        var replacement = Documents.Property("3.1", Members, "replacement");
        var alternate = Documents.Property("3.1", Members, "alternate");

        former.GetProperty("deprecated").GetBoolean().Should().BeTrue();
        fallback.GetProperty("default").GetString().Should().Be("FR");
        shown.GetProperty("readOnly").GetBoolean().Should().BeTrue();
        replacement.GetProperty("writeOnly").GetBoolean().Should().BeTrue();
        AllowsNull("3.1", replacement).Should().BeTrue();
        alternate.TryGetProperty("readOnly", out _).Should().BeFalse();
        alternate.TryGetProperty("writeOnly", out _).Should().BeFalse();
        alternate.TryGetProperty("deprecated", out _).Should().BeFalse();
    }

    /// <summary>
    /// Swashbuckle applies a member's validation attributes to a primitive it writes in place, and none beside a
    /// reference, the one it writes for a value object. A nullable value object described in place stands for that
    /// reference: it is documented as the non-nullable one is, with the rules of its type, <c>null</c> allowed.
    /// </summary>
    /// <param name="version">The document's version.</param>
    [Theory]
    [MemberData(nameof(Versions))]
    public void A_member_s_validation_attributes_are_not_applied_to_a_value_object_as_Swashbuckle_applies_none_beside_a_reference(string version)
    {
        var bounded = Documents.Property(version, Members, "bounded");

        bounded.GetProperty("$ref").GetString().Should().Be("#/components/schemas/Iban");
        bounded.TryGetProperty("maxLength", out _).Should().BeFalse();
        InPlace(version, Documents.Property(version, Members, "limited"), nameof(Iban));
        Documents.Property(version, Members, "rawLimited").GetProperty("maxLength").GetInt32().Should().Be(5);
    }

    /// <summary>
    /// A value object written by hand, which nothing registered, and a construction of a generic value object are
    /// described from the schema each declares, as the generated ones are.
    /// </summary>
    [Fact]
    public void A_value_object_written_by_hand_and_a_construction_of_a_generic_one_are_described()
    {
        Documents.Schema("3.1", nameof(HandWrittenLevel)).GetRawText().Should().Be("""{"minimum":1,"type":"integer"}""");
        Documents.Schema("3.1", nameof(HandWrittenCounter)).GetRawText().Should().Be("""{"type":"integer"}""");
        Documents.Schema("3.1", "PurchaseOrderHandWrittenTag").GetRawText().Should().Be("""{"type":"string"}""");
        Documents.Schema("3.1", "PurchaseOrderReference").GetProperty("maxLength").GetInt32().Should().Be(12);
        Documents.Schema("3.1", "PurchaseOrderReference").GetProperty("examples")[0].GetString().Should().Be("PO-1042");
    }

    /// <summary>
    /// An element of a collection and a value of a dictionary refer to their value object's component, nested ones
    /// included, and a dictionary keyed by a value object states the key's rules as the key is written: in
    /// <c>propertyNames</c> in OpenAPI 3.1, in <c>x-jsonschema-propertyNames</c> in OpenAPI 3.0, which has no such
    /// keyword. A key over a number is text held to the number's pattern, its bounds in extensions and a sentence.
    /// </summary>
    /// <param name="version">The document's version.</param>
    [Theory]
    [MemberData(nameof(Versions))]
    public void Collections_and_dictionaries_refer_to_their_value_objects_and_state_the_rules_of_their_keys(string version)
    {
        var keyword = version == "3.0" ? "x-jsonschema-propertyNames" : "propertyNames";
        JsonElement Body(string name) => Documents.Property(version, nameof(EveryValueObject), name);
        var counts = Body("countPerQuantity").GetProperty(keyword);

        Body("alternates").GetProperty("items").GetProperty("$ref").GetString().Should().Be("#/components/schemas/Iban");
        Body("countries").GetProperty("items").GetProperty("$ref").GetString().Should().Be("#/components/schemas/CountryCode");
        Body("groups").GetProperty("items").GetProperty("items").GetProperty("$ref").GetString().Should().Be("#/components/schemas/Iban");
        Body("perSku").GetProperty("additionalProperties").GetProperty("$ref").GetString().Should().Be("#/components/schemas/Quantity");
        Body("perSku").TryGetProperty(keyword, out _).Should().BeFalse("its keys are strings");
        Documents.Property(version, Members, "untyped").TryGetProperty(keyword, out _).Should().BeFalse("its keys are anything");
        JsonElement.DeepEquals(Body("stockPerCountry").GetProperty(keyword), Documents.Schema(version, nameof(CountryCode))).Should().BeTrue();
        JsonElement.DeepEquals(Documents.Property(version, Members, "stock").GetProperty(keyword), Documents.Schema(version, nameof(CountryCode)))
            .Should().BeTrue("the key of a dictionary type is found among the interfaces of its base type");
        counts.GetProperty("type").GetString().Should().Be("string");
        counts.GetProperty("pattern").GetString().Should().Be(@"^-?(?:0|[1-9]\d*)$");
        counts.GetProperty("x-minimum").GetString().Should().Be("0");
        counts.GetProperty("x-maximum").GetString().Should().Be("1000");
        counts.GetProperty("description").GetString().Should().EndWith("\n\nBetween 0 and 1000, inclusive.");
    }

    /// <summary>
    /// An application may name its components its own way; the enumeration of a closed set is named as its component is,
    /// so that a client generator reading <c>x-ms-enum</c> and one reading the reference agree.
    /// </summary>
    [Fact]
    public async Task A_closed_value_set_is_named_as_the_application_names_its_component()
    {
        var documents = await SwashbuckleDocuments.BuildAsync(static options =>
        {
            options.CustomSchemaIds(static type => type == typeof(CountryCode)
                ? "Country"
                : string.Concat(type.GetGenericArguments().Select(static argument => argument.Name)) + type.Name.Split('`')[0]);
            options.AddValueObjects();
        });

        documents.Schema("3.1", "Country").GetProperty("x-ms-enum").GetProperty("name").GetString().Should().Be("Country");
        documents.Property("3.1", Members, "country").GetProperty("x-ms-enum").GetProperty("name").GetString().Should().Be("Country");
        documents.Property("3.1", nameof(EveryValueObject), "stockPerCountry").GetProperty("propertyNames")
            .GetProperty("x-ms-enum").GetProperty("name").GetString().Should().Be("Country");
    }

    /// <summary>
    /// A <c>MapType</c> an application registered for a value object makes Swashbuckle write the value object in place
    /// rather than refer to a component: the filter describes it there, its rules replacing what the mapping said, and a
    /// nullable one keeps the <c>null</c> Swashbuckle allows it.
    /// </summary>
    [Fact]
    public async Task A_value_object_an_application_mapped_is_described_where_Swashbuckle_writes_it()
    {
        var documents = await SwashbuckleDocuments.BuildAsync(static options =>
        {
            options.MapType<Quantity>(static () => new OpenApiSchema { Type = JsonSchemaType.String });
            options.AddValueObjects();
        });
        var quantity = documents.Property("3.1", nameof(EveryValueObject), "quantity");
        var optional = documents.Property("3.1", nameof(EveryValueObject), "optional").GetProperty("items");

        quantity.GetProperty("type").GetString().Should().Be("integer");
        quantity.GetProperty("maximum").GetRawText().Should().Be("1000");
        optional.GetProperty("type").GetRawText().Should().Be("""["null","integer"]""");
        optional.GetProperty("maximum").GetRawText().Should().Be("1000");
    }

    /// <summary>
    /// Swashbuckle runs its schema filters in the order they were added. Added after the value objects', its XML
    /// comments filter replaces a value object's description with the type's summary, losing the sentence stating the
    /// bounds of a value written as text; added before, it does not. A member's summary is kept on a nullable value object
    /// described in place, as on a reference.
    /// </summary>
    [Fact]
    public async Task The_value_objects_are_added_after_the_XML_comments_whose_filter_would_replace_their_description()
    {
        var after = await SwashbuckleDocuments.BuildAsync(static options =>
        {
            options.IncludeXmlComments(Comments);
            options.AddValueObjects();
        });
        var before = await SwashbuckleDocuments.BuildAsync(static options =>
        {
            options.AddValueObjects();
            options.IncludeXmlComments(Comments);
        });

        after.Schema("3.1", nameof(LedgerBalance)).GetProperty("description").GetString()
            .Should().EndWith("\n\nBetween -1000000000000000000000 and 1000000000000000000000, inclusive.");
        after.Property("3.1", Members, "alternate").GetProperty("description").GetString().Should().Be("The account to fall back on.");
        AllowsNull("3.1", after.Property("3.1", Members, "alternate")).Should().BeTrue();
        before.Schema("3.1", nameof(LedgerBalance)).GetProperty("description").GetString().Should().Be("From the XML comments.");
    }

    /// <summary>
    /// The options an application names write the examples, in place of the minimal API ones; a number stays one, though
    /// they would write it as text, and they need no resolver of their own, as a serializer call would not.
    /// </summary>
    [Fact]
    public async Task The_options_an_application_names_write_the_examples_and_a_number_stays_one()
    {
        var documents = await SwashbuckleDocuments.BuildAsync(static options =>
            options.AddValueObjects(new JsonSerializerOptions(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.WriteAsString }));
        var amount = documents.Schema("3.1", nameof(Amount));

        amount.GetProperty("type").GetString().Should().Be("number");
        amount.GetProperty("examples")[0].GetRawText().Should().Be("1250.00");
        documents.Schema("3.1", nameof(Priority)).GetProperty("enum").EnumerateArray().Select(static value => value.ValueKind)
            .Should().AllBeEquivalentTo(JsonValueKind.Number);
    }

    /// <summary>Reads XML comments describing a value object and a member.</summary>
    private static XPathDocument Comments()
    {
        const string xml = """
            <doc><members>
            <member name="T:AdCodicem.ValueObjects.UnitTests.Domain.LedgerBalance"><summary>From the XML comments.</summary></member>
            <member name="P:AdCodicem.ValueObjects.UnitTests.Web.SwashbuckleMembers.Alternate"><summary>The account to fall back on.</summary></member>
            </members></doc>
            """;
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return new XPathDocument(reader);
    }

    /// <summary>Lists the components of the value objects the body holding one of each refers to.</summary>
    private IEnumerable<(string Id, JsonElement Schema)> ValueObjectComponents(string version)
        => Documents.Schema(version, nameof(EveryValueObject)).GetProperty("properties").EnumerateObject()
            .Select(static property => property.Value)
            .Where(static schema => schema.TryGetProperty("$ref", out _))
            .Select(schema => schema.GetProperty("$ref").GetString()!["#/components/schemas/".Length..])
            .Distinct()
            .Select(id => (id, Documents.Schema(version, id)));

    /// <summary>Builds the documents of an application that has Swashbuckle extend its references with an <c>allOf</c>.</summary>
    private static Task<SwashbuckleDocuments> AllOfDocuments()
        => SwashbuckleDocuments.BuildAsync(static options =>
        {
            options.UseAllOfToExtendReferenceSchemas();
            options.AddValueObjects();
        });

    /// <summary>
    /// Asserts a schema describes a value object in place, <c>null</c> allowed: everything its component says, and
    /// <c>null</c>, in the way of the document's version.
    /// </summary>
    private void InPlace(string version, JsonElement schema, string component, SwashbuckleDocuments? documents = null)
    {
        var described = (documents ?? Documents).Schema(version, component);

        AllowsNull(version, schema).Should().BeTrue($"{schema.GetRawText()} allows null");
        Keywords(schema).Should().BeEquivalentTo(Keywords(described), $"{schema.GetRawText()} describes {component}");
        TypesOf(schema).Where(static type => type != "null").Should().Equal(TypesOf(described));

        static IEnumerable<(string, string)> Keywords(JsonElement schema)
            => schema.EnumerateObject()
                .Where(static keyword => keyword.Name is not ("type" or "nullable"))
                .Select(static keyword => (keyword.Name, keyword.Value.GetRawText()));
    }

    /// <summary>Tells whether a schema allows <c>null</c>, as each version says it.</summary>
    private static bool AllowsNull(string version, JsonElement schema)
        => version == "3.0"
            ? schema.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean()
            : TypesOf(schema).Contains("null");

    private static IEnumerable<string?> TypesOf(JsonElement schema)
        => schema.GetProperty("type") is { ValueKind: JsonValueKind.Array } types
            ? types.EnumerateArray().Select(static type => type.GetString())
            : [schema.GetProperty("type").GetString()];
}

/// <summary>
/// Swashbuckle's description of each value object against the built-in stack's, for an application whose minimal API
/// options read and write numbers as numbers only, where both describe a number as a number.
/// </summary>
/// <param name="fixture">The two documents.</param>
public sealed class SwashbuckleParityTests(SwashbuckleParityDocument fixture) : IClassFixture<SwashbuckleParityDocument>
{
    /// <summary>The members of the body holding one of each value object of the domain, as written in JSON.</summary>
    public static TheoryData<string> EveryMember => [.. typeof(EveryValueObject).GetConstructors().Single().GetParameters()
        .Select(static parameter => JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!))];

    /// <summary>
    /// The two packages share their description of a value object: each is described alike by both, in its component,
    /// as an element, as a value and as a key, but for the name an enumeration takes after its component, which each host
    /// names its own way.
    /// </summary>
    /// <param name="member">The member of the body.</param>
    [Theory]
    [MemberData(nameof(EveryMember))]
    public void Each_value_object_is_described_as_the_built_in_package_describes_it(string member)
    {
        var swashbuckle = Described(fixture.Documents.V31, member);
        var builtIn = Described(fixture.Documents.BuiltIn!.Value, member);

        JsonNode.DeepEquals(swashbuckle, builtIn).Should().BeTrue($"{member}: {swashbuckle.ToJsonString()} should be {builtIn.ToJsonString()}");
    }

    /// <summary>
    /// What a document says of the value object a member is or holds: its component, or the elements, values and keys of
    /// a collection or a dictionary, each reference followed.
    /// </summary>
    private static JsonNode Described(JsonElement document, string member)
    {
        var schemas = document.GetProperty("components").GetProperty("schemas");
        var schema = schemas.GetProperty(nameof(EveryValueObject)).GetProperty("properties").GetProperty(member);
        if (schema.TryGetProperty("$ref", out _))
        {
            return Followed(schemas, schema);
        }

        var described = new JsonObject();
        foreach (var keyword in (string[])["items", "additionalProperties", "propertyNames"])
        {
            if (schema.TryGetProperty(keyword, out var inner))
            {
                described[keyword] = Followed(schemas, inner);
            }
        }

        return described;
    }

    /// <summary>
    /// Follows every reference of a schema to its component, leaving out what Swashbuckle writes beside a reference in
    /// OpenAPI 3.1 and the name of an enumeration.
    /// </summary>
    private static JsonNode Followed(JsonElement schemas, JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            return Followed(schemas, schemas.GetProperty(reference.GetString()!["#/components/schemas/".Length..]));
        }

        var node = JsonNode.Parse(schema.GetRawText())!;
        if (node is JsonObject followed)
        {
            foreach (var keyword in (string[])["items", "additionalProperties", "propertyNames"])
            {
                if (schema.TryGetProperty(keyword, out var inner) && inner.ValueKind == JsonValueKind.Object)
                {
                    followed[keyword] = Followed(schemas, inner);
                }
            }

            (followed["x-ms-enum"] as JsonObject)?.Remove("name");
        }

        return node;
    }
}

/// <summary>
/// How the filters are added to Swashbuckle's options.
/// </summary>
public sealed class SwashbuckleWiringTests
{
    [Fact]
    public void Called_twice_it_adds_each_filter_once_holding_the_options_of_the_last_call()
    {
        var options = new SwaggerGenOptions();
        var named = new JsonSerializerOptions();

        options.AddValueObjects().Should().BeSameAs(options);
        options.AddValueObjects(named).Should().BeSameAs(options);

        var schemaFilter = options.SchemaFilterDescriptors.Should().ContainSingle(static descriptor => descriptor.Type == typeof(ValueObjectSchemaFilter)).Subject;
        options.ParameterFilterDescriptors.Should().ContainSingle(static descriptor => descriptor.Type == typeof(ValueObjectParameterFilter));
        schemaFilter.Arguments.Should().ContainSingle().Which.Should().BeOfType<ValueObjectSwaggerSettings>()
            .Which.Options.Should().BeSameAs(named);
    }

    [Fact]
    public void The_minimal_API_options_are_the_default()
    {
        var options = new SwaggerGenOptions().AddValueObjects();

        options.SchemaFilterDescriptors.Single().Arguments.Should().ContainSingle()
            .Which.Should().BeOfType<ValueObjectSwaggerSettings>().Which.Options.Should().BeNull();
    }

    [Fact]
    public void A_missing_argument_is_refused()
    {
        var withoutOptions = () => ValueObjectSwaggerGenExtensions.AddValueObjects(null!);
        var withoutNamedOptions = () => ValueObjectSwaggerGenExtensions.AddValueObjects(null!, new JsonSerializerOptions());
        var withoutSerializerOptions = () => new SwaggerGenOptions().AddValueObjects(null!);

        withoutOptions.Should().Throw<ArgumentNullException>().WithParameterName("options");
        withoutNamedOptions.Should().Throw<ArgumentNullException>().WithParameterName("options");
        withoutSerializerOptions.Should().Throw<ArgumentNullException>().WithParameterName("serializerOptions");
    }
}
