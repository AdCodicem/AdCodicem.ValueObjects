using AdCodicem.ValueObjects.Testing.Data;

namespace AdCodicem.ValueObjects.UnitTests.TestData;

/// <summary>
/// The shrinks the sampler proposes for a property-based testing library: the declared values first, ranked, then the
/// shrinks of the underlying value the type accepts as they are, its normalizer leaving them unchanged.
/// </summary>
public class ShrinkTests
{
    [Fact]
    public void A_value_shrinks_to_the_declared_values_then_to_the_shrinks_of_its_underlying_value_the_type_accepts()
    {
        var shrinks = ValueObjectSampler.Shrink<Quantity, short>(Quantity.Create(439), Halve).ToList();

        shrinks.Select(quantity => quantity.Value).Should().Equal(0, 219, 438);
    }

    [Fact]
    public void The_declared_values_are_its_minimum_its_first_known_value_and_its_example_in_that_order()
    {
        ValueObjectSampler.Shrink<Port, ushort>(Port.Create(9000)).Select(port => port.Value).Should().Equal(1, 8080);
        ValueObjectSampler.Shrink<DocumentStatus, string>(DocumentStatus.Final).Should().Equal(DocumentStatus.Draft);
        ValueObjectSampler.Shrink<PageNumber, int>(PageNumber.Create(7)).Select(page => page.Value).Should().Equal([1], "its minimum is its known value, proposed once");
        ValueObjectSampler.Shrink<Declared<ExampleAtMinimum, int>, int>(Declared<ExampleAtMinimum, int>.Create(50))
            .Select(value => value.Value).Should().Equal([5], "its example is its minimum, proposed once");
        ValueObjectSampler.Shrink<Declared<RefusedMinimum, int>, int>(Declared<RefusedMinimum, int>.Create(50))
            .Select(value => value.Value).Should().Equal([1], "a minimum its rules refuse is no value to shrink to");
    }

    [Fact]
    public void A_declared_value_shrinks_only_to_the_declared_values_ranked_before_it()
    {
        ValueObjectSampler.Shrink<Port, ushort>(Port.Create(8080), HalvePort).Select(port => port.Value).Should().Equal(1);
        ValueObjectSampler.Shrink<Port, ushort>(Port.Create(1), HalvePort).Should().BeEmpty();
        ValueObjectSampler.Shrink<Quantity, short>(Quantity.Create(0), Halve).Should().BeEmpty();
    }

    [Fact]
    public void A_shrink_its_normalizer_changes_is_dropped()
    {
        var iban = Iban.Create("DE89370400440532013000");

        var shrinks = ValueObjectSampler.Shrink<Iban, string>(iban, text => [text.ToLowerInvariant(), $" {text} ", text[..^1]]).ToList();

        shrinks.Should().Equal(Iban.Example);
        ValueObjectSampler.Shrink<Grade, char>(Grade.Create('C'), _ => ['b']).Should()
            .Equal([Grade.Create('A')], "'b' is upper-cased to 'B', whose own shrink 'c' would lead back to 'C'");
    }

    [Fact]
    public void Every_chain_of_shrinks_ends()
    {
        var random = new Random(36);
        var sampler = new ValueObjectSampler(random);

        Ends<Quantity, short>(sampler, random, Halve);
        Ends<Port, ushort>(sampler, random, HalvePort);
        Ends<Ordering.OrderReference, string>(sampler, random, text => text.Length > 0 ? [text[..^1], text[1..], text.ToUpperInvariant()] : []);
        Ends<Iban, string>(sampler, random, text => text.Length > 0 ? [text[..^1], text.ToLowerInvariant()] : []);
        Ends<Grade, char>(sampler, random, TowardsLowerCase);
    }

    private static IEnumerable<short> Halve(short value) => value == 0 ? [] : [(short)(value / 2), (short)(value - 1)];

    private static IEnumerable<ushort> HalvePort(ushort value) => value == 0 ? [] : [(ushort)(value / 2), (ushort)(value - 1)];

    /// <summary>Shrinks a character as FsCheck does, towards 'a', 'b' and 'c': an upper-case letter to all three.</summary>
    private static IEnumerable<char> TowardsLowerCase(char value) => "abc".Where(simpler => char.IsUpper(value) || simpler < value);

    private static void Ends<TSelf, TValue>(ValueObjectSampler sampler, Random random, Func<TValue, IEnumerable<TValue>> shrink)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        for (var start = 0; start < 100; start++)
        {
            var current = sampler.Next<TSelf, TValue>();
            var visited = new HashSet<TSelf> { current };
            while (ValueObjectSampler.Shrink(current, shrink).ToList() is { Count: > 0 } shrinks)
            {
                current = shrinks[random.Next(shrinks.Count)];
                visited.Add(current).Should().BeTrue("a chain of shrinks of {0} never comes back to a value it left", typeof(TSelf).Name);
                visited.Count.Should().BeLessThan(5000, "a chain of shrinks of {0} ends", typeof(TSelf).Name);
            }
        }
    }
}
