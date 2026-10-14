using System.Runtime.Loader;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.ProtobufNet;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using ProtoBuf;
using ProtoBuf.Meta;
using static AdCodicem.ValueObjects.UnitTests.BinarySerialization.ProtoWire;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// <see cref="ValueObjectProtoModelExtensions"/>: the registration of value objects on a protobuf-net
/// <see cref="RuntimeTypeModel"/>, by assembly and one by one, beside protobuf-net's own <c>[ProtoContract]</c> on a value
/// object.
/// </summary>
/// <remarks>
/// The value objects that carry <c>[ProtoContract]</c> are this file's own, never <c>Domain/</c>'s: protobuf-net honours
/// the attribute on every model, which would change what every other test writes.
/// </remarks>
public sealed class ProtoModelTests
{
    private static readonly Guid Ticket = Guid.Parse("6f1c2a6e-1f0e-4c39-9d43-2b6c7f1b2a10");

    [Fact]
    public void Without_a_registration_protobuf_net_cannot_write_a_value_object()
    {
        var thrown = FluentActions.Invoking(() => Bytes(Model(), new Holder<Iban> { Value = Iban.Create("FR7630006000011234567890189") }))
            .Should().Throw<InvalidOperationException>().Which;

        thrown.Message.Should().Be($"No serializer defined for type: {typeof(Iban).FullName}");
    }

    /// <summary>
    /// A whole registration gives every value object the registry holds the strict serializer of the model's level, and
    /// names it as the scalar its underlying type is written as, but those over a type protobuf-net cannot write.
    /// </summary>
    [Fact]
    public void A_whole_registration_serves_every_value_object_but_those_protobuf_net_cannot_write()
    {
        var model = Model().AddValueObjects(typeof(Iban).Assembly);

        SerializerOf<Iban>(model).Should().Be<ValueObjectProtoSerializer<Iban, string, ValueObjectProtoProfiles.StrictLevel200>>();
        SerializerOf<Quantity>(model).Should().Be<ValueObjectProtoSerializer<Quantity, short, ValueObjectProtoProfiles.StrictLevel200>>();
        SerializerOf<CustomerId>(model).Should().Be<ValueObjectProtoSerializer<CustomerId, Guid, ValueObjectProtoProfiles.StrictLevel200>>();
        SerializerOf<AccountId>(model).Should().Be<ValueObjectProtoSerializer<AccountId, string, ValueObjectProtoProfiles.StrictLevel200>>();
        MetaTypeOf<Iban>(model)!.Name.Should().Be("string");
        MetaTypeOf<Quantity>(model)!.Name.Should().Be("int32");
        MetaTypeOf<RecordedAt>(model)!.Name.Should().Be(".bcl.DateTime");
        MetaTypeOf<RecordedAt>(model)!.Origin.Should().Be("protobuf-net/bcl.proto");

        MetaTypeOf<LedgerBalance>(model).Should().BeNull();
        MetaTypeOf<Fingerprint>(model).Should().BeNull();
        MetaTypeOf<OccurredAt>(model).Should().BeNull();
        FluentActions.Invoking(() => Bytes(model, new Holder<LedgerBalance> { Value = LedgerBalance.Create(1) }))
            .Should().Throw<InvalidOperationException>().WithMessage($"No serializer defined for type: {typeof(LedgerBalance).FullName}");
    }

    [Fact]
    public void A_trusted_registration_reads_without_validating()
    {
        var model = Model().AddValueObjects(trusted: true, typeof(Iban).Assembly);

        SerializerOf<Quantity>(model).Should().Be<ValueObjectProtoSerializer<Quantity, short, ValueObjectProtoProfiles.TrustedLevel200>>();
        Read<Holder<Quantity>>(model, Bytes(model, new Holder<short> { Value = 5000 })).Value.Value.Should().Be(5000);
    }

