using System.Text.Json;
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
    }

    private static OpenApiSchemaTransformerContext ContextFor<T>() => new()
    {
        DocumentName = "v1",
        JsonTypeInfo = JsonSerializerOptions.Default.GetTypeInfo(typeof(T)),
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

    /// <summary>A tally no other test uses, whose registration one test replaces.</summary>
    [ValueObject<int>]
    public readonly partial struct Tally;
}
