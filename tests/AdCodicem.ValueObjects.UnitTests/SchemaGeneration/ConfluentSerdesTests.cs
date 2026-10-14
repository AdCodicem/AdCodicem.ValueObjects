using System.Text;
using AdCodicem.ValueObjects.NewtonsoftJson;
using AdCodicem.ValueObjects.NJsonSchema;
using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Confluent.SchemaRegistry.Serdes;
using Newtonsoft.Json.Linq;
using NJsonSchema.NewtonsoftJson.Generation;
using RegistrySchemaType = Confluent.SchemaRegistry.SchemaType;

namespace AdCodicem.ValueObjects.UnitTests.SchemaGeneration;

/// <summary>
/// Confluent's JSON serializer and deserializer for the Schema Registry, over a substitute registry client, with the
/// schema settings the Kafka guide gives: <c>ValueObjectConverter</c> first, then <c>AddValueObjects()</c>.
/// </summary>
public sealed class ConfluentSerdesTests
{
    private static readonly SerializationContext Context = new(MessageComponentType.Value, "orders");

    [Fact]
    public async Task The_registered_schema_holds_each_value_object_s_rules_and_the_message_round_trips()
    {
        var registry = new Registry();
        var settings = SchemaJson.NewtonsoftWithConverter();
        var order = SchemaOrder.Sample();

        var bytes = await new JsonSerializer<SchemaOrder>(registry.Client, new JsonSerializerConfig(), settings).SerializeAsync(order, Context);

        var registered = SchemaJson.Parse(registry.Schemas.Single());
        registered["$schema"]!.ToString().Should().Be("http://json-schema.org/draft-04/schema#");
        SchemaJson.Definition(registered, "Quantity").Should().Contain("\"type\":\"integer\"").And.Contain("\"maximum\":1000.0");
        SchemaJson.Definition(registered, "Iban").Should().Contain("\"pattern\":\"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$\"").And.Contain("\"x-example\"");
        SchemaJson.Definition(registered, "CountryCode").Should().Contain("\"enum\":[\"FR\",\"BE\",\"LU\"]");

        var payload = Payload(bytes);
        payload.Should().Contain("\"Quantity\":7").And.Contain("\"Consent\":true").And.Contain("\"Amount\":12.5");

        var read = await new JsonDeserializer<SchemaOrder>(registry.Client, new JsonDeserializerConfig(), settings).DeserializeAsync(bytes, false, Context);
        read.Iban.Should().Be(order.Iban);
        read.Quantity.Should().Be(order.Quantity);
        read.Amount.Should().Be(order.Amount);
        read.Occurred.Should().Be(order.Occurred);
        read.Order.Should().Be(order.Order);
        read.OptionalQuantities.Should().Equal(order.OptionalQuantities);
        read.AmountByCountry.Should().BeEquivalentTo(order.AmountByCountry);
    }

    [Theory]
    [InlineData("Iban", "\"XX\"", ValueObjectErrorCodes.TooShort)]
    [InlineData("Quantity", "5000", ValueObjectErrorCodes.OutOfRange)]
    [InlineData("Country", "\"ZZ\"", ValueObjectErrorCodes.NotAKnownValue)]
    public async Task A_tampered_message_is_refused_by_the_converter_with_the_code_of_the_rule(string member, string json, string code)
    {
        var registry = new Registry();
        var settings = SchemaJson.NewtonsoftWithConverter();
        var bytes = await new JsonSerializer<SchemaOrder>(registry.Client, new JsonSerializerConfig(), settings).SerializeAsync(SchemaOrder.Sample(), Context);
        var message = JObject.Parse(Payload(bytes));
        message[member] = JToken.Parse(json);
        var tampered = bytes.Take(5).Concat(Encoding.UTF8.GetBytes(message.ToString(Newtonsoft.Json.Formatting.None))).ToArray();

        var read = () => new JsonDeserializer<SchemaOrder>(registry.Client, new JsonDeserializerConfig(), settings).DeserializeAsync(tampered, false, Context);

        var refusal = (await read.Should().ThrowAsync<Newtonsoft.Json.JsonSerializationException>()).Which;
        ValueObjectErrors.TryGetCode(refusal, out var carried).Should().BeTrue();
        carried.Should().Be(code);
        refusal.Message.Should().NotContain(json.Trim('"'), "the refused value is never quoted");
    }