    /// <summary>
    /// The level is the model's when the registration runs, which protobuf-net lets nobody change once a type is added.
    /// </summary>
    [Fact]
    public void The_level_is_the_model_s_when_the_registration_runs()
    {
        SerializerOf<CustomerId>(Model(CompatibilityLevel.Level240).AddValueObjects(typeof(Iban).Assembly))
            .Should().Be<ValueObjectProtoSerializer<CustomerId, Guid, ValueObjectProtoProfiles.StrictLevel240>>();
        SerializerOf<CustomerId>(Model(CompatibilityLevel.Level300).AddValueObjects(typeof(Iban).Assembly))
            .Should().Be<ValueObjectProtoSerializer<CustomerId, Guid, ValueObjectProtoProfiles.StrictLevel300>>();
        SerializerOf<CustomerId>(Model(CompatibilityLevel.Level240).AddValueObjects(trusted: true, typeof(Iban).Assembly))
            .Should().Be<ValueObjectProtoSerializer<CustomerId, Guid, ValueObjectProtoProfiles.TrustedLevel240>>();
        SerializerOf<CustomerId>(Model(CompatibilityLevel.Level300).AddValueObject<CustomerId, Guid>(trusted: true))
            .Should().Be<ValueObjectProtoSerializer<CustomerId, Guid, ValueObjectProtoProfiles.TrustedLevel300>>();

        var model = Model().AddValueObject<Iban, string>();
        FluentActions.Invoking(() => model.DefaultCompatibilityLevel = CompatibilityLevel.Level300)
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_registration_run_twice_changes_nothing()
    {
        var model = Model().AddValueObjects(typeof(Iban).Assembly).AddValueObjects(typeof(Iban).Assembly);
        model.AddValueObject<Iban, string>().AddValueObject<Iban, string>();

        SerializerOf<Iban>(model).Should().Be<ValueObjectProtoSerializer<Iban, string, ValueObjectProtoProfiles.StrictLevel200>>();
        Read<Holder<Iban>>(model, Bytes(model, new Holder<Iban> { Value = Iban.Create("FR7630006000011234567890189") }))
            .Value.Should().Be(Iban.Create("FR7630006000011234567890189"));
    }

    /// <summary>
    /// A value object the model already serializes keeps its serializer under a whole registration, and asking for
    /// another by name is refused, naming both.
    /// </summary>
    [Fact]
    public void A_value_object_the_model_already_serializes_keeps_its_serializer()
    {
        var model = Model().AddValueObject<Quantity, short>(trusted: true).AddValueObjects(typeof(Iban).Assembly);

        SerializerOf<Quantity>(model).Should().Be<ValueObjectProtoSerializer<Quantity, short, ValueObjectProtoProfiles.TrustedLevel200>>();
        SerializerOf<Iban>(model).Should().Be<ValueObjectProtoSerializer<Iban, string, ValueObjectProtoProfiles.StrictLevel200>>();

        FluentActions.Invoking(() => model.AddValueObject<Quantity, short>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage(
                "The model already serializes 'Quantity' through 'ValueObjectProtoSerializer<Quantity, short, ValueObjectProtoProfiles.TrustedLevel200>', "
                + "which AddValueObject<Quantity, short>(trusted: false) would replace with "
                + "'ValueObjectProtoSerializer<Quantity, short, ValueObjectProtoProfiles.StrictLevel200>'.");
        FluentActions.Invoking(() => Model().AddValueObject<Iban, string>().AddValueObject<Iban, string>(trusted: true))
            .Should().Throw<InvalidOperationException>().WithMessage("*AddValueObject<Iban, string>(trusted: true)*");
    }

    /// <summary>
    /// A construction of a generic value object is in no registry walk until something registers it: it is added by name,
    /// round-trips and refuses with its code.
    /// </summary>
    [Fact]
    public void A_construction_of_a_generic_value_object_is_registered_by_name()
    {
        var model = Model().AddValueObjects(typeof(Iban).Assembly);
        MetaTypeOf<Reference<ProtoOwner>>(model).Should().BeNull();

        model.AddValueObject<Reference<ProtoOwner>, string>();

        var written = Bytes(model, new Holder<Reference<ProtoOwner>> { Value = Reference<ProtoOwner>.Create("po-1") });
        written.Should().Equal(Bytes(model, new Holder<string> { Value = "PO-1" }));
        Read<Holder<Reference<ProtoOwner>>>(model, written).Value.Value.Should().Be("PO-1");
        FluentActions.Invoking(() => Read<Holder<Reference<ProtoOwner>>>(model, Bytes(model, new Holder<string> { Value = new string('x', 13) })))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.TooLong);
    }

    [Fact]
    public void A_value_object_written_by_hand_is_registered_by_name()
    {
        var model = Model().AddValueObject<HandWrittenCode, string>().AddValueObject<HandWrittenLink, Uri>();
        var link = HandWrittenLink.Create(new Uri("https://example.com/a"));

        Read<Holder<HandWrittenCode>>(model, Bytes(model, new Holder<HandWrittenCode> { Value = HandWrittenCode.Create("ABC") }))
            .Value.Should().Be(HandWrittenCode.Create("ABC"));
        Bytes(model, new Holder<HandWrittenLink> { Value = link }).Should().Equal(Bytes(model, new Holder<Uri> { Value = link.Value }));
        Read<Holder<HandWrittenLink>>(model, Bytes(model, new Holder<HandWrittenLink> { Value = link })).Value.Should().Be(link);
        MetaTypeOf<HandWrittenLink>(model)!.Name.Should().Be("string");
        FluentActions.Invoking(() => Read<Holder<HandWrittenCode>>(model, Bytes(model, new Holder<string> { Value = "AB1" })))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.InvalidFormat);
    }

