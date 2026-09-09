using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AdCodicem.ValueObjects.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AdCodicem.ValueObjects.IntegrationTests;

/// <summary>
/// Hosts the showcase application against a real PostgreSQL container.
/// </summary>
/// <param name="database">Database container.</param>
public sealed class SampleApiFactory(PostgreSqlFixture database) : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Database:Provider"] = database.ProviderName,
                ["ConnectionStrings:Banking"] = database.ConnectionString,
            }));

        return base.CreateHost(builder);
    }
}

/// <summary>
/// Walks the whole chain a request goes through: binding, validation, persistence, serialization and the
/// published OpenAPI document.
/// </summary>
/// <param name="database">Database container.</param>
public sealed class ApiTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private SampleApiFactory _factory = null!;
    private HttpClient _client = null!;

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        _factory = new SampleApiFactory(database);
        _client = _factory.CreateClient();

        await using var context = database.CreateContext();
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task A_request_body_carries_bare_underlying_values()
    {
        var response = await _client.PostAsync(
            "/customers",
            Json("""{"email":"  Ada@Example.COM ","country":"fr"}"""),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Normalized on the way in, and written back as bare values, not as objects.
        body.Should().Contain("\"email\":\"ada@example.com\"").And.Contain("\"country\":\"FR\"");
        body.Should().NotContain("\"value\"");
    }

    [Fact]
    public async Task A_rejected_body_value_produces_problem_details()
    {
        var response = await _client.PostAsync(
            "/customers",
            Json("""{"email":"not-an-email","country":"FR"}"""),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errors").ToString().Should().Contain("email", Exactly.Once());
    }

    [Fact]
    public async Task A_value_object_binds_from_a_route_segment()
    {
        var created = await CreateCustomerAsync("route.binding@example.com");

        var response = await _client.GetAsync($"/customers/{created.GetProperty("id").GetString()}", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_malformed_route_value_is_rejected_with_the_violated_rule()
    {
        var response = await _client.GetAsync("/customers/not-a-guid", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        // The stable code of the violated rule travels next to the human-readable message.
        problem.GetProperty("errorCodes").GetProperty("id").GetString()
            .Should().Be(ValueObjectErrorCodes.NotParsable);
    }

    [Fact]
    public async Task A_value_object_binds_from_the_query_string()
    {
        await CreateCustomerAsync("query.binding@example.com", "LU");

        var response = await _client.GetAsync("/customers?country=lu", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("query.binding@example.com");
    }

    [Fact]
    public async Task A_query_value_outside_the_closed_set_is_rejected()
    {
        var response = await _client.GetAsync("/customers?country=ZZ", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCodes").GetProperty("country").GetString()
            .Should().Be(ValueObjectErrorCodes.NotAKnownValue);
    }

    [Fact]
    public async Task A_minimal_api_binds_a_value_object_with_no_help_from_the_package()
    {
        // Minimal API parameter binding finds IParsable<T> on its own.
        var response = await _client.GetAsync("/accounts/FR7630006000011234567890189", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_validator_reports_the_rule_the_value_object_owns()
    {
        var response = await _client.PostAsync(
            "/accounts/import",
            Json("""{"iban":"FR7630006000011234567890188"}"""),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errorCodes").GetProperty("Iban").GetString()
            .Should().Be(ValueObjectErrorCodes.InvalidFormat);
    }

    [Fact]
    public async Task The_OpenAPI_document_describes_a_value_object_as_its_underlying_type()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);

        var iban = document.GetProperty("components").GetProperty("schemas").GetProperty("Iban");

        iban.GetProperty("type").GetString().Should().Be("string");
        iban.GetProperty("format").GetString().Should().Be("iban");
        iban.GetProperty("maxLength").GetInt32().Should().Be(34);
        iban.GetProperty("minLength").GetInt32().Should().Be(15);
        iban.GetProperty("pattern").GetString().Should().NotBeNullOrEmpty();
        iban.TryGetProperty("properties", out _).Should().BeFalse("a value object is not an object on the wire");
    }

    [Fact]
    public async Task The_OpenAPI_document_lists_the_accepted_values_of_a_closed_set()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);

        var country = document.GetProperty("components").GetProperty("schemas").GetProperty("CountryCode");

        country.GetProperty("enum").EnumerateArray().Select(value => value.GetString())
            .Should().Equal("FR", "BE", "LU", "DE");
    }

    private async Task<JsonElement> CreateCustomerAsync(string email, string country = "FR")
    {
        var response = await _client.PostAsync(
            "/customers",
            Json($$"""{"email":"{{email}}","country":"{{country}}"}"""),
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
