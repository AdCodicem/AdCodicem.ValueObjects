using AdCodicem.ValueObjects.Testing.Data;
using AutoFixture;

namespace AdCodicem.ValueObjects.AutoFixture;

/// <summary>
/// Customizes an AutoFixture fixture so that every value object it creates is drawn from the rules its type declares:
/// asked for directly, as its nullable form, or as a member of an object the fixture builds.
/// </summary>
/// <remarks>
/// <para>
/// Without it, AutoFixture calls the value object's <c>Create</c> with values of its own, longer than a declared
/// <c>MaxLength</c>, outside declared bounds, always the first known value of a closed set, and the creation fails or
/// passes by chance. With it, each value object is drawn by a <see cref="ValueObjectSampler"/>: a candidate built from its
/// schema, kept only once its rules accept it, or the example it declares.
/// </para>
/// <para>
/// A registration of the fixture wins over the customization, whether it comes before or after it:
/// <c>fixture.Register(() =&gt; EvenCode.Create(...))</c> is how a rule no schema carries, a checksum for one, is met in
/// AutoFixture's terms, as <see cref="ValueObjectSamplerOptions.Use{TSelf, TValue}"/> is in the sampler's. A specimen builder
/// the fixture's customizations held before the customization wins too, its builder being added after them.
/// </para>
/// <para>
/// A value object is found through <c>ValueObjectRegistry.TryResolve</c>, which describes a value object written by hand
/// the first time a fixture asks for it, and keeps that description. The customization is as thread-safe as the
/// <see cref="Random"/> it draws from: <see cref="Random.Shared"/> by default is, a <see cref="Random"/> created with a seed
/// is not, and gives the same values for the same requests only when a single thread makes them.
/// </para>
/// </remarks>
public sealed class ValueObjectCustomization : ICustomization
{
    private readonly ValueObjectSamplerOptions _options;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectCustomization"/> class, drawing from
    /// <see cref="Random.Shared"/> with the default sampler options.
    /// </summary>
    public ValueObjectCustomization()
        : this(new ValueObjectSamplerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectCustomization"/> class, drawing from
    /// <see cref="Random.Shared"/> with the given sampler options.
    /// </summary>
    /// <param name="options">How each value object is drawn, the generators of the rules no schema carries included.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public ValueObjectCustomization(ValueObjectSamplerOptions options)
        : this(options, Random.Shared)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueObjectCustomization"/> class, drawing from the given source of
    /// randomness with the given sampler options.
    /// </summary>
    /// <param name="options">How each value object is drawn, the generators of the rules no schema carries included.</param>
    /// <param name="random">The source of randomness: a <see cref="Random"/> created with a seed replays the same values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> or <paramref name="random"/> is <see langword="null"/>.</exception>
    public ValueObjectCustomization(ValueObjectSamplerOptions options, Random random)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);
        _options = options;
        _random = random;
    }

    /// <summary>
    /// Adds to the fixture's customizations a specimen builder that answers the request for any value object type, or
    /// for its nullable type, with a value its rules accept.
    /// </summary>
    /// <param name="fixture">The fixture to customize.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fixture"/> is <see langword="null"/>.</exception>
    public void Customize(IFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        fixture.Customizations.Add(new ValueObjectSpecimenBuilder(new ValueObjectSampler(_random, _options)));
    }
}
