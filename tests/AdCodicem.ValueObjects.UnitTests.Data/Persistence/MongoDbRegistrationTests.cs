using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.MongoDB;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// <see cref="ValueObjectBson"/> and the provider it registers: which serializer the driver gets for which type, the
/// checks made at start-up, and the per-type registration.
/// </summary>
/// <remarks>
/// The driver's registry is process-wide, and <see cref="MongoDb.EnsureRegistered"/> sets it up once, strict, before any
/// test runs here. A check that needs a registry in another state runs on a <see cref="BsonSerializerRegistry"/> of its
/// own, which the internal methods take as a parameter.
/// </remarks>
public sealed class MongoDbRegistrationTests
{
    public MongoDbRegistrationTests() => MongoDb.EnsureRegistered();

    [Fact]
    public void The_driver_gets_a_strict_serializer_for_a_value_object_and_wraps_it_for_a_nullable_one()
    {
        var serializer = BsonSerializer.LookupSerializer<Iban>().Should().BeOfType<ValueObjectBsonSerializer<Iban, string>>().Subject;
        serializer.Trusted.Should().BeFalse();
        serializer.ValueSerializer.Should().BeSameAs(BsonSerializer.LookupSerializer<string>());

        BsonSerializer.LookupSerializer<Iban?>().Should().BeOfType<NullableSerializer<Iban>>();
    }

    [Fact]
    public void The_provider_answers_value_objects_only()
    {
        var provider = new ValueObjectBsonSerializationProvider(trusted: true);
        var registry = BsonSerializer.SerializerRegistry;

        provider.GetSerializer(typeof(string), registry).Should().BeNull("a class is no value object");
        provider.GetSerializer(typeof(int), registry).Should().BeNull("a struct that is no value object is left to the driver");
        provider.GetSerializer(typeof(Iban?), registry).Should().BeNull("the driver wraps the value object's serializer itself");
        provider.GetSerializer(typeof(SelflessValue), registry).Should().BeNull("a struct without the contract is no value object");

        provider.GetSerializer(typeof(Iban), registry).Should().BeOfType<ValueObjectBsonSerializer<Iban, string>>()
            .Which.Trusted.Should().BeTrue();

        // Without a registry, the driver's.
        provider.GetSerializer(typeof(PageNumber)).Should().BeOfType<ValueObjectBsonSerializer<PageNumber, int>>()
            .Which.ValueSerializer.Should().BeSameAs(BsonSerializer.LookupSerializer<int>());

        FluentActions.Invoking(() => provider.GetSerializer(null!, registry)).Should().Throw<ArgumentNullException>().WithParameterName("type");
        FluentActions.Invoking(() => provider.GetSerializer(typeof(Iban), null!)).Should().Throw<ArgumentNullException>().WithParameterName("serializerRegistry");
    }

    /// <summary>
    /// A value object the registry does not hold yet is described by reflection and served: a construction of a generic
    /// value object written by hand, closed over a type private to this class so that no other test resolves it, and a
    /// construction of a generated one. It is not <see cref="UnregisteredCode"/>: resolving a type registers it for the
    /// process, and nothing may resolve that one.
    /// </summary>
    [Fact]
    public void A_value_object_nothing_registered_is_described_by_reflection_and_served()
    {
        var provider = new ValueObjectBsonSerializationProvider(trusted: false);
        ValueObjectRegistry.TryGet(typeof(HandWrittenTag<Crate>), out _).Should().BeFalse("nothing registered it");

        provider.GetSerializer(typeof(HandWrittenTag<Crate>), BsonSerializer.SerializerRegistry)
            .Should().BeOfType<ValueObjectBsonSerializer<HandWrittenTag<Crate>, string>>();
        provider.GetSerializer(typeof(Reference<SalesInvoice>), BsonSerializer.SerializerRegistry)
            .Should().BeOfType<ValueObjectBsonSerializer<Reference<SalesInvoice>, string>>();
    }

