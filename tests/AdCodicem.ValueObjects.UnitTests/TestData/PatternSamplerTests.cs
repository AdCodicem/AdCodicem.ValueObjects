using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Testing.Data;

namespace AdCodicem.ValueObjects.UnitTests.TestData;

/// <summary>
/// The built-in pattern sampler on every node of the tree the shared reader reads, on the windows a draw can and cannot fit,
/// and on what it leaves to rejection sampling.
/// </summary>
public class PatternSamplerTests
{
    /// <summary>Patterns of every node kind the sampler draws, each drawn as a string the pattern matches whole.</summary>
    public static TheoryData<string> Drawable =>
    [
        "abc",
        @"\.\-\x41B\t\n",
        @"\d\w\s",
        @"\D\W\S",
        "[^a-z]",
        "[a-fA-F0-9]",
        "[a-fd-z]",
        "[ab]",
        "[a-z-[aeiou]]",
        "[a-z-[0-9a-c]]",
        "[a-c-[x-z]]",
        "[a-z-[a]]",
        "[a-z-[x-z]]",
        @"[^\d\s]",
        @"[\W\d]",
        "a.c",
        "ab?c",
        "ab*c",
        "ab+c",
        "a{3}",
        "a{2,}",
        "a{2,4}",
        "a+?b*?c??",
        "cat|dog|bird",
        "(ab)",
        "(?:ab)",
        "(?<name>ab)",
        "(?'name'ab)",
        "^ab$",
        @"\Aab\z",
        @"ab\Z",
        "((a|bc){2,3}d?)+",
        "(a?)*",
        "x(?:)+y",
    ];

    [Theory]
    [MemberData(nameof(Drawable))]
    public void Every_string_drawn_is_matched_whole_by_its_pattern(string pattern)
    {
        var sampler = PatternSampler.For(pattern);
        var whole = new Regex($@"\A(?:{pattern})\z", RegexOptions.CultureInvariant);
        var random = new Random(31);

        sampler.Should().NotBeNull("{0} is inside the subset", pattern);
        for (var draw = 0; draw < 200; draw++)
        {
            var text = sampler!.Draw(random, 0, 40, 8);

            text.Should().NotBeNull();
            whole.IsMatch(text!).Should().BeTrue("'{0}' was drawn from {1}", text, pattern);
            text!.Length.Should().BeLessThanOrEqualTo(40);
        }
    }

    [Theory]
    [InlineData("[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}", 15)]
    [InlineData("[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}", 34)]
    [InlineData(@"[^@\s]+@[^@\s]+\.[^@\s]+", 254)]
    [InlineData("(a?)*", 5)]
    [InlineData("[a-z]+", 100)]
    public void A_window_of_one_length_draws_a_string_of_that_length(string pattern, int length)
    {
        var text = PatternSampler.For(pattern)!.Draw(new Random(32), length, length, 4);

        text.Should().HaveLength(length);
        Regex.IsMatch(text!, $@"\A(?:{pattern})\z").Should().BeTrue();
    }

    [Theory]
    [InlineData("(a|abc)", 2)]
    [InlineData("(ab)+", 3)]
    [InlineData("abc", 2)]
    [InlineData("(ab|abcd){1,3}", 5)]
    public void A_window_no_match_fits_draws_nothing(string pattern, int length)
        => PatternSampler.For(pattern)!.Draw(new Random(33), length, length, 4).Should().BeNull();

    [Fact]
    public void An_open_quantifier_adds_no_more_repetitions_than_asked_unless_the_window_asks_for_more()
    {
        var sampler = PatternSampler.For("a+")!;
        var random = new Random(34);

        Enumerable.Range(0, 200).Select(_ => sampler.Draw(random, 0, 1000, 3)!.Length).Distinct().Should().BeEquivalentTo([1, 2, 3, 4]);
        sampler.Draw(random, 50, 1000, 3).Should().HaveLength(50);
    }

    [Fact]
    public void Word_boundaries_draw_nothing_and_are_left_to_the_pattern()
    {
        PatternSampler.For(@"\bab\b")!.Draw(new Random(35), 0, 10, 4).Should().Be("ab");
        PatternSampler.For(@"a\Bb")!.Draw(new Random(35), 0, 10, 4).Should().Be("ab");
    }

    [Theory]
    [InlineData(@"\p{Lu}{3}")]
    [InlineData(@"[\p{L}]")]
    [InlineData(@"[a-z-[\p{Lu}]]")]
    [InlineData("[a-[a]]")]
    [InlineData(@"a|\p{L}")]
    [InlineData(@"(\p{L})")]
    [InlineData("[^ -~]")]
    [InlineData("(?=a)a")]
    [InlineData("(unclosed")]
    public void A_pattern_outside_the_subset_has_no_sampler(string pattern)
        => PatternSampler.For(pattern).Should().BeNull();

