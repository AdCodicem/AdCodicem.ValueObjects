using System.Globalization;
using System.Runtime.CompilerServices;

namespace AdCodicem.ValueObjects;

/// <summary>
/// A generic bridge to the bounds a value object declares through <see cref="IValueObjectMinimum{TValue}"/> and
/// <see cref="IValueObjectMaximum{TValue}"/>, used by generated code and by the registry.
/// </summary>
/// <remarks>
/// <para>
/// A static abstract member is reached through a type parameter, which finds it however the value object implements
/// it. Named on the type, <c>BirthDate.Minimum</c> would miss an explicit implementation and fail to compile in a file
/// the consumer cannot edit.
/// </para>
/// <para>
/// The bound is read from the value object each time it is asked for, and nothing keeps it. A bound is a constant, which
/// the JIT folds into the check that reads it, so a copy would save nothing; and a copy taken while the value object's
/// own static fields are still being initialized, by a well-known instance created before the bound is assigned, would
/// keep the default of the type for the life of the process.
/// </para>
/// </remarks>
public static class ValueObjectBound
{
    /// <summary>
    /// Gets the inclusive lower bound a value object declares.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <returns>The bound.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue Minimum<TSelf, TValue>()
        where TSelf : IValueObjectMinimum<TValue>
        => TSelf.Minimum;

    /// <summary>
    /// Gets the inclusive upper bound a value object declares.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <returns>The bound.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TValue Maximum<TSelf, TValue>()
        where TSelf : IValueObjectMaximum<TValue>
        => TSelf.Maximum;

    /// <summary>
    /// Writes a bound as text, in the one invariant form a bound of its type is written in.
    /// </summary>
    /// <typeparam name="TValue">The type of the bound.</typeparam>
    /// <param name="bound">The bound.</param>
    /// <returns>The text: digits for an integer or a decimal, the round-trip form of a real, the ISO 8601 form of a date or
    /// a time, the constant form of a duration, and the character itself for a <see cref="char"/>.</returns>
    /// <remarks>
    /// <para>
    /// It is the text the schema publishes, which the OpenAPI transformer and the type's own parser read back, and the
    /// text the message of a rejected value quotes.
    /// </para>
    /// <para>
    /// A <see cref="DateTime"/> is written without its <see cref="DateTime.Kind"/>, neither a <c>Z</c> nor an offset:
    /// the check compares clock readings, whatever their kind, and a <see cref="DateTimeKind.Local"/> bound would
    /// otherwise publish the offset of whichever machine serves the document.
    /// </para>
    /// </remarks>
    public static string Text<TValue>(TValue bound)
        => bound switch
        {
            double real => real.ToString("R", CultureInfo.InvariantCulture),
            float real => real.ToString("R", CultureInfo.InvariantCulture),
            TimeSpan duration => duration.ToString("c", CultureInfo.InvariantCulture),
            DateTime instant => DateTime.SpecifyKind(instant, DateTimeKind.Unspecified).ToString("O", CultureInfo.InvariantCulture),
            DateOnly or TimeOnly or DateTimeOffset => ((IFormattable)bound).ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => bound?.ToString() ?? string.Empty,
        };
}
