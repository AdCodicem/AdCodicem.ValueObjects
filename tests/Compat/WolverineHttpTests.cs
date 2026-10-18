using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AdCodicem.ValueObjects.AspNetCore;
using AdCodicem.ValueObjects.Wolverine.Http;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.Http;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>
/// AdCodicem.ValueObjects.Wolverine.Http on the next major of ASP.NET Core, through the code Wolverine's dynamic mode
/// writes for each endpoint and compiles at startup: a route, query, header or form value read through its rules, and a
/// refused or absent one answered 400 with its code, whatever <c>RejectUnparseableQueryValues</c> says.
/// </summary>
public sealed class WolverineHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Each_value_object_is_read_through_its_rules_and_a_refused_one_answered_with_its_code(bool strict)
    {
        await using var application = await StartAsync(strict);
        using var client = application.GetTestClient();

        (await client.GetStringAsync("/wolverine/lines/7?country=fr&quantities=1&quantities=2", TestContext.Current.CancellationToken))
            .Should().Be("line 7 FR [1,2]");
        (await CodesAsync(await client.GetAsync("/wolverine/lines/500?quantities=0", TestContext.Current.CancellationToken)))
            .Should().Equal(new Dictionary<string, string>
            {
                ["quantity"] = ValueObjectErrorCodes.OutOfRange,
                ["quantities"] = ValueObjectErrorCodes.OutOfRange,
            });
        (await CodesAsync(await client.GetAsync("/wolverine/lines/7?country=ZZ", TestContext.Current.CancellationToken)))
            .Should().Equal(new Dictionary<string, string> { ["country"] = ValueObjectErrorCodes.NotAKnownValue });

        using var absent = new HttpRequestMessage(HttpMethod.Get, "/wolverine/customers/current");
        (await CodesAsync(await client.SendAsync(absent, TestContext.Current.CancellationToken)))
            .Should().Equal(new Dictionary<string, string> { ["X-Customer"] = ValueObjectErrorCodes.Required });

        using var form = new FormUrlEncodedContent([new KeyValuePair<string, string>("quantity", "0")]);
        (await CodesAsync(await client.PostAsync("/wolverine/forms", form, TestContext.Current.CancellationToken)))
            .Should().Equal(new Dictionary<string, string> { ["quantity"] = ValueObjectErrorCodes.OutOfRange });
    }

    private static async Task<WebApplication> StartAsync(bool strict)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Host.UseWolverine(static options =>
        {
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Dynamic;
            options.Discovery.DisableConventionalDiscovery();
            options.ApplicationAssembly = typeof(WolverineHttpTests).Assembly;
        });
        builder.Services.AddWolverineHttp();
        builder.Services.AddProblemDetails();

        var application = builder.Build();
        application.MapWolverineEndpoints(options =>
        {
            options.CustomizeHttpEndpointDiscovery(static query =>
                query.Excludes.WithCondition("not the island's Wolverine endpoints", static type => type != typeof(IslandWolverineEndpoints)));
            options.RejectUnparseableQueryValues = strict;
            options.UseValueObjects();
        });
        await application.StartAsync(TestContext.Current.CancellationToken);

        return application;
    }

    private static async Task<Dictionary<string, string>> CodesAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

            return problem.GetProperty(ValueObjectProblemDetails.ExtensionName).Deserialize<Dictionary<string, string>>()!;
        }
    }
}

/// <summary>The Wolverine.Http endpoints of the island, over its domain's value objects.</summary>
public static class IslandWolverineEndpoints
{
    [WolverineGet("/wolverine/lines/{quantity}")]
    public static string GetLine(Quantity quantity, CountryCode? country, List<Quantity> quantities)
        => $"line {quantity} {country} [{string.Join(",", quantities)}]";

    [WolverineGet("/wolverine/customers/current")]
    public static string GetCustomer([FromHeader(Name = "X-Customer")] CustomerId customer) => $"customer {customer}";

    [WolverinePost("/wolverine/forms")]
    public static string PostForm([FromForm] Quantity quantity) => $"form {quantity}";
}
