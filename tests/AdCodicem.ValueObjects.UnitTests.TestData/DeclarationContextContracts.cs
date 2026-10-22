using AdCodicem.ValueObjects.Testing;

namespace AdCodicem.ValueObjects.UnitTests;

// The contract kit over the value objects reached through their declaration context (Domain/DeclarationContexts.cs).

/// <inheritdoc cref="IbanContract" />
public sealed class PurchaseOrderReferenceContract : ValueObjectContract<Reference<PurchaseOrder>, string>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<string> AcceptedValues => ["PO-1042", "PO-1"];

    protected override IEnumerable<string> RejectedValues => ["", "PO-1042-TOO-LONG"];
}

/// <inheritdoc cref="IbanContract" />
public sealed class StockContract : ValueObjectContract<Catalog<string>.Stock, int>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<int> AcceptedValues => [0, 12];

    protected override IEnumerable<int> RejectedValues => [-1];
}

/// <inheritdoc cref="IbanContract" />
public sealed class CarrierContract : ValueObjectContract<IShipping.Carrier, string>
{
    protected override bool DerivesRejectedValues => true;

    protected override IEnumerable<string> AcceptedValues => ["UPS", "DHL"];

    protected override IEnumerable<string> RejectedValues => ["", "UP", "FEDX"];
}
