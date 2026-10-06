using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using AdCodicem.ValueObjects.Metadata;

namespace AdCodicem.ValueObjects.UnitTests.XmlSerialization;

/// <summary>
/// A .NET pattern written as an XSD pattern, or left out when XSD cannot say what it means: each translation matches
/// what the .NET pattern matches, which a comparison of both engines on the same values proves.
/// </summary>
public class XsdPatternTests
{
    public static TheoryData<string, string> Written => new()
    {
        { "^[A-Z0-9]+$", "[A-Z0-9]+" },
        { "^$", string.Empty },
        { "^a$|^b$", "a|b" },
        { @"\A(?:a|b)\z", "(a|b)" },
        { @"^a\Z", "a" },
        { "^(?<x>a)b$", "(a)b" },
        { @"^\d{3}-\d{4}$", @"\p{Nd}{3}\-\p{Nd}{4}" },
        { @"^\D$", @"\P{Nd}" },
        { "^.+$", @"[^\n]+" },
        { "^a.*?b$", @"a[^\n]*b" },
        { @"^\$\d+$", @"[$]\p{Nd}+" },
        { "^[$^]$", @"[$\^]" },
        { @"^\^\.$", @"\^\." },
        { @"^a\/b$", "a/b" },
        { "^[a-z-[aeiou]]+$", "[a-z-[aeiou]]+" },
        { "^[]a]$", @"[\]a]" },
        { @"^[\d-]+$", @"[\p{Nd}\-]+" },
        { @"^\w+$", @"[\p{L}\p{Mn}\p{Nd}\p{Pc}]+" },
        { @"^\W$", @"[^\p{L}\p{Mn}\p{Nd}\p{Pc}]" },
        { @"^[\w.]+$", @"[\p{L}\p{Mn}\p{Nd}\p{Pc}.]+" },
        { @"^\s*$", "[\\t\\n\\r\u0085\\p{Z}]*" },
        { @"^\S$", "[^\\t\\n\\r\u0085\\p{Z}]" },
        { @"^[^@\s]+@[^@\s]+\.[^@\s]+$", "[^@\\t\\n\\r\u0085\\p{Z}]+@[^@\\t\\n\\r\u0085\\p{Z}]+\\.[^@\\t\\n\\r\u0085\\p{Z}]+" },
        { @"^\p{Lu}\p{Ll}*$", @"\p{Lu}\p{Ll}*" },
        { @"^\P{L}$", @"\P{L}" },
        { @"^[\p{L}-[a-z]]$", @"[\p{L}-[a-z]]" },
        { @"^[^\P{N}]$", @"[^\P{N}]" },
        { @"^\t\n\r$", @"\t\n\r" },
        { @"^[\t\n\r]$", @"[\t\n\r]" },
        { @"^\x41\u00E9$", "Aé" },
        { "^a{2,}b{3}c{1,2}d?e*$", "a{2,}b{3}c{1,2}d?e*" },
        { "^(a|b)+$", "(a|b)+" },
        { @"^\{\}\(\)\[\]\|\?\*\+$", @"\{\}\(\)\[\]\|\?\*\+" },
        { "^a{,2}$", @"a\{,2\}" },
        { @"^\#\ $", "# " },
        { @"^[\^\-\[\]\\]$", @"[\^\-\[\]\\]" },
        { "^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$", "[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}" },
    };

    [Theory]
    [MemberData(nameof(Written))]
    public void A_pattern_XSD_can_say_is_written_with_the_same_meaning(string pattern, string expected)
    {
        XsdPattern.TryWrite(pattern, out var xsd).Should().BeTrue();

        xsd.Should().Be(expected);
        Disagreements(pattern, xsd!, Candidates(pattern, new Random(pattern.Length))).Should().BeEmpty();
    }

    public static TheoryData<string> LeftOut => new()
    {
        // Not anchored at both ends, or an alternative that is not.
        "[A-Z]+", "^[A-Z]+", "[A-Z]+$", "^a|b$", "^a$|b", "^", "$", "a",

        // An anchor inside, a word boundary, an anchor in a group.
        @"^a\bb$", "^(a$)$", "^a^b$",

        // A negated \w or \s among the items of a class, which XSD cannot unite with others.
        @"^[\W]$", @"^[a\S]$",

        // A named block, which .NET and XSD name apart, and a category XSD does not have.
        @"^\p{IsGreek}$", @"^\p{Cs}$", @"^[\p{IsBasicLatin}]$", @"^(\p{IsGreek})$", @"^(a|\p{IsGreek})$", @"^\p{IsGreek}+$", @"^[a-z-[\p{IsGreek}]]$",

        // A character no XML text holds: a control character, a lone surrogate, inside a class too.
        @"^\u0000$", @"^\uD800$", @"^[\uD800-\uDBFF]$", @"^\f$", @"^[a-\x01]$", @"^[\x01-a]$",

        // A construct the reader refuses.
        "^(?=a)a$", @"^(a)\1$",
    };

