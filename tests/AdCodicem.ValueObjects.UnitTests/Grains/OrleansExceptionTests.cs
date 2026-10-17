using AdCodicem.ValueObjects.Fixtures.Grains;
using AdCodicem.ValueObjects.Orleans;
using Orleans.Serialization.Codecs;
using static AdCodicem.ValueObjects.UnitTests.Grains.OrleansWire;

namespace AdCodicem.ValueObjects.UnitTests.Grains;

/// <summary>
/// The codec both registrations add for a <see cref="ValueObjectException"/>, which Orleans would otherwise refuse to
/// write: its message, its stack trace, its code and its value object type travel, never the value it refused, and the
/// type only where the reader loads and allows it.
/// </summary>
public sealed class OrleansExceptionTests
{
    [Fact]
    public void A_value_object_exception_travels_with_its_code_and_type_and_without_its_value()
    {
        var thrown = Thrown(static () => GrainQuantity.Create(500));

        foreach (var read in new[] { RoundTrip<Exception>(thrown), RoundTrip(thrown) })
        {
            var exception = read.Should().BeOfType<ValueObjectException>().Which;
            exception.Message.Should().Be(thrown.Message);
            exception.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);
            exception.ValueObjectType.Should().Be<GrainQuantity>();
            exception.AttemptedValue.Should().BeNull("the refused value never leaves the process");
            exception.StackTrace.Should().Contain(nameof(Thrown), "the writer's stack trace is kept as a remote one");
            ValueObjectErrors.TryGetCode(exception, out var code).Should().BeTrue();
            code.Should().Be(ValueObjectErrorCodes.OutOfRange);
        }

