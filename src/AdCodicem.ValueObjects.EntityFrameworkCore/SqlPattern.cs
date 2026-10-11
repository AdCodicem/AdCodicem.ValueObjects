using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Writes the .NET pattern a value object declares in the dialect of PostgreSQL's <c>~</c> or SQL Server 2025's
/// <c>REGEXP_LIKE</c>, or names the construct the database would read apart from .NET.
/// </summary>
/// <remarks>
/// <para>
/// Both engines search the text, as .NET's <c>IsMatch</c> does, one character per code point, case-sensitively and
/// whatever the collation of the column, which the constraint sets to <c>C</c> on PostgreSQL. What both read as .NET
/// does is written: literals, escaped where the dialect reads a metacharacter and as <c>\uXXXX</c> (PostgreSQL) or
/// <c>\x{…}</c> (SQL Server) outside printable ASCII; positive classes of literals and ranges, which both compare by code
/// point; quantifiers, lazy ones made greedy, which changes which match is found, never whether one is, up to 255
/// repetitions on PostgreSQL and 1000 on SQL Server; groups, written without capturing; alternation; and the anchors
/// <c>^</c> and <c>\A</c>, written <c>^</c>, and <c>\z</c>, written <c>$</c>. A <c>$</c> or a <c>\Z</c>, which .NET
/// also matches before a final line feed, is written <c>\n?$</c> at the end of the pattern or of one of its top-level
/// alternatives, the one place that writing keeps its meaning.
/// </para>
/// <para>
/// The rest is refused, rather than written with another meaning: the class escapes <c>\d</c>, <c>\w</c>, <c>\s</c> and
/// <c>\p{…}</c>, which .NET reads over Unicode and the database over ASCII or its locale; <c>.</c>, a negated class and
/// a negated escape, which .NET matches over one UTF-16 code unit, half of a character outside the Basic Multilingual
/// Plane, so that a count over them differs; a class subtraction; a word boundary; a <c>$</c> or a <c>\Z</c> anywhere
/// else, inside a group included; a count above the dialect's maximum; half of a surrogate pair; repetitions which, written out, would
/// match more than 1000 characters; and whatever <see cref="ValueObjectPatternSyntax"/> does not read: a lookaround, a
/// backreference, an atomic, conditional or balancing group, an inline option or comment.
/// </para>
/// <para>
/// Both engines compile the pattern of a CHECK constraint when it first checks a value, not when the constraint is
/// created: a pattern either refuses then would fail every write. PostgreSQL finds an expression whose counted
/// repetitions copy too many states "too complex", <c>(?:a?b?c?d?e?f?g?h?){255}</c> or <c>(?:(?:a){100}){100}</c>, and
/// SQL Server's RE2 refuses nested repetitions whose counts multiply past 1000, <c>(?:a{10}){101}</c>: written out, the
/// repetitions of a pattern written here match 1000 characters at most, which both took in every shape probed.
/// </para>
/// </remarks>
internal static class SqlPattern
{
    /// <summary>The characters both dialects escape with a backslash outside a class.</summary>
    private const string Metacharacters = @"\^$.|?*+()[]{}";

    /// <summary>The characters both dialects escape with a backslash inside a class, <c>[</c> included, which starts a POSIX class.</summary>
    private const string ClassMetacharacters = @"\]-[^";

    /// <summary>The most characters the repetitions of a pattern match once written out, which both engines compile.</summary>
    private const int MaximumSize = 1000;

