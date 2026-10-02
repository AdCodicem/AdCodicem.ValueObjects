using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.OpenApi;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// The schema transformer called directly, with what no declaration compiled through the generator produces.
/// </summary>
public partial class SchemaTransformerTests
{
    /// <summary>
    /// A bound written in the schema a hand-made registration supplies, which the generator would have refused: one
    /// beyond the range of a double, and one that is no number at all. JSON has no number for either.
    /// </summary>
    [Fact]
    public async Task A_bound_the_document_cannot_write_as_a_number_is_left_out()
    {
        // The generated registration of the assembly runs first, so that it cannot replace the one under test.
        ValueObjectRegistry.EnsureAssemblyRegistered(typeof(Reading).Assembly);
        ValueObjectRegistry.Register(ValueObjectDescriptor.For<Reading, double>(new ValueObjectSchema
        {
            Minimum = "-1e400",
            Maximum = "high",
        }));
        var schema = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(schema, ContextFor<Reading>(), TestContext.Current.CancellationToken);

        schema.Type.Should().Be(JsonSchemaType.Number);
        schema.Minimum.Should().BeNull();
        schema.Maximum.Should().BeNull();
    }

    /// <summary>
    /// A known value is typed as the underlying value where the generator wrote the registration, and where the
    /// registry read an annotation and the type parsed the value. A hand-made schema holds whatever it was built with,
    /// and an annotation read by reflection keeps what the type could not parse. Such a value is listed as its text,
    /// rather than failing the document.
    /// </summary>
    [Fact]
    public async Task A_known_value_that_is_not_of_the_underlying_type_is_listed_as_its_text()
    {
        ValueObjectRegistry.EnsureAssemblyRegistered(typeof(Rate).Assembly);
        ValueObjectRegistry.Register(ValueObjectDescriptor.For<Rate, decimal>(new ValueObjectSchema
        {
            IsClosedValueSet = true,
            KnownValues = ["0.5", 1.5m, 2],
        }));
        var schema = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(schema, ContextFor<Rate>(), TestContext.Current.CancellationToken);

        schema.Enum.Should().NotBeNull();
        schema.Enum!.Select(value => value.ToJsonString()).Should().Equal("\"0.5\"", "1.5", "\"2\"");
    }

    /// <summary>
    /// Nothing checks an example when the type compiles, and one the type refuses has no form the type writes: it is
    /// published as the text it was declared as, rather than failing the document.
    /// </summary>
    [Fact]
    public async Task An_example_the_type_refuses_is_published_as_its_text()
    {
        ValueObjectRegistry.EnsureAssemblyRegistered(typeof(Tally).Assembly);
        ValueObjectRegistry.Register(ValueObjectDescriptor.For<Tally, int>(new ValueObjectSchema { Example = "many" }));
        var schema = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(schema, ContextFor<Tally>(), TestContext.Current.CancellationToken);

        schema.Examples.Should().ContainSingle().Which!.ToJsonString().Should().Be("\"many\"");
    }

    /// <summary>
    /// An example the type accepts may still be one its converter cannot write under the options: JSON has no number
    /// for NaN. It is published as its text as well, rather than failing the document.
    /// </summary>
    [Fact]
    public async Task An_example_the_converter_cannot_write_is_published_as_its_text()
    {
        var schema = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(schema, ContextFor<Gauge>(), TestContext.Current.CancellationToken);

        schema.Type.Should().Be(JsonSchemaType.Number);
        schema.Examples.Should().ContainSingle().Which!.ToJsonString().Should().Be("\"NaN\"");
    }

    /// <summary>
    /// A value object written as a string with a maximum alone states that one bound, and a description it did not
    /// have becomes that sentence.
    /// </summary>
    [Fact]
    public async Task A_maximum_alone_of_a_value_object_written_as_a_string_is_stated_alone()
    {
        ValueObjectRegistry.EnsureAssemblyRegistered(typeof(Deadline).Assembly);
        ValueObjectRegistry.Register(ValueObjectDescriptor.For<Deadline, DateOnly>(new ValueObjectSchema { Maximum = "2030-12-31" }));
        var schema = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(schema, ContextFor<Deadline>(), TestContext.Current.CancellationToken);

        schema.Maximum.Should().BeNull();
        schema.Extensions.Should().ContainKey("x-maximum").And.NotContainKey("x-minimum");
        schema.Description.Should().Be("At most 2030-12-31.");
    }

