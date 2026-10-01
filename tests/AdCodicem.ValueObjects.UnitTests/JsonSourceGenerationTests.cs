using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Covers the one case the <c>[JsonConverter]</c> attribute cannot reach on its own: a serializer context, whose
/// generator runs on the original compilation and therefore never sees the attribute the value object generator
/// emits.
/// </summary>
public partial class JsonSourceGenerationTests
{
    internal sealed record Transfer(Iban Account, Amount Total, CountryCode Country, BirthDate Birth);

    internal sealed record OptionalTransfer(Iban? Account, BirthDate? Birth);

    [JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
    [JsonSerializable(typeof(Transfer))]
    [JsonSerializable(typeof(OptionalTransfer))]
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

    /// <summary>
    /// A converter in the options outranks both the type's own attribute and the serializer's handling of
    /// <see cref="Nullable{T}"/>, so whatever the factory claims, it has to build. Claiming <c>Iban?</c> made it
    /// instantiate its converter over a type that breaks the converter's constraints.
    /// </summary>
    [Fact]
    public void An_optional_value_object_goes_through_the_factory_as_its_bare_value_or_null()
    {
        var options = new JsonSerializerOptions().AddValueObjects();
        var iban = Iban.Create("DE89370400440532013000");

        JsonSerializer.Serialize<Iban?>(iban, options).Should().Be("\"DE89370400440532013000\"");
        JsonSerializer.Deserialize<Iban?>("\"DE89370400440532013000\"", options).Should().Be(iban);
        JsonSerializer.Deserialize<Iban?>("null", options).Should().BeNull();

        var json = JsonSerializer.Serialize(new OptionalTransfer(iban, null), TransferContext.Default.OptionalTransfer);

        json.Should().Be("""{"Account":"DE89370400440532013000","Birth":null}""");
        JsonSerializer.Deserialize(json, TransferContext.Default.OptionalTransfer).Should().Be(new OptionalTransfer(iban, null));
    }

    [Theory]
    [InlineData(typeof(Iban), true)]
    [InlineData(typeof(Iban?), false)]
    [InlineData(typeof(int), false)]
    [InlineData(typeof(string), false)]
    [InlineData(typeof(IValueObject), false)]
    [InlineData(typeof(MarkerOnlyValue), false)]
    [InlineData(typeof(ClassBackedValue), false)]
    [InlineData(typeof(SelflessValue), false)]
    public void The_factory_claims_exactly_the_types_it_can_build_a_converter_for(Type type, bool claimed)
    {
        new ValueObjectJsonConverterFactory().CanConvert(type).Should().Be(claimed);
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
