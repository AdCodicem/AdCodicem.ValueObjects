using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.MongoDB;

/// <summary>
/// Writes the .NET pattern a value object declares as the PCRE2 pattern a MongoDB server checks the <c>pattern</c> of a
/// <c>$jsonSchema</c> with, or tells that the server could refuse a value the .NET pattern accepts: such a pattern is
/// left out of the validator rather than published with another meaning.
/// </summary>
/// <remarks>
/// <para>
/// The server reads a pattern as PCRE2 does in UTF mode, one character per code point, with ASCII-only <c>\d</c>,
/// <c>\w</c> and <c>\s</c>, a line feed alone as the newline, and no option. A pattern is written only when the server
/// accepts every value .NET accepts: literals, positive classes and the escapes <c>\d</c>, <c>\w</c>, <c>\s</c> and
/// <c>\p{…}</c> of a general category, each written as the characters of the Basic Multilingual Plane .NET matches it
/// with, listed as the ranges of a class; quantifiers, lazy ones made greedy, which changes which match is found, never
/// whether one is; groups, written without capturing; alternation; and the anchors <c>^</c>, <c>$</c>, <c>\A</c>,
/// <c>\z</c> and <c>\Z</c>, which both engines read alike. A character outside printable ASCII is written
/// <c>\x{…}</c>, which PCRE2 reads where it refuses <c>\u</c>.
/// </para>
/// <para>
/// An escape is listed rather than named because the server reads a category with the Unicode tables of its own PCRE2,
/// which lag .NET's: MongoDB 8.0 reads U+0ECE as no mark and U+A7CB as no letter, where .NET 10 reads them as the mark
/// and the letter Unicode 15 and 16 made them. Listed, a class holds the characters .NET's own tables give it,
/// whichever Unicode version the server knows; and as .NET matches a category one UTF-16 code unit at a time, never a
/// character outside the plane, the server then reads it exactly as .NET does, outside the plane too.
/// </para>
/// <para>
/// None of these matches half of a surrogate pair in .NET, so the two engines see the same characters wherever .NET
/// finds a match. <c>.</c>, a negated class, <c>\D</c>, <c>\W</c>, <c>\S</c> and <c>\P{…}</c> do match one half, and
/// a count over them differs, <c>^.{2}$</c> accepting one emoji in .NET and refusing it on the server, so a pattern
/// holding one is not written; nor is a word boundary, which matches no character but which the engines place apart,
/// since PCRE2 tells a word character by its ASCII <c>\w</c>; nor a class subtraction or a named block, which PCRE2
/// refuses, a count above 65535, which PCRE2 refuses, or a pattern that would compile past what PCRE2 holds, its
/// repeated groups copied and its classes listed, which would make the server refuse the whole validator.
/// </para>
/// </remarks>
internal static class PcrePattern
{
    /// <summary>The highest count PCRE2 takes in a quantifier.</summary>
    internal const int MaximumCount = 65535;

    /// <summary>
    /// The size, in the units <see cref="Writer"/> estimates, past which a pattern is not written: half what PCRE2 holds
    /// with the two-byte links a MongoDB server is built with, so that an estimate falling short stays within it.
    /// </summary>
    internal const int MaximumSize = 32_000;

    /// <summary>The deepest nesting of groups written, below the 250 PCRE2 takes.</summary>
    internal const int MaximumDepth = 200;

    /// <summary>The characters PCRE2 escapes with a backslash outside a class.</summary>
    private const string Metacharacters = @"\^$.|?*+()[]{}";

    /// <summary>The characters PCRE2 escapes with a backslash inside a class, <c>[</c> included, which starts a POSIX class.</summary>
    private const string ClassMetacharacters = @"\]-[^";

    /// <summary>The units <see cref="Writer"/> counts for a class, before the characters and ranges it lists.</summary>
    private const int ClassSize = 40;

    /// <summary>The units <see cref="Writer"/> counts for each character or range a class lists.</summary>
    private const int ClassItemSize = 10;

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
    private static readonly ConcurrentDictionary<string, ListedClass> Listed = new(StringComparer.Ordinal);