    [Theory]
    [MemberData(nameof(LeftOut))]
    public void A_pattern_XSD_cannot_say_is_left_out(string pattern)
    {
        XsdPattern.TryWrite(pattern, out var xsd).Should().BeFalse();

        xsd.Should().BeNull();
    }

    /// <summary>
    /// Patterns drawn from the whole subset the writer takes, each checked against values drawn from an alphabet that
    /// holds what tells the two engines apart: Unicode digits and letters, separators, metacharacters.
    /// </summary>
    [Fact]
    public void Drawn_patterns_match_in_XSD_what_they_match_in_dotnet()
    {
        string[] atoms =
        [
            "a", "b", "x", "é", " ", @"\t", @"\d", @"\D", @"\w", @"\W", @"\s", @"\S", ".", "[ab]", "[^a]", "[a-c]", @"[\d.]",
            @"[^\s@]", @"[\w-]", @"\p{Lu}", @"\P{L}", @"\p{Nd}", @"\.", @"\$", @"\^", @"\-", @"\{", "[a-z-[aeiou]]", "(a|b)",
            "(?:ab|c)", "(?<n>a)", @"\u0663", "[$]", @"[\^a]", @"[\]\\]",
        ];
        string[] quantifiers = [string.Empty, string.Empty, string.Empty, "*", "+", "?", "{2}", "{1,3}", "{2,}", "*?", "+?", "??"];
        var random = new Random(121);
        var disagreements = new List<string>();

        for (var drawn = 0; drawn < 300; drawn++)
        {
            var pattern = Draw();
            if (random.Next(4) == 0)
            {
                pattern += "|" + Draw();
            }

            XsdPattern.TryWrite(pattern, out var xsd).Should().BeTrue(pattern);
            disagreements.AddRange(Disagreements(pattern, xsd!, Candidates(pattern, random)));
        }

        disagreements.Should().BeEmpty();

        string Draw()
        {
            var body = new StringBuilder("^");
            for (var count = random.Next(1, 5); count > 0; count--)
            {
                body.Append(atoms[random.Next(atoms.Length)]).Append(quantifiers[random.Next(quantifiers.Length)]);
            }

            return body.Append('$').ToString();
        }
    }

    [Fact]
    public void A_pattern_with_a_dollar_reads_it_as_a_character_in_System_Xml_too()
    {
        XsdPattern.TryWrite(@"^a\$b$", out var xsd).Should().BeTrue();

        Accepts(Compile(xsd!), "a$b").Should().BeTrue();
        Accepts(Compile("a$b"), "a$b").Should().BeFalse("System.Xml hands an XSD pattern to .NET, which reads a bare $ as an anchor");
    }

    /// <summary>
    /// Values to try a pattern on: the empty one, then values drawn from an alphabet of what tells the engines apart,
    /// with the characters the pattern names, never ending with a line feed, which .NET's <c>$</c> takes as the end.
    /// </summary>
    private static IEnumerable<string> Candidates(string pattern, Random random)
    {
        var alphabet = "aAbBcxZ09_-.$^ \t\n\r@/{}()[]\\#|*+?\u00E9\u0085\u00A0\u0663\u0DE6\u2028\u01C5\u0301"
            + new string([.. pattern.Where(static character => !char.IsControl(character))]);
        yield return string.Empty;
        for (var drawn = 0; drawn < 200; drawn++)
        {
            var value = new StringBuilder();
            for (var length = random.Next(1, 7); length > 0; length--)
            {
                value.Append(alphabet[random.Next(alphabet.Length)]);
            }

            if (value[^1] != '\n')
            {
                yield return value.ToString();
            }
        }
    }

    private static IEnumerable<string> Disagreements(string pattern, string xsd, IEnumerable<string> candidates)
    {
        var regex = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        var type = Compile(xsd);
        foreach (var candidate in candidates)
        {
            if (regex.IsMatch(candidate) != Accepts(type, candidate))
            {
                yield return $"{pattern} -> {xsd} on '{candidate}': .NET {regex.IsMatch(candidate)}";
            }
        }
    }

    private static XmlSchemaSimpleType Compile(string xsd)
    {
        var restriction = new XmlSchemaSimpleTypeRestriction { BaseTypeName = new XmlQualifiedName("string", XmlSchema.Namespace) };
        restriction.Facets.Add(new XmlSchemaPatternFacet { Value = xsd });
        var type = new XmlSchemaSimpleType { Name = "Checked", Content = restriction };
        var schema = new XmlSchema { TargetNamespace = "urn:checked" };
        schema.Items.Add(type);
        var schemas = new XmlSchemaSet();
        schemas.Add(schema);
        schemas.Compile();
        return type;
    }

    private static bool Accepts(XmlSchemaSimpleType type, string value)
    {
        try
        {
            type.Datatype!.ParseValue(value, null, null);
            return true;
        }
        catch (XmlSchemaException)
        {
            return false;
        }
    }
}
