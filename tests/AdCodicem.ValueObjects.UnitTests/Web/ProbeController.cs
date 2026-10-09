using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.Mvc;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// Actions taking value objects from every source the model binder reads, and from a body, for the in-memory MVC
/// application of <see cref="ModelBindingTests"/>.
/// </summary>
[ApiController]
[Route("probe")]
public sealed class ProbeController : ControllerBase
{
    /// <summary>Echoes an optional country read from the query string.</summary>
    /// <param name="country">The country, if any.</param>
    /// <returns>The country, or <c>none</c>.</returns>
    [HttpGet("country")]
    public IActionResult Country([FromQuery] CountryCode? country) => Ok(country?.Value ?? "none");

    /// <summary>Echoes two optional value objects read from the query string.</summary>
    /// <param name="country">The country, if any.</param>
    /// <param name="email">The email address, if any.</param>
    /// <returns>Both values.</returns>
    [HttpGet("search")]
    public IActionResult Search([FromQuery] CountryCode? country, [FromQuery] EmailAddress? email) => Ok($"{country}|{email}");

    /// <summary>Echoes a customer identifier read from the route.</summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The identifier.</returns>
    [HttpGet("customers/{id}")]
    public IActionResult Customer(CustomerId id) => Ok(id.ToString());

    /// <summary>Echoes an amount read from the query string.</summary>
    /// <param name="total">The amount.</param>
    /// <returns>Its value.</returns>
    [HttpGet("total")]
    public IActionResult Total([FromQuery] Amount total) => Ok(total.Value);

    /// <summary>Echoes a value object written by hand, whose parser may refuse text without saying why.</summary>
    /// <param name="counter">The counter.</param>
    /// <returns>Its value.</returns>
    [HttpGet("counter")]
    public IActionResult Counter([FromQuery] HandWrittenCounter counter) => Ok(counter.Value);

    /// <summary>Counts the quantities read from the query string, every element bound under the array's name.</summary>
    /// <param name="quantities">The quantities.</param>
    /// <returns>How many there are.</returns>
    [HttpGet("batches")]
    public IActionResult Batches([FromQuery] Quantity[] quantities) => Ok(quantities.Length);

    /// <summary>Echoes the reference of a purchase order, a construction of a generic value object.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns>Its value.</returns>
    [HttpGet("purchase-orders")]
    public IActionResult PurchaseOrderReference([FromQuery] Reference<PurchaseOrder> reference) => Ok(reference.Value);

    /// <summary>Echoes the reference of an order read from a JSON body.</summary>
    /// <param name="order">The order.</param>
    /// <returns>Its reference.</returns>
    [HttpPost("orders")]
    public IActionResult Order([FromBody] ProbeOrder order) => Ok(order?.Reference.Value);

    /// <summary>Echoes an order read from a JSON body, for the country read from the query string.</summary>
    /// <param name="country">The country, if any.</param>
    /// <param name="order">The order.</param>
    /// <returns>Both values.</returns>
    [HttpPost("orders/by-country")]
    public IActionResult OrderByCountry([FromQuery] CountryCode? country, [FromBody] ProbeOrder order)
        => Ok($"{country}|{order?.Reference}");
}

/// <summary>A request body holding a value object.</summary>
/// <param name="Reference">Reference of the order.</param>
public sealed record ProbeOrder(Ordering.OrderReference Reference);
