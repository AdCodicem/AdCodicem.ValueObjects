using System.Text;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.NJsonSchema;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NJsonSchema.NewtonsoftJson.Generation;
using NSubstitute;
using RegistrySchemaType = Confluent.SchemaRegistry.SchemaType;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>A message holding value objects, which a Kafka topic carries and an API takes in a body.</summary>
public sealed class Remittance
{
    /// <summary>Gets or sets the account.</summary>
    public Iban Iban { get; set; }

    /// <summary>Gets or sets the quantity.</summary>
    public Quantity Quantity { get; set; }

    /// <summary>Gets or sets the destination, if known.</summary>
    public CountryCode? Country { get; set; }
}

/// <summary>
/// The NJsonSchema package on the next major: NSwag's OpenAPI document over TestHost, beside Microsoft.AspNetCore.OpenApi
/// 11, and Confluent's JSON serdes for the Schema Registry over a substitute registry client, each value object one
/// definition of its underlying type carrying its rules, and a refused message refused with its code.
/// </summary>
public sealed class JsonSchemaGenerationTests
{
    [Fact]
    public async Task NSwag_describes_each_value_object_as_one_component_of_its_underlying_type()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();

        // MVC's System.Text.Json formatter, or NSwag describes the Newtonsoft.Json settings
        // Microsoft.AspNetCore.Mvc.NewtonsoftJson, which this project references, would have.
        builder.Services.AddControllers();
        builder.Services.ConfigureHttpJsonOptions(static options => options.SerializerOptions.AddValueObjects());
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();
        builder.Services.AddOpenApiDocument(static settings => settings.SchemaSettings.AddValueObjects());
        await using var application = builder.Build();
        application.MapGet("/remittances/{iban}", static (Iban iban, Quantity quantity, CountryCode? country, [ParsesAs(typeof(Iban))] string? other) => Results.Ok());
        application.MapPost("/remittances", static (Remittance remittance) => Results.Ok());
        application.UseOpenApi();
        application.MapOpenApi();
        await application.StartAsync(TestContext.Current.CancellationToken);
        using var client = application.GetTestClient();

        var document = JObject.Parse(await client.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken));
        (await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken)).IsSuccessStatusCode.Should().BeTrue();
        await application.StopAsync(TestContext.Current.CancellationToken);

        var schemas = document["components"]!["schemas"]!;
        schemas["Iban"]!["pattern"]!.ToString().Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
        schemas["Quantity"]!["type"]!.ToString().Should().Be("integer");
        schemas["Quantity"]!["maximum"]!.Value<decimal>().Should().Be(100m);
        schemas["CountryCode"]!["enum"]!.ToString(Newtonsoft.Json.Formatting.None).Should().Be("""["FR","BE","LU"]""");
        schemas["Remittance"]!["properties"]!["country"]!.ToString(Newtonsoft.Json.Formatting.None)
            .Should().Be("""{"nullable":true,"oneOf":[{"$ref":"#/components/schemas/CountryCode"}]}""");
        var parameters = (JArray)document["paths"]!["/remittances/{iban}"]!["get"]!["parameters"]!;
        parameters.Single(static parameter => (string?)parameter["name"] == "iban")["schema"]!.ToString(Newtonsoft.Json.Formatting.None)
            .Should().Be("""{"$ref":"#/components/schemas/Iban"}""");
        parameters.Single(static parameter => (string?)parameter["name"] == "other")["schema"]!["minLength"]!.Value<int>().Should().Be(15);
    }

    [Fact]
    public async Task Confluent_s_serdes_register_the_rules_and_refuse_a_tampered_message_with_its_code()
    {
        var schemas = new List<string>();
        var registry = Substitute.For<ISchemaRegistryClient>();
        registry.Config.Returns(new Dictionary<string, string>());
        registry.MaxCachedSchemas.Returns(1000);
        registry.GetAssociationsByResourceNameAsync(default!, default!, default!, default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromResult(new List<Association>()));
        registry.RegisterSchemaWithResponseAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<bool>()).Returns(call =>
        {
            schemas.Add(call.ArgAt<Schema>(1).SchemaString);
            return Task.FromResult(new RegisteredSchema(call.ArgAt<string>(0), 1, schemas.Count, schemas[^1], call.ArgAt<Schema>(1).SchemaType, []));
        });
        registry.GetSchemaBySubjectAndIdAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>())
            .Returns(call => Task.FromResult(new Schema(schemas[call.ArgAt<int>(1) - 1], [], RegistrySchemaType.Json)));
        var settings = new NewtonsoftJsonSchemaGeneratorSettings();
        settings.SerializerSettings.AddValueObjects();
        settings.AddValueObjects();
        var context = new SerializationContext(MessageComponentType.Value, "remittances");
        var remittance = new Remittance { Iban = Iban.Create("DE89370400440532013000"), Quantity = Quantity.Create(7), Country = CountryCode.France };

        var bytes = await new JsonSerializer<Remittance>(registry, new JsonSerializerConfig(), settings).SerializeAsync(remittance, context);

        JObject.Parse(schemas.Single())["definitions"]!["Quantity"]!["type"]!.ToString().Should().Be("integer");
        Encoding.UTF8.GetString(bytes, 5, bytes.Length - 5).Should().Be("""{"Iban":"DE89370400440532013000","Quantity":7,"Country":"FR"}""");
        var deserializer = new JsonDeserializer<Remittance>(registry, new JsonDeserializerConfig(), settings);
        var read = await deserializer.DeserializeAsync(bytes, false, context);
        read.Iban.Should().Be(remittance.Iban);
        read.Country.Should().Be(CountryCode.France);

        var tampered = bytes.Take(5).Concat(Encoding.UTF8.GetBytes("""{"Iban":"XX","Quantity":7,"Country":null}""")).ToArray();
        var refusal = await Assert.ThrowsAsync<Newtonsoft.Json.JsonSerializationException>(() => deserializer.DeserializeAsync(tampered, false, context));
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.TooShort);
    }
}
