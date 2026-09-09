using System.Collections.Frozen;

namespace AdCodicem.ValueObjects.UnitTests;

public class ComparisonTests
{
    [Fact]
    public void Two_value_objects_holding_the_same_value_are_equal()
    {
        var left = EmailAddress.Create("Ada@Example.COM");
        var right = EmailAddress.Create("  ada@example.com  ");

        left.Should().Be(right);
        (left == right).Should().BeTrue();
        (left != right).Should().BeFalse();
        left.Equals((object)right).Should().BeTrue();
    }

    [Fact]
    public void Equality_honours_the_declared_comparison()
    {
        var lower = Ordering.OrderReference.Create("ord-42");
        var upper = Ordering.OrderReference.Create("ORD-42");

        lower.Should().Be(upper);
        lower.GetHashCode().Should().Be(upper.GetHashCode());
    }

    [Fact]
    public void Value_objects_of_different_types_are_never_equal()
    {
        var email = EmailAddress.Create("ada@example.com");

        email.Equals("ada@example.com").Should().BeFalse();
        email.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Ordering_follows_the_underlying_value()
    {
        Amount[] amounts = [Amount.Create(30m), Amount.Create(10m), Amount.Create(20m)];

        Array.Sort(amounts);

        amounts.Select(amount => amount.Value).Should().ContainInOrder(10m, 20m, 30m);
    }

    [Fact]
    public void The_relational_operators_agree_with_CompareTo()
    {
        var small = Amount.Create(10m);
        var large = Amount.Create(20m);

        (small < large).Should().BeTrue();
        (small <= large).Should().BeTrue();
        (large > small).Should().BeTrue();
        (large >= small).Should().BeTrue();
        (small >= Amount.Create(10m)).Should().BeTrue();
        small.CompareTo(large).Should().BeNegative();
    }

    [Fact]
    public void The_non_generic_comparison_rejects_a_foreign_type()
    {
        var act = () => ((IComparable)Amount.Create(1m)).CompareTo("nonsense");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_null_comparand_sorts_first()
    {
        ((IComparable)Amount.Create(1m)).CompareTo(null).Should().BePositive();
    }

    [Fact]
    public void A_value_object_is_usable_as_a_dictionary_key()
    {
        var counts = new Dictionary<CountryCode, int>
        {
            [CountryCode.France] = 1,
        };

        counts[CountryCode.Create("  fr  ")].Should().Be(1);
        counts.ContainsKey(CountryCode.Belgium).Should().BeFalse();
    }

    [Fact]
    public void A_value_object_is_usable_in_a_frozen_set()
    {
        var served = CountryCode.KnownValues.ToFrozenSet();

        served.Contains(CountryCode.Create("BE")).Should().BeTrue();
        served.Count.Should().Be(3);
    }

    [Fact]
    public void The_struct_stays_the_size_of_its_underlying_value()
    {
        // The whole point of a struct value object: wrapping costs nothing in memory.
        System.Runtime.CompilerServices.Unsafe.SizeOf<Amount>().Should().Be(sizeof(decimal));
        System.Runtime.CompilerServices.Unsafe.SizeOf<Quantity>().Should().Be(sizeof(short));
        System.Runtime.CompilerServices.Unsafe.SizeOf<CustomerId>().Should().Be(16);
    }
}