    /// <summary>
    /// The check compares a normalized value with the bound as declared, so the bound is published as declared: never
    /// normalized as an input would be, which would move it to the first of its month here.
    /// </summary>
    [Fact]
    public async Task A_bound_is_published_as_declared_whatever_the_normalizer_does_to_an_input()
    {
        var schema = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(schema, ContextFor<BillingMonth>(), TestContext.Current.CancellationToken);

        BillingMonth.TryCreate(new DateOnly(2030, 6, 20), out _).Should().BeTrue("the normalized value is the first of June");
        ((JsonNode)((JsonNodeExtension)schema.Extensions!["x-maximum"]).Node).GetValue<string>().Should().Be("2030-06-15");
        schema.Description.Should().EndWith("\n\nAt most 2030-06-15.");
    }

    /// <summary>
    /// A bound the underlying type cannot read, which only a schema made by hand holds, is stated as it was written.
    /// </summary>
    [Fact]
    public async Task A_bound_the_type_cannot_read_is_stated_as_written()
    {
        ValueObjectRegistry.EnsureAssemblyRegistered(typeof(Moment).Assembly);
        ValueObjectRegistry.Register<Moment, DateOnly>(new ValueObjectSchema { Minimum = "soon" });
        var schema = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(schema, ContextFor<Moment>(), TestContext.Current.CancellationToken);

        ((JsonNodeExtension)schema.Extensions!["x-minimum"]).Node.ToJsonString().Should().Be("\"soon\"");
        schema.Description.Should().Be("At least soon.");
    }

    /// <summary>
    /// A bound the type writes as no string, which only a schema made by hand gives a value object documented as no
    /// number, is quoted in the sentence as its JSON.
    /// </summary>
    [Fact]
    public async Task A_bound_written_as_no_string_is_quoted_as_its_json()
    {
        ValueObjectRegistry.EnsureAssemblyRegistered(typeof(Toggle).Assembly);
        ValueObjectRegistry.Register<Toggle, bool>(new ValueObjectSchema { Minimum = "false" });
        var schema = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(schema, ContextFor<Toggle>(), TestContext.Current.CancellationToken);

        schema.Type.Should().Be(JsonSchemaType.Boolean);
        ((JsonNodeExtension)schema.Extensions!["x-minimum"]).Node.ToJsonString().Should().Be("false");
        schema.Description.Should().Be("At least false.");
    }

    /// <summary>
    /// A value object written by hand that nothing registered binds through the model binder and validates through
    /// FluentValidation, which both resolve it by reflection. The transformer resolves it the same way, so it is
    /// documented as its underlying value rather than left as the object the serializer would describe.
    /// </summary>
    [Fact]
    public async Task A_hand_written_value_object_nothing_registered_is_documented_as_its_underlying_value()
    {
        var schema = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(
            schema,
            ContextFor<HandWrittenLevel>(),
            TestContext.Current.CancellationToken);

        schema.Type.Should().Be(JsonSchemaType.Integer);
        schema.Minimum.Should().Be("1", "the registry reads the bound off the hook the type implements");
    }

    /// <summary>
    /// A construction of a generic value object is described from the schema the generator wrote on it, which the
    /// registry finds once asked for the construction.
    /// </summary>
    [Fact]
    public async Task A_construction_of_a_generic_value_object_is_documented_from_its_generated_schema()
    {
        var reference = new OpenApiSchema();
        var stock = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(
            reference,
            ContextFor<Reference<PurchaseOrder>>(),
            TestContext.Current.CancellationToken);
        await new ValueObjectSchemaTransformer().TransformAsync(
            stock,
            ContextFor<Catalog<Iban>.Stock>(),
            TestContext.Current.CancellationToken);

        reference.Type.Should().Be(JsonSchemaType.String);
        reference.MaxLength.Should().Be(12);
        reference.Examples.Should().ContainSingle().Which!.ToJsonString().Should().Be("\"PO-1042\"");
        stock.Type.Should().Be(JsonSchemaType.Integer);
        stock.Minimum.Should().Be("0");
    }

