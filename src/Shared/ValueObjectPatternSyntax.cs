using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace AdCodicem.ValueObjects.Shared;

/// <summary>
/// Reads the .NET regular expression a value object declares into a syntax tree, which a package writes in the dialect
/// of another engine, checks against it, or draws values from, or tells that the pattern is outside the subset it reads.
/// </summary>
/// <remarks>
/// <para>
/// This file is linked into each package that takes a pattern out of .NET, as <c>ValueObjectSchemaKeywords</c> is, so
/// that each compiles its own internal copy: the contracts write an XSD pattern from it. What a dialect accepts of the
/// tree is the writer's decision; the reader only refuses what it cannot read with certainty.
/// </para>
/// <para>
/// It reads a pattern as .NET reads it with no <c>RegexOptions</c>, which a schema's pattern text never carries:
/// literals, the escapes of a character (<c>\.</c>, <c>\n</c>, <c>\t</c>, <c>\xHH</c>, <c>\uHHHH</c>…), <c>.</c>,
/// character classes with their ranges, negations and subtractions, the class escapes <c>\d</c>, <c>\w</c>, <c>\s</c>,
/// <c>\p{…}</c> and their negations, the quantifiers <c>*</c>, <c>+</c>, <c>?</c> and <c>{n}</c>, <c>{n,}</c>,
/// <c>{n,m}</c>, greedy or lazy, alternation, capturing, non-capturing and named groups, and the anchors <c>^</c>,
/// <c>$</c>, <c>\A</c>, <c>\z</c>, <c>\Z</c>, <c>\b</c> and <c>\B</c>.
/// </para>
/// <para>
/// It refuses a lookaround, a backreference, an atomic or conditional group, a balancing group, an inline option or
/// comment, <c>\G</c>, <c>\cX</c> and an octal escape, a quantified anchor, a range or a <c>-</c> beside a class escape
/// inside a class, an unescaped <c>[</c> inside a class that does not start a subtraction, and a pattern .NET would not
/// compile. A <c>{</c> that starts no quantifier is a literal, as .NET reads it.
/// </para>
/// </remarks>
internal static class ValueObjectPatternSyntax
{
    /// <summary>
    /// Reads a .NET pattern into its syntax tree.
    /// </summary>
    /// <param name="pattern">The pattern, as <c>ValueObjectSchema.Pattern</c> holds it.</param>
    /// <param name="root">
    /// The tree: a <see cref="PatternSequence"/>, or a <see cref="PatternAlternation"/> of them for a pattern with an
    /// alternation outside any group.
    /// </param>
    /// <returns><see langword="false"/> for a pattern outside the subset the reader reads.</returns>
    public static bool TryParse(string pattern, [NotNullWhen(true)] out PatternNode? root)
    {
        var reader = new Reader(pattern);
        root = reader.ReadAlternation();
        if (root is null || !reader.AtEnd)
        {
            // An unmatched ')' stops the top-level sequence before the end.
            root = null;
            return false;
        }

        return true;
    }

    private sealed class Reader(string pattern)
    {
        private int _position;

        public bool AtEnd => _position == pattern.Length;

        private char? Current => _position < pattern.Length ? pattern[_position] : null;

        public PatternNode? ReadAlternation()
        {
            var alternatives = ImmutableArray.CreateBuilder<PatternSequence>();
            while (true)
            {
                if (ReadSequence() is not { } sequence)
                {
                    return null;
                }

                alternatives.Add(sequence);
                if (Current != '|')
                {
                    break;
                }

                _position++;
            }

            return alternatives.Count == 1 ? alternatives[0] : new PatternAlternation(alternatives.ToImmutable());
        }

        private PatternSequence? ReadSequence()
        {
            var items = ImmutableArray.CreateBuilder<PatternNode>();
            while (Current is { } current && current is not ('|' or ')'))
            {
                if (ReadAtom() is not { } atom || ReadQuantifier(atom) is not { } item)
                {
                    return null;
                }

                items.Add(item);
            }

            return new PatternSequence(items.ToImmutable());
        }

