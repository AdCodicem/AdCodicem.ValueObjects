using AdCodicem.ValueObjects.Testing;

namespace AdCodicem.ValueObjects.UnitTests;

/// <summary>
/// Every value object of this assembly is put through the shipped contract kit. This is what a consumer writes
/// for their own types: a handful of accepted and rejected values, and the rest is checked for them.
/// </summary>
public sealed class IbanContract : ValueObjectContract<Iban, string>
{
    protected override IEnumerable<string> AcceptedValues =>
    [
        "FR7630006000011234567890189",
        "DE89370400440532013000",
        "fr76 3000 6000 0112 3456 7890 189",
    ];

    protected override IEnumerable<string> RejectedValues =>
    [
        string.Empty,
        "FR76",
        "not-an-iban",
        "FR7630006000011234567890188",
    ];
}

/// <inheritdoc cref="IbanContract" />
public sealed class EmailAddressContract : ValueObjectContract<EmailAddress, string>
{
    protected override IEnumerable<string> AcceptedValues => ["ada@example.com", "  Grace@Example.ORG  "];

    protected override IEnumerable<string> RejectedValues => [string.Empty, "no-at-sign", "two@@at.example"];
}

/// <inheritdoc cref="IbanContract" />
public sealed class AmountContract : ValueObjectContract<Amount, decimal>
{
    protected override IEnumerable<decimal> AcceptedValues => [0m, 0.01m, 1250.505m, 999_999.99m];

    protected override IEnumerable<decimal> RejectedValues => [-0.01m, -1m];
}

/// <inheritdoc cref="IbanContract" />
public sealed class PercentageContract : ValueObjectContract<Percentage, decimal>
{
    protected override IEnumerable<decimal> AcceptedValues => [0m, 50m, 100m];

    protected override IEnumerable<decimal> RejectedValues => [-1m, 100.01m];
}

/// <inheritdoc cref="IbanContract" />
public sealed class CustomerIdContract : ValueObjectContract<CustomerId, Guid>
{
    protected override IEnumerable<Guid> AcceptedValues =>
    [
        Guid.Parse("0192f4a0-0000-7000-8000-000000000001"),
        Guid.Parse("0192f4a0-0000-7000-8000-000000000002"),
    ];

    protected override IEnumerable<Guid> RejectedValues => [Guid.Empty];
}

/// <inheritdoc cref="IbanContract" />
public sealed class CountryCodeContract : ValueObjectContract<CountryCode, string>
{
    protected override IEnumerable<string> AcceptedValues => ["FR", "BE", "lu"];

    protected override IEnumerable<string> RejectedValues => [string.Empty, "ZZ", "FRA"];
}

/// <inheritdoc cref="IbanContract" />
public sealed class BirthDateContract : ValueObjectContract<BirthDate, DateOnly>
{
    protected override IEnumerable<DateOnly> AcceptedValues =>
    [
        new DateOnly(1900, 1, 1),
        new DateOnly(1980, 5, 17),
        new DateOnly(2100, 12, 31),
    ];

    protected override IEnumerable<DateOnly> RejectedValues => [new DateOnly(1899, 12, 31), new DateOnly(2101, 1, 1)];
}

/// <inheritdoc cref="IbanContract" />
public sealed class QuantityContract : ValueObjectContract<Quantity, short>
{
    protected override IEnumerable<short> AcceptedValues => [0, 1, 1000];

    protected override IEnumerable<short> RejectedValues => [-1, 1001];
}

/// <inheritdoc cref="IbanContract" />
public sealed class OrderReferenceContract : ValueObjectContract<Ordering.OrderReference, string>
{
    protected override IEnumerable<string> AcceptedValues => ["ORD-42", "  ord-99  "];

    protected override IEnumerable<string> RejectedValues => [string.Empty, "no"];
}

/// <summary>
/// Generated identifiers go through the same kit as everything else, which is the point: nothing about them is
/// exempt from the value object contract. The accepted values are minted once into a static so that every pass
/// of the kit sees the same set — <c>New()</c> would hand back a different one on each enumeration.
/// </summary>
public sealed class AccountIdContract : ValueObjectContract<AccountId, string>
{
    private static readonly string[] Minted =
    [
        AccountId.New().Value,
        AccountId.New().Value.ToLowerInvariant(),
        AccountId.New().Value,
    ];

    protected override IEnumerable<string> AcceptedValues => Minted;

    protected override IEnumerable<string> RejectedValues =>
    [
        string.Empty,
        "acc_",
        "acc_2K7X9WQMZ4H3N8VYB6TC",
        "not-an-identifier",
        SubscriptionId.New().Value,
    ];
}

/// <inheritdoc cref="AccountIdContract" />
public sealed class LedgerEntryIdContract : ValueObjectContract<LedgerEntryId, string>
{
    private static readonly string[] Minted = [LedgerEntryId.New().Value, LedgerEntryId.New().Value];

    protected override IEnumerable<string> AcceptedValues => Minted;

    protected override IEnumerable<string> RejectedValues => [string.Empty, "ldg_entry_nope", AccountId.New().Value];
}
