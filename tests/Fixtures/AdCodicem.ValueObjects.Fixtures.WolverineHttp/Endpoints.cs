using System.Reflection;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Attributes;
using Wolverine.Configuration;
using Wolverine.Http;

namespace AdCodicem.ValueObjects.Fixtures.WolverineHttp;

/// <summary>
/// Endpoints taking value objects from every source, in every shape the package binds, and the parameters it leaves to
/// Wolverine: an <c>[AsParameters]</c> type and a JSON body. Each answers with the text of what it received.
/// </summary>
public static class ValueObjectEndpoints
{
    [WolverineGet("/orders/{id}")]
    public static string Order(OrderId id) => $"order {id}";

    [WolverineGet("/lines/{qty}")]
    public static string Line(Quantity qty) => $"line {qty}";

    [WolverineGet("/refs/{reference}")]
    public static string Reference(Reference<PurchaseOrder> reference) => $"reference {reference}";

    [WolverineGet("/hand/{code}")]
    public static string Hand(HandWrittenCode code) => $"hand {code}";

    [WolverineGet("/due/{due}")]
    public static string Due(DueDate due) => $"due {due}";

    [WolverineGet("/items")]
    public static string Items(Quantity qty) => $"items {qty}";

    [WolverineGet("/named")]
    public static string Named([FromQuery(Name = "q")] Quantity quantity) => $"named {quantity}";

    [WolverineGet("/quoted")]
    public static string Quoted([FromQuery(Name = "a\"b\\c")] Quantity quantity) => $"quoted {quantity}";

    [WolverineGet("/optional")]
    public static string Optional(CountryCode? country) => $"optional {(country is null ? "null" : country.ToString())}";

#pragma warning disable VO0010 // A default value makes the parameter optional, as in a minimal API: absent, it is left out.
    [WolverineGet("/defaulted")]
    public static string Defaulted(Quantity qty = default) => $"defaulted {(IsDefault<Quantity, int>(qty) ? "default" : qty.ToString())}";
#pragma warning restore VO0010

    [WolverineGet("/many")]
    public static string Many(Quantity[] qty) => $"many [{string.Join(",", qty)}]";

    [WolverineGet("/list")]
    public static string List(List<Quantity> qty) => $"list [{string.Join(",", qty)}] {qty.GetType().Name}";

    [WolverineGet("/sequence")]
    public static string Sequence(IEnumerable<Quantity> qty) => $"sequence [{string.Join(",", qty)}]";

    [WolverineGet("/readonly")]
    public static string ReadOnly(IReadOnlyList<Quantity> qty) => $"readonly [{string.Join(",", qty)}]";

    [WolverineGet("/when")]
    public static string When(DueDate due, Timestamp at, Consent consent, Amount amount)
        => $"when {due} {at:O} {consent} {amount}";

    [WolverineGet("/header")]
    public static string Header([FromHeader(Name = "X-Customer")] CustomerCode customer) => $"header {customer}";

    [WolverineGet("/nullable-header")]
    public static string NullableHeader([FromHeader(Name = "X-Country")] CountryCode? country)
        => $"nullable-header {(country is null ? "null" : country.ToString())}";

    [WolverineGet("/renamed/{n}")]
    public static string Renamed([FromRoute(Name = "n")] Quantity quantity) => $"renamed {quantity}";

    [WolverineGet("/optional-segment/{qty?}")]
    public static string OptionalSegment(Quantity qty) => $"optional-segment {qty}";

    [WolverineGet("/nullable-segment/{qty?}")]
    public static string NullableSegment(Quantity? qty) => $"nullable-segment {(qty is null ? "null" : qty.ToString())}";

    [WolverineGet("/constrained/{qty:int}")]
    public static string Constrained(Quantity qty) => $"constrained {qty}";

    [WolverineGet("/route-or-header/{qty}")]
    public static string RouteOrHeader([FromHeader(Name = "qty")] Quantity qty) => $"route-or-header {qty}";

    [WolverineGet("/as-parameters")]
    public static string AsParameters([AsParameters] LineParameters line)
        => $"as-parameters {line.Customer} {line.Quantity} {(line.Country is null ? "null" : line.Country.ToString())}";

    [WolverinePost("/orders")]
    public static string Submit(OrderSubmitted order) => $"submitted {order.Quantity}";

    private static bool IsDefault<TSelf, TValue>(TSelf value)
        where TSelf : struct, IValueObject<TSelf, TValue>
        => value.IsDefault;
}

/// <summary>A compound endpoint, whose <c>Before</c> method takes the value object its handler takes.</summary>
public static class CompoundEndpoint
{
    /// <summary>Records that it ran, and with which value.</summary>
    /// <param name="qty">The value the handler takes too.</param>
    public static void Before(Quantity qty) => RequestTrace.Write($"before {qty}");

    [WolverineGet("/compound")]
    public static string Get(Quantity qty) => $"compound {qty}";
}

/// <summary>Endpoints reading value objects from a form.</summary>
public static class FormEndpoints
{
    [WolverinePost("/form")]
    public static string Form([FromForm] Quantity qty) => $"form {qty}";

