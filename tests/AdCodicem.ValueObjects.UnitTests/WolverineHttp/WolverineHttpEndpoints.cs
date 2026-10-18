using System.Reflection;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using JasperFx;
using JasperFx.CodeGeneration;
using JasperFx.CodeGeneration.Frames;
using JasperFx.CodeGeneration.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;
using Wolverine.Configuration;
using Wolverine.Http;

namespace AdCodicem.ValueObjects.UnitTests.WolverineHttp;

/// <summary>The owner a test closes <see cref="Reference{TOwner}"/> over, so that nothing else closes it alike.</summary>
public sealed class WolverineOwner;

/// <summary>Endpoints taking a value object in every shape the package binds.</summary>
public static class WolverineShapeEndpoints
{
    [WolverineGet("/single")]
    public static string SingleValue(Quantity qty) => qty.ToString();

    [WolverineGet("/nullable")]
    public static string NullableValue(Quantity? qty) => $"{qty}";

    [WolverineGet("/array")]
    public static string ArrayValue(Quantity[] qty) => $"{qty.Length}";

    [WolverineGet("/list")]
    public static string ListValue(List<Quantity> qty) => $"{qty.Count}";

    [WolverineGet("/ilist")]
    public static string InterfaceList(IList<Quantity> qty) => $"{qty.Count}";

    [WolverineGet("/readonly")]
    public static string ReadOnly(IReadOnlyList<Quantity> qty) => $"{qty.Count}";

    [WolverineGet("/sequence")]
    public static string Sequence(IEnumerable<Quantity> qty) => $"{qty.Count()}";

#pragma warning disable VO0010 // A default value makes the parameter optional, which is what the test reads.
    [WolverineGet("/defaulted")]
    public static string Defaulted(Quantity qty = default) => qty.ToString();
#pragma warning restore VO0010

    [WolverineGet("/generic")]
    public static string Generic(Reference<WolverineOwner> reference) => reference.ToString();

    [WolverineGet("/hand")]
    public static string Hand(HandWrittenCode code) => code.ToString();
}

/// <summary>Endpoints reading a value object from each source, under the names their attributes give.</summary>
public static class WolverineSourceEndpoints
{
    [WolverinePost("/form")]
    public static string Form([FromForm(Name = "q")] Quantity quantity) => quantity.ToString();

    [WolverinePost("/form-many")]
    public static string FormMany([FromForm] Quantity[] qty) => $"{qty.Length}";

    [WolverineGet("/renamed/{N}")]
    public static string Renamed([FromRoute(Name = "n")] Quantity quantity) => quantity.ToString();

    [WolverineGet("/route/{qty}")]
    public static string RouteOverHeader([FromHeader(Name = "X-Qty")] Quantity qty) => qty.ToString();

    [WolverineGet("/route-attribute/{qty}")]
    public static string RouteAttribute([FromRoute] Quantity qty) => qty.ToString();

    [WolverineGet("/header")]
    public static string Header([FromHeader(Name = "X-Quantity")] Quantity quantity) => quantity.ToString();

    [WolverineGet("/header-default")]
    public static string HeaderByName([FromHeader] Quantity qty) => qty.ToString();

    [WolverineGet("/query")]
    public static string Query([FromQuery(Name = "q")] Quantity quantity) => quantity.ToString();

    [WolverineGet("/query-default")]
    public static string QueryByName([FromQuery] Quantity qty) => qty.ToString();

    [WolverineGet("/optional/{qty?}")]
    public static string OptionalSegment(Quantity qty) => qty.ToString();

    [WolverineGet("/optional-nullable/{qty?}")]
    public static string NullableSegment(Quantity? qty) => $"{qty}";
}

/// <summary>
/// An endpoint reading a nullable value object from a route value <c>[FromRoute]</c> names, for which Wolverine's route
/// factory creates no variable, since it parses no nullable type under a name an attribute gives.
/// </summary>
public static class WolverineNullableRenamedEndpoint
{
    [WolverineGet("/renamed-nullable/{n}")]
    public static string Get([FromRoute(Name = "n")] Quantity? quantity) => $"{quantity}";
}

/// <summary>A compound endpoint whose <c>Before</c> method takes the value object its handler takes.</summary>
public static class WolverineCompoundEndpoint
{
    public static void Before(Quantity qty) => _ = qty;

    [WolverineGet("/compound")]
    public static string Get(Quantity qty) => qty.ToString();
}

