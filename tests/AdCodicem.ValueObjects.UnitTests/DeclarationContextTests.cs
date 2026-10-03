using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Identifiers;
using AdCodicem.ValueObjects.Json;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Testing;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Value objects the generated code reaches through their context: generic, nested in a generic type or an interface,
/// private or protected. Each behaves as any other value object does, on the typed path and on the paths that only know
/// its type.
/// </summary>
public partial class DeclarationContextTests
{
    internal sealed record Shipment(Reference<PurchaseOrder> Order, Catalog<string>.Stock Stock, IShipping.Carrier Carrier);

    [JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
    [JsonSerializable(typeof(Shipment))]
    internal sealed partial class ShipmentContext : JsonSerializerContext;

    private static Shipment Sample => new(
        Reference<PurchaseOrder>.Create("PO-1042"),
        Catalog<string>.Stock.Create(12),
        IShipping.Carrier.Create("UPS"));

    /// <summary>
    /// Each construction of a generic value object is a type of its own, generated once for all of them.
    /// </summary>
    [Fact]
    public void Each_construction_of_a_generic_value_object_is_a_value_object_of_its_own()
    {
        var order = Reference<PurchaseOrder>.Create(" po-1042 ");
        var invoice = Reference<SalesInvoice>.Parse("po-1042", CultureInfo.InvariantCulture);

        order.Value.Should().Be("PO-1042");
        invoice.Value.Should().Be(order.Value);
        typeof(Reference<PurchaseOrder>).Should().NotBe<Reference<SalesInvoice>>();
        Reference<SalesInvoice>.TryCreate("PO-1042-TOO-LONG", out _, out var validation).Should().BeFalse();
        validation.ErrorCode.Should().Be(ValueObjectErrorCodes.TooLong);

        (Catalog<string>.Stock.Create(5) + Catalog<string>.Stock.Create(7)).Should().Be(Catalog<string>.Stock.Create(12));
        var negative = () => Catalog<int>.Stock.Create(-1);
        negative.Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// The serializer resolving types by reflection reads the <c>[JsonConverter]</c> of the type, which names the
    /// factory closing the generated converter over the construction.
    /// </summary>
    [Fact]
    public void A_construction_serializes_as_its_bare_underlying_value_through_its_attribute()
    {
        var json = JsonSerializer.Serialize(Sample);

        json.Should().Be("""{"Order":"PO-1042","Stock":12,"Carrier":"UPS"}""");
        JsonSerializer.Deserialize<Shipment>("""{"Order":"po-1042","Stock":12,"Carrier":"UPS"}""").Should().Be(Sample);

        var refused = () => JsonSerializer.Deserialize<Shipment>("""{"Order":"po-1042","Stock":-1,"Carrier":"UPS"}""");
        refused.Should().Throw<JsonException>().WithMessage("*not a valid Stock*");
    }

    /// <summary>
    /// A source-generated context never sees the attribute, and reaches the generated converter of the construction
    /// through the factory and the registry instead.
    /// </summary>
    [Fact]
    public void A_construction_serializes_through_a_serializer_context()
    {
        var json = JsonSerializer.Serialize(Sample, ShipmentContext.Default.Shipment);

        json.Should().Be("""{"Order":"PO-1042","Stock":12,"Carrier":"UPS"}""");
        JsonSerializer.Deserialize(json, ShipmentContext.Default.Shipment).Should().Be(Sample);
    }

    /// <summary>
    /// The generic definition is registered, and each construction is described once asked for, with the schema and the
    /// converter the generator wrote on it.
    /// </summary>
    [Fact]
    public void The_registry_describes_a_construction_from_its_registered_definition()
    {
        ValueObjectRegistry.GetRegisteredGenericDefinitions().Should().Contain([typeof(Reference<>), typeof(Catalog<>.Stock)]);

        ValueObjectRegistry.TryResolve(typeof(Reference<SalesInvoice>?), out var descriptor).Should().BeTrue();

        descriptor!.ValueObjectType.Should().Be<Reference<SalesInvoice>>();
        descriptor.ValueType.Should().Be<string>();
        descriptor.Schema.Should().BeSameAs(Reference<SalesInvoice>.Schema);
        descriptor.JsonConverter.Should().BeOfType<Reference<SalesInvoice>.ValueJsonConverter>();
        descriptor.TryParse("si-9", CultureInfo.InvariantCulture, out var parsed, out _).Should().BeTrue();
        parsed.Should().Be(Reference<SalesInvoice>.Create("SI-9"));
        ValueObjectRegistry.GetRegistered().Should().Contain(descriptor);
    }

    /// <summary>
    /// A generic value object written by hand registers its definition by hand. Its constructions carry no generated
    /// schema or converter, so each is described from its annotations, as any hand-written value object is.
    /// </summary>
    [Fact]
    public void A_hand_written_generic_definition_is_described_from_its_annotations()
    {
        ValueObjectRegistry.RegisterGenericDefinition(typeof(HandWrittenTag<>));

        ValueObjectRegistry.TryResolve(typeof(HandWrittenTag<PurchaseOrder>), out var descriptor).Should().BeTrue();

        descriptor!.Schema.Should().BeSameAs(ValueObjectSchema.Unconstrained);
        descriptor.JsonConverter.Should().BeNull();
        descriptor.Create("urgent").Should().Be(HandWrittenTag<PurchaseOrder>.Create("urgent"));
    }

    [Fact]
    public void Only_the_definition_of_a_generic_value_object_registers_as_one()
    {
        var closed = () => ValueObjectRegistry.RegisterGenericDefinition(typeof(Reference<PurchaseOrder>));
        var foreign = () => ValueObjectRegistry.RegisterGenericDefinition(typeof(List<>));

        closed.Should().Throw<ArgumentException>().WithParameterName("definition").WithMessage("*Reference*");
        foreign.Should().Throw<ArgumentException>().WithParameterName("definition");
    }

    /// <summary>
    /// The factory claims a construction of a generic value object, and nothing else. One the registry cannot close,
    /// a hand-written generic value object never registered, has no converter to hand out.
    /// </summary>
    [Fact]
    public void The_generic_converter_factory_claims_a_construction_and_hands_out_its_generated_converter()
    {
        var factory = new GenericValueObjectJsonConverterFactory();

        factory.CanConvert(typeof(Reference<PurchaseOrder>)).Should().BeTrue();
        factory.CanConvert(typeof(Reference<PurchaseOrder>?)).Should().BeFalse("the serializer wraps the converter itself");
        factory.CanConvert(typeof(Iban)).Should().BeFalse();
        factory.CanConvert(typeof(List<int>)).Should().BeFalse();
        factory.CreateConverter(typeof(Catalog<int>.Stock), JsonSerializerOptions.Default)
            .Should().BeOfType<Catalog<int>.Stock.ValueJsonConverter>();

        var unknown = () => factory.CreateConverter(typeof(List<int>), JsonSerializerOptions.Default);
        unknown.Should().Throw<NotSupportedException>().WithMessage("*RegisterGenericDefinition*");
    }

    /// <summary>
    /// The value object factory of the JSON package finds a construction nothing has asked for yet in neither registry:
    /// it resolves it, and hands out the generated converter the construction's descriptor carries, which serializes it
    /// as its bare underlying value.
    /// </summary>
    [Fact]
    public void The_value_object_converter_factory_hands_out_the_converter_of_a_construction_resolved_on_demand()
    {
        ValueObjectRegistry.TryGet(typeof(Reference<Requisition>), out _).Should().BeFalse("nothing has asked for it yet");

        var converter = new ValueObjectJsonConverterFactory().CreateConverter(typeof(Reference<Requisition>), JsonSerializerOptions.Default);

        converter.Should().BeOfType<Reference<Requisition>.ValueJsonConverter>();
        ValueObjectRegistry.TryGet(typeof(Reference<Requisition>), out var descriptor).Should().BeTrue();
        descriptor!.JsonConverter.Should().BeSameAs(converter);
        JsonSerializer.Serialize(Reference<Requisition>.Create("rq-7"), new JsonSerializerOptions().AddValueObjects()).Should().Be("\"RQ-7\"");
    }

    /// <summary>The owner of references no other test makes, so that nothing has resolved them yet.</summary>
    private sealed class Requisition;

    /// <summary>
    /// <see cref="TypeDescriptor"/> hands the construction to the converter the attribute names, which converts through
    /// the construction's descriptor as the converter generated on a value object that is not generic does: text in the
    /// culture it is given, the underlying value, and a refusal carrying the rule.
    /// </summary>
    [Fact]
    public void A_construction_converts_through_its_type_converter()
    {
        var converter = TypeDescriptor.GetConverter(typeof(Catalog<decimal>.Stock));
        var french = CultureInfo.GetCultureInfo("fr-FR");

        converter.Should().BeOfType<GenericValueObjectTypeConverter>();
        converter.CanConvertFrom(typeof(string)).Should().BeTrue();
        converter.CanConvertFrom(typeof(int)).Should().BeTrue();
        converter.CanConvertFrom(typeof(Guid)).Should().BeFalse();
        converter.ConvertFrom(null, CultureInfo.InvariantCulture, "12").Should().Be(Catalog<decimal>.Stock.Create(12));
        converter.ConvertFrom(12).Should().Be(Catalog<decimal>.Stock.Create(12));
        converter.CanConvertTo(typeof(string)).Should().BeTrue();
        converter.CanConvertTo(typeof(int)).Should().BeTrue();
        converter.ConvertTo(null, french, Catalog<decimal>.Stock.Create(1200), typeof(string)).Should().Be("1200");
        converter.ConvertTo(null, CultureInfo.InvariantCulture, Catalog<decimal>.Stock.Create(12), typeof(int)).Should().Be(12);
        converter.ConvertTo(null, CultureInfo.InvariantCulture, 12, typeof(string)).Should().Be("12", "anything else is the base's");

        var refused = () => converter.ConvertFrom(null, CultureInfo.InvariantCulture, "-1");
        var refusal = refused.Should().Throw<ValueObjectException>().WithMessage("'Stock' rejected the supplied text: *").Which;
        refusal.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
        refusal.AttemptedValue.Should().Be("-1", "a value object nobody classified keeps the text it refused");
        var foreign = () => converter.ConvertFrom(Guid.Empty);
        foreign.Should().Throw<NotSupportedException>();
        var notValueObject = () => new GenericValueObjectTypeConverter(typeof(List<int>));
        notValueObject.Should().Throw<ArgumentException>().WithParameterName("type");

        // Without a culture, the invariant one; a type the converter does not write is the base's; and a construction
        // of a generic value object is named as it is declared, without the arity the run time appends.
        converter.ConvertFrom(null, null, "1200").Should().Be(Catalog<decimal>.Stock.Create(1200));
        converter.ConvertTo(null, null, Catalog<decimal>.Stock.Create(1200), typeof(string)).Should().Be("1200");
        converter.CanConvertTo(typeof(Guid)).Should().BeFalse();
        var tooLong = () => TypeDescriptor.GetConverter(typeof(Reference<PurchaseOrder>)).ConvertFrom(null, null, "PO-1042-TOO-LONG");
        tooLong.Should().Throw<ValueObjectException>().WithMessage("'Reference' rejected the supplied text: *");
    }

    /// <summary>
    /// A generic definition has no instance and no descriptor: it is no value object, as every question about one
    /// answers, while each of its constructions is.
    /// </summary>
    [Fact]
    public void A_generic_definition_is_no_value_object()
    {
        ValueObjectRegistry.IsValueObject(typeof(Reference<>)).Should().BeFalse();
        ValueObjectRegistry.GetUnderlyingType(typeof(Catalog<>.Stock)).Should().BeNull();
        ValueObjectRegistry.TryResolve(typeof(Reference<>), out _).Should().BeFalse();
        ValueObjectRegistry.IsValueObject(typeof(Reference<SalesInvoice>)).Should().BeTrue();
    }

    /// <summary>
    /// The registration of a private value object goes through classes of its own, nested in the types around it, so
    /// loading the assembly runs none of the author's static constructors: nothing else touches the archive.
    /// </summary>
    [Fact]
    public void Registering_a_private_value_object_runs_no_static_constructor_around_it()
    {
        ValueObjectRegistry.EnsureAssemblyRegistered(typeof(Archive).Assembly);
        var shelf = typeof(Archive).GetNestedType("Shelf", BindingFlags.NonPublic)!;

        ValueObjectRegistry.TryGet(shelf, out _).Should().BeTrue();
        StaticConstructors.Ran.Should().NotContain(nameof(Archive));
    }

    /// <summary>
    /// A private or protected value object is registered from the type declaring it, an identifier's prefix with it, and
    /// one nested in an interface directly.
    /// </summary>
    [Fact]
    public void A_private_protected_or_interface_nested_value_object_is_registered()
    {
        ValueObjectRegistry.EnsureAssemblyRegistered(typeof(Strongbox).Assembly);

        ValueObjectRegistry.TryGet(Strongbox.SecretType, out var secret).Should().BeTrue();
        secret!.Schema.MinLength.Should().Be(8);
        secret.GetValue(Strongbox.Seal("correct horse")).Should().Be("correct horse");
        ValueObjectRegistry.TryGet(typeof(IShipping.Carrier), out _).Should().BeTrue();

        EntityIdRegistry.TryGetByPrefix("box", out var identifier).Should().BeTrue();
        identifier!.ValueObjectType.Should().Be(Strongbox.IdentifierType);
    }
}

/// <inheritdoc cref="IbanContract" />
public sealed class PurchaseOrderReferenceContract : ValueObjectContract<Reference<PurchaseOrder>, string>
{
    protected override IEnumerable<string> AcceptedValues => ["PO-1042", "PO-1"];

    protected override IEnumerable<string> RejectedValues => ["", "PO-1042-TOO-LONG"];
}

/// <inheritdoc cref="IbanContract" />
public sealed class StockContract : ValueObjectContract<Catalog<string>.Stock, int>
{
    protected override IEnumerable<int> AcceptedValues => [0, 12];

    protected override IEnumerable<int> RejectedValues => [-1];
}

/// <inheritdoc cref="IbanContract" />
public sealed class CarrierContract : ValueObjectContract<IShipping.Carrier, string>
{
    protected override IEnumerable<string> AcceptedValues => ["UPS", "DHL"];

    protected override IEnumerable<string> RejectedValues => ["", "UP", "FEDX"];
}
