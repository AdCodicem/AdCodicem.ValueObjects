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
    public void A_hand_written_numeric_value_object_inherits_the_interface_defaults()
    {
        var two = HandWrittenCounter.Create(2);
        var five = HandWrittenCounter.Create(5);

        ZeroOf<HandWrittenCounter, int>().Value.Should().Be(0);
        OneOf<HandWrittenCounter, int>().Value.Should().Be(1);
        IsZero<HandWrittenCounter, int>(HandWrittenCounter.Create(0)).Should().BeTrue();
        IsZero<HandWrittenCounter, int>(two).Should().BeFalse();
        AbsOf<HandWrittenCounter, int>(HandWrittenCounter.Create(-3)).Should().Be(HandWrittenCounter.Create(3));
        MinOf<HandWrittenCounter, int>(two, five).Should().Be(two);
        MinOf<HandWrittenCounter, int>(five, two).Should().Be(two);
        MaxOf<HandWrittenCounter, int>(two, five).Should().Be(five);
        MaxOf<HandWrittenCounter, int>(five, two).Should().Be(five);
    }

    [Fact]
    public void A_hand_written_sum_adds_up_starts_from_zero_and_refuses_a_missing_sequence()
    {
        SumOf<HandWrittenCounter, int>([HandWrittenCounter.Create(1), HandWrittenCounter.Create(2), HandWrittenCounter.Create(3)])
            .Should().Be(HandWrittenCounter.Create(6));
        SumOf<HandWrittenCounter, int>([]).Should().Be(HandWrittenCounter.Create(0));

        var act = () => SumOf<HandWrittenCounter, int>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void A_hand_written_value_object_exposes_its_boxed_value_through_the_interface_default()
    {
        // The default is an explicit implementation, so only the interface reaches it.
        ((IValueObject)HandWrittenCounter.Create(5)).GetBoxedValue().Should().Be(5);
    }

    [Fact]
    public void An_integral_sum_that_overflows_throws_rather_than_wrapping()
    {
        var act = () => SumOf<HandWrittenCounter, int>([HandWrittenCounter.Create(int.MaxValue), HandWrittenCounter.Create(1)]);

        act.Should().Throw<OverflowException>("a total that wraps around to int.MinValue is a total that lies");
    }

    private static TSelf ZeroOf<TSelf, TValue>()
        where TSelf : struct, INumericValueObject<TSelf, TValue>
        where TValue : struct, INumber<TValue>
        => TSelf.Zero;

    private static TSelf OneOf<TSelf, TValue>()
        where TSelf : struct, INumericValueObject<TSelf, TValue>
        where TValue : struct, INumber<TValue>
        => TSelf.One;

    private static bool IsZero<TSelf, TValue>(TSelf value)
        where TSelf : struct, INumericValueObject<TSelf, TValue>
        where TValue : struct, INumber<TValue>
        => value.IsZero;

    private static TSelf AbsOf<TSelf, TValue>(TSelf value)
        where TSelf : struct, INumericValueObject<TSelf, TValue>
        where TValue : struct, INumber<TValue>
        => TSelf.Abs(value);

    private static TSelf MinOf<TSelf, TValue>(TSelf left, TSelf right)
        where TSelf : struct, INumericValueObject<TSelf, TValue>
        where TValue : struct, INumber<TValue>
        => TSelf.Min(left, right);

    private static TSelf MaxOf<TSelf, TValue>(TSelf left, TSelf right)
        where TSelf : struct, INumericValueObject<TSelf, TValue>
        where TValue : struct, INumber<TValue>
        => TSelf.Max(left, right);

    private static TSelf SumOf<TSelf, TValue>(IEnumerable<TSelf> values)
        where TSelf : struct, INumericValueObject<TSelf, TValue>
        where TValue : struct, INumber<TValue>
        => TSelf.Sum(values);
}
