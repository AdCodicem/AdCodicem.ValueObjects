using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using AdCodicem.ValueObjects.Swashbuckle;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The Swashbuckle filters called directly, with what no document Swashbuckle builds hands them: options of every
/// shape, a repository in any state, data contracts and schemas another resolver or filter could produce.
/// </summary>
public sealed class SwashbuckleFilterTests
{
    private static readonly EmptyModelMetadataProvider Metadata = new();

    /// <summary>
    /// The examples are written with the minimal API options unless the application named others: options whose
    /// resolver knows nothing of the value object fall back to the example's text, which proves which options wrote it.
    /// </summary>
    [Fact]
    public void The_minimal_API_options_write_the_examples_unless_the_application_named_others()
    {
        var blind = new HttpJsonOptions();
        blind.SerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine();
        var byDefault = Describe<Amount>(Filter(http: blind));
        var named = Describe<Amount>(Filter(http: blind, named: new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        byDefault.Examples![0].GetValueKind().Should().Be(JsonValueKind.String, "the minimal API options hold no contract for it");
        named.Examples![0].GetValueKind().Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public void Options_that_read_numbers_as_numbers_only_and_hold_a_resolver_are_taken_as_they_are()
    {
        var named = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var filter = Filter(named: named);

        filter.Options.Should().BeSameAs(named);
        filter.Options.Should().BeSameAs(filter.Options, "the options are worked out once");
    }

    /// <summary>
    /// Options that read a number from text, write one as text, or read and write the named literals of a real, though
    /// they hold a resolver, are copied with numbers as numbers only: a number is described with a number's type and
    /// example, and a real with no named literal.
    /// </summary>
    /// <param name="handling">How the options handle numbers.</param>
    [Theory]
    [InlineData(JsonNumberHandling.AllowReadingFromString)]
    [InlineData(JsonNumberHandling.WriteAsString)]
    [InlineData(JsonNumberHandling.AllowNamedFloatingPointLiterals)]
    public void Options_that_handle_numbers_otherwise_are_copied_with_numbers_as_numbers_and_their_resolver(JsonNumberHandling handling)
    {
        var named = new JsonSerializerOptions(JsonSerializerDefaults.Web) { NumberHandling = handling, TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var filter = Filter(named: named);

        var options = filter.Options;
        var amount = Describe<Amount>(filter);

        options.Should().NotBeSameAs(named);
        options.NumberHandling.Should().Be(JsonNumberHandling.Strict);
        options.TypeInfoResolver.Should().BeSameAs(named.TypeInfoResolver);
        options.PropertyNamingPolicy.Should().BeSameAs(JsonNamingPolicy.CamelCase);
        named.NumberHandling.Should().Be(handling, "the application's options are left as they are");
        amount.Type.Should().Be(JsonSchemaType.Number);
        amount.Examples![0].ToJsonString().Should().Be("1250.00");
        Describe<Tolerance>(filter).AnyOf.Should().BeNull("a real reads no named literal under numbers as numbers only");
    }

    /// <summary>
    /// A copy of the application's options keeps everything they hold but their number handling, their converters
    /// included, which write the examples: here one that writes an amount as text.
    /// </summary>
    [Fact]
    public void A_copy_of_the_application_s_options_keeps_their_converters()
    {
        var named = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new AmountAsText() } };

        var example = Describe<Amount>(Filter(named: named)).Examples![0];

        example.GetValueKind().Should().Be(JsonValueKind.String);
        example.GetValue<string>().Should().Be("1250.00");
    }

    /// <summary>
    /// Options that hold no resolver are given the one a serializer call would use, without which no example could be
    /// written as the type writes it, whether they read numbers from text or not.
    /// </summary>
    /// <param name="handling">How the options handle numbers.</param>
    [Theory]
    [InlineData(JsonNumberHandling.Strict)]
    [InlineData(JsonNumberHandling.WriteAsString)]
    public void Options_without_a_resolver_are_copied_with_the_one_a_serializer_call_would_use(JsonNumberHandling handling)
    {
        var named = new JsonSerializerOptions { NumberHandling = handling };

        var filter = Filter(named: named);

        filter.Options.Should().NotBeSameAs(named);
        filter.Options.TypeInfoResolver.Should().BeOfType<DefaultJsonTypeInfoResolver>();
        named.TypeInfoResolver.Should().BeNull();
        Describe<Amount>(filter).Examples![0].ToJsonString().Should().Be("1250.00");
    }

    [Fact]
    public void A_reference_handed_to_the_schema_filter_is_left_as_it_is()
    {
        var reference = new OpenApiSchemaReference(nameof(Iban));

        Filter().Apply(reference, Context(typeof(Iban)));

        reference.Description.Should().BeNull();
    }

    /// <summary>
    /// The <c>allOf</c> Swashbuckle wraps a reference in refers to the component, which is described: the wrapper is left
    /// as it is, though it is handed over under the value object's type.
    /// </summary>
    [Fact]
    public void The_allOf_a_reference_is_wrapped_in_is_left_as_it_is()
    {
        var wrapper = new OpenApiSchema { AllOf = [new OpenApiSchemaReference(nameof(Iban))], AdditionalPropertiesAllowed = false };

        Filter().Apply(wrapper, Context(typeof(Iban)));

        wrapper.Type.Should().BeNull();
        wrapper.AdditionalPropertiesAllowed.Should().BeFalse();
    }

    /// <summary>
    /// The schema Swashbuckle gives a value object's type, an object allowing no other property, is replaced by the
    /// underlying type's, which an empty <c>allOf</c> does not prevent.
    /// </summary>
    [Fact]
    public void An_object_allowing_no_other_property_becomes_the_underlying_type()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AllOf = [],
            AdditionalPropertiesAllowed = false,
            Properties = new Dictionary<string, IOpenApiSchema> { ["value"] = new OpenApiSchema { Type = JsonSchemaType.String } },
        };

        Filter().Apply(schema, Context(typeof(Quantity?)));

        schema.Type.Should().Be(JsonSchemaType.Integer);
        schema.AdditionalPropertiesAllowed.Should().BeTrue();
        schema.Properties.Should().BeEmpty();
    }

