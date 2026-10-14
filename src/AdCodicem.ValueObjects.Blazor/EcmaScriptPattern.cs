using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.Blazor;

/// <summary>
/// Writes the .NET pattern a value object declares as the value of an HTML <c>pattern</c> attribute, or tells that a
/// browser could refuse a text the .NET pattern accepts: such a pattern is left off the input rather than published with
/// another meaning.
/// </summary>
/// <remarks>
/// <para>
/// A browser compiles the attribute as <c>^(?:…)$</c>, an ECMAScript regular expression with the <c>v</c> flag, and
/// matches it against the whole text, one character per code point; a pattern it cannot compile gives the input no
/// constraint at all. A pattern is written only when the browser accepts every text .NET accepts, which holds when each of
/// its alternatives is anchored at both ends, so that .NET matches the whole text too, and when no part of it can match
/// half of a surrogate pair in .NET: literals, positive classes and the escapes <c>\d</c>, <c>\w</c>, <c>\s</c> and
/// <c>\p{…}</c> of a general category, each written as the characters of the Basic Multilingual Plane .NET matches it
/// with, listed as the ranges of a class, so that the browser reads it with .NET's Unicode tables, whichever version it
/// knows; quantifiers, lazy ones made greedy, which changes which match is found, never whether the whole text matches;
/// groups, written without capturing; alternation; and the anchors <c>^</c>, <c>$</c>, <c>\A</c>, <c>\z</c> and
/// <c>\Z</c>, the text of an input holding no line break. A character outside printable ASCII is written
/// <c>\u{…}</c>, and only the characters the <c>v</c> flag reads as syntax are escaped: <c>\-</c> outside a class is an
/// error there.
/// </para>
/// <para>
/// <c>.</c>, a negated class, <c>\D</c>, <c>\W</c>, <c>\S</c> and <c>\P{…}</c> match half of a surrogate pair in .NET and
/// a whole code point in the browser, <c>^.{2}$</c> accepting one emoji in .NET and refusing it in the browser, so a
/// pattern holding one is not written; nor is a surrogate, a range reaching the surrogates, the categories <c>C</c> and
/// <c>Cs</c>, which hold them, a named block, which only .NET names, a word boundary, which ECMAScript places by its ASCII
/// <c>\w</c>, or a class subtraction.
/// </para>
/// </remarks>
internal static class EcmaScriptPattern
{
    /// <summary>The characters the <c>v</c> flag reads as syntax outside a class, escaped with a backslash.</summary>
    private const string Syntax = @"^$\.*+?()[]{}|/";

    /// <summary>
    /// The characters the <c>v</c> flag reads as syntax inside a class, or reserves there when doubled, escaped with a
    /// backslash, which it accepts before each.
    /// </summary>
    private const string ClassSyntax = @"^$\.*+?()[]{}|/-&!#%,:;<=>@`~";

    /// <summary>
    /// The Unicode general categories a <c>\p{…}</c> is written from; not <c>C</c>, which holds the surrogates, nor
    /// <c>Cs</c>.
    /// </summary>
    private static readonly string[] Categories =
    [
        "L", "Lu", "Ll", "Lt", "Lm", "Lo", "M", "Mn", "Mc", "Me", "N", "Nd", "Nl", "No", "P", "Pc", "Pd", "Ps", "Pe", "Pi",
        "Pf", "Po", "Z", "Zs", "Zl", "Zp", "S", "Sm", "Sc", "Sk", "So", "Cc", "Cf", "Co", "Cn",
    ];

    /// <summary>The characters each class escape matches in .NET, listed once per escape.</summary>
    private static readonly ConcurrentDictionary<string, string> Listed = new(StringComparer.Ordinal);

    /// <summary>
    /// Writes a .NET pattern as the value of a <c>pattern</c> attribute a browser refuses no text with that the .NET
    /// pattern accepts.
    /// </summary>
    /// <param name="pattern">The .NET pattern, read with no option.</param>
    /// <param name="written">The attribute's value.</param>
    /// <returns><see langword="false"/> when a browser could read the pattern apart.</returns>
    public static bool TryWrite(string pattern, [NotNullWhen(true)] out string? written)
    {
        written = null;
        if (!ValueObjectPatternSyntax.TryParse(pattern, out var root) || !AnchoredAtBothEnds(root))
        {
            return false;
        }

        var output = new StringBuilder();
        if (!Write(root, output))
        {
            return false;
        }

        written = output.ToString();
        return true;
    }

    /// <summary>
    /// Tells whether each alternative of a pattern is anchored at its start and at its end, so that .NET, as the browser,
    /// accepts a text only when the whole of it matches.
    /// </summary>
    private static bool AnchoredAtBothEnds(PatternNode root)
    {
        var alternatives = root is PatternAlternation alternation ? alternation.Alternatives : [(PatternSequence)root];

        return alternatives.All(static alternative => alternative.Items is
        [
            PatternAnchor { Kind: PatternAnchorKind.StartOfLine or PatternAnchorKind.StartOfText },
            ..,
            PatternAnchor { Kind: PatternAnchorKind.EndOfLine or PatternAnchorKind.EndOfText or PatternAnchorKind.EndOfTextOrFinalNewline },
        ]);
    }