/// <summary>A compound endpoint whose <c>Before</c> method takes another value object under the handler's name.</summary>
public static class WolverineClashingEndpoint
{
    public static void Before(CountryCode qty) => _ = qty;

    [WolverineGet("/clash")]
    public static string Get(Quantity qty) => qty.ToString();
}

/// <summary>An endpoint naming a route parameter its template lacks.</summary>
public static class WolverineMissingRouteEndpoint
{
    [WolverineGet("/missing/{other}")]
    public static string Get([FromRoute(Name = "absent")] Quantity quantity) => quantity.ToString();
}

/// <summary>An endpoint reading one name from a header and from the query string.</summary>
public static class WolverineTwoSourcesEndpoint
{
    [WolverineGet("/two-sources")]
    public static string Get([FromHeader(Name = "q")] Quantity fromHeader, [FromQuery(Name = "q")] Quantity fromQuery)
        => $"{fromHeader} {fromQuery}";
}

/// <summary>An endpoint taking no value object.</summary>
public static class WolverinePlainEndpoint
{
    [WolverineGet("/plain")]
    public static string Get(int count) => $"{count}";
}

/// <summary>An endpoint taking a value object of the query string, and nothing else Wolverine parses.</summary>
public static class WolverineQueryOnlyEndpoint
{
    [WolverineGet("/query-only")]
    public static string Get(Quantity qty) => qty.ToString();
}

/// <summary>An endpoint taking a value object of every family, from the route, the query, a header and a form.</summary>
public static class WolverineFamilyEndpoints
{
    [WolverineGet("/families/{iban}/{customer}")]
    public static string Route(Iban iban, CustomerId customer, Quantity quantity, Consent consent, BirthDate birth, CountryCode country)
        => $"{iban} {customer} {quantity} {consent} {birth} {country}";

    [WolverineGet("/families/more")]
    public static string More(
        LedgerBalance balance,
        AccountId account,
        Ordering.OrderReference order,
        Reference<WolverineOwner> reference,
        HandWrittenTag<WolverineOwner> tag,
        HandWrittenCode code,
        [FromHeader(Name = "X-Quantities")] Quantity? quantity,
        [FromQuery(Name = "a\"b\\c")] Quantity[] quoted)
        => $"{balance} {account} {order} {reference} {tag} {code} {quantity} {quoted.Length}";

    [WolverinePost("/families/form")]
    public static string Form([FromForm] Quantity qty, [FromForm] List<CountryCode> countries, [FromForm] IReadOnlyList<Iban> ibans)
        => $"{qty} {countries.Count} {ibans.Count}";
}

/// <summary>
/// An endpoint whose <c>After</c> method takes the route value its handler takes, which Wolverine looks up again by name
/// after parameter matching: the second read was a second local of the same name (CS0128) before the package took over
/// Wolverine's variable.
/// </summary>
public static class WolverineAfterRouteEndpoint
{
    [WolverineGet("/after-route/{qty}")]
    public static string Get(Quantity qty) => qty.ToString();

    public static void After(Quantity qty) => _ = qty;
}

/// <summary>An endpoint whose <c>After</c> method takes the header its handler takes.</summary>
public static class WolverineAfterHeaderEndpoint
{
    [WolverineGet("/after-header")]
    public static string Get([FromHeader(Name = "X-Quantity")] Quantity quantity) => quantity.ToString();

    public static void After([FromHeader(Name = "X-Quantity")] Quantity quantity) => _ = quantity;
}

/// <summary>An endpoint whose <c>After</c> method takes the query value its handler takes, both under <c>[FromQuery]</c>.</summary>
public static class WolverineAfterQueryEndpoint
{
    [WolverineGet("/after-query")]
    public static string Get([FromQuery(Name = "q")] Quantity quantity) => quantity.ToString();

    public static void After([FromQuery(Name = "q")] Quantity quantity) => _ = quantity;
}

/// <summary>
/// An endpoint reading a route value under the name <c>[FromRoute]</c> gives, whose <c>After</c> method takes it under
/// that name.
/// </summary>
public static class WolverineAfterRenamedEndpoint
{
    [WolverineGet("/after-renamed/{n}")]
    public static string Get([FromRoute(Name = "n")] Quantity quantity) => quantity.ToString();

    public static void After(Quantity n) => _ = n;
}

