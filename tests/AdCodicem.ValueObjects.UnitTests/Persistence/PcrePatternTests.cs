using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.MongoDB;

namespace AdCodicem.ValueObjects.UnitTests.Persistence;

/// <summary>
/// The writer of a .NET pattern in the PCRE2 dialect a MongoDB server checks a <c>$jsonSchema</c> pattern with: what it
/// writes, as .NET reads it, and what it leaves out because the server could refuse a value .NET accepts. The
/// integration suite runs the written patterns against a server.
/// </summary>
/// <remarks>
/// A class escape is written as the characters of the Basic Multilingual Plane .NET matches it with, which this class
/// lists itself, from <see cref="CharUnicodeInfo"/> and the definitions .NET documents, apart from the writer, which asks
/// .NET's regular expression engine: <c>&lt;Nd&gt;</c> in an expected pattern stands for that list.
/// </remarks>
public sealed class PcrePatternTests
{
    /// <summary>The characters .NET documents each escape as, by the token an expected pattern names it with.</summary>
    private static readonly Dictionary<string, Func<char, bool>> Escapes = new(StringComparer.Ordinal)
    {
        ["<Nd>"] = static c => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.DecimalDigitNumber,
        ["<w>"] = static c => CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
            or UnicodeCategory.NonSpacingMark or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation,
        ["<s>"] = char.IsWhiteSpace,
        ["<Lu>"] = static c => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.UppercaseLetter,
        ["<Cn>"] = static c => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.OtherNotAssigned,
        ["<Co>"] = static c => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.PrivateUse,
        ["<Zs>"] = static c => CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.SpaceSeparator,
    };

    /// <summary>
    /// Gets the patterns the writer writes, with what it writes and whether every value they accept lies in the Basic
    /// Multilingual Plane.
    /// </summary>
    public static TheoryData<string, string, bool> Written => new()
    {
        { "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", true },
        { "^acc_[0123456789abcdefghjkmnpqrstvwxyz]{20}$", "^acc_[0123456789abcdefghjkmnpqrstvwxyz]{20}$", true },
        { @"^\+[0-9]{6,15}$", @"^\+[0-9]{6,15}$", true },
        { @"^\d{3}$", Expand("^[<Nd>]{3}$"), true },
        { @"^\w+$", Expand("^[<w>]+$"), true },
        { @"^\s$", Expand("^[<s>]$"), true },
        { @"^[\w\s\d-]+$", Expand(@"^[<w><s><Nd>\-]+$"), true },
        { @"^\p{Lu}\p{Cn}\p{Co}\p{Zs}$", Expand("^[<Lu>][<Cn>][<Co>][<Zs>]$"), true },
        { @"^[\p{Lu}\d]+$", Expand("^[<Lu><Nd>]+$"), true },
        { @"^\u00E9\x41\t\n$", @"^\x{E9}A\x{9}\x{A}$", true },
        { @"^[\u0000-\uD7FF\uE000-\uFFFF]+$", @"^[\x{0}-\x{D7FF}\x{E000}-\x{FFFF}]+$", true },
        { @"^(?<year>\d{4})-(\d{2})(?:x)$", Expand("^(?:[<Nd>]{4})-(?:[<Nd>]{2})(?:x)$"), true },
        { "^a+?b*?c??d{2,3}?e{2,}?$", "^a+b*c?d{2,3}e{2,}$", true },
        { "^a{0,65535}$", "^a{0,65535}$", true },
        { @"^\$\.\^\|\?\*\+\(\)\[\]\{\}\\$", @"^\$\.\^\|\?\*\+\(\)\[\]\{\}\\$", true },
        { @"^[\]\[\\\-\^]$", @"^[\]\[\\\-\^]$", true },
        { "^ !\"#%&',/:;<=>@_`~}$", "^ !\"#%&',/:;<=>@_`~\\}$", true },
        { @"\Aab\z|^cd\Z", @"\Aab\z|^cd\Z", true },
        { "^(?:ab){1000}$", "^(?:ab){1000}$", true },
        { "^$", "^$", true },
        { "^a|b$", "^a|b$", false },
        { "^ab", "^ab", false },
        { "ab$", "ab$", false },
        { "a^b$", "a^b$", false },
        { "(?:^a$)", "(?:^a$)", false },
    };

