using System.Numerics;
using System.Runtime.CompilerServices;

namespace AdCodicem.ValueObjects;

/// <summary>
/// Thin generic bridges to the underlying type's own parsing and formatting, used by generated code.
/// </summary>
/// <remarks>
/// Going through a constrained generic call rather than naming <c>Guid.TryParse</c>, <c>Decimal.TryParse</c> and
/// the rest one by one keeps the generator honest: any type reachable here is guaranteed by the compiler to
/// expose the API, and the JIT devirtualizes the call for the concrete type argument, so nothing is paid at
/// run time for the indirection.
/// </remarks>
public static class UnderlyingValue
{
    /// <summary>
    /// Parses a span using the underlying type's own parser.
    /// </summary>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="text">Text to parse.</param>
    /// <param name="provider">Format provider.</param>
    /// <param name="value">The parsed value.</param>
    /// <returns><see langword="true"/> when the text was parsed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse<TValue>(ReadOnlySpan<char> text, IFormatProvider? provider, out TValue value)
        where TValue : ISpanParsable<TValue>
        => TValue.TryParse(text, provider, out value!);

    /// <summary>
    /// Parses a string using the underlying type's own parser.
    /// </summary>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="text">Text to parse.</param>
    /// <param name="provider">Format provider.</param>
    /// <param name="value">The parsed value.</param>
    /// <returns><see langword="true"/> when the text was parsed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParse<TValue>(string? text, IFormatProvider? provider, out TValue value)
        where TValue : IParsable<TValue>
        => TValue.TryParse(text, provider, out value!);

    /// <summary>
    /// Formats a value into a destination span using the underlying type's own formatter.
    /// </summary>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="value">Value to format.</param>
    /// <param name="destination">Destination buffer.</param>
    /// <param name="charsWritten">Number of characters written.</param>
    /// <param name="format">Format specifier.</param>
    /// <param name="provider">Format provider.</param>
    /// <returns><see langword="true"/> when the value fitted in <paramref name="destination"/>.</returns>
    /// <remarks>
    /// Several BCL types, <see cref="Guid"/> among them, implement the four-argument
    /// <see cref="ISpanFormattable.TryFormat"/> explicitly and only expose a shorter public overload. Routing
    /// through a constrained type parameter reaches the interface method uniformly, and the call is
    /// devirtualized for the concrete type argument.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryFormat<TValue>(
        in TValue value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
        where TValue : ISpanFormattable
        => value.TryFormat(destination, out charsWritten, format, provider);

    /// <summary>
    /// Gets the additive identity of a numeric underlying type.
    /// </summary>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <returns>Zero.</returns>
    /// <remarks>
    /// The numeric constants of the BCL primitives are static abstract members of <c>INumberBase</c>, reachable
    /// only through a type parameter. These bridges are how generated code names them.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue Zero<TValue>()
        where TValue : INumberBase<TValue>
        => TValue.Zero;

    /// <summary>
    /// Gets the multiplicative identity of a numeric underlying type.
    /// </summary>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <returns>One.</returns>
    /// <inheritdoc cref="Zero{TValue}" path="/remarks"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue One<TValue>()
        where TValue : INumberBase<TValue>
        => TValue.One;

    /// <summary>
    /// Returns the absolute value of a numeric underlying value.
    /// </summary>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="value">Value to take the absolute value of.</param>
    /// <returns>The absolute value.</returns>
    /// <inheritdoc cref="Zero{TValue}" path="/remarks"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue Abs<TValue>(TValue value)
        where TValue : INumberBase<TValue>
        => TValue.Abs(value);

    /// <summary>
    /// Determines whether a numeric underlying value is zero.
    /// </summary>
    /// <typeparam name="TValue">Underlying value type.</typeparam>
    /// <param name="value">Value to test.</param>
    /// <returns><see langword="true"/> when the value is zero.</returns>
    /// <inheritdoc cref="Zero{TValue}" path="/remarks"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsZero<TValue>(TValue value)
        where TValue : INumberBase<TValue>
        => TValue.IsZero(value);
}
