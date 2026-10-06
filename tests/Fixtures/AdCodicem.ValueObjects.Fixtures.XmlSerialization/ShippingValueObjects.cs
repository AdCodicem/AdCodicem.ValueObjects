using System.Text.RegularExpressions;
using AdCodicem.ValueObjects.Annotations;
using AdCodicem.ValueObjects.Identifiers;

[assembly: ValueObjectXmlSerialization]

namespace AdCodicem.ValueObjects.Fixtures.XmlSerialization;

/// <summary>A tracking code: two letters, then digits, in upper case.</summary>
[ValueObject<string>(MinLength = 4, MaxLength = 12)]
public readonly partial struct TrackingCode : IValueObjectNormalizer<string>, IValueObjectPatternValidator
{
    /// <summary>Gets the shape of a tracking code.</summary>
    [GeneratedRegex("^[A-Z]{2}[0-9]{2,10}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Pattern { get; }

    /// <inheritdoc />
    public static string NormalizeValue(string value) => value.Trim().ToUpperInvariant();
}

/// <summary>A remark, which may be empty.</summary>
[ValueObject<string>(AllowEmpty = true, MaxLength = 40)]
public readonly partial struct Remark;

/// <summary>A consignment, never the empty identifier.</summary>
[ValueObject<Guid>]
public readonly partial struct ConsignmentId : IValueObjectValidator<Guid>
{
    /// <inheritdoc />
    public static ValidationResult ValidateValue(in Guid value)
        => value == Guid.Empty ? ValidationResult.Required("The value must not be the empty identifier.") : ValidationResult.Success;
}

/// <summary>Whether a parcel is insured.</summary>
[ValueObject<bool>]
public readonly partial struct Insured;

/// <summary>A handling class, from A to E.</summary>
[ValueObject<char>]
public readonly partial struct HandlingClass : IValueObjectMinimum<char>, IValueObjectMaximum<char>
{
    /// <inheritdoc />
    public static char Minimum => 'A';

    /// <inheritdoc />
    public static char Maximum => 'E';
}

/// <summary>A tilt, in degrees.</summary>
[ValueObject<sbyte>]
public readonly partial struct TiltDegrees : IValueObjectMinimum<sbyte>, IValueObjectMaximum<sbyte>
{
    /// <inheritdoc />
    public static sbyte Minimum => -10;

    /// <inheritdoc />
    public static sbyte Maximum => 10;
}

/// <summary>A number of pallets.</summary>
[ValueObject<byte>]
public readonly partial struct PalletCount : IValueObjectMinimum<byte>, IValueObjectMaximum<byte>
{
    /// <inheritdoc />
    public static byte Minimum => 1;

    /// <inheritdoc />
    public static byte Maximum => 30;
}

/// <summary>A deck of a ship, below the waterline when negative.</summary>
[ValueObject<short>]
public readonly partial struct DeckLevel : IValueObjectMinimum<short>, IValueObjectMaximum<short>
{
    /// <inheritdoc />
    public static short Minimum => -2;

    /// <inheritdoc />
    public static short Maximum => 20;
}

/// <summary>A dock door.</summary>
[ValueObject<ushort>]
public readonly partial struct DockDoor : IValueObjectMinimum<ushort>
{
    /// <inheritdoc />
    public static ushort Minimum => 1;
}

/// <summary>A number of parcels, from 1 to 100.</summary>
[ValueObject<int>]
public readonly partial struct ParcelCount : IValueObjectMinimum<int>, IValueObjectMaximum<int>
{
    /// <inheritdoc />
    public static int Minimum => 1;

    /// <inheritdoc />
    public static int Maximum => 100;
}

/// <summary>A sequence number in a manifest, which starts at zero.</summary>
[ValueObject<uint>]
public readonly partial struct ManifestSequence;

/// <summary>A gross weight, in grams.</summary>
[ValueObject<long>]
public readonly partial struct GrossGrams : IValueObjectMinimum<long>
{
    /// <inheritdoc />
    public static long Minimum => 0;
}

/// <summary>The serial number of a container.</summary>
[ValueObject<ulong>]
public readonly partial struct ContainerSerial;

/// <summary>A customs balance, in the smallest unit of a currency, whose upper bound no <see cref="decimal"/> holds.</summary>
[ValueObject<Int128>]
public readonly partial struct CustomsBalance : IValueObjectMinimum<Int128>, IValueObjectMaximum<Int128>
{
    /// <inheritdoc />
    public static Int128 Minimum => -1_000_000;

    /// <inheritdoc />
    public static Int128 Maximum => Int128.MaxValue;
}

/// <summary>The fingerprint of a tariff.</summary>
[ValueObject<UInt128>]
public readonly partial struct TariffFingerprint;

/// <summary>A demurrage balance, whose lower bound no <see cref="decimal"/> holds.</summary>
[ValueObject<Int128>]
public readonly partial struct DemurrageBalance : IValueObjectMinimum<Int128>, IValueObjectMaximum<Int128>
{
    /// <inheritdoc />
    public static Int128 Minimum => Int128.MinValue;

    /// <inheritdoc />
    public static Int128 Maximum => 100;
}

/// <summary>A band of risk, one of whose values no <see cref="decimal"/> holds.</summary>
[ValueObject<Int128>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct RiskBand : IValueObjectMinimum<Int128>
{
    /// <summary>The lowest band.</summary>
    [KnownValue]
    public static readonly RiskBand Lowest = Known(1);

    /// <summary>The highest band.</summary>
    [KnownValue]
    public static readonly RiskBand Highest = Known(Int128.MaxValue);

    /// <inheritdoc />
    public static Int128 Minimum => 0;
}

/// <summary>A declared value, never negative.</summary>
[ValueObject<decimal>]
public readonly partial struct DeclaredValue : IValueObjectMinimum<decimal>
{
    /// <inheritdoc />
    public static decimal Minimum => 0m;
}

/// <summary>A heading, in degrees.</summary>
[ValueObject<double>]
public readonly partial struct Heading : IValueObjectMinimum<double>, IValueObjectMaximum<double>
{
    /// <inheritdoc />
    public static double Minimum => -180;

    /// <inheritdoc />
    public static double Maximum => 180;
}

/// <summary>A sensor reading, which may be infinite or not a number.</summary>
[ValueObject<double>]
public readonly partial struct Reading;

/// <summary>The gain of a sensor, which may be infinite or not a number.</summary>
[ValueObject<float>]
public readonly partial struct Gain;

/// <summary>How full a container is.</summary>
[ValueObject<float>]
public readonly partial struct FillRatio : IValueObjectMinimum<float>, IValueObjectMaximum<float>
{
    /// <inheritdoc />
    public static float Minimum => 0;

    /// <inheritdoc />
    public static float Maximum => 1;
}

/// <summary>A shipping date.</summary>
[ValueObject<DateOnly>]
public readonly partial struct ShippingDate : IValueObjectMinimum<DateOnly>
{
    /// <inheritdoc />
    public static DateOnly Minimum => new(2000, 1, 1);
}

/// <summary>A pickup time, during the day.</summary>
[ValueObject<TimeOnly>]
public readonly partial struct PickupTime : IValueObjectMinimum<TimeOnly>, IValueObjectMaximum<TimeOnly>
{
    /// <inheritdoc />
    public static TimeOnly Minimum => new(6, 0);

    /// <inheritdoc />
    public static TimeOnly Maximum => new(22, 0);
}

/// <summary>When a parcel was scanned.</summary>
[ValueObject<DateTime>]
public readonly partial struct ScannedAt : IValueObjectMinimum<DateTime>
{
    /// <inheritdoc />
    public static DateTime Minimum => new(2000, 1, 1);
}

/// <summary>When a parcel was delivered.</summary>
[ValueObject<DateTimeOffset>]
public readonly partial struct DeliveredAt : IValueObjectMinimum<DateTimeOffset>
{
    /// <inheritdoc />
    public static DateTimeOffset Minimum => new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

/// <summary>A transit time, up to a day.</summary>
[ValueObject<TimeSpan>]
public readonly partial struct TransitTime : IValueObjectMinimum<TimeSpan>, IValueObjectMaximum<TimeSpan>
{
    /// <inheritdoc />
    public static TimeSpan Minimum => TimeSpan.Zero;

    /// <inheritdoc />
    public static TimeSpan Maximum => TimeSpan.FromDays(1);
}

/// <summary>The state of a parcel.</summary>
[ValueObject<string>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct ParcelState
{
    /// <summary>The parcel is registered.</summary>
    [KnownValue]
    public static readonly ParcelState Created = Known("created");

    /// <summary>The parcel left the depot.</summary>
    [KnownValue]
    public static readonly ParcelState Shipped = Known("shipped");

    /// <summary>The parcel reached its recipient.</summary>
    [KnownValue]
    public static readonly ParcelState Delivered = Known("delivered");
}

/// <summary>A level of service.</summary>
[ValueObject<int>(ValueSet = ValueSetKind.Closed)]
public readonly partial struct ServiceLevel
{
    /// <summary>The cheapest.</summary>
    [KnownValue]
    public static readonly ServiceLevel Economy = Known(1);

    /// <summary>The usual.</summary>
    [KnownValue]
    public static readonly ServiceLevel Standard = Known(2);

    /// <summary>The fastest.</summary>
    [KnownValue]
    public static readonly ServiceLevel Express = Known(3);
}

/// <summary>What a tag is put on.</summary>
public sealed class Shipment;

/// <summary>A tag put on something, by its owner's type.</summary>
/// <typeparam name="TOwner">What the tag is put on.</typeparam>
[ValueObject<string>(MaxLength = 8)]
public readonly partial struct ShipmentTag<TOwner>;

/// <summary>A pallet carrying a load.</summary>
/// <typeparam name="TLoad">What it carries.</typeparam>
public sealed class Pallet<TLoad>
{
    /// <summary>A slot of a pallet, nested in a generic type.</summary>
    public sealed class Slot;
}

/// <summary>A depot.</summary>
public static partial class Depot
{
    /// <summary>A loading bay of a depot.</summary>
    [ValueObject<string>(MaxLength = 3)]
    public readonly partial struct Bay;
}

/// <summary>A shipment's identifier.</summary>
[EntityId("shp")]
public readonly partial struct ShipmentId;

/// <summary>
/// A note whose author writes it in XML themselves: the generator leaves its implementation alone.
/// </summary>
[ValueObject<string>]
public readonly partial struct CarrierNote : System.Xml.Serialization.IXmlSerializable
{
    /// <inheritdoc />
    public System.Xml.Schema.XmlSchema? GetSchema() => null;

    /// <inheritdoc />
    public void ReadXml(System.Xml.XmlReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        System.Runtime.CompilerServices.Unsafe.AsRef(in this) = Create(reader.ReadElementContentAsString()["note:".Length..]);
    }

    /// <inheritdoc />
    public void WriteXml(System.Xml.XmlWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteString("note:" + Value);
    }
}
