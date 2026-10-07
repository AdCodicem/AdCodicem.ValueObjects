using AdCodicem.ValueObjects.MessagePack;
using MessagePack;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// What a value object is written as through MessagePack, beside what the bare value it carries is written as, for the
/// samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class MessagePackParity
{
    /// <summary>
    /// Checks, under MessagePack's standard options and under options whose <c>Native*</c> resolvers write a
    /// <see cref="Guid"/>, a <see cref="decimal"/> and a <see cref="DateTime"/> in binary, that the formatter the options
    /// resolve for a value object writes exactly the bytes the options write for the value it carries, and reads them back
    /// as the value the primitive reads back, strict and trusted.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="value">An instance.</param>
    public static void Check<TSelf, TValue>(TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = typeof(TSelf).Name;
        foreach (var (wire, options) in new[] { ("standard", MessagePackWire.Standard), ("native", MessagePackWire.Native) })
        {
            var strict = options.WithValueObjects();
            var trusted = options.WithValueObjects(trusted: true);
            strict.Resolver.GetFormatter<TSelf>().Should().BeOfType<ValueObjectFormatter<TSelf, TValue>>();

            var written = MessagePackSerializer.Serialize(value, strict);
            written.Should().Equal(
                MessagePackSerializer.Serialize(value.Value, options),
                "{0} is written as its {1} is, under the {2} options",
                name,
                typeof(TValue).Name,
                wire);

            var expected = MessagePackSerializer.Deserialize<TValue>(written, options);
            MessagePackSerializer.Deserialize<TSelf>(written, strict).Value
                .Should().Be(expected, "{0} reads back what its {1} reads, under the {2} options", name, typeof(TValue).Name, wire);
            MessagePackSerializer.Deserialize<TSelf>(written, trusted).Value
                .Should().Be(expected, "a trusted {0} reads the same, under the {1} options", name, wire);
        }
    }
}
