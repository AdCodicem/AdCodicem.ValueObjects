using System.Text;
using AdCodicem.ValueObjects.Fixtures.Grains;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Orleans;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using AdCodicem.ValueObjects.UnitTests.TestData;
using Orleans.Serialization;
using Orleans.Serialization.Codecs;
using static AdCodicem.ValueObjects.UnitTests.Grains.OrleansWire;

namespace AdCodicem.ValueObjects.UnitTests.Grains;

/// <summary>
/// <see cref="ValueObjectCodec{TSelf, TValue}"/> and the two registrations, through Orleans' serializer: a value object
/// written as the underlying value it replaces, with its type where the member is typed <see cref="object"/>, read back
/// through its rules unless trusted, refused on write when its type rejects its default, and each refusal carrying its
/// rule's code.
/// </summary>
/// <remarks>
/// Grain calls are <see cref="OrleansGrainCallTests"/>'s, and the exceptions a grain lets escape
/// <see cref="OrleansExceptionTests"/>'s.
/// </remarks>
public sealed class OrleansCodecTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, is written as the bare value it carries is,
    /// reads back what that value reads back, and refuses what its rules refuse with their codes.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_is_written_as_the_value_it_carries(string type)
        => Samples.All[type].RoundTripsThroughOrleansAsItsUnderlyingValue();

    /// <summary>
    /// A message holding value objects alone, nullable, in a list, a dictionary and an array is written byte for byte as
    /// its twin over primitives, and each reads the other's bytes.
    /// </summary>
    [Fact]
    public void An_order_of_value_objects_is_written_as_its_twin_over_primitives()
    {
        var id = Guid.Parse("6f1c2a6e-1f0e-4c39-9d43-2b6c7f1b2a10");
        var order = new GrainOrder
        {
            Id = GrainOrderId.Create(id),
            Sku = GrainSku.Create("abc-123"),
            Quantity = GrainQuantity.Create(3),
            Discount = null,
            Skus = [GrainSku.Create("abc-123"), GrainSku.Create("xyz-999")],
            Stock = new() { [GrainSku.Create("xyz-999")] = GrainQuantity.Create(7) },
            Amount = GrainAmount.Create(12.5m),
            Due = GrainDueDate.Create(new DateOnly(2026, 10, 17)),
            Gift = GrainGiftWrap.Create(true),
            Weight = GrainWeight.Create(1.25),
            Related = [GrainOrderId.Create(id)],
            Optional = GrainSku.Create("opt-001"),
        };

        // Each string a distinct instance, which Orleans writes in full, as the codec writes every text: Orleans writes a
        // string it met before in the message as a reference to it.
        var plain = new PlainGrainOrder
        {
            Id = id,
            Sku = Distinct("ABC-123"),
            Quantity = 3,
            Discount = null,
            Skus = [Distinct("ABC-123"), Distinct("XYZ-999")],
            Stock = new() { [Distinct("XYZ-999")] = 7 },
            Amount = 12.5m,
            Due = new DateOnly(2026, 10, 17),
            Gift = true,
            Weight = 1.25,
            Related = [id],
            Optional = Distinct("OPT-001"),
        };

        var written = Strict.SerializeToArray(order);
        written.Should().Equal(Bare.SerializeToArray(plain));

        var back = Strict.Read<GrainOrder>(written);
        back.Should().BeEquivalentTo(order);
        Bare.Read<PlainGrainOrder>(written).Should().BeEquivalentTo(plain);
        Strict.Read<GrainOrder>(Bare.SerializeToArray(plain)).Should().BeEquivalentTo(order);
    }

    [Fact]
    public void A_nullable_value_object_is_written_as_a_nullable_primitive_is()
    {
        Strict.SerializeToArray(new GrainHolder<GrainQuantity?> { Value = null })
            .Should().Equal(Bare.SerializeToArray(new GrainHolder<int?> { Value = null }));
        Strict.SerializeToArray(new GrainHolder<GrainQuantity?> { Value = GrainQuantity.Create(2) })
            .Should().Equal(Bare.SerializeToArray(new GrainHolder<int?> { Value = 2 }));

        Strict.Read<GrainHolder<GrainQuantity?>>(Bare.SerializeToArray(new GrainHolder<int?> { Value = null })).Value.Should().BeNull();
        Strict.Read<GrainHolder<GrainQuantity?>>(Bare.SerializeToArray(new GrainHolder<int?> { Value = 2 })).Value
            .Should().Be(GrainQuantity.Create(2));
        Strict.Deserialize<GrainQuantity?>(Strict.SerializeToArray<GrainQuantity?>(null)).Should().BeNull();
    }

    /// <summary>
    /// A value object met twice in a message writes its text in full each time, where Orleans writes a string it met before
    /// as a reference to it, which its codec of a nullable value object reads only as null: two members made from one
    /// literal, which a normalizer returns unchanged, and one value twice in a list, an array or a dictionary of nullable
    /// value objects read back, and a peer over the primitives reads them.
    /// </summary>
    [Fact]
    public void A_value_object_met_twice_writes_its_text_in_full_each_time()
    {
        var sku = GrainSku.Create("ABC-123");
        var text = sku.Value;
        text.Should().BeSameAs(GrainSku.Create("ABC-123").Value, "the normalizer returns a text it does not change");

        Strict.SerializeToArray(new GrainHolder<List<GrainSku>> { Value = [sku, sku] })
            .Should().Equal(Bare.SerializeToArray(new GrainHolder<List<string>> { Value = [Distinct(text), Distinct(text)] }));

        var order = new GrainOrder { Sku = sku, Quantity = GrainQuantity.Create(1), Optional = GrainSku.Create("ABC-123") };
        var written = Strict.SerializeToArray(order);
        Strict.Read<GrainOrder>(written).Should().BeEquivalentTo(order);
        Bare.Read<PlainGrainOrder>(written).Optional.Should().Be(text);

        Strict.Read<GrainHolder<List<GrainSku?>>>(Strict.SerializeToArray(new GrainHolder<List<GrainSku?>> { Value = [sku, null, sku] })).Value
            .Should().Equal(sku, null, sku);
        Strict.Read<GrainHolder<GrainSku?[]>>(Strict.SerializeToArray(new GrainHolder<GrainSku?[]> { Value = [sku, sku] })).Value
            .Should().Equal(sku, sku);
        Strict.Read<GrainHolder<Dictionary<GrainSku, GrainSku?>>>(
                Strict.SerializeToArray(new GrainHolder<Dictionary<GrainSku, GrainSku?>> { Value = new() { [sku] = sku } })).Value
            .Should().Equal(new Dictionary<GrainSku, GrainSku?> { [sku] = sku });
    }

    [Fact]
    public void A_text_beyond_ASCII_is_written_as_its_UTF8_bytes_as_the_string_is()
    {
        var code = AliasedCode.Create("ñé€");

        var written = Strict.SerializeToArray(new GrainHolder<AliasedCode> { Value = code });
        written.Should().Equal(Bare.SerializeToArray(new GrainHolder<string> { Value = "ñé€" }));
        Strict.Read<GrainHolder<AliasedCode>>(written).Value.Should().Be(code);
    }

    /// <summary>
    /// A text written in full takes its place among the references of the message, as the string would, so a reference
    /// written after it still points at what it named.
    /// </summary>
    [Fact]
    public void A_text_written_in_full_keeps_the_references_after_it()
    {
        var shared = Distinct("shared");
        var stock = new Dictionary<GrainSku, string> { [GrainSku.Create("ABC-123")] = shared, [GrainSku.Create("XYZ-999")] = shared };

        Strict.Read<GrainHolder<Dictionary<GrainSku, string>>>(Strict.SerializeToArray(new GrainHolder<Dictionary<GrainSku, string>> { Value = stock })).Value
            .Should().Equal(new Dictionary<GrainSku, string> { [GrainSku.Create("ABC-123")] = "shared", [GrainSku.Create("XYZ-999")] = "shared" });
    }

    /// <summary>
    /// A text a peer over the primitives repeats, which Orleans writes as a reference to the first, reads into a value
    /// object, but not into a nullable one: Orleans' own codec of a nullable value, which the package cannot replace, reads
    /// a reference only as null. Pinned, so that an Orleans release reading it fails here first.
    /// </summary>
    [Fact]
    public void A_text_a_primitive_peer_repeats_reads_into_a_value_object_but_not_into_a_nullable_one()
    {
        const string text = "ABC-123";

        Strict.Read<GrainHolder<List<GrainSku>>>(Bare.SerializeToArray(new GrainHolder<List<string>> { Value = [text, text] })).Value
            .Should().Equal(GrainSku.Create(text), GrainSku.Create(text));

        var refusal = FluentActions.Invoking(() => Strict.Read<GrainHolder<List<GrainSku?>>>(
                Bare.SerializeToArray(new GrainHolder<List<string?>> { Value = [text, text] })))
            .Should().Throw<ReferenceNotFoundException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out _).Should().BeFalse("Orleans' own exception carries no code");
    }

    /// <summary>
    /// A value object written by hand over a reference type other than <see cref="string"/> is written as Orleans writes
    /// that type, an instance met before in the message as a reference to it: twice in a list it reads back, twice in a
    /// list of nullable ones it does not, Orleans' codec of a nullable value reading a reference only as null.
    /// </summary>
    [Fact]
    public void A_value_object_written_by_hand_over_another_reference_type_met_twice_does_not_read_as_a_nullable_one()
    {
        var serializer = Build(static builder => builder.AddValueObject<Declared<OrleansLinkDeclaration, Uri>, Uri>());
        var link = Declared<OrleansLinkDeclaration, Uri>.Create(new Uri("https://example.com/orders"));

        serializer.Read<GrainHolder<List<Declared<OrleansLinkDeclaration, Uri>>>>(
                serializer.SerializeToArray(new GrainHolder<List<Declared<OrleansLinkDeclaration, Uri>>> { Value = [link, link] })).Value
            .Should().Equal(link, link);
        FluentActions.Invoking(() => serializer.Read<GrainHolder<List<Declared<OrleansLinkDeclaration, Uri>?>>>(
                serializer.SerializeToArray(new GrainHolder<List<Declared<OrleansLinkDeclaration, Uri>?>> { Value = [link, link] })))
            .Should().Throw<ReferenceNotFoundException>();
    }

    /// <summary>
    /// A value object held by a member typed <see cref="object"/> is written with its type, an object whose field 0 is the
    /// underlying value, and reads back as itself, alone, in a collection, nullable, or a construction of a generic value
    /// object the registry describes.
    /// </summary>
    [Fact]
    public void A_value_object_in_a_member_typed_object_is_written_with_its_type_and_reads_back_as_itself()
    {
        var written = Strict.SerializeToArray(new GrainBox { Value = GrainSku.Create("abc-123") });
        Encoding.UTF8.GetString(written).Should().Contain("AdCodicem.ValueObjects.Fixtures.Grains.GrainSku");
        Strict.Read<GrainBox>(written).Value.Should().Be(GrainSku.Create("abc-123"));

        object[] values =
        [
            GrainQuantity.Create(5),
            GrainOrderId.Create(Guid.Parse("6f1c2a6e-1f0e-4c39-9d43-2b6c7f1b2a10")),
            new List<GrainSku> { GrainSku.Create("abc-123") },
            new GrainQuantity?[] { GrainQuantity.Create(1), null },
            Reference<OrleansCodecOwner>.Create("po-1"),
            Iban.Create(ValidIban),
        ];
        foreach (var value in values)
        {
            Strict.Read<GrainBox>(Strict.SerializeToArray(new GrainBox { Value = value })).Value
                .Should().BeEquivalentTo(value, "a {0} in a member typed object reads back as itself", value.GetType().Name);
        }

        Strict.Deserialize<object>(Strict.SerializeToArray<object>(GrainSku.Create("abc-123"))).Should().Be(GrainSku.Create("abc-123"));
    }

    /// <summary>
    /// A value object held by a member typed by an interface it implements is written with its type, as in a member typed
    /// <see cref="object"/>, and reads back as itself.
    /// </summary>
    [Fact]
    public void A_value_object_in_a_member_typed_by_an_interface_is_written_with_its_type_and_reads_back_as_itself()
    {
        var written = Strict.SerializeToArray(new GrainInterfaceBox { Comparable = GrainQuantity.Create(5), Any = GrainSku.Create("abc-123") });

        Encoding.UTF8.GetString(written).Should().Contain(typeof(GrainQuantity).FullName).And.Contain(typeof(GrainSku).FullName);
        var read = Strict.Read<GrainInterfaceBox>(written);
        read.Comparable.Should().Be(GrainQuantity.Create(5));
        read.Any.Should().Be(GrainSku.Create("ABC-123"));
    }

    /// <summary>
    /// A value object written with its type takes its place among the references of the message, as Orleans' own structs
    /// do, so a reference written after it still points at what it named.
    /// </summary>
    [Fact]
    public void A_value_object_written_with_its_type_keeps_the_references_after_it()
    {
        var text = Distinct("shared");
        object[] values = [GrainSku.Create("abc-123"), text, text, GrainQuantity.Create(3), text];

        var read = Strict.Read<GrainHolder<object[]>>(Strict.SerializeToArray(new GrainHolder<object[]> { Value = values })).Value;

        read.Should().Equal(GrainSku.Create("ABC-123"), "shared", "shared", GrainQuantity.Create(3), "shared");
    }

    [Fact]
    public void An_alias_replaces_the_name_of_the_value_object_on_the_wire()
    {
        var written = Strict.SerializeToArray(new GrainBox { Value = AliasedCode.Create("ab") });

        Encoding.UTF8.GetString(written).Should().Contain("fixtures.grains.aliased-code").And.NotContain(nameof(AliasedCode));
        Strict.Read<GrainBox>(written).Value.Should().Be(AliasedCode.Create("ab"));
    }

    [Fact]
    public void A_value_read_goes_through_the_rules_and_is_normalized()
    {
        Strict.Read<GrainHolder<Iban>>(Primitive("fr76 3000 6000 0112 3456 7890 189")).Value.Should().Be(Iban.Create(ValidIban));
        Strict.Read<GrainHolder<GrainSku>>(Primitive(" abc-123 ")).Value.Should().Be(GrainSku.Create("ABC-123"));
    }

    /// <summary>
    /// A value the type refuses is refused with the rule that fired, its message, and its code, never with the value.
    /// </summary>
    [Fact]
    public void Each_rule_refuses_a_value_with_its_own_code_and_never_its_value()
    {
        Refused<Iban>(Primitive(string.Empty), ValueObjectErrorCodes.Required);
        Refused<Iban>(Primitive("FR76"), ValueObjectErrorCodes.TooShort);
        Refused<Iban>(Primitive("FR76" + new string('1', 31)), ValueObjectErrorCodes.TooLong);
        Refused<Iban>(Primitive("FR7630006000011234567890@89"), ValueObjectErrorCodes.InvalidFormat);
        Refused<Iban>(Primitive("FR7630006000011234567890188"), ValueObjectErrorCodes.InvalidFormat);
        Refused<GrainQuantity>(Primitive(500), ValueObjectErrorCodes.OutOfRange);
        Refused<CountryCode>(Primitive("ZZ"), ValueObjectErrorCodes.NotAKnownValue);
        Refused<CustomerId>(Primitive(Guid.Empty), ValueObjectErrorCodes.Required);

        var refusal = Refused<GrainQuantity>(Primitive(500), ValueObjectErrorCodes.OutOfRange);
        refusal.Message.Should().Be("The value read is not a valid GrainQuantity: The value must be less than or equal to 100.");
        refusal.InnerException.Should().BeNull();
    }

    [Fact]
    public void An_element_or_a_dictionary_value_is_refused_as_a_member_is()
    {
        Refused<List<GrainSku>>(Bare.SerializeToArray(new GrainHolder<List<string>> { Value = ["ABC-123", "TOO-LONG-1"] }), ValueObjectErrorCodes.TooLong);
        Refused<Dictionary<GrainSku, GrainQuantity>>(
            Bare.SerializeToArray(new GrainHolder<Dictionary<string, int>> { Value = new() { ["ABC-123"] = 500 } }),
            ValueObjectErrorCodes.OutOfRange);
        Refused<GrainQuantity?>(Bare.SerializeToArray(new GrainHolder<int?> { Value = 0 }), ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void A_value_object_in_a_member_typed_object_is_read_through_its_rules()
    {
        var forged = Strict.SerializeToArray(new GrainBox { Value = Unchecked.Of<GrainQuantity, int>(1000) });

        var refusal = FluentActions.Invoking(() => Strict.Read<GrainBox>(forged)).Should().Throw<SerializerException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        Trusted.Read<GrainBox>(forged).Value.Should().Be(Unchecked.Of<GrainQuantity, int>(1000));
    }

    [Fact]
    public void A_null_is_refused_as_required_whatever_the_trust()
    {
        foreach (var serializer in new[] { Strict, Trusted })
        {
            var refusal = Refused<GrainSku>(Primitive<string?>(null), ValueObjectErrorCodes.Required, serializer);
            refusal.Message.Should().Be("A null cannot be read as GrainSku; declare the member as GrainSku? instead.");
        }
    }

    /// <summary>
    /// A value the codec of the underlying value cannot read is refused as not parsable, whichever exception that codec
    /// throws, and never quotes it.
    /// </summary>
    [Fact]
    public void A_value_the_underlying_codec_cannot_read_is_refused_as_not_parsable()
    {
        var refusal = Refused<Bic>(Primitive(3), ValueObjectErrorCodes.NotParsable);
        refusal.Message.Should().Be("The Orleans value read cannot be read as Bic, a value object over String.");

        Refused<PageNumber>(Primitive("x"), ValueObjectErrorCodes.NotParsable);
        Refused<PageNumber>(Primitive(5_000_000_000L), ValueObjectErrorCodes.NotParsable);
        Refused<Grade>(Primitive(70000), ValueObjectErrorCodes.NotParsable);
        Refused<EffectiveDate>(Primitive(int.MaxValue), ValueObjectErrorCodes.NotParsable);
        Refused<RecordedAt>(Primitive(long.MaxValue), ValueObjectErrorCodes.NotParsable);
        Refused<OpeningTime>(Primitive(long.MaxValue), ValueObjectErrorCodes.NotParsable);
        Refused<Ratio>(Primitive(1e300), ValueObjectErrorCodes.NotParsable);
        Refused<CustomerId>(Primitive("abc"), ValueObjectErrorCodes.NotParsable).InnerException.Should().BeNull();

        // A narrower value of a wider type is read, as Orleans reads it for the primitive.
        Strict.Read<GrainHolder<PageNumber>>(Primitive(7L)).Value.Should().Be(PageNumber.Create(7));
    }

    [Fact]
    public void Truncated_bytes_fail_as_Orleans_reader_fails_them()
    {
        var written = Strict.SerializeToArray(GrainSku.Create("abc-123"));

        FluentActions.Invoking(() => Strict.Deserialize<GrainSku>(written[..^2])).Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void A_value_object_that_refuses_without_a_reason_is_refused_as_not_parsable()
    {
        var serializer = Build(static builder => builder.AddValueObject<HandWrittenId<MuteProfile>, string>());

        var refusal = Refused<HandWrittenId<MuteProfile>>(Primitive("mute_anything"), ValueObjectErrorCodes.NotParsable, serializer);
        refusal.Message.Should().StartWith("The value read is not a valid HandWrittenId`1");
    }

    /// <summary>
    /// A value object written with its type, which a later version may write with more fields, is read from field 0, any
    /// other field skipped; without field 0 it is refused as not parsable.
    /// </summary>
    [Fact]
    public void A_value_object_written_with_its_type_is_read_from_its_field_zero()
    {
        var extra = Craft((ref writer) =>
        {
            writer.WriteStartObject(0, typeof(object), typeof(GrainSku));
            StringCodec.WriteField(ref writer, 0, "abc-123");
            StringCodec.WriteField(ref writer, 1, "extra");
            writer.WriteEndObject();
        });
        Strict.Deserialize<object>(extra).Should().Be(GrainSku.Create("ABC-123"));

        var missing = Craft((ref writer) =>
        {
            writer.WriteStartObject(0, typeof(object), typeof(GrainSku));
            StringCodec.WriteField(ref writer, 1, "extra");
            writer.WriteEndObject();
        });
        var refusal = FluentActions.Invoking(() => Strict.Deserialize<object>(missing)).Should().Throw<SerializerException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
        code.Should().Be(ValueObjectErrorCodes.NotParsable);

        var refusedInside = Craft((ref writer) =>
        {
            writer.WriteStartObject(0, typeof(object), typeof(GrainQuantity));
            Int32Codec.WriteField(ref writer, 0, 500);
            writer.WriteEndObject();
        });
        FluentActions.Invoking(() => Strict.Deserialize<object>(refusedInside)).Should().Throw<SerializerException>()
            .Which.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    [Fact]
    public void A_member_the_message_leaves_out_holds_the_default_instance()
    {
        var older = Bare.SerializeToArray(new GrainNothing());

        var read = Strict.Read<GrainHolder<GrainQuantity>>(older).Value;
        ((IValueObject<GrainQuantity, int>)read).IsDefault.Should().BeTrue("Orleans never asks the codec for a member it does not find");
    }

    /// <summary>
    /// The default instance of a type that refuses its value is refused on write, before anything is sent; one whose value
    /// the type accepts is written as its value is.
    /// </summary>
    [Fact]
    public void A_default_is_written_only_when_its_type_accepts_its_value()
    {
#pragma warning disable VO0010 // the default instances, on purpose
        var quantity = FluentActions.Invoking(() => Strict.SerializeToArray(new GrainHolder<GrainQuantity> { Value = default }))
            .Should().Throw<SerializerException>().Which;
        quantity.Message.Should().Be("The value to write is not a valid GrainQuantity: The value must be greater than or equal to 1.");
        quantity.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
        FluentActions.Invoking(() => Strict.SerializeToArray(new GrainBox { Value = default(GrainSku) }))
            .Should().Throw<SerializerException>().Which.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.Required);
        FluentActions.Invoking(() => Trusted.SerializeToArray(new GrainHolder<GrainQuantity> { Value = default }))
            .Should().Throw<SerializerException>("the trust covers reads alone")
            .Which.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);

        Strict.SerializeToArray(new GrainHolder<GrainAmount> { Value = default })
            .Should().Equal(Bare.SerializeToArray(new GrainHolder<decimal> { Value = 0m }));
#pragma warning restore VO0010
        Strict.SerializeToArray(new GrainHolder<GrainQuantity> { Value = Unchecked.Of<GrainQuantity, int>(500) })
            .Should().Equal(Bare.SerializeToArray(new GrainHolder<int> { Value = 500 }), "an instance that is not the default is written as it is");
    }

    [Fact]
    public void A_trusted_read_neither_validates_nor_normalizes()
    {
        Trusted.Read<GrainHolder<GrainQuantity>>(Primitive(500)).Value.Value.Should().Be(500);
        Trusted.Read<GrainHolder<GrainSku>>(Primitive(" nope ")).Value.Value.Should().Be(" nope ");
        Build(static builder => builder.AddValueObject<GrainQuantity, int>(trusted: true))
            .Read<GrainHolder<GrainQuantity>>(Primitive(500)).Value.Value.Should().Be(500);
    }

    [Fact]
    public void A_codec_is_strict_unless_it_is_told_to_trust()
    {
        new ValueObjectCodec<GrainSku, string>().Trusted.Should().BeFalse();
        new ValueObjectCodec<GrainSku, string>(trusted: true).Trusted.Should().BeTrue();
        new ValueObjectCodecSelector(trusted: false).Trusted.Should().BeFalse();
        new ValueObjectCodecSelector(trusted: true).Trusted.Should().BeTrue();
    }

    /// <summary>
    /// Each registration answers before the ones made earlier: the last trust applies, and a typed registration made after
    /// <c>AddValueObjects</c> answers for its type.
    /// </summary>
    [Fact]
    public void The_last_registration_answers_first()
    {
        var forged = Primitive(500);

        Read(builder => builder.AddValueObjects().AddValueObjects(trusted: true)).Should().Be(500);
        ReadRefused(builder => builder.AddValueObjects(trusted: true).AddValueObjects());
        Read(builder => builder.AddValueObjects().AddValueObject<GrainQuantity, int>(trusted: true)).Should().Be(500);
        ReadRefused(builder => builder.AddValueObject<GrainQuantity, int>(trusted: true).AddValueObjects());

        int Read(Action<ISerializerBuilder> configure) => Build(configure).Read<GrainHolder<GrainQuantity>>(forged).Value.Value;

        void ReadRefused(Action<ISerializerBuilder> configure)
            => FluentActions.Invoking(() => Read(configure)).Should().Throw<SerializerException>()
                .Which.Data[ValueObjectErrors.ErrorCodeKey].Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// The typed registration serializes its value object without the registry, a construction of a generic one included,
    /// and allows it where Orleans writes its type; a value object it does not name has no codec.
    /// </summary>
    [Fact]
    public void A_typed_registration_serializes_its_value_object_alone()
    {
        var serializer = Build(static builder => builder.AddValueObject<Reference<OrleansTypedOwner>, string>());

        var holder = serializer.SerializeToArray(new GrainHolder<Reference<OrleansTypedOwner>> { Value = Reference<OrleansTypedOwner>.Create("po-2") });
        holder.Should().Equal(Bare.SerializeToArray(new GrainHolder<string> { Value = "PO-2" }));
        serializer.Read<GrainHolder<Reference<OrleansTypedOwner>>>(holder).Value.Should().Be(Reference<OrleansTypedOwner>.Create("PO-2"));
        serializer.Read<GrainBox>(serializer.SerializeToArray(new GrainBox { Value = Reference<OrleansTypedOwner>.Create("po-3") })).Value
            .Should().Be(Reference<OrleansTypedOwner>.Create("PO-3"));
        Refused<Reference<OrleansTypedOwner>>(Primitive("PO-123456789012"), ValueObjectErrorCodes.TooLong, serializer);

        FluentActions.Invoking(() => serializer.SerializeToArray(new GrainHolder<GrainSku> { Value = GrainSku.Create("abc-123") }))
            .Should().Throw<CodecNotFoundException>();
    }

    [Fact]
    public void A_value_object_written_by_hand_is_resolved_by_the_registry()
    {
        var code = Declared<OrleansCodeDeclaration, string>.Create("ab");

        var written = Strict.SerializeToArray(new GrainHolder<Declared<OrleansCodeDeclaration, string>> { Value = code });
        written.Should().Equal(Bare.SerializeToArray(new GrainHolder<string> { Value = "ab" }));
        Strict.Read<GrainHolder<Declared<OrleansCodeDeclaration, string>>>(written).Value.Should().Be(code);
        Strict.Read<GrainBox>(Strict.SerializeToArray(new GrainBox { Value = code })).Value.Should().Be(code);
        Refused<Declared<OrleansCodeDeclaration, string>>(Primitive("abcd"), ValueObjectErrorCodes.TooLong);
    }

    [Fact]
    public void Each_registration_refuses_a_missing_builder()
    {
        FluentActions.Invoking(static () => ValueObjectSerializerBuilderExtensions.AddValueObjects(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("builder");
        FluentActions.Invoking(static () => ValueObjectSerializerBuilderExtensions.AddValueObject<GrainSku, string>(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    /// <summary>
    /// The selector answers for a value object, never for a nullable one, which Orleans' own codec of a nullable value
    /// serves by asking for the value object's, nor for any other type. Orleans never asks it for a nullable value: this
    /// is called directly.
    /// </summary>
    [Fact]
    public void The_selector_answers_for_value_objects_alone()
    {
        var selector = new ValueObjectCodecSelector(trusted: false);

        selector.IsSupportedType(typeof(GrainSku)).Should().BeTrue();
        selector.IsSupportedType(typeof(GrainSku?)).Should().BeFalse();
        selector.IsSupportedType(typeof(Guid)).Should().BeFalse();
        selector.IsSupportedType(typeof(GrainOrder)).Should().BeFalse();
        selector.GetSpecializedCodec(typeof(GrainSku)).Should().BeOfType<ValueObjectCodec<GrainSku, string>>()
            .Which.Trusted.Should().BeFalse();
    }

    /// <summary>
    /// The type filter allows every value object and has no opinion on any other type, which Orleans then refuses as it
    /// would without the package: an object whose header names a type that is no value object is not read.
    /// </summary>
    [Fact]
    public void The_type_filter_allows_value_objects_and_leaves_every_other_type_to_Orleans()
    {
        var filter = new ValueObjectTypeFilter();
        filter.IsTypeAllowed(typeof(GrainSku)).Should().BeTrue();
        filter.IsTypeAllowed(typeof(Reference<OrleansCodecOwner>)).Should().BeTrue();
        filter.IsTypeAllowed(typeof(FileInfo)).Should().BeNull();
        filter.IsTypeAllowed(typeof(GrainOrder)).Should().BeNull();

        // Written by a peer allowing the type, since a writer applies its own type filters.
        using var permissive = Services(static builder => builder.Configure(static options => options.AddAllowedType(typeof(FileInfo))));
        var crafted = Craft(permissive, (ref writer) =>
        {
            writer.WriteStartObject(0, typeof(object), typeof(FileInfo));
            writer.WriteEndObject();
        });
        FluentActions.Invoking(() => Strict.Deserialize<object>(crafted)).Should().Throw<InvalidOperationException>()
            .WithMessage("Type \"System.IO.FileInfo\" is not allowed.*");
    }

    [Fact]
    public void Without_the_package_Orleans_finds_no_codec_for_a_value_object()
    {
        FluentActions.Invoking(() => Bare.SerializeToArray(GrainSku.Create("abc-123"))).Should().Throw<CodecNotFoundException>();
    }

    private static string Distinct(string text) => new(text.AsSpan());

    private static byte[] Primitive<TValue>(TValue value) => Bare.SerializeToArray(new GrainHolder<TValue> { Value = value });

    private static SerializerException Refused<T>(byte[] bytes, string code, Serializer? serializer = null)
    {
        var refusal = FluentActions.Invoking(() => (serializer ?? Strict).Read<GrainHolder<T>>(bytes))
            .Should().Throw<SerializerException>().Which;
        ValueObjectErrors.TryGetCode(refusal, out var carried).Should().BeTrue();
        carried.Should().Be(code);
        return refusal;
    }
}

/// <summary>The owner of the references the registry describes for <see cref="OrleansCodecTests"/>, which nothing else names.</summary>
public sealed class OrleansCodecOwner;

/// <summary>The owner of the references a typed registration serializes, which nothing else names.</summary>
public sealed class OrleansTypedOwner;

/// <summary>A link written by hand over <see cref="Uri"/>, which a typed registration serializes and nothing else declares.</summary>
public sealed class OrleansLinkDeclaration : IDeclaration<Uri>
{
    public static ValueObjectSchema Schema { get; } = ValueObjectSchema.Unconstrained;
}

/// <summary>A code of up to three characters, written by hand, which nothing else declares.</summary>
public sealed class OrleansCodeDeclaration : IDeclaration<string>
{
    public static ValueObjectSchema Schema { get; } = new() { MaxLength = 3 };

    public static ValidationResult Validate(string value)
        => value is null ? ValidationResult.Required() : value.Length > 3 ? ValidationResult.Failure(ValueObjectErrorCodes.TooLong, "Too long.") : ValidationResult.Success;
}