/// <summary>An endpoint a middleware type with a <c>Finally</c> method is applied to.</summary>
public static class WolverineFinallyRouteEndpoint
{
    [WolverineGet("/finally-route/{qty}")]
    public static string Get(Quantity qty) => qty.ToString();
}

/// <summary>
/// An endpoint a policy adds <see cref="WolverineUnmatchedMiddleware"/> to without matching its parameters, which
/// Wolverine fills by name from the route values when it writes the code.
/// </summary>
public static class WolverineUnmatchedRouteEndpoint
{
    [WolverineGet("/unmatched-route/{qty}")]
    public static string Get(Quantity qty) => qty.ToString();
}

/// <summary>A middleware method a policy adds to an endpoint without matching its parameters.</summary>
public static class WolverineUnmatchedMiddleware
{
    public static void Before(Quantity qty) => _ = qty;
}

/// <summary>
/// Endpoints with a parameter attribute loading by a value the package reads, from the route or the query string, before
/// or after the package reads it.
/// </summary>
public static class WolverineEntityEndpoints
{
    [WolverineGet("/entity-route/{id}")]
    public static string Route(CustomerId id, [WolverineLookup("id")] WolverineCustomerView view) => $"{id} {view.Text}";

    [WolverineGet("/entity-query")]
    public static string Query(CustomerId id, [WolverineLookup("id")] WolverineCustomerView view) => $"{id} {view.Text}";

    [WolverineGet("/entity-first/{id}")]
    public static string First([WolverineLookup("id")] WolverineCustomerView view, CustomerId id) => $"{id} {view.Text}";
}

/// <summary>A middleware type whose <c>Finally</c> method takes the route value its endpoint takes.</summary>
public static class WolverineFinallyMiddleware
{
    public static void Finally(Quantity qty) => _ = qty;
}

/// <summary>What <see cref="WolverineLookupAttribute"/> loads.</summary>
/// <param name="Text">A description of the customer.</param>
public sealed record WolverineCustomerView(string Text);

/// <summary>Loads a customer's view, as an entity loader would.</summary>
public static class WolverineCustomerLookup
{
    /// <summary>Finds the view of a customer.</summary>
    /// <param name="id">The customer.</param>
    /// <returns>Its view.</returns>
    public static WolverineCustomerView Find(CustomerId id) => new($"view of {id}");
}

/// <summary>
/// A parameter attribute finding its identity by name from any source, through the lookup <c>[Entity]</c> calls
/// (<c>tryFindIdentityVariable</c>), and loading the parameter from it.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class WolverineLookupAttribute : WolverineParameterAttribute
{
    /// <summary>Creates the attribute.</summary>
    /// <param name="argumentName">The name of the identity.</param>
    public WolverineLookupAttribute(string argumentName)
        : base(argumentName)
        => ValueSource = ValueSource.Anything;

    /// <inheritdoc />
    public override Variable Modify(IChain chain, ParameterInfo parameter, IServiceContainer container, GenerationRules rules)
    {
        if (!tryFindIdentityVariable(chain, parameter, typeof(CustomerId), out var id))
        {
            throw new InvalidOperationException($"No identity named {ArgumentName}.");
        }

        var find = new MethodCall(typeof(WolverineCustomerLookup), nameof(WolverineCustomerLookup.Find));
        find.Arguments[0] = id;
        chain.Middleware.Add(find);
        return find.ReturnVariable!;
    }
}

/// <summary>A middleware method a policy registered after <c>UseValueObjects()</c> adds to an endpoint.</summary>
public static class WolverineLateMiddleware
{
    public static void Before([FromHeader(Name = "X-Late")] CountryCode late) => _ = late;
}

/// <summary>Endpoints a later policy adds <see cref="WolverineLateMiddleware"/> to.</summary>
public static class WolverineLateEndpoints
{
    [WolverineGet("/late")]
    public static string Late(Quantity qty) => qty.ToString();

    [WolverineGet("/late-only")]
    public static string LateOnly(int count) => $"{count}";
}

/// <summary>The class an endpoint takes after a form value, which no JSON body holds.</summary>
public sealed class WolverinePayload
{
    /// <summary>Gets or sets a text.</summary>
    public string? Text { get; set; }
}

/// <summary>An endpoint taking a class parameter after a form value.</summary>
public static class WolverineFormPayloadEndpoint
{
    [WolverinePost("/form-payload")]
    public static string Post([FromForm] Quantity qty, WolverinePayload payload) => $"{qty} {payload.Text}";
}

