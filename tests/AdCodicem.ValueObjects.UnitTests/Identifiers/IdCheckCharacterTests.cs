using System.Text;
using AdCodicem.ValueObjects.Identifiers;

namespace AdCodicem.ValueObjects.UnitTests.Identifiers;

/// <summary>
/// The check character promises something precise, so it is tested exhaustively rather than by sampling: a
/// checksum that turns out to be weaker than documented is worse than none, because callers stop looking.
/// </summary>
public class IdCheckCharacterTests
{
    private const string Prefix = "acc";
    private const string Body = "2k7x9wqmz4h3n8vyb6tcr0fgj";

    [Fact]
    public void Compute_is_deterministic()
    {
        var first = IdCheckCharacter.Compute(Prefix, Body);
        var second = IdCheckCharacter.Compute(Prefix, Body);

        first.Should().Be(second);
        CrockfordBase32.IsSymbol(first).Should().BeTrue();
    }

    /// <summary>
    /// The guarantee the documentation states first: no single mistyped character can ever go unnoticed.
    /// </summary>
    [Fact]
    public void Every_single_character_substitution_is_detected()
    {
        var expected = IdCheckCharacter.Compute(Prefix, Body);

        for (var position = 0; position < Body.Length; position++)
        {
            foreach (var replacement in CrockfordBase32.Alphabet)
            {
                if (replacement == Body[position])
                {
                    continue;
                }

                var corrupted = Replace(Body, position, replacement);

                IdCheckCharacter.Compute(Prefix, corrupted).Should().NotBe(
                    expected,
                    "substituting '{0}' at position {1} must change the check character",
                    replacement,
                    position);
            }
        }
    }

    /// <summary>
    /// The second guarantee, stated with its exception rather than rounded up: adjacent transpositions are
    /// caught unless the two Crockford values differ by exactly 16, which is a consequence of working modulo a
    /// power of two and is documented as such.
    /// </summary>
    [Fact]
    public void Adjacent_transpositions_are_detected_except_when_the_values_differ_by_sixteen()
    {
        var expected = IdCheckCharacter.Compute(Prefix, Body);

        for (var position = 0; position < Body.Length - 1; position++)
        {
            var left = CrockfordBase32.Decode(Body[position]);
            var right = CrockfordBase32.Decode(Body[position + 1]);

            if (left == right)
            {
                continue;
            }

            var transposed = Transpose(Body, position);
            var detected = IdCheckCharacter.Compute(Prefix, transposed) != expected;

            detected.Should().Be(
                Math.Abs(left - right) != 16,
                "transposing positions {0} and {1} (values {2} and {3})",
                position,
                position + 1,
                left,
                right);
        }
    }

    [Fact]
    public void Transpositions_of_values_sixteen_apart_are_the_only_ones_that_slip_through()
    {
        var undetected = 0;
        var total = 0;

        for (var left = 0; left < 32; left++)
        {
            for (var right = 0; right < 32; right++)
            {
                if (left == right)
                {
                    continue;
                }

                var body = $"{CrockfordBase32.Encode(left)}{CrockfordBase32.Encode(right)}{Body[2..]}";
                var swapped = Transpose(body, 0);

                total++;
                if (IdCheckCharacter.Compute(Prefix, body) == IdCheckCharacter.Compute(Prefix, swapped))
                {
                    undetected++;
                    Math.Abs(left - right).Should().Be(16);
                }
            }
        }

        total.Should().Be(992);
        undetected.Should().Be(32, "the 16 unordered pairs sixteen apart, counted in both orders");
    }

    /// <summary>
    /// A body copied from one identifier type onto another must not validate, even before anyone knows which
    /// prefix was expected. That is what makes the check character useful to the polymorphic parser.
    /// </summary>
    [Fact]
    public void The_prefix_changes_the_check_character()
    {
        IdCheckCharacter.Compute("acc", Body).Should().NotBe(IdCheckCharacter.Compute("cus", Body));
    }

    [Fact]
    public void The_prefix_seed_reaches_every_possible_value()
    {
        var seen = new HashSet<char>();

        for (var first = 'a'; first <= 'z'; first++)
        {
            for (var second = 'a'; second <= 'z'; second++)
            {
                seen.Add(IdCheckCharacter.Compute($"{first}{second}", Body));
            }
        }

        seen.Should().HaveCount(32, "a seed that clustered would let a body drift between prefixes unnoticed");
    }

    [Fact]
    public void A_character_that_does_not_decode_contributes_nothing()
    {
        // Validation refuses such a character before the check is ever consulted; this only pins down that
        // computing over one does not throw.
        var act = () => IdCheckCharacter.Compute(Prefix, "U!U!U");

        act.Should().NotThrow();
    }

    private static string Replace(string text, int position, char replacement)
    {
        var builder = new StringBuilder(text);
        builder[position] = replacement;

        return builder.ToString();
    }

    private static string Transpose(string text, int position)
    {
        var builder = new StringBuilder(text);
        (builder[position], builder[position + 1]) = (builder[position + 1], builder[position]);

        return builder.ToString();
    }
}