    /// <summary>
    /// Gets patterns the writer leaves out, each for the reason it gives.
    /// </summary>
    public static TheoryData<string, string> Refused => new()
    {
        { "^.{2}$", "'.' matches half of a surrogate pair" },
        { "^(?:a|.)$", "'.' matches half of a surrogate pair, in a group's alternative too" },
        { "a|.", "'.' matches half of a surrogate pair, in an alternative too" },
        { "^[^a]{2}$", "a negated class matches half of a surrogate pair" },
        { @"^[\D]$", "a negated escape matches half of a surrogate pair" },
        { @"^\D$", "a negated escape matches half of a surrogate pair" },
        { @"^\W$", "a negated escape matches half of a surrogate pair" },
        { @"^\S$", "a negated escape matches half of a surrogate pair" },
        { @"^\P{L}{2}$", "a negated category matches half of a surrogate pair" },
        { @"^\p{C}$", "C holds the surrogates" },
        { @"^[\p{Cs}]$", "Cs is the surrogates" },
        { @"^[\uD800-\uDBFF]$", "a range of surrogates matches half of a pair" },
        { @"^[\u0000-\uFFFF]{2}$", "a range reaching the surrogates matches half of a pair" },
        { @"^[\uDFFF-\uFFFF]$", "a range starting at the last surrogate matches half of a pair" },
        { @"^[a-\uD800]$", "a range ending at the first surrogate matches half of a pair" },
        { @"^[a\uDC00]$", "a surrogate in a class matches half of a pair" },
        { @"^\uD83D\uDE00$", "a surrogate written alone matches half of a pair" },
        { @"\bword\b", "the engines place a word boundary apart" },
        { @"\Bx", "the engines place a word boundary apart" },
        { "^[a-z-[aeiou]]$", "PCRE2 has no class subtraction" },
        { @"^\p{IsBasicLatin}+$", "PCRE2 names no .NET block" },
        { "^a{65536}$", "PCRE2 refuses a count above 65535" },
        { "^a{1,65536}$", "PCRE2 refuses a count above 65535" },
        { "^(?:ab){10000}$", "PCRE2 holds no pattern that large once the group is copied" },
        { "^(?:(?:a){100}){100}$", "PCRE2 holds no pattern that large once the groups are copied" },
        { "^(?:(?:ab){40000}){40000}$", "the copies of copies would overflow a 32-bit count" },
        { "^(?:(?:(?:a){65535}){65535})$", "the copies of copies would overflow a 32-bit count" },
        { "(?:(?:ab){40000}){40000}x", "the copies of copies would overflow a 32-bit count, followed by more" },
        { "^(?=a)", "the reader refuses a lookahead" },
        { "^(a", "the reader refuses what .NET does not compile" },
        { string.Empty, "an empty pattern says nothing" },
    };

    /// <summary>
    /// Gets each class escape the writer writes: <c>\d</c>, <c>\w</c>, <c>\s</c> and every general category but
    /// <c>C</c> and <c>Cs</c>.
    /// </summary>
    public static TheoryData<string> ClassEscapes => new(
        new[] { @"\d", @"\w", @"\s" }.Concat(
            new[]
            {
                "L", "Lu", "Ll", "Lt", "Lm", "Lo", "M", "Mn", "Mc", "Me", "N", "Nd", "Nl", "No", "P", "Pc", "Pd", "Ps", "Pe",
                "Pi", "Pf", "Po", "Z", "Zs", "Zl", "Zp", "S", "Sm", "Sc", "Sk", "So", "Cc", "Cf", "Co", "Cn",
            }.Select(static category => $@"\p{{{category}}}")));

    [Theory]
    [MemberData(nameof(Written))]
    public void A_pattern_both_engines_read_alike_is_written_in_PCRE2(string pattern, string expected, bool basicPlaneOnly)
    {
        PcrePattern.TryWrite(pattern, out var pcre, out var basicPlane).Should().BeTrue();

        pcre.Should().Be(expected);
        basicPlane.Should().Be(basicPlaneOnly);
    }

    [Theory]
    [MemberData(nameof(Refused))]
    public void A_pattern_the_server_could_read_apart_is_left_out(string pattern, string reason)
    {
        PcrePattern.TryWrite(pattern, out var pcre, out var basicPlane).Should().BeFalse(reason);

        pcre.Should().BeNull();
        basicPlane.Should().BeFalse();
    }