        thrown.AttemptedValue.Should().Be(500, "the exception thrown keeps it");
    }

    [Fact]
    public void A_subclass_arrives_as_a_value_object_exception()
    {
        var read = RoundTrip<Exception>(new GrainRefusal("refused the application's way"));

        var exception = read.Should().BeOfType<ValueObjectException>().Which;
        exception.Message.Should().Be("refused the application's way");
        exception.ErrorCode.Should().Be("fixture.custom");
        exception.ValueObjectType.Should().Be<GrainQuantity>();
        exception.AttemptedValue.Should().BeNull();
    }

    /// <summary>
    /// A member typed as a subclass is refused on write, with no code, since the subclass would arrive as a
    /// <see cref="ValueObjectException"/>, which the reader could not assign to it; typed as the library's exception, the
    /// subclass travels.
    /// </summary>
    [Fact]
    public void A_member_typed_as_a_subclass_is_refused_on_write()
    {
        var refusal = FluentActions.Invoking(() => Strict.SerializeToArray(new GrainHolder<GrainRefusal> { Value = new GrainRefusal("refused") }))
            .Should().Throw<NotSupportedException>().Which;

        refusal.Message.Should().Be(
            "A member, an argument or a result typed GrainRefusal cannot cross Orleans: a subclass of ValueObjectException "
            + "arrives as ValueObjectException, so type it ValueObjectException or Exception.");
        ValueObjectErrors.TryGetCode(refusal, out _).Should().BeFalse("a misuse carries no code");
        Strict.Read<GrainHolder<ValueObjectException?>>(Strict.SerializeToArray(new GrainHolder<ValueObjectException?> { Value = new GrainRefusal("refused") }))
            .Value.Should().BeOfType<ValueObjectException>().Which.ErrorCode.Should().Be("fixture.custom");
    }

    [Fact]
    public void An_exception_never_thrown_carries_no_stack_trace()
    {
        var read = RoundTrip(new ValueObjectException("no type", valueObjectType: null, errorCode: null, attemptedValue: null));

        read!.StackTrace.Should().BeNull();
        read.ValueObjectType.Should().BeNull();
        read.ErrorCode.Should().BeNull();
        read.Message.Should().Be("no type");
    }

    /// <summary>
    /// The type is restored only where it is a value object the reader loads and allows: otherwise the exception arrives
    /// with no type, its code intact.
    /// </summary>
    [Fact]
    public void The_value_object_type_is_restored_only_where_the_reader_loads_and_allows_it()
    {
        RoundTrip(new ValueObjectException("not one", typeof(string), "code", attemptedValue: null))!.ValueObjectType
            .Should().BeNull("a type that is no value object is not restored");

        var thrown = Thrown(static () => GrainQuantity.Create(500));
        var skusOnly = Build(static builder => builder.AddValueObject<GrainSku, string>());
        var written = Strict.SerializeToArray<Exception>(thrown);
        var read = skusOnly.Read<Exception>(written).Should().BeOfType<ValueObjectException>().Which;
        read.ValueObjectType.Should().BeNull("the reader's type filters refuse GrainQuantity");
        read.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange);

        // A writer that does not allow the type writes the exception all the same.
        Strict.Read<Exception>(skusOnly.SerializeToArray<Exception>(thrown)).Should().BeOfType<ValueObjectException>()
            .Which.ValueObjectType.Should().Be<GrainQuantity>();
    }

    /// <summary>
    /// A type name the reader cannot load, cannot parse, or that is cut short leaves the exception without a type.
    /// </summary>
    /// <param name="typeName">The name written.</param>
    [Theory]
    [InlineData("Nope.Missing,Nope")]
    [InlineData("]]][[")]
    [InlineData("")]
    public void A_type_name_the_reader_cannot_load_leaves_the_exception_without_a_type(string typeName)
    {
        var read = Strict.Read<Exception>(Exception(typeName, extra: false)).Should().BeOfType<ValueObjectException>().Which;

        read.ValueObjectType.Should().BeNull();
        read.ErrorCode.Should().Be("value_object.crafted");
        read.Message.Should().Be("crafted");
    }

    [Fact]
    public void A_field_a_later_version_writes_is_skipped()
    {
        var read = Strict.Read<Exception>(Exception(typeName: null, extra: true)).Should().BeOfType<ValueObjectException>().Which;

        read.ErrorCode.Should().Be("value_object.crafted");
        read.ValueObjectType.Should().BeNull();
    }

    [Fact]
    public void An_exception_written_without_a_message_reads_with_an_empty_one()
    {
        var bytes = Craft((ref writer) =>
        {
            writer.WriteStartObject(0, typeof(Exception), typeof(ValueObjectException));
            StringCodec.WriteField(ref writer, 2, "value_object.crafted");
            writer.WriteEndObject();
        });

        var read = Strict.Read<Exception>(bytes).Should().BeOfType<ValueObjectException>().Which;
        read.Message.Should().BeEmpty();
        read.ErrorCode.Should().Be("value_object.crafted");
        read.StackTrace.Should().BeNull();
    }

    [Fact]
    public void A_missing_exception_reads_back_as_null()
    {
        Strict.Read<GrainHolder<ValueObjectException?>>(Strict.SerializeToArray(new GrainHolder<ValueObjectException?> { Value = null })).Value
            .Should().BeNull();
    }

    [Fact]
    public void The_same_exception_twice_is_written_twice_as_a_value()
    {
        var thrown = Thrown(static () => GrainQuantity.Create(500));

        var read = Strict.Read<GrainHolder<Exception[]>>(Strict.SerializeToArray(new GrainHolder<Exception[]> { Value = [thrown, thrown] })).Value;

        read.Should().HaveCount(2).And.AllBeOfType<ValueObjectException>();
        read[0].Should().NotBeSameAs(read[1]);
        read.Cast<ValueObjectException>().Should().AllSatisfy(static exception => exception.ErrorCode.Should().Be(ValueObjectErrorCodes.OutOfRange));
    }

    [Fact]
    public void Without_the_package_Orleans_refuses_to_write_a_value_object_exception()
    {
        FluentActions.Invoking(() => Bare.SerializeToArray<Exception>(Thrown(static () => GrainQuantity.Create(500))))
            .Should().Throw<Exception>().Which.Message.Should().Contain(nameof(ValueObjectException));
    }

    private static ValueObjectException Thrown(Action create)
    {
        try
        {
            create();
        }
        catch (ValueObjectException exception)
        {
            return exception;
        }

        throw new InvalidOperationException("Nothing was refused.");
    }

    private static T RoundTrip<T>(T value) => Strict.Deserialize<T>(Strict.SerializeToArray(value))!;

    private static byte[] Exception(string? typeName, bool extra)
        => Craft((ref writer) =>
        {
            writer.WriteStartObject(0, typeof(Exception), typeof(ValueObjectException));
            StringCodec.WriteField(ref writer, 0, "crafted");
            StringCodec.WriteField(ref writer, 2, "value_object.crafted");
            StringCodec.WriteField(ref writer, 1, typeName);
            if (extra)
            {
                StringCodec.WriteField(ref writer, 1, "extra");
            }

            writer.WriteEndObject();
        });
}
