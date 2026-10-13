using System.CodeDom.Compiler;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;

namespace AdCodicem.ValueObjects.RdgTests;

/// <summary>
/// Minimal API endpoints whose parameters, members gathered with <c>[AsParameters]</c> and body members hold the text of
/// this project's value objects with <see cref="ParsesAsAttribute"/>, and a value object <see cref="NotDefaultAttribute"/>
/// refuses at its default, validated by <c>AddValidation()</c> over the binding the Request Delegate Generator writes.
/// </summary>
public static class AnnotationEndpoints
{
    /// <summary>Maps the endpoints.</summary>
    /// <param name="routes">Where they are mapped.</param>
    public static void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet(
            "/annotations/query",
            static string ([ParsesAs(typeof(Sku))] string? skuText, [ParsesAs<Quantity, int>] string? quantityText) => skuText ?? "(none)");
        routes.MapGet("/annotations/criteria", static string ([AsParameters] TextCriteria criteria) => criteria.Item ?? "(none)");
        routes.MapPost("/annotations/placements", static string (Placement placement) => placement.Sku.Value);
    }
}

/// <summary>
/// Criteria gathered with <c>[AsParameters]</c>, as a class: the Request Delegate Generator of 10.0.12 does not compile a
/// positional record of <c>string?</c> there (CS8639).
/// </summary>
public sealed class TextCriteria
{
    [FromQuery(Name = "item")]
    [ParsesAs(typeof(Sku))]
    public string? Item { get; set; }

    [FromHeader(Name = "X-Units")]
    [ParsesAs<Quantity, int>]
    public string? Units { get; set; }
}

/// <summary>A body holding a value object that must be set and the text of another.</summary>
public sealed class Placement
{
    [NotDefault]
    public Sku Sku { get; set; }

    [ParsesAs(typeof(Sku))]
    public string? Alternate { get; set; }
}

/// <summary>The application mapping <see cref="AnnotationEndpoints"/> under <c>AddValidation()</c>.</summary>
public sealed class AnnotationApplication : IAsyncLifetime
{
    private WebApplication _application = null!;

    /// <summary>Gets a client of the in-memory server.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Gets the endpoints the application maps.</summary>
    public IReadOnlyList<Endpoint> Endpoints => _application.Services.GetRequiredService<EndpointDataSource>().Endpoints;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddValidation();

        _application = builder.Build();
        AnnotationEndpoints.Map(_application);
        await _application.StartAsync(TestContext.Current.CancellationToken);
        Client = _application.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _application.DisposeAsync();
    }
}

/// <summary>
/// The attributes over the binding the Request Delegate Generator writes: each refusal answered by
/// <c>AddValidation()</c> with the message of its rule, under the name the parameter or the member binds under, as the
/// reflection-based binding answers it.
/// </summary>
public sealed class AnnotationTests(AnnotationApplication application) : IClassFixture<AnnotationApplication>
{
    [Fact]
    public void Every_endpoint_is_written_by_the_Request_Delegate_Generator()
    {
        var endpoints = application.Endpoints.OfType<RouteEndpoint>().ToList();

        endpoints.Should().HaveCount(3);
        endpoints.Should().AllSatisfy(static endpoint => endpoint.Metadata.OfType<GeneratedCodeAttribute>()
            .Should().Contain(static attribute => attribute.Tool!.StartsWith("Microsoft.AspNetCore.Http.RequestDelegateGenerator,", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Text_query_parameters_are_refused_under_their_names()
    {
        var errors = await ErrorsAsync(await application.Client.GetAsync("/annotations/query?skuText=AB&quantityText=500", TestContext.Current.CancellationToken));

        errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["skuText"] = ["A SKU holds a dash."],
            ["quantityText"] = ["The value must be less than or equal to 100."],
        });
    }

    [Fact]
    public async Task Text_the_rules_accept_reaches_the_handler_as_it_was_sent()
    {
        var accepted = await application.Client.GetStringAsync("/annotations/query?skuText=AB-1&quantityText=5", TestContext.Current.CancellationToken);

        accepted.Should().Be("AB-1");
    }

    [Fact]
    public async Task Members_gathered_with_AsParameters_are_refused_under_their_names()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/annotations/criteria?item=AB");
        request.Headers.Add("X-Units", "0");

        var errors = await ErrorsAsync(await application.Client.SendAsync(request, TestContext.Current.CancellationToken));

        errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Item"] = ["A SKU holds a dash."],
            ["Units"] = ["The value must be greater than or equal to 1."],
        });
    }

    [Fact]
    public async Task A_body_member_left_at_its_default_and_refused_text_are_answered_with_their_rules()
    {
        var errors = await ErrorsAsync(await application.Client.PostAsJsonAsync(
            "/annotations/placements",
            new { alternate = "AB" },
            TestContext.Current.CancellationToken));

        errors.Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            ["Sku"] = ["The Sku field is required."],
            ["Alternate"] = ["A SKU holds a dash."],
        });
    }

    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest, text);

            return JsonDocument.Parse(text).RootElement.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
        }
    }
}
