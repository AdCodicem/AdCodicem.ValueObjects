using AdCodicem.ValueObjects.Identifiers;

namespace AdCodicem.ValueObjects.UnitTests.Identifiers;

public class CrockfordBase32Tests
{
    [Fact]
    public void The_alphabet_holds_thirty_two_distinct_symbols()
    {
        CrockfordBase32.Alphabet.Should().HaveLength(32);
        CrockfordBase32.Alphabet.Distinct().Should().HaveCount(32);
    }

    /// <summary>
    /// The property the whole design leans on: because the alphabet ascends in ASCII, an ordinal comparison of
    /// two encoded values reproduces the comparison of the numbers behind them. That is what lets the time
    /// bucket at the head of an identifier order a database index chronologically with no decoding at all.
    /// </summary>
    [Fact]
    public void The_alphabet_ascends_so_ordinal_order_reproduces_numeric_order()
    {
        for (var value = 1; value < 32; value++)
        {
            CrockfordBase32.Alphabet[value].Should().BeGreaterThan(
                CrockfordBase32.Alphabet[value - 1],
                "symbol {0} must sort after symbol {1}",
                value,
                value - 1);
        }
    }

    [Fact]
    public void The_alphabet_excludes_the_characters_a_reader_confuses()
    {
        CrockfordBase32.Alphabet.Should().NotContain("I").And.NotContain("L").And.NotContain("O").And.NotContain("U");
    }

    [Fact]
    public void Encode_and_Decode_round_trip_every_value()
    {
        for (var value = 0; value < 32; value++)
        {
            CrockfordBase32.Decode(CrockfordBase32.Encode(value)).Should().Be(value);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(32)]
    public void Encode_refuses_a_value_that_does_not_fit_in_five_bits(int value)
    {
        var act = () => CrockfordBase32.Encode(value);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Decode_accepts_either_case()
    {
        foreach (var symbol in CrockfordBase32.Alphabet)
        {
            CrockfordBase32.Decode(char.ToLowerInvariant(symbol)).Should().Be(CrockfordBase32.Decode(symbol));
        }
    }

    [Theory]
    [InlineData('I', 1)]
    [InlineData('i', 1)]
    [InlineData('L', 1)]
    [InlineData('l', 1)]
    [InlineData('O', 0)]
    [InlineData('o', 0)]
    public void Decode_folds_the_Crockford_aliases(char alias, int expected)
    {
        CrockfordBase32.Decode(alias).Should().Be(expected);
    }

    [Theory]
    [InlineData('U')]
    [InlineData('u')]
    [InlineData('-')]
    [InlineData(' ')]
    [InlineData('_')]
    [InlineData('é')]
    [InlineData('あ')]
    public void Decode_rejects_what_is_not_a_symbol(char character)
    {
        CrockfordBase32.Decode(character).Should().Be(-1);
        CrockfordBase32.IsSymbol(character).Should().BeFalse();
    }

    [Fact]
    public void Canonicalize_settles_after_one_pass()
    {
        for (var character = (char)0; character < 128; character++)
        {
            var once = CrockfordBase32.Canonicalize(character);

            CrockfordBase32.Canonicalize(once).Should().Be(once, "normalization must be idempotent");
        }
    }

    [Fact]
    public void Canonicalize_carries_an_unusable_character_through_rather_than_rejecting_it()
    {
        CrockfordBase32.Canonicalize('U').Should().Be('U');
    }
}
