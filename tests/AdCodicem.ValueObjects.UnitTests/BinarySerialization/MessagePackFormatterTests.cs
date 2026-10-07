using AdCodicem.ValueObjects.MessagePack;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using AdCodicem.ValueObjects.UnitTests.GeneratedSurface;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using static AdCodicem.ValueObjects.UnitTests.BinarySerialization.MessagePackWire;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// <see cref="ValueObjectFormatter{TSelf, TValue}"/>: a value object written as the bare value the options' formatter of
/// its underlying type writes, read back through its rules, and refused on write when the type rejects it.
/// </summary>
/// <remarks>
/// Most formatters here are built by hand and driven directly, as MessagePack drives them, so that the exception they
/// throw is seen as it is; the serializer, which wraps it, is driven where the wrapping is the point. The resolver and the
/// options are <see cref="MessagePackResolverTests"/>'s, and SignalR is <see cref="SignalRMessagePackTests"/>'s.
/// </remarks>
public sealed class MessagePackFormatterTests
{
    private const string ValidIban = "FR7630006000011234567890189";

    /// <summary>
    /// Every value object of the domain, over each of the underlying types, is written as the bare value it carries is,
    /// and reads back what that value reads back.
    /// </summary>
    /// <param name="type">The name of the sample.</param>
    [Theory]
    [MemberData(nameof(Samples.Names), MemberType = typeof(Samples))]
    public void Every_value_object_is_written_as_the_value_it_carries(string type)
        => Samples.All[type].RoundTripsThroughMessagePackAsItsUnderlyingValue();

    [Fact]
    public void A_value_read_goes_through_the_rules_and_is_normalized()
    {
        Read(new ValueObjectFormatter<Iban, string>(), Bytes("fr76 3000 6000 0112 3456 7890 189"), Standard)
            .Should().Be(Iban.Create(ValidIban));
    }

    [Fact]
    public void A_trusted_read_neither_validates_nor_normalizes()
    {
        var formatter = new ValueObjectFormatter<Quantity, short>(trusted: true);

        Read(formatter, Bytes((short)5000), Standard).Value.Should().Be(5000);
        Read(new ValueObjectFormatter<Iban, string>(trusted: true), Bytes("fr76 3000"), Standard).Value.Should().Be("fr76 3000");
    }

    [Fact]
    public void A_formatter_is_strict_unless_it_is_told_to_trust()
    {
        new ValueObjectFormatter<Iban, string>().Trusted.Should().BeFalse();
        new ValueObjectFormatter<Iban, string>(trusted: true).Trusted.Should().BeTrue();
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
    }