    /// <summary>
    /// Writes a .NET pattern in a database's dialect.
    /// </summary>
    /// <param name="pattern">The .NET pattern, read with no option.</param>
    /// <param name="dialect">The dialect, <see cref="ValueObjectPatternDialect.SqlServer2025"/> or <see cref="ValueObjectPatternDialect.PostgreSql"/>.</param>
    /// <param name="written">The pattern in the dialect.</param>
    /// <param name="refusal">The construct the database would read apart, as a phrase.</param>
    /// <returns><see langword="false"/> when the pattern holds a construct the database would read apart.</returns>
    public static bool TryWrite(
        string pattern,
        ValueObjectPatternDialect dialect,
        [NotNullWhen(true)] out string? written,
        [NotNullWhen(false)] out string? refusal)
    {
        written = null;
        if (!ValueObjectPatternSyntax.TryParse(pattern, out var root))
        {
            refusal = "a construct only .NET reads, such as a lookaround, a backreference, an atomic, conditional or "
                      + "balancing group, or an inline option";
            return false;
        }

        var writer = new Writer(dialect);
        var alternatives = root is PatternAlternation alternation ? alternation.Alternatives : [(PatternSequence)root];
        for (var index = 0; index < alternatives.Length; index++)
        {
            if (index > 0)
            {
                writer.Output.Append('|');
            }

            var items = alternatives[index].Items;
            for (var item = 0; item < items.Length; item++)
            {
                if (!writer.Write(items[item], last: item == items.Length - 1))
                {
                    refusal = writer.Refusal!;
                    return false;
                }
            }
        }

        if (Size(root) > MaximumSize)
        {
            refusal = string.Create(
                CultureInfo.InvariantCulture,
                $"repetitions which, written out, match more than {MaximumSize} characters, which the database refuses as too complex when it first checks a value");
            return false;
        }

        written = writer.Output.ToString();
        refusal = null;
        return true;
    }

    /// <summary>
    /// Counts the characters a pattern matches once each counted repetition is written out, up to one past the most both
    /// engines compile.
    /// </summary>
    /// <param name="node">The pattern, or a node of it.</param>
    /// <returns>The count, or <see cref="MaximumSize"/> plus one past it.</returns>
    private static int Size(PatternNode node) => node switch
    {
        PatternSequence sequence => Sum(sequence.Items),
        PatternAlternation alternation => Sum(alternation.Alternatives),
        PatternGroup group => Size(group.Content),
        PatternQuantifier quantifier => (int)Math.Min(
            (long)Size(quantifier.Item) * Math.Max(quantifier.Maximum ?? quantifier.Minimum, 1),
            MaximumSize + 1),
        PatternAnchor => 0,
        _ => 1,
    };

    /// <summary>Counts the characters of nodes side by side, up to one past the most both engines compile.</summary>
    private static int Sum<TNode>(ImmutableArray<TNode> nodes)
        where TNode : PatternNode
    {
        var total = 0;
        foreach (var node in nodes)
        {
            total = Math.Min(total + Size(node), MaximumSize + 1);
        }

        return total;
    }

    /// <summary>
    /// Writes the nodes of a tree, stopping at the first one the dialect would read apart.
    /// </summary>
    /// <param name="dialect">The dialect.</param>
    private sealed class Writer(ValueObjectPatternDialect dialect)
    {
        public StringBuilder Output { get; } = new();

        public string? Refusal { get; private set; }

        private int MaximumCount => dialect == ValueObjectPatternDialect.PostgreSql ? 255 : 1000;

        /// <summary>Writes a node.</summary>
        /// <param name="node">The node.</param>
        /// <param name="last">Whether it ends the pattern or a top-level alternative, where nothing follows it.</param>
        /// <returns><see langword="false"/> when the dialect would read the node apart.</returns>
        public bool Write(PatternNode node, bool last = false) => node switch
        {
            PatternSequence sequence => sequence.Items.All(item => Write(item)),
            PatternAlternation alternation => WriteAlternation(alternation),
            PatternGroup group => WriteGroup(group),
            PatternQuantifier quantifier => WriteQuantifier(quantifier),
            PatternLiteral literal => AppendCharacter(literal.Character, inClass: false),
            PatternCharacterClass characterClass => WriteClass(characterClass),
            PatternClassEscape escape => Refuse(
                $@"\{escape.Letter}{(escape.Category is null ? string.Empty : "{" + escape.Category + "}")}, which .NET "
                + "reads over Unicode and the database over ASCII or its locale: list the characters in a class, [0-9] for \\d"),
            PatternAnchor anchor => WriteAnchor(anchor.Kind, last),

            // '.', any character but a line feed.
            _ => Refuse(". (any character), which .NET matches over one UTF-16 code unit and the database over a whole "
                        + "character: list the characters in a class"),
        };