    /// <summary>Writes a node of the tree.</summary>
    /// <returns><see langword="false"/> when the browser could read it apart.</returns>
    private static bool Write(PatternNode node, StringBuilder output)
    {
        switch (node)
        {
            case PatternAlternation alternation:
                for (var index = 0; index < alternation.Alternatives.Length; index++)
                {
                    if (index > 0)
                    {
                        output.Append('|');
                    }

                    if (!Write(alternation.Alternatives[index], output))
                    {
                        return false;
                    }
                }

                return true;
            case PatternSequence sequence:
                foreach (var item in sequence.Items)
                {
                    if (!Write(item, output))
                    {
                        return false;
                    }
                }

                return true;
            case PatternGroup group:
                output.Append("(?:");
                if (!Write(group.Content, output))
                {
                    return false;
                }

                output.Append(')');
                return true;
            case PatternQuantifier quantifier:
                if (!Write(quantifier.Item, output))
                {
                    return false;
                }

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
                return Append(literal.Character, output, inClass: false);
            case PatternClassEscape escape:
                // Outside a class, as a class of its own.
                if (!TryList(escape, out var listed))
                {
                    return false;
                }

                output.Append('[').Append(listed).Append(']');
                return true;
            case PatternCharacterClass characterClass:
                return AppendClass(characterClass, output);
            case PatternAnchor anchor:
                return AppendAnchor(anchor.Kind, output);
            default:
                // '.', which matches half of a surrogate pair in .NET.
                return false;
        }
    }

    /// <summary>Writes an anchor.</summary>
    /// <returns><see langword="false"/> for a word boundary, which the engines place apart.</returns>
    private static bool AppendAnchor(PatternAnchorKind kind, StringBuilder output)
    {
        switch (kind)
        {
            case PatternAnchorKind.StartOfLine or PatternAnchorKind.StartOfText:
                output.Append('^');
                return true;
            case PatternAnchorKind.EndOfLine or PatternAnchorKind.EndOfText or PatternAnchorKind.EndOfTextOrFinalNewline:
                output.Append('$');
                return true;
            default:
                return false;
        }
    }

    /// <summary>Writes a class.</summary>
    /// <returns><see langword="false"/> for a negated class, a subtraction, or an item the browser could read apart.</returns>
    private static bool AppendClass(PatternCharacterClass characterClass, StringBuilder output)
    {
        if (characterClass.Negated || characterClass.Subtraction is not null)
        {
            return false;
        }

        output.Append('[');
        foreach (var item in characterClass.Items)
        {
            switch (item)
            {
                case PatternLiteral literal:
                    if (!Append(literal.Character, output, inClass: true))
                    {
                        return false;
                    }

                    break;
                case PatternRange range:
                    // A range reaching the surrogates matches half of a pair; one that does not has no surrogate at either end.
                    if (range.From <= '\uDFFF' && range.To >= '\uD800')
                    {
                        return false;
                    }

                    Append(range.From, output, inClass: true);
                    output.Append('-');
                    Append(range.To, output, inClass: true);
                    break;
                default:
                    if (!TryList((PatternClassEscape)item, out var listed))
                    {
                        return false;
                    }

                    output.Append(listed);
                    break;
            }
        }

        output.Append(']');
        return true;
    }

    /// <summary>
    /// Writes a character, escaped where the <c>v</c> flag reads it as syntax, and as <c>\u{…}</c> outside printable
    /// ASCII.
    /// </summary>
    /// <returns><see langword="false"/> for a surrogate, which matches half of a pair in .NET, and is not written.</returns>
    private static bool Append(char character, StringBuilder output, bool inClass)
    {
        if (char.IsSurrogate(character))
        {
            return false;
        }

        if (character is < ' ' or > '~')
        {
            output.Append(@"\u{").Append(((int)character).ToString("X", CultureInfo.InvariantCulture)).Append('}');
            return true;
        }

        if ((inClass ? ClassSyntax : Syntax).Contains(character, StringComparison.Ordinal))
        {
            output.Append('\\');
        }

        output.Append(character);
        return true;
    }

    /// <summary>
    /// Lists the characters of the Basic Multilingual Plane a class escape matches in .NET, as the items of a class:
    /// .NET's own regular expression engine tells, character by character.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> for a negated escape, which matches half of a surrogate pair, and for a named block or the
    /// categories <c>C</c> and <c>Cs</c>, which are not written.
    /// </returns>
    /// <remarks>None of the escapes written matches a surrogate, so no item is one.</remarks>
    private static bool TryList(PatternClassEscape escape, [NotNullWhen(true)] out string? listed)
    {
        if (escape.Negated || (escape.Letter == 'p' && !Categories.Contains(escape.Category, StringComparer.Ordinal)))
        {
            listed = null;
            return false;
        }

        listed = Listed.GetOrAdd(escape.Letter == 'p' ? $@"\p{{{escape.Category}}}" : $@"\{escape.Letter}", static escape =>
        {
            var regex = new Regex("[" + escape + "]", RegexOptions.CultureInvariant);
            var items = new StringBuilder();
            var first = -1;
            Span<char> character = stackalloc char[1];
            for (var code = 0; code <= char.MaxValue + 1; code++)
            {
                if (code <= char.MaxValue)
                {
                    character[0] = (char)code;
                    if (regex.IsMatch(character))
                    {
                        first = first < 0 ? code : first;
                        continue;
                    }
                }

                if (first >= 0)
                {
                    Append((char)first, items, inClass: true);
                    if (code - 1 > first)
                    {
                        items.Append('-');
                        Append((char)(code - 1), items, inClass: true);
                    }

                    first = -1;
                }
            }

            return items.ToString();
        });

        return true;
    }
}
