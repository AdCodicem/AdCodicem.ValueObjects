using AdCodicem.ValueObjects.Fixtures.Grains;
using AdCodicem.ValueObjects.Orleans;
using AdCodicem.ValueObjects.Testing.Data;
using Orleans.Serialization;

namespace AdCodicem.ValueObjects.UnitTests.Grains;

/// <summary>
/// What a value object is written as through Orleans, beside what the bare value it carries is written as, for the
/// samples of <see cref="GeneratedSurface.Samples"/>, and what crosses a grain call.
/// </summary>
public static class OrleansParity
{
    /// <summary>
    /// Checks that a serializer the value object is registered on, by the registry (<c>AddValueObjects()</c>, strict) and by
    /// its type arguments (<c>AddValueObject&lt;TSelf, TValue&gt;(trusted: true)</c>), writes a holder of it exactly as it
    /// writes a holder of the value it carries, reads those bytes back as the value object and as the primitive, reads the
    /// primitive's bytes as the value object, and round-trips it in a member typed <see cref="object"/>; and that each value
    /// the sampler derives from its rules as refused, written as the primitive, is read exactly as <c>TryCreate</c> takes
    /// it: refused with the code <c>TryCreate</c> reports, <c>not_parsable</c> where it reports none, or accepted as it
    /// normalizes it.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="value">An instance.</param>
    public static void Check<TSelf, TValue>(TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var name = typeof(TSelf).Name;
        foreach (var (wire, serializer) in new[] { ("strict, by the registry", OrleansWire.Strict), ("trusted, by its type", Typed<TSelf, TValue>.Trusted) })
        {
            var because = $"{name} is written as its {typeof(TValue).Name} is ({wire})";
            var written = serializer.SerializeToArray(new GrainHolder<TSelf> { Value = value });
            var plain = OrleansWire.Bare.SerializeToArray(new GrainHolder<TValue> { Value = value.Value });

            written.Should().Equal(plain, because);
            serializer.Read<GrainHolder<TSelf>>(written).Value.Should().Be(value, because);
            serializer.Read<GrainHolder<TSelf>>(plain).Value.Should().Be(value, because);
            OrleansWire.Bare.Read<GrainHolder<TValue>>(written).Value.Should().Be(value.Value, because);
            serializer.Read<GrainBox>(serializer.SerializeToArray(new GrainBox { Value = value })).Value
                .Should().Be(value, "{0} in a member typed object reads back as itself ({1})", name, wire);
        }

        foreach (var violation in new ValueObjectSampler(new Random(181)).RejectedValues<TSelf, TValue>())
        {
            var bytes = OrleansWire.Bare.SerializeToArray(new GrainHolder<TValue> { Value = violation.Value });
            var read = () => OrleansWire.Strict.Read<GrainHolder<TSelf>>(bytes).Value;
            if (TSelf.TryCreate(violation.Value, out var accepted, out var validation))
            {
                read().Should().Be(accepted, "{0} reads {1} as TryCreate takes it", name, violation.Rule);
            }
            else
            {
                var refusal = read.Should().Throw<SerializerException>().Which;
                ValueObjectErrors.TryGetCode(refusal, out var code).Should().BeTrue();
                code.Should().Be(validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable, "{0} refuses {1} with its code", name, violation.Rule);
                refusal.Message.Should().StartWith($"The value read is not a valid {name}: ");
            }
        }
    }

    /// <summary>
    /// Checks that a grain call carries the value object as an argument and a result of a generic method, as itself and
    /// in an argument typed <see cref="object"/>, through a cluster whose silo and client both call
    /// <c>AddValueObjects()</c>.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <param name="grain">A grain of the cluster.</param>
    /// <param name="value">An instance.</param>
    /// <returns>The check.</returns>
    public static async Task CheckAsync<TSelf>(IValueObjectGrain grain, TSelf value)
        where TSelf : struct
    {
        (await grain.Echo(value)).Should().Be(value, "{0} crosses a grain call as itself", typeof(TSelf).Name);
        (await grain.Echo<TSelf?>(value)).Should().Be(value, "{0}? crosses a grain call", typeof(TSelf).Name);
        (await grain.Echo<object>(value)).Should().Be(value, "{0} crosses a grain call typed object", typeof(TSelf).Name);
    }

    /// <summary>The serializer registering one value object by its type arguments, built once per type.</summary>
    private static class Typed<TSelf, TValue>
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        public static Serializer Trusted { get; } = OrleansWire.Build(static serializer => serializer.AddValueObject<TSelf, TValue>(trusted: true));
    }
}
