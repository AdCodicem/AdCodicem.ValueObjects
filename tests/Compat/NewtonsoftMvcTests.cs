using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>A controller applying JSON Patch documents through the value objects' rules.</summary>
[ApiController]
[Route("patches")]
public sealed class PatchesController : ControllerBase
{
    [HttpPatch]
    public IActionResult Patch([FromBody] JsonPatchDocument<PatchedAccount> patch)
    {
        var account = PatchedAccount.Fresh();

        return patch.TryApplyTo(account, ModelState, HttpContext)
            ? Ok(account.Summary())
            : ValidationProblem(extensions: new Dictionary<string, object?>
            {
                [ValueObjectProblemDetails.ExtensionName] = ValueObjectProblemDetails.GetErrorCodes(HttpContext),
            });
    }
}

/// <summary>An account a JSON Patch document patches.</summary>
public sealed class PatchedAccount
{
    public Iban Iban { get; set; }

    public Iban? Backup { get; set; }

    public Quantity Quantity { get; set; }

    public Amount Balance { get; set; }

    public CountryCode Country { get; set; }

    public List<Iban> Ibans { get; set; } = [];

    public Dictionary<CountryCode, Quantity> PerCountry { get; set; } = [];

    public Reference<PurchaseOrder> Purchase { get; set; }

    public static PatchedAccount Fresh() => new()
    {
        Iban = Iban.Create("FR7630006000011234567890189"),
        Quantity = Quantity.Create(3),
        Balance = Amount.Create(12.5m),
        Country = CountryCode.France,
        Ibans = [Iban.Create("GB29NWBK60161331926819")],
        PerCountry = new() { [CountryCode.France] = Quantity.Create(1) },
        Purchase = Reference<PurchaseOrder>.Create("PO-1"),
    };

    public Dictionary<string, string?> Summary() => new()
    {
        ["iban"] = Iban.Value,
        ["backup"] = Backup?.Value,
        ["balance"] = Balance.Value.ToString(CultureInfo.InvariantCulture),
        ["ibans"] = string.Join(",", Ibans.Select(static iban => iban.Value)),
        ["perCountry"] = string.Join(",", PerCountry.Select(static entry => $"{entry.Key.Value}={entry.Value.Value}")),
    };
}

/// <summary>
/// MVC reading request bodies with Newtonsoft.Json, through AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson, on the
/// next major of ASP.NET Core and of Microsoft.AspNetCore.Mvc.NewtonsoftJson: the formatter, built against the current
/// major and deriving from the framework's, reads the body as the framework's of this major does, and records the code
/// of a refused value under the key of its error; and JSON Patch documents applied through <c>TryApplyTo</c>, whose
/// adapters, built against the current major, derive from the JSON Patch of this major.
/// </summary>
public sealed class NewtonsoftMvcTests : IAsyncLifetime
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
            .AddNewtonsoftJson()
            .AddValueObjectsNewtonsoftJson();
        builder.Services.Configure<ApiBehaviorOptions>(static options => options.AddValueObjectProblemDetails());

        _application = builder.Build();
        _application.MapControllers();
        await _application.StartAsync(TestContext.Current.CancellationToken);
        _client = _application.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    [Fact]
    public async Task A_request_body_carries_bare_values()
    {
        using var response = await PostOrderAsync(" Ada@Example.com ");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("ada@example.com");
    }

    [Fact]
    public async Task A_value_a_request_body_refuses_is_answered_with_the_code_under_the_key_of_its_error()
    {
        using var response = await PostOrderAsync("not an address");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty(ValueObjectProblemDetails.ExtensionName).GetProperty("email").GetString()
            .Should().Be(ValueObjectErrorCodes.InvalidFormat);
        problem.GetProperty("errors").GetProperty("email")[0].GetString()
            .Should().Be("The value is not a valid EmailAddress: The value does not match the expected format.");
    }

    [Fact]
    public async Task A_value_the_query_string_refuses_keeps_its_code()
    {
        using var response = await _client.GetAsync("/customers?country=ZZ", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty(ValueObjectProblemDetails.ExtensionName).GetProperty("country").GetString()
            .Should().Be(ValueObjectErrorCodes.NotAKnownValue);
    }

    /// <summary>
    /// A refused JSON Patch document is answered with each error under the path of its operation and the code of the
    /// rule a value broke, a removal of a value object that cannot be null included.
    /// </summary>
    [Fact]
    public async Task A_refused_patch_document_is_answered_under_the_paths_of_its_operations_with_their_codes()
    {
        using var response = await PatchAsync(
            """
            [{"op":"replace","path":"/quantity","value":101},{"op":"replace","path":"/iban","value":"XX"},
             {"op":"add","path":"/perCountry/XX","value":3},{"op":"remove","path":"/purchase"}]
            """);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty(ValueObjectProblemDetails.ExtensionName).Deserialize<Dictionary<string, string>>().Should().Equal(new Dictionary<string, string>
        {
            ["/quantity"] = ValueObjectErrorCodes.OutOfRange,
            ["/iban"] = ValueObjectErrorCodes.TooShort,
            ["/perCountry/XX"] = ValueObjectErrorCodes.NotAKnownValue,
            ["/purchase"] = ValueObjectErrorCodes.Required,
        });
        problem.GetProperty("errors").GetProperty("/purchase")[0].GetString().Should().Be("The value cannot be removed: a valid Reference`1 is required.");
    }

    /// <summary>
    /// An accepted JSON Patch document keeps the values the client sent, normalized, the digits of a decimal included,
    /// and <c>move</c> and <c>copy</c> take value objects as the instances they are.
    /// </summary>
    [Fact]
    public async Task An_accepted_patch_document_keeps_the_values_the_client_sent()
    {
        using var response = await PatchAsync(
            """
            [{"op":"replace","path":"/iban","value":"de89 3704 0044 0532 0130 00"},{"op":"replace","path":"/balance","value":1234567890123456789.12},
             {"op":"add","path":"/perCountry/be","value":7},{"op":"move","from":"/ibans/0","path":"/backup"},
             {"op":"copy","from":"/iban","path":"/ibans/-"}]
            """);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<Dictionary<string, string?>>(TestContext.Current.CancellationToken)).Should().Equal(new Dictionary<string, string?>
        {
            ["iban"] = "DE89370400440532013000",
            ["backup"] = "GB29NWBK60161331926819",
            ["balance"] = "1234567890123456789.12",
            ["ibans"] = "DE89370400440532013000",
            ["perCountry"] = "FR=1,BE=7",
        });
    }

    private async Task<HttpResponseMessage> PatchAsync(string json)
    {
        using var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json-patch+json");

        return await _client.PatchAsync("/patches", body, TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> PostOrderAsync(string email)
    {
        using var body = new StringContent(
            $$"""{"customer":"0193b1c0-0000-7000-8000-000000000001","email":"{{email}}","amount":1,"country":"fr","payment":"{{PaymentId.New()}}","purchase":"po-1"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        return await _client.PostAsync("/customers/orders", body, TestContext.Current.CancellationToken);
    }
}
