namespace AdCodicem.ValueObjects.EntityFrameworkCore;

/// <summary>
/// Chooses the rules <see cref="ValueObjectConventionExtensions.AddValueObjectCheckConstraints"/> writes as CHECK
/// constraints, and the dialect it writes a pattern in.
/// </summary>
public sealed class ValueObjectCheckOptions
{
    private readonly HashSet<Type> _patternExclusions = [];

    /// <summary>
    /// Gets or sets the rules written as CHECK constraints: lengths, bounds and closed value sets, unless one is removed.
    /// </summary>
    public ValueObjectChecks Rules { get; set; } =
        ValueObjectChecks.Lengths | ValueObjectChecks.Bounds | ValueObjectChecks.KnownValues;

    /// <summary>
    /// Gets or sets the dialect of regular expressions the database runs, which a pattern is written in: none by default,
    /// so that no pattern is written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model cannot tell which dialect the database runs, the compatibility level of a SQL Server database least of
    /// all, so it is stated. A dialect that does not match the provider at hand, PostgreSQL's under SQLite in tests, writes
    /// no pattern; SQLite has no regular expressions.
    /// </para>
    /// <para>
    /// Naming one checks the pattern of each column the convention constrains when the model is built, under SQLite, or
    /// the provider of the other dialect, as under the database it names: a pattern holding a construct the dialect reads
    /// apart from .NET, or whose regular expression sets any option but <c>CultureInvariant</c>, <c>Compiled</c>,
    /// <c>ExplicitCapture</c> and <c>NonBacktracking</c>, which its text does not carry, <c>IgnoreCase</c> say, or
    /// <c>ECMAScript</c> and <c>RightToLeft</c>, which VO0025 does not report, fails the model with an
    /// <see cref="InvalidOperationException"/> naming the value object, the construct or the option, and the dialect,
    /// unless <see cref="ExcludePattern{TSelf}"/> leaves that value object out. A pattern a value object written by hand
    /// states in its schema alone is checked from its text alone. Another provider than SQL Server, PostgreSQL and SQLite
    /// gets no constraint, and no check.
    /// </para>
    /// </remarks>
    public ValueObjectPatternDialect Patterns { get; set; }

    /// <summary>
    /// Gets the value objects whose pattern is written as no constraint, and checked by nothing.
    /// </summary>
    internal IReadOnlySet<Type> PatternExclusions => _patternExclusions;

    /// <summary>
    /// Leaves the pattern of a value object out of the constraints, and out of the check of
    /// <see cref="Patterns"/>, its other rules being written as before.
    /// </summary>
    /// <typeparam name="TSelf">The value object, over <see cref="string"/>; a construction of a generic one is left out alone.</typeparam>
    /// <returns>The same options, so calls can be chained.</returns>
    /// <remarks>
    /// For a value object whose pattern the dialect would read apart from .NET, an email address's <c>[^@\s]</c> say, which
    /// would otherwise fail the model: the database then checks its length, as before, and the application its pattern.
    /// </remarks>
    public ValueObjectCheckOptions ExcludePattern<TSelf>()
        where TSelf : struct, IValueObject<TSelf, string>
    {
        _patternExclusions.Add(typeof(TSelf));

        return this;
    }
}
