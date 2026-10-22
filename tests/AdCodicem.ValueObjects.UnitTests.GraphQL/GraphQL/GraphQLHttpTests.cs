using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using AdCodicem.ValueObjects.HotChocolate;
using AdCodicem.ValueObjects.UnitTests.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AdCodicem.ValueObjects.UnitTests.GraphQL;

/// <summary>
/// A refused value over HTTP, through HotChocolate's ASP.NET Core endpoint: the status it answers with, and the message
/// in the language <c>UseRequestLocalization()</c> reads from the request.
/// </summary>
public sealed class GraphQLHttpTests : IAsyncLifetime
{
    private const string GraphQLResponse = "application/graphql-response+json";
    private const string Json = "application/json";
    private const string Valid = "FR7630006000011234567890189";

    private WebApplication _application = null!;
    private HttpClient _client = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddLocalization();
        builder.Services.AddGraphQLServer()
            .AddQueryType<ShopQuery>()
            .AddValueObjects(static options => options.UseResource<ValidationMessages>());

        _application = builder.Build();
        _application.UseRequestLocalization(static options => options
            .AddSupportedCultures("en-US", "fr-FR")
            .AddSupportedUICultures("en-US", "fr-FR")
            .SetDefaultCulture("en-US"));
        _application.MapGraphQL();
        await _application.StartAsync(TestContext.Current.CancellationToken);
        _client = _application.GetTestClient();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
    }

    /// <summary>
    /// A refused argument is a field error: 200, with <c>data</c> <see langword="null"/> when the field cannot be, and the
    /// field <see langword="null"/> when it can.
    /// </summary>
    [Theory]
    [InlineData(GraphQLResponse)]
    [InlineData(Json)]
    public async Task A_refused_argument_answers_200_with_the_field_error(string accept)
    {
        var (status, body) = await PostAsync("""{ account(iban: "XX", qty: 3) { iban } }""", accept: accept);
        status.Should().Be(HttpStatusCode.OK);
        body["data"].Should().BeNull();
        body.Error().Code().Should().Be(ValueObjectErrorCodes.TooShort);

        var (nullableStatus, nullable) = await PostAsync("""{ lookup(iban: "XX") }""", accept: accept);
        nullableStatus.Should().Be(HttpStatusCode.OK);
        nullable["data"]!.ToJsonString().Should().Be("""{"lookup":null}""");
        nullable.Error().Code().Should().Be(ValueObjectErrorCodes.TooShort);
    }

    /// <summary>
    /// A refused variable is a request error: 400 under the GraphQL response media type, and 200 under plain JSON.
    /// </summary>
    [Theory]
    [InlineData(GraphQLResponse, HttpStatusCode.BadRequest)]
    [InlineData(Json, HttpStatusCode.OK)]
    public async Task A_refused_variable_answers_with_the_status_of_a_request_error(string accept, HttpStatusCode expected)
    {
        var (status, body) = await PostAsync("query($i: Iban!) { lookup(iban: $i) }", """{"i":"XX"}""", accept);

        status.Should().Be(expected);
        body.ContainsKey("data").Should().BeFalse();
        body.Error().Code().Should().Be(ValueObjectErrorCodes.TooShort);
    }

    /// <summary>
    /// The message is the application's resource's in the culture of the request, which <c>UseRequestLocalization()</c>
    /// reads from it, and the rule's in a culture the resource does not hold.
    /// </summary>
    [Fact]
    public async Task A_refusal_is_answered_in_the_language_of_the_request()
    {
        (await PostAsync($$"""{ account(iban: "{{Valid}}", qty: 5000) { iban } }""", language: "fr-FR")).Body.Error().Message()
            .Should().Be("« qty » doit être compris entre 0 et 1000.");
        (await PostAsync($$"""query($q: Quantity!) { account(iban: "{{Valid}}", qty: $q) { iban } }""", """{"q":5000}""", language: "fr-FR"))
            .Body.Error().Message().Should().Be("« q » doit être compris entre 0 et 1000.");
        (await PostAsync("""{ account(iban: "XX", qty: 3) { iban } }""", language: "en-US")).Body.Error().Message()
            .Should().Be("The value must be at least 15 characters long.");
    }

    private async Task<(HttpStatusCode Status, JsonObject Body)> PostAsync(
        string document,
        string? variables = null,
        string accept = GraphQLResponse,
        string language = "en-US")
    {
        var payload = new JsonObject { ["query"] = document };
        if (variables is not null)
        {
            payload["variables"] = JsonNode.Parse(variables);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, Json),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(language));

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (response.StatusCode, JsonNode.Parse(body)!.AsObject());
    }
}