    /// <summary>
    /// A pattern written means, in .NET, what it meant before: the writer only changes how a set of characters is
    /// spelled, so .NET accepts the same values through either. The server's own reading is the integration suite's.
    /// </summary>
    [Theory]
    [MemberData(nameof(Written))]
    public void A_pattern_written_matches_in_dotnet_what_the_pattern_it_was_written_from_matches(string pattern, string expected, bool basicPlaneOnly)
    {
        _ = basicPlaneOnly;

        var dotnet = AsDotnet(expected);
        var original = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        string[] candidates =
        [
            string.Empty, "a", "ab", "cd", "abc", "FR7630006000011234567890189", "+33612345678", "123", "\u0663\u0664\u0665",
            "word", "x\u00E9_1", " ", "\u00A0", "\u2028", "\t", "a b-1", "\u00E9A\t\n", "2024-01x", "aabbbcdde", "aabcddeee",
            "$.^|?*+()[]{}\\", "]", "[", "-", "^", " !\"#%&',/:;<=>@_`~}", "ab\n", "cd\n", "a", "\uD83D\uDE00",
            "acc_0123456789abcdefghjk", "A\u0378\uE000 ", "\uA7CB\u0ECE\u1C89", "\uA7CB\u0378\uE000\u3000",
        ];

        foreach (var candidate in candidates)
        {
            dotnet.IsMatch(candidate).Should().Be(original.IsMatch(candidate), "'{0}' and '{1}' both read '{2}'", pattern, expected, candidate);
        }
    }

    /// <summary>
    /// A class escape is written as the list of the characters .NET matches it with, never by its name, which the server
    /// would read with the Unicode tables of its own PCRE2: the list matches, character for character, what the escape
    /// matches in .NET, letters Unicode 16 added included.
    /// </summary>
    /// <param name="escape">The escape.</param>
    [Theory]
    [MemberData(nameof(ClassEscapes))]
    public void A_class_escape_is_written_as_the_characters_dotnet_matches_it_with(string escape)
    {
        PcrePattern.TryWrite($"^{escape}$", out var pcre, out _).Should().BeTrue();

        pcre.Should().NotContain(@"\p").And.NotContain(@"\d").And.NotContain(@"\w").And.NotContain(@"\s");
        var written = AsDotnet(pcre!);
        var original = new Regex($"^{escape}$", RegexOptions.None, TimeSpan.FromSeconds(1));
        for (var code = 0; code <= char.MaxValue; code++)
        {
            var character = ((char)code).ToString();
            if (written.IsMatch(character) != original.IsMatch(character))
            {
                Assert.Fail($"{escape} and its list read U+{code:X4} apart.");
            }
        }
    }

