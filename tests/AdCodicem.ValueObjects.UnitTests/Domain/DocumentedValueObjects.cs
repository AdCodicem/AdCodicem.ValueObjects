namespace AdCodicem.ValueObjects.UnitTests.Domain;

// Declarations only the OpenAPI document reads differently from the rest of the domain: a double or a float bound
// written with an exponent, which only those two types read, and a decimal bound, published as the decimal written.

/// <summary>A mass, in kilograms, from an electron's to a star's: bounds an exponent writes legibly.</summary>
[ValueObject<double>]
public readonly partial struct Mass : IValueObjectMinimum<double>, IValueObjectMaximum<double>
{
    public static double Minimum => 9.1e-31;

    public static double Maximum => 2e32;
}

/// <summary>The most a single transfer may move, in euros.</summary>
[ValueObject<decimal>]
public readonly partial struct TransferLimit : IValueObjectMinimum<decimal>, IValueObjectMaximum<decimal>
{
    public static decimal Minimum => 0m;

    public static decimal Maximum => 1000000m;
}

/// <summary>The luminance of a screen, in nits.</summary>
[ValueObject<float>]
public readonly partial struct Luminance : IValueObjectMinimum<float>, IValueObjectMaximum<float>
{
    public static float Minimum => 0f;

    public static float Maximum => 1.5e3f;
}

// Closed value sets over underlying types other than string, which the document lists as their JSON converter
// writes them: numbers as numbers whatever their width, a date or a time in its round-trip form.

/// <summary>The priority of a task.</summary>
[ValueObject<int>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct Priority
{
    [KnownValue]
    public static readonly Priority Low = Known(1);

    [KnownValue]
    public static readonly Priority High = Known(3);
}

/// <summary>The storage a plan grants, in bytes.</summary>
[ValueObject<long>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct StorageQuota
{
    [KnownValue]
    public static readonly StorageQuota Standard = Known(1_000_000_000L);

    [KnownValue]
    public static readonly StorageQuota Large = Known(10_000_000_000L);
}

/// <summary>A value-added tax rate, in percent.</summary>
[ValueObject<decimal>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct VatRate
{
    [KnownValue]
    public static readonly VatRate Standard = Known(20.0m);

    [KnownValue(Description = "Food, books and medicine.")]
    public static readonly VatRate Reduced = Known(5.5m);
}

/// <summary>The weight of a vote.</summary>
[ValueObject<double>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct VoteWeight
{
    [KnownValue]
    public static readonly VoteWeight Half = Known(0.5);

    [KnownValue]
    public static readonly VoteWeight Full = Known(1.0);
}

/// <summary>The opacity of a layer.</summary>
[ValueObject<float>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct Opacity
{
    [KnownValue]
    public static readonly Opacity Translucent = Known(0.25f);

    [KnownValue]
    public static readonly Opacity Opaque = Known(1f);
}

/// <summary>Whether the terms were accepted, which a contract requires.</summary>
[ValueObject<bool>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct TermsAccepted
{
    [KnownValue]
    public static readonly TermsAccepted Accepted = Known(true);
}

/// <summary>An HTTP status a probe reports.</summary>
[ValueObject<short>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct HttpStatus
{
    [KnownValue]
    public static readonly HttpStatus Ok = Known(200);

    [KnownValue]
    public static readonly HttpStatus NotFound = Known(404);
}

/// <summary>The size of a block on disk, in bytes.</summary>
[ValueObject<uint>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct BlockSize
{
    [KnownValue]
    public static readonly BlockSize Small = Known(4096u);

    [KnownValue]
    public static readonly BlockSize Large = Known(65536u);
}

/// <summary>The date from which archived records are kept.</summary>
[ValueObject<DateOnly>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct CutOffDate
{
    [KnownValue]
    public static readonly CutOffDate Epoch = Known(new DateOnly(2000, 1, 1));

    [KnownValue]
    public static readonly CutOffDate Millennium = Known(new DateOnly(2001, 1, 1));
}

/// <summary>The time a shift starts.</summary>
[ValueObject<TimeOnly>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct ShiftStart
{
    [KnownValue]
    public static readonly ShiftStart Early = Known(new TimeOnly(6, 0));

    [KnownValue]
    public static readonly ShiftStart Late = Known(new TimeOnly(14, 0));
}

/// <summary>The moment a service went live, with the offset it went live at.</summary>
[ValueObject<DateTimeOffset>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct LaunchMoment
{
    [KnownValue]
    public static readonly LaunchMoment Launch = Known(new DateTimeOffset(2000, 1, 1, 9, 0, 0, new TimeSpan(1, 0, 0)));

    [KnownValue]
    public static readonly LaunchMoment Relaunch = Known(new DateTimeOffset(2001, 1, 1, 9, 0, 0, new TimeSpan(1, 0, 0)));
}

/// <summary>An answer to a yes-or-no question.</summary>
[ValueObject<char>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct Answer
{
    [KnownValue]
    public static readonly Answer Yes = Known('Y');

    [KnownValue]
    public static readonly Answer No = Known('N');
}
