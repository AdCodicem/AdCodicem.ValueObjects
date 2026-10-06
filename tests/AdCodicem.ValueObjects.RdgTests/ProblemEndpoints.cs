using AdCodicem.ValueObjects.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AdCodicem.ValueObjects.RdgTests;

/// <summary>
/// The endpoints whose refusals the problem details tests send, under a <c>/problems</c> group covered by
/// <see cref="ValueObjectEndpointConventionBuilderExtensions.WithValueObjectProblemDetails"/>: one call site per
/// endpoint, which every application mapping them shares.
/// </summary>
/// <remarks>
/// Each handler's parameters are named apart from every other handler's of the project with the same types: the RDG
/// writes one interceptor for two handlers of the same parameter types and names, and drops the attributes of the second.
/// </remarks>
public static class ProblemEndpoints
{
    /// <summary>Maps the endpoints.</summary>
    /// <param name="routes">Where they are mapped.</param>
    public static void Map(IEndpointRouteBuilder routes)
    {
        var problems = routes.MapGroup("/problems").WithValueObjectProblemDetails();

        problems.MapGet("/items/{item}", static string (Sku item) => item.Value);
        problems.MapGet("/lines", static string (Quantity amount, Sku? product) => $"{amount.Value} {product?.Value}");
        problems.MapGet("/named/{n}", static string ([FromRoute(Name = "n")] Quantity count, [FromQuery(Name = "q")] Quantity units) => $"{count.Value} {units.Value}");
        problems.MapGet("/owner", static string ([FromHeader(Name = "X-Owner")] CustomerId owner) => owner.Value);
        problems.MapGet("/search", static string ([AsParameters] ProblemSearch search) => $"{search.Page.Value} {search.Product?.Value} {search.Buyer.Value}");
        problems.MapGet("/batches", static string (Quantity[] counts) => string.Join(',', counts.Select(static count => count.Value)));
        problems.MapGet("/warehouses/{warehouse}", static string (Code<Warehouse> warehouse) => warehouse.Value);
        problems.MapGet("/places/{place}", static string (Catalog.Shelf place) => place.Value);
        problems.MapGet("/mixed/{id}", static string (int id, Quantity size) => $"{id} {size.Value}");
        problems.MapPost("/orders", static string (ProblemOrder order) => order.Product.Value);
        problems.MapPost("/parcels", static string ([FromBody] Sku parcel) => parcel.Value);
    }
}

/// <summary>A search, bound through <c>[AsParameters]</c>.</summary>
/// <param name="Page">The page, from the query string under the name of the member.</param>
/// <param name="Product">The product, optional, under the query name <c>s</c>.</param>
/// <param name="Buyer">The buyer, from a header.</param>
public sealed record ProblemSearch(Quantity Page, [FromQuery(Name = "s")] Sku? Product, [FromHeader(Name = "X-Buyer")] CustomerId Buyer);

/// <summary>An order read from a JSON body.</summary>
/// <param name="Product">The product ordered.</param>
/// <param name="Amount">How many.</param>
public sealed record ProblemOrder(Sku Product, Quantity Amount);
