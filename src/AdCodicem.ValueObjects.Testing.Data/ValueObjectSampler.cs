using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Metadata;
using AdCodicem.ValueObjects.Shared;

namespace AdCodicem.ValueObjects.Testing.Data;

/// <summary>
/// Draws value objects their own rules accept, from the schema they declare: test data that never restates a rule the
/// type already holds.
/// </summary>
/// <remarks>
/// <para>
/// A candidate comes from <c>TSelf.Schema</c>. A closed value set gives one of its known values, an open one gives a known
/// value part of the time (<see cref="ValueObjectSamplerOptions.KnownValueShare"/>). A string gets a length between
/// <c>MinLength</c> and <c>MaxLength</c>; without a maximum, at most <see cref="ValueObjectSamplerOptions.MaxExtraLength"/>
/// characters beyond the shortest it can be, its <c>MinLength</c>, the shortest match of its pattern, or one character
/// when it declares neither. The pattern the type declares fills it: drawn from the pattern when it is inside the subset
/// the built-in sampler reads, padded at an end no anchor holds when its matches are too short, and otherwise drawn by
/// rejection sampling, from the pattern read without its lookarounds and with its categories reduced to printable ASCII,
/// or from ASCII letters and digits, and kept only once the pattern matches. A number, a date, a time or a duration gets a
/// value between <c>Minimum</c> and <c>Maximum</c>, or across the type's own range on a side left open; a character
/// keeps to printable ASCII there, unless the bound declared on the other side lies beyond it, where the open side runs
/// to the type's own extreme. A <see cref="Guid"/> and a <see cref="bool"/> are drawn as they are. A generator
/// registered with <see cref="ValueObjectSamplerOptions.Use{TSelf, TValue}"/> replaces all of that for its type.
/// </para>
/// <para>
/// Each candidate then goes through <c>TSelf.TryCreate</c>, so the normalizer and the validators apply and nothing the type
/// refuses comes out. After <see cref="ValueObjectSamplerOptions.MaxAttempts"/> candidates, the sampler falls back on the
/// example the type declares, and failing that throws a <see cref="ValueObjectSamplingException"/> that shows the
/// registration to add, or names the registered generator whose values the type refused. A rule no schema carries, a
/// checksum for one, makes most candidates fail: the example then comes out most of the time, which a generator registered
/// for the type avoids.
/// </para>
/// <para>
/// A sampler is as thread-safe as the <see cref="Random"/> it draws from: <see cref="Random.Shared"/> is, a
/// <see cref="Random"/> created with a seed is not, and replays the same values given the same calls. The typed members
/// read <c>TSelf.Schema</c> and never the registry.
/// </para>
/// </remarks>
public sealed class ValueObjectSampler
{
    /// <summary>What a string is drawn from when no pattern draws it: ASCII letters and digits.</summary>
    private const string Alphanumerics = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>The pattern of each type, compiled once, which the strings drawn without its tree are checked against.</summary>
    private static readonly ConcurrentDictionary<string, Regex?> Filters = new(StringComparer.Ordinal);

    private readonly Random _random;
    private readonly ValueObjectSamplerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectSampler"/> class.
    /// </summary>
    /// <param name="random">The source of randomness: <see cref="Random.Shared"/>, or a <see cref="Random"/> created with a seed to replay a run.</param>
    /// <param name="options">How it draws, or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="random"/> is <see langword="null"/>.</exception>
    public ValueObjectSampler(Random random, ValueObjectSamplerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(random);
        _random = random;
        _options = options ?? new ValueObjectSamplerOptions();
    }

    /// <summary>
    /// Draws a value the type accepts: a candidate built from its schema, kept only once its rules accept it, or the
    /// example it declares once no candidate passed.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <returns>The value object.</returns>
    /// <exception cref="ValueObjectSamplingException">
    /// No candidate, drawn from the schema or from the generator registered for the type, passed its rules, and it declares
    /// no example they accept.
    /// </exception>
    public TSelf Next<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
        => TryNext<TSelf, TValue>(out var value, out var refusal)
            ? value
            : throw new ValueObjectSamplingException(
                typeof(TSelf),
                _options.MaxAttempts,
                refusal.ErrorCode,
                $"options.Use<{TypeNames.Of(typeof(TSelf))}, {TypeNames.Of(typeof(TValue))}>(random => ...)",
                fromGenerator: _options.GeneratorFor<TSelf, TValue>() is not null);