    /// <summary>
    /// A value object over a type protobuf-net writes but names otherwise than a scalar of its own is registered, and the
    /// export names it as protobuf-net does.
    /// </summary>
    [Fact]
    public void A_value_object_over_a_type_the_export_names_otherwise_keeps_protobuf_net_s_name()
    {
        var model = Model().AddValueObject<TestData.Declared<RawBytes, byte[]>, byte[]>();

        MetaTypeOf<TestData.Declared<RawBytes, byte[]>>(model)!.Name.Should().NotBe("bytes");
        Bytes(model, new Holder<TestData.Declared<RawBytes, byte[]>> { Value = TestData.Declared<RawBytes, byte[]>.Create([1, 2]) })
            .Should().Equal(0x0A, 0x02, 0x01, 0x02);
        ProtoScalars.Of(typeof(byte[]), CompatibilityLevel.Level200).Should().BeNull();
    }

    [Fact]
    public void A_value_object_over_a_type_protobuf_net_cannot_write_is_refused_by_name()
    {
        FluentActions.Invoking(() => Model().AddValueObject<Fingerprint, UInt128>())
            .Should().Throw<NotSupportedException>()
            .WithMessage("'Fingerprint' is a value object over UInt128, which protobuf-net has no inbuilt serializer for at Level200.");
        FluentActions.Invoking(() => Model(CompatibilityLevel.Level300).AddValueObject<OccurredAt, DateTimeOffset>())
            .Should().Throw<NotSupportedException>()
            .WithMessage("'OccurredAt' is a value object over DateTimeOffset, which protobuf-net has no inbuilt serializer for at Level300.");
    }

