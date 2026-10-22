using AdCodicem.ValueObjects.ProtobufNet;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using ProtoBuf;
using ProtoBuf.Meta;
using ProtoBuf.Serializers;
using static AdCodicem.ValueObjects.UnitTests.BinarySerialization.ProtoWire;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// <see cref="ValueObjectProtoSerializer{TSelf, TValue, TProfile}"/> and its two-argument form: a value object written as
/// protobuf-net writes its underlying type, read back through its rules, and refused on write when the type rejects it.
/// </summary>
/// <remarks>
/// Each test registers the value objects it writes on a model of its own, with <c>AddValueObject</c>, which
/// <see cref="ProtoModelTests"/> covers; the <c>.proto</c> export is <see cref="ProtoSchemaTests"/>'s.
/// </remarks>
public sealed class ProtoSerializerTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, is written as the bare value it carries is,
    /// and reads back what that value reads back, at each level, strict and trusted.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_is_written_as_the_value_it_carries(string type)
        => Samples.All[type].RoundTripsThroughProtobufAsItsUnderlyingValue();

    [Fact]
    public void A_value_read_goes_through_the_rules_and_is_normalized()
    {
        var model = Model().AddValueObject<Iban, string>();

        Read<Holder<Iban>>(model, Bytes(model, new Holder<string> { Value = "fr76 3000 6000 0112 3456 7890 189" }))
            .Value.Should().Be(Iban.Create(ValidIban));
    }

    [Fact]
    public void A_trusted_read_neither_validates_nor_normalizes()
    {
        var model = Model().AddValueObject<Iban, string>(trusted: true).AddValueObject<Quantity, short>(trusted: true);

        Read<Holder<Quantity>>(model, Bytes(model, new Holder<short> { Value = 5000 })).Value.Value.Should().Be(5000);
        Read<Holder<Iban>>(model, Bytes(model, new Holder<string> { Value = "fr76 3000" })).Value.Value.Should().Be("fr76 3000");
    }

    /// <summary>
    /// A value the type refuses is refused with the rule that fired, its message, and its code, never with the value.
    /// </summary>
    [Fact]
    public void Each_rule_refuses_a_value_with_its_own_code_and_never_its_value()
    {
        RefusedRead<Iban, string>(string.Empty, ValueObjectErrorCodes.Required);
        RefusedRead<Iban, string>("FR76", ValueObjectErrorCodes.TooShort);
        RefusedRead<Iban, string>("FR76" + new string('1', 31), ValueObjectErrorCodes.TooLong);
        RefusedRead<Iban, string>("FR7630006000011234567890@89", ValueObjectErrorCodes.InvalidFormat);
        RefusedRead<Iban, string>("FR7630006000011234567890188", ValueObjectErrorCodes.InvalidFormat);
        RefusedRead<Quantity, short>(1001, ValueObjectErrorCodes.OutOfRange);
        RefusedRead<CountryCode, string>("ZZ", ValueObjectErrorCodes.NotAKnownValue);
        RefusedRead<CustomerId, Guid>(Guid.Empty, ValueObjectErrorCodes.Required);

        static void RefusedRead<TSelf, TValue>(TValue value, string code)
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            foreach (var level in Levels)
            {
                var model = Model(level).AddValueObject<TSelf, TValue>();
                var bytes = Bytes(model, new Holder<TValue> { Value = value });

                // Guid.Empty: protobuf-net writes nothing for the primitive, and never calls a serializer for a field left
                // out; an empty bcl.Guid message, which a writer may send, is read instead. At Level300, where a Guid is
                // text, empty text is no Guid at all.
                if (bytes.Length == 0)
                {
                    if (level == CompatibilityLevel.Level300)
                    {
                        continue;
                    }

                    bytes = [0x0A, 0x00];
                }

                var refusal = FluentActions.Invoking(() => Read<Holder<TSelf>>(model, bytes))
                    .Should().Throw<ProtoException>().Which;
                refusal.Message.Should().StartWith($"The value read is not a valid {typeof(TSelf).Name}: ");
                if (value?.ToString() is { Length: > 2 } text)
                {
                    refusal.Message.Should().NotContain(text, "a refusal never quotes the value");
                }

                refusal.InnerException.Should().BeNull();
                refusal.ShouldCarry(code);
                refusal.Data["AdCodicem.ValueObjects.ProtobufNet.ValueObject"].Should().Be(typeof(TSelf).Name);
            }
        }
    }

    [Fact]
    public void A_value_object_that_refuses_without_a_reason_is_refused_as_not_parsable()
    {
        var model = Model().AddValueObject<HandWrittenRating, int>();

        var refusal = FluentActions.Invoking(() => Read<Holder<HandWrittenRating>>(model, Bytes(model, new Holder<int> { Value = 9 })))
            .Should().Throw<ProtoException>().Which;

        refusal.Message.Should().Be("The value read is not a valid HandWrittenRating: ");
        refusal.ShouldCarry(ValueObjectErrorCodes.NotParsable);
        Read<Holder<HandWrittenRating>>(model, Bytes(model, new Holder<int> { Value = 4 })).Value.Should().Be(HandWrittenRating.Create(4));
    }

    /// <summary>
    /// A value the inbuilt serializer of the underlying type cannot read is refused as not parsable, strict or trusted,
    /// without the serializer's own exception, whose message may quote the value: one case for each exception
    /// protobuf-net throws.
    /// </summary>
    [Fact]
    public void A_value_its_underlying_serializer_cannot_read_is_refused_as_not_parsable_without_quoting_it()
    {
        var primitive = Model();

        // ProtoException: a field of another wire type, a varint where text is expected, and a string where a bcl.Guid
        // message is expected at Level200.
        Unreadable<Iban, string>([0x08, 0x05], "Iban", "String");
        Unreadable<CustomerId, Guid>(Bytes(primitive, new Holder<string> { Value = "x" }), "CustomerId", "Guid");

        // OverflowException: a varint beyond the range of a short.
        Unreadable<Quantity, short>(Bytes(primitive, new Holder<long> { Value = long.MaxValue }), "Quantity", "Int16");

        // ArgumentOutOfRangeException: a day number beyond DateOnly's range, ticks beyond TimeOnly's, and a bcl.DateTime
        // beyond DateTime's.
        Unreadable<EffectiveDate, DateOnly>(Bytes(primitive, new Holder<int> { Value = int.MaxValue }), "EffectiveDate", "DateOnly");
        Unreadable<OpeningTime, TimeOnly>(Bytes(primitive, new Holder<long> { Value = long.MaxValue }), "OpeningTime", "TimeOnly");
        Unreadable<RecordedAt, DateTime>([0x0A, 0x06, 0x08, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F], "RecordedAt", "DateTime");

        // UriFormatException, a FormatException: text no Uri parses, read into a value object written by hand over Uri.
        Unreadable<HandWrittenLink, Uri>(Bytes(primitive, new Holder<string> { Value = "http://[::1" }), "HandWrittenLink", "Uri");

        // EndOfStreamException: text announced longer than the bytes left.
        Unreadable<Iban, string>([0x0A, 0x05, 0x41], "Iban", "String");

        // InvalidOperationException: a decimal written as text at Level300 that no decimal parses, one beyond decimal's
        // range, text longer than the 32 bytes protobuf-net reads a decimal into, and a negative length. Its messages quote
        // the text: "Unable to parse decimal: 'abc'".
        Unreadable<Amount, decimal>(Bytes(primitive, new Holder<string> { Value = "abc" }), "Amount", "Decimal", CompatibilityLevel.Level300);
        Unreadable<Amount, decimal>(Bytes(primitive, new Holder<string> { Value = "1e400" }), "Amount", "Decimal", CompatibilityLevel.Level300);
        Unreadable<Amount, decimal>(Bytes(primitive, new Holder<string> { Value = new string('9', 32) }), "Amount", "Decimal", CompatibilityLevel.Level300);
        Unreadable<Amount, decimal>(Bytes(primitive, new Holder<string> { Value = "1." + new string('0', 31) }), "Amount", "Decimal", CompatibilityLevel.Level300);
        Unreadable<Iban, string>([0x0A, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F], "Iban", "String");

        static void Unreadable<TSelf, TValue>(byte[] bytes, string name, string over, CompatibilityLevel level = CompatibilityLevel.Level200)
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            foreach (var trusted in new[] { false, true })
            {
                var model = Model(level).AddValueObject<TSelf, TValue>(trusted);
                var refusal = FluentActions.Invoking(() => Read<Holder<TSelf>>(model, bytes))
                    .Should().Throw<ProtoException>().Which;
                refusal.Message.Should().Be($"The protobuf value read cannot be read as {name}, a value object over {over}.");
                refusal.InnerException.Should().BeNull();
                refusal.ShouldCarry(ValueObjectErrorCodes.NotParsable);
            }
        }
    }

    /// <summary>
    /// A stream disposed while a value is read is no value the underlying serializer refused: its
    /// <see cref="ObjectDisposedException"/>, an <see cref="InvalidOperationException"/>, goes through as it is.
    /// </summary>
    [Fact]
    public void A_stream_disposed_while_a_value_is_read_is_not_refused_as_not_parsable()
    {
        var model = Model().AddValueObject<Iban, string>();
        var bytes = Bytes(model, new Holder<Iban> { Value = Iban.Create(ValidIban) });

        // protobuf-net reads ten bytes ahead for a field's header, a byte at a time from this stream, then asks for more as
        // the serializer reads the text's length: the stream is disposed by then.
        using var stream = new DisposedMidway(bytes, served: 10);
        FluentActions.Invoking(() => model.Deserialize<Holder<Iban>>(stream)).Should().Throw<ObjectDisposedException>();
        stream.Served.Should().Be(10, "the header alone was read when the stream was found disposed");
    }

    /// <summary>
    /// What a deserializer, an array or a message initializer hands out before any rule ran is refused on write when the
    /// type rejects it, alone, nullable, in a list and as a dictionary key, strict or trusted.
    /// </summary>
    [Fact]
    public void An_uninitialized_value_the_type_rejects_is_refused_on_write()
    {
        foreach (var trusted in new[] { false, true })
        {
            var model = Model().AddValueObject<PageNumber, int>(trusted).AddValueObject<Iban, string>(trusted);

#pragma warning disable VO0010 // The uninitialized instance is what the write must refuse.
            RefusedWrite(() => Bytes(model, new Holder<PageNumber>()), "PageNumber", ValueObjectErrorCodes.OutOfRange);
            RefusedWrite(() => Bytes(model, new Holder<Iban>()), "Iban", ValueObjectErrorCodes.Required);
            RefusedWrite(() => Bytes(model, new Holder<PageNumber?> { Value = default(PageNumber) }), "PageNumber", ValueObjectErrorCodes.OutOfRange);
            RefusedWrite(() => Bytes(model, new Holder<List<Iban>> { Value = [default] }), "Iban", ValueObjectErrorCodes.Required);
            RefusedWrite(() => Bytes(model, new Holder<Dictionary<Iban, int>> { Value = new() { [default] = 1 } }), "Iban", ValueObjectErrorCodes.Required);
#pragma warning restore VO0010
        }

        static void RefusedWrite(Func<byte[]> write, string name, string code)
        {
            var refusal = write.Should().Throw<ProtoException>().Which;
            refusal.Message.Should().StartWith($"The value to write is not a valid {name}: ");
            refusal.ShouldCarry(code);
            refusal.Data["AdCodicem.ValueObjects.ProtobufNet.ValueObject"].Should().Be(name);
        }
    }

    /// <summary>
    /// A valid zero is written, where protobuf-net leaves out the primitive's: readers of either read it back.
    /// </summary>
    [Fact]
    public void A_valid_zero_is_written_where_the_primitive_is_left_out()
    {
        var model = Model().AddValueObject<SequenceNumber, uint>().AddValueObject<Consent, bool>();

        Bytes(model, new Holder<SequenceNumber> { Value = SequenceNumber.Create(0) }).Should().Equal(0x08, 0x00);
        Bytes(model, new Holder<uint> { Value = 0 }).Should().BeEmpty();
        Bytes(model, new Holder<Consent> { Value = Consent.Create(false) }).Should().Equal(0x08, 0x00);
        Read<Holder<uint>>(model, [0x08, 0x00]).Value.Should().Be(0);
        Read<Holder<SequenceNumber>>(model, [0x08, 0x00]).Value.Should().Be(SequenceNumber.Create(0));
    }

    /// <summary>
    /// protobuf-net never calls a serializer for a field the message leaves out, nor for the zero a map entry leaves
    /// out: the member holds the default instance, which no rule checked, unless it is declared nullable.
    /// </summary>
    [Fact]
    public void A_field_left_out_holds_the_default_instance_and_a_nullable_one_null()
    {
        var model = Model().AddValueObject<Iban, string>().AddValueObject<Quantity, short>().AddValueObject<CountryCode, string>()
            .AddValueObject<PageNumber, int>();

        var order = Read<ProtoOrder>(model, []);
        ((IValueObject<Iban, string>)order.Account).IsDefault.Should().BeTrue("an IBAN is required, and nothing checked this one");
        order.Quantity.Value.Should().Be(0);
        order.Discount.Should().BeNull();

        var pages = Bytes(model, new Holder<Dictionary<string, int>> { Value = new() { ["FR"] = 0 } });
        Read<Holder<Dictionary<CountryCode, PageNumber>>>(model, pages).Value!
            .Should().ContainSingle().Which.Value.Value.Should().Be(0, "a page number is at least 1, and nothing checked this one");
    }

    [Fact]
    public void A_whole_message_is_written_as_the_same_message_over_primitives_and_read_back()
    {
        var model = Model().AddValueObject<Iban, string>().AddValueObject<Quantity, short>().AddValueObject<CountryCode, string>();
        var order = new ProtoOrder
        {
            Account = Iban.Create(ValidIban),
            Quantity = Quantity.Create(3),
            Discount = Quantity.Create(2),
            Lines = [Quantity.Create(1), Quantity.Create(2)],
            Stock = new() { [CountryCode.France] = Quantity.Create(7) },
        };
        var plain = new PlainProtoOrder
        {
            Account = ValidIban,
            Quantity = 3,
            Discount = 2,
            Lines = [1, 2],
            Stock = new() { ["FR"] = 7 },
        };

        var written = Bytes(model, order);
        written.Should().Equal(Bytes(model, plain));
        var back = Read<ProtoOrder>(model, written);
        back.Account.Should().Be(order.Account);
        back.Quantity.Should().Be(order.Quantity);
        back.Discount.Should().Be(order.Discount);
        back.Lines.Should().Equal(order.Lines);
        back.Stock.Should().Equal(order.Stock);
    }

    /// <summary>
    /// A <see langword="null"/> in a list of nullable value objects is protobuf-net's to refuse, as for a list of nullable
    /// integers, and kept under <see cref="NullWrappedValueAttribute"/> exactly as such a list keeps it.
    /// </summary>
    [Fact]
    public void A_null_in_a_list_of_nullable_value_objects_is_kept_only_under_NullWrappedValue_as_for_a_primitive()
    {
        var model = Model().AddValueObject<Quantity, short>();

        FluentActions.Invoking(() => Bytes(model, new Holder<List<Quantity?>> { Value = [Quantity.Create(1), null] }))
            .Should().Throw<NullReferenceException>();
        FluentActions.Invoking(() => Bytes(model, new Holder<List<short?>> { Value = [1, null] }))
            .Should().Throw<NullReferenceException>();

        var written = Bytes(model, new WrappedQuantities { Values = [Quantity.Create(1), null, Quantity.Create(3)] });
        written.Should().Equal(Bytes(model, new WrappedShorts { Values = [1, null, 3] }));
        Read<WrappedQuantities>(model, written).Values.Should().Equal(Quantity.Create(1), null, Quantity.Create(3));
    }

    /// <summary>
    /// The elements of a <see cref="NullWrappedValueAttribute"/> list, which protobuf-net reads and writes through the
    /// serializer's <c>ISerializer&lt;TSelf?&gt;</c>, are held to the rules as any value object: a refused one read, and a
    /// default one written, are refused with their codes, through the three-argument serializer and the two-argument one.
    /// </summary>
    [Fact]
    public void The_elements_of_a_NullWrappedValue_list_are_read_and_written_through_the_rules()
    {
        var model = Model().AddValueObject<Quantity, short>().AddValueObject<PageNumber, int>();

        FluentActions.Invoking(() => Read<WrappedQuantities>(model, Bytes(model, new WrappedShorts { Values = [1, null, 5000] })))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.OutOfRange);