    [Fact]
    public void A_type_that_is_neither_a_value_object_nor_a_container_is_left_as_it_is()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.String };

        Filter().Apply(schema, Context(typeof(string)));

        schema.Type.Should().Be(JsonSchemaType.String);
        schema.Pattern.Should().BeNull();
    }

    /// <summary>
    /// Only a reference to a nullable value object is described in place: a non-nullable one stays a reference, as does
    /// one to a nullable enumeration, and an element Swashbuckle wrote in place already is kept.
    /// </summary>
    [Fact]
    public void Only_the_reference_to_a_nullable_value_object_an_element_is_is_described_in_place()
    {
        var quantities = Array(new OpenApiSchemaReference(nameof(Quantity)));
        var shades = Array(new OpenApiSchemaReference(nameof(Shade)));
        var inline = new OpenApiSchema { Type = JsonSchemaType.Integer };
        var written = Array(inline);
        var nullable = Array(new OpenApiSchemaReference(nameof(Quantity)));

        Filter().Apply(quantities, Context(typeof(List<Quantity>)));
        Filter().Apply(shades, Context(typeof(List<Shade?>)));
        Filter().Apply(written, Context(typeof(List<Quantity?>)));
        Filter().Apply(nullable, Context(typeof(List<Quantity?>)));

        quantities.Items.Should().BeOfType<OpenApiSchemaReference>();
        shades.Items.Should().BeOfType<OpenApiSchemaReference>();
        written.Items.Should().BeSameAs(inline);
        nullable.Items.Should().BeOfType<OpenApiSchema>().Which.Type.Should().Be(JsonSchemaType.Integer | JsonSchemaType.Null);

        static OpenApiSchema Array(IOpenApiSchema items) => new() { Type = JsonSchemaType.Array, Items = items };
    }

    /// <summary>
    /// A contract that names no element type, as another resolver may give, leaves the element as it is.
    /// </summary>
    [Fact]
    public void A_collection_whose_contract_names_no_element_is_left_as_it_is()
    {
        var schema = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchemaReference(nameof(Quantity)) };
        var contracts = Substitute.For<ISerializerDataContractResolver>();
        contracts.GetDataContractForType(typeof(IEnumerable)).Returns(DataContract.ForArray(typeof(IEnumerable), itemType: null));

        Filter(contracts: contracts).Apply(schema, Context(typeof(IEnumerable)));

        schema.Items.Should().BeOfType<OpenApiSchemaReference>();
    }

    /// <summary>
    /// The rules of a key are stated once: a <c>propertyNames</c> another filter wrote is kept, and a dictionary that is
    /// not generic, whose keys are anything, gets none.
    /// </summary>
    [Fact]
    public void A_dictionary_s_keys_are_described_only_when_nothing_described_them_and_they_are_value_objects()
    {
        var names = new OpenApiSchema { Type = JsonSchemaType.String };
        var described = new OpenApiSchema { Type = JsonSchemaType.Object, PropertyNames = names };
        var untyped = new OpenApiSchema { Type = JsonSchemaType.Object };
        var keyed = new OpenApiSchema { Type = JsonSchemaType.Object };
        var readOnly = new OpenApiSchema { Type = JsonSchemaType.Object };

        Filter().Apply(described, Context(typeof(Dictionary<CountryCode, int>)));
        Filter().Apply(untyped, Context(typeof(Hashtable)));
        Filter().Apply(keyed, Context(typeof(Dictionary<CountryCode, int>)));
        Filter().Apply(readOnly, Context(typeof(IReadOnlyDictionary<Quantity, int>)));

        described.PropertyNames.Should().BeSameAs(names);
        untyped.PropertyNames.Should().BeNull();
        keyed.PropertyNames.Should().BeOfType<OpenApiSchema>().Which.Enum.Should().HaveCount(3);
        readOnly.PropertyNames.Should().BeOfType<OpenApiSchema>().Which.Type.Should().Be(JsonSchemaType.String);
    }

    /// <summary>
    /// A property is described in place only when Swashbuckle refers to a nullable value object for it and lets it be
    /// <c>null</c>: one another resolver lists without its member is taken as nothing marks it required, one it says is
    /// not nullable keeps its reference, and one missing from the schema, or written in place already, is left alone.
    /// </summary>
    [Fact]
    public void A_property_is_described_in_place_only_when_it_refers_to_a_nullable_value_object_Swashbuckle_lets_be_null()
    {
        var inline = new OpenApiSchema { Type = JsonSchemaType.String };
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["unattributed"] = new OpenApiSchemaReference(nameof(Iban)),
                ["notNullable"] = new OpenApiSchemaReference(nameof(Iban)),
                ["inline"] = inline,
            },
        };
        var contracts = Contracts(DataContract.ForObject(typeof(SwashbuckleFilterTests),
        [
            new DataProperty("unattributed", typeof(Iban?), isNullable: true, isReadOnly: true),
            new DataProperty("notNullable", typeof(Iban?), isNullable: false),
            new DataProperty("inline", typeof(Iban?), isNullable: true),
            new DataProperty("missing", typeof(Iban?), isNullable: true),
        ]));

        Filter(contracts: contracts).Apply(schema, Context(typeof(SwashbuckleFilterTests)));

        schema.Properties["unattributed"].Should().BeOfType<OpenApiSchema>().Which.ReadOnly.Should().BeTrue();
        schema.Properties["unattributed"].Type.Should().Be(JsonSchemaType.String | JsonSchemaType.Null);
        schema.Properties["notNullable"].Should().BeOfType<OpenApiSchemaReference>();
        schema.Properties["inline"].Should().BeSameAs(inline);
        schema.Properties.Should().NotContainKey("missing");
    }

    /// <summary>
    /// Under <c>UseAllOfToExtendReferenceSchemas</c>, the wrapper of a property's reference is described again only when
    /// Swashbuckle marked it <c>null</c> alone and it wraps one reference to a nullable value object: a wrapper that
    /// states a type, states none, wraps nothing, more than one schema, a schema in place or the reference to an
    /// enumeration, and a schema marked <c>null</c> that wraps nothing, keep what they hold.
    /// </summary>
    [Fact]
    public void A_wrapper_is_described_again_only_when_Swashbuckle_marked_it_null_around_one_nullable_value_object()
    {
        var wrappers = new Dictionary<string, OpenApiSchema>
        {
            ["described"] = Wrapper(JsonSchemaType.Null, new OpenApiSchemaReference(nameof(Iban))),
            ["typed"] = Wrapper(JsonSchemaType.Null | JsonSchemaType.String, new OpenApiSchemaReference(nameof(Iban))),
            ["untyped"] = Wrapper(null, new OpenApiSchemaReference(nameof(Iban))),
            ["empty"] = Wrapper(JsonSchemaType.Null),
            ["twice"] = Wrapper(JsonSchemaType.Null, new OpenApiSchemaReference(nameof(Iban)), new OpenApiSchemaReference(nameof(Iban))),
            ["enumeration"] = Wrapper(JsonSchemaType.Null, new OpenApiSchemaReference(nameof(Shade))),
            ["inline"] = Wrapper(JsonSchemaType.Null, new OpenApiSchema { Type = JsonSchemaType.String }),
        };
        wrappers["unwrapped"] = new OpenApiSchema { Type = JsonSchemaType.Null };
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = wrappers.ToDictionary(static wrapper => wrapper.Key, static wrapper => (IOpenApiSchema)wrapper.Value),
        };
        var contracts = Contracts(DataContract.ForObject(typeof(SwashbuckleFilterTests),
        [
            new DataProperty("described", typeof(Iban?), isNullable: true),
            new DataProperty("typed", typeof(Iban?), isNullable: true),
            new DataProperty("untyped", typeof(Iban?), isNullable: true),
            new DataProperty("empty", typeof(Iban?), isNullable: true),
            new DataProperty("twice", typeof(Iban?), isNullable: true),
            new DataProperty("enumeration", typeof(Shade?), isNullable: true),
            new DataProperty("inline", typeof(Iban?), isNullable: true),
            new DataProperty("unwrapped", typeof(Iban?), isNullable: true),
        ]));

        Filter(contracts: contracts).Apply(schema, Context(typeof(SwashbuckleFilterTests)));

        wrappers["described"].Type.Should().BeNull();
        wrappers["described"].AllOf.Should().ContainSingle().Which.Should().BeOfType<OpenApiSchema>()
            .Which.Type.Should().Be(JsonSchemaType.String | JsonSchemaType.Null);
        wrappers["typed"].AllOf.Should().ContainSingle().Which.Should().BeOfType<OpenApiSchemaReference>();
        wrappers["untyped"].AllOf.Should().ContainSingle().Which.Should().BeOfType<OpenApiSchemaReference>();
        wrappers["empty"].AllOf.Should().BeEmpty();
        wrappers["twice"].AllOf.Should().HaveCount(2).And.AllBeOfType<OpenApiSchemaReference>();
        wrappers["enumeration"].AllOf.Should().ContainSingle().Which.Should().BeOfType<OpenApiSchemaReference>();
        wrappers["inline"].Type.Should().Be(JsonSchemaType.Null);
        wrappers["inline"].AllOf.Should().ContainSingle().Which.Type.Should().Be(JsonSchemaType.String);
        wrappers["unwrapped"].Type.Should().Be(JsonSchemaType.Null);
        foreach (var (name, wrapper) in wrappers)
        {
            schema.Properties[name].Should().BeSameAs(wrapper, "a wrapper is described where it is");
        }

        static OpenApiSchema Wrapper(JsonSchemaType? type, params IOpenApiSchema[] wrapped) => new() { Type = type, AllOf = [.. wrapped] };
    }

    /// <summary>
    /// An object whose schema holds no properties of its own, as the one Swashbuckle writes under
    /// <c>UseAllOfForInheritance</c>, whose properties sit in an <c>allOf</c>, is left as it is.
    /// </summary>
    [Fact]
    public void An_object_whose_schema_holds_no_properties_is_left_as_it_is()
    {
        var schema = new OpenApiSchema { AllOf = [new OpenApiSchema { Properties = new Dictionary<string, IOpenApiSchema>() }] };
        var contracts = Contracts(DataContract.ForObject(typeof(SwashbuckleFilterTests), [new DataProperty("alternate", typeof(Iban?), isNullable: true)]));

        Filter(contracts: contracts).Apply(schema, Context(typeof(SwashbuckleFilterTests)));

        schema.Properties.Should().BeNull();
    }

    /// <summary>
    /// The enumeration of a closed set is named after the component the repository gave the value object, whatever the
    /// identifier its selector would give, and, before the value object has a component, after the identifier the
    /// selector will give it.
    /// </summary>
    [Fact]
    public void A_closed_set_is_named_after_its_component_or_the_identifier_it_will_get()
    {
        var generator = new SchemaGeneratorOptions { SchemaIdSelector = static type => $"Selected{type.Name}" };
        var registered = new SchemaRepository();
        registered.RegisterType(typeof(CountryCode), "Country");
        var named = new OpenApiSchema();
        var unnamed = new OpenApiSchema();

        Filter(generator: generator).Apply(named, new SchemaFilterContext(typeof(CountryCode), null!, registered));
        Filter(generator: generator).Apply(unnamed, new SchemaFilterContext(typeof(CountryCode), null!, new SchemaRepository()));

        NameOf(named).Should().Be("Country");
        NameOf(unnamed).Should().Be("SelectedCountryCode");

        static string NameOf(OpenApiSchema schema)
            => ((JsonNodeExtension)schema.Extensions!["x-ms-enum"]).Node["name"]!.GetValue<string>();
    }

    [Fact]
    public void A_parameter_reference_is_left_as_it_is()
    {
        var generator = Substitute.For<ISchemaGenerator>();

        new ValueObjectParameterFilter().Apply(new OpenApiParameterReference("quantity"), Context(Parameter(typeof(Quantity), typeof(string)), generator));

        generator.ReceivedCalls().Should().BeEmpty();
    }

    /// <summary>
    /// A parameter is described again only when it is a value object, or an array of them, that Swashbuckle described
    /// from another type: one without a description of its own, one without a type, one MVC names as its model, and one
    /// that is no value object keep what Swashbuckle gave them.
    /// </summary>
    [Fact]
    public void A_parameter_is_described_again_only_when_Swashbuckle_described_a_value_object_from_another_type()
    {
        var generator = Substitute.For<ISchemaGenerator>();
        var filter = new ValueObjectParameterFilter();

        filter.Apply(new OpenApiParameter(), Context(description: null, generator));
        filter.Apply(new OpenApiParameter(), Context(new ApiParameterDescription { Name = "untyped" }, generator));
        filter.Apply(new OpenApiParameter(), Context(Parameter(typeof(Quantity), typeof(Quantity)), generator));
        filter.Apply(new OpenApiParameter(), Context(Parameter(typeof(int), typeof(string)), generator));
        filter.Apply(new OpenApiParameter(), Context(Parameter(typeof(Shade[]), typeof(string[])), generator));

        generator.ReceivedCalls().Should().BeEmpty();
    }

    /// <summary>
    /// A value object Swashbuckle described from its text, or with no model at all, is described from its declared type,
    /// as Swashbuckle describes an MVC action's, with the description of the component the schema refers to.
    /// </summary>
    [Fact]
    public void A_parameter_described_from_its_text_refers_to_its_value_object_with_the_component_s_description()
    {
        var repository = new SchemaRepository();
        repository.AddDefinition(nameof(Quantity), new OpenApiSchema { Description = "A quantity." });
        var generator = Substitute.For<ISchemaGenerator>();
        generator.GenerateSchema(typeof(Quantity), repository, Arg.Any<MemberInfo>(), Arg.Any<ParameterInfo>(), Arg.Any<ApiParameterRouteInfo>())
            .Returns(new OpenApiSchemaReference(nameof(Quantity)));
        var fromText = new OpenApiParameter();
        var withoutModel = new OpenApiParameter();

        new ValueObjectParameterFilter().Apply(fromText, Context(Parameter(typeof(Quantity), typeof(string)), generator, repository));
        new ValueObjectParameterFilter().Apply(withoutModel, Context(new ApiParameterDescription { Name = "q", Type = typeof(Quantity) }, generator, repository));

        fromText.Schema.Should().BeOfType<OpenApiSchemaReference>();
        fromText.Description.Should().Be("A quantity.");
        withoutModel.Description.Should().Be("A quantity.");
    }

    /// <summary>
    /// The schema is asked of Swashbuckle as Swashbuckle asks it for an MVC action's parameter: with the member, the
    /// parameter and the route, whose defaults, attributes and constraints it reads. A minimal API's parameters have no
    /// member, which only another description of the API could give.
    /// </summary>
    [Fact]
    public void A_parameter_is_described_with_its_member_its_parameter_and_its_route()
    {
        var repository = new SchemaRepository();
        var generator = Substitute.For<ISchemaGenerator>();
        var member = typeof(AccountFilter).GetProperty(nameof(AccountFilter.Least))!;
        var declaration = typeof(DocumentedAccountsController).GetMethod(nameof(DocumentedAccountsController.Get))!.GetParameters()[1];
        var route = new ApiParameterRouteInfo();
        var description = Parameter(typeof(Quantity), typeof(string));
        description.RouteInfo = route;

        new ValueObjectParameterFilter().Apply(
            new OpenApiParameter(),
            new ParameterFilterContext(description, generator, repository, new Microsoft.OpenApi.OpenApiDocument(), member, declaration));

        generator.Received(1).GenerateSchema(typeof(Quantity), repository, member, declaration, route);
    }

    /// <summary>
    /// A parameter keeps the description it has, and takes none from a schema that is no reference, from a reference
    /// that names no identifier, or from one to a component the repository does not hold.
    /// </summary>
    [Fact]
    public void A_parameter_takes_the_component_s_description_only_when_it_has_none_and_the_component_is_known()
    {
        var repository = new SchemaRepository();
        repository.AddDefinition(nameof(Quantity), new OpenApiSchema { Description = "A quantity." });
        var described = new OpenApiParameter { Description = "How many." };
        var array = new OpenApiParameter();
        var anonymous = new OpenApiParameter();
        var unknown = new OpenApiParameter();

        new ValueObjectParameterFilter().Apply(described, Context(Parameter(typeof(Quantity), typeof(string)), Returning(new OpenApiSchemaReference(nameof(Quantity))), repository));
        new ValueObjectParameterFilter().Apply(array, Context(Parameter(typeof(Quantity[]), typeof(string[])), Returning(new OpenApiSchema { Type = JsonSchemaType.Array }), repository));
        new ValueObjectParameterFilter().Apply(anonymous, Context(Parameter(typeof(Quantity), typeof(string)), Returning(new OpenApiSchemaReference(nameof(Quantity)) { Reference = new JsonSchemaReference() }), repository));
        new ValueObjectParameterFilter().Apply(unknown, Context(Parameter(typeof(Quantity), typeof(string)), Returning(new OpenApiSchemaReference(nameof(Iban))), repository));

        described.Description.Should().Be("How many.");
        array.Schema!.Type.Should().Be(JsonSchemaType.Array);
        array.Description.Should().BeNull();
        anonymous.Description.Should().BeNull();
        unknown.Description.Should().BeNull();

        static ISchemaGenerator Returning(IOpenApiSchema schema)
        {
            var generator = Substitute.For<ISchemaGenerator>();
            generator.GenerateSchema(Arg.Any<Type>(), Arg.Any<SchemaRepository>(), Arg.Any<MemberInfo>(), Arg.Any<ParameterInfo>(), Arg.Any<ApiParameterRouteInfo>())
                .Returns(schema);
            return generator;
        }
    }

    /// <summary>Builds the schema filter Swashbuckle would build, with what a test needs in place of the container's.</summary>
    private static ValueObjectSchemaFilter Filter(
        HttpJsonOptions? http = null,
        JsonSerializerOptions? named = null,
        ISerializerDataContractResolver? contracts = null,
        SchemaGeneratorOptions? generator = null)
        => new(
            Options.Create(http ?? new HttpJsonOptions()),
            Options.Create(generator ?? new SchemaGeneratorOptions()),
            contracts ?? new JsonSerializerDataContractResolver(new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            new ValueObjectSwaggerSettings(named));

    /// <summary>Describes a value object's component as the filter does.</summary>
    private static OpenApiSchema Describe<TValueObject>(ValueObjectSchemaFilter filter)
    {
        var schema = new OpenApiSchema();
        filter.Apply(schema, Context(typeof(TValueObject)));
        return schema;
    }

    private static SchemaFilterContext Context(Type type) => new(type, null!, new SchemaRepository());

    private static ParameterFilterContext Context(ApiParameterDescription? description, ISchemaGenerator generator, SchemaRepository? repository = null)
        => new(description!, generator, repository ?? new SchemaRepository(), new Microsoft.OpenApi.OpenApiDocument());

    /// <summary>Describes a parameter of a declared type whose model is another.</summary>
    private static ApiParameterDescription Parameter(Type declared, Type model)
        => new() { Name = "parameter", Type = declared, ModelMetadata = Metadata.GetMetadataForType(model), Source = BindingSource.Query };

    private static ISerializerDataContractResolver Contracts(DataContract contract)
    {
        var contracts = Substitute.For<ISerializerDataContractResolver>();
        contracts.GetDataContractForType(Arg.Any<Type>()).Returns(contract);
        return contracts;
    }

    /// <summary>A converter of an application's that writes an amount as text, which only writes here.</summary>
    private sealed class AmountAsText : JsonConverter<Amount>
    {
        /// <inheritdoc />
        public override Amount Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, Amount value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
    }
}