    [Fact]
    public void A_value_object_over_a_128_bit_integer_is_refused_unless_the_application_registered_a_serializer_for_it()
    {
        BsonSerializer.LookupSerializer<LedgerBalance>().Should().BeOfType<UnrepresentableValueObjectSerializer<LedgerBalance>>();
        BsonSerializer.LookupSerializer<Fingerprint>().Should().BeOfType<UnrepresentableValueObjectSerializer<Fingerprint>>();

        // The driver writes a bare Int128 as an empty document, which is what the value object would lose its value to.
        MongoDb.Write(BsonSerializer.LookupSerializer<Int128>(), Int128.One).Should().Be(new BsonDocument());

        // The provider takes the serializer of the underlying type from the registry the driver hands it.
        var registry = new BsonSerializerRegistry();
        var int128 = new Int128AsText();
        registry.RegisterSerializer(typeof(Int128), int128);
        var serializer = new ValueObjectBsonSerializationProvider(trusted: false).GetSerializer(typeof(LedgerBalance), registry)
            .Should().BeOfType<ValueObjectBsonSerializer<LedgerBalance, Int128>>().Subject;
        serializer.ValueSerializer.Should().BeSameAs(int128);

        MongoDb.Write(serializer, LedgerBalance.Create(-5)).Should().Be(new BsonString("-5"));
        MongoDb.Read(serializer, "42").Should().Be(LedgerBalance.Create(42));
    }

    [Fact]
    public void A_registry_whose_serializer_of_Guid_has_no_representation_is_refused_with_the_remedy()
    {
        var registry = new BsonSerializerRegistry();
        registry.RegisterSerializer(typeof(Guid), new GuidSerializer());

        FluentActions.Invoking(() => ValueObjectBson.EnsureGuidRepresentation(registry))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("A value object over Guid is registered, and the serializer of Guid has no representation "
                + "(GuidRepresentation.Unspecified), so it would throw on the first write. Call "
                + "BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard)) before this method.");
    }

    public static TheoryData<string, IBsonSerializer<Guid>> Representations => new()
    {
        { "Standard", new GuidSerializer(GuidRepresentation.Standard) },
        { "C# legacy", new GuidSerializer(GuidRepresentation.CSharpLegacy) },
        { "as text", new GuidSerializer(BsonType.String) },
        { "the application's own", new GuidAsText() },
    };

    /// <summary>
    /// Any serializer of <see cref="Guid"/> that writes passes: only the driver's own with no representation is refused.
    /// </summary>
    /// <param name="case">The serializer, described.</param>
    /// <param name="serializer">The serializer of <see cref="Guid"/>.</param>
    [Theory]
    [MemberData(nameof(Representations))]
    public void A_serializer_of_Guid_that_writes_passes(string @case, IBsonSerializer<Guid> serializer)
    {
        var registry = new BsonSerializerRegistry();
        registry.RegisterSerializer(typeof(Guid), serializer);

        FluentActions.Invoking(() => ValueObjectBson.EnsureGuidRepresentation(registry)).Should().NotThrow(@case);
    }