#pragma warning disable VO0010 // The uninitialized instance is what the write must refuse.
        FluentActions.Invoking(() => Bytes(model, new WrappedPages { Values = [PageNumber.Create(1), default(PageNumber)] }))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.OutOfRange);

        // ProtoSku names the two-argument serializer on itself, which protobuf-net applies on any model.
        var bare = Model();
        FluentActions.Invoking(() => Read<ProtoSpare>(bare, Bytes(bare, new PlainProtoSpare { Skus = ["ABC", null, "ABCDEFGHIJ"] })))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.TooLong);
        FluentActions.Invoking(() => Bytes(bare, new ProtoSpare { Skus = [ProtoSku.Create("abc"), default(ProtoSku)] }))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.Required);
#pragma warning restore VO0010
        Read<ProtoSpare>(bare, Bytes(bare, new PlainProtoSpare { Skus = [" abc ", null] })).Skus.Should().Equal(ProtoSku.Create("ABC"), null);
    }

    /// <summary>
    /// protobuf-net calls a custom serializer whatever <c>DataFormat</c> the member declares, so a value object is written
    /// and read as <c>DataFormat.Default</c> writes its underlying type: a <c>ZigZag</c> or <c>FixedSize</c> member of a
    /// value object writes what the same member over the primitive writes without one, and reads what a <c>ZigZag</c>
    /// writer sends for 7 as 14.
    /// </summary>
    [Fact]
    public void A_member_s_DataFormat_never_reaches_a_value_object()
    {
        var model = Model().AddValueObject<Quantity, short>().AddValueObject<PageNumber, int>().AddValueObject<RecordedAt, DateTime>();
        var recorded = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);
        var formatted = new ProtoFormatted { Quantity = Quantity.Create(7), Page = PageNumber.Create(7), Recorded = RecordedAt.Create(recorded) };

        var written = Bytes(model, formatted);
        written.Should().Equal(Bytes(model, new PlainProtoUnformatted { Quantity = 7, Page = 7, Recorded = recorded }));
        written.Should().NotEqual(Bytes(model, new PlainProtoFormatted { Quantity = 7, Page = 7, Recorded = recorded }));
        Hex(written).Should().StartWith("08071007");
        Hex(Bytes(model, new PlainProtoFormatted { Quantity = 7, Page = 7, Recorded = recorded })).Should().StartWith("080E1507000000");

        Read<ProtoFormatted>(model, [0x08, 0x0E]).Quantity.Should().Be(Quantity.Create(14), "the bytes a ZigZag writer sends for 7");
    }

    /// <summary>
    /// A root value object, the message itself, is written at its serializer's level, where protobuf-net writes a root
    /// primitive at <c>Level200</c> whatever the model's level: from <c>Level240</c>, a root value object over a type the
    /// level changes is not what the root primitive writes, and the root primitive's bytes are refused, while the same
    /// value object as a member writes what the member over the primitive writes.
    /// </summary>
    [Fact]
    public void A_root_value_object_is_written_at_its_serializer_s_level_where_a_root_primitive_is_at_Level200()
    {
        var customer = CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001"));
        var model = Model(CompatibilityLevel.Level300).AddValueObject<CustomerId, Guid>().AddValueObject<Amount, decimal>();

        var root = Bytes(model, customer);
        root.Should().Equal(Bytes(model, new Holder<Guid> { Value = customer.Value }), "the Guid is text at Level300, as a member's");
        root.Should().NotEqual(Bytes(model, customer.Value));
        Bytes(model, customer.Value).Should().Equal(Bytes(Model(), customer.Value), "protobuf-net writes a root Guid at Level200");
        Read<CustomerId>(model, root).Should().Be(customer);
        FluentActions.Invoking(() => Read<CustomerId>(model, Bytes(model, customer.Value)))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);
        FluentActions.Invoking(() => Read<Amount>(model, Bytes(model, 12.5m)))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);

        var level240 = Model(CompatibilityLevel.Level240).AddValueObject<RecordedAt, DateTime>();
        var recorded = RecordedAt.Create(new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc));
        Bytes(level240, recorded).Should().NotEqual(Bytes(level240, recorded.Value));
        Read<RecordedAt>(level240, Bytes(level240, recorded)).Should().Be(recorded);

        var level200 = Model().AddValueObject<CustomerId, Guid>();
        Bytes(level200, customer).Should().Equal(Bytes(level200, customer.Value), "both are at Level200");
    }

    /// <summary>
    /// protobuf-net writes and reads a list of value objects unpacked, whatever the member says, as it does any type a
    /// custom serializer carries: a packed list, which a proto3 writer of another language sends for a repeated number,
    /// cannot be read.
    /// </summary>
    [Fact]
    public void A_list_of_value_objects_is_never_packed_and_a_packed_list_cannot_be_read()
    {
        var model = Model().AddValueObject<Quantity, short>();

        var packed = Bytes(model, new PackedShorts { Values = [1, 2, 3] });
        packed.Should().Equal(0x0A, 0x03, 0x01, 0x02, 0x03);
        Bytes(model, new PackedQuantities { Values = [Quantity.Create(1), Quantity.Create(2), Quantity.Create(3)] })
            .Should().Equal(0x08, 0x01, 0x08, 0x02, 0x08, 0x03);

        FluentActions.Invoking(() => Read<PackedQuantities>(model, packed))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.NotParsable);
    }

    /// <summary>
    /// The two-argument serializer is the strict one at Level200, as the attribute route names it: the same bytes, the
    /// same refusals.
    /// </summary>
    [Fact]
    public void The_two_argument_serializer_is_the_strict_one_at_Level200()
    {
        var shorter = Model();
        var metaType = shorter.Add(typeof(CustomerId), applyDefaultBehaviour: false);
        metaType.SerializerType = typeof(ValueObjectProtoSerializer<CustomerId, Guid>);
        shorter.Add(typeof(Iban), applyDefaultBehaviour: false).SerializerType = typeof(ValueObjectProtoSerializer<Iban, string>);
        var longer = Model().AddValueObject<CustomerId, Guid>().AddValueObject<Iban, string>();
        var customer = CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001"));

        var written = Bytes(shorter, new Holder<CustomerId> { Value = customer });
        written.Should().Equal(Bytes(longer, new Holder<CustomerId> { Value = customer }));
        Read<Holder<CustomerId>>(shorter, written).Value.Should().Be(customer);
        var account = Bytes(shorter, new Holder<Iban?> { Value = Iban.Create(ValidIban) });
        account.Should().Equal(Bytes(longer, new Holder<Iban?> { Value = Iban.Create(ValidIban) }));
        Read<Holder<Iban?>>(shorter, account).Value.Should().Be(Iban.Create(ValidIban));
        FluentActions.Invoking(() => Read<Holder<Iban>>(shorter, Bytes(shorter, new Holder<string> { Value = "FR76" })))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.TooShort);
