using System.Globalization;
using System.Text;
using System.Xml;
using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.Metadata;

/// <summary>
/// Writes the .NET pattern a value object declares as the pattern of an XSD simple type, or tells that XSD cannot say
/// what it means: such a pattern is left out of the schema rather than published with another meaning.
/// </summary>
/// <remarks>
/// <para>
/// An XSD pattern matches the whole value, as if anchored at both ends, and reads <c>^</c> and <c>$</c> as characters.
/// A pattern is therefore written only when each of its alternatives is anchored at both ends, by <c>^</c> or
/// <c>\A</c> and by <c>$</c>, <c>\z</c> or <c>\Z</c>, and holds no other anchor; the anchors are dropped. XSD has no
/// counterpart for <c>$</c> matching before a final <c>\n</c>, which a value read from XML seldom ends with.
/// </para>
/// <para>
/// What .NET and XSD read alike is written as it is; what they read apart is written in the form XSD gives the .NET
/// meaning: <c>.</c> as <c>[^\n]</c>, <c>\d</c> as <c>\p{Nd}</c>, <c>\w</c> and <c>\s</c> as the classes .NET reads them
/// as, a <c>$</c> as <c>[$]</c>, which System.Xml would otherwise read as an anchor, and every metacharacter escaped. A
/// lazy quantifier becomes greedy, which changes which match is found, never whether one is. A named block of
/// <c>\p{…}</c>, a <c>\W</c> or <c>\S</c> inside a class, and a character no XML text can hold are not written.
/// </para>
/// <para>
/// Both sides count a character outside the Basic Multilingual Plane apart, .NET as two and XSD as one, as they do
/// for <c>maxLength</c>.
/// </para>
/// </remarks>
internal static class XsdPattern
{
    /// <summary>The characters XSD escapes with a backslash outside a class.</summary>
    private const string Metacharacters = @"\|.?*+(){}-[]^";

    /// <summary>The characters XSD escapes with a backslash inside a class.</summary>
    private const string ClassMetacharacters = @"\-[]^";

    /// <summary>What <c>\w</c> matches in .NET, as the items of an XSD class.</summary>
    private const string WordItems = @"\p{L}\p{Mn}\p{Nd}\p{Pc}";

    /// <summary>
    /// What <c>\s</c> matches in .NET, as the items of an XSD class, but <c>\f</c> and <c>\v</c>, which no XML text
    /// holds.
    /// </summary>
    private const string SpaceItems = "\\t\\n\\r\u0085\\p{Z}";

    /// <summary>The Unicode general categories XSD names, each matching what it matches in .NET.</summary>
    private static readonly string[] Categories =
    [
        "L", "Lu", "Ll", "Lt", "Lm", "Lo", "M", "Mn", "Mc", "Me", "N", "Nd", "Nl", "No", "P", "Pc", "Pd", "Ps", "Pe", "Pi",
        "Pf", "Po", "Z", "Zs", "Zl", "Zp", "S", "Sm", "Sc", "Sk", "So", "C", "Cc", "Cf", "Co", "Cn",
    ];

    /// <summary>
    /// Writes a .NET pattern as an XSD pattern.
    /// </summary>
    /// <param name="pattern">The .NET pattern.</param>
    /// <param name="xsd">The XSD pattern, matching what the .NET pattern matches.</param>
    /// <returns><see langword="false"/> when XSD cannot say what the pattern means.</returns>
    public static bool TryWrite(string pattern, [NotNullWhen(true)] out string? xsd)
    {
        xsd = null;
        if (!ValueObjectPatternSyntax.TryParse(pattern, out var root))
        {
            return false;
        }

        var output = new StringBuilder(pattern.Length + 8);
        var alternatives = root is PatternAlternation alternation ? alternation.Alternatives : [(PatternSequence)root];
        for (var index = 0; index < alternatives.Length; index++)
        {
            var items = alternatives[index].Items;
            if (items.Length < 2
                || items[0] is not PatternAnchor { Kind: PatternAnchorKind.StartOfLine or PatternAnchorKind.StartOfText }
                || items[^1] is not PatternAnchor { Kind: PatternAnchorKind.EndOfLine or PatternAnchorKind.EndOfText or PatternAnchorKind.EndOfTextOrFinalNewline })
            {
                return false;
            }

            if (index > 0)
            {
                output.Append('|');
            }

            for (var item = 1; item < items.Length - 1; item++)
            {
                if (!TryWrite(items[item], output))
                {
                    return false;
                }
            }
        }

        xsd = output.ToString();
        return true;
    }

