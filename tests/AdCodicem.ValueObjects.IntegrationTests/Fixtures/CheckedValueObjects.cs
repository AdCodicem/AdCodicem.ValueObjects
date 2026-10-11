using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

// Value objects whose rules a column compares apart from .NET unless the CHECK constraint is written with care: bounds
// finer than a column's scale or past what a column holds, floats no decimal fraction states, instants at other offsets,
// closed sets of each kind, and patterns. CheckConstraintTests writes every value each accepts, and refuses another
// writer's row; CheckConstraintPatternTests does so for the patterns.

/// <summary>A tracking code, counted as .NET counts it: no normalizer trims it.</summary>
[ValueObject<string>(MinLength = 5, MaxLength = 10)]
public readonly partial struct TrackingCode;

/// <summary>A fee, whose bounds are finer than a cent.</summary>
[ValueObject<decimal>]
public readonly partial struct Fee : IValueObjectMinimum<decimal>, IValueObjectMaximum<decimal>
{
    public static decimal Minimum => 0.001m;

    public static decimal Maximum => 9.995m;
}

/// <summary>An unsigned 64-bit counter, whose maximum no signed 64-bit integer holds.</summary>
[ValueObject<ulong>]
public readonly partial struct Counter : IValueObjectMinimum<ulong>, IValueObjectMaximum<ulong>
{
    public static ulong Minimum => 1;

    public static ulong Maximum => ulong.MaxValue - 1;
}

/// <summary>A fraction, whose bounds are no float exactly.</summary>
[ValueObject<float>]
public readonly partial struct Fraction : IValueObjectMinimum<float>, IValueObjectMaximum<float>
{
    public static float Minimum => 0.1f;

    public static float Maximum => 0.7f;
}

/// <summary>An initial, from a space on.</summary>
[ValueObject<char>]
public readonly partial struct Initial : IValueObjectMinimum<char>, IValueObjectMaximum<char>
{
    public static char Minimum => ' ';

    public static char Maximum => 'z';
}

/// <summary>An instant whose minimum is a tick past a second.</summary>
[ValueObject<DateTime>]
public readonly partial struct Stamp : IValueObjectMinimum<DateTime>
{
    public static DateTime Minimum => new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddTicks(1);
}

/// <summary>A deadline, its bounds at offsets of their own.</summary>
[ValueObject<DateTimeOffset>]
public readonly partial struct Deadline : IValueObjectMinimum<DateTimeOffset>, IValueObjectMaximum<DateTimeOffset>
{
    public static DateTimeOffset Minimum => new(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));

    public static DateTimeOffset Maximum => new(2030, 1, 1, 0, 0, 0, TimeSpan.FromHours(-5));
}

/// <summary>A window, negative and past a day, which SQL Server's <c>time</c> cannot hold.</summary>
[ValueObject<TimeSpan>]
public readonly partial struct Window : IValueObjectMinimum<TimeSpan>, IValueObjectMaximum<TimeSpan>
{
    public static TimeSpan Minimum => TimeSpan.FromHours(-2);

    public static TimeSpan Maximum => TimeSpan.FromDays(2);
}

/// <summary>A closed set of units, looked up ignoring case and stored in upper case.</summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, Comparison = StringComparison.OrdinalIgnoreCase)]
public readonly partial struct MeasureUnit : IValueObjectNormalizer<string>
{
    [KnownValue]
    public static readonly MeasureUnit Kilogram = Known("KG");

    [KnownValue]
    public static readonly MeasureUnit Litre = Known("L");

    public static string NormalizeValue(string value) => value.ToUpperInvariant();
}

/// <summary>A closed set of rates, written at scales of their own.</summary>
[ValueObject<decimal>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct Rate
{
    [KnownValue]
    public static readonly Rate Nil = Known(0m);

    [KnownValue]
    public static readonly Rate Half = Known(0.5m);

    [KnownValue]
    public static readonly Rate Whole = Known(1.0m);
}

/// <summary>A closed set of floats, no decimal fraction exactly.</summary>
[ValueObject<float>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct Factor
{
    [KnownValue]
    public static readonly Factor Tenth = Known(0.1f);

    [KnownValue]
    public static readonly Factor Half = Known(0.5f);
}

/// <summary>A closed set of instants, which another offset states as well.</summary>
[ValueObject<DateTimeOffset>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct Milestone
{
    [KnownValue]
    public static readonly Milestone Start = Known(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
}

/// <summary>A slug, lower-case words joined by hyphens, whose pattern a CHECK constraint states.</summary>
[ValueObject<string>(MaxLength = 40)]
public readonly partial struct Slug : IValueObjectPatternValidator
{
    [System.Text.RegularExpressions.GeneratedRegex(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    public static partial System.Text.RegularExpressions.Regex Pattern { get; }
}

/// <summary>A ticket code, three letters, a hyphen and four digits, whose pattern a CHECK constraint states.</summary>
[ValueObject<string>(MaxLength = 8)]
public readonly partial struct TicketCode : IValueObjectPatternValidator
{
    [System.Text.RegularExpressions.GeneratedRegex(
        "^[A-Z]{3}-[0-9]{4}$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 1000)]
    public static partial System.Text.RegularExpressions.Regex Pattern { get; }
}
