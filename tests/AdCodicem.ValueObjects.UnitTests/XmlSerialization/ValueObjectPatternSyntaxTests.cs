using System.Globalization;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.UnitTests.XmlSerialization;

/// <summary>
/// The reader of a .NET pattern every package that takes one out of .NET shares, tested on its tree directly: it reads
/// what .NET reads alike, and refuses what it cannot read with certainty (src/Shared/ValueObjectPatternSyntax.cs).
/// </summary>
public class ValueObjectPatternSyntaxTests
{
    public static TheoryData<string, string> Read => new()
    {
        { string.Empty, "seq()" },
        { "^a$", "seq(StartOfLine,'a',EndOfLine)" },
        { "a|b|", "alt(seq('a'),seq('b'),seq())" },
        { "(?:a|b)c", "seq(nocap(alt(seq('a'),seq('b'))),'c')" },
        { "(?<word>a)(?'other_1'b)(c)", "seq(group<word>(seq('a')),group<other_1>(seq('b')),group(seq('c')))" },
        { "a*b+c?d{2}e{2,}f{2,3}", "seq(rep{0,}('a'),rep{1,}('b'),rep{0,1}('c'),rep{2,2}('d'),rep{2,}('e'),rep{2,3}('f'))" },
        { "a*?b+?c??d{2,3}?", "seq(rep{0,}?('a'),rep{1,}?('b'),rep{0,1}?('c'),rep{2,3}?('d'))" },
        { "(ab)+.", "seq(rep{1,}(group(seq('a','b'))),any)" },
        { @"\.\$\^\\\/\-\#\ ", @"seq('.','$','^','\','/','-','#',' ')" },
        { @"\n\r\t\f\v\a\e\x41\u00E9", @"seq('\u000A','\u000D','\u0009','\u000C','\u000B','\u0007','\u001B','A','é')" },
        { @"\d\D\w\W\s\S\p{Lu}\P{IsGreek}", @"seq(\d,\D,\w,\W,\s,\S,\p{Lu},\P{IsGreek})" },
        { @"\A\z\Z\b\B^$", "seq(StartOfText,EndOfText,EndOfTextOrFinalNewline,WordBoundary,NotWordBoundary,StartOfLine,EndOfLine)" },
        { "[a-z0-9_]", "seq([a-z,0-9,'_'])" },
        { "[^a]", "seq([^'a'])" },
        { "[]a]", "seq([']','a'])" },
        { "[^]a]", "seq([^']','a'])" },
        { "[a-][-a]", "seq(['a','-'],['-','a'])" },
        { @"[\d\]\-\b\x41-\x5A\p{L}]", @"seq([\d,']','-','\u0008',A-Z,\p{L}])" },
        { "[a-z-[aeiou]]", "seq([a-z-['a','e','i','o','u']])" },
        { "[^a-z-[^b]]", "seq([^a-z-[^'b']])" },
        { "[a-z-]", "seq([a-z,'-'])" },
        { "[.$^|(){}*+?]", "seq(['.','$','^','|','(',')','{','}','*','+','?'])" },
        { "a{", "seq('a','{')" },
        { "a{,2}", "seq('a','{',',','2','}')" },
        { "a{2", "seq('a','{','2')" },
        { "a{2,", "seq('a','{','2',',')" },
        { "a{x}", "seq('a','{','x','}')" },
        { "}]", "seq('}',']')" },
        { "x{0,0}", "seq(rep{0,0}('x'))" },
        { "x{2147483647}", "seq(rep{2147483647,2147483647}('x'))" },
    };

    [Theory]
    [MemberData(nameof(Read))]
    public void A_pattern_in_the_subset_is_read_as_dotnet_reads_it(string pattern, string tree)
    {
        _ = new Regex(pattern);

        ValueObjectPatternSyntax.TryParse(pattern, out var root).Should().BeTrue();

        Render(root!).Should().Be(tree);
    }