    /// <summary>
    /// Under <c>WriteAsString</c> a number goes on the wire as text, its example and its known values with it. It is
    /// documented as System.Text.Json documents a bare number under the same options, a number or a string held to a
    /// numeric pattern, and its bounds, which <c>minimum</c> and <c>maximum</c> apply to the number alone, are also
    /// stated as they are for a value written as a string.
    /// </summary>
    [Fact]
    public async Task A_number_written_as_text_is_documented_as_a_number_or_its_text()
    {
        var options = new JsonSerializerOptions(JsonSerializerOptions.Default) { NumberHandling = JsonNumberHandling.WriteAsString };
        var socket = new OpenApiSchema();
        var tier = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(socket, ContextFor<Socket>(options), TestContext.Current.CancellationToken);
        await new ValueObjectSchemaTransformer().TransformAsync(tier, ContextFor<Tier>(options), TestContext.Current.CancellationToken);

        socket.Type.Should().Be(JsonSchemaType.Integer | JsonSchemaType.String);
        socket.Pattern.Should().Be(@"^-?(?:0|[1-9]\d*)$");
        socket.Minimum.Should().Be("1");
        ((JsonNodeExtension)socket.Extensions!["x-minimum"]).Node.ToJsonString().Should().Be("\"1\"");
        socket.Description.Should().EndWith("\n\nAt least 1.");
        socket.Examples.Should().ContainSingle().Which!.ToJsonString().Should().Be("\"8080\"");
        tier.Enum!.Select(value => value.ToJsonString()).Should().Equal("\"1\"", "\"10\"");
    }

    /// <summary>
    /// A number that may only be read as text is still written as a number, which <c>minimum</c> and <c>maximum</c>
    /// describe: the type admits the text, and nothing else changes. A decimal and a real are held to their own pattern.
    /// </summary>
    [Fact]
    public async Task A_number_read_from_text_is_documented_as_a_number_or_its_text()
    {
        var options = new JsonSerializerOptions(JsonSerializerOptions.Default) { NumberHandling = JsonNumberHandling.AllowReadingFromString };
        var socket = new OpenApiSchema();
        var rate = new OpenApiSchema();
        var gauge = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(socket, ContextFor<Socket>(options), TestContext.Current.CancellationToken);
        await new ValueObjectSchemaTransformer().TransformAsync(rate, ContextFor<Rate>(options), TestContext.Current.CancellationToken);
        await new ValueObjectSchemaTransformer().TransformAsync(gauge, ContextFor<Gauge>(options), TestContext.Current.CancellationToken);

        socket.Type.Should().Be(JsonSchemaType.Integer | JsonSchemaType.String);
        socket.Minimum.Should().Be("1");
        socket.Extensions.Should().BeNullOrEmpty();
        socket.Examples.Should().ContainSingle().Which!.ToJsonString().Should().Be("8080");
        rate.Pattern.Should().Be(@"^-?(?:0|[1-9]\d*)(?:\.\d+)?$");
        gauge.Pattern.Should().Be(@"^-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$");
    }

    /// <summary>
    /// Under <c>AllowNamedFloatingPointLiterals</c> a real may also be NaN or an infinity, written as text: it is the
    /// number or one of the literals, as System.Text.Json documents a bare real, but only the literals its bounds let
    /// through, since a bound refuses NaN and the infinity on its side.
    /// </summary>
    [Fact]
    public async Task A_real_under_named_literals_is_the_number_or_the_literals_its_bounds_let_through()
    {
        var options = new JsonSerializerOptions(JsonSerializerOptions.Default)
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals | JsonNumberHandling.WriteAsString,
        };
        var gauge = new OpenApiSchema();
        var depth = new OpenApiSchema();
        var socket = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(gauge, ContextFor<Gauge>(options), TestContext.Current.CancellationToken);
        await new ValueObjectSchemaTransformer().TransformAsync(depth, ContextFor<Depth>(options), TestContext.Current.CancellationToken);
        await new ValueObjectSchemaTransformer().TransformAsync(socket, ContextFor<Socket>(options), TestContext.Current.CancellationToken);

