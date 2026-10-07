namespace AdCodicem.ValueObjects.Testing.Data;

/// <summary>
/// How a <see cref="ValueObjectSampler"/> draws: how many candidates it tries, how often it takes a known value of an open
/// set, how long a string without a maximum length gets, and the generators of the rules no schema carries.
/// </summary>
/// <remarks>
/// Options are read, never written, while a sampler draws: set them up before the first draw, and share them between
/// samplers as you like.
/// </remarks>
public sealed class ValueObjectSamplerOptions
{
    private readonly Dictionary<Type, Delegate> _generators = [];
    private int _maxAttempts = 100;
    private double _knownValueShare = 0.5;
    private int _maxExtraLength = 32;

    /// <summary>
    /// Gets or sets how many candidates a draw tries before it falls back on the example the type declares; 100 by
    /// default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxAttempts
    {
        get => _maxAttempts;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxAttempts = value;
        }
    }

    /// <summary>
    /// Gets or sets the share of draws taken from the known values of an open value set, from 0 to 1, the others drawn
    /// from the rest of its schema; one half by default.
    /// </summary>
    /// <remarks>A closed value set always draws one of its known values.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not between 0 and 1.</exception>
    public double KnownValueShare
    {
        get => _knownValueShare;
        set
        {
            if (value is not (>= 0d and <= 1d))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The share of known values is between 0 and 1.");
            }

            _knownValueShare = value;
        }
    }

    /// <summary>
    /// Gets or sets the most characters a string without a maximum length gets beyond the shortest it can be (its minimum
    /// length, the shortest match of its pattern, or one character when it declares neither), and the most repetitions an
    /// open quantifier of a pattern (<c>*</c>, <c>+</c>, <c>{n,}</c>) adds to its minimum unless the lengths the type
    /// declares ask for more; 32 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int MaxExtraLength
    {
        get => _maxExtraLength;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _maxExtraLength = value;
        }
    }

    /// <summary>
    /// Gets or sets a sampler tried before the built-in one on the pattern of a string value object: it receives the
    /// pattern and the source of randomness, and returns a string the pattern matches, or <see langword="null"/> for a
    /// pattern it cannot sample, which leaves it to the built-in sampler.
    /// </summary>
    /// <remarks>
    /// Every string it returns still goes through the rules of the type. It serves <see cref="ValueObjectSampler.Next{TSelf, TValue}"/>;
    /// <see cref="ValueObjectSampler.Boundaries{TSelf, TValue}"/> and <see cref="ValueObjectSampler.RejectedValues{TSelf, TValue}"/>,
    /// which need a string of an exact length, use the built-in sampler.
    /// </remarks>
    public Func<string, Random, string?>? PatternSampler { get; set; }

    /// <summary>
    /// Registers the generator of the underlying value of a value object, for a rule its schema cannot carry, such as a
    /// checksum: the sampler draws its candidates from it rather than from the schema, and keeps only those the type's
    /// rules accept.
    /// </summary>
    /// <typeparam name="TSelf">The value object.</typeparam>
    /// <typeparam name="TValue">Its underlying type.</typeparam>
    /// <param name="generator">Draws a candidate underlying value from the sampler's source of randomness.</param>
    /// <returns>These options, so that registrations chain.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="generator"/> is <see langword="null"/>.</exception>
    /// <remarks>A later registration for the same value object replaces the earlier one.</remarks>
    public ValueObjectSamplerOptions Use<TSelf, TValue>(Func<Random, TValue> generator)
        where TSelf : struct, IValueObject<TSelf, TValue>
    {
        ArgumentNullException.ThrowIfNull(generator);
        _generators[typeof(TSelf)] = generator;
        return this;
    }

    /// <summary>Gets the generator registered for a value object, or <see langword="null"/>.</summary>
    internal Func<Random, TValue>? GeneratorFor<TSelf, TValue>()
        where TSelf : struct, IValueObject<TSelf, TValue>
        => _generators.TryGetValue(typeof(TSelf), out var generator) ? (Func<Random, TValue>)generator : null;
}
