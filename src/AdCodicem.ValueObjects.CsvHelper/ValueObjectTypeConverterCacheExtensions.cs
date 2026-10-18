using CsvHelper.TypeConversion;

namespace AdCodicem.ValueObjects.CsvHelper;

/// <summary>
/// Registers value objects on a CsvHelper context's converter cache, so that each is mapped, read and written as one
/// field.
/// </summary>
/// <remarks>
/// <para>
/// CsvHelper reads the cache when it builds a map: register on the context of every reader and writer, before its first
/// <c>GetRecords</c>, <c>WriteRecords</c> or <c>RegisterClassMap</c>. A converter added later changes nothing for a type
/// already mapped, a class map registered first keeps the converter it found, and a class map calling
/// <c>AutoMap(CultureInfo)</c> builds a context of its own, which sees no converter of the reader's.
/// </para>
/// <para>
/// Without a converter, CsvHelper maps a value object as a record of its own: it writes a <c>Value</c> column, and reads
/// default instances with nothing thrown.
/// </para>
/// </remarks>
public static class ValueObjectTypeConverterCacheExtensions
{
    /// <summary>
    /// Adds <see cref="ValueObjectTypeConverterFactory"/>, so that every value object the context meets is mapped, read and
    /// written as one field.
    /// </summary>
    /// <param name="cache">The context's converter cache, <c>csv.Context.TypeConverterCache</c>.</param>
    /// <returns>The cache, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cache"/> is <see langword="null"/>.</exception>
    public static TypeConverterCache AddValueObjects(this TypeConverterCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);

        cache.AddConverterFactory(ValueObjectTypeConverterFactory.Instance);
        return cache;
    }

    /// <summary>
    /// Adds the converter of one value object, a construction of a generic one or one written by hand included, without the
    /// registry.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="cache">The context's converter cache, <c>csv.Context.TypeConverterCache</c>.</param>
    /// <returns>The cache, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cache"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The converter serves the nullable type too, through CsvHelper's own <c>NullableConverter</c>, and answers before any
    /// factory, <see cref="ValueObjectTypeConverterFactory"/> included.
    /// </remarks>
    public static TypeConverterCache AddValueObject<TSelf, TValue>(this TypeConverterCache cache)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(cache);

        cache.AddConverter<TSelf>(new ValueObjectTypeConverter<TSelf, TValue>());
        return cache;
    }
}
