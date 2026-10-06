using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// MVC reading request bodies with Newtonsoft.Json, through AdCodicem.ValueObjects.AspNetCore.NewtonsoftJson, on the
/// next major of ASP.NET Core and of Microsoft.AspNetCore.Mvc.NewtonsoftJson: the formatter, built against the current
/// major and deriving from the framework's, reads the body as the framework's of this major does, and records the code
/// of a refused value under the key of its error.
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

    private async Task<HttpResponseMessage> PostOrderAsync(string email)
    {
        using var body = new StringContent(
            $$"""{"customer":"0193b1c0-0000-7000-8000-000000000001","email":"{{email}}","amount":1,"country":"fr","payment":"{{PaymentId.New()}}","purchase":"po-1"}""",
            System.Text.Encoding.UTF8,
            "application/json");

        return await _client.PostAsync("/customers/orders", body, TestContext.Current.CancellationToken);
    }
}