    [Fact]
    public void A_null_model_or_assembly_list_is_refused()
    {
        FluentActions.Invoking(() => ((RuntimeTypeModel)null!).AddValueObjects(typeof(Iban).Assembly))
            .Should().Throw<ArgumentNullException>().WithParameterName("model");
        FluentActions.Invoking(() => Model().AddValueObjects(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("assemblies");
        FluentActions.Invoking(() => ((RuntimeTypeModel)null!).AddValueObject<Iban, string>())
            .Should().Throw<ArgumentNullException>().WithParameterName("model");
    }

    /// <summary>
    /// A failed use, a value object nobody registered, freezes nothing: a registration made afterwards serves it.
    /// </summary>
    [Fact]
    public void A_registration_after_a_failed_use_serves_the_value_object()
    {
        var model = Model();
        FluentActions.Invoking(() => Bytes(model, new Holder<Iban> { Value = Iban.Create("FR7630006000011234567890189") }))
            .Should().Throw<InvalidOperationException>();

        model.AddValueObjects(typeof(Iban).Assembly);

        Bytes(model, new Holder<Iban> { Value = Iban.Create("FR7630006000011234567890189") })
            .Should().Equal(Bytes(model, new Holder<string> { Value = "FR7630006000011234567890189" }));
    }

    /// <summary>
    /// A whole registration runs the generated registration of the assemblies given first: here a copy of a module nothing
    /// has used, loaded into a context of its own, whose value objects nothing has registered.
    /// </summary>
    [Fact]
    public void A_whole_registration_registers_the_value_objects_of_a_module_nothing_has_used_yet()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "AdCodicem.ValueObjects.Fixtures.Untouched.dll");
        var assembly = new AssemblyLoadContext("A copy of the module nothing has used, for protobuf-net").LoadFromAssemblyPath(path);
        var rank = assembly.GetType("AdCodicem.ValueObjects.Fixtures.Untouched.Rank", throwOnError: true)!;
        ValueObjectRegistry.TryGet(rank, out _).Should().BeFalse("nothing has used this copy of the assembly yet");

        var model = Model().AddValueObjects(assembly);

        var metaType = model.GetTypes().Cast<MetaType>().Should().ContainSingle(type => type.Type == rank).Which;
        metaType.SerializerType!.GetGenericArguments().Should().Equal(rank, typeof(int), typeof(ValueObjectProtoProfiles.StrictLevel200));
        metaType.Name.Should().Be("int32");
        var five = rank.GetMethod("Create", [typeof(int)])!.Invoke(null, [5])!;
        using var accepted = new MemoryStream(Bytes(model, new Holder<int> { Value = 5 }));
        using var refused = new MemoryStream(Bytes(model, new Holder<int> { Value = 11 }));
        model.Deserialize(rank, accepted).Should().Be(five);
        FluentActions.Invoking(() => model.Deserialize(rank, refused))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// The default model, which <c>Serializer.*</c> reads, serves the value objects once they are registered on it, at
    /// its level, Level200, which protobuf-net lets nobody change.
    /// </summary>
    [Fact]
    public void The_default_model_serves_Serializer_once_the_value_objects_are_registered_on_it()
    {
        var model = ProtoDefaultModel.EnsureRegistered();
        var order = new ProtoOrder { Account = Iban.Create("FR7630006000011234567890189"), Quantity = Quantity.Create(3) };

        using var stream = new MemoryStream();

        // protobuf-net's analyzer points every Serializer call at the [ProtoModel] the suite declares: the default model
        // is what this test is about.
#pragma warning disable PBN3010
        Serializer.Serialize(stream, order);
        stream.Position = 0;
        var back = Serializer.Deserialize<ProtoOrder>(stream);
#pragma warning restore PBN3010

        back.Account.Should().Be(order.Account);
        back.Quantity.Should().Be(order.Quantity);
        model.DefaultCompatibilityLevel.Should().Be(CompatibilityLevel.Level200);
        FluentActions.Invoking(() => model.DefaultCompatibilityLevel = CompatibilityLevel.Level300)
            .Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// <c>[ProtoContract(Serializer = …)]</c> on the value object needs no registration on a runtime model, and the
    /// serializer it names is kept by a whole registration, which names the value object in the export when the
    /// serializer is one of this package's and the attribute names it no other way.
    /// </summary>
    [Fact]
    public void A_serializer_named_on_the_value_object_works_alone_and_is_kept()
    {
        var bare = Model();
        var sku = ProtoSku.Create("abc-123");
        var plain = Model(CompatibilityLevel.Level300);

        Read<Holder<ProtoSku>>(bare, Bytes(bare, new Holder<ProtoSku> { Value = sku })).Value.Should().Be(sku);
        Bytes(bare, new ProtoSpare { Sku = sku }).Should().Equal(Bytes(bare, new Holder<string> { Value = "ABC-123" }));
        var spare = Read<ProtoSpare>(bare, Bytes(bare, new ProtoSpare { Sku = sku, Skus = [sku, null] }));
        spare.Sku.Should().Be(sku);
        spare.Skus.Should().Equal(sku, null);
        Bytes(bare, new Holder<ProtoTicket> { Value = ProtoTicket.Create(Ticket) })
            .Should().Equal(Bytes(plain, new Holder<Guid> { Value = Ticket }), "the profile writes a Guid as text");

        // Nothing names a value object of the attribute route on a model nothing registered on, but the attribute's Name:
        // the export refers to a message it never defines.
        var unnamed = bare.GetSchema(typeof(ProtoAttributed), ProtoSyntax.Proto3).Split('\n').Select(line => line.Trim()).ToList();
        unnamed.Should().Contain(["ProtoSku Sku = 1;", "ProtoTicket Ticket = 2;", "int32 Seats = 3;"]);
        unnamed.Should().NotContain(line => line.StartsWith("message ProtoSku", StringComparison.Ordinal));

        var model = Model().AddValueObjects(typeof(Iban).Assembly);
        SerializerOf<ProtoSku>(model).Should().Be<ValueObjectProtoSerializer<ProtoSku, string>>();
        SerializerOf<ProtoTicket>(model).Should().Be<ValueObjectProtoSerializer<ProtoTicket, Guid, ValueObjectProtoProfiles.StrictLevel300>>();
        SerializerOf<ProtoSeats>(model).Should().Be<ValueObjectProtoSerializer<ProtoSeats, int, ValueObjectProtoProfiles.TrustedLevel200>>();
        SerializerOf<ProtoTally>(model).Should().Be<ValueObjectProtoSerializer<ProtoTally, long, ProtoTallyProfile>>();
        MetaTypeOf<ProtoSku>(model)!.Name.Should().Be("string");
        MetaTypeOf<ProtoTicket>(model)!.Name.Should().Be("string");
        MetaTypeOf<ProtoSeats>(model)!.Name.Should().Be("int32", "the attribute names it");
        MetaTypeOf<ProtoTally>(model)!.Name.Should().BeNullOrEmpty("the serializer is the application's");
        MetaTypeOf<ProtoBatch>(model)!.Name.Should().Be(".bcl.Decimal", "a trusted profile of the package names its level");
        MetaTypeOf<ProtoBatch>(model)!.Origin.Should().Be("protobuf-net/bcl.proto");
        MetaTypeOf<ProtoVoucher>(model)!.Name.Should().Be(".bcl.Guid", "the two-argument serializer writes at Level200");
        MetaTypeOf<ProtoVoucher>(model)!.Origin.Should().Be("protobuf-net/bcl.proto");
        MetaTypeOf<ProtoStamp>(model)!.Name.Should().Be(".bcl.DateTime", "a profile of the package at Level200 names its level");
        MetaTypeOf<ProtoStamp>(model)!.Origin.Should().Be("protobuf-net/bcl.proto");
        var level300 = Model(CompatibilityLevel.Level300).AddValueObjects(typeof(Iban).Assembly);
        MetaTypeOf<ProtoVoucher>(level300)!.Name.Should().Be(".bcl.Guid", "the serializer's level, not the model's");
        MetaTypeOf<ProtoStamp>(level300)!.Name.Should().Be(".bcl.DateTime", "the serializer's level, not the model's");

        var named = model.GetSchema(typeof(ProtoAttributed), ProtoSyntax.Proto3).Split('\n').Select(line => line.Trim()).ToList();
        named.Should().Contain(["string Sku = 1;", "string Ticket = 2;", "int32 Seats = 3;", "ProtoTally Tally = 4;"]);
        named.Should().NotContain(line => line.StartsWith("message ProtoTally", StringComparison.Ordinal), "the profile is the application's");
        Read<Holder<ProtoSeats>>(model, Bytes(model, new Holder<int> { Value = 0 })).Value.Value.Should().Be(0, "the profile trusts the bytes");

        FluentActions.Invoking(() => Model().AddValueObject<ProtoSku, string>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("The model already serializes 'ProtoSku' through 'ValueObjectProtoSerializer<ProtoSku, string>'*");
        Model().AddValueObject<ProtoSeats, int>(trusted: true).GetTypes().Cast<MetaType>().Should().ContainSingle();
    }

    /// <summary>
    /// A bare <c>[ProtoContract]</c> on a value object makes protobuf-net write it as an empty message, which loses the
    /// value with nothing thrown; registered before the model meets it, the value object is written as its value; after
    /// the model has written one, protobuf-net refuses the change.
    /// </summary>
    [Fact]
    public void A_bare_ProtoContract_on_a_value_object_is_taken_over_when_registered_first()
    {
        var unregistered = Model();
        var written = Bytes(unregistered, new Holder<ProtoBareCode> { Value = ProtoBareCode.Create("ab") });
        written.Should().Equal(0x0A, 0x00);
        Read<Holder<ProtoBareCode>>(unregistered, written).Value.Value.Should().BeEmpty();

        var registered = Model().AddValueObjects(typeof(Iban).Assembly);
        Bytes(registered, new Holder<ProtoBareCode> { Value = ProtoBareCode.Create("ab") })
            .Should().Equal(Bytes(registered, new Holder<string> { Value = "ab" }));
        MetaTypeOf<ProtoBareCode>(registered)!.Name.Should().Be("string");

        var added = Model();
        added.Add(typeof(ProtoBareCode));
        added.AddValueObject<ProtoBareCode, string>();
        Read<Holder<ProtoBareCode>>(added, Bytes(added, new Holder<string> { Value = "ab" })).Value.Value.Should().Be("ab");

        FluentActions.Invoking(() => unregistered.AddValueObject<ProtoBareCode, string>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage($"The type cannot be changed once a serializer has been generated for {typeof(ProtoBareCode).FullName}*");
    }

    private static MetaType? MetaTypeOf<T>(RuntimeTypeModel model)
        => model.GetTypes().Cast<MetaType>().FirstOrDefault(metaType => metaType.Type == typeof(T));

    private static Type? SerializerOf<T>(RuntimeTypeModel model) => MetaTypeOf<T>(model)?.SerializerType;
}

/// <summary>The owner of the references this file's tests close <see cref="Reference{TOwner}"/> over, and nobody else.</summary>
public sealed class ProtoOwner;

/// <summary>Bytes, which protobuf-net writes and its export names otherwise than the scalars of the package's table.</summary>
public sealed class RawBytes : TestData.IDeclaration<byte[]>
{
    public static ValueObjectSchema Schema => ValueObjectSchema.Unconstrained;
}

/// <summary>The attribute route at protobuf-net's default, through the two-argument serializer.</summary>
[ValueObject<string>(MaxLength = 8)]
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<ProtoSku, string>))]
public readonly partial struct ProtoSku : IValueObjectNormalizer<string>
{
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}

/// <summary>The attribute route through a shipped profile: a <see cref="Guid"/> written as text.</summary>
[ValueObject<Guid>]
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<ProtoTicket, Guid, ValueObjectProtoProfiles.StrictLevel300>))]
public readonly partial struct ProtoTicket;

