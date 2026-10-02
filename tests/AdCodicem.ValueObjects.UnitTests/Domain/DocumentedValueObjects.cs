namespace AdCodicem.ValueObjects.UnitTests.Domain;

#pragma warning disable VO0028 // The deprecated Minimum and Maximum options are what these types hold the generated code and the document to.

// Declarations only the OpenAPI document reads differently from the rest of the domain: a double or a float bound
// written with an exponent, which only those two types read, and a decimal bound, published as the decimal written.

/// <summary>A mass, in kilograms, from an electron's to a star's: bounds an exponent writes legibly.</summary>
[ValueObject<double>(Minimum = "9.1e-31", Maximum = "2e32")]
public readonly partial struct Mass;

/// <summary>The most a single transfer may move, in euros.</summary>
[ValueObject<decimal>(Minimum = "0", Maximum = "1000000")]
public readonly partial struct TransferLimit;

/// <summary>The luminance of a screen, in nits.</summary>
[ValueObject<float>(Minimum = "0", Maximum = "1.5e3")]
public readonly partial struct Luminance;

// Closed value sets over underlying types other than string, which the document lists as their JSON converter
// writes them: numbers as numbers whatever their width, a date or a time in its round-trip form.

/// <summary>The priority of a task.</summary>
[ValueObject<int>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Low", 1)]
[KnownValue("High", 3)]
public readonly partial struct Priority;

/// <summary>The storage a plan grants, in bytes.</summary>
[ValueObject<long>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Standard", 1_000_000_000L)]
[KnownValue("Large", 10_000_000_000L)]
public readonly partial struct StorageQuota;

/// <summary>A value-added tax rate, in percent.</summary>
[ValueObject<decimal>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Standard", "20.0")]
[KnownValue("Reduced", "5.5")]
public readonly partial struct VatRate;

/// <summary>The weight of a vote.</summary>
[ValueObject<double>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Half", 0.5)]
[KnownValue("Full", 1.0)]
public readonly partial struct VoteWeight;

/// <summary>The opacity of a layer.</summary>
[ValueObject<float>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Translucent", 0.25f)]
[KnownValue("Opaque", 1f)]
public readonly partial struct Opacity;

/// <summary>Whether the terms were accepted, which a contract requires.</summary>
[ValueObject<bool>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Accepted", true)]
public readonly partial struct TermsAccepted;

/// <summary>An HTTP status a probe reports.</summary>
[ValueObject<short>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Ok", 200)]
[KnownValue("NotFound", 404)]
public readonly partial struct HttpStatus;

/// <summary>The size of a block on disk, in bytes.</summary>
[ValueObject<uint>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Small", 4096u)]
[KnownValue("Large", 65536u)]
public readonly partial struct BlockSize;

/// <summary>The date from which archived records are kept.</summary>
[ValueObject<DateOnly>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Epoch", "2000-01-01")]
[KnownValue("Millennium", "2001-01-01")]
public readonly partial struct CutOffDate;

/// <summary>The time a shift starts.</summary>
[ValueObject<TimeOnly>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Early", "06:00")]
[KnownValue("Late", "14:00")]
public readonly partial struct ShiftStart;

/// <summary>The moment a service went live, with the offset it went live at.</summary>
[ValueObject<DateTimeOffset>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Launch", "2000-01-01T09:00:00+01:00")]
[KnownValue("Relaunch", "2001-01-01T09:00:00+01:00")]
public readonly partial struct LaunchMoment;

/// <summary>An answer to a yes-or-no question.</summary>
[ValueObject<char>(ValueSet = ValueSetKind.Closed)]
[KnownValue("Yes", "Y")]
[KnownValue("No", "N")]
public readonly partial struct Answer;