    [Fact]
    public void A_value_object_that_refuses_without_a_reason_is_refused_as_not_parsable()
    {
        var formatter = new ValueObjectFormatter<HandWrittenId<MuteProfile>, string>();

        var refusal = FluentActions.Invoking(() => Read(formatter, Bytes("mute_anything"), Standard))
            .Should().Throw<MessagePackSerializationException>().Which;

        refusal.Message.Should().StartWith("The value read is not a valid HandWrittenId`1: ");
        Code(refusal).Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    [Fact]
    public void A_nil_is_refused_as_required_whatever_the_trust()
    {
        var nil = Bytes<string?>(null);

        foreach (var trusted in new[] { false, true })
        {
            Nil(() => Read(new ValueObjectFormatter<Iban, string>(trusted), nil, Standard), "Iban");
            Nil(() => Read(new ValueObjectFormatter<PageNumber, int>(trusted), nil, Standard), "PageNumber");
        }

        static void Nil(Action read, string name)
        {
            var refusal = read.Should().Throw<MessagePackSerializationException>().Which;
            refusal.Message.Should().Be($"A nil cannot be read as {name}; declare the member as a nullable {name}? instead.");
            refusal.InnerException.Should().BeNull();
            Code(refusal).Should().Be(ValueObjectErrorCodes.Required);
        }
    }

    [Fact]
    public void A_nullable_value_object_reads_a_nil_as_null_and_a_value_as_its_value()
    {
        MessagePackSerializer.Deserialize<Iban?>(Bytes<string?>(null), Strict).Should().BeNull();
        MessagePackSerializer.Deserialize<Iban?>(Bytes(ValidIban), Strict).Should().Be(Iban.Create(ValidIban));
        MessagePackSerializer.Serialize<Iban?>(null, Strict).Should().Equal(Bytes<string?>(null));
        MessagePackSerializer.Serialize<Iban?>(Iban.Create(ValidIban), Strict).Should().Equal(Bytes(ValidIban));
    }

    /// <summary>
    /// A value the formatter of the underlying type cannot read is refused as not parsable, without the formatter's own
    /// exception, whose message may quote the value: one case for each exception the formatters and the reader throw,
    /// and bytes that end where the value should start.
    /// </summary>
    [Fact]
    public void A_value_its_underlying_formatter_cannot_read_is_refused_as_not_parsable_without_quoting_it()
    {
        // MessagePackSerializationException: a string where a number is expected.
        Unreadable(new ValueObjectFormatter<Quantity, short>(), Bytes("secret"), "Quantity", "Int16");

        // OverflowException: beyond the range of a byte and of a char.
        Unreadable(new ValueObjectFormatter<Score, byte>(), Bytes(300), "Score", "Byte");
        Unreadable(new ValueObjectFormatter<Grade, char>(), Bytes(100_000), "Grade", "Char");

        // ArgumentOutOfRangeException, "Actual value was 2147483647.": a day number beyond DateOnly's range.
        Unreadable(new ValueObjectFormatter<EffectiveDate, DateOnly>(), Bytes(int.MaxValue), "EffectiveDate", "DateOnly");

        // ArgumentException, from DateTime.FromBinary: under NativeDateTimeResolver, a 64-bit form beyond DateTime's range.
        Unreadable(new ValueObjectFormatter<RecordedAt, DateTime>(), Bytes(long.MaxValue), "RecordedAt", "DateTime", Native);

        // UriFormatException, a FormatException: text no Uri parses, read into a value object written by hand over Uri.
        Unreadable(new ValueObjectFormatter<HandWrittenLink, Uri>(), Bytes("http://[::1"), "HandWrittenLink", "Uri");
        Unreadable(new ValueObjectFormatter<HandWrittenLink, Uri>(), Bytes("http://host:99999/"), "HandWrittenLink", "Uri");

        // EndOfStreamException: an integer where a timestamp's extension header is expected, and truncated text.
        Unreadable(new ValueObjectFormatter<RecordedAt, DateTime>(), Bytes(5), "RecordedAt", "DateTime");
        Unreadable(new ValueObjectFormatter<Iban, string>(), [0xA5, 0x41], "Iban", "String");

        // No byte at all where the value should start, which no nil check can read either.
        Unreadable(new ValueObjectFormatter<Iban, string>(), [], "Iban", "String");
        Unreadable(new ValueObjectFormatter<PageNumber, int>(), [], "PageNumber", "Int32");

        static void Unreadable<TSelf, TValue>(
            ValueObjectFormatter<TSelf, TValue> formatter,
            byte[] bytes,
            string name,
            string over,
            MessagePackSerializerOptions? options = null)
            where TSelf : struct, IValueObject<TSelf, TValue>
        {
            foreach (var trusted in new[] { formatter, new ValueObjectFormatter<TSelf, TValue>(trusted: true) })
            {
                var refusal = FluentActions.Invoking(() => Read(trusted, bytes, options ?? Standard))
                    .Should().Throw<MessagePackSerializationException>().Which;
                refusal.Message.Should().Be($"The MessagePack value read cannot be read as {name}, a value object over {over}.");
                refusal.InnerException.Should().BeNull();
                Code(refusal).Should().Be(ValueObjectErrorCodes.NotParsable);
            }
        }
    }

    /// <summary>
    /// Bytes cut between two values, before a value object an array or an object still expects, are refused as not
    /// parsable, inside MessagePack's own exception, as bytes cut inside the value are; an array announcing more
    /// elements than bytes remain is refused by MessagePack's reader, before any value object is read.
    /// </summary>
    [Fact]
    public void Bytes_cut_before_a_value_object_are_refused_as_not_parsable()
    {
        // An array announcing two IBANs, cut after the first.
        Cut<Iban[]>([.. Bytes(new[] { ValidIban, ValidIban }).AsSpan(0, 1 + Bytes(ValidIban).Length)], "Iban");

        // An object of value objects, cut after the array header and its first member, before its IBAN.
        var customer = Guid.NewGuid();
        var order = Bytes(new PrimitiveOrder { Customer = customer, Account = ValidIban, Quantity = 3 });
        Cut<ValueObjectOrder>([.. order.AsSpan(0, 1 + Bytes(customer).Length)], "Iban");

        // Two quantities announced, one byte left: the array header is refused, with no code, as for an array of shorts.
        byte[] announced = [0x92, 0x01];
        foreach (var options in new[] { Strict, Trusting })
        {
            var thrown = FluentActions.Invoking(() => MessagePackSerializer.Deserialize<Quantity[]>(announced, options, TestContext.Current.CancellationToken))
                .Should().Throw<MessagePackSerializationException>().Which;
            thrown.InnerException.Should().BeOfType<EndOfStreamException>();
            Code(thrown).Should().BeNull();
        }

        static void Cut<T>(byte[] bytes, string name)
        {
            foreach (var options in new[] { Strict, Trusting })
            {
                var thrown = FluentActions.Invoking(() => MessagePackSerializer.Deserialize<T>(bytes, options, TestContext.Current.CancellationToken))
                    .Should().Throw<MessagePackSerializationException>().Which;
                thrown.Message.Should().Be($"Failed to deserialize {typeof(T).FullName} value.");
                thrown.InnerException.Should().BeOfType<MessagePackSerializationException>()
                    .Which.Message.Should().StartWith($"The MessagePack value read cannot be read as {name}, ");
                Code(thrown).Should().Be(ValueObjectErrorCodes.NotParsable);
            }
        }
    }

    [Fact]
    public void An_exception_that_is_no_refusal_propagates_as_it_is()
    {
        var options = Standard.WithResolver(
            CompositeResolver.Create([new ThrowingStringFormatter()], [StandardResolver.Instance]));

        var thrown = FluentActions.Invoking(() => Read(new ValueObjectFormatter<Iban, string>(), Bytes(ValidIban), options))
            .Should().Throw<InvalidOperationException>().Which;

        thrown.Message.Should().Be(ThrowingStringFormatter.Message);
        Code(thrown).Should().BeNull();
    }

    [Fact]
    public void Options_without_a_formatter_for_the_underlying_type_are_a_misconfiguration_both_ways()
    {
        var options = Standard.WithResolver(CompositeResolver.Create(ValueObjectResolver.Instance));
        var formatter = new ValueObjectFormatter<Iban, string>();

        Code(FluentActions.Invoking(() => Read(formatter, Bytes(ValidIban), options))
            .Should().Throw<FormatterNotRegisteredException>().Which).Should().BeNull();
        Code(FluentActions.Invoking(() => Write(formatter, Iban.Create(ValidIban), options))
            .Should().Throw<FormatterNotRegisteredException>().Which).Should().BeNull();
    }

    /// <summary>
    /// What a deserializer, an array or a message initializer hands out before any rule ran is refused on write when the
    /// type rejects it, alone, as a dictionary key and inside a nullable value object, before anything is written.
    /// </summary>
    [Fact]
    public void An_uninitialized_value_the_type_rejects_is_refused_on_write()
    {
        // The trust governs reads alone: a trusted formatter refuses the same writes.
        foreach (var options in new[] { Strict, Trusting })
        {
#pragma warning disable VO0010 // The uninitialized instance is what the write must refuse.
            RefusedWrite(() => MessagePackSerializer.Serialize(default(PageNumber), options), "PageNumber", ValueObjectErrorCodes.OutOfRange);
            RefusedWrite(() => MessagePackSerializer.Serialize(default(Iban), options), "Iban", ValueObjectErrorCodes.Required);
            RefusedWrite(
                () => MessagePackSerializer.Serialize(new Dictionary<Iban, int> { [default] = 1 }, options),
                "Iban",
                ValueObjectErrorCodes.Required);
            RefusedWrite(
                () => MessagePackSerializer.Serialize<PageNumber?>(default(PageNumber), options),
                "PageNumber",
                ValueObjectErrorCodes.OutOfRange);
#pragma warning restore VO0010
        }

        static void RefusedWrite(Func<byte[]> write, string name, string code)
        {
            var thrown = write.Should().Throw<MessagePackSerializationException>().Which;
            thrown.Message.Should().StartWith("Failed to serialize ");
            var refusal = thrown.InnerException.Should().BeOfType<MessagePackSerializationException>().Which;
            refusal.Message.Should().StartWith($"The value to write is not a valid {name}: ");
            Code(refusal).Should().Be(code);
            Code(thrown).Should().Be(code);
        }
    }

    [Fact]
    public void An_uninitialized_value_the_type_accepts_is_written()
    {
#pragma warning disable VO0010 // Quantity accepts its zero, which an uninitialized instance holds.
        Write(new ValueObjectFormatter<Quantity, short>(), default, Standard).Should().Equal(Bytes((short)0));
#pragma warning restore VO0010
    }

    [Fact]
    public void A_value_read_on_trust_is_written_back_as_it_was_read()
    {
        var trusted = Read(new ValueObjectFormatter<Quantity, short>(trusted: true), Bytes((short)5000), Standard);

        Write(new ValueObjectFormatter<Quantity, short>(), trusted, Standard).Should().Equal(Bytes((short)5000));
    }

    [Fact]
    public void Options_are_required_both_ways()
    {
        var formatter = new ValueObjectFormatter<Iban, string>();

        FluentActions.Invoking(() => Write(formatter, Iban.Create(ValidIban), null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("options");
        FluentActions.Invoking(() => Read(formatter, Bytes(ValidIban), null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    [Fact]
    public void A_refusal_carries_its_code_through_the_exception_MessagePack_wraps_it_in()
    {
        var thrown = FluentActions.Invoking(() => MessagePackSerializer.Deserialize<Quantity>(Bytes((short)1001), Strict))
            .Should().Throw<MessagePackSerializationException>().Which;

        thrown.Message.Should().Be($"Failed to deserialize {typeof(Quantity).FullName} value.");
        thrown.InnerException.Should().BeOfType<MessagePackSerializationException>()
            .Which.Message.Should().StartWith("The value read is not a valid Quantity: ");
        Code(thrown).Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    /// <summary>
    /// A value object written by hand over a type none of the generator's 22 is, which nothing registered, goes through
    /// the formatter MessagePack holds for that type.
    /// </summary>
    [Fact]
    public void A_value_object_written_by_hand_goes_through_the_formatter_of_its_underlying_type()
    {
        var link = HandWrittenLink.Create(new Uri("https://example.com/a"));

        var written = MessagePackSerializer.Serialize(link, Strict);

        written.Should().Equal(Bytes(new Uri("https://example.com/a")));
        MessagePackSerializer.Deserialize<HandWrittenLink>(written, Strict).Should().Be(link);
        Code(FluentActions.Invoking(() => MessagePackSerializer.Deserialize<HandWrittenLink>(Bytes(new Uri("/relative", UriKind.Relative)), Strict))
            .Should().Throw<MessagePackSerializationException>().Which).Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    /// <summary>
    /// A construction of a generic value object nothing registered, over a type private to this test, is described by
    /// reflection and served like any other.
    /// </summary>
    [Fact]
    public void A_construction_of_a_generic_value_object_nothing_registered_is_served()
    {
        var written = MessagePackSerializer.Serialize(Reference<MessagePackFormatterTests>.Create("po-1"), Strict);

        written.Should().Equal(Bytes("PO-1"));
        MessagePackSerializer.Deserialize<Reference<MessagePackFormatterTests>>(Bytes(" po-2 "), Strict).Value.Should().Be("PO-2");
        Code(FluentActions.Invoking(() => MessagePackSerializer.Deserialize<Reference<MessagePackFormatterTests>>(Bytes(new string('x', 13)), Strict))
            .Should().Throw<MessagePackSerializationException>().Which).Should().Be(ValueObjectErrorCodes.TooLong);
    }

    /// <summary>
    /// Reads a value the type refuses through a strict formatter, and checks the refusal against the rule
    /// <c>TryCreate</c> reports for it.
    /// </summary>
    private static void RefusedRead<TSelf, TValue>(TValue raw, string code)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        TSelf.TryCreate(raw, out _, out var validation).Should().BeFalse();
        validation.ErrorCode.Should().Be(code);

        var refusal = FluentActions.Invoking(() => Read(new ValueObjectFormatter<TSelf, TValue>(), Bytes(raw), Standard))
            .Should().Throw<MessagePackSerializationException>().Which;

        refusal.Message.Should().Be($"The value read is not a valid {typeof(TSelf).Name}: {validation.ErrorMessage}");
        if (raw?.ToString() is { Length: > 0 } text)
        {
            refusal.Message.Should().NotContain(text);
        }

        refusal.InnerException.Should().BeNull();
        Code(refusal).Should().Be(code);
    }

    /// <summary>
    /// A formatter of <see cref="string"/> that throws what no refusal throws.
    /// </summary>
    internal sealed class ThrowingStringFormatter : IMessagePackFormatter<string?>
    {
        public const string Message = "A formatter of the application failed.";

        public void Serialize(ref MessagePackWriter writer, string? value, MessagePackSerializerOptions options)
            => throw new InvalidOperationException(Message);

        public string? Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
            => throw new InvalidOperationException(Message);
    }
}
