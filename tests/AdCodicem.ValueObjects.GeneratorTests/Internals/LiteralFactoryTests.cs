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

    private sealed class NullText
    {
        public override string? ToString() => null;
    }
}
