using AdCodicem.ValueObjects.MongoDB;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Bson.Serialization.Serializers;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// <see cref="ValueObjectBsonSerializer{TSelf, TValue}"/>: a value object stored as the bare value the serializer of its
/// underlying type writes, read back through its rules, and refused on write when the type rejects it.
/// </summary>
/// <remarks>
/// The serializers here are built by hand over the driver's own serializers, and driven through a document writer and
/// reader, which is what the driver does with them; the registry and the provider are
/// <see cref="MongoDbRegistrationTests"/>'s, and a real server is the integration suite's.
/// </remarks>
public sealed class MongoDbSerializerTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    private static readonly Guid Customer = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

    public MongoDbSerializerTests() => MongoDb.EnsureRegistered();

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, is stored as the bare value it carries is,
    /// and reads back what that value reads back.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_is_stored_as_the_value_it_carries(string type)
        => Samples.All[type].RoundTripsThroughBsonAsItsUnderlyingValue();

    [Fact]
    public void A_value_object_beyond_the_range_its_primitive_is_stored_in_fails_as_the_primitive_does()
    {
        // The driver writes a uint as an Int32 and a ulong as an Int64, and throws on one beyond their range: the value
        // object writes what the primitive writes, so it throws the same.
        FluentActions.Invoking(() => MongoDb.Write(BsonSerializer.LookupSerializer<uint>(), uint.MaxValue))
            .Should().Throw<OverflowException>();
        FluentActions.Invoking(() => MongoDb.Write(BsonSerializer.LookupSerializer<SequenceNumber>(), SequenceNumber.Create(uint.MaxValue)))
            .Should().Throw<OverflowException>();
        FluentActions.Invoking(() => MongoDb.Write(BsonSerializer.LookupSerializer<ulong>(), ulong.MaxValue))
            .Should().Throw<OverflowException>();
        FluentActions.Invoking(() => MongoDb.Write(BsonSerializer.LookupSerializer<ByteCount>(), ByteCount.Create(ulong.MaxValue)))
            .Should().Throw<OverflowException>();
    }

    [Fact]
    public void A_value_read_goes_through_the_rules_and_is_normalized()
    {
        var serializer = new ValueObjectBsonSerializer<Iban, string>(new StringSerializer());

        MongoDb.Read(serializer, "fr76 3000 6000 0112 3456 7890 189").Should().Be(Iban.Create(ValidIban));
    }

    [Fact]
    public void A_value_the_type_refuses_is_refused_on_read_with_its_rule_and_never_its_value()
    {
        var serializer = new ValueObjectBsonSerializer<Iban, string>(new StringSerializer());
        Iban.TryCreate("FR76", out _, out var validation).Should().BeFalse();

        var refusal = FluentActions.Invoking(() => MongoDb.Read(serializer, "FR76"))
            .Should().Throw<FormatException>().Which;

        refusal.Message.Should().Be($"The value read is not a valid Iban: {validation.ErrorMessage}").And.NotContain("FR76");
        refusal.InnerException.Should().BeNull();
        Code(refusal).Should().Be(ValueObjectErrorCodes.TooShort);
    }

    [Fact]
    public void Each_rule_refuses_a_value_with_its_own_code()
    {
        Code(Refused(new ValueObjectBsonSerializer<PageNumber, int>(new Int32Serializer()), 0))
            .Should().Be(ValueObjectErrorCodes.OutOfRange);
        Code(Refused(new ValueObjectBsonSerializer<CountryCode, string>(new StringSerializer()), "ZZ"))
            .Should().Be(ValueObjectErrorCodes.NotAKnownValue);
        Code(Refused(new ValueObjectBsonSerializer<CustomerId, Guid>(new GuidSerializer(GuidRepresentation.Standard)), new BsonBinaryData(Guid.Empty, GuidRepresentation.Standard)))
            .Should().Be(ValueObjectErrorCodes.Required);
    }

    [Fact]
    public void A_trusted_serializer_reads_a_value_the_type_refuses_as_it_is_and_writes_it_back_unchanged()
    {
        var serializer = new ValueObjectBsonSerializer<Iban, string>(new StringSerializer(), trusted: true);

        var read = MongoDb.Read(serializer, "not an iban");

        read.Value.Should().Be("not an iban");

        // Only an instance equal to the default is validated on write: one read on trust went through
        // CreateUnchecked, as one read by a trusted EF Core or Dapper mapping does, and goes back as it came.
        MongoDb.Write(serializer, read).Should().Be(new BsonString("not an iban"));
        MongoDb.Write(new ValueObjectBsonSerializer<Iban, string>(new StringSerializer()), read).Should().Be(new BsonString("not an iban"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_BSON_null_is_refused_as_required_whatever_the_trust(bool trusted)
    {
        var serializer = new ValueObjectBsonSerializer<Iban, string>(new StringSerializer(), trusted);
        using var reader = new BsonDocumentReader(new BsonDocument { { "v", BsonNull.Value }, { "next", 1 } });
        reader.ReadStartDocument();
        reader.ReadName("v");

        var refusal = FluentActions.Invoking(() => serializer.Deserialize(BsonDeserializationContext.CreateRoot(reader)))
            .Should().Throw<FormatException>().Which;

        refusal.Message.Should().Be("A BSON null cannot be read as Iban; declare the member as a nullable Iban? instead.");
        Code(refusal).Should().Be(ValueObjectErrorCodes.Required);

        // The null was consumed: the reader stands on the next element.
        reader.ReadName().Should().Be("next");
        reader.ReadInt32().Should().Be(1);
    }

    [Fact]
    public void A_nullable_value_object_reads_and_writes_a_BSON_null_through_the_driver()
    {
        var serializer = BsonSerializer.LookupSerializer<Iban?>();

        MongoDb.Write(serializer, null).Should().Be(BsonNull.Value);
        MongoDb.Read(serializer, BsonNull.Value).Should().BeNull();
        MongoDb.Read(serializer, ValidIban).Should().Be(Iban.Create(ValidIban));
    }

    /// <summary>
    /// The largest date JavaScript represents, 8.64e15 milliseconds after the epoch, which another writer can store and
    /// no .NET date or time reaches.
    /// </summary>
    private const long JavaScriptMaxDate = 8_640_000_000_000_000;

    public static TheoryData<string, IBsonSerializer, BsonValue, string> Unreadable => new()
    {
        { "text into a value object over text", new ValueObjectBsonSerializer<Iban, string>(new StringSerializer()), new BsonInt32(42), "The BSON Int32 read cannot be read as Iban, a value object over String." },
        { "text the number serializer does not parse", new ValueObjectBsonSerializer<PageNumber, int>(new Int32Serializer()), new BsonString("abc"), "The BSON String read cannot be read as PageNumber, a value object over Int32." },
        { "a long beyond the range of an int", new ValueObjectBsonSerializer<PageNumber, int>(new Int32Serializer()), new BsonInt64(9_000_000_000), "The BSON Int64 read cannot be read as PageNumber, a value object over Int32." },
        { "a double with a fraction into an int", new ValueObjectBsonSerializer<PageNumber, int>(new Int32Serializer()), new BsonDouble(2.5), "The BSON Double read cannot be read as PageNumber, a value object over Int32." },
        { "text that is no Guid", new ValueObjectBsonSerializer<CustomerId, Guid>(new GuidSerializer(GuidRepresentation.Standard)), new BsonString("not-a-guid"), "The BSON String read cannot be read as CustomerId, a value object over Guid." },
        { "a date beyond DateTime", new ValueObjectBsonSerializer<RecordedAt, DateTime>(new DateTimeSerializer()), new BsonDateTime(JavaScriptMaxDate), "The BSON DateTime read cannot be read as RecordedAt, a value object over DateTime." },
        { "a date beyond DateOnly", new ValueObjectBsonSerializer<EffectiveDate, DateOnly>(new DateOnlySerializer()), new BsonDateTime(JavaScriptMaxDate), "The BSON DateTime read cannot be read as EffectiveDate, a value object over DateOnly." },
        { "a date beyond DateTimeOffset", new ValueObjectBsonSerializer<OccurredAt, DateTimeOffset>(new DateTimeOffsetSerializer()), new BsonDateTime(JavaScriptMaxDate), "The BSON DateTime read cannot be read as OccurredAt, a value object over DateTimeOffset." },
        { "ticks before midnight", new ValueObjectBsonSerializer<OpeningTime, TimeOnly>(new TimeOnlySerializer()), new BsonInt64(-1), "The BSON Int64 read cannot be read as OpeningTime, a value object over TimeOnly." },
    };

    /// <summary>
    /// A value the serializer of the underlying type cannot read is refused as not parsable, without the driver's own
    /// exception, whose message may quote the value: "The input string 'abc' was not in a correct format.", or "The
    /// value 8640000000000000 for the BsonDateTime MillisecondsSinceEpoch is outside the range...".
    /// </summary>
    /// <param name="case">What is read.</param>
    /// <param name="serializer">The serializer of the value object.</param>
    /// <param name="value">The BSON value.</param>
    /// <param name="message">The message of the refusal.</param>
    [Theory]
    [MemberData(nameof(Unreadable))]
    public void A_value_its_primitive_cannot_read_is_refused_as_not_parsable_without_quoting_it(
        string @case,
        IBsonSerializer serializer,
        BsonValue value,
        string message)
    {
        var refusal = FluentActions.Invoking(() => ReadBoxed(serializer, value)).Should().Throw<Exception>().Which;

        refusal.Should().BeOfType<FormatException>(@case);
        refusal.Message.Should().Be(message);
        refusal.InnerException.Should().BeNull("the driver's message may quote the value");
        Code(refusal).Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    [Fact]
    public void An_exception_of_the_serializer_of_the_underlying_value_that_is_no_refusal_propagates_as_it_is()
    {
        var serializer = new ValueObjectBsonSerializer<Iban, string>(new MisconfiguredStringSerializer());

        FluentActions.Invoking(() => MongoDb.Read(serializer, ValidIban))
            .Should().Throw<BsonSerializationException>().WithMessage("Misconfigured.");

        // The driver's own serializer of TimeOnly, given units it does not know, throws an ArgumentException on every
        // read; only the ArgumentOutOfRangeException of a value beyond the type's range is a refusal.
        var opening = new ValueObjectBsonSerializer<OpeningTime, TimeOnly>(new TimeOnlySerializer(BsonType.Int64, (TimeOnlyUnits)99));
        FluentActions.Invoking(() => MongoDb.Read(opening, new BsonInt64(1)))
            .Should().Throw<ArgumentException>().Which.Should().NotBeOfType<ArgumentOutOfRangeException>();
    }

#pragma warning disable VO0010 // The uninitialized instances are what the serializer refuses, or writes when the type accepts them.
    [Fact]
    public void A_default_instance_the_type_refuses_is_refused_on_write_with_its_rule()
    {
        var page = FluentActions.Invoking(() => MongoDb.Write(new ValueObjectBsonSerializer<PageNumber, int>(new Int32Serializer()), default))
            .Should().Throw<BsonSerializationException>().Which;
        page.Message.Should().StartWith("The value to write is not a valid PageNumber: ");
        Code(page).Should().Be(ValueObjectErrorCodes.OutOfRange);

        var iban = FluentActions.Invoking(() => MongoDb.Write(new ValueObjectBsonSerializer<Iban, string>(new StringSerializer()), default))
            .Should().Throw<BsonSerializationException>().Which;
        Code(iban).Should().Be(ValueObjectErrorCodes.Required);

        var customer = FluentActions.Invoking(() => MongoDb.Write(new ValueObjectBsonSerializer<CustomerId, Guid>(new GuidSerializer(GuidRepresentation.Standard)), default))
            .Should().Throw<BsonSerializationException>().Which;
        Code(customer).Should().Be(ValueObjectErrorCodes.Required);
    }

    [Fact]
    public void A_default_instance_the_type_accepts_is_written()
    {
        MongoDb.Write(new ValueObjectBsonSerializer<SequenceNumber, uint>(new UInt32Serializer()), default).Should().Be(new BsonInt32(0));
        MongoDb.Write(new ValueObjectBsonSerializer<Label, string>(new StringSerializer()), default).Should().Be(new BsonString(string.Empty));
    }

    [Fact]
    public void A_nullable_value_object_holding_a_default_instance_the_type_refuses_is_refused_and_one_holding_nothing_is_a_BSON_null()
    {
        var serializer = BsonSerializer.LookupSerializer<Iban?>();

        var refusal = FluentActions.Invoking(() => MongoDb.Write<Iban?>(serializer, default(Iban)))
            .Should().Throw<BsonSerializationException>().Which;
        Code(refusal).Should().Be(ValueObjectErrorCodes.Required);

        MongoDb.Write(serializer, null).Should().Be(BsonNull.Value);
    }
#pragma warning restore VO0010

    [Fact]
    public void The_serializer_takes_its_representation_from_the_serializer_of_the_underlying_value()
    {
        new ValueObjectBsonSerializer<Iban, string>(new StringSerializer()).Representation.Should().Be(BsonType.String);
        new ValueObjectBsonSerializer<PageNumber, int>(new Int32Serializer(BsonType.String)).Representation.Should().Be(BsonType.String);
        new ValueObjectBsonSerializer<PageNumber, int>(new Int32Serializer()).Representation.Should().Be(BsonType.Int32);
        new ValueObjectBsonSerializer<Iban, string>(new MisconfiguredStringSerializer()).Representation
            .Should().Be(BsonType.Undefined, "a serializer that does not say gives no representation");
    }

    [Fact]
    public void LINQ_finds_the_value_of_a_value_object_in_its_own_field()
    {
        var valueSerializer = new StringSerializer();
        var serializer = new ValueObjectBsonSerializer<Iban, string>(valueSerializer);

        serializer.TryGetMemberSerializationInfo("Value", out var info).Should().BeTrue();
        info.ElementPath.Should().BeEmpty();
        info.Serializer.Should().BeSameAs(valueSerializer);
        info.NominalType.Should().Be<string>();

        serializer.TryGetMemberSerializationInfo("CountryCode", out var other).Should().BeFalse();
        other.Should().BeNull();
    }

    [Fact]
    public void An_option_on_the_member_reaches_the_serializer_of_the_underlying_value_and_keeps_the_trust()
    {
        var valueSerializer = new Int32Serializer();
        IChildSerializerConfigurable serializer = new ValueObjectBsonSerializer<PageNumber, int>(valueSerializer, trusted: true);

        serializer.ChildSerializer.Should().BeSameAs(valueSerializer);

        var reconfigured = serializer.WithChildSerializer(new Int32Serializer(BsonType.String))
            .Should().BeOfType<ValueObjectBsonSerializer<PageNumber, int>>().Subject;
        reconfigured.Trusted.Should().BeTrue();
        reconfigured.ValueSerializer.Should().Be(new Int32Serializer(BsonType.String));
        MongoDb.Write(reconfigured, PageNumber.Create(7)).Should().Be(new BsonString("7"));
    }

    [Fact]
    public void Two_serializers_are_equal_for_the_same_trust_over_equal_serializers_of_the_underlying_value()
    {
        var strict = new ValueObjectBsonSerializer<Iban, string>(new StringSerializer());
        var same = new ValueObjectBsonSerializer<Iban, string>(new StringSerializer());

        strict.Equals(same).Should().BeTrue();
        strict.GetHashCode().Should().Be(same.GetHashCode());
        strict.Equals(new ValueObjectBsonSerializer<Iban, string>(new StringSerializer(), trusted: true)).Should().BeFalse();
        strict.Equals(new ValueObjectBsonSerializer<Iban, string>(new StringSerializer(BsonType.Symbol))).Should().BeFalse();
        strict.Equals(new StringSerializer()).Should().BeFalse();
        strict.Equals(strict).Should().BeTrue();
        strict.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void The_constructor_and_the_two_directions_refuse_what_they_cannot_use()
    {
        FluentActions.Invoking(() => new ValueObjectBsonSerializer<Iban, string>(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("valueSerializer");

        var serializer = new ValueObjectBsonSerializer<Iban, string>(new StringSerializer());
        FluentActions.Invoking(() => serializer.Serialize(null!, default, Iban.Create(ValidIban)))
            .Should().Throw<ArgumentNullException>().WithParameterName("context");
        FluentActions.Invoking(() => serializer.Deserialize(null!, default))
            .Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public void A_value_object_over_Guid_refuses_a_serializer_of_Guid_with_no_representation_on_write_and_on_a_binary_read()
    {
        var serializer = new ValueObjectBsonSerializer<CustomerId, Guid>(new GuidSerializer());
        const string Remedy = "CustomerId is a value object over Guid, and the serializer of Guid it goes through has no "
            + "representation (GuidRepresentation.Unspecified). Call BsonSerializer.RegisterSerializer(new "
            + "GuidSerializer(GuidRepresentation.Standard)) at start-up, before anything is serialized, or give the member a "
            + "representation with [BsonGuidRepresentation].";

        FluentActions.Invoking(() => MongoDb.Write(serializer, CustomerId.Create(Customer)))
            .Should().Throw<BsonSerializationException>().WithMessage(Remedy);
        FluentActions.Invoking(() => MongoDb.Read(serializer, new BsonBinaryData(Customer, GuidRepresentation.Standard)))
            .Should().Throw<BsonSerializationException>().WithMessage(Remedy);

        // Text needs no representation to be read.
        MongoDb.Read(serializer, Customer.ToString()).Should().Be(CustomerId.Create(Customer));
    }

    [Fact]
    public void A_value_object_over_Guid_writes_through_any_representation_but_the_unspecified_one()
    {
        var customer = CustomerId.Create(Customer);

        MongoDb.Write(new ValueObjectBsonSerializer<CustomerId, Guid>(new GuidSerializer(GuidRepresentation.CSharpLegacy)), customer)
            .Should().Be(new BsonBinaryData(Customer, GuidRepresentation.CSharpLegacy));
        MongoDb.Write(new ValueObjectBsonSerializer<CustomerId, Guid>(new GuidSerializer(BsonType.String)), customer)
            .Should().Be(new BsonString(Customer.ToString()));
        MongoDb.Write(new ValueObjectBsonSerializer<CustomerId, Guid>(new GuidSerializer(GuidRepresentation.Standard)), customer)
            .Should().Be(new BsonBinaryData(Customer, GuidRepresentation.Standard));
    }

    [Fact]
    public void A_value_object_written_by_hand_is_stored_as_the_value_it_carries()
    {
        var link = HandWrittenLink.Create(new Uri("https://example.com/a"));
        var serializer = BsonSerializer.LookupSerializer<HandWrittenLink>();

        serializer.Should().BeOfType<ValueObjectBsonSerializer<HandWrittenLink, Uri>>();
        MongoDb.Write(serializer, link).Should().Be(MongoDb.Write(BsonSerializer.LookupSerializer<Uri>(), link.Value));
        MongoDb.Read(serializer, "https://example.com/a").Should().Be(link);
        Code(Refused(serializer, "relative/path")).Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    [Fact]
    public void A_value_object_written_by_hand_that_refuses_without_a_rule_is_refused_as_not_parsable()
    {
        var serializer = new ValueObjectBsonSerializer<HandWrittenId<MuteProfile>, string>(new StringSerializer());

        var refusal = Refused(serializer, "mut_anything");

        refusal.Should().BeOfType<FormatException>();
        Code(refusal).Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    [Fact]
    public void A_construction_of_a_generic_value_object_is_stored_as_the_value_it_carries()
    {
        var serializer = BsonSerializer.LookupSerializer<Reference<PurchaseOrder>>();

        serializer.Should().BeOfType<ValueObjectBsonSerializer<Reference<PurchaseOrder>, string>>();
        MongoDb.Write(serializer, Reference<PurchaseOrder>.Create("po-7")).Should().Be(new BsonString("PO-7"));
        MongoDb.Read(serializer, " po-8 ").Should().Be(Reference<PurchaseOrder>.Create("PO-8"));
    }

    /// <summary>
    /// Reads a value the serializer refuses, and hands the exception back.
    /// </summary>
    private static Exception Refused<T>(IBsonSerializer<T> serializer, BsonValue value)
        => FluentActions.Invoking(() => MongoDb.Read(serializer, value)).Should().Throw<Exception>().Which;

    /// <summary>
    /// Reads a BSON value through a serializer whose type the caller does not name, as the value of an element.
    /// </summary>
    private static object? ReadBoxed(IBsonSerializer serializer, BsonValue value)
    {
        using var reader = new BsonDocumentReader(new BsonDocument("v", value));
        reader.ReadStartDocument();
        reader.ReadName("v");

        return serializer.Deserialize(BsonDeserializationContext.CreateRoot(reader));
    }

    private static string? Code(Exception exception) => ValueObjectErrors.TryGetCode(exception, out var code) ? code : null;

    /// <summary>
    /// A serializer of text that says nothing of its representation, and fails every read as a misconfigured serializer
    /// does, with an exception that is no refusal of the value.
    /// </summary>
    private sealed class MisconfiguredStringSerializer : SerializerBase<string>
    {
        public override string Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
            => throw new BsonSerializationException("Misconfigured.");
    }
}
