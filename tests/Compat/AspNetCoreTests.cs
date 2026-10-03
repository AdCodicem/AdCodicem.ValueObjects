using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>A controller binding value objects from the route, the query string and the body.</summary>
[ApiController]
[Route("customers")]
public sealed class CustomersController : ControllerBase
{
    [HttpGet("{id}")]
    public IActionResult Get(CustomerId id) => Ok(id.ToString());

    [HttpGet]
    public IActionResult List([FromQuery] CountryCode? country) => Ok(country?.Value ?? "none");

    [HttpPost("orders")]
    public IActionResult Place([FromBody] OrderPlaced order) => Ok(order?.Email.Value);
}

/// <summary>
/// MVC model binding through the AspNetCore package, and minimal API binding through the IParsable the generator emits,
/// on the next major of ASP.NET Core.
/// </summary>
public sealed class AspNetCoreTests : IAsyncLifetime
{
    private WebApplication _application = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers()
            .ConfigureApplicationPartManager(static manager =>
            {
                manager.ApplicationParts.Clear();
                manager.ApplicationParts.Add(new AssemblyPart(typeof(CustomersController).Assembly));
            })
            .AddValueObjects();
        builder.Services.Configure<ApiBehaviorOptions>(static options => options.AddValueObjectProblemDetails());

        _application = builder.Build();
        _application.MapControllers();
        _application.MapGet("/minimal/{iban}", static (Iban iban) => Results.Ok(iban.Value));
        await _application.StartAsync(TestContext.Current.CancellationToken);
        _client = _application.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    [Fact]
    public async Task A_value_object_binds_from_a_route_segment()
        => (await _client.GetStringAsync("/customers/0193b1c0-0000-7000-8000-000000000001", TestContext.Current.CancellationToken))
            .Should().Be("0193b1c0-0000-7000-8000-000000000001");

    [Fact]
    public async Task A_value_object_binds_from_the_query_string_normalized()
        => (await _client.GetStringAsync("/customers?country=%20lu", TestContext.Current.CancellationToken)).Should().Be("LU");

    [Fact]
    public async Task A_rejected_value_is_answered_with_the_code_of_the_rule_it_breaks()
    {
        using var response = await _client.GetAsync("/customers?country=ZZ", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty(ValueObjectProblemDetails.ExtensionName).GetProperty("country").GetString()
            .Should().Be(ValueObjectErrorCodes.NotAKnownValue);
    }

    [Fact]
    public async Task A_request_body_carries_bare_values()
    {
        using var body = new StringContent(
            $$"""{"customer":"0193b1c0-0000-7000-8000-000000000001","email":" Ada@Example.com ","amount":1,"country":"fr","payment":"{{PaymentId.New()}}","purchase":"po-1"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        using var response = await _client.PostAsync("/customers/orders", body, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("ada@example.com");
    }

    [Fact]
    public async Task A_minimal_API_binds_a_value_object_with_nothing_registered()
        => (await _client.GetStringAsync("/minimal/fr7630006000011234567890189", TestContext.Current.CancellationToken))
            .Should().Be("\"FR7630006000011234567890189\"");
}