/// <summary>
/// Parameters the package declines, read by <c>ValueObjectHttpParameter.TryDescribe</c> directly: none is an endpoint.
/// </summary>
public static class WolverineDeclinedParameters
{
    public static void Matrix(Quantity[,] qty) => _ = qty;

    public static void NullableElements(Quantity?[] qty) => _ = qty;

    public static void Collection(ICollection<Quantity> qty) => _ = qty;

    public static void Dictionary(Dictionary<string, Quantity> qty) => _ = qty;

    public static void Primitive(short qty) => _ = qty;

    public static void Marker(MarkerOnlyValue qty) => _ = qty;

    public static void Selfless(SelflessValue qty) => _ = qty;

    public static void ClassBacked(ClassBackedValue qty) => _ = qty;

    public static void Imposter(WolverineImposter qty) => _ = qty;

    public static void Open<TOwner>(Reference<TOwner> qty)
        where TOwner : class
        => _ = qty;

    public static void AsParameters([AsParameters] Quantity qty) => _ = qty;

    public static void Body([FromBody] Quantity qty) => _ = qty;

    public static void Service([FromServices] Quantity qty) => _ = qty;

    public static void KeyedService([FromKeyedServices("key")] Quantity qty) => _ = qty;

    public static void NotBody([NotBody] Quantity qty) => _ = qty;

    public static void Loaded([WolverineLoaded] Quantity qty) => _ = qty;

    public static void RouteArray(Quantity[] qty) => _ = qty;

    public static void HeaderArray([FromHeader(Name = "X-Quantities")] Quantity[] qty) => _ = qty;

    public static void HeaderList([FromHeader(Name = "X-Quantities")] List<Quantity> qty) => _ = qty;

    public static void RouteSequence([FromRoute(Name = "qty")] IEnumerable<Quantity> quantities) => _ = quantities;
}

/// <summary>An attribute of Wolverine's family of parameter attributes, which load or bind a parameter themselves.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class WolverineLoadedAttribute : WolverineParameterAttribute
{
    /// <inheritdoc />
    public override Variable Modify(IChain chain, ParameterInfo parameter, IServiceContainer container, GenerationRules rules)
        => throw new NotSupportedException("No endpoint takes it.");
}

/// <summary>
/// A struct implementing the contract over another value object, which every member delegates to: the generated code
/// would assign a <see cref="HandWrittenCode"/> to it, so the package binds a struct implementing it over itself alone.
/// </summary>
#pragma warning disable CA1036, CA2260 // The imposter is the point: it implements the contract over another type, and needs no operator.
public readonly struct WolverineImposter : IValueObject<HandWrittenCode, string>
{
    public static Metadata.ValueObjectSchema Schema => HandWrittenCode.Schema;

    public string Value => string.Empty;

    public bool IsDefault => true;

    public static string Normalize(string value) => HandWrittenCode.Normalize(value);

    public static ValidationResult Validate(in string value) => HandWrittenCode.Validate(value);

    public static HandWrittenCode Create(string value) => HandWrittenCode.Create(value);

    public static bool TryCreate(string value, out HandWrittenCode result) => HandWrittenCode.TryCreate(value, out result);

    public static bool TryCreate(string value, out HandWrittenCode result, out ValidationResult validation)
        => HandWrittenCode.TryCreate(value, out result, out validation);

    public static HandWrittenCode CreateUnchecked(string value) => HandWrittenCode.CreateUnchecked(value);

    public static bool TryParse(ReadOnlySpan<char> text, IFormatProvider? provider, out HandWrittenCode result, out ValidationResult validation)
        => HandWrittenCode.TryParse(text, provider, out result, out validation);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out HandWrittenCode result)
        => HandWrittenCode.TryParse(s, provider, out result);

    public static bool TryParse(string? s, IFormatProvider? provider, out HandWrittenCode result)
        => HandWrittenCode.TryParse(s, provider, out result);

    public static HandWrittenCode Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => HandWrittenCode.Parse(s, provider);

    public static HandWrittenCode Parse(string s, IFormatProvider? provider) => HandWrittenCode.Parse(s, provider);

    public bool Equals(HandWrittenCode other) => other.IsDefault;

    public int CompareTo(HandWrittenCode other) => other.IsDefault ? 0 : -1;

    public string ToString(string? format, IFormatProvider? formatProvider) => string.Empty;

    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        charsWritten = 0;
        return true;
    }
}
#pragma warning restore CA1036, CA2260
