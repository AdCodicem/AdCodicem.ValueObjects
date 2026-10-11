namespace AdCodicem.ValueObjects.IntegrationTests.Fixtures;

// The keys the key generation tests store: a Guid value object with no rule, which accepts Guid.Empty, and one over each
// integer type HasValueObjectKeyGeneration generates, the int one counted from one, so that a strict read accepts what the
// database generates and nothing else.

/// <summary>The identifier of a parcel, which accepts <see cref="Guid.Empty"/>.</summary>
[ValueObject<Guid>]
public readonly partial struct ParcelId;

/// <summary>The number of a ticket, counted from one.</summary>
[ValueObject<int>]
public readonly partial struct TicketNumber : IValueObjectMinimum<int>
{
    public static int Minimum => 1;
}

/// <summary>The number of a ledger.</summary>
[ValueObject<long>]
public readonly partial struct LedgerNumber;

/// <summary>The number of a shelf.</summary>
[ValueObject<short>]
public readonly partial struct ShelfNumber;
