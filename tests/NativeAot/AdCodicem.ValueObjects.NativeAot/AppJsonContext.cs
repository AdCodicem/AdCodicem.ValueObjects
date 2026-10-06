using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.Json;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// The source-generated serializer context, the only one the application serializes through: reflection-based
/// serialization is off in both runs. It names the factory of the JSON package, which hands each value object the
/// converter the generator registered for it, or its general-purpose one to a value object written by hand, and lists
/// every value object the application registers, which the script finds here by type and reports missing otherwise,
/// every underlying type, which the script writes a raw value with, and the one value object written by hand that nothing
/// registers, which the factory refuses.
/// </summary>
[JsonSourceGenerationOptions(Converters = [typeof(ValueObjectJsonConverterFactory)])]
[JsonSerializable(typeof(Consent))]
[JsonSerializable(typeof(Grade))]
[JsonSerializable(typeof(Adjustment))]
[JsonSerializable(typeof(Score))]
[JsonSerializable(typeof(Port))]
[JsonSerializable(typeof(PageNumber))]
[JsonSerializable(typeof(SequenceNumber))]
[JsonSerializable(typeof(FileSize))]
[JsonSerializable(typeof(ByteCount))]
[JsonSerializable(typeof(LedgerBalance))]
[JsonSerializable(typeof(Fingerprint))]
[JsonSerializable(typeof(Latitude))]
[JsonSerializable(typeof(Ratio))]
[JsonSerializable(typeof(OpeningTime))]
[JsonSerializable(typeof(RecordedAt))]
[JsonSerializable(typeof(OccurredAt))]
[JsonSerializable(typeof(Duration))]
[JsonSerializable(typeof(EffectiveDate))]
[JsonSerializable(typeof(Tolerance))]
[JsonSerializable(typeof(PhoneNumber))]
[JsonSerializable(typeof(Label))]
[JsonSerializable(typeof(DocumentStatus))]
[JsonSerializable(typeof(Quantity))]
[JsonSerializable(typeof(Amount))]
[JsonSerializable(typeof(VatRate))]
[JsonSerializable(typeof(CustomerId))]
[JsonSerializable(typeof(EmailAddress))]
[JsonSerializable(typeof(OrderId))]
[JsonSerializable(typeof(DocumentNumber<PurchaseOrder>))]
[JsonSerializable(typeof(HandWrittenCode))]
[JsonSerializable(typeof(HandWrittenLink))]
[JsonSerializable(typeof(UnregisteredCode))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(char))]
[JsonSerializable(typeof(sbyte))]
[JsonSerializable(typeof(byte))]
[JsonSerializable(typeof(short))]
[JsonSerializable(typeof(ushort))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(uint))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(ulong))]
[JsonSerializable(typeof(Int128))]
[JsonSerializable(typeof(UInt128))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(DateOnly))]
[JsonSerializable(typeof(TimeOnly))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Uri))]
[JsonSerializable(typeof(AnyEntityId))]
[JsonSerializable(typeof(Order))]
[JsonSerializable(typeof(Page))]
[JsonSerializable(typeof(Tally))]
[JsonSerializable(typeof(Bookmark))]
internal sealed partial class AppJsonContext : JsonSerializerContext;

/// <summary>An order, as a request body carries it and as the endpoint answers it.</summary>
internal sealed record Order(
    EmailAddress Email,
    Quantity Quantity,
    Amount Amount,
    VatRate Rate,
    DocumentStatus Status,
    CustomerId Customer,
    DocumentNumber<PurchaseOrder> Number,
    OrderId? Id,
    OpeningTime? Opens);

/// <summary>The page a request asked for, if any.</summary>
internal sealed record Page(PageNumber? Number);

/// <summary>
/// Counts keyed by value objects over a number and a boolean, whose keys the JSON Schema describes as the text the
/// converter writes them in, and an instant, held to the pattern of the form it is written in.
/// </summary>
internal sealed record Tally(Dictionary<Quantity, int> PerQuantity, Dictionary<Consent, int> PerConsent, RecordedAt Recorded);

/// <summary>
/// Value objects written by hand, which the factory's general-purpose converter serves: as a value, as a nullable one,
/// and as a key.
/// </summary>
internal sealed record Bookmark(HandWrittenLink Link, HandWrittenLink? Mirror, Dictionary<HandWrittenCode, int> PerCode);