    public static TheoryData<string> Refused => new()
    {
        // Lookarounds, atomic and conditional groups, inline options and comments.
        "(?=a)", "(?!a)", "(?<=a)", "(?<!a)", "(?>a)", "(?(a)b|c)", "(?i)a", "(?i:a)", "(?#note)",

        // A balancing group, an empty name, a name never closed, a name of other characters.
        "(?<a-b>c)", "(?<>a)", "(?<a", "(?<a.b>c)",

        // Backreferences, octal and control escapes, \G, an escape .NET does not know, one at the end.
        @"(a)\1", @"(?<a>x)\k<a>", @"\0", @"\cA", @"\G", @"\q", @"\_", @"\é", @"a\",

        // Hexadecimal escapes too short or not hexadecimal, a category without braces or name.
        @"\x4", @"\u12", @"\xZZ", @"\p", @"\p{}", @"\pL", @"\p{L",

        // A quantifier following nothing, a nested one, a quantified anchor, a range written backwards, a count too large.
        "*a", "+", "?", "{2}", "a**", "a{2}{3}", "a*?+", "a?{2}", "^*", "$?", @"\b+", "a{3,1}", "a{99999999999}", "a{99999999999,}", "a{2,99999999999}", "{99999999999}", "a*??", "a{2}+",

        // Groups and classes left open or closed twice.
        "(", "(a", "a)", "(a))", "[", "[a", "[]", "[^", "[a-", "[a-z", "[a-z-",

        // Inside a class: a range beside a class escape, backwards, followed by a '-', a '[' starting nothing, an unknown escape.
        @"[a-\d]", @"[\d-z]", "[z-a]", "[a-z-0]", "[[]", "[a[]", @"[\q]", @"[\", @"[\A]",

        // A subtraction that does not close its class, or is itself left open.
        "[a-z-[b]x]", "[a-[b", "[a-[b]",
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void A_pattern_outside_the_subset_is_refused(string pattern)
    {
        ValueObjectPatternSyntax.TryParse(pattern, out var root).Should().BeFalse();

        root.Should().BeNull();
    }

    [Fact]
    public void A_class_escape_says_whether_it_is_negated()
    {
        ValueObjectPatternSyntax.TryParse(@"\d\P{L}", out var root).Should().BeTrue();

        var escapes = ((PatternSequence)root!).Items.Cast<PatternClassEscape>().ToList();
        escapes[0].Negated.Should().BeFalse();
        escapes[1].Negated.Should().BeTrue();
        escapes[1].Category.Should().Be("L");
    }

    /// <summary>Writes a tree in a compact form the cases above compare.</summary>
    internal static string Render(PatternNode node) => node switch
    {
        PatternAlternation alternation => $"alt({string.Join(',', alternation.Alternatives.Select(Render))})",
        PatternSequence sequence => $"seq({string.Join(',', sequence.Items.Select(Render))})",
        PatternGroup { Name: { } name } group => $"group<{name}>({Render(group.Content)})",
        PatternGroup { Capturing: true } group => $"group({Render(group.Content)})",
        PatternGroup group => $"nocap({Render(group.Content)})",
        PatternQuantifier quantifier => string.Create(
            CultureInfo.InvariantCulture,
            $"rep{{{quantifier.Minimum},{quantifier.Maximum}}}{(quantifier.Lazy ? "?" : string.Empty)}({Render(quantifier.Item)})"),
        PatternLiteral literal => $"'{Character(literal.Character)}'",
        PatternRange range => $"{range.From}-{range.To}",
        PatternAnyCharacter => "any",
        PatternClassEscape { Category: { } category } escape => $@"\{escape.Letter}{{{category}}}",
        PatternClassEscape escape => $@"\{escape.Letter}",
        PatternCharacterClass characterClass => $"[{(characterClass.Negated ? "^" : string.Empty)}{string.Join(',', characterClass.Items.Select(Render))}"
            + (characterClass.Subtraction is { } subtraction ? $"-{Render(subtraction)}" : string.Empty) + "]",
        PatternAnchor anchor => anchor.Kind.ToString(),
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    private static string Character(char character)
        => char.IsControl(character) ? string.Create(CultureInfo.InvariantCulture, $"\\u{(int)character:X4}") : character.ToString();
}
