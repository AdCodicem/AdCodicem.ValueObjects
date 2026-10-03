using AdCodicem.ValueObjects.Generators.Internal;
using AdCodicem.ValueObjects.Generators.Model;

namespace AdCodicem.ValueObjects.GeneratorTests.Internals;

/// <summary>
/// What <see cref="LiteralFactory"/> does with a value no attribute argument can carry.
/// </summary>
/// <remarks>
/// An attribute argument is a primitive, a string, a type or an array, and the text of each is never null. A
/// value whose text is null can therefore only be handed over directly.
/// </remarks>
public sealed class LiteralFactoryTests
{
    [Fact]
    public void A_value_whose_text_is_null_converts_to_nothing()
    {
        LiteralFactory.TryCreate(UnderlyingType.String, new NullText(), out var literal).Should().BeFalse();

        literal.Should().BeEmpty();
    }

    /// <summary>
    /// On .NET Framework, where the compiler may run, the round-trip form of a real does not always read back as the
    /// value; on .NET it does. Text that names a neighbouring value can therefore only be handed over directly, and
    /// gives way to the digits that always name the value itself.
    /// </summary>
    [Fact]
    public void A_real_whose_text_names_a_neighbouring_value_is_written_with_every_digit()
    {
        var third = 1d / 3d;
        var tenth = 0.1f;

        LiteralFactory.ReadsBack(third, "0.333333333333333").Should().Be("0.33333333333333331");
        LiteralFactory.ReadsBack(tenth, "0.1000001").Should().Be("0.100000001");
        LiteralFactory.ReadsBack(third, "0.3333333333333333").Should().Be("0.3333333333333333");
        LiteralFactory.ReadsBack(tenth, "0.1").Should().Be("0.1");
        LiteralFactory.RoundTrip(third).Should().Be("0.3333333333333333");
        LiteralFactory.RoundTrip(tenth).Should().Be("0.1");
    }

    /// <summary>
    /// The generator asks whether text is readable only once the one form of the type refused it, which a string never
    /// does: any text is a string.
    /// </summary>
    [Fact]
    public void Any_text_is_readable_as_a_string()
    {
        LiteralFactory.IsReadable(UnderlyingType.String, "anything at all").Should().BeTrue();
    }

    private sealed class NullText
    {
        public override string? ToString() => null;
    }
}