    /// <summary>
    /// Draws a value the type accepts, or reports why none could be drawn: the refusal of the last candidate, or no refusal
    /// when the schema gave no candidate at all.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="value">The value object, or <see langword="default"/> when none could be drawn.</param>
    /// <param name="refusal">
    /// <see cref="ValidationResult.Success"/> when a value was drawn; otherwise the refusal of the last candidate, or
    /// <see cref="ValidationResult.Success"/> again when the schema gave none.
    /// </param>
    /// <returns><see langword="true"/> when a value was drawn.</returns>
    public bool TryNext<TSelf, TValue>(out TSelf value, out ValidationResult refusal)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var schema = TSelf.Schema;
        var known = Known<TValue>(schema);
        var generator = _options.GeneratorFor<TSelf, TValue>();
        refusal = ValidationResult.Success;
        for (var attempt = 0; attempt < _options.MaxAttempts; attempt++)
        {
            if (!TryCandidate(schema, known, generator, out var candidate))
            {
                continue;
            }

            if (TSelf.TryCreate(candidate, out value, out var validation))
            {
                refusal = ValidationResult.Success;
                return true;
            }

            refusal = validation;
        }

        if (TryExample<TSelf, TValue>(schema, out value))
        {
            refusal = ValidationResult.Success;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Draws a value of a value object known only by its descriptor, as <see cref="Next{TSelf, TValue}"/> draws it, closed
    /// over the descriptor's type arguments through its visitor.
    /// </summary>
    /// <param name="descriptor">The descriptor, as <c>ValueObjectRegistry</c> holds it.</param>
    /// <returns>The value object, boxed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ValueObjectSamplingException">
    /// No candidate, drawn from the schema or from the generator registered for the type, passed its rules, and it declares
    /// no example they accept.
    /// </exception>
    public object Next(ValueObjectDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.Accept(new NextVisitor(this));
    }

    /// <summary>
    /// Gets the accepted values at the edges the schema declares: its <c>Minimum</c> and <c>Maximum</c>, a string of its
    /// <c>MinLength</c> (the empty string without one) and of its <c>MaxLength</c>, and each known value, each once its
    /// rules accept it, normalized and without repeats.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <returns>The values, in that order; none for a schema that declares no edge.</returns>
    /// <remarks>A property over a wide type lands on its edges by chance once in a blue moon: draw these as well.</remarks>
    public IEnumerable<TValue> Boundaries<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var schema = TSelf.Schema;
        var candidates = new List<TValue>();
        if (Underlying<TValue>.Instance is { } underlying)
        {
            if (Bound(underlying, schema.Minimum, out var minimum))
            {
                candidates.Add(minimum);
            }

            if (Bound(underlying, schema.Maximum, out var maximum))
            {
                candidates.Add(maximum);
            }
        }
        else if (typeof(TValue) == typeof(string))
        {
            AddText(candidates, EdgeText(schema, Math.Max(schema.MinLength ?? 0, 0)));
            if (schema.MaxLength is { } longest and >= 0)
            {
                AddText(candidates, EdgeText(schema, longest));
            }
        }

        candidates.AddRange(Known<TValue>(schema));

        var seen = new HashSet<TSelf>();
        var accepted = new List<TValue>();
        foreach (var candidate in candidates)
        {
            if (TSelf.TryCreate(candidate, out var created) && seen.Add(created))
            {
                accepted.Add(created.Value);
            }
        }

        return accepted;
    }

