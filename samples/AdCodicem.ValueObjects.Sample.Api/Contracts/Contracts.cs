using AdCodicem.ValueObjects.FluentValidation;
using FluentValidation;

namespace AdCodicem.ValueObjects.Sample.Api.Contracts;

/// <summary>
/// Request body for creating a customer, typed with value objects all the way down.
/// </summary>
/// <param name="Email">Email address of the customer.</param>
/// <param name="Country">Country of residence.</param>
public sealed record CreateCustomerRequest(EmailAddress Email, CountryCode Country);

/// <summary>
/// Request body for opening an account.
/// </summary>
/// <param name="Iban">Account number.</param>
/// <param name="InitialBalance">Balance the account starts with.</param>
public sealed record OpenAccountRequest(Iban Iban, Amount InitialBalance);

/// <summary>
/// A customer as returned by the API.
/// </summary>
/// <param name="Id">Identifier.</param>
/// <param name="Email">Email address.</param>
/// <param name="Country">Country of residence.</param>
/// <param name="Accounts">Accounts held.</param>
public sealed record CustomerResponse(CustomerId Id, EmailAddress Email, CountryCode Country, IReadOnlyList<AccountResponse> Accounts);

/// <summary>
/// An account as returned by the API.
/// </summary>
/// <param name="Iban">Account number.</param>
/// <param name="Balance">Current balance.</param>
public sealed record AccountResponse(Iban Iban, Amount Balance);

/// <summary>
/// A request that carries raw text rather than value objects, as an inbound message from another system would.
/// </summary>
/// <param name="Iban">Account number, as text.</param>
public sealed record ImportAccountRequest(string? Iban);

/// <summary>
/// Shows a validator deferring to the rules the value object already owns instead of restating them.
/// </summary>
public sealed class ImportAccountRequestValidator : AbstractValidator<ImportAccountRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImportAccountRequestValidator"/> class.
    /// </summary>
    public ImportAccountRequestValidator()
    {
        RuleFor(request => request.Iban)
            .NotEmpty()
            .MustParseAs(typeof(Iban));
    }
}
