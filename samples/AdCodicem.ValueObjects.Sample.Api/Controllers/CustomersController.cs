using AdCodicem.ValueObjects.Sample.Api.Contracts;
using AdCodicem.ValueObjects.Sample.Api.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdCodicem.ValueObjects.Sample.Api.Controllers;

/// <summary>
/// Customers, exercising value objects in routes, query strings, request bodies and responses.
/// </summary>
[ApiController]
[Route("customers")]
public sealed class CustomersController(BankingDbContext database) : ControllerBase
{
    /// <summary>Creates a customer.</summary>
    /// <param name="request">Customer to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created customer.</returns>
    [HttpPost]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerResponse>> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var customer = new Customer
        {
            Id = CustomerId.New(),
            Email = request.Email,
            Country = request.Country,
        };

        database.Customers.Add(customer);
        await database.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = customer.Id.ToString() }, ToResponse(customer));
    }

    /// <summary>Reads a customer by identifier, bound straight from the route segment.</summary>
    /// <param name="id">Customer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The customer.</returns>
    [HttpGet("{id}")]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(CustomerId id, CancellationToken cancellationToken)
    {
        var customer = await database.Customers
            .Include(entity => entity.Accounts)
            .FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

        return customer is null ? NotFound() : ToResponse(customer);
    }

    /// <summary>Lists customers, optionally filtered on a value object compared in SQL.</summary>
    /// <param name="country">Country to filter on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching customers.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CustomerResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CustomerResponse>>> List(
        [FromQuery] CountryCode? country,
        CancellationToken cancellationToken)
    {
        var query = database.Customers.Include(entity => entity.Accounts).AsQueryable();

        if (country is { } value)
        {
            // Translated to a plain comparison on the underlying column.
            query = query.Where(entity => entity.Country == value);
        }

        var customers = await query.OrderBy(entity => entity.Email).ToListAsync(cancellationToken);

        return customers.ConvertAll(ToResponse);
    }

    /// <summary>Opens an account for a customer.</summary>
    /// <param name="id">Customer identifier.</param>
    /// <param name="request">Account to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The opened account.</returns>
    [HttpPost("{id}/accounts")]
    [ProducesResponseType<AccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountResponse>> OpenAccount(
        CustomerId id,
        [FromBody] OpenAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!await database.Customers.AnyAsync(entity => entity.Id == id, cancellationToken))
        {
            return NotFound();
        }

        var account = new BankAccount
        {
            Iban = request.Iban,
            CustomerId = id,
            Balance = request.InitialBalance,
        };

        database.Accounts.Add(account);
        await database.SaveChangesAsync(cancellationToken);

        return Created($"/accounts/{account.Iban}", new AccountResponse(account.Iban, account.Balance));
    }

    private static CustomerResponse ToResponse(Customer customer)
        => new(
            customer.Id,
            customer.Email,
            customer.Country,
            customer.Accounts.ConvertAll(account => new AccountResponse(account.Iban, account.Balance)));
}
