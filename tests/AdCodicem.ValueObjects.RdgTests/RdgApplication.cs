using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

namespace AdCodicem.ValueObjects.RdgTests;

/// <summary>
/// The application the tests send their requests to: minimal API endpoints binding each value object of this project
/// from a route value, the query string or a header, through the code the Request Delegate Generator writes.
/// </summary>
public sealed class RdgApplication : IAsyncLifetime
{
    private WebApplication _application = null!;

    /// <summary>Gets a client of the in-memory server.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        _application = builder.Build();

        // Each handler returns a member the generator writes under an explicit return type: the RDG cannot infer the
        // type of a member it does not see, and without one the project does not build (CS0411, CS1031).
        _application.MapGet("/skus/{sku}", static string (Sku sku) => sku.Value);
        _application.MapGet("/quantities/{quantity}", static int (Quantity quantity) => quantity.Value);
        _application.MapGet("/codes/{code}", static string (Code<Warehouse> code) => code.Value);
        _application.MapGet("/shelves/{shelf}", static string (Catalog.Shelf shelf) => shelf.Value);
        _application.MapGet("/customers", static string (CustomerId id) => id.Value);
        _application.MapGet("/search", static string (Sku? sku) => sku?.Value ?? "(none)");
        // Named apart from the query parameter of the same type above: the RDG writes one interceptor for two handlers
        // of the same parameter types and names and drops the attribute of the second, so that a header named "id"
        // would be read from the query string. It does so for a Guid as well.
        _application.MapGet("/current", static string ([FromHeader(Name = "X-Customer")] CustomerId customer) => customer.Value);

        await _application.StartAsync(TestContext.Current.CancellationToken);
        Client = _application.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
    }
}
