using AdCodicem.ValueObjects.Shared;
using AdCodicem.ValueObjects.Testing.Data;
using FsCheck;
using FsCheck.Fluent;

namespace AdCodicem.ValueObjects.FsCheck;

/// <summary>
/// The FsCheck arbitrary of a value object: values its rules accept, drawn from the schema it declares, biased towards the
/// edges it declares, and shrinking to values it accepts.
/// </summary>
/// <remarks>
/// <para>
/// FsCheck draws the seed of each value, which seeds the <see cref="ValueObjectSampler"/> that draws it, so that a failing
/// run replays from the seed FsCheck reports. One value in four is one of the type's boundaries
/// (<see cref="ValueObjectSampler.Boundaries{TSelf, TValue}"/>), its bounds, its lengths and its known values, since a
/// property over a wide type lands on its edges by chance once in a blue moon; the size FsCheck passes is not read.
/// </para>
/// <para>
/// A counterexample shrinks through <see cref="ValueObjectSampler.Shrink{TSelf, TValue}"/>: towards the values the type
/// declares, its minimum, its first known value and its example, then through FsCheck's own shrinker of the underlying
/// value, keeping only what the type accepts as it is, its normalizer leaving it unchanged. FsCheck has a shrinker for the
/// integers up to 64 bits, <see cref="float"/>, <see cref="double"/>, <see cref="decimal"/>, <see cref="char"/>,
/// <see cref="string"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/> and <see cref="TimeSpan"/>; a value object over
/// another type, <see cref="bool"/>, <see cref="Guid"/>, <see cref="DateOnly"/> or <see cref="Int128"/> for one, shrinks to
/// its declared values alone. From a declared value, a shrink goes on only to the declared values ranked before it.
/// </para>
/// <para>
/// Nothing of the value object is read before the first draw: neither its schema nor its type initializer runs when the
/// arbitrary is built or merged.
/// </para>
/// </remarks>
public static class ValueObjectArbitrary
{
    private static readonly ValueObjectSamplerOptions Defaults = new();

    /// <summary>
    /// The underlying types FsCheck's default map has a shrinker for: it draws bool and Guid without one, handles neither
    /// DateOnly nor TimeOnly, and derives the 128-bit integers by reflection.
    /// </summary>
    private static readonly HashSet<Type> Shrinkable =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(float), typeof(double), typeof(decimal), typeof(char), typeof(string), typeof(DateTime), typeof(DateTimeOffset),
        typeof(TimeSpan),
    ];

    /// <summary>
    /// Gets the arbitrary of a value object: values its rules accept, one in four at its boundaries, shrunk to values it
    /// accepts.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="options">How each value is drawn, the generators of the rules no schema carries included, or <see langword="null"/> for the defaults.</param>
    /// <returns>The arbitrary, to merge into a map with <c>MergeArb</c> or to hand to <c>Prop.ForAll</c>.</returns>
    /// <remarks>
    /// A draw that finds no value the type accepts throws a <see cref="ValueObjectSamplingException"/>, out of the
    /// property's check.
    /// </remarks>
    public static Arbitrary<TSelf> For<TSelf, TValue>(ValueObjectSamplerOptions? options = null)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var sampling = options ?? Defaults;
        var boundaries = new Lazy<TSelf[]>(() =>
            [.. new ValueObjectSampler(new Random(0), sampling).Boundaries<TSelf, TValue>().Select(TSelf.Create)]);
        var drawn = Gen.Choose(int.MinValue, int.MaxValue).Select(seed => Draw<TSelf, TValue>(seed, sampling));
        var generator = Gen.Choose(0, 3).SelectMany(pick => pick == 0 && boundaries.Value.Length > 0 ? Gen.Elements(boundaries.Value) : drawn);
        var shrinkUnderlying = new Lazy<Func<TValue, IEnumerable<TValue>>?>(ShrinkerOf<TValue>);
        return Arb.From(generator, current => ValueObjectSampler.Shrink<TSelf, TValue>(current, shrinkUnderlying.Value));
    }

    /// <summary>FsCheck's shrinker of an underlying type, or <see langword="null"/> for a type it has none for.</summary>
    private static Func<TValue, IEnumerable<TValue>>? ShrinkerOf<TValue>()
    {
        if (!Shrinkable.Contains(typeof(TValue)))
        {
            return null;
        }

        if (typeof(TValue) != typeof(DateTime))
        {
            return ArbMap.Default.ArbFor<TValue>().Shrinker;
        }

        // FsCheck shrinks a UTC or local DateTime, as the sampler draws one, to the same instant of no kind and to nothing
        // else, a value equal to the one shrunk: its shrinks of the instant of no kind are given back their kind.
        var shrinker = ArbMap.Default.ArbFor<DateTime>().Shrinker;
        Func<DateTime, IEnumerable<DateTime>> keepingKind = value =>
            shrinker(DateTime.SpecifyKind(value, DateTimeKind.Unspecified)).Select(smaller => DateTime.SpecifyKind(smaller, value.Kind));
        return (Func<TValue, IEnumerable<TValue>>)(object)keepingKind;
    }

    private static TSelf Draw<TSelf, TValue>(int seed, ValueObjectSamplerOptions options)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        try
        {
            return new ValueObjectSampler(new Random(seed), options).Next<TSelf, TValue>();
        }
        catch (ValueObjectSamplingException exception) when (!exception.FromGenerator)
        {
            // A generator registered with the options keeps the sampler's message, which names that registration.
            throw new ValueObjectSamplingException(
                exception.ValueObjectType,
                exception.Attempts,
                exception.ErrorCode,
                $"options.Use<{TypeNames.Of(typeof(TSelf))}, {TypeNames.Of(typeof(TValue))}>(random => ...), on the options given to "
                + "MergeValueObjects or ValueObjectArbitrary.For");
        }
    }
}
