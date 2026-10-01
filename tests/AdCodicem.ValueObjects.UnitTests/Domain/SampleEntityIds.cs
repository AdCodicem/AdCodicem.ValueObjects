using AdCodicem.ValueObjects.Identifiers;

namespace AdCodicem.ValueObjects.UnitTests.Domain;

/// <summary>
/// The public identifier of an account.
/// </summary>
[EntityId("acc")]
public readonly partial struct AccountId;

/// <summary>
/// The public identifier of a subscription.
/// </summary>
[EntityId("sub")]
public readonly partial struct SubscriptionId;

/// <summary>
/// The public identifier of a domain event, minted often enough to want a narrower bucket.
/// </summary>
[EntityId("evt", Granularity = IdGranularity.Minute)]
public readonly partial struct EventId;

/// <summary>
/// A ledger entry identifier, written to rarely enough that a daily bucket keeps the index tight.
/// </summary>
[EntityId("ldg_entry", Granularity = IdGranularity.Day, Description = "Identifies one line of the ledger.")]
public readonly partial struct LedgerEntryId;

/// <summary>
/// The identifier of a grant, which can be revoked after it was issued: its validation hook reads state that changes.
/// </summary>
[EntityId("rvk")]
public readonly partial struct RevocableId : IValueObjectValidator<string>
{
    /// <summary>
    /// Gets the identifier revoked in the current execution flow, if any. Scoped to the flow so that the test
    /// revoking one does not reach a test running beside it.
    /// </summary>
    internal static AsyncLocal<string?> Revoked { get; } = new();

    public static ValidationResult ValidateValue(in string value)
        => string.Equals(value, Revoked.Value, StringComparison.Ordinal)
            ? ValidationResult.Failure("grant.revoked", "The grant was revoked.")
            : ValidationResult.Success;
}
