using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.Testing.Data;

/// <summary>
/// Draws strings a pattern matches whole, of a length inside a window, from the tree <see cref="ValueObjectPatternSyntax"/>
/// reads: the subset of a .NET pattern whose characters it can enumerate.
/// </summary>
/// <remarks>
/// <para>
/// It draws literals and escapes, character classes with their ranges, negations and subtractions, <c>.</c>, <c>\d</c>,
/// <c>\w</c> and <c>\s</c> and their negations, every quantifier, alternation and every group the reader reads. The
/// anchors draw nothing: <c>^</c>, <c>$</c>, <c>\A</c>, <c>\z</c> and <c>\Z</c> hold at either end of a whole match, and
/// <c>\b</c> and <c>\B</c> are left to the pattern itself, which the type checks every candidate against.
/// </para>
/// <para>
/// A class escape draws from ASCII: <c>\d</c> from the ten digits, <c>\w</c> from the letters, the digits and the
/// underscore, <c>\s</c> from the space and the controls from tab to carriage return. Those are characters .NET matches
/// with each, and printable ASCII is all it matches with each there, so a negated class or escape, and <c>.</c>, draw from
/// the printable ASCII characters it matches. A pattern holding a <c>\p{…}</c> or <c>\P{…}</c> category, a class emptied
/// by its subtraction, or a construct the reader refuses is outside the subset: <see cref="For"/> gives no sampler for it.
/// </para>
/// <para>
/// An open quantifier repeats its item at most <c>MaxExtraLength</c> times beyond its minimum, unless the window asks for
/// more. A pattern no anchor holds at an end matches inside a longer string: when its longest match is shorter than the
/// window asks, a match is padded with ASCII letters and digits at that end, to at most <c>MaxExtraLength</c> characters
/// beyond the window's minimum. A draw can fail where a match of a fitting length exists, when the lengths a part matches
/// leave gaps: the caller draws again.
/// </para>
/// <para>
/// Read loosely (<see cref="Loosely"/>), for rejection sampling, a pattern also loses its lookarounds, and a <c>\p{…}</c>
/// or <c>\P{…}</c> category draws from the printable ASCII characters it matches: what that reading draws may not match
/// the pattern, which the caller then checks.
/// </para>
/// </remarks>
internal sealed class PatternSampler
{
    /// <summary>The length that stands for no maximum, which saturating arithmetic keeps.</summary>
    private const int Unbounded = int.MaxValue;

    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';

    /// <summary>What a match no anchor holds at an end is padded with there: ASCII letters and digits.</summary>
    private static readonly CharacterSet Padding = new([('0', '9'), ('A', 'Z'), ('a', 'z')]);

    private static readonly ConcurrentDictionary<string, PatternSampler?> Samplers = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, PatternSampler?> LooseSamplers = new(StringComparer.Ordinal);

    private readonly PatternNode _root;
    private readonly bool _loose;
    private readonly bool _openStart;
    private readonly bool _openEnd;
    private readonly Dictionary<PatternNode, (int Minimum, int Maximum)> _lengths = [];
    private readonly Dictionary<PatternNode, CharacterSet> _characters = [];

    private PatternSampler(PatternNode root, bool loose)
    {
        _root = root;
        _loose = loose;

        // An alternation outside any group is open at an end only when none of its alternatives is held there: padding
        // one alternative an anchor holds would break it.
        var alternatives = root is PatternAlternation alternation ? alternation.Alternatives : [(PatternSequence)root];
        _openStart = alternatives.All(static sequence => sequence.Items is not [PatternAnchor { Kind: PatternAnchorKind.StartOfLine or PatternAnchorKind.StartOfText }, ..]);
        _openEnd = alternatives.All(static sequence => sequence.Items is not [.., PatternAnchor { Kind: PatternAnchorKind.EndOfLine or PatternAnchorKind.EndOfText or PatternAnchorKind.EndOfTextOrFinalNewline }]);
    }

    /// <summary>Gets the length of the shortest string the pattern matches whole.</summary>
    public int MinimumLength => _lengths[_root].Minimum;

    /// <summary>Gets the sampler of a pattern, read once, or <see langword="null"/> for a pattern outside the subset.</summary>
    /// <param name="pattern">The pattern, as <c>ValueObjectSchema.Pattern</c> holds it.</param>
    /// <returns>The sampler, or <see langword="null"/>.</returns>
    public static PatternSampler? For(string pattern) => Samplers.GetOrAdd(pattern, static text => Read(text));

    /// <summary>
    /// Gets the sampler of a pattern read loosely, once: without its lookarounds, and with each category drawn from the
    /// printable ASCII characters it matches. What it draws may not match the pattern, and has to be checked against it.
    /// </summary>
    /// <param name="pattern">The pattern, as <c>ValueObjectSchema.Pattern</c> holds it.</param>
    /// <returns>The sampler, or <see langword="null"/> for a pattern even that reading leaves outside the subset.</returns>
    public static PatternSampler? Loosely(string pattern)
        => LooseSamplers.GetOrAdd(pattern, static text => Read(WithoutLookarounds(text), loose: true));