        private PatternNode? ReadAtom()
        {
            var current = pattern[_position];
            switch (current)
            {
                case '(':
                    return ReadGroup();
                case '[':
                    _position++;
                    return ReadClass();
                case '\\':
                    return ReadEscape();
                case '.':
                    _position++;
                    return PatternAnyCharacter.Instance;
                case '^':
                    _position++;
                    return new PatternAnchor(PatternAnchorKind.StartOfLine);
                case '$':
                    _position++;
                    return new PatternAnchor(PatternAnchorKind.EndOfLine);
                case '*' or '+' or '?':
                    // A quantifier following nothing.
                    return null;
                case '{' when TryReadBraces(_position, out _, out _, out _):
                    return null;
                default:
                    // '{' starting no quantifier, '}' and ']' are literals.
                    _position++;
                    return new PatternLiteral(current);
            }
        }

        private PatternNode? ReadQuantifier(PatternNode atom)
        {
            long minimum;
            long? maximum;
            switch (Current)
            {
                case '*':
                    (minimum, maximum) = (0, null);
                    _position++;
                    break;
                case '+':
                    (minimum, maximum) = (1, null);
                    _position++;
                    break;
                case '?':
                    (minimum, maximum) = (0, 1);
                    _position++;
                    break;
                case '{' when TryReadBraces(_position, out minimum, out maximum, out var end):
                    _position = end;
                    break;
                default:
                    return atom;
            }

            var lazy = Current == '?';
            if (lazy)
            {
                _position++;
            }

            // A quantified anchor, which .NET accepts and no other dialect means alike, or a range written backwards and a
            // count past int.MaxValue, which .NET refuses. A quantifier right after this one is refused by the next atom,
            // as a quantifier following nothing.
            if (atom is PatternAnchor
                || minimum > maximum
                || minimum > int.MaxValue
                || maximum > int.MaxValue)
            {
                return null;
            }

            return new PatternQuantifier(atom, (int)minimum, (int?)maximum, lazy);
        }

        /// <summary>Reads <c>{n}</c>, <c>{n,}</c> or <c>{n,m}</c>, which alone make a <c>{</c> a quantifier in .NET.</summary>
        private bool TryReadBraces(int start, out long minimum, out long? maximum, out int end)
        {
            maximum = null;
            end = start + 1;
            if (!TryReadCount(ref end, out minimum))
            {
                return false;
            }

            if (end < pattern.Length && pattern[end] == ',')
            {
                end++;
                if (TryReadCount(ref end, out var upper))
                {
                    maximum = upper;
                }
            }
            else
            {
                maximum = minimum;
            }

            if (end >= pattern.Length || pattern[end] != '}')
            {
                return false;
            }

            end++;
            return true;
        }

        /// <summary>Reads a run of digits as a count, held at <c>int.MaxValue + 1</c> past it, where .NET refuses the pattern.</summary>
        private bool TryReadCount(ref int index, out long count)
        {
            var start = index;
            count = 0;
            while (index < pattern.Length && pattern[index] is >= '0' and <= '9')
            {
                count = Math.Min((count * 10) + (pattern[index] - '0'), int.MaxValue + 1L);
                index++;
            }

            return index > start;
        }

        private PatternGroup? ReadGroup()
        {
            _position++;
            var capturing = true;
            string? name = null;
            if (Current == '?')
            {
                _position++;
                switch (Current)
                {
                    case ':':
                        _position++;
                        capturing = false;
                        break;
                    case '<' or '\'' when !IsLookbehind():
                        var close = pattern[_position] == '<' ? '>' : '\'';
                        var end = pattern.IndexOf(close, _position + 1);
                        if (end < 0)
                        {
                            return null;
                        }

                        name = pattern[(_position + 1)..end];

                        // A balancing group names two groups with a '-'.
                        if (name.Length == 0 || !name.All(static character => char.IsAsciiLetterOrDigit(character) || character == '_'))
                        {
                            return null;
                        }

                        _position = end + 1;
                        break;
                    default:
                        // A lookaround, an atomic or conditional group, an inline option or a comment.
                        return null;
                }
            }

            if (ReadAlternation() is not { } content || Current != ')')
            {
                return null;
            }

            _position++;
            return new PatternGroup(content, capturing, name);
        }

