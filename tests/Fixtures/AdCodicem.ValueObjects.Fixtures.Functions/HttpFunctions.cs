using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace AdCodicem.ValueObjects.Fixtures.Functions;

/// <summary>HTTP triggers binding value objects from route values, query values and a request body.</summary>
public static class HttpFunctions
{
    [Function("Ready")]
    public static IResult Ready([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ready")] HttpRequest request)
        => Results.Text("ready");

    [Function("Order")]
    public static IResult Order([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{id}")] HttpRequest request, OrderId id)
        => Results.Text($"OrderId({id.Value})");

    [Function("Line")]
    public static IResult Line([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "lines/{qty}")] HttpRequest request, Quantity qty)
        => Results.Text($"Quantity({qty.Value})");

    [Function("Amount")]
    public static IResult Amount([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "amounts/{amount}")] HttpRequest request, Amount amount)
        => Results.Text(FormattableString.Invariant($"Amount({amount.Value})"));

    [Function("Due")]
    public static IResult Due([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "due/{date}")] HttpRequest request, DueDate date)
        => Results.Text($"DueDate({date.Value:yyyy-MM-dd})");

    [Function("Query")]
    public static IResult Query([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "query")] HttpRequest request, CustomerCode customer)
        => Results.Text($"CustomerCode('{customer.Value}')");

    [Function("Keyed")]
    public static IResult Keyed([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "keyed")] HttpRequest request, CustomerCode? code)
        => Results.Text($"CustomerCode? = {(code is { } value ? value.Value : "null")}");

    [Function("QueryOptional")]
    public static IResult QueryOptional([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "query-optional")] HttpRequest request, CustomerCode? customer)
        => Results.Text($"CustomerCode? = {(customer is { } code ? code.Value : "null")}");

    [Function("Two")]
    public static IResult Two([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "two/{id}/{qty}")] HttpRequest request, OrderId id, Quantity qty)
        => Results.Text($"OrderId({id.Value}) Quantity({qty.Value})");

    [Function("Generic")]
    public static IResult Generic([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "refs/{reference}")] HttpRequest request, Reference<PurchaseOrder> reference)
        => Results.Text($"Reference<PurchaseOrder>('{reference.Value}')");

    [Function("Hand")]
    public static IResult Hand([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "hand/{letters}")] HttpRequest request, HandWrittenCode letters)
        => Results.Text($"HandWrittenCode('{letters.Value}')");

    [Function("PlainInt")]
    public static IResult PlainInt([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ints/{n}")] HttpRequest request, int n)
        => Results.Text(FormattableString.Invariant($"int {n}"));

    [Function("Mixed")]
    public static IResult Mixed([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mixed/{qty}/{n}")] HttpRequest request, Quantity qty, int n)
        => Results.Text(FormattableString.Invariant($"Quantity({qty.Value}) int {n}"));

    [Function("Body")]
    public static IActionResult Body([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "body")] HttpRequest request, [FromBody] OrderSubmitted order)
        => new OkObjectResult($"OrderSubmitted({order.OrderId.Value}, '{order.Customer.Value}', {order.Quantity.Value})");

    [Function("BodyAndRoute")]
    public static IActionResult BodyAndRoute([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "body/{qty}")] HttpRequest request, [FromBody] OrderSubmitted order, Quantity qty)
        => new OkObjectResult($"{order.Quantity.Value} {qty.Value}");
}