    /// <summary>
    /// Gets values the schema rules out, each with the rule it breaks: a string one character shorter than its
    /// <c>MinLength</c> and the empty string, one character longer than its <c>MaxLength</c>, a value one step below its
    /// <c>Minimum</c> and one above its <c>Maximum</c>, and a value outside its closed value set.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <returns>The values; none for a schema that rules nothing out.</returns>
    /// <remarks>
    /// <para>
    /// A string of the wrong length is otherwise drawn as the pattern asks, so that its length alone breaks a rule, and
    /// drawn again while the type's normalizer brings it back to a right length, a trimmed space for one. The empty string
    /// is derived only beside a <c>MinLength</c> of 2 or more, since the schema does not say whether a type without one
    /// accepts it. A step is one unit of the bound's last written digit for a <see cref="decimal"/>, one unit
    /// in the last place for a binary floating-point number, one tick for a date, a time or a duration, and one day for a
    /// <see cref="DateOnly"/>; no step is taken past the type's own extremes. A value outside a closed set is drawn from the
    /// rest of the schema, and compared with the known values once normalized, ignoring case for a string.
    /// </para>
    /// <para>
    /// A type whose normalizer clamps a value into its rules, rather than its validator refusing it, accepts some of these:
    /// a number past a bound, or a string its normalizer cuts or pads to its lengths whatever the draw.
    /// </para>
    /// </remarks>
    public IEnumerable<SchemaViolation<TValue>> RejectedValues<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var schema = TSelf.Schema;
        var violations = new List<SchemaViolation<TValue>>();
        if (typeof(TValue) == typeof(string))
        {
            if (schema.MinLength is { } shortest and > 0)
            {
                var rule = $"MinLength ({shortest})";
                violations.Add(new(WrongLength<TSelf, TValue>(schema, shortest, static text => text[..^1], length => length < shortest), rule));
                if (shortest > 1)
                {
                    violations.Add(new((TValue)(object)string.Empty, rule));
                }
            }

            if (schema.MaxLength is { } longest and >= 0)
            {
                // The last character again, rather than one a normalizer might strip.
                violations.Add(new(
                    WrongLength<TSelf, TValue>(schema, longest, static text => text.Length == 0 ? "A" : text + text[^1], length => length > longest),
                    $"MaxLength ({longest})"));
            }
        }
        else if (Underlying<TValue>.Instance is { } underlying)
        {
            if (Bound(underlying, schema.Minimum, out var minimum) && underlying.TryBelow(minimum, out var below))
            {
                violations.Add(new(below, $"Minimum ({schema.Minimum})"));
            }

            if (Bound(underlying, schema.Maximum, out var maximum) && underlying.TryAbove(maximum, out var above))
            {
                violations.Add(new(above, $"Maximum ({schema.Maximum})"));
            }
        }

        if (schema.IsClosedValueSet && TryOutside<TSelf, TValue>(schema, out var outside))
        {
            violations.Add(new(outside, "a closed value set"));
        }

