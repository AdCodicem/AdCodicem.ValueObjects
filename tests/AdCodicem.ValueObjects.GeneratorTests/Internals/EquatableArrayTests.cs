using System.Collections;
using AdCodicem.ValueObjects.Generators.Internal;

namespace AdCodicem.ValueObjects.GeneratorTests.Internals;

/// <summary>
/// The members of <see cref="EquatableArray{T}"/> no generator run reaches.
/// </summary>
/// <remarks>
/// The pipeline compares models through <see cref="IEquatable{T}"/> and never hashes, boxes or default-constructs
/// one of these arrays, so <c>IncrementalityTests</c> reach the typed comparison and nothing else. The rest is
/// what any equatable struct owes its callers, and is tested here directly.
/// </remarks>
public sealed class EquatableArrayTests
{
    [Fact]
    public void A_default_array_behaves_as_an_empty_one()
    {
        var unset = default(EquatableArray<string>);

        List<string> items = [.. unset];

        unset.Length.Should().Be(0);
        unset.IsEmpty.Should().BeTrue();
        items.Should().BeEmpty();
        unset.Equals(EquatableArray<string>.Empty).Should().BeTrue();
        unset.GetHashCode().Should().Be(EquatableArray<string>.Empty.GetHashCode());
    }

    [Fact]
    public void Arrays_compare_by_content_even_when_boxed()
    {
        var array = EquatableArray<string>.From(["a", "b"]);
        object same = EquatableArray<string>.From(["a", "b"]);
        object different = EquatableArray<string>.From(["a", "c"]);
        object shorter = EquatableArray<string>.From(["a"]);

        array.Equals(same).Should().BeTrue();
        array.Equals(different).Should().BeFalse();
        array.Equals(shorter).Should().BeFalse();
        array.Equals("a").Should().BeFalse();
    }

    [Fact]
    public void Arrays_with_the_same_content_hash_alike()
    {
        var first = EquatableArray<string>.From(["a", "b"]);
        var second = EquatableArray<string>.From(["a", "b"]);

        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void An_array_enumerates_through_the_non_generic_interface()
    {
        IEnumerable array = EquatableArray<string>.From(["a", "b"]);

        var items = new List<object?>();
        foreach (var item in array)
        {
            items.Add(item);
        }

        items.Should().Equal("a", "b");
    }
}
