using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AdCodicem.ValueObjects.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AdCodicem.ValueObjects.CompatTests;

/// <summary>A body holding value objects whose rules the document carries.</summary>
/// <param name="Iban">Lengths and a pattern.</param>
/// <param name="Email">A pattern and a format.</param>
/// <param name="Amount">A typed minimum.</param>
/// <param name="Quantity">Typed bounds.</param>
/// <param name="Born">Typed bounds on a type JSON writes as a string.</param>
/// <param name="Ratio">A real bounded on one side.</param>
/// <param name="Country">A closed set.</param>
/// <param name="Payment">An entity identifier.</param>
/// <param name="Purchase">A construction of a generic value object.</param>
public sealed record OpenAccount(
    Iban Iban,
    EmailAddress Email,
    Amount Amount,
    Quantity Quantity,
    BirthDate Born,
    Ratio Ratio,
    CountryCode Country,
    PaymentId Payment,
    Reference<PurchaseOrder> Purchase);

/// <summary>
/// The schema transformer, compiled against the Microsoft.OpenApi major the current ASP.NET Core takes, inside the
/// document generator of the next one, which takes the next Microsoft.OpenApi major: every member of the object model
/// the transformer writes is reached at least once.
/// </summary>
public sealed class OpenApiTests : IAsyncLifetime
{
    private JsonElement _schemas;
    private JsonElement _withNamedLiterals;

    public async ValueTask InitializeAsync()
    {
        _schemas = await SchemasAsync(static _ => { });
        _withNamedLiterals = await SchemasAsync(static json => json.SerializerOptions.NumberHandling |= JsonNumberHandling.AllowNamedFloatingPointLiterals);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public void The_document_is_built_by_the_next_major_of_Microsoft_OpenApi()
        => typeof(Microsoft.OpenApi.OpenApiDocument).Assembly.GetName().Version!.Major
            .Should().BeGreaterThanOrEqualTo(3, "the package is compiled against Microsoft.OpenApi 2, which .NET 10 takes");

    [Fact]
    public void Lengths_pattern_and_format_reach_the_schema()
    {
        var iban = _schemas.GetProperty(nameof(Iban));

        iban.GetProperty("type").GetString().Should().Be("string");
        iban.GetProperty("minLength").GetInt32().Should().Be(15);
        iban.GetProperty("maxLength").GetInt32().Should().Be(34);
        iban.GetProperty("pattern").GetString().Should().Be("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$");
        iban.GetProperty("format").GetString().Should().Be("iban");
        iban.TryGetProperty("properties", out _).Should().BeFalse("a value object is documented as its underlying value");
    }

    [Fact]
    public void Typed_bounds_on_a_number_become_minimum_and_maximum()
    {
        var quantity = _schemas.GetProperty(nameof(Quantity));

        quantity.GetProperty("minimum").GetInt32().Should().Be(1);
        quantity.GetProperty("maximum").GetInt32().Should().Be(100);
        _schemas.GetProperty(nameof(Amount)).GetProperty("minimum").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public void Typed_bounds_on_a_string_become_extensions_and_a_sentence()
    {
        var born = _schemas.GetProperty(nameof(BirthDate));

        born.GetProperty("type").GetString().Should().Be("string");
        born.GetProperty("x-minimum").GetString().Should().Be("1900-01-01");
        born.GetProperty("x-maximum").GetString().Should().Be("2100-12-31");
        born.GetProperty("description").GetString().Should().Contain("Between 1900-01-01 and 2100-12-31, inclusive.");
    }

    [Fact]
    public void A_closed_set_becomes_an_enum()
        => _schemas.GetProperty(nameof(CountryCode)).GetProperty("enum").EnumerateArray().Select(static value => value.GetString())
            .Should().Equal("FR", "BE", "LU");

    [Fact]
    public void An_entity_identifier_publishes_its_format_and_an_example()
    {
        var payment = _schemas.GetProperty(nameof(PaymentId));

        payment.GetProperty("pattern").GetString().Should().Be("^pay_[0123456789abcdefghjkmnpqrstvwxyz]{21}$");
        payment.GetProperty("minLength").GetInt32().Should().Be(25);
        payment.GetProperty("maxLength").GetInt32().Should().Be(25);
        payment.GetProperty("examples")[0].GetString().Should().StartWith("pay_");
    }

    [Fact]
    public void A_construction_of_a_generic_value_object_is_documented_as_its_underlying_value()
    {
        var purchase = _schemas.GetProperty("ReferenceOfPurchaseOrder");

        purchase.GetProperty("type").GetString().Should().Be("string");
        purchase.GetProperty("maxLength").GetInt32().Should().Be(12);
    }

    [Fact]
    public void A_named_literal_a_real_accepts_becomes_an_alternative()
    {
        var anyOf = _withNamedLiterals.GetProperty(nameof(Ratio)).GetProperty("anyOf");

        anyOf[0].GetProperty("minimum").GetInt32().Should().Be(0);
        anyOf[1].GetProperty("enum").EnumerateArray().Select(static value => value.GetString()).Should().Equal("Infinity");
    }

    private static async Task<JsonElement> SchemasAsync(Action<JsonOptions> configureJson)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(configureJson);
        builder.Services.AddOpenApi(static options => options.AddValueObjects());

        await using var application = builder.Build();
        application.MapPost("/accounts", static (OpenAccount body) => Results.Ok(body));
        application.MapOpenApi();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);

        return document.GetProperty("components").GetProperty("schemas").Clone();
    }
}
