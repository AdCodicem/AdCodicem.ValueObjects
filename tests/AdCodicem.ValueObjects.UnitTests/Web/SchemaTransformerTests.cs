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
}
