using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Fixtures.WithoutJson;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.Metadata;
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

    internal sealed record Grant(Allowance Amount, Sku Item);

    [JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
    [JsonSerializable(typeof(Transfer))]
    [JsonSerializable(typeof(OptionalTransfer))]
    [JsonSerializable(typeof(Grant))]
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

    /// <summary>
    /// The converter travels with the descriptor, created the first time it is asked for and the same from then on. An
    /// assembly referencing the JSON package, as this one does, also publishes it to the package's own registry, which an
    /// older package reads alone, and which comes first.
    /// </summary>
    [Fact]
    public void The_generator_registers_a_converter_for_every_value_object_of_the_assembly()
    {
        ValueObjectRegistry.TryGet(typeof(Iban), out var descriptor).Should().BeTrue();
        descriptor!.JsonConverter.Should().BeOfType<Iban.ValueJsonConverter>()
            .And.BeSameAs(descriptor.JsonConverter, "the descriptor creates its converter once");

        ValueObjectJsonRegistry.TryGet(typeof(Iban), out var converter).Should().BeTrue();
        converter.Should().BeOfType<Iban.ValueJsonConverter>()
            .And.NotBeSameAs(descriptor.JsonConverter, "the package's registry answers from its own first");
        ValueObjectJsonRegistry.Count.Should().BeGreaterThan(50, "every value object of this assembly is published there");
        ValueObjectJsonRegistry.TryGet(typeof(Iban?), out _).Should().BeFalse("the serializer wraps the converter of Iban");
    }

    /// <summary>
    /// A converter registered by hand travels with the descriptor as one the registration creates does, the same
    /// instance from then on.
    /// </summary>
    [Fact]
    public void A_converter_registered_by_hand_travels_with_the_descriptor()
    {
        var converter = new Ledger.ValueJsonConverter();

        ValueObjectRegistry.Register<Ledger, string>(Ledger.Schema, converter);

        ValueObjectRegistry.TryGet(typeof(Ledger), out var descriptor).Should().BeTrue();
        descriptor!.JsonConverter.Should().BeSameAs(converter).And.BeSameAs(descriptor.JsonConverter);
        var missing = () => ValueObjectRegistry.Register<Ledger, string>(Ledger.Schema, (JsonConverter<Ledger>)null!);
        missing.Should().Throw<ArgumentNullException>();
        var missingFactory = () => ValueObjectRegistry.Register<Ledger, string>(Ledger.Schema, (Func<JsonConverter<Ledger>>)null!);
        missingFactory.Should().Throw<ArgumentNullException>();
        var missingInRegistry = () => ValueObjectJsonRegistry.Register<Ledger>(null!);
        missingInRegistry.Should().Throw<ArgumentNullException>();
    }

    /// <summary>A ledger's code, which one test registers by hand.</summary>
    [ValueObject<string>]
    public readonly partial struct Ledger;

    /// <summary>
    /// A domain assembly may reference the contracts and the generator alone, while the API project declares the
    /// serializer context and names the factory. The converters of its value objects reach the context through their
    /// descriptors, which the generator always registers, rather than through a converter built by reflection, which
    /// trimming and native AOT do not support and which would write a 128-bit integer as a JSON number.
    /// </summary>
    [Fact]
    public void A_serializer_context_uses_the_converters_of_an_assembly_that_does_not_reference_the_json_package()
    {
        var grant = new Grant(Allowance.Create(Int128.Parse("123456789012345678901234567890", CultureInfo.InvariantCulture)), Sku.Create(" ab-1 "));

        var json = JsonSerializer.Serialize(grant, TransferContext.Default.Grant);

        json.Should().Be("""{"Amount":"123456789012345678901234567890","Item":"AB-1"}""");
        JsonSerializer.Deserialize(json, TransferContext.Default.Grant).Should().Be(grant);
        TransferContext.Default.Options.GetConverter(typeof(Allowance)).Should().BeOfType<Allowance.ValueJsonConverter>();
        ValueObjectJsonRegistry.TryGet(typeof(Sku), out var converter).Should().BeTrue();
        converter.Should().BeOfType<Sku.ValueJsonConverter>();
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
    [InlineData(typeof(IEntityId), false)]
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
