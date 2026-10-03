using System.Collections.Frozen;
using System.Numerics;

namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// The checked conversions between the numeric types a value object may wrap, for a converter that knows the types
/// only at run time.
/// </summary>
/// <remarks>
/// The type converter the generator writes on a numeric value object names each type in an arm of its own. The one a
/// generic value object names, <see cref="GenericValueObjectTypeConverter"/>, learns the underlying type from the
/// descriptor, and goes through the same bridges, <see cref="UnderlyingValue.TryConvertToInteger{TNumber, TValue}"/> and
/// <see cref="UnderlyingValue.TryConvertToReal{TNumber, TValue}"/>, from here.
/// </remarks>
internal static class NumberConversion
{
    /// <summary>The numeric types a value object may wrap, with the name the generated code gives each in a message.</summary>
    private static readonly FrozenDictionary<Type, string> Names = new Dictionary<Type, string>
    {
        [typeof(sbyte)] = "sbyte",
        [typeof(byte)] = "byte",
        [typeof(short)] = "short",
        [typeof(ushort)] = "ushort",
        [typeof(int)] = "int",
        [typeof(uint)] = "uint",
        [typeof(long)] = "long",
        [typeof(ulong)] = "ulong",
        [typeof(Int128)] = "System.Int128",
        [typeof(UInt128)] = "System.UInt128",
        [typeof(decimal)] = "decimal",
        [typeof(double)] = "double",
        [typeof(float)] = "float",
    }.ToFrozenDictionary();

    /// <summary>
    /// Tells a numeric type a value object may wrap.
    /// </summary>
    /// <param name="type">Type to test.</param>
    /// <returns>
    /// <see langword="true"/> for <see cref="sbyte"/> to <see cref="UInt128"/>, <see cref="decimal"/>,
    /// <see cref="double"/> and <see cref="float"/>.
    /// </returns>
    public static bool IsNumber(Type type) => Names.ContainsKey(type);

    /// <summary>
    /// Gets the name the generated code gives a numeric type in a message: its keyword, or its name in
    /// <see cref="System"/> for a 128-bit integer.
    /// </summary>
    /// <param name="type">A numeric type, as <see cref="IsNumber(Type)"/> tells one.</param>
    /// <returns>The name.</returns>
    public static string NameOf(Type type) => Names[type];

    /// <summary>
    /// Converts a number to another numeric type with a checked conversion that never truncates.
    /// </summary>
    /// <param name="number">The number, of a type <see cref="IsNumber(Type)"/> tells; anything else is refused.</param>
    /// <param name="destination">The type to convert it to.</param>
    /// <param name="converted">The same number, boxed as a <paramref name="destination"/>.</param>
    /// <returns>
    /// <see langword="true"/> when both types are numbers and <paramref name="destination"/> holds the number: whole for
    /// an integer type, within its range for a real.
    /// </returns>
    public static bool TryConvert(object? number, Type destination, [NotNullWhen(true)] out object? converted)
    {
        converted = number switch
        {
            sbyte value => To(value, destination),
            byte value => To(value, destination),
            short value => To(value, destination),
            ushort value => To(value, destination),
            int value => To(value, destination),
            uint value => To(value, destination),
            long value => To(value, destination),
            ulong value => To(value, destination),
            Int128 value => To(value, destination),
            UInt128 value => To(value, destination),
            decimal value => To(value, destination),
            double value => To(value, destination),
            float value => To(value, destination),
            _ => null,
        };

        return converted is not null;
    }

    private static object? To<TNumber>(TNumber number, Type destination)
        where TNumber : INumberBase<TNumber>
    {
        if (destination == typeof(sbyte))
        {
            return Integer<TNumber, sbyte>(number);
        }

        if (destination == typeof(byte))
        {
            return Integer<TNumber, byte>(number);
        }

        if (destination == typeof(short))
        {
            return Integer<TNumber, short>(number);
        }

        if (destination == typeof(ushort))
        {
            return Integer<TNumber, ushort>(number);
        }

        if (destination == typeof(int))
        {
            return Integer<TNumber, int>(number);
        }

        if (destination == typeof(uint))
        {
            return Integer<TNumber, uint>(number);
        }

        if (destination == typeof(long))
        {
            return Integer<TNumber, long>(number);
        }

        if (destination == typeof(ulong))
        {
            return Integer<TNumber, ulong>(number);
        }

        if (destination == typeof(Int128))
        {
            return Integer<TNumber, Int128>(number);
        }

        if (destination == typeof(UInt128))
        {
            return Integer<TNumber, UInt128>(number);
        }

        if (destination == typeof(decimal))
        {
            return Real<TNumber, decimal>(number);
        }

        if (destination == typeof(double))
        {
            return Real<TNumber, double>(number);
        }

        return destination == typeof(float) ? Real<TNumber, float>(number) : null;
    }

    private static object? Integer<TNumber, TValue>(TNumber number)
        where TNumber : INumberBase<TNumber>
        where TValue : IBinaryInteger<TValue>
        => UnderlyingValue.TryConvertToInteger(number, out TValue value) ? value : null;

    private static object? Real<TNumber, TValue>(TNumber number)
        where TNumber : INumberBase<TNumber>
        where TValue : IFloatingPoint<TValue>
        => UnderlyingValue.TryConvertToReal(number, out TValue value) ? value : null;
}