/// <summary>The attribute route, trusted, under the name the attribute gives: its scalar, as the guide advises.</summary>
[ValueObject<int>]
[ProtoContract(Name = "int32", Serializer = typeof(ValueObjectProtoSerializer<ProtoSeats, int, ValueObjectProtoProfiles.TrustedLevel200>))]
public readonly partial struct ProtoSeats : IValueObjectMinimum<int>
{
    public static int Minimum => 1;
}

/// <summary>The attribute route through a trusted profile of the package, at Level240.</summary>
[ValueObject<decimal>]
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<ProtoBatch, decimal, ValueObjectProtoProfiles.TrustedLevel240>))]
public readonly partial struct ProtoBatch;

/// <summary>The attribute route through the two-argument serializer over a type the level changes: a <c>bcl.Guid</c>.</summary>
[ValueObject<Guid>]
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<ProtoVoucher, Guid>))]
public readonly partial struct ProtoVoucher;

/// <summary>The attribute route through a strict profile of the package at Level200, with no name: a <c>bcl.DateTime</c>.</summary>
[ValueObject<DateTime>]
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<ProtoStamp, DateTime, ValueObjectProtoProfiles.StrictLevel200>))]
public readonly partial struct ProtoStamp;

/// <summary>The attribute route through a profile of the application's own.</summary>
[ValueObject<long>]
[ProtoContract(Serializer = typeof(ValueObjectProtoSerializer<ProtoTally, long, ProtoTallyProfile>))]
public readonly partial struct ProtoTally;