        private bool Refuse(string refusal)
        {
            Refusal = refusal;
            return false;
        }

        private bool WriteAlternation(PatternAlternation alternation)
        {
            for (var index = 0; index < alternation.Alternatives.Length; index++)
            {
                if (index > 0)
                {
                    Output.Append('|');
                }

                if (!Write(alternation.Alternatives[index]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool WriteGroup(PatternGroup group)
        {
            Output.Append("(?:");
            if (!Write(group.Content))
            {
                return false;
            }

            Output.Append(')');
            return true;
        }

        private bool WriteQuantifier(PatternQuantifier quantifier)
        {
            if (quantifier.Minimum > MaximumCount || quantifier.Maximum > MaximumCount)
            {
                return Refuse(string.Create(
                    CultureInfo.InvariantCulture,
                    $"a count above {MaximumCount}, the most the database repeats"));
            }

            if (!Write(quantifier.Item))
            {
                return false;
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
            return true;
        }

        private bool WriteAnchor(PatternAnchorKind kind, bool last)
        {
            switch (kind)
            {
                case PatternAnchorKind.StartOfLine or PatternAnchorKind.StartOfText:
                    Output.Append('^');
                    return true;
                case PatternAnchorKind.EndOfText:
                    Output.Append('$');
                    return true;
                case PatternAnchorKind.EndOfLine or PatternAnchorKind.EndOfTextOrFinalNewline when last:
                    // .NET also matches before a final line feed, where both databases match at the end alone.
                    Output.Append(@"\n?$");
                    return true;
                case PatternAnchorKind.EndOfLine or PatternAnchorKind.EndOfTextOrFinalNewline:
                    return Refuse(@"a $ or a \Z anywhere but at the end of the pattern or of one of its top-level "
                                  + @"alternatives, which .NET also matches before a final line feed: move it after the "
                                  + @"group, ^(?:a|b)$ rather than ^(?:a$|b$)");
                default:
                    return Refuse(@"a word boundary, \b or \B, which .NET places by its Unicode \w and the database by "
                                  + "ASCII or its locale");
            }
        }

        private bool WriteClass(PatternCharacterClass characterClass)
        {
            if (characterClass.Negated)
            {
                return Refuse("a negated class, [^…], which .NET matches over one UTF-16 code unit and the database "
                              + "over a whole character: list the characters it accepts");
            }

            if (characterClass.Subtraction is not null)
            {
                return Refuse("a class subtraction, [a-z-[aeiou]], which only .NET reads: list the characters it keeps");
            }

            Output.Append('[');
            foreach (var item in characterClass.Items)
            {
                var written = item switch
                {
                    PatternLiteral literal => AppendCharacter(literal.Character, inClass: true),
                    PatternRange range => AppendRange(range),
                    _ => Write(item),
                };

                if (!written)
                {
                    return false;
                }
            }

            Output.Append(']');
            return true;
        }

        private bool AppendRange(PatternRange range)
        {
            if (range.From <= '\uDFFF' && range.To >= '\uD800')
            {
                return Refuse("a range reaching the surrogates, which .NET matches as halves of a character");
            }

            AppendCharacter(range.From, inClass: true);
            Output.Append('-');
            AppendCharacter(range.To, inClass: true);
            return true;
        }

        private bool AppendCharacter(char character, bool inClass)
        {
            if (char.IsSurrogate(character))
            {
                return Refuse("half of a surrogate pair, which .NET matches as a character of its own");
            }

            if (character is < ' ' or > '~')
            {
                Output.Append(dialect == ValueObjectPatternDialect.PostgreSql
                    ? string.Create(CultureInfo.InvariantCulture, $@"\u{(int)character:X4}")
                    : string.Create(CultureInfo.InvariantCulture, $@"\x{{{(int)character:X}}}"));
                return true;
            }

            if ((inClass ? ClassMetacharacters : Metacharacters).Contains(character, StringComparison.Ordinal))
            {
                Output.Append('\\');
            }

            Output.Append(character);
            return true;
        }
    }
}
