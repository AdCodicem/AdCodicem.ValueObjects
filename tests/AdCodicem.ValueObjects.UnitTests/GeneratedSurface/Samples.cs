using System.Globalization;

namespace AdCodicem.ValueObjects.UnitTests.GeneratedSurface;

/// <summary>One <see cref="Sample"/> for every value object of the domain.</summary>
public static class Samples
{
    public static IReadOnlyDictionary<string, Sample> All { get; } = Build();

    public static TheoryData<string> Names => [.. All.Keys];

    public static TheoryData<string> NumericNames => [.. All.Where(sample => sample.Value.IsNumeric).Select(sample => sample.Key)];

    private static Dictionary<string, Sample> Build()
    {
        Sample[] samples =
        [
            Sample.Of<Iban, string>(
                Iban.Create("DE89370400440532013000"), Iban.Create("FR7630006000011234567890189"), "not-an-iban"),
            Sample.Of<EmailAddress, string>(
                EmailAddress.Create("ada@example.com"), EmailAddress.Create("grace@example.com"), "no-at-sign"),
            Sample.Numeric<Amount, decimal>(Amount.Create(1.5m), Amount.Create(2m), "-1"),
            Sample.Numeric<Percentage, decimal>(Percentage.Create(10m), Percentage.Create(20m), "101"),
            Sample.Of<CustomerId, Guid>(
                CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000001")),
                CustomerId.Create(Guid.Parse("0192f4a0-0000-7000-8000-000000000002")),
                Guid.Empty.ToString()),
            Sample.Of<CountryCode, string>(CountryCode.Belgium, CountryCode.France, "ZZ"),
            Sample.Of<BirthDate, DateOnly>(
                BirthDate.Create(new DateOnly(1980, 5, 17)), BirthDate.Create(new DateOnly(1990, 1, 1)), "1850-01-01"),
            Sample.Numeric<Quantity, short>(Quantity.Create(1), Quantity.Create(2), "1001"),
            Sample.Of<Ordering.OrderReference, string>(
                Ordering.OrderReference.Create("ord-1"), Ordering.OrderReference.Create("ord-2"), "no"),
            Sample.Of<AccountId, string>(AccountId.New(), AccountId.New(), "acc_nope"),
            Sample.Of<SubscriptionId, string>(SubscriptionId.New(), SubscriptionId.New(), "sub_nope"),
            Sample.Of<EventId, string>(EventId.New(), EventId.New(), "evt_nope"),
            Sample.Of<LedgerEntryId, string>(LedgerEntryId.New(), LedgerEntryId.New(), "ldg_entry_nope"),
            Sample.Of<Consent, bool>(Consent.Create(false), Consent.Create(true), "maybe"),
            Sample.Of<Grade, char>(Grade.Create('A'), Grade.Create('B'), "Z"),
            Sample.Numeric<Adjustment, sbyte>(Adjustment.Create(-1), Adjustment.Create(1), "11"),
            Sample.Numeric<Score, byte>(Score.Create(1), Score.Create(2), "101"),
            Sample.Numeric<Port, ushort>(Port.Create(80), Port.Create(443), "0"),
            Sample.Numeric<PageNumber, int>(PageNumber.Create(1), PageNumber.Create(2), "0"),
            Sample.Numeric<SequenceNumber, uint>(SequenceNumber.Create(1), SequenceNumber.Create(2), "-1"),
            Sample.Numeric<FileSize, long>(FileSize.Create(1), FileSize.Create(2), "-1"),
            Sample.Numeric<ByteCount, ulong>(ByteCount.Create(1), ByteCount.Create(2), "-1"),
            Sample.Numeric<LedgerBalance, Int128>(LedgerBalance.Create(-1), LedgerBalance.Create(1), "1000000000000000000001"),
            Sample.Numeric<Fingerprint, UInt128>(Fingerprint.Create(1), Fingerprint.Create(2), "-1"),
            Sample.Numeric<Latitude, double>(Latitude.Create(-1), Latitude.Create(1), "91"),
            Sample.Numeric<Ratio, float>(Ratio.Create(0.25f), Ratio.Create(0.5f), "2"),
            Sample.Of<OpeningTime, TimeOnly>(
                OpeningTime.Create(new TimeOnly(7, 0)), OpeningTime.Create(new TimeOnly(8, 0)), "13:00"),
            Sample.Of<RecordedAt, DateTime>(
                RecordedAt.Create(new DateTime(2001, 1, 1)), RecordedAt.Create(new DateTime(2002, 1, 1)), "1999-01-01"),
            Sample.Of<OccurredAt, DateTimeOffset>(
                OccurredAt.Create(new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                OccurredAt.Create(new DateTimeOffset(2002, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                "1999-01-01T00:00:00+00:00"),
            Sample.Of<Duration, TimeSpan>(
                Duration.Create(TimeSpan.FromHours(1)), Duration.Create(TimeSpan.FromHours(2)), "-00:00:01"),
            Sample.Of<PhoneNumber, string>(PhoneNumber.Create("+33123456789"), PhoneNumber.Create("+4930123456"), "123"),
            Sample.Of<Label, string>(Label.Create("a"), Label.Create("b"), new string('x', 201)),
        ];

        return samples.ToDictionary(sample => sample.ToString()!, StringComparer.Ordinal);
    }
}
