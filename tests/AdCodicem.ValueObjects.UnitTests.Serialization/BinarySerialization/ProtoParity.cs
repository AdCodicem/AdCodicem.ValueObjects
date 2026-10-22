using AdCodicem.ValueObjects.ProtobufNet;
using ProtoBuf;
using ProtoBuf.Meta;
using static AdCodicem.ValueObjects.UnitTests.BinarySerialization.ProtoWire;

namespace AdCodicem.ValueObjects.UnitTests.BinarySerialization;

/// <summary>
/// What a value object is written as through protobuf-net, beside what the bare value it carries is written as, for the
/// samples of <see cref="GeneratedSurface.Samples"/>.
/// </summary>
public static class ProtoParity
{
    /// <summary>
    /// Checks, at each compatibility level, strict and trusted, that a model the value object is registered on writes a
    /// message holding it, nullable or in a list, an array or a set, exactly as it writes the message over the value it
    /// carries, reads the bytes back as the value the primitive reads back, and names the member in its <c>.proto</c>
    /// export as it names the primitive's, imports included; that a root value object writes what the root primitive
    /// writes wherever the level changes nothing for the primitive, protobuf-net writing a root primitive at
    /// <c>Level200</c> whatever the model's level, and differs from it elsewhere; and, for an underlying type protobuf-net
    /// cannot write, that the value object is refused by name and left out of a whole registration.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="value">An instance.</param>
    public static void Check<TSelf, TValue>(TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = typeof(TSelf).Name;
        foreach (var level in Levels)
        {
            if (TypeModel.GetInbuiltSerializer<TValue>(level) is null)
            {
                var refused = () => Model(level).AddValueObject<TSelf, TValue>();
                refused.Should().Throw<NotSupportedException>()
                    .WithMessage($"*{name}* value object over {typeof(TValue).Name}, which protobuf-net has no inbuilt serializer for at {level}.");
                var whole = Model(level).AddValueObjects(typeof(TSelf).Assembly);
                whole.GetTypes().Cast<MetaType>().Should().NotContain(metaType => metaType.Type == typeof(TSelf), "{0} is left out", name);
                continue;
            }

            foreach (var trusted in new[] { false, true })
            {
                var model = Model(level).AddValueObject<TSelf, TValue>(trusted);
                var primitive = Model(level);
                var because = $"{name} is written as its {typeof(TValue).Name} is, at {level}, {(trusted ? "trusted" : "strict")}";

                var written = Bytes(model, new Holder<TSelf> { Value = value });
                var plain = Bytes(primitive, new Holder<TValue> { Value = value.Value });
                if (value.IsDefault)
                {
                    // protobuf-net omits a primitive's zero, which a reader takes back as zero; a value object's is written.
                    plain.Should().BeEmpty();
                    Read<Holder<TValue>>(primitive, written).Value.Should().Be(value.Value, because);
                    Read<Holder<TSelf>>(model, plain).Value.IsDefault.Should().BeTrue(because);
                }
                else
                {
                    written.Should().Equal(plain, because);
                    Bytes(model, new Holder<TSelf?> { Value = value }).Should().Equal(plain, because);
                    var list = Bytes(primitive, new Holder<List<TValue>> { Value = [value.Value, value.Value] });
                    Bytes(model, new Holder<List<TSelf>> { Value = [value, value] }).Should().Equal(list, because);

                    // An array and a set are repeated fields too: a TValue[] is no comparison for a byte, written as bytes.
                    Bytes(model, new Holder<TSelf[]> { Value = [value, value] }).Should().Equal(list, because);
                    Bytes(model, new Holder<HashSet<TSelf>> { Value = [value] })
                        .Should().Equal(Bytes(primitive, new Holder<List<TValue>> { Value = [value.Value] }), because);

                    // A root value object is written at its serializer's level, a root primitive at Level200 whatever the
                    // model's: the two agree wherever the level changes nothing for the primitive.
                    var root = Bytes(model, value);
                    var levelFree = Bytes(Model(), new Holder<TValue> { Value = value.Value }).SequenceEqual(plain);
                    if (levelFree)
                    {
                        root.Should().Equal(Bytes(primitive, value.Value), because);
                    }
                    else
                    {
                        root.Should().NotEqual(Bytes(primitive, value.Value), because);
                    }

                    Read<TSelf>(model, root).Value.Should().Be(Read<Holder<TValue>>(primitive, plain).Value, because);
                }

                var expected = Read<Holder<TValue>>(primitive, written).Value;
                Read<Holder<TSelf>>(model, written).Value.Value.Should().Be(expected, because);
                Read<Holder<TSelf?>>(model, written).Value!.Value.Value.Should().Be(expected, because);
                Read<Holder<List<TSelf>>>(model, Bytes(model, new Holder<List<TSelf>> { Value = [value] })).Value!
                    .Should().ContainSingle().Which.Value.Should().Be(expected, because);
                Read<Holder<TSelf[]>>(model, Bytes(model, new Holder<TSelf[]> { Value = [value] })).Value!
                    .Should().ContainSingle().Which.Value.Should().Be(expected, because);
                Read<Holder<HashSet<TSelf>>>(model, Bytes(model, new Holder<HashSet<TSelf>> { Value = [value] })).Value!
                    .Should().ContainSingle().Which.Value.Should().Be(expected, because);

                Fields(model.GetSchema(typeof(Holder<TSelf>), ProtoSyntax.Proto3))
                    .Should().Equal(Fields(primitive.GetSchema(typeof(Holder<TValue>), ProtoSyntax.Proto3)), because);
            }
        }
    }

    /// <summary>
    /// Keeps the imports and the fields of a <c>.proto</c> export, which name nothing of the message itself, without the
    /// comments protobuf-net adds, a default value it could not write for a primitive.
    /// </summary>
    private static string[] Fields(string schema)
        => [.. schema.Split('\n')
            .Select(line => line.Split(" //", 2)[0].Trim())
            .Where(line => line.StartsWith("import ", StringComparison.Ordinal) || line.Contains(" = ", StringComparison.Ordinal))];
}