    [Fact]
    public void A_pattern_is_read_once()
    {
        PatternSampler.For("x{2}").Should().BeSameAs(PatternSampler.For("x{2}"));
        PatternSampler.Loosely("x{3}").Should().BeSameAs(PatternSampler.Loosely("x{3}"));
    }

    [Theory]
    [InlineData("@acme\\.com$", 12, "^[0-9A-Za-z]{3}@acme\\.com$")]
    [InlineData(@"\Aab", 6, "^ab[0-9A-Za-z]{4}$")]
    [InlineData(@"^\d{3}-\d{4}", 10, "^[0-9]{3}-[0-9]{4}[0-9A-Za-z]{2}$")]
    [InlineData("(a|abc)", 5, "^[0-9A-Za-z]*(a|abc)[0-9A-Za-z]*$")]
    public void A_match_shorter_than_the_window_is_padded_at_an_end_no_anchor_holds(string pattern, int length, string expected)
    {
        var sampler = PatternSampler.For(pattern)!;
        var random = new Random(45);

        for (var draw = 0; draw < 100; draw++)
        {
            sampler.Draw(random, length, length, 4).Should().HaveLength(length).And.MatchRegex(expected);
        }
    }

    [Fact]
    public void A_match_held_at_both_ends_or_by_one_alternative_at_an_end_is_never_padded()
    {
        PatternSampler.For("^ab$")!.Draw(new Random(46), 5, 5, 4).Should().BeNull();
        PatternSampler.For(@"\Aab\z")!.Draw(new Random(46), 5, 5, 4).Should().BeNull();
        PatternSampler.For("^a|b$")!.Draw(new Random(46), 3, 3, 4).Should().BeNull();
        PatternSampler.For("abc")!.Draw(new Random(46), 5, 2, 4).Should().BeNull("no match fits a window shorter than its own");
    }

    [Theory]
    [InlineData(@"^(?=.*\d)[A-Za-z\d]{8}$", @"^[A-Za-z\d]{8}$")]
    [InlineData("(?<!x)a(?!b)", "a")]
    [InlineData("(?=a(b)c)d", "d")]
    [InlineData("(?=[)])x", "x")]
    [InlineData("(?![]a])x", "x")]
    [InlineData("(?=[](])x", "x")]
    [InlineData(@"(?=[\](])x", "x")]
    [InlineData("(?=[a-z-[aeiou]])x", "x")]
    [InlineData("[a-z-[(]](?!b)", "[a-z-[(]]")]
    [InlineData(@"(?=\))x", "x")]
    [InlineData("[(?=]x", "[(?=]x")]
    [InlineData("[^]a]x", "[^]a]x")]
    [InlineData(@"\(?=x", @"\(?=x")]
    [InlineData("(?<name>a)", "(?<name>a)")]
    [InlineData("(?=a", "")]
    [InlineData("[a", "[a")]
    [InlineData("a\\", "a\\")]
    public void A_pattern_read_loosely_loses_its_lookarounds(string pattern, string expected)
        => PatternSampler.WithoutLookarounds(pattern).Should().Be(expected);

    [Theory]
    [InlineData(@"^\p{Lu}{3}$", "^[A-Z]{3}$")]
    [InlineData(@"^[\p{Nd}x]{2}$", "^[0-9x]{2}$")]
    [InlineData(@"^\P{L}$", "^[^A-Za-z]$")]
    [InlineData(@"^(?=.*\d)[A-Za-z\d]{8}$", @"^[A-Za-z\d]{8}$")]
    public void A_pattern_read_loosely_draws_its_categories_from_printable_ASCII(string pattern, string expected)
    {
        var sampler = PatternSampler.Loosely(pattern);
        var random = new Random(47);

        sampler.Should().NotBeNull();
        for (var draw = 0; draw < 100; draw++)
        {
            sampler!.Draw(random, 0, 20, 4).Should().MatchRegex(expected).And.MatchRegex("^[ -~]*$");
        }
    }

    [Theory]
    [InlineData(@"\p{IsCyrillic}")]
    [InlineData(@"\p{Bogus}")]
    [InlineData(@"(a)\1")]
    public void A_pattern_even_a_loose_reading_leaves_outside_has_no_sampler(string pattern)
        => PatternSampler.Loosely(pattern).Should().BeNull();
}
