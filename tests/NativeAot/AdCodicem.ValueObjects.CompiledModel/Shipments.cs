using System.Collections;

namespace AdCodicem.ValueObjects.CompiledModel;

/// <summary>The shipments the round trip writes, and what it compares them by.</summary>
internal static class Shipments
{
    private static readonly Guid Customer = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    /// <summary>Gets a shipment holding every value object, each optional one included, at the edges of its rules.</summary>
    public static Shipment Full { get; } = new()
    {
        Id = OrderId.New(),
        Previous = OrderId.New(),
        Consent = Consent.Create(true),
        OptionalConsent = Consent.Create(false),
        Grade = Grade.Create('B'),
        OptionalGrade = Grade.Create('f'),
        Adjustment = Adjustment.Create(-10),
        OptionalAdjustment = Adjustment.Create(10),
        Score = Score.Create(100),
        OptionalScore = Score.Create(0),
        Quantity = Quantity.Create(3),
        OptionalQuantity = Quantity.Create(1000),
        Port = Port.Create(8080),
        OptionalPort = Port.Create(ushort.MaxValue),
        Page = PageNumber.First,
        OptionalPage = PageNumber.Create(int.MaxValue),
        Sequence = SequenceNumber.Create(0),
        OptionalSequence = SequenceNumber.Create(uint.MaxValue),
        Size = FileSize.Create(0),
        OptionalSize = FileSize.Create(long.MaxValue),
        Transferred = ByteCount.Create(ulong.MaxValue),
        OptionalTransferred = ByteCount.Create(1),
        Ratio = Ratio.Create(0.25f),
        OptionalRatio = Ratio.Create(1f),
        Latitude = Latitude.Create(-90),
        OptionalLatitude = Latitude.Create(45.5),
        Amount = Amount.Create(12.345m),
        OptionalAmount = Amount.Create(0m),
        Rate = VatRate.Reduced,
        OptionalRate = VatRate.Standard,
        Customer = CustomerId.Create(Customer),
        OptionalCustomer = CustomerId.Create(Guid.Parse("00000000-0000-0000-0000-000000000001")),
        Recorded = RecordedAt.Create(new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Unspecified)),
        OptionalRecorded = RecordedAt.Create(new DateTime(2099, 12, 31, 0, 0, 0, DateTimeKind.Unspecified)),
        Occurred = OccurredAt.Create(new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromHours(2))),
        OptionalOccurred = OccurredAt.Create(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero)),
        Effective = EffectiveDate.LedgerStart,
        OptionalEffective = EffectiveDate.Create(new DateOnly(2024, 2, 29)),
        Opens = OpeningTime.Create(new TimeOnly(6, 0)),
        OptionalOpens = OpeningTime.Create(new TimeOnly(12, 0)),
        Duration = Duration.Create(TimeSpan.FromMinutes(90)),
        OptionalDuration = Duration.Create(new TimeSpan(23, 59, 59)), // SQL Server maps a TimeSpan to time, which stops short of a day.
        Email = EmailAddress.Create(" Ada@Example.com"),
        OptionalEmail = EmailAddress.Create("grace@example.org"),
        Status = DocumentStatus.Draft,
        OptionalStatus = DocumentStatus.Final,
        Label = Label.Create(string.Empty),
        OptionalLabel = Label.Create("fragile"),
        Number = DocumentNumber<PurchaseOrder>.Create("po-1042"),
        OptionalNumber = DocumentNumber<PurchaseOrder>.Create("PO-7"),
        Contacts = [EmailAddress.Create("ada@example.com"), EmailAddress.Create(" Grace@Example.org")],
        OptionalContacts = [null, EmailAddress.Create("ada@example.com")],
        Batches = [Quantity.Create(1), Quantity.Create(1000)],
        OptionalBatches = [Quantity.Create(5), null],
        Related = [DocumentNumber<PurchaseOrder>.Create("po-1"), DocumentNumber<PurchaseOrder>.Create("PO-2")],
    };

    /// <summary>Gets a shipment holding every required value object, none of the optional ones, and empty collections.</summary>
    public static Shipment Sparse { get; } = new()
    {
        Id = OrderId.New(),
        Consent = Full.Consent,
        Grade = Full.Grade,
        Adjustment = Full.Adjustment,
        Score = Full.Score,
        Quantity = Quantity.Create(1),
        Port = Full.Port,
        Page = Full.Page,
        Sequence = Full.Sequence,
        Size = Full.Size,
        Transferred = Full.Transferred,
        Ratio = Full.Ratio,
        Latitude = Full.Latitude,
        Amount = Full.Amount,
        Rate = Full.Rate,
        Customer = Full.Customer,
        Recorded = Full.Recorded,
        Occurred = Full.Occurred,
        Effective = Full.Effective,
        Opens = Full.Opens,
        Duration = Full.Duration,
        Email = Full.Email,
        Status = Full.Status,
        Label = Full.Label,
        Number = Full.Number,
    };

    /// <summary>Tells whether a context runs on the model its build names: the compiled one, or the one built at run time.</summary>
    public static bool IsTheModelBuiltWith(ShopContext context, bool strict)
#if COMPILED_MODEL
        => context.Model.GetType() == (strict ? typeof(Compiled.Strict.StrictShopContextModel) : typeof(Compiled.Relaxed.ShopContextModel));
#else
        => context.Model.GetType() == typeof(Microsoft.EntityFrameworkCore.Metadata.RuntimeModel) && strict == context is StrictShopContext;
#endif

    /// <summary>Lists the properties two shipments hold different values in, a collection compared element by element.</summary>
    public static string Differences(Shipment expected, Shipment actual)
        => string.Join(
            ", ",
            typeof(Shipment).GetProperties()
                .Where(property => !Same(property.GetValue(expected), property.GetValue(actual)))
                .Select(property => $"{property.Name} {Show(property.GetValue(actual))} for {Show(property.GetValue(expected))}"));

    private static bool Same(object? expected, object? actual)
        => expected is IEnumerable elements and not string && actual is IEnumerable others
            ? elements.Cast<object?>().SequenceEqual(others.Cast<object?>())
            : Equals(expected, actual);

    private static string Show(object? value)
        => value is IEnumerable elements and not string
            ? $"[{string.Join(", ", elements.Cast<object?>().Select(static element => element ?? "null"))}]"
            : value?.ToString() ?? "null";
}
