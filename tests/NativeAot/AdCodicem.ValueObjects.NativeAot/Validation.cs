using AdCodicem.ValueObjects.FluentValidation;
using FluentValidation;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>A form carrying raw values, which a validator holds to the rules of the value objects they become.</summary>
internal sealed record OrderForm(string? Email, short Quantity, CustomerId Customer, string? Status, string? Number);

/// <summary>States each rule once, by deferring to the value object that owns it.</summary>
internal sealed class OrderFormValidator : AbstractValidator<OrderForm>
{
    public OrderFormValidator()
    {
        RuleFor(form => form.Email).MustParseAs(typeof(EmailAddress));
        RuleFor(form => form.Quantity).MustSatisfy<OrderForm, Quantity, short>();
        RuleFor(form => form.Customer).NotDefault<OrderForm, CustomerId, Guid>();
        RuleFor(form => form.Status).MustParseAs(typeof(DocumentStatus)).WithErrorCode("order.status");

        // A construction of a generic value object, which native AOT finds only because the application registered it.
        RuleFor(form => form.Number).MustParseAs(typeof(DocumentNumber<PurchaseOrder>));
    }
}

/// <summary>Runs the validator over forms it accepts and forms it refuses.</summary>
internal static class Validation
{
    public static void Run(Report report)
    {
        var validator = new OrderFormValidator();
#pragma warning disable VO0010 // An uninitialized identifier is what NotDefault catches.
        OrderForm[] forms =
        [
            new(" Ada@Example.com ", 3, CustomerId.Parse("0f8fad5b-d9cb-469f-a165-70867728950e", null), "draft", "po-1042"),
            new(null, 1000, CustomerId.Parse("0f8fad5b-d9cb-469f-a165-70867728950e", null), null, null),
            new("ada", 0, default, "archived", "PO-1042-0001-X"),
            new("", 1001, default, "", ""),
        ];
#pragma warning restore VO0010

        foreach (var form in forms)
        {
            var result = validator.Validate(form);
            var errors = result.Errors.Select(error => $"{error.PropertyName} {error.ErrorCode} \"{error.ErrorMessage}\"");
            report.Line($"FluentValidation {form}", result.IsValid ? "valid" : string.Join("; ", errors));
        }
    }
}