        gauge.Type.Should().BeNull("the alternatives carry it");
        gauge.AnyOf.Should().HaveCount(2);
        gauge.AnyOf![0].Type.Should().Be(JsonSchemaType.Number | JsonSchemaType.String);
        gauge.AnyOf[1].Enum!.Select(value => value.ToJsonString()).Should().Equal("\"NaN\"", "\"Infinity\"", "\"-Infinity\"");
        gauge.Examples.Should().ContainSingle().Which!.ToJsonString().Should().Be("\"NaN\"");
        depth.AnyOf![0].Minimum.Should().Be("0");
        depth.AnyOf[1].Enum!.Select(value => value.ToJsonString()).Should().Equal("\"Infinity\"");
        socket.AnyOf.Should().BeNull("an integer has no named literal");
    }

    /// <summary>
    /// A real whose bounds let no literal through is documented as the number alone.
    /// </summary>
    [Fact]
    public async Task A_real_whose_bounds_refuse_every_literal_is_the_number_alone()
    {
        var options = new JsonSerializerOptions(JsonSerializerOptions.Default) { NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
        var schema = new OpenApiSchema();

        await new ValueObjectSchemaTransformer().TransformAsync(schema, ContextFor<Latitude>(options), TestContext.Current.CancellationToken);

        schema.AnyOf.Should().BeNull();
        schema.Type.Should().Be(JsonSchemaType.Number);
        schema.Minimum.Should().Be("-90");
    }

    private static OpenApiSchemaTransformerContext ContextFor<T>(JsonSerializerOptions? options = null) => new()
    {
        DocumentName = "v1",
        JsonTypeInfo = (options ?? JsonSerializerOptions.Default).GetTypeInfo(typeof(T)),
        JsonPropertyInfo = null,
        ParameterDescription = null,
        ApplicationServices = new ServiceCollection().BuildServiceProvider(),
    };

    /// <summary>A reading no other test uses, whose registration one test replaces.</summary>
    [ValueObject<double>]
    public readonly partial struct Reading;

    /// <summary>A rate no other test uses, whose registration one test replaces.</summary>
    [ValueObject<decimal>]
    public readonly partial struct Rate;

    /// <summary>A deadline no other test uses, whose registration one test replaces.</summary>
    [ValueObject<DateOnly>]
    public readonly partial struct Deadline;

    /// <summary>A gauge over a double nothing bounds, whose example JSON has no number for.</summary>
    [ValueObject<double>(Example = "NaN")]
    public readonly partial struct Gauge;

    /// <summary>A depth, never negative, so it holds positive infinity and no other literal.</summary>
    [ValueObject<double>]
    public readonly partial struct Depth : IValueObjectMinimum<double>
    {
        public static double Minimum => 0;
    }

    /// <summary>A billing month, held as its first day, whose upper bound falls in the middle of a month.</summary>
    [ValueObject<DateOnly>]
    public readonly partial struct BillingMonth : IValueObjectNormalizer<DateOnly>, IValueObjectMaximum<DateOnly>
    {
        public static DateOnly Maximum => new(2030, 6, 15);

        public static DateOnly NormalizeValue(DateOnly value) => new(value.Year, value.Month, 1);
    }

    /// <summary>A toggle no other test uses, whose registration one test replaces with a bound.</summary>
    [ValueObject<bool>]
    public readonly partial struct Toggle;

    /// <summary>A moment no other test uses, whose registration one test replaces with a bound it cannot read.</summary>
    [ValueObject<DateOnly>]
    public readonly partial struct Moment;

    /// <summary>A socket's port, never zero.</summary>
    [ValueObject<ushort>(Example = "8080")]
    public readonly partial struct Socket : IValueObjectMinimum<ushort>
    {
        public static ushort Minimum => 1;
    }

    /// <summary>A tier, one of two numbers.</summary>
    [ValueObject<int>(ValueSet = ValueSetKind.Closed)]
    [KnownValue("Low", 1)]
    [KnownValue("High", 10)]
    public readonly partial struct Tier;

    /// <summary>A tally no other test uses, whose registration one test replaces.</summary>
    [ValueObject<int>]
    public readonly partial struct Tally;
}