/// <summary>Strict, at Level240: the profile of <see cref="ProtoTally"/>.</summary>
public sealed class ProtoTallyProfile : IValueObjectProtoProfile
{
    public static bool Trusted => false;

    public static CompatibilityLevel CompatibilityLevel => CompatibilityLevel.Level240;
}

/// <summary>The trap: a bare <c>[ProtoContract]</c> on the value object.</summary>
[ValueObject<string>(MaxLength = 5)]
[ProtoContract]
public readonly partial struct ProtoBareCode;

/// <summary>A message over value objects that carry their serializer.</summary>
[ProtoContract]
public sealed class ProtoAttributed
{
    [ProtoMember(1)]
    public ProtoSku Sku { get; set; }

    [ProtoMember(2)]
    public ProtoTicket Ticket { get; set; }

    [ProtoMember(3)]
    public ProtoSeats Seats { get; set; }

    [ProtoMember(4)]
    public ProtoTally Tally { get; set; }

    [ProtoMember(5)]
    public List<ProtoSku>? Skus { get; set; }
}

/// <summary>
/// A message over a nullable value object that carries its serializer, alone and in a list that keeps its nulls, which a
/// generated model cannot hold.
/// </summary>
[ProtoContract]
public sealed class ProtoSpare
{
    [ProtoMember(1)]
    public ProtoSku? Sku { get; set; }

    [ProtoMember(2)]
    [NullWrappedValue]
    public List<ProtoSku?>? Skus { get; set; }
}

/// <summary>The same message over the primitives, at the levels the value objects' serializers name.</summary>
[ProtoContract]
public sealed class PlainProtoAttributed
{
    [ProtoMember(1)]
    public string? Sku { get; set; }

    [ProtoMember(2)]
    [CompatibilityLevel(CompatibilityLevel.Level300)]
    public Guid Ticket { get; set; }

    [ProtoMember(3)]
    public int Seats { get; set; }

    [ProtoMember(4)]
    public long Tally { get; set; }

    [ProtoMember(5)]
    public List<string>? Skus { get; set; }
}