    [Fact]
    public async Task A_message_holding_a_value_its_type_refuses_is_never_produced()
    {
        var order = SchemaOrder.Sample();
#pragma warning disable VO0010 // The default instance, which CustomerId refuses.
        order.Customer = default;
#pragma warning restore VO0010

        var write = () => new JsonSerializer<SchemaOrder>(new Registry().Client, new JsonSerializerConfig(), SchemaJson.NewtonsoftWithConverter()).SerializeAsync(order, Context);

        var refusal = (await write.Should().ThrowAsync<Newtonsoft.Json.JsonSerializationException>()).Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.Required);
    }

    [Fact]
    public async Task Without_the_package_the_converter_s_numbers_fail_Confluent_s_own_validation_with_no_code()
    {
        var settings = new NewtonsoftJsonSchemaGeneratorSettings();
        settings.SerializerSettings.AddValueObjects();

        var write = () => new JsonSerializer<SchemaOrder>(new Registry().Client, new JsonSerializerConfig(), settings).SerializeAsync(SchemaOrder.Sample(), Context);

        var refusal = (await write.Should().ThrowAsync<InvalidDataException>()).Which;
        refusal.Message.Should().Contain("#/Quantity").And.Contain("#/Consent");
        ValueObjectErrors.TryGetCode(refusal, out _).Should().BeFalse();
    }

    [Fact]
    public async Task Without_the_converter_numbers_travel_as_text_and_the_default_consumer_cannot_read_an_offset_back()
    {
        var registry = new Registry();
        var settings = SchemaJson.NewtonsoftText();

        var bytes = await new JsonSerializer<SchemaOrder>(registry.Client, new JsonSerializerConfig(), settings).SerializeAsync(SchemaOrder.Sample(), Context);

        Payload(bytes).Should().Contain("\"Quantity\":\"7\"").And.Contain("\"Consent\":\"True\"");
        SchemaJson.Definition(SchemaJson.Parse(registry.Schemas.Single()), "Quantity").Should().Contain("\"type\":\"string\"");

        var read = () => new JsonDeserializer<SchemaOrder>(registry.Client, new JsonDeserializerConfig(), settings).DeserializeAsync(bytes, false, Context);
        (await read.Should().ThrowAsync<Newtonsoft.Json.JsonSerializationException>()).Which.Message.Should().Contain(nameof(OccurredAt));
    }

    private static string Payload(byte[] bytes) => Encoding.UTF8.GetString(bytes, 5, bytes.Length - 5);

    /// <summary>A registry client that keeps the schemas registered with it, in memory, as a registry would.</summary>
    private sealed class Registry
    {
        public Registry()
        {
            Client.Config.Returns(new Dictionary<string, string>());
            Client.MaxCachedSchemas.Returns(1000);

            // Confluent 2.16's default subject name strategy asks the registry for the subject associated with the topic.
            Client.GetAssociationsByResourceNameAsync(default!, default!, default!, default!, default!, default, default)
                .ReturnsForAnyArgs(Task.FromResult(new List<Association>()));
            Client.RegisterSchemaWithResponseAsync(Arg.Any<string>(), Arg.Any<Schema>(), Arg.Any<bool>())
                .Returns(call =>
                {
                    var schema = call.ArgAt<Schema>(1);
                    Schemas.Add(schema.SchemaString);

                    return Task.FromResult(new RegisteredSchema(call.ArgAt<string>(0), 1, Schemas.Count, schema.SchemaString, schema.SchemaType, schema.References));
                });
            Client.GetSchemaBySubjectAndIdAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>())
                .Returns(call => Task.FromResult(new Schema(Schemas[call.ArgAt<int>(1) - 1], [], RegistrySchemaType.Json)));
        }

        public ISchemaRegistryClient Client { get; } = Substitute.For<ISchemaRegistryClient>();

        public List<string> Schemas { get; } = [];
    }
}
