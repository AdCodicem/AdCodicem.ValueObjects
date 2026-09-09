using System.Numerics;

namespace AdCodicem.ValueObjects;

/// <summary>
/// A value object over a numeric underlying type, exposing arithmetic through the generic math interfaces.
/// </summary>
/// <typeparam name="TSelf">The value object type itself.</typeparam>
/// <typeparam name="TValue">Underlying numeric type.</typeparam>
/// <remarks>
/// <para>
/// Every arithmetic operator routes its result back through <see cref="IValueObject{TSelf, TValue}.Create"/>,
/// so an operation that would produce an invalid value throws instead of silently escaping the type's rules —
/// subtracting 30 from a <c>Percentage</c> of 20 is a bug, not a negative percentage.
/// </para>
/// <para>
/// Unary negation is emitted by the generator only for signed underlying types and is intentionally absent from
/// this contract, so that value objects over unsigned integers can still take part in generic arithmetic. The
/// same goes for dividing two value objects into a bare ratio: the operator is emitted on the concrete type,
/// but declaring it here alongside division by a scalar would make the two instantiations of
/// <see cref="IDivisionOperators{TSelf, TOther, TResult}"/> unifiable.
/// </para>
/// </remarks>
public interface INumericValueObject<TSelf, TValue> :
    IValueObject<TSelf, TValue>,
    IAdditionOperators<TSelf, TSelf, TSelf>,
    ISubtractionOperators<TSelf, TSelf, TSelf>,
    IComparisonOperators<TSelf, TSelf, bool>,
    IMultiplyOperators<TSelf, TValue, TSelf>,
    IDivisionOperators<TSelf, TValue, TSelf>
    where TSelf : struct, INumericValueObject<TSelf, TValue>
    where TValue : struct, INumber<TValue>
{
    /// <summary>
    /// Gets the value object representing zero.
    /// </summary>
    /// <exception cref="ValueObjectException">Zero is not a valid value for <typeparamref name="TSelf"/>.</exception>
    static virtual TSelf Zero => TSelf.Create(TValue.Zero);

    /// <summary>
    /// Gets the value object representing one.
    /// </summary>
    /// <exception cref="ValueObjectException">One is not a valid value for <typeparamref name="TSelf"/>.</exception>
    static virtual TSelf One => TSelf.Create(TValue.One);

    /// <summary>
    /// Gets a value indicating whether the carried value is zero.
    /// </summary>
    bool IsZero => TValue.IsZero(Value);

    /// <summary>
    /// Returns the absolute value of <paramref name="value"/>.
    /// </summary>
    /// <param name="value">Value to take the absolute value of.</param>
    /// <returns>The absolute value.</returns>
    /// <exception cref="ValueObjectException">The absolute value is not valid for <typeparamref name="TSelf"/>.</exception>
    static virtual TSelf Abs(TSelf value) => TSelf.Create(TValue.Abs(value.Value));

    /// <summary>
    /// Returns the smaller of two values.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>The smaller of the two operands.</returns>
    static virtual TSelf Min(TSelf left, TSelf right) => left.CompareTo(right) <= 0 ? left : right;

    /// <summary>
    /// Returns the larger of two values.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>The larger of the two operands.</returns>
    static virtual TSelf Max(TSelf left, TSelf right) => left.CompareTo(right) >= 0 ? left : right;

    /// <summary>
    /// Computes the sum of a sequence of values.
    /// </summary>
    /// <param name="values">Values to add up.</param>
    /// <returns>The sum, or the value object built from <c>TValue.Zero</c> for an empty sequence.</returns>
    /// <remarks>
    /// Accumulation happens on the underlying type and the result is validated once, so an intermediate total
    /// that momentarily leaves the valid range does not throw.
    /// </remarks>
    static virtual TSelf Sum(IEnumerable<TSelf> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var total = TValue.Zero;
        foreach (var value in values)
        {
            total += value.Value;
        }

        return TSelf.Create(total);
    }
}
