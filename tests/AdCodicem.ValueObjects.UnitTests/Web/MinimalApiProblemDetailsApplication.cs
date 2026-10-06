using System.CodeDom.Compiler;
using AdCodicem.ValueObjects.AspNetCore.Http;
using AdCodicem.ValueObjects.UnitTests.Domain.HandWritten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.UnitTests.Web;

/// <summary>
/// A minimal API application, bound through the reflection-based binding, whose <c>/api</c> group answers a refused
/// value object with problem details carrying its rule code, with <c>RouteHandlerOptions.ThrowOnBadRequest</c> as one of
/// the subclasses sets it.
/// </summary>
public abstract class MinimalApiProblemDetailsApplication(bool throwOnBadRequest) : IAsyncLifetime
{
    /// <summary>The tool the Request Delegate Generator names in the metadata of the endpoints it writes.</summary>
    public const string RequestDelegateGenerator =
        "Microsoft.AspNetCore.Http.RequestDelegateGenerator, Version=10.0.12.0, Culture=neutral, PublicKeyToken=adb9793829ddae60";

    private WebApplication _application = null!;

    /// <summary>Gets a value indicating whether the binder throws instead of answering, as in Development.</summary>
    public bool ThrowOnBadRequest => throwOnBadRequest;

    /// <summary>Gets a client of the in-memory server.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Gets the in-memory server, which takes a request built on its own context, an empty header included.</summary>
    public TestServer Server => _application.GetTestServer();

    /// <summary>Gets the endpoints the application maps.</summary>
    public IReadOnlyList<Endpoint> Endpoints => _application.Services.GetRequiredService<EndpointDataSource>().Endpoints;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = throwOnBadRequest);

        // The trace identifier differs on every response; without it, a body can be compared whole.
        builder.Services.AddProblemDetails(static options =>
            options.CustomizeProblemDetails = static context => context.ProblemDetails.Extensions.Remove("traceId"));
        builder.Services.AddValueObjectHttpProblemDetails();

        _application = builder.Build();
        _application.UseExceptionHandler();
        Map(_application);
        await _application.StartAsync(TestContext.Current.CancellationToken);
        Client = _application.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private static void Map(WebApplication application)
    {
        var api = application.MapGroup("/api").WithValueObjectProblemDetails();

        // Inferred from the route, and from the query string, required and optional.
        api.MapGet("/accounts/{iban}", static (Iban iban) => iban.Value);
        api.MapGet("/lines", static (Quantity quantity, CountryCode? country) => $"{quantity} {country}");

        // Named by an attribute, and named after the parameter by one.
        api.MapGet("/named/{n}", static ([FromRoute(Name = "n")] Quantity number, [FromQuery(Name = "q")] Quantity amount) => $"{number} {amount}");
        api.MapGet("/unnamed/{number}", static ([FromRoute] Quantity number, [FromQuery] Quantity amount, [FromHeader] CustomerId customer) => $"{number} {amount} {customer}");
        api.MapGet("/customers/current", static ([FromHeader(Name = "X-Customer")] CustomerId customer) => customer.ToString());

        // The members of an [AsParameters] record and of a class with setters.
        api.MapGet("/search", static ([AsParameters] SearchQuery query) => $"{query.Page} {query.Country} {query.Customer}");
        api.MapGet("/filter", static ([AsParameters] SearchFilter filter) => $"{filter.Page} {filter.Country}");

        // Arrays from the query string, and from headers, which are not covered; an array named like a route parameter,
        // which the binder still reads from the query string.
        api.MapGet("/batches", static (Quantity[] quantities, [FromQuery(Name = "c")] CountryCode?[] countries) => $"{quantities.Length} {countries.Length}");
        api.MapGet("/headers", static ([FromHeader(Name = "X-Quantity")] Quantity[] quantities) => quantities.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        api.MapGet("/arrays/{quantities}", static (Quantity[] quantities) => quantities.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // A construction of a generic value object, and value objects written by hand, which nothing registered.
        api.MapGet("/references/{reference}", static (Reference<SalesInvoice> reference) => reference.Value);
        api.MapGet("/counters/{counter}", static (HandWrittenCounter counter) => counter.ToString());
        api.MapGet("/codes/{code}", static (HandWrittenCode code) => code.Value);

        // A body, inferred and explicit, alone and beside a query value, and a form field, which is not covered.
        api.MapPost("/orders", static (ProbeOrder order) => order.Reference.Value);
        api.MapPost("/placements", static (Quantity quantity, ProbeOrder order) => $"{quantity} {order.Reference.Value}");
        api.MapPost("/bodies", static ([FromBody] Iban iban) => iban.Value);
        api.MapPost("/forms", static ([FromForm] Iban iban) => iban.Value).DisableAntiforgery();

        // A value object beside a parameter that is none, and parameters none of which is a value object.
        api.MapGet("/mixed/{id}", static (int id, Quantity quantity) => $"{id} {quantity}");
        api.MapGet("/plain/{n}", static (int n) => n);
        api.MapGet("/raw", static (HttpContext context) => context.Response.WriteAsync("raw"));

        // Covered twice, by the group and by the endpoint.
        api.MapGet("/twice/{iban}", static (Iban iban) => iban.Value).WithValueObjectProblemDetails();

        // As the Request Delegate Generator marks the endpoints it writes, and as other tools mark theirs.
        api.MapGet("/generated", static (Quantity quantity, CountryCode? country, Quantity[] quantities) => $"{quantity} {country} {quantities.Length}")
            .WithMetadata(new GeneratedCodeAttribute(RequestDelegateGenerator, "10.0.12.0"));
        api.MapGet("/tooled", static (Quantity quantity, CountryCode? country) => $"{quantity} {country}")
            .WithMetadata(new GeneratedCodeAttribute(null, null), new GeneratedCodeAttribute("Another.Tool", "1.0"));

        // Outside the group.
        application.MapGet("/outside/{iban}", static (Iban iban) => iban.Value);
    }
}

/// <summary>The application in Production, where the binder answers a refusal itself.</summary>
public sealed class ProductionProblemDetailsApplication() : MinimalApiProblemDetailsApplication(throwOnBadRequest: false);

/// <summary>The application in Development, where the binder throws for a refusal.</summary>
public sealed class DevelopmentProblemDetailsApplication() : MinimalApiProblemDetailsApplication(throwOnBadRequest: true);

/// <summary>A search, bound from the query string and a header through <c>[AsParameters]</c>.</summary>
/// <param name="Page">The page, required, under the name of the member.</param>
/// <param name="Country">The country, optional, under the query name <c>c</c>.</param>
/// <param name="Customer">The customer, from a header.</param>
public sealed record SearchQuery(Quantity Page, [FromQuery(Name = "c")] CountryCode? Country, [FromHeader(Name = "X-Customer")] CustomerId Customer);

/// <summary>A filter, bound through <c>[AsParameters]</c> on the setters of a class.</summary>
public sealed class SearchFilter
{
    /// <summary>Gets or sets the page.</summary>
    public Quantity Page { get; set; }

    /// <summary>Gets or sets the country.</summary>
    public CountryCode? Country { get; set; }
}