        private bool IsLookbehind()
            => pattern[_position] == '<' && _position + 1 < pattern.Length && pattern[_position + 1] is '=' or '!';

        private PatternNode? ReadEscape()
        {
            _position++;
            if (Current is not { } escaped)
            {
                return null;
            }

            _position++;
            switch (escaped)
            {
                case 'b':
                    return new PatternAnchor(PatternAnchorKind.WordBoundary);
                case 'B':
                    return new PatternAnchor(PatternAnchorKind.NotWordBoundary);
                case 'A':
                    return new PatternAnchor(PatternAnchorKind.StartOfText);
                case 'z':
                    return new PatternAnchor(PatternAnchorKind.EndOfText);
                case 'Z':
                    return new PatternAnchor(PatternAnchorKind.EndOfTextOrFinalNewline);
                default:
                    return ReadCharacterEscape(escaped, inClass: false);
            }
        }

        /// <summary>
        /// Reads what follows a backslash, but an anchor: a class escape, or the character it stands for.
        /// </summary>
        private PatternNode? ReadCharacterEscape(char escaped, bool inClass)
        {
            switch (escaped)
            {
                case 'd' or 'D' or 'w' or 'W' or 's' or 'S':
                    return new PatternClassEscape(escaped, null);
                case 'p' or 'P':
                    if (Current != '{')
                    {
                        return null;
                    }

                    var close = pattern.IndexOf('}', _position);
                    if (close <= _position + 1)
                    {
                        return null;
                    }

                    var category = pattern[(_position + 1)..close];
                    _position = close + 1;
                    return new PatternClassEscape(escaped, category);
                case 'b' when inClass:
                    return new PatternLiteral('\b');
                case 'n':
                    return new PatternLiteral('\n');
                case 'r':
                    return new PatternLiteral('\r');
                case 't':
                    return new PatternLiteral('\t');
                case 'f':
                    return new PatternLiteral('\f');
                case 'v':
                    return new PatternLiteral('\v');
                case 'a':
                    return new PatternLiteral('\a');
                case 'e':
                    return new PatternLiteral('\u001B');
                case 'x':
                    return ReadHexadecimal(2);
                case 'u':
                    return ReadHexadecimal(4);
                default:
                    // A word character is a backreference, an octal or control escape, or one .NET does not know; any
                    // other character escapes itself. Outside ASCII, .NET refuses one that is a word character.
                    return char.IsAscii(escaped) && !char.IsAsciiLetterOrDigit(escaped) && escaped != '_'
                        ? new PatternLiteral(escaped)
                        : null;
            }
        }