    /// <summary>
    /// Writes a .NET pattern as a PCRE2 pattern a MongoDB server refuses no value with that the .NET pattern accepts.
    /// </summary>
    /// <param name="pattern">The .NET pattern, read with no option.</param>
    /// <param name="pcre">The PCRE2 pattern.</param>
    /// <param name="basicPlaneOnly">
    /// Whether every value the pattern accepts lies in the Basic Multilingual Plane, which holds when each of its
    /// alternatives is anchored at both ends: the server then counts the length of such a value as .NET does.
    /// </param>
    /// <returns><see langword="false"/> when the server could read the pattern apart, or the pattern holds nothing.</returns>
    public static bool TryWrite(string pattern, [NotNullWhen(true)] out string? pcre, out bool basicPlaneOnly)
    {
        pcre = null;
        basicPlaneOnly = false;
        if (!ValueObjectPatternSyntax.TryParse(pattern, out var root))
        {
            return false;
        }

        var writer = new Writer();
        if (writer.Write(root) is null || writer.Output.Length == 0)
        {
            return false;
        }

        pcre = writer.Output.ToString();
        var alternatives = root is PatternAlternation alternation ? alternation.Alternatives : [(PatternSequence)root];
        basicPlaneOnly = alternatives.All(static alternative => alternative.Items is
        [
            PatternAnchor { Kind: PatternAnchorKind.StartOfLine or PatternAnchorKind.StartOfText },
            ..,
            PatternAnchor { Kind: PatternAnchorKind.EndOfLine or PatternAnchorKind.EndOfText or PatternAnchorKind.EndOfTextOrFinalNewline },
        ]);

        return true;
    }

    /// <summary>
    /// Lists the characters of the Basic Multilingual Plane a class escape matches in .NET, as the items of a PCRE2
    /// class: .NET's own regular expression engine tells, character by character.
    /// </summary>
    /// <param name="escape">The escape, as .NET reads it: <c>\d</c>, <c>\w</c>, <c>\s</c> or <c>\p{…}</c>.</param>
    /// <remarks>
    /// None of the escapes written matches a surrogate, <c>C</c> and <c>Cs</c> being left out, so no item is one.
    /// </remarks>
    private static ListedClass List(string escape) => Listed.GetOrAdd(escape, static escape =>
    {
        var regex = new Regex("[" + escape + "]", RegexOptions.CultureInvariant);
        var items = new StringBuilder();
        var count = 0;
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
                AppendCharacter(items, (char)first, inClass: true);
                if (code - 1 > first)
                {
                    items.Append('-');
                    AppendCharacter(items, (char)(code - 1), inClass: true);
                }

                count++;
                first = -1;
            }
        }

