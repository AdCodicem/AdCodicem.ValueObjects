using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Json;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Covers the one case the <c>[JsonConverter]</c> attribute cannot reach on its own: a serializer context, whose
/// generator runs on the original compilation and therefore never sees the attribute the value object generator
/// emits.
/// </summary>
public partial class JsonSourceGenerationTests
{
    internal sealed record Transfer(Iban Account, Amount Total, CountryCode Country, BirthDate Birth);

    [JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
    [JsonSerializable(typeof(Transfer))]
    internal sealed partial class TransferContext : JsonSerializerContext;

    private static Transfer Sample => new(
        Iban.Create("DE89370400440532013000"),
        Amount.Create(99.5m),
        CountryCode.Belgium,
        BirthDate.Create(new DateOnly(1980, 5, 17)));

    [Fact]
    public void A_serializer_context_writes_the_bare_underlying_values()
    {
        var json = JsonSerializer.Serialize(Sample, TransferContext.Default.Transfer);

        json.Should().Be(
            """
            {"Account":"DE89370400440532013000","Total":99.50,"Country":"BE","Birth":"1980-05-17"}
            """);
    }

    [Fact]
    public void A_serializer_context_reads_the_bare_underlying_values()
    {
        const string json =
            """
            {"Account":"de89 3704 0044 0532 013000","Total":99.5,"Country":"be","Birth":"1980-05-17"}
            """;

        var transfer = JsonSerializer.Deserialize(json, TransferContext.Default.Transfer)!;

        transfer.Should().Be(Sample);
    }

    [Fact]
    public void A_serializer_context_still_enforces_the_rules()
    {
        var act = () => JsonSerializer.Deserialize(
            """{"Account":"DE89370400440532013000","Total":-1,"Country":"BE","Birth":"1980-05-17"}""",
            TransferContext.Default.Transfer);

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void The_generator_publishes_a_converter_for_every_value_object_of_the_assembly()
    {
        ValueObjectJsonRegistry.TryGet(typeof(Iban), out var converter).Should().BeTrue();
        converter.Should().BeOfType<Iban.ValueJsonConverter>();
        ValueObjectJsonRegistry.Count.Should().BeGreaterThanOrEqualTo(9);
    }

    [Fact]
    public void The_factory_can_also_be_added_to_plain_options()
    {
        var options = new JsonSerializerOptions().AddValueObjects();

        options.Converters.Should().ContainSingle();
        options.AddValueObjects().Converters.Should().ContainSingle("adding it twice must not duplicate it");
        JsonSerializer.Serialize(Amount.Create(1m), options).Should().Be("1.00");
    }
}