    [Fact]
    public void The_serializer_of_Guid_is_checked_only_when_a_value_object_is_over_Guid()
    {
        var empty = new BsonSerializerRegistry();
        var withoutRepresentation = new BsonSerializerRegistry();
        withoutRepresentation.RegisterSerializer(typeof(Guid), new GuidSerializer());
        var iban = Descriptor<Iban>();
        var customer = Descriptor<CustomerId>();

        // A registry with no serializer at all would throw if it were asked for one.
        FluentActions.Invoking(() => ValueObjectBson.EnsureGuidRepresentation([iban], empty)).Should().NotThrow();
        FluentActions.Invoking(() => ValueObjectBson.EnsureGuidRepresentation([iban, customer], withoutRepresentation))
            .Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Every value object the driver mapped counts, whether the registry holds it, holds only its generic definition, or
    /// has never heard of it; the other types the driver mapped, the application's documents first, do not.
    /// </summary>
    [Fact]
    public void A_value_object_the_driver_already_mapped_through_a_class_map_fails_the_registration_by_name()
    {
        Type[] mapped =
        [
            typeof(PageNumber),
            typeof(MongoDbRegistrationTests),
            typeof(Reference<PurchaseOrder>),
            typeof(Iban),
            typeof(Int128),
            typeof(SelflessValue),
            typeof(UnregisteredCode),
            typeof(HandWrittenId<MuteProfile>),
        ];

        FluentActions.Invoking(() => ValueObjectBson.EnsureNotMappedYet(mapped))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("MongoDB.Driver has already mapped HandWrittenId<MuteProfile>, Iban, PageNumber, "
                + "Reference<PurchaseOrder>, UnregisteredCode through a class map, which writes a value object as {} and reads "
                + "it back as its default, and keeps that serializer for the rest of the process. Call ValueObjectBson.Register "
                + "at start-up, before anything is serialized or any class map is built.");
        FluentActions.Invoking(() => ValueObjectBson.EnsureNotMappedYet([typeof(MongoDbRegistrationTests), typeof(Int128), typeof(SelflessValue)]))
            .Should().NotThrow();

        // The suite registered before anything was serialized, so the driver mapped no value object.
        FluentActions.Invoking(() => ValueObjectBson.EnsureNotMappedYet(BsonClassMap.GetRegisteredClassMaps().Select(static map => map.ClassType)))
            .Should().NotThrow();
    }

    [Fact]
    public void Registering_again_with_the_same_trust_changes_nothing_and_with_the_other_trust_throws()
    {
        FluentActions.Invoking(() => ValueObjectBson.Register(typeof(Iban).Assembly)).Should().NotThrow();
        FluentActions.Invoking(() => ValueObjectBson.Register(trusted: false)).Should().NotThrow();
        BsonSerializer.LookupSerializer<Iban>().Should().BeOfType<ValueObjectBsonSerializer<Iban, string>>().Which.Trusted.Should().BeFalse();

        FluentActions.Invoking(() => ValueObjectBson.Register(trusted: true, typeof(Iban).Assembly))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("The serializers of value objects are already registered with trusted: false, and cannot be "
                + "registered again with trusted: true. Give a value object the other trust with "
                + "ValueObjectBson.Register<TSelf, TValue>(trusted), before the driver first asks for it.");

        FluentActions.Invoking(() => ValueObjectBson.Register(null!)).Should().Throw<ArgumentNullException>().WithParameterName("assemblies");
    }

    [Fact]
    public void One_value_object_is_registered_with_the_serializer_the_provider_would_give_it()
    {
        // The provider already served Iban, strict: the same serializer registered again changes nothing.
        BsonSerializer.LookupSerializer<Iban>();
        FluentActions.Invoking(() => ValueObjectBson.Register<Iban, string>()).Should().NotThrow();
        FluentActions.Invoking(() => ValueObjectBson.Register<Iban, string>(trusted: true))
            .Should().Throw<BsonSerializationException>().WithMessage("There is already a different serializer registered for type Iban.");

        // A construction nothing asked for yet, with the other trust; and one over Guid, whose representation is set.
        ValueObjectBson.Register<Reference<MongoDbRegistrationTests>, string>(trusted: true);
        BsonSerializer.LookupSerializer<Reference<MongoDbRegistrationTests>>()
            .Should().BeOfType<ValueObjectBsonSerializer<Reference<MongoDbRegistrationTests>, string>>().Which.Trusted.Should().BeTrue();
        FluentActions.Invoking(() => ValueObjectBson.Register<CustomerId, Guid>()).Should().NotThrow();
    }

    [Fact]
    public void One_value_object_over_a_128_bit_integer_is_refused_with_the_remedy()
    {
        FluentActions.Invoking(() => ValueObjectBson.Register<LedgerBalance, Int128>())
            .Should().Throw<NotSupportedException>()
            .WithMessage($"'{typeof(LedgerBalance)}' is a value object over Int128, which MongoDB has no representation for. "
                + "Register a serializer of the application's own for Int128, with BsonSerializer.RegisterSerializer, before "
                + "this method.");
    }

    [Fact]
    public void The_serializer_that_refuses_a_value_object_names_it_and_the_remedy_both_ways()
    {
        var serializer = new UnrepresentableValueObjectSerializer<Fingerprint>(typeof(UInt128));
        const string Message = "Fingerprint is a value object over UInt128, which MongoDB has no representation for. Register a "
            + "serializer of the application's own for UInt128, with BsonSerializer.RegisterSerializer, before anything is "
            + "serialized: the value object then writes what that serializer writes.";

        FluentActions.Invoking(() => MongoDb.Write(serializer, Fingerprint.Create(1))).Should().Throw<BsonSerializationException>().WithMessage(Message);
        FluentActions.Invoking(() => MongoDb.Read(serializer, "1")).Should().Throw<BsonSerializationException>().WithMessage(Message);
    }

    private static ValueObjectDescriptor Descriptor<T>()
        => ValueObjectRegistry.TryGet(typeof(T), out var descriptor) ? descriptor : throw new InvalidOperationException($"{typeof(T)} is not registered.");

    /// <summary>What the tag of the reflection fallback's test belongs to, private so that no other test closes it.</summary>
    private sealed class Crate;

    /// <summary>
    /// A serializer of <see cref="Int128"/> an application registers, writing the number as text.
    /// </summary>
    private sealed class Int128AsText : SerializerBase<Int128>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Int128 value)
            => context.Writer.WriteString(value.ToString(null, System.Globalization.CultureInfo.InvariantCulture));

        public override Int128 Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            => Int128.Parse(context.Reader.ReadString(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A serializer of <see cref="Guid"/> an application registers, writing it as text.
    /// </summary>
    private sealed class GuidAsText : SerializerBase<Guid>
    {
        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Guid value)
            => context.Writer.WriteString(value.ToString());
    }
}
