using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace AdCodicem.ValueObjects.NativeAot.Protobuf;

/// <summary>The message the script writes, over value objects and over the primitives, and the values it spoils.</summary>
internal static class Samples
{
    /// <summary>The members protobuf-net's <c>TypeModel</c> demands of the type it writes or reads.</summary>
    public const DynamicallyAccessedMemberTypes Contract =
        DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors
        | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods
        | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields
        | DynamicallyAccessedMemberTypes.PublicNestedTypes | DynamicallyAccessedMemberTypes.NonPublicNestedTypes
        | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties;

    private static readonly Guid Ticket = Guid.Parse("6f1c2a6e-1f0e-4c39-9d43-2b6c7f1b2a10");

    /// <summary>Gets, for each value object with a rule a non-zero value can break, a value it refuses.</summary>
    public static IReadOnlyList<(string Subject, Action<PlainEverything> Spoil)> Refused { get; } =
    [
        ("Grade", message => message.Grade = 'Z'),
        ("Adjustment", message => message.Adjustment = 11),
        ("Score", message => message.Score = 101),
        ("PageNumber", message => message.PageNumber = -1),
        ("FileSize", message => message.FileSize = -1),
        ("Latitude", message => message.Latitude = 91),
        ("Ratio", message => message.Ratio = 2),
        ("OpeningTime", message => message.OpeningTime = new TimeOnly(13, 0)),
        ("RecordedAt", message => message.RecordedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        ("Duration", message => message.Duration = TimeSpan.FromSeconds(-1)),
        ("EffectiveDate", message => message.EffectiveDate = new DateOnly(1999, 12, 31)),
        ("PhoneNumber", message => message.PhoneNumber = "123"),
        ("Label", message => message.Label = new string('x', 201)),
        ("DocumentStatus", message => message.DocumentStatus = "archived"),
        ("Price", message => message.Price = -1m),
    ];

    public static Everything Everything() => new()
    {
        Consent = Consent.Create(true),
        Grade = Grade.Create('b'),
        Adjustment = Adjustment.Create(-3),
        Score = Score.Create(42),
        Port = Port.Create(8080),
        PageNumber = PageNumber.Create(7),
        SequenceNumber = SequenceNumber.Create(5),
        FileSize = FileSize.Create(1234),
        ByteCount = ByteCount.Create(99),
        Latitude = Latitude.Create(48.85),
        Ratio = Ratio.Create(0.5f),
        OpeningTime = OpeningTime.Create(new TimeOnly(8, 30)),
        RecordedAt = RecordedAt.Create(new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc)),
        Duration = Duration.Create(new TimeSpan(1, 30, 0)),
        EffectiveDate = EffectiveDate.Create(new DateOnly(2026, 10, 13)),
        Tolerance = Tolerance.Create(0.25),
        PhoneNumber = PhoneNumber.Create("+33123456789"),
        Label = Label.Create("hello"),
        DocumentStatus = DocumentStatus.Final,
        Ticket = TicketId.Create(Ticket),
        Price = Price.Create(12.5m),
    };

    public static PlainEverything Plain() => new()
    {
        Consent = true,
        Grade = 'B',
        Adjustment = -3,
        Score = 42,
        Port = 8080,
        PageNumber = 7,
        SequenceNumber = 5,
        FileSize = 1234,
        ByteCount = 99,
        Latitude = 48.85,
        Ratio = 0.5f,
        OpeningTime = new TimeOnly(8, 30),
        RecordedAt = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc),
        Duration = new TimeSpan(1, 30, 0),
        EffectiveDate = new DateOnly(2026, 10, 13),
        Tolerance = 0.25,
        PhoneNumber = "+33123456789",
        Label = "hello",
        DocumentStatus = "final",
        Ticket = Ticket,
        Price = 12.5m,
    };

    /// <summary>Writes the values a message holds, in the invariant culture.</summary>
    public static string Describe(Everything message) => string.Join(
        ", ",
        new object[]
        {
            message.Consent.Value, message.Grade.Value, message.Adjustment.Value, message.Score.Value, message.Port.Value,
            message.PageNumber.Value, message.SequenceNumber.Value, message.FileSize.Value, message.ByteCount.Value,
            message.Latitude.Value, message.Ratio.Value, message.OpeningTime.Value, message.RecordedAt.Value.ToString("O", CultureInfo.InvariantCulture),
            message.Duration.Value, message.EffectiveDate.Value, message.Tolerance.Value, message.PhoneNumber.Value,
            message.Label.Value, message.DocumentStatus.Value, message.Ticket.Value, message.Price.Value,
        }.Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)));
}