#pragma warning disable VO0010 // The uninitialized instance is what the write must refuse.
        FluentActions.Invoking(() => Bytes(shorter, new Holder<Iban>()))
            .Should().Throw<ProtoException>().Which.ShouldCarry(ValueObjectErrorCodes.Required);
#pragma warning restore VO0010
        new ValueObjectProtoSerializer<Iban, string>().Features
            .Should().Be(new ValueObjectProtoSerializer<Iban, string, ValueObjectProtoProfiles.StrictLevel200>().Features);
    }

    [Fact]
    public void The_profiles_the_package_ships_name_each_trust_at_each_level()
    {
        Profile<ValueObjectProtoProfiles.StrictLevel200>().Should().Be((false, CompatibilityLevel.Level200));
        Profile<ValueObjectProtoProfiles.StrictLevel240>().Should().Be((false, CompatibilityLevel.Level240));
        Profile<ValueObjectProtoProfiles.StrictLevel300>().Should().Be((false, CompatibilityLevel.Level300));
        Profile<ValueObjectProtoProfiles.TrustedLevel200>().Should().Be((true, CompatibilityLevel.Level200));
        Profile<ValueObjectProtoProfiles.TrustedLevel240>().Should().Be((true, CompatibilityLevel.Level240));
        Profile<ValueObjectProtoProfiles.TrustedLevel300>().Should().Be((true, CompatibilityLevel.Level300));

        static (bool Trusted, CompatibilityLevel Level) Profile<TProfile>()
            where TProfile : IValueObjectProtoProfile
            => (TProfile.Trusted, TProfile.CompatibilityLevel);
    }

    /// <summary>
    /// A profile of the application's own fixes the trust and the level as the shipped ones do: here trusted, and a
    /// <see cref="Guid"/> written as text, at Level300.
    /// </summary>
    [Fact]
    public void A_profile_of_the_application_s_own_fixes_the_trust_and_the_level()
    {
        var model = Model();
        model.Add(typeof(CustomerId), applyDefaultBehaviour: false).SerializerType =
            typeof(ValueObjectProtoSerializer<CustomerId, Guid, TrustedTextProfile>);

        var written = Bytes(model, new Holder<CustomerId> { Value = CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001")) });
        written.Should().Equal(Bytes(Model(CompatibilityLevel.Level300), new Holder<Guid> { Value = Guid.Parse("0192f4a0-0000-7000-8000-000000000001") }));
        Read<Holder<CustomerId>>(model, Bytes(model, new Holder<string> { Value = Guid.Empty.ToString() })).Value.Value
            .Should().Be(Guid.Empty, "the profile trusts the bytes");
    }

    /// <summary>
    /// A serializer named on a value object over a type protobuf-net cannot write refuses it by name, from whichever of its
    /// members protobuf-net calls first.
    /// </summary>
    [Fact]
    public void A_serializer_over_a_type_protobuf_net_cannot_write_refuses_it_by_name()
    {
        const string Message = "'LedgerBalance' is a value object over Int128, which protobuf-net has no inbuilt serializer for at Level200.";
        var shorter = new ValueObjectProtoSerializer<LedgerBalance, Int128>();
        var longer = new ValueObjectProtoSerializer<LedgerBalance, Int128, ValueObjectProtoProfiles.StrictLevel200>();

        FluentActions.Invoking(() => shorter.Features).Should().Throw<NotSupportedException>().WithMessage(Message);
        FluentActions.Invoking(() => longer.Features).Should().Throw<NotSupportedException>().WithMessage(Message);
        Thrown(() => ReadDirectly<LedgerBalance>(shorter)).Should().BeOfType<NotSupportedException>().Which.Message.Should().Be(Message);
        Thrown(() => ReadDirectly<LedgerBalance>(longer)).Should().BeOfType<NotSupportedException>().Which.Message.Should().Be(Message);
        Thrown(() => WriteDirectly<LedgerBalance>(shorter, LedgerBalance.Create(1))).Should().BeOfType<NotSupportedException>().Which.Message.Should().Be(Message);
        Thrown(() => WriteDirectly<LedgerBalance>(longer, LedgerBalance.Create(1))).Should().BeOfType<NotSupportedException>().Which.Message.Should().Be(Message);
    }

    /// <summary>
    /// protobuf-net never hands a serializer a <see langword="null"/> to write, which it writes nothing for: the nullable
    /// form refuses one it is handed directly.
    /// </summary>
    [Fact]
    public void The_nullable_form_refuses_a_null_it_is_handed_directly()
    {
        Thrown(() => WriteDirectly<Iban?>(new ValueObjectProtoSerializer<Iban, string>(), null))
            .Should().BeOfType<ArgumentNullException>().Which.ParamName.Should().Be("value");
        Thrown(() => WriteDirectly<Iban?>(new ValueObjectProtoSerializer<Iban, string, ValueObjectProtoProfiles.StrictLevel240>(), null))
            .Should().BeOfType<ArgumentNullException>().Which.ParamName.Should().Be("value");
    }

    /// <summary>Reads a value through a serializer from no bytes, with no model around it.</summary>
    private static T ReadDirectly<T>(ISerializer<T> serializer)
    {
        var state = ProtoReader.State.Create(ReadOnlyMemory<byte>.Empty, Model());
        try
        {
            return serializer.Read(ref state, default!);
        }
        finally
        {
            state.Dispose();
        }
    }

    /// <summary>Writes a value through a serializer, with no model around it.</summary>
    private static void WriteDirectly<T>(ISerializer<T> serializer, T value)
    {
        var state = ProtoWriter.State.Create(Stream.Null, Model());
        try
        {
            serializer.Write(ref state, value);
        }
        finally
        {
            state.Dispose();
        }
    }

    /// <summary>Runs an action and returns what it threw.</summary>
    private static Exception? Thrown(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>Trusted, at Level300: a profile of the application's own.</summary>
    private sealed class TrustedTextProfile : IValueObjectProtoProfile
    {
        public static bool Trusted => true;

        public static CompatibilityLevel CompatibilityLevel => CompatibilityLevel.Level300;
    }

    /// <summary>A stream that hands out its bytes one at a time, and is found disposed once it has handed out some.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="served">How many it hands out before it is found disposed.</param>
    private sealed class DisposedMidway(byte[] bytes, int served) : Stream
    {
        public int Served { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(Served == served, this);
            if (count == 0 || Served == bytes.Length)
            {
                return 0;
            }

            buffer[offset] = bytes[Served++];
            return 1;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

/// <summary>An order holding value objects, alone, nullable, in a list and in a dictionary.</summary>
[ProtoContract]
public sealed class ProtoOrder
{
    [ProtoMember(1)]
    public Iban Account { get; set; }

    [ProtoMember(2)]
    public Quantity Quantity { get; set; }

    [ProtoMember(3)]
    public Quantity? Discount { get; set; }

    [ProtoMember(4)]
    public List<Quantity>? Lines { get; set; }

    [ProtoMember(5)]
    public Dictionary<CountryCode, Quantity>? Stock { get; set; }
}

/// <summary>The same order over the primitives.</summary>
[ProtoContract]
public sealed class PlainProtoOrder
{
    [ProtoMember(1)]
    public string? Account { get; set; }

    [ProtoMember(2)]
    public short Quantity { get; set; }

    [ProtoMember(3)]
    public short? Discount { get; set; }

    [ProtoMember(4)]
    public List<short>? Lines { get; set; }

    [ProtoMember(5)]
    public Dictionary<string, short>? Stock { get; set; }
}

/// <summary>A list of nullable value objects that keeps its nulls.</summary>
[ProtoContract]
public sealed class WrappedQuantities
{
    [ProtoMember(1)]
    [NullWrappedValue]
    public List<Quantity?>? Values { get; set; }
}

/// <summary>A list of nullable page numbers that keeps its nulls: a page number refuses its default, zero.</summary>
[ProtoContract]
public sealed class WrappedPages
{
    [ProtoMember(1)]
    [NullWrappedValue]
    public List<PageNumber?>? Values { get; set; }
}

/// <summary>A list of nullable integers that keeps its nulls.</summary>
[ProtoContract]
public sealed class WrappedShorts
{
    [ProtoMember(1)]
    [NullWrappedValue]
    public List<short?>? Values { get; set; }
}

/// <summary>
/// The message over the primitives that <see cref="ProtoSpare"/>'s list of <see cref="ProtoSku"/> keeps its nulls as.
/// </summary>
[ProtoContract]
public sealed class PlainProtoSpare
{
    [ProtoMember(2)]
    [NullWrappedValue]
    public List<string?>? Skus { get; set; }
}

/// <summary>Value objects on members that declare a <see cref="DataFormat"/>, which never reaches them.</summary>
[ProtoContract]
public sealed class ProtoFormatted
{
    [ProtoMember(1, DataFormat = DataFormat.ZigZag)]
    public Quantity Quantity { get; set; }

    [ProtoMember(2, DataFormat = DataFormat.FixedSize)]
    public PageNumber Page { get; set; }

#pragma warning disable CS0618 // Obsolete, CompatibilityLevel replacing it, and still a format a member may declare.
    [ProtoMember(3, DataFormat = DataFormat.WellKnown)]
    public RecordedAt Recorded { get; set; }
#pragma warning restore CS0618
}

/// <summary>The same message over the primitives, which the formats reach.</summary>
[ProtoContract]
public sealed class PlainProtoFormatted
{
    [ProtoMember(1, DataFormat = DataFormat.ZigZag)]
    public short Quantity { get; set; }

    [ProtoMember(2, DataFormat = DataFormat.FixedSize)]
    public int Page { get; set; }

#pragma warning disable CS0618 // Obsolete, CompatibilityLevel replacing it, and still a format a member may declare.
    [ProtoMember(3, DataFormat = DataFormat.WellKnown)]
    public DateTime Recorded { get; set; }
#pragma warning restore CS0618
}

/// <summary>The same message over the primitives, without the formats: what <see cref="ProtoFormatted"/> writes.</summary>
[ProtoContract]
public sealed class PlainProtoUnformatted
{
    [ProtoMember(1)]
    public short Quantity { get; set; }

    [ProtoMember(2)]
    public int Page { get; set; }

    [ProtoMember(3)]
    public DateTime Recorded { get; set; }
}

/// <summary>A list of value objects declared packed.</summary>
[ProtoContract]
public sealed class PackedQuantities
{
    [ProtoMember(1, IsPacked = true)]
    public List<Quantity>? Values { get; set; }
}

/// <summary>A list of integers declared packed.</summary>
[ProtoContract]
public sealed class PackedShorts
{
    [ProtoMember(1, IsPacked = true)]
    public List<short>? Values { get; set; }
}