    [WolverinePost("/form-many")]
    public static string FormMany([FromForm] Quantity[] qty) => $"form-many [{string.Join(",", qty)}]";
}

/// <summary>
/// Endpoints where Wolverine looks a value up again by its source and name once their parameters are matched: an
/// <c>After</c> method taking the handler's route value or header, a middleware type's <c>Finally</c> method, and a
/// parameter attribute loading by a route or query value as <c>[Entity]</c> does. Each wrote a second local of the name
/// before the package took over Wolverine's variable, and failed to compile.
/// </summary>
public static class AfterRouteEndpoint
{
    [WolverineGet("/after-route/{qty}")]
    public static string Get(Quantity qty) => $"after-route {qty}";

    /// <summary>Records that it ran, and with which value.</summary>
    /// <param name="qty">The route value the handler takes.</param>
    public static void After(Quantity qty) => RequestTrace.Write($"after {qty}");
}

/// <summary>An endpoint whose <c>After</c> method takes the header its handler takes.</summary>
public static class AfterHeaderEndpoint
{
    [WolverineGet("/after-header")]
    public static string Get([FromHeader(Name = "X-Customer")] CustomerCode customer) => $"after-header {customer}";

    /// <summary>Records that it ran, and with which value.</summary>
    /// <param name="customer">The header the handler takes.</param>
    public static void After([FromHeader(Name = "X-Customer")] CustomerCode customer) => RequestTrace.Write($"after {customer}");
}

/// <summary>An endpoint <see cref="FinallyMiddleware"/> is applied to.</summary>
public static class FinallyEndpoint
{
    [WolverineGet("/finally/{qty}")]
    public static string Get(Quantity qty) => $"finally {qty}";
}

/// <summary>A middleware type whose <c>Finally</c> method takes the route value of the endpoint it is applied to.</summary>
public static class FinallyMiddleware
{
    /// <summary>Records that it ran, and with which value.</summary>
    /// <param name="qty">The route value the handler takes.</param>
    public static void Finally(Quantity qty) => RequestTrace.Write($"finally {qty}");
}

/// <summary>Endpoints loading an order's view by its identifier, read from the route or the query string.</summary>
public static class EntityEndpoints
{
    [WolverineGet("/entity/{id}")]
    public static string Route(OrderId id, [LookupOrder("id")] OrderView view) => $"entity {view.Text}";

    [WolverineGet("/entity")]
    public static string Query(OrderId id, [LookupOrder("id")] OrderView view) => $"entity {view.Text}";
}

/// <summary>What <see cref="LookupOrderAttribute"/> loads.</summary>
/// <param name="Text">A description of the order.</param>
public sealed record OrderView(string Text);

/// <summary>Loads an order's view, as an entity loader would.</summary>
public static class OrderLookup
{
    /// <summary>Finds the view of an order.</summary>
    /// <param name="id">The order.</param>
    /// <returns>Its view.</returns>
    public static OrderView Find(OrderId id) => new($"view of {id}");
}

/// <summary>
/// A parameter attribute finding its identity by name from any source, through the lookup <c>[Entity]</c> calls
/// (<c>tryFindIdentityVariable</c>), and loading the parameter from it.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class LookupOrderAttribute : WolverineParameterAttribute
{
    /// <summary>Creates the attribute.</summary>
    /// <param name="argumentName">The name of the identity.</param>
    public LookupOrderAttribute(string argumentName)
        : base(argumentName)
        => ValueSource = ValueSource.Anything;

    /// <inheritdoc />
    public override Variable Modify(IChain chain, ParameterInfo parameter, IServiceContainer container, GenerationRules rules)
    {
        if (!tryFindIdentityVariable(chain, parameter, typeof(OrderId), out var id))
        {
            throw new InvalidOperationException($"No identity named {ArgumentName}.");
        }

        var find = new MethodCall(typeof(OrderLookup), nameof(OrderLookup.Find));
        find.Arguments[0] = id;
        chain.Middleware.Add(find);
        return find.ReturnVariable!;
    }
}

/// <summary>A middleware method a policy registered after <c>UseValueObjects()</c> adds to the endpoints of <see cref="LateEndpoints"/>.</summary>
public static class LateMiddleware
{
    /// <summary>Records that it ran, and with which value.</summary>
    /// <param name="late">A header the endpoint does not read.</param>
    public static void Before([FromHeader(Name = "X-Late")] CustomerCode late) => RequestTrace.Write($"late {late}");
}

/// <summary>Endpoints a later policy adds <see cref="LateMiddleware"/> to, one taking a value object and one taking none.</summary>
public static class LateEndpoints
{
    [WolverineGet("/late")]
    public static string Late(Quantity qty) => $"late {qty}";

    [WolverineGet("/late-only")]
    public static string LateOnly() => "late-only";
}

/// <summary>The class an endpoint takes after a form value, which no JSON body holds.</summary>
public sealed class Payload
{
    /// <summary>Gets or sets a text.</summary>
    public string? Text { get; set; }
}

/// <summary>An endpoint taking a class parameter after a form value.</summary>
public static class FormPayloadEndpoint
{
    [WolverinePost("/form-payload")]
    public static string Post([FromForm] Quantity qty, Payload payload) => $"form-payload {qty} {payload is not null}";
}
