using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.Marten;

/// <summary>
/// What Marten calls to turn a value it read from a column or a projection into the value object.
/// </summary>
/// <typeparam name="TSelf">The value object.</typeparam>
/// <typeparam name="TValue">Its underlying type.</typeparam>
/// <remarks>
/// A document is read by the value object's JSON converter, which refuses a value its rules refuse with a
/// <see cref="ValueObjectJsonException"/>; a value a query projects (<c>Select(x =&gt; x.Quantity)</c>,
/// <c>MaxAsync(x =&gt; x.Quantity)</c>) is read through this builder instead, which refuses it with the same exception,
/// so that one <c>catch</c> covers a stored value whichever way it is read.
/// </remarks>
internal static class ValueObjectReads<TSelf, TValue>
    where TSelf : struct, IValueObject<TSelf, TValue>
{
    /// <summary>
    /// Reads a value through the rules, a refusal thrown as the converter refuses the same value in a document.
    /// </summary>
    /// <param name="value">The value read.</param>
    /// <returns>The value object.</returns>
    /// <exception cref="ValueObjectJsonException">The value object refuses the value.</exception>
    public static TSelf Read(TValue value)
        => TSelf.TryCreate(value, out var result, out var validation)
            ? result
            : throw new ValueObjectJsonException(
                $"The value is not a valid {TypeNames.Of(typeof(TSelf))}: {validation.ErrorMessage}",
                typeof(TSelf),
                validation.ErrorCode ?? ValueObjectErrorCodes.NotParsable);

    /// <summary>Reads a value through the rules, for a member of type <c>TSelf?</c>.</summary>
    /// <param name="value">The value read.</param>
    /// <returns>The value object.</returns>
    /// <exception cref="ValueObjectJsonException">The value object refuses the value.</exception>
    public static TSelf? ReadNullable(TValue value) => Read(value);
}
