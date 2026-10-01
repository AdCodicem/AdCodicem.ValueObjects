using System.Numerics;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// What a value object written by hand inherits from the contracts, rather than from the generator.
/// </summary>
/// <remarks>
/// The generator emits a concrete member for every default body of <see cref="INumericValueObject{TSelf, TValue}"/>
/// and <see cref="IValueObject{TValue}"/>, so generated types never run them. Static virtual members are reachable
/// only through a type parameter, hence the constrained helpers at the bottom.
/// </remarks>
public class HandWrittenValueObjectTests
{
    [Fact]
    public void An_integral_sum_that_overflows_throws_rather_than_wrapping()
    {
        var act = () => SumOf<HandWrittenCounter, int>([HandWrittenCounter.Create(int.MaxValue), HandWrittenCounter.Create(1)]);

        act.Should().Throw<OverflowException>("a total that wraps around to int.MinValue is a total that lies");
    }

    private static TSelf SumOf<TSelf, TValue>(IEnumerable<TSelf> values)
        where TSelf : struct, INumericValueObject<TSelf, TValue>
        where TValue : struct, INumber<TValue>
        => TSelf.Sum(values);
}