        return new ListedClass(items.ToString(), count);
    });

    /// <summary>
    /// Writes a character, escaped where PCRE2 reads it as a metacharacter, and as <c>\x{…}</c> outside printable ASCII.
    /// </summary>
    /// <returns><see langword="false"/> for a surrogate, which matches half of a pair in .NET, and is not written.</returns>
    private static bool AppendCharacter(StringBuilder output, char character, bool inClass)
    {
        if (char.IsSurrogate(character))
        {
            return false;
        }

        if (character is < ' ' or > '~')
        {
            output.Append(@"\x{").Append(((int)character).ToString("X", CultureInfo.InvariantCulture)).Append('}');
            return true;
        }

        if ((inClass ? ClassMetacharacters : Metacharacters).Contains(character, StringComparison.Ordinal))
        {
            output.Append('\\');
        }

        output.Append(character);
        return true;
    }

    /// <summary>The characters of a class escape, as the items of a PCRE2 class, and how many items they are.</summary>
    /// <param name="Items">The characters and ranges, written.</param>
    /// <param name="Count">How many characters and ranges.</param>
    private sealed record ListedClass(string Items, int Count);

    /// <summary>
    /// Writes the nodes of a tree, and estimates the size PCRE2 compiles each to, from above: a literal to its opcode and
    /// its bytes, a class to its bitmap and the characters and ranges it lists, a repeated group to as many copies as
    /// its count, as PCRE2 compiles it.
    /// </summary>
    /// <remarks>
    /// A node past <see cref="MaximumSize"/> cannot be written, so no sum of sizes overflows: each is counted in a
    /// <see cref="long"/> from sizes that stay within it.
    /// </remarks>
    private sealed class Writer
    {
        private int _depth;

        public StringBuilder Output { get; } = new();

        /// <summary>Writes a node.</summary>
        /// <param name="node">The node.</param>
        /// <returns>
        /// Its estimated size, or <see langword="null"/> when it cannot be written or would compile past
        /// <see cref="MaximumSize"/>.
        /// </returns>
        public long? Write(PatternNode node) => node switch
        {
            PatternSequence sequence => WriteSequence(sequence),
            PatternAlternation alternation => WriteAlternation(alternation),
            PatternGroup group => WriteGroup(group),
            PatternQuantifier quantifier => WriteQuantifier(quantifier),
            PatternLiteral literal => AppendCharacter(Output, literal.Character, inClass: false) ? (literal.Character < 0x80 ? 2 : 4) : null,
            PatternClassEscape escape => AppendEscape(escape),
            PatternCharacterClass characterClass => AppendClass(characterClass),
            PatternAnchor anchor => AppendAnchor(anchor.Kind),

            // '.', which matches half of a surrogate pair in .NET.
            _ => null,
        } is { } size && size <= MaximumSize ? size : null;

        private long? WriteSequence(PatternSequence sequence)
        {
            var size = 0L;
            foreach (var item in sequence.Items)
            {
                if (Write(item) is not { } written)
                {
                    return null;
                }

                size += written;
            }

            return size;
        }

        private long? WriteAlternation(PatternAlternation alternation)
        {
            var size = 0L;
            for (var index = 0; index < alternation.Alternatives.Length; index++)
            {
                if (index > 0)
                {
                    Output.Append('|');
                }

                if (Write(alternation.Alternatives[index]) is not { } written)
                {
                    return null;
                }

                size += written + 3;
            }

            return size;
        }

        private long? WriteGroup(PatternGroup group)
        {
            if (++_depth > MaximumDepth)
            {
                return null;
            }

            Output.Append("(?:");
            var content = Write(group.Content);
            Output.Append(')');
            _depth--;

            return content + 6;
        }

        private long? WriteQuantifier(PatternQuantifier quantifier)
        {
            if (quantifier.Minimum > MaximumCount || quantifier.Maximum > MaximumCount || Write(quantifier.Item) is not { } item)
            {
                return null;
            }

            Output.Append((quantifier.Minimum, quantifier.Maximum) switch
            {
                (0, null) => "*",
                (1, null) => "+",
                (0, 1) => "?",
                (var minimum, null) => string.Create(CultureInfo.InvariantCulture, $"{{{minimum},}}"),
                (var minimum, var maximum) when minimum == maximum => string.Create(CultureInfo.InvariantCulture, $"{{{minimum}}}"),
                (var minimum, var maximum) => string.Create(CultureInfo.InvariantCulture, $"{{{minimum},{maximum}}}"),
            });

            // PCRE2 repeats a character with one counted opcode, and a group by copying it, once per repetition up to the
            // maximum, or once more than the minimum when there is none.
            if (quantifier.Item is not PatternGroup)
            {
                return item + 4;
            }

            var copies = Math.Max(quantifier.Maximum ?? (quantifier.Minimum + 1), 1);
            return copies * (item + 1);
        }

        private long? AppendAnchor(PatternAnchorKind kind)
        {
            var written = kind switch
            {
                PatternAnchorKind.StartOfLine => "^",
                PatternAnchorKind.EndOfLine => "$",
                PatternAnchorKind.StartOfText => @"\A",
                PatternAnchorKind.EndOfText => @"\z",
                PatternAnchorKind.EndOfTextOrFinalNewline => @"\Z",

                // \b and \B: the engines tell a word character apart.
                _ => null,
            };

            if (written is null)
            {
                return null;
            }

            Output.Append(written);
            return 1;
        }

        private long? AppendClass(PatternCharacterClass characterClass)
        {
            if (characterClass.Negated || characterClass.Subtraction is not null)
            {
                return null;
            }

            Output.Append('[');
            var size = (long)ClassSize;
            foreach (var item in characterClass.Items)
            {
                var written = item switch
                {
                    PatternLiteral literal => AppendCharacter(Output, literal.Character, inClass: true) ? 1 : null,
                    PatternRange range => AppendRange(range),
                    _ => AppendListed((PatternClassEscape)item),
                };

                if (written is null)
                {
                    return null;
                }

                size += written.Value * ClassItemSize;
            }

            Output.Append(']');
            return size;
        }

        /// <summary>Writes a range of a class.</summary>
        /// <returns>The one item it lists, or <see langword="null"/> when it reaches the surrogates.</returns>
        private int? AppendRange(PatternRange range)
        {
            // A range reaching the surrogates matches half of a pair; one that does not has no surrogate at either end.
            if (range.From <= '\uDFFF' && range.To >= '\uD800')
            {
                return null;
            }

            AppendCharacter(Output, range.From, inClass: true);
            Output.Append('-');
            AppendCharacter(Output, range.To, inClass: true);
            return 1;
        }

        /// <summary>Writes a class escape outside a class, as a class of its own.</summary>
        private long? AppendEscape(PatternClassEscape escape)
        {
            Output.Append('[');
            if (AppendListed(escape) is not { } count)
            {
                return null;
            }

            Output.Append(']');
            return ClassSize + ((long)count * ClassItemSize);
        }

        /// <summary>Writes the characters a class escape matches in .NET, as the items of a class.</summary>
        /// <returns>How many items it lists, or <see langword="null"/> when the escape cannot be written.</returns>
        private int? AppendListed(PatternClassEscape escape)
        {
            // A negated escape matches half of a surrogate pair; a named block, which only .NET names, or C, which holds
            // the surrogates, is left out with it.
            if (escape.Negated || (escape.Letter == 'p' && !Categories.Contains(escape.Category, StringComparer.Ordinal)))
            {
                return null;
            }

            var listed = List(escape.Letter == 'p' ? $@"\p{{{escape.Category}}}" : $@"\{escape.Letter}");
            Output.Append(listed.Items);
            return listed.Count;
        }
    }
}
