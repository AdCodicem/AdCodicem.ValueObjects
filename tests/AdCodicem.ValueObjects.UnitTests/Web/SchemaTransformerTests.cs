using System.Text.Json;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.OpenApi;
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
    /// A known value is typed as the underlying value only where the generator wrote the registration. An annotation
    /// read by reflection holds what the attribute was given - a decimal written as text, as it has to be - and a
    /// hand-made schema holds whatever it was built with. Such a value is listed as its text, rather than failing the
    /// document.
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
}
