using System.Globalization;
using System.Numerics;

namespace AdCodicem.ValueObjects.Testing.Data;

/// <summary>
/// How the sampler draws, reads and steps a value of an underlying type that takes a bound: one instance per such type of
/// the closed table, none for <see cref="string"/>, <see cref="bool"/>, <see cref="Guid"/> or a type outside it.
/// </summary>
/// <typeparam name="TValue">The underlying type.</typeparam>
internal abstract class Underlying<TValue>
{
    /// <summary>Gets the instance for <typeparamref name="TValue"/>, or <see langword="null"/>.</summary>
    public static Underlying<TValue>? Instance { get; } = Create();

    /// <summary>Gets the lowest value drawn on a side the schema leaves open.</summary>
    protected abstract TValue Lowest { get; }

    /// <summary>Gets the highest value drawn on a side the schema leaves open.</summary>
    protected abstract TValue Highest { get; }

    /// <summary>
    /// Gets the window a value is drawn from: the declared bounds, and the type's own extremes on a side left open.
    /// </summary>
    /// <param name="minimum">The declared minimum, read.</param>
    /// <param name="hasMinimum">Whether the schema declares a minimum it could read.</param>
    /// <param name="maximum">The declared maximum, read.</param>
    /// <param name="hasMaximum">Whether the schema declares a maximum it could read.</param>
    /// <returns>The lowest and the highest value to draw, both included.</returns>
    public virtual (TValue Low, TValue High) Window(TValue minimum, bool hasMinimum, TValue maximum, bool hasMaximum)
        => (hasMinimum ? minimum : Lowest, hasMaximum ? maximum : Highest);

    /// <summary>Draws a value between two, both included; the lower when they are the wrong way round.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <param name="low">The lowest value.</param>
    /// <param name="high">The highest value.</param>
    /// <returns>The value.</returns>
    public abstract TValue Draw(Random random, TValue low, TValue high);

    /// <summary>Reads a bound, written as <c>ValueObjectBound.Text</c> writes one.</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The bound.</param>
    /// <returns><see langword="false"/> for text the type does not read as a finite value.</returns>
    public abstract bool TryParse(string text, out TValue value);

    /// <summary>Steps to the value just below, the smallest step the type takes.</summary>
    /// <param name="value">The value.</param>
    /// <param name="below">The value just below.</param>
    /// <returns><see langword="false"/> when the value is the lowest the type holds.</returns>
    public abstract bool TryBelow(TValue value, out TValue below);

    /// <summary>Steps to the value just above, the smallest step the type takes.</summary>
    /// <param name="value">The value.</param>
    /// <param name="above">The value just above.</param>
    /// <returns><see langword="false"/> when the value is the highest the type holds.</returns>
    public abstract bool TryAbove(TValue value, out TValue above);

    /// <summary>Draws a whole number between two, both included, with no bias; the lower when they are inverted.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <param name="low">The lowest number.</param>
    /// <param name="high">The highest number.</param>
    /// <returns>The number.</returns>
    protected static BigInteger Between(Random random, BigInteger low, BigInteger high)
    {
        var count = BigInteger.Max(high - low, BigInteger.Zero) + BigInteger.One;
        var bytes = new byte[count.GetByteCount(isUnsigned: true)];
        var limit = BigInteger.One << (bytes.Length * 8);

        // The draws past the largest multiple of the count are drawn again, which keeps every number equally likely.
        var usable = limit - (limit % count);
        while (true)
        {
            random.NextBytes(bytes);
            var drawn = new BigInteger(bytes, isUnsigned: true);
            if (drawn < usable)
            {
                return low + (drawn % count);
            }
        }
    }