        return violations;
    }

    /// <summary>
    /// Proposes simpler values the type accepts than one it holds, for a property-based testing library to shrink a
    /// counterexample to: the values its schema declares, its <c>Minimum</c>, its first known value and its example, then
    /// the shrinks of the underlying value the type accepts as they are.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="value">The value to shrink.</param>
    /// <param name="shrinkUnderlying">The shrinker of the underlying type, or <see langword="null"/> to propose the declared values alone.</param>
    /// <returns>The proposals, none equal to the value or to one another.</returns>
    /// <remarks>
    /// <para>
    /// A shrink of the underlying value is proposed only when the type accepts it and its normalizer leaves it unchanged: a
    /// normalizer maps a shrink to a value that may be no simpler, as upper-casing maps the <c>'b'</c> a character shrinks to
    /// back to <c>'B'</c>, and two values would then shrink to each other until the library gives up.
    /// </para>
    /// <para>
    /// The declared values are ranked in that order, and from one of them only those ranked before it are proposed, and no
    /// shrink of its underlying value: every chain of shrinks ends wherever the chains of the underlying shrinker end.
    /// </para>
    /// </remarks>
    public static IEnumerable<TSelf> Shrink<TSelf, TValue>(TSelf value, Func<TValue, IEnumerable<TValue>>? shrinkUnderlying = null)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var targets = Targets<TSelf, TValue>();
        var rank = targets.IndexOf(value);
        foreach (var target in rank < 0 ? targets : targets.GetRange(0, rank))
        {
            yield return target;
        }

        if (rank >= 0 || shrinkUnderlying is null)
        {
            yield break;
        }

        var proposed = new HashSet<TSelf>(targets) { value };
        foreach (var smaller in shrinkUnderlying(value.Value))
        {
            if (TSelf.TryCreate(smaller, out var created)
                && EqualityComparer<TValue>.Default.Equals(created.Value, smaller)
                && proposed.Add(created))
            {
                yield return created;
            }
        }
    }

    private static TValue[] Known<TValue>(ValueObjectSchema schema)
        => schema.KnownValues.IsDefaultOrEmpty ? [] : [.. schema.KnownValues.OfType<TValue>()];

    private static bool Bound<TValue>(Underlying<TValue> underlying, string? text, out TValue bound)
    {
        bound = default!;
        return text is not null && underlying.TryParse(text, out bound);
    }

    private static void AddText<TValue>(List<TValue> candidates, string? text)
    {
        if (text is not null)
        {
            candidates.Add((TValue)(object)text);
        }
    }

    private static bool TryExample<TSelf, TValue>(ValueObjectSchema schema, out TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        if (schema.Example is TValue example)
        {
            return TSelf.TryCreate(example, out value);
        }

        // A schema written by hand may hold the text of the example, which the type parses as the schemas read it.
        if (schema.Example is { } text)
        {
            return TSelf.TryParse(Convert.ToString(text, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, out value, out _);
        }

        value = default;
        return false;
    }

    /// <summary>The declared values a shrink aims at, accepted and without repeats: Minimum, the first known value, the example.</summary>
    private static List<TSelf> Targets<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var schema = TSelf.Schema;
        var targets = new List<TSelf>(3);
        if (Underlying<TValue>.Instance is { } underlying && Bound(underlying, schema.Minimum, out var minimum))
        {
            Keep(targets, TSelf.TryCreate(minimum, out var created), created);
        }

        if (Known<TValue>(schema) is [var first, ..])
        {
            Keep(targets, TSelf.TryCreate(first, out var created), created);
        }

        Keep(targets, TryExample<TSelf, TValue>(schema, out var example), example);
        return targets;
    }

    private static void Keep<TSelf>(List<TSelf> targets, bool created, TSelf value)
    {
        if (created && !targets.Contains(value))
        {
            targets.Add(value);
        }
    }

    private static bool IsMember<TValue>(TValue[] known, TValue value)
        => typeof(TValue) == typeof(string)
            ? known.Any(member => string.Equals((string?)(object?)member, (string?)(object?)value, StringComparison.OrdinalIgnoreCase))
            : known.Contains(value);

    /// <summary>The pattern compiled once, or <see langword="null"/> for one .NET does not compile, which checks nothing.</summary>
    private static Regex? Filter(string pattern)
        => Filters.GetOrAdd(pattern, static text =>
        {
            try
            {
                return new Regex(text, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException)
            {
                return null;
            }
        });

    private static (TValue Low, TValue High) Window<TValue>(Underlying<TValue> underlying, ValueObjectSchema schema)
    {
        var hasMinimum = Bound(underlying, schema.Minimum, out var minimum);
        var hasMaximum = Bound(underlying, schema.Maximum, out var maximum);
        return underlying.Window(minimum, hasMinimum, maximum, hasMaximum);
    }

    private bool TryCandidate<TValue>(ValueObjectSchema schema, TValue[] known, Func<Random, TValue>? generator, out TValue candidate)
    {
        if (generator is not null)
        {
            candidate = generator(_random);
            return true;
        }

        if (known.Length > 0 && (schema.IsClosedValueSet || _random.NextDouble() < _options.KnownValueShare))
        {
            candidate = known[_random.Next(known.Length)];
            return true;
        }

        return TryDraw(schema, out candidate);
    }

    /// <summary>Draws a candidate from the rules of the schema, its known values left out.</summary>
    private bool TryDraw<TValue>(ValueObjectSchema schema, out TValue candidate)
    {
        if (Underlying<TValue>.Instance is { } underlying)
        {
            var (low, high) = Window(underlying, schema);
            candidate = underlying.Draw(_random, low, high);
            return true;
        }

        if (typeof(TValue) == typeof(string))
        {
            var text = NextText(schema);
            candidate = (TValue)(object)text!;
            return text is not null;
        }

        if (typeof(TValue) == typeof(bool))
        {
            candidate = (TValue)(object)(_random.Next(2) == 0);
            return true;
        }

        if (typeof(TValue) == typeof(Guid))
        {
            Span<byte> bytes = stackalloc byte[16];
            _random.NextBytes(bytes);
            candidate = (TValue)(object)new Guid(bytes);
            return true;
        }

        // An underlying type outside the table draws only from its known values, its generator or its example.
        candidate = default!;
        return false;
    }

    /// <summary>
    /// Draws a string of the lengths the schema declares, never empty unless its maximum is zero, from the custom pattern
    /// sampler, then the built-in one, then by rejection sampling.
    /// </summary>
    /// <remarks>
    /// Without a maximum, a string gets at most <see cref="ValueObjectSamplerOptions.MaxExtraLength"/> characters beyond
    /// the shortest it can be: its minimum, one character, or the shortest match of its pattern, whichever is longest.
    /// </remarks>
    private string? NextText(ValueObjectSchema schema)
    {
        var shortest = Math.Max(schema.MinLength ?? 0, 1);
        var pattern = schema.Pattern;
        var sampler = pattern is null ? null : PatternSampler.For(pattern);
        var shortestMatch = pattern is null ? 0 : (sampler ?? PatternSampler.Loosely(pattern))?.MinimumLength ?? 0;
        var reach = Math.Max(shortest, shortestMatch);
        var high = Math.Max(schema.MaxLength ?? (int)Math.Min((long)reach + _options.MaxExtraLength, int.MaxValue), 0);
        var low = Math.Min(shortest, high);
        if (pattern is not null && _options.PatternSampler?.Invoke(pattern, _random) is { } custom)
        {
            return custom;
        }

        return sampler?.Draw(_random, low, high, _options.MaxExtraLength) ?? Rejection(pattern, low, high);
    }

    /// <summary>Draws a string of an exact length the pattern matches, from the built-in sampler, then by rejection.</summary>
    private string? EdgeText(ValueObjectSchema schema, int length)
        => (schema.Pattern is { } pattern ? PatternSampler.For(pattern)?.Draw(_random, length, length, _options.MaxExtraLength) : null)
            ?? Rejection(schema.Pattern, length, length);

    /// <summary>
    /// Draws strings between two lengths until the pattern matches one: the fallback for a pattern outside the subset of
    /// the built-in sampler, or a window it could not fit. Each is drawn from the pattern read loosely, without its
    /// lookarounds and with its categories reduced to printable ASCII, or, where even that reading fails, from ASCII
    /// letters and digits.
    /// </summary>
    private string? Rejection(string? pattern, int low, int high)
    {
        var filter = pattern is null ? null : Filter(pattern);
        var loose = pattern is null ? null : PatternSampler.Loosely(pattern);
        for (var attempt = 0; attempt < _options.MaxAttempts; attempt++)
        {
            var text = loose?.Draw(_random, low, high, _options.MaxExtraLength) ?? Alphanumeric(low, high);
            if (filter?.IsMatch(text) != false)
            {
                return text;
            }
        }

        return null;
    }

    private string Alphanumeric(int low, int high)
        => string.Create((int)_random.NextInt64(low, (long)high + 1), _random, static (span, random) =>
        {
            for (var index = 0; index < span.Length; index++)
            {
                span[index] = Alphanumerics[random.Next(Alphanumerics.Length)];
            }
        });

    /// <summary>
    /// Draws a string of a wrong length as <paramref name="derive"/> makes it from a string of an edge length, preferring
    /// one whose normalized form still has a wrong length: a normalizer may take out a character the pattern let the draw
    /// put at an end, a space for one. When none does, the last one drawn, which a type that cuts or pads a value into its
    /// lengths accepts, as it should be told.
    /// </summary>
    private TValue WrongLength<TSelf, TValue>(ValueObjectSchema schema, int length, Func<string, string> derive, Func<int, bool> isWrong)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var derived = string.Empty;
        for (var attempt = 0; attempt < _options.MaxAttempts; attempt++)
        {
            var drawn = EdgeText(schema, length);
            derived = derive(drawn ?? new string('A', length));
            if (drawn is null || isWrong(((string?)(object?)TSelf.Normalize((TValue)(object)derived))?.Length ?? 0))
            {
                break;
            }
        }

        return (TValue)(object)derived;
    }

    private bool TryOutside<TSelf, TValue>(ValueObjectSchema schema, out TValue outside)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        var known = Known<TValue>(schema);
        var open = schema with { IsClosedValueSet = false, KnownValues = [], KnownValueDetails = [] };
        for (var attempt = 0; attempt < _options.MaxAttempts; attempt++)
        {
            if (TryDraw(open, out outside) && !IsMember(known, TSelf.Normalize(outside)))
            {
                return true;
            }
        }

        outside = default!;
        return false;
    }

    private sealed class NextVisitor(ValueObjectSampler sampler) : IValueObjectVisitor<object>
    {
        public object Visit<TSelf, TValue>()
            where TSelf : struct, IValueObject<TSelf, TValue>
            => sampler.Next<TSelf, TValue>();
    }
}
