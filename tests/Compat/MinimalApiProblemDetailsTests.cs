using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// The minimal API problem details of AdCodicem.ValueObjects.AspNetCore.Http on the next major of ASP.NET Core: the
/// endpoint filter, which reads the binder's description of each parameter and the status it left, and the exception
/// handler, which finds the endpoint in the feature the exception handler middleware hands it.
/// </summary>
public sealed class MinimalApiProblemDetailsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_refused_route_or_query_value_is_answered_with_the_code_of_its_rule(bool throwOnBadRequest)
    {
        await using var application = await StartAsync(throwOnBadRequest);
        using var client = application.GetTestClient();

        var route = await ProblemAsync(await client.GetAsync("/api/accounts/XX", TestContext.Current.CancellationToken));
        var query = await ProblemAsync(await client.GetAsync("/api/lines?quantity=500&country=ZZ", TestContext.Current.CancellationToken));

        route.GetProperty(ValueObjectProblemDetails.ExtensionName).GetProperty("iban").GetString().Should().Be(ValueObjectErrorCodes.TooShort);
        route.GetProperty("errors").GetProperty("iban").GetArrayLength().Should().Be(1);
        query.GetProperty(ValueObjectProblemDetails.ExtensionName).Deserialize<Dictionary<string, string>>().Should().Equal(
            new Dictionary<string, string>
            {
                ["quantity"] = ValueObjectErrorCodes.OutOfRange,
                ["country"] = ValueObjectErrorCodes.NotAKnownValue,
            });
    }

    [Fact]
    public async Task A_refused_body_is_answered_with_the_code_under_its_JSON_path_when_the_binder_throws()
    {
        await using var application = await StartAsync(throwOnBadRequest: true);
        using var client = application.GetTestClient();

        using var response = await client.PostAsJsonAsync("/api/orders", new { quantity = 0 }, TestContext.Current.CancellationToken);

        (await ProblemAsync(response)).GetProperty(ValueObjectProblemDetails.ExtensionName).GetProperty("$.quantity").GetString()
            .Should().Be(ValueObjectErrorCodes.OutOfRange);
    }

    private static async Task<WebApplication> StartAsync(bool throwOnBadRequest)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = throwOnBadRequest);
        builder.Services.AddProblemDetails();
        builder.Services.AddValueObjectHttpProblemDetails();

        var application = builder.Build();
        application.UseExceptionHandler();
        var api = application.MapGroup("/api").WithValueObjectProblemDetails();
        api.MapGet("/accounts/{iban}", static (Iban iban) => iban.Value);
        api.MapGet("/lines", static (Quantity quantity, CountryCode? country) => $"{quantity} {country}");
        api.MapPost("/orders", static (Line line) => line.Quantity.Value);
        await application.StartAsync(TestContext.Current.CancellationToken);

        return application;
    }

    private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

            return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        }
    }
}

/// <summary>A line of an order, read from a JSON body.</summary>
/// <param name="Quantity">How many.</param>
public sealed record Line(Quantity Quantity);