    private static Underlying<TValue>? Create()
    {
        object? instance =
            typeof(TValue) == typeof(sbyte) ? new IntegerUnderlying<sbyte>()
            : typeof(TValue) == typeof(byte) ? new IntegerUnderlying<byte>()
            : typeof(TValue) == typeof(short) ? new IntegerUnderlying<short>()
            : typeof(TValue) == typeof(ushort) ? new IntegerUnderlying<ushort>()
            : typeof(TValue) == typeof(int) ? new IntegerUnderlying<int>()
            : typeof(TValue) == typeof(uint) ? new IntegerUnderlying<uint>()
            : typeof(TValue) == typeof(long) ? new IntegerUnderlying<long>()
            : typeof(TValue) == typeof(ulong) ? new IntegerUnderlying<ulong>()
            : typeof(TValue) == typeof(Int128) ? new IntegerUnderlying<Int128>()
            : typeof(TValue) == typeof(UInt128) ? new IntegerUnderlying<UInt128>()
            : typeof(TValue) == typeof(char) ? new CharUnderlying()
            : typeof(TValue) == typeof(double) ? new FloatingUnderlying<double>()
            : typeof(TValue) == typeof(float) ? new FloatingUnderlying<float>()
            : typeof(TValue) == typeof(decimal) ? new DecimalUnderlying()
            : typeof(TValue) == typeof(DateOnly) ? new TickUnderlying<DateOnly>(
                static value => value.DayNumber,
                static day => DateOnly.FromDayNumber((int)day),
                DateOnly.MinValue.DayNumber,
                DateOnly.MaxValue.DayNumber)
            : typeof(TValue) == typeof(TimeOnly) ? new TickUnderlying<TimeOnly>(
                static value => value.Ticks,
                static ticks => new TimeOnly(ticks),
                TimeOnly.MinValue.Ticks,
                TimeOnly.MaxValue.Ticks)
            : typeof(TValue) == typeof(DateTime) ? new TickUnderlying<DateTime>(
                static value => value.Ticks,
                static ticks => new DateTime(ticks, DateTimeKind.Utc),
                DateTime.MinValue.Ticks,
                DateTime.MaxValue.Ticks)
            : typeof(TValue) == typeof(DateTimeOffset) ? new TickUnderlying<DateTimeOffset>(
                static value => value.UtcTicks,
                static ticks => new DateTimeOffset(ticks, TimeSpan.Zero),
                DateTimeOffset.MinValue.UtcTicks,
                DateTimeOffset.MaxValue.UtcTicks)
            : typeof(TValue) == typeof(TimeSpan) ? new TickUnderlying<TimeSpan>(
                static value => value.Ticks,
                static ticks => new TimeSpan(ticks),
                TimeSpan.MinValue.Ticks,
                TimeSpan.MaxValue.Ticks)
            : null;

        return (Underlying<TValue>?)instance;
    }
}

/// <summary>A whole number: drawn evenly between its bounds, stepped by one.</summary>
/// <typeparam name="T">The integer type.</typeparam>
internal sealed class IntegerUnderlying<T> : Underlying<T>
    where T : IBinaryInteger<T>, IMinMaxValue<T>
{
    protected override T Lowest => T.MinValue;

    protected override T Highest => T.MaxValue;

    public override T Draw(Random random, T low, T high)
        => T.CreateChecked(Between(random, BigInteger.CreateChecked(low), BigInteger.CreateChecked(high)));

    public override bool TryParse(string text, out T value)
        => T.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value!);

    // Saturating, the step from an extreme lands on the extreme itself, which tells it apart.
    public override bool TryBelow(T value, out T below)
    {
        below = T.CreateSaturating(BigInteger.CreateChecked(value) - BigInteger.One);
        return below != value;
    }

    public override bool TryAbove(T value, out T above)
    {
        above = T.CreateSaturating(BigInteger.CreateChecked(value) + BigInteger.One);
        return above != value;
    }
}

/// <summary>A character: printable ASCII on a side the schema leaves open, since UTF-16 holds controls and lone surrogates.</summary>
internal sealed class CharUnderlying : Underlying<char>
{
    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';

    protected override char Lowest => FirstPrintable;

    protected override char Highest => LastPrintable;

    /// <summary>
    /// Keeps to printable ASCII on an open side, unless the bound declared on the other side lies beyond it, where the
    /// open side takes the type's own extreme.
    /// </summary>
    public override (char Low, char High) Window(char minimum, bool hasMinimum, char maximum, bool hasMaximum)
    {
        var low = hasMinimum ? minimum : hasMaximum && maximum < Lowest ? char.MinValue : Lowest;
        var high = hasMaximum ? maximum : low <= Highest ? Highest : char.MaxValue;
        return (low, high);
    }

    public override char Draw(Random random, char low, char high) => (char)Between(random, low, high);

    public override bool TryParse(string text, out char value) => char.TryParse(text, out value);

    public override bool TryBelow(char value, out char below)
    {
        below = (char)Math.Max(value - 1, char.MinValue);
        return below != value;
    }

    public override bool TryAbove(char value, out char above)
    {
        above = (char)Math.Min(value + 1, char.MaxValue);
        return above != value;
    }
}

