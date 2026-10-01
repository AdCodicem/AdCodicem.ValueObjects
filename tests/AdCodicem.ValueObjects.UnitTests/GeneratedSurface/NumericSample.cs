using System.Numerics;
using System.Reflection;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

/// <summary>A <see cref="Sample{TSelf, TValue}"/> of a value object that declares <c>Arithmetic = true</c>.</summary>
public sealed class NumericSample<TSelf, TValue> : Sample<TSelf, TValue>
    where TSelf : struct, INumericValueObject<TSelf, TValue>
    where TValue : struct, INumber<TValue>
{
    public NumericSample(TSelf first, TSelf second, string refused)
        : base(first, second, refused)
    {
        TValue.IsZero(Large.Value).Should().BeFalse("the larger value divides the others");
    }

    public override bool IsNumeric => true;

    public override void ComputesThroughEveryArithmeticMember()
    {
        ExpectSame(() => TSelf.Zero, () => TSelf.Create(TValue.Zero));
        ExpectSame(() => TSelf.One, () => TSelf.Create(TValue.One));
        Small.IsZero.Should().Be(TValue.IsZero(Small.Value));
        ExpectSame(() => TSelf.Abs(Small), () => TSelf.Create(TValue.Abs(Small.Value)));

        TSelf.Min(Small, Large).Should().Be(Small);
        TSelf.Min(Large, Small).Should().Be(Small);
        TSelf.Max(Small, Large).Should().Be(Large);
        TSelf.Max(Large, Small).Should().Be(Large);

        ExpectSame(() => TSelf.Sum([Small, Large]), () => TSelf.Create(Small.Value + Large.Value));
        FluentActions.Invoking(() => TSelf.Sum(null!)).Should().Throw<ArgumentNullException>();

        ExpectSame(() => Small + Large, () => TSelf.Create(Small.Value + Large.Value));
        ExpectSame(() => Large - Small, () => TSelf.Create(Large.Value - Small.Value));
        ExpectSame(() => Large * TValue.One, () => TSelf.Create(Large.Value));
        ExpectSame(() => Large / TValue.One, () => TSelf.Create(Large.Value));

        // Declared on the concrete type only: scaling from the left, the bare ratio, and negation for a signed type.
        var scaleFromTheLeft = Method<Func<TValue, TSelf, TSelf>>("op_Multiply", typeof(TValue), typeof(TSelf));
        ExpectSame(() => scaleFromTheLeft(TValue.One, Large), () => TSelf.Create(Large.Value));
        Method<Func<TSelf, TSelf, TValue>>("op_Division", typeof(TSelf), typeof(TSelf))(Large, Large)
            .Should().Be(TValue.One);

        var negation = typeof(TSelf).GetMethod("op_UnaryNegation", BindingFlags.Public | BindingFlags.Static, [typeof(TSelf)]);
        if (negation is not null)
        {
            var negate = negation.CreateDelegate<Func<TSelf, TSelf>>();
            ExpectSame(() => negate(Small), () => TSelf.Create(TValue.Zero - Small.Value));
        }
    }

    /// <summary>
    /// Asserts that an emitted member gives what building the value by hand gives: the same value, or the same
    /// rejection when the result breaks one of the type's rules.
    /// </summary>
    private static void ExpectSame(Func<TSelf> actual, Func<TSelf> expected)
    {
        TSelf wanted;
        try
        {
            wanted = expected();
        }
        catch (ValueObjectException rejection)
        {
            actual.Should().Throw<ValueObjectException>().Which.ErrorCode.Should().Be(rejection.ErrorCode);
            return;
        }

        actual().Should().Be(wanted);
    }
}