    private static bool TryWrite(PatternNode node, StringBuilder output)
    {
        switch (node)
        {
            case PatternSequence sequence:
                return sequence.Items.All(item => TryWrite(item, output));
            case PatternAlternation alternation:
                for (var index = 0; index < alternation.Alternatives.Length; index++)
                {
                    if (index > 0)
                    {
                        output.Append('|');
                    }

                    if (!TryWrite(alternation.Alternatives[index], output))
                    {
                        return false;
                    }
                }

                return true;
            case PatternGroup group:
                output.Append('(');
                if (!TryWrite(group.Content, output))
                {
                    return false;
                }

                output.Append(')');
                return true;
            case PatternQuantifier quantifier:
                if (!TryWrite(quantifier.Item, output))
                {
                    return false;
                }

                // Lazy or greedy, the pattern matches the same values once it must match the whole of each.
                output.Append((quantifier.Minimum, quantifier.Maximum) switch
                {
                    (0, null) => "*",
                    (1, null) => "+",
                    (0, 1) => "?",
                    (var minimum, null) => string.Create(CultureInfo.InvariantCulture, $"{{{minimum},}}"),
                    (var minimum, var maximum) when minimum == maximum => string.Create(CultureInfo.InvariantCulture, $"{{{minimum}}}"),
                    (var minimum, var maximum) => string.Create(CultureInfo.InvariantCulture, $"{{{minimum},{maximum}}}"),
                });
                return true;
            case PatternLiteral literal:
                return TryAppendCharacter(literal.Character, output, inClass: false);
            case PatternAnyCharacter:
                output.Append(@"[^\n]");
                return true;
            case PatternClassEscape escape:
                return TryAppendEscape(escape, output, inClass: false);
            case PatternCharacterClass characterClass:
                return TryAppendClass(characterClass, output);
            default:
                // An anchor anywhere but at the ends of an alternative.
                return false;
        }
    }

    private static bool TryAppendClass(PatternCharacterClass characterClass, StringBuilder output)
    {
        output.Append('[');
        if (characterClass.Negated)
        {
            output.Append('^');
        }

        foreach (var item in characterClass.Items)
        {
            var written = item switch
            {
                PatternLiteral literal => TryAppendCharacter(literal.Character, output, inClass: true),
                PatternRange range => TryAppendRange(range, output),
                _ => TryAppendEscape((PatternClassEscape)item, output, inClass: true),
            };

            if (!written)
            {
                return false;
            }
        }

        if (characterClass.Subtraction is { } subtraction)
        {
            output.Append('-');
            if (!TryAppendClass(subtraction, output))
            {
                return false;
            }
        }

        output.Append(']');
        return true;
    }

    private static bool TryAppendRange(PatternRange range, StringBuilder output)
    {
        if (!TryAppendCharacter(range.From, output, inClass: true))
        {
            return false;
        }

        output.Append('-');
        return TryAppendCharacter(range.To, output, inClass: true);
    }

    private static bool TryAppendEscape(PatternClassEscape escape, StringBuilder output, bool inClass)
    {
        switch (char.ToLowerInvariant(escape.Letter))
        {
            case 'd':
                output.Append(escape.Negated ? @"\P{Nd}" : @"\p{Nd}");
                return true;
            case 'p':
                if (!Categories.Contains(escape.Category, StringComparer.Ordinal))
                {
                    // A named block: .NET and XSD name the blocks apart, and differ on what some of them hold.
                    return false;
                }

                output.Append(escape.Negated ? @"\P{" : @"\p{").Append(escape.Category).Append('}');
                return true;
            default:
                // \w and \s, as the classes .NET reads them as. A negation cannot be one item among others in XSD.
                if (inClass && escape.Negated)
                {
                    return false;
                }

                var items = escape.Letter is 'w' or 'W' ? WordItems : SpaceItems;
                output.Append(inClass ? items : (escape.Negated ? "[^" : "[") + items + "]");
                return true;
        }
    }

    private static bool TryAppendCharacter(char character, StringBuilder output, bool inClass)
    {
        switch (character)
        {
            case '\n':
                output.Append(@"\n");
                return true;
            case '\r':
                output.Append(@"\r");
                return true;
            case '\t':
                output.Append(@"\t");
                return true;
            case '$' when !inClass:
                // XSD reads it as a character; System.Xml hands the pattern to .NET, which would read it as an anchor.
                output.Append("[$]");
                return true;
            default:
                // A lone surrogate, or a control character no XML text can hold.
                if (!XmlConvert.IsXmlChar(character))
                {
                    return false;
                }

                if ((inClass ? ClassMetacharacters : Metacharacters).Contains(character, StringComparison.Ordinal))
                {
                    output.Append('\\');
                }

                output.Append(character);
                return true;
        }
    }
}
