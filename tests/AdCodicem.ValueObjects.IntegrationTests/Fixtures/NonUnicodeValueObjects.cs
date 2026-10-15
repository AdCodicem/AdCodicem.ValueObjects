using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Annotations;

namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

// Value objects declaring their text non-Unicode beside the BIC of the unit suite's UnderlyingTypes.cs, each held to
// ASCII as VO0047 asks: a closed set, a key, and a value object declaring no length. NonUnicodeTextTests stores them in
// varchar on SQL Server, and SqlServerCheckConstraintScriptTests writes their constraints.

/// <summary>A shelf level, from a closed set spelled in ASCII and compared ordinally, stored as non-Unicode text.</summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed, Unicode = false)]
public readonly partial struct ShelfLevel
{
    [KnownValue]
    public static readonly ShelfLevel Top = Known("TOP");

    [KnownValue]
    public static readonly ShelfLevel Bottom = Known("BOTTOM");
}

/// <summary>A stock-keeping unit, the key of a product, stored as non-Unicode text.</summary>
[ValueObject<string>(MaxLength = 12, Unicode = false)]
public readonly partial struct Sku : IValueObjectPatternValidator
{
    [GeneratedRegex("^[A-Z0-9-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}

/// <summary>A routing tag, held to ASCII and declaring no length: <c>varchar(max)</c> on SQL Server.</summary>
[ValueObject<string>(Unicode = false)]
public readonly partial struct RoutingTag : IValueObjectPatternValidator
{
    [GeneratedRegex("^[a-z0-9-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }
}