/// <summary>A binary floating-point number: drawn evenly between its bounds, stepped by one unit in the last place.</summary>
/// <typeparam name="T">The floating-point type.</typeparam>
internal sealed class FloatingUnderlying<T> : Underlying<T>
    where T : IFloatingPointIeee754<T>, IMinMaxValue<T>
{
    protected override T Lowest => T.MinValue;

    protected override T Highest => T.MaxValue;

    public override T Draw(Random random, T low, T high)
    {
        // Bounds the wrong way round hold no value: the lower one comes back, and the type's rules refuse it.
        if (low >= high)
        {
            return low;
        }

        // Weighted rather than low + share * (high - low), which overflows across the whole range; the clamp takes back
        // the last unit a rounding may push past a bound.
        var share = T.CreateChecked(random.NextDouble());
        return T.Clamp((low * (T.One - share)) + (high * share), low, high);
    }

    public override bool TryParse(string text, out T value)
        => T.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value!) && T.IsFinite(value);

    public override bool TryBelow(T value, out T below)
    {
        below = T.BitDecrement(value);
        return T.IsFinite(below);
    }

    public override bool TryAbove(T value, out T above)
    {
        above = T.BitIncrement(value);
        return T.IsFinite(above);
    }
}

/// <summary>A decimal: drawn evenly between its bounds, stepped by one unit of its last written digit.</summary>
internal sealed class DecimalUnderlying : Underlying<decimal>
{
    protected override decimal Lowest => decimal.MinValue;

    protected override decimal Highest => decimal.MaxValue;

    public override decimal Draw(Random random, decimal low, decimal high)
    {
        // Bounds the wrong way round hold no value: the lower one comes back, and the type's rules refuse it.
        if (low >= high)
        {
            return low;
        }

        // Bounds of one sign have a difference the type holds; bounds of opposite signs may not, and are weighted, which
        // cannot overflow either since the two terms are of opposite signs.
        var share = (decimal)random.NextDouble();
        var drawn = (low < 0) == (high < 0)
            ? low + ((high - low) * share)
            : (low * (1 - share)) + (high * share);
        return Math.Clamp(drawn, low, high);
    }

    public override bool TryParse(string text, out decimal value)
        => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    // One unit of the value's last written digit: just below 0.01 is 0.00, just above 100 is 101. Only the extremes
    // themselves would overflow, and stepping from the value held one step inside lands back on them instead.
    public override bool TryBelow(decimal value, out decimal below)
    {
        var step = Step(value);
        below = decimal.Max(value, decimal.MinValue + step) - step;
        return below != value;
    }

    public override bool TryAbove(decimal value, out decimal above)
    {
        var step = Step(value);
        above = decimal.Min(value, decimal.MaxValue - step) + step;
        return above != value;
    }

    private static decimal Step(decimal value) => new(1, 0, 0, false, value.Scale);
}

/// <summary>A date, a time or a duration, drawn and stepped as the count of ticks, or of days, it stands for.</summary>
/// <typeparam name="T">The type.</typeparam>
/// <param name="toTicks">Reads the count.</param>
/// <param name="fromTicks">Builds the value of a count.</param>
/// <param name="minimumTicks">The lowest count the type holds.</param>
/// <param name="maximumTicks">The highest count the type holds.</param>
internal sealed class TickUnderlying<T>(Func<T, long> toTicks, Func<long, T> fromTicks, long minimumTicks, long maximumTicks)
    : Underlying<T>
    where T : IParsable<T>
{
    protected override T Lowest => fromTicks(minimumTicks);

    protected override T Highest => fromTicks(maximumTicks);

    public override T Draw(Random random, T low, T high) => fromTicks((long)Between(random, toTicks(low), toTicks(high)));

    public override bool TryParse(string text, out T value) => T.TryParse(text, CultureInfo.InvariantCulture, out value!);

    // Compared before the step, which would overflow a long at the extremes of a TimeSpan and wrap around to the other.
    public override bool TryBelow(T value, out T below)
    {
        var ticks = toTicks(value);
        var stepped = ticks > minimumTicks ? ticks - 1 : ticks;
        below = fromTicks(stepped);
        return stepped != ticks;
    }

    public override bool TryAbove(T value, out T above)
    {
        var ticks = toTicks(value);
        var stepped = ticks < maximumTicks ? ticks + 1 : ticks;
        above = fromTicks(stepped);
        return stepped != ticks;
    }
}