    /// <summary>Reads a pattern into a sampler, without the cache.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="loose">Whether a category draws from the printable ASCII characters it matches, rather than leaving the pattern outside the subset.</param>
    /// <returns>The sampler, or <see langword="null"/> for a pattern outside the subset.</returns>
    public static PatternSampler? Read(string pattern, bool loose = false)
    {
        if (!ValueObjectPatternSyntax.TryParse(pattern, out var root))
        {
            return null;
        }

        var sampler = new PatternSampler(root, loose);
        return sampler.Compile(root) ? sampler : null;
    }

    /// <summary>
    /// Gets a pattern without its lookarounds: what is left matches every string the pattern matches, and more.
    /// </summary>
    /// <param name="pattern">The pattern.</param>
    /// <returns>The pattern without its lookarounds; itself when it holds none.</returns>
    public static string WithoutLookarounds(string pattern)
    {
        var builder = new StringBuilder(pattern.Length);
        var index = 0;
        while (index < pattern.Length)
        {
            if (IsLookaround(pattern, index))
            {
                index = AfterGroup(pattern, index);
                continue;
            }

            var next = pattern[index] switch
            {
                '\\' => Math.Min(index + 2, pattern.Length),
                '[' => AfterClass(pattern, index),
                _ => index + 1,
            };
            builder.Append(pattern, index, next - index);
            index = next;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Draws a string the pattern matches, whose length is between two, both included: a whole match, padded at an end
    /// no anchor holds when no match is long enough.
    /// </summary>
    /// <param name="random">The source of randomness.</param>
    /// <param name="minimumLength">The fewest characters.</param>
    /// <param name="maximumLength">The most characters.</param>
    /// <param name="maximumExtraRepetitions">The most repetitions an open quantifier adds to its minimum unless the window asks for more.</param>
    /// <returns>The string, or <see langword="null"/> when the draw could not fit the window.</returns>
    public string? Draw(Random random, int minimumLength, int maximumLength, int maximumExtraRepetitions)
    {
        // A match shorter than the window asks is drawn as any match, then padded at an end no anchor holds, to a length
        // the window holds, at most as many characters beyond its minimum as an open quantifier adds.
        var padded = (_openStart || _openEnd) && _lengths[_root].Maximum < minimumLength && minimumLength <= maximumLength;
        var builder = new StringBuilder();
        if (!new Drawing(this, random, maximumExtraRepetitions, builder).Draw(_root, padded ? 0 : minimumLength, maximumLength))
        {
            return null;
        }

        if (!padded)
        {
            return builder.ToString();
        }

        var length = random.NextInt64(minimumLength, Math.Min(maximumLength, (long)minimumLength + maximumExtraRepetitions) + 1);
        var padding = (int)length - builder.Length;
        var before = !_openStart ? 0 : _openEnd ? random.Next(padding + 1) : padding;
        var match = builder.ToString();
        builder.Clear();
        Pad(builder, random, before);
        builder.Append(match);
        Pad(builder, random, padding - before);
        return builder.ToString();
    }

    private static int Add(long left, long right) => (int)Math.Min(left + right, Unbounded);

    private static void Pad(StringBuilder builder, Random random, int count)
    {
        for (var index = 0; index < count; index++)
        {
            builder.Append(Padding.Pick(random));
        }
    }

    private static bool IsLookaround(string pattern, int index)
        => pattern.AsSpan(index) is ['(', '?', '=' or '!', ..] or ['(', '?', '<', '=' or '!', ..];

    /// <summary>Gets the index after the group that opens at an index, nested groups and classes included.</summary>
    private static int AfterGroup(string pattern, int index)
    {
        var depth = 0;
        while (index < pattern.Length)
        {
            switch (pattern[index])
            {
                case '\\':
                    index += 2;
                    continue;
                case '[':
                    index = AfterClass(pattern, index);
                    continue;
                case '(':
                    depth++;
                    break;
                case ')' when --depth == 0:
                    return index + 1;
            }

            index++;
        }

        return pattern.Length;
    }

    /// <summary>
    /// Gets the index after the first unescaped <c>]</c> of the class that opens at an index. A subtraction, which closes
    /// its class, ends there too, and the <c>]</c> after it is then read as a character outside, which changes nothing
    /// about the groups around it.
    /// </summary>
    private static int AfterClass(string pattern, int index)
    {
        index++;
        if (index < pattern.Length && pattern[index] == '^')
        {
            index++;
        }

        // A ']' first in a class is one of its characters.
        if (index < pattern.Length && pattern[index] == ']')
        {
            index++;
        }

        while (index < pattern.Length)
        {
            switch (pattern[index])
            {
                case '\\':
                    index += 2;
                    continue;
                case ']':
                    return index + 1;
            }

            index++;
        }

        return pattern.Length;
    }

    private static int Multiply(long left, long right) => (int)Math.Min(left * right, Unbounded);

    /// <summary>
    /// Records the lengths of a node and of every node under it, and the characters of every node that matches one.
    /// </summary>
    /// <returns><see langword="false"/> for a node outside the subset.</returns>
    private bool Compile(PatternNode node)
    {
        (int Minimum, int Maximum) lengths;
        switch (node)
        {
            case PatternSequence sequence:
                lengths = (0, 0);
                foreach (var item in sequence.Items)
                {
                    if (!Compile(item))
                    {
                        return false;
                    }

                    var (minimum, maximum) = _lengths[item];
                    lengths = (Add(lengths.Minimum, minimum), Add(lengths.Maximum, maximum));
                }

                break;
            case PatternAlternation alternation:
                lengths = (Unbounded, 0);
                foreach (var alternative in alternation.Alternatives)
                {
                    if (!Compile(alternative))
                    {
                        return false;
                    }

                    var (minimum, maximum) = _lengths[alternative];
                    lengths = (Math.Min(lengths.Minimum, minimum), Math.Max(lengths.Maximum, maximum));
                }

                break;
            case PatternGroup group:
                if (!Compile(group.Content))
                {
                    return false;
                }

                lengths = _lengths[group.Content];
                break;
            case PatternQuantifier quantifier:
                if (!Compile(quantifier.Item))
                {
                    return false;
                }

                var repeated = _lengths[quantifier.Item];
                lengths = (Multiply(repeated.Minimum, quantifier.Minimum), Multiply(repeated.Maximum, quantifier.Maximum ?? Unbounded));
                break;
            case PatternAnchor:
                lengths = (0, 0);
                break;
            default:
                if (Characters(node, _loose) is not { Count: > 0 } characters)
                {
                    return false;
                }

                _characters[node] = characters;
                lengths = (1, 1);
                break;
        }

        _lengths[node] = lengths;
        return true;
    }

    /// <summary>
    /// Gets the characters a node matching one character draws from, or <see langword="null"/> for a category escape read
    /// strictly.
    /// </summary>
    private static CharacterSet? Characters(PatternNode node, bool loose)
    {
        switch (node)
        {
            case PatternLiteral literal:
                return new([(literal.Character, literal.Character)]);
            case PatternRange range:
                return new([(range.From, range.To)]);
            case PatternClassEscape { Category: null } escape:
                CharacterSet set = char.ToLowerInvariant(escape.Letter) switch
                {
                    'd' => new([('0', '9')]),
                    'w' => new([('0', '9'), ('A', 'Z'), ('_', '_'), ('a', 'z')]),
                    _ => new([('\t', '\r'), (' ', ' ')]),
                };
                return escape.Negated ? CharacterSet.Printable.Except(set) : set;
            case PatternCharacterClass characterClass:
                var items = new List<(char From, char To)>();
                foreach (var item in characterClass.Items)
                {
                    if (Characters(item, loose) is not { } characters)
                    {
                        return null;
                    }

                    items.AddRange(characters.Ranges);
                }

                var union = new CharacterSet(items);
                var result = characterClass.Negated ? CharacterSet.Printable.Except(union) : union;
                if (characterClass.Subtraction is not { } subtraction)
                {
                    return result;
                }

                return Characters(subtraction, loose) is { } removed ? result.Except(removed) : null;
            case PatternAnyCharacter:
                return CharacterSet.Printable;
            default:
                // A category escape, \p{…} or \P{…}.
                return loose ? Members((PatternClassEscape)node) : null;
        }
    }

    /// <summary>
    /// Gets the printable ASCII characters a category escape matches, as .NET tells them; <see langword="null"/> for a
    /// category .NET does not know.
    /// </summary>
    private static CharacterSet? Members(PatternClassEscape escape)
    {
        Regex category;
        try
        {
            category = new Regex($"\\{escape.Letter}{{{escape.Category}}}", RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var members = new List<(char From, char To)>();
        for (var character = FirstPrintable; character <= LastPrintable; character++)
        {
            if (category.IsMatch(character.ToString()))
            {
                members.Add((character, character));
            }
        }

        return new(members);
    }

    /// <summary>A set of characters, as sorted ranges that neither overlap nor touch.</summary>
    private sealed class CharacterSet
    {
        public CharacterSet(IEnumerable<(char From, char To)> ranges)
        {
            var merged = new List<(char From, char To)>();
            foreach (var range in ranges.OrderBy(static range => range.From))
            {
                if (merged.Count > 0 && range.From <= merged[^1].To + 1)
                {
                    merged[^1] = (merged[^1].From, (char)Math.Max(merged[^1].To, range.To));
                }
                else
                {
                    merged.Add(range);
                }
            }

            Ranges = merged;
            Count = merged.Sum(static range => range.To - range.From + 1);
        }

        /// <summary>Gets the printable ASCII characters, what a negation and <c>.</c> draw from.</summary>
        public static CharacterSet Printable { get; } = new([(FirstPrintable, LastPrintable)]);

        public List<(char From, char To)> Ranges { get; }

        public int Count { get; }

        public CharacterSet Except(CharacterSet removed)
        {
            var kept = new List<(char From, char To)>();
            foreach (var (from, to) in Ranges)
            {
                var start = (int)from;
                foreach (var (cutFrom, cutTo) in removed.Ranges)
                {
                    if (cutTo < start || cutFrom > to)
                    {
                        continue;
                    }

                    if (cutFrom > start)
                    {
                        kept.Add(((char)start, (char)(cutFrom - 1)));
                    }

                    start = cutTo + 1;
                }

                if (start <= to)
                {
                    kept.Add(((char)start, to));
                }
            }

            return new(kept);
        }

        public char Pick(Random random)
        {
            var index = random.Next(Count);
            var range = 0;
            while (index > Ranges[range].To - Ranges[range].From)
            {
                index -= Ranges[range].To - Ranges[range].From + 1;
                range++;
            }

            return (char)(Ranges[range].From + index);
        }
    }

    /// <summary>One draw: the window of each node computed from what the nodes around it can still take.</summary>
    private sealed class Drawing(PatternSampler sampler, Random random, int maximumExtraRepetitions, StringBuilder builder)
    {
        public bool Draw(PatternNode node, long low, long high)
        {
            var (minimum, maximum) = sampler._lengths[node];
            low = Math.Max(low, minimum);
            high = Math.Min(high, maximum);
            if (low > high)
            {
                return false;
            }

            switch (node)
            {
                case PatternSequence sequence:
                    return Sequence(sequence.Items, low, high);
                case PatternAlternation alternation:
                    var fitting = alternation.Alternatives
                        .Where(alternative => sampler._lengths[alternative].Minimum <= high && sampler._lengths[alternative].Maximum >= low)
                        .ToArray();
                    return fitting.Length > 0 && Draw(fitting[random.Next(fitting.Length)], low, high);
                case PatternGroup group:
                    return Draw(group.Content, low, high);
                case PatternQuantifier quantifier:
                    return Repeat(quantifier, low, high);
                case PatternAnchor:
                    return true;
                default:
                    builder.Append(sampler._characters[node].Pick(random));
                    return true;
            }
        }

        /// <summary>
        /// Draws each item in turn, in the window that leaves the items after it what they need and no more than they take.
        /// </summary>
        private bool Sequence(IReadOnlyList<PatternNode> items, long low, long high)
        {
            var restMinimum = new long[items.Count + 1];
            var restMaximum = new long[items.Count + 1];
            for (var index = items.Count - 1; index >= 0; index--)
            {
                var (minimum, maximum) = sampler._lengths[items[index]];
                restMinimum[index] = Add(restMinimum[index + 1], minimum);
                restMaximum[index] = Add(restMaximum[index + 1], maximum);
            }

            var start = builder.Length;
            for (var index = 0; index < items.Count; index++)
            {
                var written = builder.Length - start;
                if (!Draw(items[index], low - written - restMaximum[index + 1], high - written - restMinimum[index + 1]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Draws a count of repetitions the window can hold, then each repetition in the window the ones after it leave.
        /// </summary>
        private bool Repeat(PatternQuantifier quantifier, long low, long high)
        {
            var (itemMinimum, itemMaximum) = sampler._lengths[quantifier.Item];

            // Enough repetitions to reach the window, and no more than fit in it: an open quantifier adds a few beyond its
            // minimum, unless the window asks for more; an item that may match nothing needs no more than one a character.
            long countLow = itemMaximum > 0 ? Math.Max(quantifier.Minimum, (low + itemMaximum - 1) / itemMaximum) : quantifier.Minimum;
            long countHigh = quantifier.Maximum ?? Math.Max((long)quantifier.Minimum + maximumExtraRepetitions, countLow);
            countHigh = Math.Min(countHigh, itemMinimum > 0 ? high / itemMinimum : quantifier.Minimum + high);
            if (countLow > countHigh)
            {
                return false;
            }

            var count = random.NextInt64(countLow, countHigh + 1);
            var start = builder.Length;
            for (var repetition = 0L; repetition < count; repetition++)
            {
                var written = builder.Length - start;
                var rest = count - repetition - 1;
                if (!Draw(quantifier.Item, low - written - Multiply(rest, itemMaximum), high - written - (rest * itemMinimum)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
