using AdCodicem.ValueObjects.FluentValidation;
using AdCodicem.ValueObjects.Testing;
using FluentValidation;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>A request carrying raw text, validated by the rules of the value objects it will become.</summary>
/// <param name="Iban">The IBAN, as typed.</param>
/// <param name="Quantity">The quantity, as typed.</param>
public sealed record ImportRequest(string? Iban, int Quantity);

/// <summary>Defers to the value objects instead of restating their rules.</summary>
public sealed class ImportRequestValidator : AbstractValidator<ImportRequest>
{
    public ImportRequestValidator()
    {
        RuleFor(request => request.Iban).MustParseAs(typeof(Iban));
        RuleFor(request => request.Quantity).MustSatisfy<ImportRequest, Quantity, int>();
    }
}

public sealed class ValidationTests
{
    [Fact]
    public void A_validator_reports_the_rule_each_member_breaks()
    {
        var result = new ImportRequestValidator().Validate(new ImportRequest("FR0030006000011234567890190", 101));

        result.Errors.Select(failure => (failure.PropertyName, failure.ErrorCode)).Should().BeEquivalentTo(
        [
            (nameof(ImportRequest.Iban), ValueObjectErrorCodes.InvalidFormat),
            (nameof(ImportRequest.Quantity), ValueObjectErrorCodes.OutOfRange),
        ]);
    }

    [Fact]
    public void A_validator_accepts_what_the_value_objects_accept()
        => new ImportRequestValidator().Validate(new ImportRequest("fr76 3000 6000 0112 3456 7890 189", 3)).IsValid.Should().BeTrue();
}

/// <summary>The contract kit, run by xUnit v3 on the next major.</summary>
public sealed class IbanContract : ValueObjectContract<Iban, string>
{
    protected override IEnumerable<string> AcceptedValues => ["FR7630006000011234567890189", "fr76 3000 6000 0112 3456 7890 189"];

    protected override IEnumerable<string> RejectedValues => ["", "not-an-iban", "FR0030006000011234567890190"];
}

/// <summary>The contract kit over a construction of a generic value object.</summary>
public sealed class PurchaseReferenceContract : ValueObjectContract<Reference<PurchaseOrder>, string>
{
    protected override IEnumerable<string> AcceptedValues => ["PO-1", " po-2 "];

    protected override IEnumerable<string> RejectedValues => ["", "THIRTEEN-CHARS"];
}
