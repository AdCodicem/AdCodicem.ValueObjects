using Microsoft.AspNetCore.Mvc;

namespace AdCodicem.ValueObjects.NativeAot;

/// <summary>
/// The minimal API endpoints the script calls over Kestrel, each binding value objects from another source. The Request
/// Delegate Generator writes their binding in both runs, from the interfaces it reads in the domain's assembly.
/// </summary>
internal static class Endpoints
{
    public static void Map(WebApplication app)
    {
        // A route value.
        app.MapGet("/quantities/{quantity}", (Quantity quantity) => TypedResults.Ok(quantity));

        // A query value, required.
        app.MapGet("/customers", (CustomerId id) => TypedResults.Ok(id));

        // A query value, optional.
        app.MapGet("/pages", (PageNumber? number) => TypedResults.Ok(new Page(number)));

        // A header, carrying an entity identifier.
        app.MapGet("/orders/current", ([FromHeader(Name = "X-Order-Id")] OrderId id) => TypedResults.Ok(id));

        // A body.
        app.MapPost("/orders", (Order order) => TypedResults.Ok(order));

        // A closed set of numbers, and a construction of a generic value object registered by hand.
        app.MapGet("/rates/{rate}", (VatRate rate) => TypedResults.Ok(rate));
        app.MapGet("/documents/{number}", (DocumentNumber<PurchaseOrder> number) => TypedResults.Ok(number));

        // A member the generator writes, returned under the explicit return type the RDG needs.
        app.MapGet("/emails/{email}", string (EmailAddress email) => email.Value);
    }
}