        private PatternLiteral? ReadHexadecimal(int digits)
        {
            if (_position + digits > pattern.Length
                || !int.TryParse(pattern.AsSpan(_position, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
            {
                return null;
            }

            _position += digits;
            return new PatternLiteral((char)code);
        }

        /// <summary>Reads a class, its opening bracket read.</summary>
        private PatternCharacterClass? ReadClass()
        {
            var negated = Current == '^';
            if (negated)
            {
                _position++;
            }

            var items = ImmutableArray.CreateBuilder<PatternNode>();
            PatternCharacterClass? subtraction = null;
            while (true)
            {
                if (Current is not { } current)
                {
                    return null;
                }

                if (current == ']' && items.Count > 0)
                {
                    _position++;
                    break;
                }

                if (current == '-' && items.Count > 0 && _position + 1 < pattern.Length && pattern[_position + 1] == '[')
                {
                    // A subtraction, which must close the class.
                    _position += 2;
                    subtraction = ReadClass();
                    if (subtraction is null || Current != ']')
                    {
                        return null;
                    }

                    _position++;
                    break;
                }

                if (ReadClassItem() is not { } item)
                {
                    return null;
                }

                items.Add(item);
            }

            return new PatternCharacterClass(negated, items.ToImmutable(), subtraction);
        }

        /// <summary>Reads a character, a range or a class escape inside a class; ']' first in it is a literal.</summary>
        private PatternNode? ReadClassItem()
        {
            if (ReadClassCharacter() is not { } first)
            {
                return null;
            }

            // A '-' before ']' or before a subtraction is not a range; '-' beside a class escape is read by .NET as a
            // literal, and left out here.
            if (Current != '-' || _position + 1 >= pattern.Length || pattern[_position + 1] is ']' or '[')
            {
                return first;
            }

            if (first is not PatternLiteral from)
            {
                return null;
            }

            _position++;
            if (ReadClassCharacter() is not PatternLiteral to || to.Character < from.Character)
            {
                return null;
            }

            // After a range, .NET reads a '-' as a literal; that is left out too.
            return Current == '-' && _position + 1 < pattern.Length && pattern[_position + 1] is not (']' or '[')
                ? null
                : new PatternRange(from.Character, to.Character);
        }

        private PatternNode? ReadClassCharacter()
        {
            var current = pattern[_position++];
            switch (current)
            {
                case '\\':
                    if (Current is not { } escaped)
                    {
                        return null;
                    }

                    _position++;
                    return ReadCharacterEscape(escaped, inClass: true);
                case '[':
                    // .NET reads it as a literal; a reader of another dialect may not.
                    return null;
                default:
                    return new PatternLiteral(current);
            }
        }
    }
}

/// <summary>A node of the syntax tree <see cref="ValueObjectPatternSyntax"/> reads.</summary>
internal abstract class PatternNode;

/// <summary>Two alternatives or more, of which a match takes one.</summary>
/// <param name="alternatives">The alternatives, in order.</param>
internal sealed class PatternAlternation(ImmutableArray<PatternSequence> alternatives) : PatternNode
{
    /// <summary>Gets the alternatives, in order.</summary>
    public ImmutableArray<PatternSequence> Alternatives { get; } = alternatives;
}

/// <summary>Nodes matched one after the other; none for the empty pattern.</summary>
/// <param name="items">The nodes, in order.</param>
internal sealed class PatternSequence(ImmutableArray<PatternNode> items) : PatternNode
{
    /// <summary>Gets the nodes, in order.</summary>
    public ImmutableArray<PatternNode> Items { get; } = items;
}

/// <summary>A group: <c>(…)</c>, <c>(?:…)</c>, <c>(?&lt;name&gt;…)</c> or <c>(?'name'…)</c>.</summary>
/// <param name="content">What the group holds: a sequence or an alternation.</param>
/// <param name="capturing">Whether the group captures.</param>
/// <param name="name">The name of a named group.</param>
internal sealed class PatternGroup(PatternNode content, bool capturing, string? name) : PatternNode
{
    /// <summary>Gets what the group holds: a <see cref="PatternSequence"/> or a <see cref="PatternAlternation"/>.</summary>
    public PatternNode Content { get; } = content;

    /// <summary>Gets a value indicating whether the group captures.</summary>
    public bool Capturing { get; } = capturing;

    /// <summary>Gets the name of a named group, or <see langword="null"/>.</summary>
    public string? Name { get; } = name;
}

/// <summary>A node repeated between two counts.</summary>
/// <param name="item">The node repeated, never an anchor.</param>
/// <param name="minimum">The fewest repetitions.</param>
/// <param name="maximum">The most, or <see langword="null"/> for no limit.</param>
/// <param name="lazy">Whether the quantifier is lazy, which changes which match is found, never whether one is.</param>
internal sealed class PatternQuantifier(PatternNode item, int minimum, int? maximum, bool lazy) : PatternNode
{
    /// <summary>Gets the node repeated, never an anchor.</summary>
    public PatternNode Item { get; } = item;

    /// <summary>Gets the fewest repetitions.</summary>
    public int Minimum { get; } = minimum;

    /// <summary>Gets the most repetitions, or <see langword="null"/> for no limit.</summary>
    public int? Maximum { get; } = maximum;

    /// <summary>Gets a value indicating whether the quantifier is lazy.</summary>
    public bool Lazy { get; } = lazy;
}

/// <summary>One character, written as itself or escaped.</summary>
/// <param name="character">The character.</param>
internal sealed class PatternLiteral(char character) : PatternNode
{
    /// <summary>Gets the character.</summary>
    public char Character { get; } = character;
}

/// <summary>A range of characters in a class, <c>a-z</c>.</summary>
/// <param name="from">The first character.</param>
/// <param name="to">The last character, never before the first.</param>
internal sealed class PatternRange(char from, char to) : PatternNode
{
    /// <summary>Gets the first character.</summary>
    public char From { get; } = from;

    /// <summary>Gets the last character.</summary>
    public char To { get; } = to;
}

/// <summary><c>.</c>: any character but <c>\n</c>.</summary>
internal sealed class PatternAnyCharacter : PatternNode
{
    /// <summary>Gets the one instance.</summary>
    public static PatternAnyCharacter Instance { get; } = new();

    private PatternAnyCharacter()
    {
    }
}

/// <summary>A class escape: <c>\d</c>, <c>\w</c>, <c>\s</c>, <c>\p{…}</c>, or its negation in upper case.</summary>
/// <param name="letter">The letter after the backslash: <c>d</c>, <c>D</c>, <c>w</c>, <c>W</c>, <c>s</c>, <c>S</c>, <c>p</c> or <c>P</c>.</param>
/// <param name="category">The Unicode category or named block of <c>\p</c> and <c>\P</c>, as written.</param>
internal sealed class PatternClassEscape(char letter, string? category) : PatternNode
{
    /// <summary>Gets the letter after the backslash.</summary>
    public char Letter { get; } = letter;

    /// <summary>Gets the category or named block of <c>\p</c> and <c>\P</c>, as written, or <see langword="null"/>.</summary>
    public string? Category { get; } = category;

    /// <summary>Gets a value indicating whether the escape matches what its lower-case form does not.</summary>
    public bool Negated => char.IsUpper(Letter);
}

/// <summary>
/// A character class, <c>[…]</c>: its items are <see cref="PatternLiteral"/>, <see cref="PatternRange"/> and
/// <see cref="PatternClassEscape"/> nodes.
/// </summary>
/// <param name="negated">Whether the class matches what its items do not.</param>
/// <param name="items">The items, at least one.</param>
/// <param name="subtraction">The class subtracted from it, <c>[a-z-[aeiou]]</c>.</param>
internal sealed class PatternCharacterClass(bool negated, ImmutableArray<PatternNode> items, PatternCharacterClass? subtraction) : PatternNode
{
    /// <summary>Gets a value indicating whether the class matches what its items do not.</summary>
    public bool Negated { get; } = negated;

    /// <summary>Gets the items, at least one.</summary>
    public ImmutableArray<PatternNode> Items { get; } = items;

    /// <summary>Gets the class subtracted from it, or <see langword="null"/>.</summary>
    public PatternCharacterClass? Subtraction { get; } = subtraction;
}

/// <summary>A position a match is held to, which matches no character.</summary>
/// <param name="kind">The position.</param>
internal sealed class PatternAnchor(PatternAnchorKind kind) : PatternNode
{
    /// <summary>Gets the position.</summary>
    public PatternAnchorKind Kind { get; } = kind;
}

/// <summary>The positions an anchor holds a match to, without <c>RegexOptions.Multiline</c>.</summary>
internal enum PatternAnchorKind
{
    /// <summary><c>^</c>: the start of the text.</summary>
    StartOfLine,

    /// <summary><c>$</c>: the end of the text, or before a <c>\n</c> that ends it.</summary>
    EndOfLine,

    /// <summary><c>\A</c>: the start of the text.</summary>
    StartOfText,

    /// <summary><c>\z</c>: the end of the text.</summary>
    EndOfText,

    /// <summary><c>\Z</c>: the end of the text, or before a <c>\n</c> that ends it.</summary>
    EndOfTextOrFinalNewline,

    /// <summary><c>\b</c>: between a word character and another.</summary>
    WordBoundary,

    /// <summary><c>\B</c>: anywhere <c>\b</c> is not.</summary>
    NotWordBoundary,
}
