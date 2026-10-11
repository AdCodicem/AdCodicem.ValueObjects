using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// Actions taking value objects from every source the model binder reads, from headers, from a body, and through a
/// binder an action names, for the in-memory MVC application of <see cref="ModelBindingTests"/>.
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

    /// <summary>Echoes an optional country read from a header.</summary>
    /// <param name="country">The country, if any.</param>
    /// <returns>The country, or <c>none</c>.</returns>
    [HttpGet("headers/country")]
    public IActionResult HeaderCountry([FromHeader(Name = "X-Country")] CountryCode? country) => Ok(country?.Value ?? "none");

    /// <summary>Echoes a customer identifier read from the header named after the parameter.</summary>
    /// <param name="customer">The identifier.</param>
    /// <returns>The identifier.</returns>
    [HttpGet("headers/customer")]
    public IActionResult HeaderCustomer([FromHeader] CustomerId customer) => Ok(customer.ToString());

    /// <summary>Counts the quantities read from a header, its comma-separated values each bound.</summary>
    /// <param name="quantities">The quantities.</param>
    /// <returns>How many there are.</returns>
    [HttpGet("headers/quantities")]
    public IActionResult HeaderQuantities([FromHeader(Name = "X-Quantity")] Quantity[] quantities) => Ok(quantities.Length);

    /// <summary>Echoes the reference of a purchase order read from a header, a construction of a generic value object.</summary>
    /// <param name="reference">The reference.</param>
    /// <returns>Its value.</returns>
    [HttpGet("headers/reference")]
    public IActionResult HeaderReference([FromHeader(Name = "X-Reference")] Reference<PurchaseOrder> reference) => Ok(reference.Value);

    /// <summary>Echoes a value object written by hand, read from a header.</summary>
    /// <param name="counter">The counter.</param>
    /// <returns>Its value.</returns>
    [HttpGet("headers/counter")]
    public IActionResult HeaderCounter([FromHeader(Name = "X-Counter")] HandWrittenCounter counter) => Ok(counter.Value);

    /// <summary>Echoes the country of a search whose members come from the query string and from headers.</summary>
    /// <param name="search">The search.</param>
    /// <returns>Its country, else the one it ships to, or <c>none</c>.</returns>
    [HttpGet("headers/search")]
    public IActionResult HeaderSearch([FromQuery] ProbeHeaderSearch search) => Ok(search.Country?.Value ?? search.ShipTo?.Value ?? "none");

    /// <summary>Echoes a country read from the body itself, a JSON string.</summary>
    /// <param name="country">The country.</param>
    /// <returns>Its value.</returns>
    [HttpPost("countries")]
    public IActionResult BodyCountry([FromBody] CountryCode country) => Ok(country.Value);

    /// <summary>Echoes a country bound by the binder the parameter names.</summary>
    /// <param name="country">The country.</param>
    /// <returns>Its value.</returns>
    [HttpGet("countries/named-binder")]
    public IActionResult NamedBinderCountry([ModelBinder(typeof(LuxembourgBinder))] CountryCode country) => Ok(country.Value);

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

/// <summary>A search bound from the query string, whose countries are read from headers.</summary>
public sealed class ProbeHeaderSearch
{
    /// <summary>Gets the page, from the query string.</summary>
    public Quantity? Page { get; init; }

    /// <summary>Gets the country, from a header.</summary>
    [FromHeader(Name = "X-Country")]
    public CountryCode? Country { get; init; }

    /// <summary>Gets the country it ships to, from the header named after the member.</summary>
    [FromHeader]
    public CountryCode? ShipTo { get; init; }
}

/// <summary>A binder an action names, which binds Luxembourg whatever the request holds.</summary>
public sealed class LuxembourgBinder : IModelBinder
{
    /// <inheritdoc />
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        bindingContext.Result = ModelBindingResult.Success(CountryCode.Create("LU"));

        return Task.CompletedTask;
    }
}