    [Fact]
    public void Groups_nest_down_to_the_depth_PCRE2_takes_and_no_deeper()
    {
        static string Nested(int depth) => "^" + new string('(', depth) + "a" + new string(')', depth) + "$";

        PcrePattern.TryWrite(Nested(PcrePattern.MaximumDepth), out var pcre, out _).Should().BeTrue();
        pcre.Should().Be("^" + string.Concat(Enumerable.Repeat("(?:", PcrePattern.MaximumDepth)) + "a" + new string(')', PcrePattern.MaximumDepth) + "$");

        PcrePattern.TryWrite(Nested(PcrePattern.MaximumDepth + 1), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void Groups_side_by_side_are_no_deeper_than_one()
    {
        var siblings = "^" + string.Concat(Enumerable.Repeat("(?:a)", PcrePattern.MaximumDepth + 1)) + "$";

        PcrePattern.TryWrite(siblings, out var pcre, out _).Should().BeTrue();
        pcre.Should().Be(siblings);
    }

    /// <summary>
    /// The size PCRE2 compiles a pattern to is estimated from above: an ASCII literal two units, another four; a class
    /// 40 units and ten per character or range it lists, an escape being the class it is written as; a quantifier over
    /// a character or a class four more; an anchor one; an alternative three more; a group six more, and repeated, one
    /// copy, a unit larger, per repetition up to the maximum, or once more than the minimum.
    /// </summary>
    [Fact]
    public void The_size_a_repeated_group_compiles_to_is_counted_from_above()
    {
        // (?:ab) is ten units, and eleven a copy.
        AcceptedUpTo("(?:ab){{{0}}}", 2909);
        PcrePattern.TryWrite("(?:ab){2908,}", out _, out _).Should().BeTrue();
        PcrePattern.TryWrite("(?:ab){2909,}", out _, out _).Should().BeFalse();
        PcrePattern.TryWrite("(?:ab){0}", out var never, out _).Should().BeTrue();
        never.Should().Be("(?:ab){0}");

        // A class of two characters, 60 units, in a group, 67 a copy.
        AcceptedUpTo("(?:[ab]){{{0}}}", 477);

        // A character counted, 6 units, in a group, 13 a copy.
        AcceptedUpTo("(?:a{{2}}){{{0}}}", 2461);

        // Two alternatives, 10 units, in a group, 17 a copy.
        AcceptedUpTo("(?:a|b){{{0}}}", 1882);

        // An anchor and a character, 3 units, in a group, 10 a copy.
        AcceptedUpTo("(?:^a){{{0}}}", 3200);

        // \w, the class of what .NET matches with it, in a group, inside a class or not.
        var word = 40 + (10 * Runs(Escapes["<w>"])) + 6 + 1;
        AcceptedUpTo(@"(?:\w){{{0}}}", PcrePattern.MaximumSize / word);
        AcceptedUpTo(@"(?:[\w]){{{0}}}", PcrePattern.MaximumSize / word);

        // A group's six units, and an alternative's three, count on their own.
        PcrePattern.TryWrite("(?:" + new string('a', 15997) + ")", out _, out _).Should().BeTrue();
        PcrePattern.TryWrite("(?:" + new string('a', 15998) + ")", out _, out _).Should().BeFalse();
        PcrePattern.TryWrite(new string('a', 8000) + "|" + new string('a', 7997), out _, out _).Should().BeTrue();
        PcrePattern.TryWrite(new string('a', 8000) + "|" + new string('a', 7998), out _, out _).Should().BeFalse();

        // A character is repeated by one counted opcode, whatever the count.
        PcrePattern.TryWrite("[a-z]{65535}", out _, out _).Should().BeTrue();
        PcrePattern.TryWrite(new string('a', PcrePattern.MaximumSize / 2), out _, out _).Should().BeTrue();
        PcrePattern.TryWrite(new string('a', (PcrePattern.MaximumSize / 2) + 1), out _, out _).Should().BeFalse();
        PcrePattern.TryWrite(new string('\u00E9', PcrePattern.MaximumSize / 4), out _, out _).Should().BeTrue();
        PcrePattern.TryWrite(new string('\u00E9', (PcrePattern.MaximumSize / 4) + 1), out _, out _).Should().BeFalse();

        static void AcceptedUpTo(string format, int repetitions)
        {
            PcrePattern.TryWrite(string.Format(CultureInfo.InvariantCulture, format, repetitions), out _, out _).Should().BeTrue("{0} repetitions fit", repetitions);
            PcrePattern.TryWrite(string.Format(CultureInfo.InvariantCulture, format, repetitions + 1), out _, out _).Should().BeFalse("{0} repetitions do not", repetitions + 1);
        }
    }

    /// <summary>Reads a PCRE2 pattern the writer wrote as .NET reads it: PCRE2's <c>\x{…}</c> is .NET's <c>\u</c>.</summary>
    private static Regex AsDotnet(string pcre)
        => new(Regex.Replace(pcre, @"\\x\{([0-9A-F]+)\}", match => $@"\u{Convert.ToInt32(match.Groups[1].Value, 16):X4}"), RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>Writes an expected pattern, each token standing for the list of the characters of an escape.</summary>
    private static string Expand(string expected)
        => Escapes.Aggregate(expected, static (text, escape) => text.Replace(escape.Key, List(escape.Value), StringComparison.Ordinal));

    /// <summary>
    /// Lists the characters of the Basic Multilingual Plane that match, as PCRE2 writes the items of a class: a run of
    /// one character alone, a longer one as a range, printable ASCII as itself, escaped where a class reads it as a
    /// metacharacter, any other character as <c>\x{…}</c>.
    /// </summary>
    private static string List(Func<char, bool> matches)
    {
        static string Item(char character)
            => character is < ' ' or > '~' ? $@"\x{{{(int)character:X}}}" : (@"\]-[^".Contains(character, StringComparison.Ordinal) ? "\\" : string.Empty) + character;

        var items = new StringBuilder();
        foreach (var (first, last) in RunsOf(matches))
        {
            items.Append(Item(first));
            if (last > first)
            {
                items.Append('-').Append(Item(last));
            }
        }

        return items.ToString();
    }

    private static int Runs(Func<char, bool> matches) => RunsOf(matches).Count();

    private static IEnumerable<(char First, char Last)> RunsOf(Func<char, bool> matches)
    {
        for (var code = 0; code <= char.MaxValue; code++)
        {
            if (!matches((char)code))
            {
                continue;
            }

            var last = code;
            while (last < char.MaxValue && matches((char)(last + 1)))
            {
                last++;
            }

            yield return ((char)code, (char)last);
            code = last;
        }
    }
}
